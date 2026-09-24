namespace RestoranYonetim.Services;

// ÖNEMLİ: Sadece uzantı/Content-Type kontrolü stored XSS'e açık kapı bırakır (Content-Type
// istemci tarafından sahtelenebilir); bu yüzden dosyanın gerçek ilk baytları (magic number) da kontrol edilir.
public static class ImageUploadValidator
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

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

        return null;
    }

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

        // GIF87a / GIF89a: 47 49 46 38
        if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
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
