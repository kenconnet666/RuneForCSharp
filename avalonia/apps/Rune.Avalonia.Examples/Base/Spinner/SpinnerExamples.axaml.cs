using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Spinner 的示例（vue-tsx-pro 文档站 feedback/Progress 页的 spinner 一节）。</summary>
static class SpinnerExamples
{
    public static Example[] All =>
    [
        new("Progress", "spinner", "旋转指示", "Spinner 默认跟随所在文字的大小与颜色，旁边有文字时是装饰；单独出现时写 Label，读屏当作进度条念出来。", () => new SpinnerExample()),
    ];
}

/// <summary>Progress/spinner（上游的 Spinner 示例在 Progress 页）：大小、跟随文字、带说明。</summary>
public partial class SpinnerExample : UserControl
{
    /// <summary>加载示例。</summary>
    public SpinnerExample() => InitializeComponent();
}
