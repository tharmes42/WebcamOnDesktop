using Microsoft.AI.MachineLearning;
using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Diagnostics;

namespace WebcamOnDesktop.Helpers
{


    public sealed class u2netInput
    {
        public TensorFloat input; // shape(1,3,320,320)
    }

    public sealed class u2netOutput
    {
        public TensorFloat o0; // shape(1,1,320,320)
        public TensorFloat o1; // shape(1,1,320,320)
        public TensorFloat o2; // shape(1,1,320,320)
        public TensorFloat o3; // shape(1,1,320,320)
        public TensorFloat o4; // shape(1,1,320,320)
        public TensorFloat o5; // shape(1,1,320,320)
        public TensorFloat o6; // shape(1,1,320,320)
    }

    public sealed class u2netModel
    {
        private LearningModel model;
        private LearningModelSession session;
        private LearningModelBinding binding;
        public static async Task<u2netModel> CreateFromStreamAsync(IRandomAccessStreamReference stream)
        {
            u2netModel learningModel = new u2netModel();
            learningModel.model = await LearningModel.LoadFromStreamAsync(stream);
            learningModel.session = new LearningModelSession(learningModel.model);
            learningModel.binding = new LearningModelBinding(learningModel.session);
            return learningModel;
        }
        public async Task<u2netOutput> EvaluateAsync(u2netInput input)
        {
            binding.Bind("input", input.input);
            var result = await session.EvaluateAsync(binding, "0");
            var output = new u2netOutput();
            output.o0 = result.Outputs["o0"] as TensorFloat;
            output.o1 = result.Outputs["o1"] as TensorFloat;
            output.o2 = result.Outputs["o2"] as TensorFloat;
            output.o3 = result.Outputs["o3"] as TensorFloat;
            output.o4 = result.Outputs["o4"] as TensorFloat;
            output.o5 = result.Outputs["o5"] as TensorFloat;
            output.o6 = result.Outputs["o6"] as TensorFloat;
            return output;
        }
    }

    public class Segmentation {

        private StorageFile modelFile;
        private u2netModel model;
        private SoftwareBitmapSource sourceImage;
        private Image targetImage;
        public Segmentation()
        {
            targetImage = new Image();
        }


        private async void ProcessImage(IRandomAccessStream stream)
        {
           
            // Use Picket to get file
            //var file = await GetImageFile();

            //SoftwareBitmap softwareBitmap;
            byte[] bytes;



            //// Load image & scale to tensor input dimensions
            //using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
            //{
            //    bytes = await GetImageAsByteArrayAsync(stream, 320, 320, BitmapPixelFormat.Rgba8);
            //    softwareBitmap = await GetImageAsSoftwareBitmapAsync(stream, 320, 320, BitmapPixelFormat.Bgra8);
            //}

            bytes = await GetImageAsByteArrayAsync(stream, 320, 320, BitmapPixelFormat.Rgba8);
            //softwareBitmap = await GetImageAsSoftwareBitmapAsync(stream, 320, 320, BitmapPixelFormat.Bgra8);



            
            // Convert rgba-rgba-...-rgba to bb...b-rr...r-gg...g as colour weighted tensor (0..1)
            TensorFloat input = TensorFloat.CreateFromIterable(new long[] { 1, 3, 320, 320 }, TensorBrg(bytes));

            if (modelFile == null) { 
                // Load model & perform inference
                modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/u2net.onnx"));
                model = await u2netModel.CreateFromStreamAsync(modelFile);
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();
            u2netOutput output = await model.EvaluateAsync(new u2netInput { input = input });
            sw.Stop();
            //textTimer.Text = sw.ElapsedMilliseconds.ToString();

            //await ToImage(output.o6, o6);
            //await ToImage(output.o5, o5);
            //await ToImage(output.o4, o4);
            //await ToImage(output.o3, o3);
            //await ToImage(output.o2, o2);
            //await ToImage(output.o1, o1);
            await ToBlendedImage(bytes, output.o0, targetImage);
        }



        private async Task<StorageFile> GetImageFile()
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");

            var file = await picker.PickSingleFileAsync();

            return file;
        }


        private async Task<byte[]> GetImageAsByteArrayAsync(IRandomAccessStream stream, uint width, uint height, BitmapPixelFormat pixelFormat)
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            var transform = new BitmapTransform() { ScaledWidth = width, ScaledHeight = height, InterpolationMode = BitmapInterpolationMode.NearestNeighbor };
            var data = await decoder.GetPixelDataAsync(pixelFormat, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);

            return data.DetachPixelData();
        }


        private async Task<SoftwareBitmap> GetImageAsSoftwareBitmapAsync(IRandomAccessStream stream, uint width, uint height, BitmapPixelFormat pixelFormat)
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            var transform = new BitmapTransform() { ScaledWidth = width, ScaledHeight = height, InterpolationMode = BitmapInterpolationMode.NearestNeighbor };
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(pixelFormat, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);

            return softwareBitmap;
        }

        public IEnumerable<float> TensorBrg(byte[] bytes)
        {
            // Original in rgb (0,1,2), we want brg(2,0,1)
            for (int i = 2; i < bytes.Length; i += 4)
            {
                var b = Convert.ToSingle(((bytes[i] / 255.0) - 0.406) / 0.225);
                yield return b;
            }

            for (int i = 0; i < bytes.Length; i += 4)
            {
                var r = Convert.ToSingle(((bytes[i] / 255.0) - 0.485) / 0.229);
                yield return r;
            }

            for (int i = 1; i < bytes.Length; i += 4)
            {
                var g = Convert.ToSingle(((bytes[i] / 255.0) - 0.456) / 0.224);
                yield return g;
            }
        }

        private async Task ToImage(TensorFloat tensorFloat, Image image)
        {
            var pixels = tensorFloat
                   .GetAsVectorView()
                   .SelectMany(
                       f =>
                       {
                           byte v = Convert.ToByte(f * 255);
                           return new byte[] { v, v, v, 255 };
                       })
                   .ToArray();

            var writeableBitmap = new WriteableBitmap(320, 320);

            // Open a stream to copy the image contents to the WriteableBitmap's pixel buffer 
            using (Stream stream = writeableBitmap.PixelBuffer.AsStream())
            {
                await stream.WriteAsync(pixels, 0, pixels.Length);
            }

            var dest = SoftwareBitmap.CreateCopyFromBuffer(writeableBitmap.PixelBuffer, BitmapPixelFormat.Bgra8, 320, 320, BitmapAlphaMode.Premultiplied);
            var destSouce = new SoftwareBitmapSource();
            await destSouce.SetBitmapAsync(dest);

            image.Source = destSouce;
        }
        private IEnumerable<byte> ApplyTensorAsMask(byte[] data, TensorFloat tensorFloat, float cutoff)
        {
            var tensorData = tensorFloat.GetAsVectorView().ToArray();

            for (int i = 0; i < data.Length; i += 4)
            {
                var alpha = Math.Clamp(tensorData[i / 4], 0, 1);

                if (alpha > cutoff)
                {
                    yield return Convert.ToByte(data[i + 2] * alpha);
                    yield return Convert.ToByte(data[i + 1] * alpha);
                    yield return Convert.ToByte(data[i + 0] * alpha);
                    yield return Convert.ToByte(alpha * 255);
                }
                else
                {
                    yield return 0;
                    yield return 0;
                    yield return 0;
                    yield return 0;
                }

            }
        }

        private async Task ToBlendedImage(byte[] data, TensorFloat tensorFloat, Image target)
        {
            var image = ApplyTensorAsMask(data, tensorFloat, 0.0f).ToArray();
            var writeableBitmap = new WriteableBitmap(320, 320);

            // Open a stream to copy the image contents to the WriteableBitmap's pixel buffer 
            using (Stream stream = writeableBitmap.PixelBuffer.AsStream())
            {
                await stream.WriteAsync(image, 0, image.Length);
            }

            var dest = SoftwareBitmap.CreateCopyFromBuffer(writeableBitmap.PixelBuffer, BitmapPixelFormat.Bgra8, 320, 320, BitmapAlphaMode.Premultiplied);
            var destSouce = new SoftwareBitmapSource();
            await destSouce.SetBitmapAsync(dest);

            target.Source = destSouce;
    }
    }
}
