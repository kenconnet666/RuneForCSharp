using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Rune.Avalonia.Examples;

/// <summary>DocsKit 页的示例（vue-tsx-pro 文档站 docs-kit/DocsKit）。目前只有色块；文章、代码预览这几个示例站自己就在用。</summary>
static class DocsKitExamples
{
    public static Example[] All =>
    [
        new("DocsKit", "swatches", "色块", "ColorSwatch 列出主题的颜色：颜色绑主题的画刷资源（DynamicResource），换主题时色块与取值跟着变；半透明的颜色（surfaceTranslucent）下面垫棋盘格。", Swatches),
    ];

    static readonly string[] Tokens = ["Accent", "AccentSoft", "Success", "Warning", "Danger", "Info", "Surface", "SurfaceTranslucent", "Text", "TextMuted", "Border", "Chart3"];

    static Control Swatches()
    {
        var grid = new Grid { MinItemWidth = "7rem", Gap = Space.Md };
        foreach (var token in Tokens)
        {
            var swatch = new ColorSwatch { Label = char.ToLowerInvariant(token[0]) + token[1..] };
            swatch[!ColorSwatch.ColorProperty] = new DynamicResourceExtension($"Rune.Brush.{token}");
            grid.Children.Add(swatch);
        }
        return grid;
    }
}
