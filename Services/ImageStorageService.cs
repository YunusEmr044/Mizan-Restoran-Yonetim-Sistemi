using RestoranYonetim.Services.Publishing;
using SkiaSharp;

namespace RestoranYonetim.Services;

// Jpeg: sosyal medya paylaşım görselleri (og:image) için - bazı önizleme servisleri WebP göstermiyor.
public enum ImageOutput { WebP, Png, Jpeg }

// Panelden yüklenen görselleri küçültüp WebP'ye çevirerek wwwroot/uploads/{folder} altına kaydeder ve siler.
// Doğrulama çağıranın sorumluluğunda (ImageUploadValidator.ValidateAsync); GIF'ler animasyon korunsun diye olduğu gibi kopyalanır.
public class ImageStorageService
{
    private const int WebPQuality = 80;
    private const int JpegQuality = 85;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ImageStorageService>? _logger;

    public ImageStorageService(IWebHostEnvironment env, ILogger<ImageStorageService>? logger = null)
    {
        _env = env;
        _logger = logger;
    }

    private string UploadsRoot => Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));

    public async Task<string> SaveAsync(IFormFile file, string folder, string? nameHint, string fallbackName, int maxDimension = 1920, ImageOutput output = ImageOutput.WebP)
    {
        var uploadsDir = Path.Combine(UploadsRoot, folder);
        Directory.CreateDirectory(uploadsDir);

        await using var input = new MemoryStream();
        await file.CopyToAsync(input);
        var bytes = input.ToArray();

        var isGif = ImageUploadValidator.IsGif(bytes);
        var extension = isGif ? ".gif" : output switch { ImageOutput.Png => ".png", ImageOutput.Jpeg => ".jpg", _ => ".webp" };
        var fileName = SlugHelper.BuildFileName(nameHint, fallbackName, extension);

        await File.WriteAllBytesAsync(Path.Combine(uploadsDir, fileName), isGif ? bytes : Optimize(bytes, maxDimension, output));
        return $"/uploads/{folder}/{fileName}";
    }

    // Yalnızca /uploads/ altındaki yerel dosyaları siler; dosya kilitli/erişilemezse sessizce geçer (DB işlemi zaten tamamlanmıştır).
    public void Delete(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var root = UploadsRoot + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(_env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException ex)
        {
            _logger?.LogWarning(ex, "Görsel dosyası silinemedi: {Path}", fullPath);
        }
    }

    private static byte[] Optimize(byte[] bytes, int maxDimension, ImageOutput output)
    {
        using var codec = SKCodec.Create(new MemoryStream(bytes)) ?? throw new InvalidOperationException("Görsel çözümlenemedi.");
        using var decoded = SKBitmap.Decode(codec) ?? throw new InvalidOperationException("Görsel çözümlenemedi.");
        using var rotated = Rotate(decoded, codec.EncodedOrigin);
        var upright = rotated ?? decoded;

        var (width, height) = ImageResize.FitWithin(upright.Width, upright.Height, maxDimension);
        using var resized = width == upright.Width && height == upright.Height
            ? null
            : upright.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));

        using var image = SKImage.FromBitmap(resized ?? upright);
        using var encoded = output switch
        {
            ImageOutput.Png => image.Encode(SKEncodedImageFormat.Png, 100),
            ImageOutput.Jpeg => image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality),
            _ => image.Encode(SKEncodedImageFormat.Webp, WebPQuality)
        };
        return encoded.ToArray();
    }

    // EXIF yönü dik değilse döndürülmüş yeni bir bitmap, değilse null döner.
    private static SKBitmap? Rotate(SKBitmap source, SKEncodedOrigin origin)
    {
        var degrees = origin switch
        {
            SKEncodedOrigin.BottomRight or SKEncodedOrigin.BottomLeft => 180,
            SKEncodedOrigin.RightTop or SKEncodedOrigin.LeftTop => 90,
            SKEncodedOrigin.LeftBottom or SKEncodedOrigin.RightBottom => 270,
            _ => 0
        };
        if (degrees == 0)
        {
            return null;
        }

        var swap = degrees != 180;
        var rotated = new SKBitmap(swap ? source.Height : source.Width, swap ? source.Width : source.Height);
        using var canvas = new SKCanvas(rotated);
        canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        using var sourceImage = SKImage.FromBitmap(source);
        canvas.DrawImage(sourceImage, 0, 0, SKSamplingOptions.Default);
        return rotated;
    }
}
