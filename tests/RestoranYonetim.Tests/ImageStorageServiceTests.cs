using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using RestoranYonetim.Services;
using SkiaSharp;

namespace RestoranYonetim.Tests;

public sealed class ImageStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mizan-img-" + Guid.NewGuid().ToString("N"));
    private readonly ImageStorageService _service;

    public ImageStorageServiceTests()
    {
        Directory.CreateDirectory(_root);
        _service = new ImageStorageService(new FakeEnv(_root));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task SaveAsync_LargePng_ResizedToWebP()
    {
        var url = await _service.SaveAsync(FormFile(Encode(3000, 2000, SKEncodedImageFormat.Png), "a.png", "image/png"), "test", null, "test");

        Assert.EndsWith(".webp", url);
        var (w, h, format) = Inspect(url);
        Assert.Equal((1920, 1280, SKEncodedImageFormat.Webp), (w, h, format));
    }

    [Fact]
    public async Task SaveAsync_ExifRotatedJpeg_IsUpright()
    {
        var jpeg = WithExifOrientation(Encode(400, 200, SKEncodedImageFormat.Jpeg), orientation: 6);

        var url = await _service.SaveAsync(FormFile(jpeg, "p.jpg", "image/jpeg"), "test", null, "test");

        var (w, h, _) = Inspect(url);
        Assert.Equal((200, 400), (w, h));
    }

    [Fact]
    public async Task SaveAsync_Gif_CopiedUnchanged()
    {
        var gif = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0, 1, 0, 0, 0, 0, 0x3B };

        var url = await _service.SaveAsync(FormFile(gif, "a.gif", "image/gif"), "test", null, "test");

        Assert.EndsWith(".gif", url);
        Assert.Equal(gif, await File.ReadAllBytesAsync(PhysicalPath(url)));
    }

    [Fact]
    public async Task SaveAsync_PngOutputWithMax512()
    {
        var url = await _service.SaveAsync(FormFile(Encode(1024, 1024, SKEncodedImageFormat.Png), "f.png", "image/png"), "favicon", null, "favicon", 512, ImageOutput.Png);

        Assert.EndsWith(".png", url);
        Assert.Equal((512, 512, SKEncodedImageFormat.Png), Inspect(url));
    }

    [Fact]
    public async Task SaveAsync_JpegOutputForShareImages()
    {
        var url = await _service.SaveAsync(FormFile(Encode(2400, 1260, SKEncodedImageFormat.Png), "og.png", "image/png"), "seo", null, "og", 1200, ImageOutput.Jpeg);

        Assert.EndsWith(".jpg", url);
        Assert.Equal((1200, 630, SKEncodedImageFormat.Jpeg), Inspect(url));
    }

    [Fact]
    public async Task Delete_RemovesUploadedFile()
    {
        var url = await _service.SaveAsync(FormFile(Encode(10, 10, SKEncodedImageFormat.Png), "a.png", "image/png"), "test", null, "test");

        _service.Delete(url);

        Assert.False(File.Exists(PhysicalPath(url)));
    }

    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/uploads/../css/site.css")]
    [InlineData("https://example.com/uploads/x.webp")]
    [InlineData(null)]
    public void Delete_IgnoresPathsOutsideUploads(string? url)
    {
        var protectedFile = Path.Combine(_root, "css", "site.css");
        Directory.CreateDirectory(Path.GetDirectoryName(protectedFile)!);
        File.WriteAllText(protectedFile, "x");

        _service.Delete(url);

        Assert.True(File.Exists(protectedFile));
    }

    [Fact]
    public void Delete_DoesNotTouchDemoFolder()
    {
        const string url = "/demo/menu/x.webp";
        var demoFile = PhysicalPath(url);
        Directory.CreateDirectory(Path.GetDirectoryName(demoFile)!);
        File.WriteAllText(demoFile, "x");

        _service.Delete(url);

        Assert.True(File.Exists(demoFile));
    }

    private string PhysicalPath(string url) => Path.Combine(_root, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    private (int, int, SKEncodedImageFormat) Inspect(string url)
    {
        using var codec = SKCodec.Create(PhysicalPath(url));
        return (codec.Info.Width, codec.Info.Height, codec.EncodedFormat);
    }

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.SaddleBrown);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    // JPEG'in SOI işaretinden hemen sonra yalnızca Orientation etiketi içeren bir APP1 (EXIF) segmenti ekler.
    private static byte[] WithExifOrientation(byte[] jpeg, ushort orientation)
    {
        var tiff = new byte[]
        {
            0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, (byte)orientation, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };
        var payload = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var length = payload.Length + 2;
        var app1 = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)length }.Concat(payload);
        return jpeg.Take(2).Concat(app1).Concat(jpeg.Skip(2)).ToArray();
    }

    private static IFormFile FormFile(byte[] bytes, string name, string contentType) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = contentType };

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public FakeEnv(string root) => WebRootPath = root;
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
    }
}
