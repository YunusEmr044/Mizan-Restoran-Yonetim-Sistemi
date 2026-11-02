using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Tests;

public class ImageResizeTests
{
    [Theory]
    [InlineData(8000, 6000, 1920, 1920, 1440)]
    [InlineData(1000, 800, 1920, 1000, 800)]
    [InlineData(6000, 8000, 1920, 1440, 1920)]
    [InlineData(1921, 1, 1920, 1920, 1)]
    public void FitWithin(int width, int height, int max, int expectedWidth, int expectedHeight)
    {
        Assert.Equal((expectedWidth, expectedHeight), ImageResize.FitWithin(width, height, max));
    }
}
