using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void ToSafeHtml_RendersHeadingAndList()
    {
        var html = MarkdownRenderer.ToSafeHtml("# Başlık\n\n- a");

        Assert.Contains("<h1", html);
        Assert.Contains("<li>a</li>", html);
    }

    [Fact]
    public void ToSafeHtml_EscapesRawHtml()
    {
        var html = MarkdownRenderer.ToSafeHtml("<script>alert(1)</script><img src=x onerror=y>");

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
