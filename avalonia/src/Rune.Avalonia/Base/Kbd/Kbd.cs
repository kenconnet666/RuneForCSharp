using global::Avalonia;
using global::Avalonia.Automation.Peers;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Data;
using global::Avalonia.Data.Converters;
using global::Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using global::Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 按键（vue-tsx-pro 的 Kbd）：快捷键的说明，组合键写成几个 Kbd。
/// <code>
/// &lt;rune:Kbd&gt;Ctrl&lt;/rune:Kbd&gt; + &lt;rune:Kbd&gt;K&lt;/rune:Kbd&gt;
/// &lt;TextBlock&gt;按 &lt;InlineUIContainer&gt;&lt;rune:Kbd&gt;Esc&lt;/rune:Kbd&gt;&lt;/InlineUIContainer&gt; 关闭&lt;/TextBlock&gt;   &lt;!-- 放在一行文字里 --&gt;
/// </code>
/// 字号是外面的 0.85 倍（CSS 的 <c>font-size: 0.85em</c>），等宽字体；其余尺寸按自己的字号算（主题里的 em）：
/// 最小宽度 1.6em、左右内边距 space.xs、圆角 radii.sm、行高 snug；描边 1px、底边 2px，看起来像一个按键。
/// </summary>
public class Kbd : ContentControl
{
    /// <summary>字号相对外面的倍数。</summary>
    const double FontScale = 0.85;

    IDisposable? fontSize;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // 字号随外面变：绑定到父元素的（继承的）字号 × 0.85，按模板优先级，使用方带类或伪类的样式盖得过
        if (this.GetVisualParent() is { } parent)
            fontSize = Bind(FontSizeProperty, new CompiledBinding(new CompiledBindingPathBuilder().Property(TextElement.FontSizeProperty, PropertyInfoAccessorFactory.CreateAvaloniaPropertyAccessor).Build())
            {
                Source = parent,
                Converter = new FuncValueConverter<double, double>(size => size * FontScale),
                Priority = BindingPriority.Template,
            });
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new KbdAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        fontSize?.Dispose();
        fontSize = null;
    }

    /// <summary>读屏直接读里面的文字（同 Badge），Kbd 本身不在控件视图里。</summary>
    sealed class KbdAutomationPeer(Kbd owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;
    }
}
