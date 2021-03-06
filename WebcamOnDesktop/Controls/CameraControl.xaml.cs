using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using WebcamOnDesktop.Helpers;
using WebcamOnDesktop.Views;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace WebcamOnDesktop.Controls
{
    public sealed partial class CameraControl
    {

        public static readonly DependencyProperty CanSwitchProperty =
            DependencyProperty.Register("CanSwitch", typeof(bool), typeof(CameraControl), new PropertyMetadata(false));

        public static readonly DependencyProperty PanelProperty =
            DependencyProperty.Register("Panel", typeof(Panel), typeof(CameraControl), new PropertyMetadata(Panel.Front, OnPanelChanged));

        public static readonly DependencyProperty IsInitializedProperty =
            DependencyProperty.Register("IsInitialized", typeof(bool), typeof(CameraControl), new PropertyMetadata(false));

        public static readonly DependencyProperty CameraButtonStyleProperty =
            DependencyProperty.Register("CameraButtonStyle", typeof(Style), typeof(CameraControl), new PropertyMetadata(null));

        public static readonly DependencyProperty SwitchCameraButtonStyleProperty =
            DependencyProperty.Register("SwitchCameraButtonStyle", typeof(Style), typeof(CameraControl), new PropertyMetadata(null));

        public static readonly DependencyProperty CameraSelectedProperty =
            DependencyProperty.Register("CameraSelected", typeof(string), typeof(CameraControl), new PropertyMetadata(null));
        // Rotation metadata to apply to the preview stream and recorded videos (MF_MT_VIDEO_ROTATION)
        // Reference:https://docs.microsoft.com/windows/uwp/audio-video-camera/handle-device-orientation-with-mediacapture
        private readonly Guid _rotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");
        private readonly DisplayInformation _displayInformation = DisplayInformation.GetForCurrentView();
        private readonly SimpleOrientationSensor _orientationSensor = SimpleOrientationSensor.GetDefault();
        private MediaCapture mediaCapture;
        private bool isPreviewing;
        private bool mirroringPreview;
        private SimpleOrientation deviceOrientation = SimpleOrientation.NotRotated;
        private DisplayOrientations displayOrientation = DisplayOrientations.Portrait;
        private DeviceInformationCollection _cameraDevices;
        private bool capturing;


        /// <summary>
        /// Cache of properties from the current MediaCapture device which is used for capturing the preview frame.
        /// </summary>
        private VideoEncodingProperties videoProperties;

        /// <summary>
        /// References a FaceTracker instance.
        /// </summary>
        private FaceTracker faceTracker;

        /// <summary>
        /// A periodic timer to execute FaceTracker on preview frames
        /// </summary>
        private ThreadPoolTimer frameProcessingTimer;

        /// <summary>
        /// Flag to ensure FaceTracking logic only executes one at a time
        /// </summary>
        private int busy = 0;


        public bool CanSwitch
        {
            get { return (bool)GetValue(CanSwitchProperty); }
            set { SetValue(CanSwitchProperty, value); }
        }

        public Panel Panel
        {
            get { return (Panel)GetValue(PanelProperty); }
            set { SetValue(PanelProperty, value); }
        }

        public bool IsInitialized
        {
            get { return (bool)GetValue(IsInitializedProperty); }
            private set { SetValue(IsInitializedProperty, value); }
        }

        public Style CameraButtonStyle
        {
            get { return (Style)GetValue(CameraButtonStyleProperty); }
            set { SetValue(CameraButtonStyleProperty, value); }
        }

        public Style SwitchCameraButtonStyle
        {
            get { return (Style)GetValue(SwitchCameraButtonStyleProperty); }
            set { SetValue(SwitchCameraButtonStyleProperty, value); }
        }

        public string CameraSelected
        {
            get { return (string)GetValue(CameraSelectedProperty); }
            set { SetValue(CameraSelectedProperty, value); }
        }


        public Size PreviewSize
        {
            get { return new Size(PreviewControl.ActualWidth, PreviewControl.ActualHeight); }
        }

        public CameraControl()
        {
            InitializeComponent();

        }


        /// <summary>
        /// Creates the FaceTracker object which we will use for face detection and tracking.
        /// Initializes a new MediaCapture instance and starts the Preview streaming to the CamPreview UI element.
        /// </summary>
        /// <returns>Async Task object returning true if initialization and streaming were successful and false if an exception occurred.</returns>
        public async Task<bool> InitializeCameraAsync()
        {
            bool successful = false;

            faceTracker = await FaceTracker.CreateAsync();

            try
            {
                if (mediaCapture == null)
                {
                    mediaCapture = new MediaCapture();
                    mediaCapture.Failed += MediaCapture_Failed;

                    _cameraDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                    if (_cameraDevices == null || !_cameraDevices.Any())
                    {
                        throw new NotSupportedException();
                    }

                    string cameraSelectedId = null;
                    for (int i = 0; i < _cameraDevices.Count; i++)
                    {
                        if (_cameraDevices[i].Name == CameraSelected)
                        {
                            cameraSelectedId = _cameraDevices[i]?.Id;
                            break;
                        }

                    }

                    var device = _cameraDevices.FirstOrDefault(camera => camera.EnclosureLocation?.Panel == Panel);

                    //var cameraId = device?.Id ?? _cameraDevices.First().Id;
                    var cameraId = cameraSelectedId ?? device?.Id;

                    //limit request to "Video", to avoid to require permissions for "Audio" (which we don't use anyway)
                    await mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
                    {
                        VideoDeviceId = cameraId,
                        StreamingCaptureMode = StreamingCaptureMode.Video
                    });

                    if (Panel == Panel.Back)
                    {
                        mirroringPreview = false;
                    }
                    else
                    {
                        mirroringPreview = true;
                    }

                    // TODO: enable this to mirror
                    //mirroringPreview = false;

                    IsInitialized = true;
                    CanSwitch = _cameraDevices?.Count > 1;
                    RegisterOrientationEventHandlers();
                    await StartPreviewAsync();

                    // Run the timer at 66ms, which is approximately 15 frames per second.
                    TimeSpan timerInterval = TimeSpan.FromMilliseconds(66);
                    this.frameProcessingTimer = ThreadPoolTimer.CreatePeriodicTimer(ProcessCurrentVideoFrame, timerInterval);

                    videoProperties = (VideoEncodingProperties)mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);

                    // TODO: export this information to provide correct aspectratio
                    //double cameraWidth = videoProps.Width;
                    //double cameraHeight = videoProps.Height;

                    //double previewOutputWidth = PreviewControl.ActualWidth;
                    //double previewOutputHeight = PreviewControl.ActualHeight;

                    //double cameraRatio = cameraWidth / cameraHeight;
                    //double previewOutputRatio = previewOutputWidth / previewOutputHeight;

                    //double actualWidth = (cameraRatio <= previewOutputRatio) ?
                    //    previewOutputHeight * cameraRatio
                    //    : previewOutputWidth;
                    //double actualHeight = (cameraRatio <= previewOutputRatio) ?
                    //    previewOutputHeight
                    //    : previewOutputWidth / cameraRatio;
                }
                successful = true;
            }
            catch (UnauthorizedAccessException)
            {
                errorMessage.Text = "Camera_Exception_UnauthorizedAccess".GetLocalized();
            }
            catch (NotSupportedException)
            {
                errorMessage.Text = "Camera_Exception_NotSupported".GetLocalized();
            }
            catch (TaskCanceledException)
            {
                errorMessage.Text = "Camera_Exception_InitializationCanceled".GetLocalized();
            }
            catch (Exception)
            {
                errorMessage.Text = "Camera_Exception_InitializationError".GetLocalized();
            }
            return successful;
        }


        /// <summary>
        /// This method is invoked by a ThreadPoolTimer to execute the FaceTracker and Visualization logic.
        /// </summary>
        /// <param name="timer">Timer object invoking this call</param>
        private async void ProcessCurrentVideoFrame(ThreadPoolTimer timer)
        {

            // If busy is already 1, then the previous frame is still being processed,
            // in which case we skip the current frame.
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
            {
                return;
            }

            await ProcessCurrentVideoFrameAsync();
            Interlocked.Exchange(ref busy, 0);
        }

        /// <summary>
        /// This method is called to execute the FaceTracker and Visualization logic at each timer tick.
        /// </summary>
        /// <remarks>
        /// Keep in mind this method is called from a Timer and not synchronized with the camera stream. Also, the processing time of FaceTracker
        /// will vary depending on the size of each frame and the number of faces being tracked. That is, a large image with several tracked faces may
        /// take longer to process.
        /// </remarks>
        private async Task ProcessCurrentVideoFrameAsync()
        {
            // Create a VideoFrame object specifying the pixel format we want our capture image to be (NV12 bitmap in this case).
            // GetPreviewFrame will convert the native webcam frame into this format.
            const BitmapPixelFormat InputPixelFormat = BitmapPixelFormat.Nv12;
            using (VideoFrame previewFrame = new VideoFrame(InputPixelFormat, (int)this.videoProperties.Width, (int)this.videoProperties.Height))
            {
                try
                {
                    await this.mediaCapture.GetPreviewFrameAsync(previewFrame);
                }
                catch (UnauthorizedAccessException)
                {
                    // Lost access to the camera.
                    //AbandonStreaming();
                    return;
                }
                catch (Exception)
                {
                    //if we set an error text here, we get RPC_E_WRONG_THREAD
                    //errorMessage.Text = "PreviewFrame with format '{InputPixelFormat}' is not supported by your Webcam";
                    return;
                }

                // The returned VideoFrame should be in the supported NV12 format but we need to verify this.
                if (!FaceDetector.IsBitmapPixelFormatSupported(previewFrame.SoftwareBitmap.BitmapPixelFormat))
                {
                    errorMessage.Text = "PixelFormat '{previewFrame.SoftwareBitmap.BitmapPixelFormat}' is not supported by FaceDetector";
                    return;
                }

                IList<DetectedFace> faces;
                try
                {
                    faces = await this.faceTracker.ProcessNextFrameAsync(previewFrame);
                }
                catch (Exception ex)
                {
                    errorMessage.Text = ex.ToString();
                    return;
                }

                // Create our visualization using the frame dimensions and face results but run it on the UI thread.
                var previewFrameSize = new Windows.Foundation.Size(previewFrame.SoftwareBitmap.PixelWidth, previewFrame.SoftwareBitmap.PixelHeight);
                var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    this.SetupVisualization(previewFrameSize, faces);
                });
            }
        }

        /// <summary>
        /// Takes the webcam image and FaceTracker results and assembles the visualization onto the Canvas.
        /// </summary>
        /// <param name="framePizelSize">Width and height (in pixels) of the video capture frame</param>
        /// <param name="foundFaces">List of detected faces; output from FaceTracker</param>
        private void SetupVisualization(Windows.Foundation.Size framePixelSize, IList<DetectedFace> foundFaces)
        {
            this.VisualizationCanvas.Children.Clear();

            if (framePixelSize.Width != 0.0 && framePixelSize.Height != 0.0)
            {
                double widthScale = this.VisualizationCanvas.ActualWidth / framePixelSize.Width;
                double heightScale = this.VisualizationCanvas.ActualHeight / framePixelSize.Height;

                foreach (DetectedFace face in foundFaces)
                {

                    //double mirrorX = (face.FaceBox.X * widthScale) > (this.VisualizationCanvas.ActualWidth / 2) ? (face.FaceBox.X * widthScale) - (this.VisualizationCanvas.ActualWidth / 2) : (face.FaceBox.X * widthScale) + (this.VisualizationCanvas.ActualWidth / 2);

                    // Create a rectangle element for displaying the face box but since we're using a Canvas
                    // we must scale the rectangles according to the frames's actual size.
                    Rectangle box = new Rectangle()
                    {
                        Width = face.FaceBox.Width * widthScale,
                        Height = face.FaceBox.Height * heightScale,
                        //Margin = new Thickness(face.FaceBox.X * widthScale, face.FaceBox.Y * heightScale, 0, 0),
                        Margin = new Thickness(face.FaceBox.X * widthScale, face.FaceBox.Y * heightScale, 0, 0),
                        Stroke = new SolidColorBrush(Windows.UI.Colors.Blue),
                        StrokeThickness = 2,
                        Fill = new SolidColorBrush(Windows.UI.Colors.Transparent)
                        //Style = this.Resources["HighlightedFaceBoxStyle"] as Style
                        //Style = new Style() { TargetType = Rectangle, Setters = }
                    };
                    //box.RenderTransform = new ScaleTransform() { ScaleX = -1, CenterX = 160 , CenterY = 0 };
                    //box.RenderTransform = new RotateTransform() { Angle=180, CenterX = 160, CenterY = 120 };
                    
                    //TODO enable this to draw the box
                    //this.VisualizationCanvas.Children.Add(box);
                }
            }
        }



        public async Task CleanupCameraAsync()
        {
            if (IsInitialized)
            {
                if (isPreviewing)
                {
                    await StopPreviewAsync();
                }

                UnregisterOrientationEventHandlers();
                IsInitialized = false;
            }

            if (mediaCapture != null)
            {
                mediaCapture.Failed -= MediaCapture_Failed;
                mediaCapture.Dispose();
                mediaCapture = null;
            }
        }

        public static async Task<ObservableCollection<string>> GetCamerasAsync()
        {
            //IList<string> lines = new List<string>(new string[] { "element1.1", "element1.2", "element1.3", "element2.1", "element2.2", "element2.3" });
            ObservableCollection<string> cameras = new ObservableCollection<string>();
            DeviceInformationCollection _cameraDevices;

            try
            {
                _cameraDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                if (_cameraDevices == null || !_cameraDevices.Any())
                {
                    throw new NotSupportedException();
                }



                for (int i = 0; i < _cameraDevices.Count; i++)
                {
                    cameras.Add(_cameraDevices[i].Name);

                }

            }
            catch (NotSupportedException)
            {
                //errorMessage.Text = "Camera_Exception_NotSupported".GetLocalized();
            }
            return cameras;
        }

        public void SwitchPanel()
        {
            Panel = (Panel == Panel.Front) ? Panel.Back : Panel.Front;
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            //await TakePhoto();
        }

        private void SwitchButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPanel();
        }


        private async void CleanAndInitialize()
        {
            await Task.Run(async () => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await CleanupCameraAsync();
                await InitializeCameraAsync();
            }));
        }

        private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            Task.Run(async () => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await CleanupCameraAsync();
            }));
        }

        private async Task StartPreviewAsync()
        {
            PreviewControl.Source = mediaCapture;
            PreviewControl.FlowDirection = mirroringPreview ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            if (mediaCapture != null)
            {
                await mediaCapture.StartPreviewAsync();
                await SetPreviewRotationAsync();
                isPreviewing = true;
            }
        }

        private async Task SetPreviewRotationAsync()
        {
            displayOrientation = _displayInformation.CurrentOrientation;
            int rotationDegrees = 0; //_displayOrientation.ToDegrees();

            if (mirroringPreview)
            {
                rotationDegrees = (360 - rotationDegrees) % 360;
            }

            if (mediaCapture != null)
            {
                var props = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
                props.Properties.Add(_rotationKey, rotationDegrees);
                await mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
            }
        }

        private async Task StopPreviewAsync()
        {
            isPreviewing = false;
            await mediaCapture.StopPreviewAsync();
            PreviewControl.Source = null;
        }


        private void RegisterOrientationEventHandlers()
        {
            if (_orientationSensor != null)
            {
                _orientationSensor.OrientationChanged += OrientationSensor_OrientationChanged;
                deviceOrientation = _orientationSensor.GetCurrentOrientation();
            }

            _displayInformation.OrientationChanged += DisplayInformation_OrientationChanged;
            displayOrientation = _displayInformation.CurrentOrientation;
        }

        private void UnregisterOrientationEventHandlers()
        {
            if (_orientationSensor != null)
            {
                _orientationSensor.OrientationChanged -= OrientationSensor_OrientationChanged;
            }

            _displayInformation.OrientationChanged -= DisplayInformation_OrientationChanged;
        }

        private void OrientationSensor_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args)
        {
            if (args.Orientation != SimpleOrientation.Faceup && args.Orientation != SimpleOrientation.Facedown)
            {
                deviceOrientation = args.Orientation;
            }
        }

        private async void DisplayInformation_OrientationChanged(DisplayInformation sender, object args)
        {
            displayOrientation = sender.CurrentOrientation;
            await SetPreviewRotationAsync();
        }

        private static void OnPanelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (CameraControl)d;

            if (ctrl.IsInitialized)
            {
                ctrl.CleanAndInitialize();
            }
        }
    }


    public class Camera
    {
        #region Properties
        //public string Name => FirstName + " " + LastName;
        public string Label;
        #endregion

        public Camera(string label)
        {
            Label = label;

        }


        public static async Task<ObservableCollection<Camera>> GetCamerasAsync()
        {
            /*StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Contacts.txt"));
            IList<string> lines = await FileIO.ReadLinesAsync(file);
            */
            IList<string> lines = new List<string>(new string[] { "element1.1", "element1.2", "element1.3", "element2.1", "element2.2", "element2.3" });

            ObservableCollection<Camera> cameras = new ObservableCollection<Camera>();

            for (int i = 0; i < lines.Count; i += 3)
            {
                cameras.Add(new Camera(lines[i]));
            }

            return cameras;
        }


    }


}
