namespace RestoranYonetim.Services.Publishing;

public static class ImageResize
{
    // En uzun kenar max'ı aşıyorsa oranı koruyarak küçültür; küçük görseller büyütülmez.
    public static (int Width, int Height) FitWithin(int width, int height, int max)
    {
        var longest = Math.Max(width, height);
        if (longest <= max)
        {
            return (width, height);
        }

        var scale = (double)max / longest;
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }
}
