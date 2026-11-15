using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Rune.Avalonia.Examples;

/// <summary>DocsKit 页的示例（vue-tsx-pro 文档站 docs-kit/DocsKit）：属性表与色块；文章、代码预览这几个示例站自己就在用。</summary>
static class DocsKitExamples
{
    public static Example[] All =>
    [
        new("DocsKit", "handwritten", "手写的属性表", "给自己的组件手写 API 数据：名称、种类、类型、必填、默认值、可绑定、说明，档位类的列出每个可选值。外观属性同 Table：Density 默认 compact，这里加了斑马纹。", Handwritten),
        new("DocsKit", "swatches", "色块", "ColorSwatch 列出主题的颜色：颜色绑主题的画刷资源（DynamicResource），换主题时色块与取值跟着变；半透明的颜色（surfaceTranslucent）下面垫棋盘格。", Swatches),
    ];

    static Control Handwritten() => new PropsTable
    {
        Caption = "Rating 的属性",
        Striped = true,
        Props =
        [
            new("Value", ApiPropKind.Prop, "double", "当前评分，父级写 Value=\"{Binding Score}\"") { Bindable = true, Default = "0" },
            new("Count", ApiPropKind.Prop, "int", "星星的个数") { Default = "5" },
            new("Size", ApiPropKind.Prop, "TextSize", "尺寸")
            {
                Default = "Md",
                Values = [new("Sm", "表格、列表里"), new("Md", "表单里（默认）"), new("Lg", "详情页的总评分")],
            },
            new("Label", ApiPropKind.Prop, "string", "评分的名称，读屏念的是它") { Required = true },
        ],
    };

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
