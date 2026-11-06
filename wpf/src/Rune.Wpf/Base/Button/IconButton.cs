using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf;

/// <summary>
/// 只有图标的按钮（vue-tsx-pro 的 IconButton）：宽高相等，默认 ghost 样式、中性色（一排图标不会满屏品牌色），其余外观同 <see cref="Button"/>。
/// <code>
/// &lt;rune:IconButton Icon="{x:Static rune:Lucide.Settings}" AutomationProperties.Name="设置" /&gt;
/// </code>
/// 没有文字，必须写 <c>AutomationProperties.Name</c>（TS 的 aria-label），读屏才知道它是做什么的。
/// 部件（<see cref="IconButtonParts"/>）引用部件样式键，Provider 的部件样式能改。
/// </summary>
public class IconButton : ButtonCore
{
    static IconButton() => DefaultStyleKeyProperty.OverrideMetadata(typeof(IconButton), new FrameworkPropertyMetadata(typeof(IconButton)));
}

/// <summary>图标按钮的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:IconButtonParts.Icon}" TargetType="rune:Icon"&gt;</c>。</summary>
public static class IconButtonParts
{
    /// <summary>内容层（包着图标，TS 的 propsContent）。</summary>
    public static readonly PartKey Content = new(typeof(IconButton), nameof(Content), typeof(StackPanel));

    /// <summary>图标（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(IconButton), nameof(Icon), typeof(Icon));

    /// <summary>加载指示（TS 的 propsSpinner）。</summary>
    public static readonly PartKey Spinner = new(typeof(IconButton), nameof(Spinner), typeof(Spinner));
}
