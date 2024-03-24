using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using WebcamOnDesktop.Helpers;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace WebcamOnDesktop.Controls
{
    public sealed partial class CameraControl
    {

        public static readonly DependencyProperty CanSwitchProperty =
            DependencyProperty.Register("CanSwitch", typeof(bool), typeof(CameraControl), new PropertyMetadata(false));

        public static readonly DependencyProperty FlipHorizontalProperty =
            DependencyProperty.Register("FlipHorizontal", typeof(bool), typeof(CameraControl), new PropertyMetadata(false));

        public static readonly DependencyProperty FlipVerticalProperty =
            DependencyProperty.Register("FlipVertical", typeof(bool), typeof(CameraControl), new PropertyMetadata(false));

        public static readonly DependencyProperty FullResProperty =
            DependencyProperty.Register("FullRes", typeof(bool), typeof(CameraControl), new PropertyMetadata(false));

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
        private SimpleOrientation deviceOrientation = SimpleOrientation.NotRotated;
        private DisplayOrientations displayOrientation = DisplayOrientations.Portrait;
        private DeviceInformationCollection _cameraDevices;


        /// <summary>
        /// Cache of properties from the current MediaCapture device which is used for capturing the preview frame.
        /// </summary>
        private VideoEncodingProperties videoProperties;



        public bool CanSwitch
        {
            get { return (bool)GetValue(CanSwitchProperty); }
            set { SetValue(CanSwitchProperty, value); }
        }


        public bool FlipHorizontal
        {
            get { return (bool)GetValue(FlipHorizontalProperty); }
            set { SetValue(FlipHorizontalProperty, value); }
        }

        public bool FlipVertical
        {
            get { return (bool)GetValue(FlipVerticalProperty); }
            set { SetValue(FlipVerticalProperty, value); }
        }

        public bool FullRes
        {
            get { return (bool)GetValue(FullResProperty); }
            set { SetValue(FullResProperty, value); }
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
        /// Initializes a new MediaCapture instance and starts the Preview streaming to the CamPreview UI element.
        /// </summary>
        /// <returns>Async Task object returning true if initialization and streaming were successful and false if an exception occurred.</returns>
        public async Task<bool> InitializeCameraAsync()
        {
            bool successful = false;


            try
            {
                if (mediaCapture == null)
                {
                    mediaCapture = new MediaCapture();
                    mediaCapture.Failed += MediaCapture_Failed;
                    mediaCapture.CameraStreamStateChanged += MediaCapture_CameraStreamStateChanged;

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


                    IsInitialized = true;
                    CanSwitch = _cameraDevices?.Count > 1;
                    RegisterOrientationEventHandlers();
                    await StartPreviewAsync();

                    //if FullRes then go into full resolution mode
                    //TODO: remove bool assignment, replace with option in settings
                    FullRes = false;
                    if (FullRes)
                    {
                        // Query all properties of the device
                        IEnumerable<StreamResolution> allProperties = mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview).Select(x => new StreamResolution(x));

                        // Order them by resolution then frame rate
                        allProperties = allProperties.OrderByDescending(x => x.Height * x.Width).ThenByDescending(x => x.FrameRate);

                        // Populate the combo box with the entries
                        foreach (var property in allProperties)
                        {
                            /*ComboBoxItem comboBoxItem = new ComboBoxItem();
                            comboBoxItem.Content = property.GetFriendlyName();
                            comboBoxItem.Tag = property;
                            CameraSettingsComboBox.Items.Add(comboBoxItem);*/
                            //var encodingProperties = (selectedItem.Tag as StreamResolution).EncodingProperties;
                            //TODO: filter for 30fps and 1920x1080
                            string resolution = property.GetFriendlyName();
                            errorMessage.Text = resolution;
                            if (resolution.Contains("1920"))
                            {
                                var encodingProperties = (property as StreamResolution).EncodingProperties;
                                await SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, encodingProperties);
                                break;
                            }
                        }

                    }


                    videoProperties = (VideoEncodingProperties)mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);

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
        /// Sets encoding properties on a camera stream. Ensures CaptureElement and preview stream are stopped before setting properties.
        /// </summary>
        public async Task SetMediaStreamPropertiesAsync(MediaStreamType streamType, IMediaEncodingProperties encodingProperties)
        {
            // Stop preview and unlink the CaptureElement from the MediaCapture object
            await mediaCapture.StopPreviewAsync();
            PreviewControl.Source = null;

            // Apply desired stream properties
            await mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, encodingProperties);

            // Recreate the CaptureElement pipeline and restart the preview
            PreviewControl.Source = mediaCapture;
            await mediaCapture.StartPreviewAsync();
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
            Debug.WriteLine("MediaCapture_Failed |" + errorEventArgs.ToString());
            Task.Run(async () => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await CleanupCameraAsync();
            }));
        }

        /// <summary>
        /// Invoked if CameraStreamState changes, tries to restart camera if stream was shut down
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void MediaCapture_CameraStreamStateChanged(MediaCapture sender, object args)
        {
            if (sender.CameraStreamState == Windows.Media.Devices.CameraStreamState.Shutdown)
            {
                //try to restart the camera, because of unexpected shutdown
                Debug.WriteLine("MediaCapture_CameraStreamStateChanged | Unexpected Shutdown, trying to restart | CameraStreamState: " + sender.CameraStreamState);
                CleanAndInitialize();
            }
        }

        private async Task StartPreviewAsync()
        {
            PreviewControl.Source = mediaCapture;
            PreviewControl.FlowDirection = FlipHorizontal ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;


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
            //int rotationDegrees = 0; //_displayOrientation.ToDegrees();

            int rotationDegrees = FlipVertical ? 180 : 0;


            /*if (mirroringPreview)
            {
                rotationDegrees = (360 - rotationDegrees) % 360;
            }*/

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


    /*    public class Camera
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
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Contacts.txt"));
                IList<string> lines = await FileIO.ReadLinesAsync(file);

                IList<string> lines = new List<string>(new string[] { "element1.1", "element1.2", "element1.3", "element2.1", "element2.2", "element2.3" });

                ObservableCollection<Camera> cameras = new ObservableCollection<Camera>();

                for (int i = 0; i < lines.Count; i += 3)
                {
                    cameras.Add(new Camera(lines[i]));
                }

                return cameras;
            }


        }*/


}
