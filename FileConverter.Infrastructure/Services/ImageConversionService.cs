using System.Drawing;
using System.Drawing.Imaging;
using FileConverter.Core.Interfaces;

namespace FileConverter.Infrastructure.Services;

public class ImageConversionService : IImageConversionService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IPdfConversionService _pdfConversionService;

    public ImageConversionService(
        IFileSystemService fileSystemService,
        IPdfConversionService pdfConversionService)
    {
        _fileSystemService = fileSystemService;
        _pdfConversionService = pdfConversionService;
    }

    public async Task<string> ConvertImageAsync(string sourcePath, string targetExt, string outputDir, CancellationToken ct = default)
    {
        var cleanTarget = targetExt.TrimStart('.').ToLowerInvariant();
        if (cleanTarget == "pdf")
        {
            return await _pdfConversionService.ConvertImageToPdfAsync(sourcePath, outputDir, ct);
        }

        return await Task.Run(() =>
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Source image not found", sourcePath);
            }

            _fileSystemService.EnsureDirectoryExists(outputDir);
            var baseName = Path.GetFileNameWithoutExtension(sourcePath);
            var destination = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, "." + cleanTarget);

            using var sourceImage = System.Drawing.Image.FromFile(sourcePath);

            switch (cleanTarget)
            {
                case "png":
                    sourceImage.Save(destination, ImageFormat.Png);
                    break;

                case "jpg" or "jpeg":
                    var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 92L);
                    if (jpgEncoder != null)
                    {
                        sourceImage.Save(destination, jpgEncoder, encoderParams);
                    }
                    else
                    {
                        sourceImage.Save(destination, ImageFormat.Jpeg);
                    }
                    break;

                case "bmp":
                    sourceImage.Save(destination, ImageFormat.Bmp);
                    break;

                case "gif":
                    sourceImage.Save(destination, ImageFormat.Gif);
                    break;

                case "tiff" or "tif":
                    sourceImage.Save(destination, ImageFormat.Tiff);
                    break;

                case "ico":
                    SaveAsIcon(sourceImage, destination);
                    break;

                case "webp":
                    using (var magickImg = new ImageMagick.MagickImage(sourcePath))
                    {
                        magickImg.Format = ImageMagick.MagickFormat.WebP;
                        magickImg.Quality = 85;
                        magickImg.Write(destination);
                    }
                    break;

                default:
                    throw new NotSupportedException($"Image target format '{targetExt}' is not supported.");
            }

            return destination;
        }, ct);
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        return ImageCodecInfo.GetImageEncoders().FirstOrDefault(codec => codec.FormatID == format.Guid);
    }

    private static void SaveAsIcon(System.Drawing.Image sourceImage, string destinationPath)
    {
        // Generate high-resolution 256x256 or 64x64 icon bitmap
        int size = Math.Min(Math.Max(sourceImage.Width, sourceImage.Height), 256);
        using var resized = new Bitmap(size, size);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.Clear(Color.Transparent);
            g.DrawImage(sourceImage, 0, 0, size, size);
        }

        using var ms = new MemoryStream();
        resized.Save(ms, ImageFormat.Png);
        var pngBytes = ms.ToArray();

        using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICO Header
        bw.Write((short)0); // Reserved
        bw.Write((short)1); // Type: 1 = ICO
        bw.Write((short)1); // Count: 1 image

        // ICO Directory Entry
        bw.Write((byte)(size >= 256 ? 0 : size)); // Width
        bw.Write((byte)(size >= 256 ? 0 : size)); // Height
        bw.Write((byte)0); // Color count
        bw.Write((byte)0); // Reserved
        bw.Write((short)1); // Color planes
        bw.Write((short)32); // Bits per pixel
        bw.Write((int)pngBytes.Length); // Image size in bytes
        bw.Write((int)22); // Image data offset (6 header + 16 directory entry)

        // PNG payload
        bw.Write(pngBytes);
    }
}
