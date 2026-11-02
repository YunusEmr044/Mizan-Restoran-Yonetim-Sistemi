using SkiaSharp;

namespace RestoranYonetim.Services;

// ÖNEMLİ: Sadece uzantı/Content-Type kontrolü stored XSS'e açık kapı bırakır (Content-Type
// istemci tarafından sahtelenebilir); bu yüzden dosyanın gerçek ilk baytları (magic number) da kontrol edilir.
public static class ImageUploadValidator
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const long MaxPixels = 40_000_000;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };

    // Geçerliyse null, değilse kullanıcıya gösterilecek Türkçe hata mesajını döner.
    public static async Task<string?> ValidateAsync(IFormFile? file)
    {
        if (file is not { Length: > 0 })
        {
            return "Lütfen bir dosya seçin.";
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return "Dosya çok büyük (en fazla 5 MB olabilir).";
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            return "Sadece JPG, PNG, WEBP veya GIF formatında görsel yükleyebilirsiniz.";
        }

        if (string.IsNullOrEmpty(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
        {
            return "Dosya türü tanınamadı - lütfen geçerli bir görsel dosyası seçin.";
        }

        if (!await HasValidImageSignatureAsync(file))
        {
            return "Dosya içeriği geçerli bir görsel gibi görünmüyor.";
        }

        return ValidateDecodable(file);
    }

    // Küçük ama devasa çözünürlüklü bir dosya (ör. 30000x30000 PNG) piksellere açılırken gigabaytlarca bellek ister;
    // boyut, görsel açılmadan yalnızca başlıktan okunup sınırlanır.
    private static string? ValidateDecodable(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var codec = SKCodec.Create(stream);
        if (codec == null)
        {
            return "Dosya içeriği geçerli bir görsel gibi görünmüyor.";
        }

        return (long)codec.Info.Width * codec.Info.Height > MaxPixels
            ? "Görselin çözünürlüğü çok yüksek (en fazla 40 megapiksel olabilir)."
            : null;
    }

    // GIF87a / GIF89a: 47 49 46 38
    public static bool IsGif(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38;

    private static async Task<bool> HasValidImageSignatureAsync(IFormFile file)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, 12));
        if (read < 4)
        {
            return false;
        }

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return true;
        }

        // PNG: 89 50 4E 47
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            return true;
        }

        if (IsGif(header))
        {
            return true;
        }

        // WEBP: "RIFF" .... "WEBP"
        if (read >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return true;
        }

        return false;
    }
}
