using ImageMagick;
using PhotoMap.Worker.Models.Image;
using SkiaSharp;

namespace PhotoMap.Worker.Services.Implementations.Core
{
    /// <summary>
    /// Every Skia object holds native memory the GC does not see, so each one is disposed as soon as it is replaced:
    /// left to the finalizer, the decoded bitmaps of the photos processed in parallel pile up.
    /// </summary>
    public class ImageProcessor : IDisposable
    {
        private const int Quality = 100;

        private readonly SKEncodedOrigin _origin;

        private SKBitmap _bitmap;
        private SKBitmap? _cropped;

        public ImageProcessor(string filePath) : this(File.ReadAllBytes(filePath))
        {
        }

        /// <param name="bytes">The contents of the image file.</param>
        /// <param name="minSize">
        /// The largest size the image is cropped to. When given, the image is decoded scaled down to no less than it,
        /// instead of at the full resolution, which takes a fraction of the memory.
        /// </param>
        public ImageProcessor(byte[] bytes, int? minSize = null)
        {
            if (IsHeic(bytes))
            {
                bytes = ConvertHeicToJpeg(bytes, minSize);
            }

            using var codec = SKCodec.Create(new MemoryStream(bytes))
                ?? throw new InvalidDataException("The file is not an image or its format is not supported.");

            _origin = codec.EncodedOrigin;
            _bitmap = SKBitmap.Decode(codec, GetDecodeInfo(codec, minSize))
                ?? throw new InvalidDataException("Failed to decode the image.");
        }

        /// <summary>
        /// Crops the square of the given size out of the middle of the image. Each crop is made from the whole image,
        /// not from the previous crop, so the sizes can be cropped in any order.
        /// </summary>
        public void Crop(int size)
        {
            int width, height;

            if (_bitmap.Width > _bitmap.Height)
            {
                height = size;
                width = _bitmap.Width * size / _bitmap.Height;
            }
            else
            {
                width = size;
                height = _bitmap.Height * size / _bitmap.Width;
            }

            using var resized = _bitmap.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));

            var cropped = new SKBitmap(size, size);
            using (var canvas = new SKCanvas(cropped))
            {
                canvas.DrawBitmap(resized, -(width - size) / 2, -(height - size) / 2, SKSamplingOptions.Default);
            }

            _cropped?.Dispose();
            _cropped = cropped;
        }

        public void Rotate()
        {
            var rotated = RotateBitmap(_bitmap, _origin);
            if (rotated == null)
            {
                return;
            }

            _bitmap.Dispose();
            _bitmap = rotated;
        }

        /// <summary>
        /// The last crop, or the whole image when it has not been cropped, encoded to JPEG.
        /// </summary>
        public byte[] GetImageBytes()
        {
            using var data = (_cropped ?? _bitmap).Encode(SKEncodedImageFormat.Jpeg, Quality);

            return data.ToArray();
        }

        public void Dispose()
        {
            _bitmap.Dispose();
            _cropped?.Dispose();
        }

        private static bool IsHeic(byte[] bytes)
        {
            var heicSignature = "ftyp"u8;

            return bytes.Length >= 8 && bytes.AsSpan(4, 4).SequenceEqual(heicSignature);
        }

        private static byte[] ConvertHeicToJpeg(byte[] bytes, int? minSize)
        {
            using var image = new MagickImage(bytes);

            if (minSize != null)
            {
                // shrinks the shorter side to the size, as the scaled decoding does for the other formats
                image.Resize(new MagickGeometry((uint)minSize.Value, (uint)minSize.Value) { FillArea = true, Greater = true });
            }

            return image.ToByteArray(MagickFormat.Jpeg);
        }

        /// <summary>
        /// JPEG, the format of most photos, can be decoded at 1/2, 1/4 or 1/8 of its size: the smallest of those not
        /// smaller than the size is used. The shorter side is compared, which rotating does not change.
        /// </summary>
        private static SKImageInfo GetDecodeInfo(SKCodec codec, int? minSize)
        {
            var info = codec.Info;
            var shorterSide = Math.Min(info.Width, info.Height);

            if (minSize == null || minSize.Value >= shorterSide)
            {
                return info;
            }

            var scaled = codec.GetScaledDimensions((float)minSize.Value / shorterSide);

            return Math.Min(scaled.Width, scaled.Height) >= minSize.Value
                ? info.WithSize(scaled.Width, scaled.Height)
                : info;
        }

        private static SKBitmap? RotateBitmap(SKBitmap bitmap, SKEncodedOrigin orientation)
        {
            var bitmapOptions = GetBitmapOptions(bitmap, orientation);
            if (bitmapOptions == null)
            {
                return null;
            }

            var rotated = new SKBitmap(bitmapOptions.Width, bitmapOptions.Height);

            using var canvas = new SKCanvas(rotated);

            canvas.Translate(bitmapOptions.Dx, bitmapOptions.Dy);
            canvas.RotateDegrees(bitmapOptions.Degrees);
            canvas.DrawBitmap(bitmap, 0, 0, SKSamplingOptions.Default);

            return rotated;
        }

        private static BitmapOptions? GetBitmapOptions(SKBitmap bitmap, SKEncodedOrigin orientation)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;

            return orientation switch
            {
                SKEncodedOrigin.BottomRight => new BitmapOptions(width, height, width, height, 180),
                SKEncodedOrigin.RightTop => new BitmapOptions(height, width, height, 0, 90),
                SKEncodedOrigin.LeftBottom => new BitmapOptions(height, width, 0, height, 270),
                _ => null
            };
        }
    }
}
