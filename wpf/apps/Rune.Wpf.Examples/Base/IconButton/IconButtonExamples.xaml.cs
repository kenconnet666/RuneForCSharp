using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>IconButton 的示例（vue-tsx-pro 文档站 base/IconButton）。focusableWhenDisabled、Tooltip、CopyButton、CloseButton、IconLinkButton 做出来后补上其余几节。</summary>
static class IconButtonExamples
{
    public static Example[] All =>
    [
        new("IconButton", "toolbar", "基础用法", "宽高相等，默认 ghost 样式、中性色（一排图标不会满屏品牌色）；外观选项同 Button。没有文字，必须写 AutomationProperties.Name，读屏才知道它是做什么的。", () => new IconButtonToolbar()),
    ];
}

/// <summary>IconButton/toolbar。</summary>
public partial class IconButtonToolbar : UserControl
{
    /// <summary>加载示例。</summary>
    public IconButtonToolbar() => InitializeComponent();
}
