using Microsoft.AspNetCore.Http;
using RestoranYonetim.Services;
using SkiaSharp;

namespace RestoranYonetim.Tests;

public class ImageUploadValidatorTests
{
    [Fact]
    public async Task ValidateAsync_RealPng_IsValid()
    {
        using var bitmap = new SKBitmap(50, 50);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);

        Assert.Null(await ImageUploadValidator.ValidateAsync(FormFile(data.ToArray(), "a.png", "image/png")));
    }

    [Fact]
    public async Task ValidateAsync_HugeDimensionPng_IsRejected()
    {
        var error = await ImageUploadValidator.ValidateAsync(FormFile(PngHeaderOnly(30000, 30000), "bomb.png", "image/png"));

        Assert.Contains("çözünürlüğü çok yüksek", error);
    }

    [Fact]
    public async Task ValidateAsync_ValidMagicButGarbage_IsRejected()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF }.Concat(new byte[200]).ToArray();

        Assert.NotNull(await ImageUploadValidator.ValidateAsync(FormFile(bytes, "x.jpg", "image/jpeg")));
    }

    private static IFormFile FormFile(byte[] bytes, string name, string contentType) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = contentType };

    // Yalnızca imza + IHDR + boş IDAT + IEND içeren PNG: çözümleyici boyutları okur, piksel verisi yoktur.
    private static byte[] PngHeaderOnly(int width, int height)
    {
        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, width);
        WriteBigEndian(ihdr, 4, height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        var signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var idat = new byte[] { 0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01 };
        return signature.Concat(Chunk("IHDR", ihdr)).Concat(Chunk("IDAT", idat)).Concat(Chunk("IEND", Array.Empty<byte>())).ToArray();
    }

    private static byte[] Chunk(string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        var crc = new byte[4];
        WriteBigEndian(crc, 0, (int)Crc32(typeBytes.Concat(data).ToArray()));
        return length.Concat(typeBytes).Concat(data).Concat(crc).ToArray();
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return ~crc;
    }
}
