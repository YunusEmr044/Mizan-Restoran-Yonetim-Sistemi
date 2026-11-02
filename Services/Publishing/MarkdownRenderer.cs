using Markdig;

namespace RestoranYonetim.Services.Publishing;

// Panelden girilen Markdown'ı HTML'e çevirir. Ham HTML kapalıdır (DisableHtml) - metne yazılan etiketler kaçışlanır, XSS oluşmaz.
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public static string ToSafeHtml(string markdown) => Markdown.ToHtml(markdown ?? string.Empty, Pipeline);
}
