namespace Rune.Wpf.Examples;

/// <summary>
/// Icon 的示例（vue-tsx-pro 文档站 base/Icon）。文档站的 with-text 要等 Text、Button、Badge，draw 等描线动画，到时补上。
/// 示例里的排版先用平台自带的面板与 TextBlock，Flex、Grid、Text 做出来后换掉。
/// </summary>
static class IconExamples
{
    public static Example[] All =>
    [
        new("Icon", "basic", "基础用法", "从 Lucide 类按名取图标交给 Data；全部图标都在 Lucide 类里，第一次用到时才创建。", () => new IconBasic()),
        new("Icon", "sizes", "尺寸与线宽", "不写 Size 时是 1em，跟随所在文字；档位对应主题字号，数字是像素。StrokeWidth 调线宽，大图标用细一点的线更精致。", () => new IconSizes()),
        new("Icon", "tones", "色调", "不写 Tone 时跟随文字颜色；状态图标用 success、warning、danger 等色调，和提示条、标记的颜色一致。", () => new IconTones()),
        new("Icon", "direction", "从右到左", "FlipInRtl 让图标在从右到左的排版里水平翻转：箭头、chevron、“返回”这类有方向的图标要翻，时钟、勾号不翻。", () => new IconDirection()),
        new("Icon", "accessibility", "装饰与含义", "旁边已有文字时图标只是装饰，默认对读屏隐藏；单独出现、表达含义的图标要给 AutomationProperties.Name。", () => new IconAccessibility()),
        new("Icon", "custom", "自定义图标", "按 Lucide 的格式写 24 格画布上的路径；实心的部分写在 fillPath 里，颜色仍跟随文字。", () => new IconCustom()),
    ];
}
