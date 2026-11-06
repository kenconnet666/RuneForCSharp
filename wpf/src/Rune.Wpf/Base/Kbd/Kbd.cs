using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf;

/// <summary>
/// 按键（vue-tsx-pro 的 Kbd）：快捷键的说明，组合键写成几个 Kbd。
/// <code>
/// &lt;rune:Kbd&gt;Ctrl&lt;/rune:Kbd&gt; + &lt;rune:Kbd&gt;K&lt;/rune:Kbd&gt;
/// &lt;TextBlock&gt;按 &lt;InlineUIContainer&gt;&lt;rune:Kbd&gt;Esc&lt;/rune:Kbd&gt;&lt;/InlineUIContainer&gt; 关闭&lt;/TextBlock&gt;   &lt;!-- 放在一行文字里 --&gt;
/// </code>
/// 字号是外面的 0.85 倍（CSS 的 <c>font-size: 0.85em</c>，EmSize 按继承来的字号强制），等宽字体；其余尺寸按自己的字号算：
/// 最小宽度 1.6em、左右内边距 space.xs、圆角 radii.sm、行高 snug；描边 1px、底边 2px，看起来像一个按键。
/// </summary>
public class Kbd : ContentControl
{
    /// <summary>圆角（主题按 radii.sm 设 em 系数）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(Kbd), new FrameworkPropertyMetadata(default(CornerRadius), null, EmSize.CoerceCornerRadius));

    static Kbd()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Kbd), new FrameworkPropertyMetadata(typeof(Kbd)));
        FocusableProperty.OverrideMetadata(typeof(Kbd), new FrameworkPropertyMetadata(false));
        EmSize.Register(typeof(Kbd), CornerRadiusProperty, fontScale: 0.85);
    }

    /// <summary>圆角。</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new KbdAutomationPeer(this);

    /// <summary>读屏直接读里面的文字（同 Badge），Kbd 本身不在控件视图里。</summary>
    sealed class KbdAutomationPeer(Kbd owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;
    }
}
