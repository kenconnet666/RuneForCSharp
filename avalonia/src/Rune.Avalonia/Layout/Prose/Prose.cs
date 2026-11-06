using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Rune.Avalonia;

/// <summary>
/// 长文的容器（vue-tsx-pro 的 Prose，article）：段落、标题、列表、代码块依次排下来时的间距、行长与行高。
/// <code>
/// &lt;rune:Prose&gt;
///   &lt;rune:H1&gt;介绍&lt;/rune:H1&gt;
///   &lt;rune:Paragraph&gt;…&lt;/rune:Paragraph&gt;
/// &lt;/rune:Prose&gt;
/// </code>
/// 是一个竖排的 Flex：块之间默认 space.lg（1em），子元素各自不带外边距；行高 relaxed；<see cref="LimitLineLength"/> 时最宽约 70 个字宽（70ch）。
/// TS 的 measure 在这里叫 LimitLineLength（Measure 是布局的方法）。
/// 读屏里是文档。
/// </summary>
public class Prose : Flex
{
    /// <summary>限制行长（约 70 个字宽），默认 true；放在窄栏里时可以关掉。</summary>
    public static readonly StyledProperty<bool> LimitLineLengthProperty = AvaloniaProperty.Register<Prose, bool>(nameof(LimitLineLength), true);

    IDisposable? maxWidth;

    static Prose() => AffectsMeasure<Prose>(LimitLineLengthProperty, TextElement.FontFamilyProperty);

    /// <summary>新建：先按默认字体算一次行长（字号、字体一直没变时没有变化通知）。</summary>
    public Prose() => UpdateMaxWidth();

    /// <summary>限制行长。</summary>
    public bool LimitLineLength { get => GetValue(LimitLineLengthProperty); set => SetValue(LimitLineLengthProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LimitLineLengthProperty || change.Property == TextElement.FontSizeProperty || change.Property == TextElement.FontFamilyProperty) UpdateMaxWidth();
    }

    /// <summary>70ch：字体里"0"的宽度的 70 倍，按模板优先级设（使用方写的 MaxWidth 盖得过）。</summary>
    void UpdateMaxWidth()
    {
        maxWidth?.Dispose();
        maxWidth = null;
        if (!LimitLineLength) return;
        var typeface = new Typeface(GetValue(TextElement.FontFamilyProperty));
        using var zero = new TextLayout("0", typeface, GetValue(TextElement.FontSizeProperty), null);
        maxWidth = SetValue(MaxWidthProperty, 70 * zero.WidthIncludingTrailingWhitespace, BindingPriority.Template);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ProseAutomationPeer(this);

    sealed class ProseAutomationPeer(Prose owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;
    }
}
