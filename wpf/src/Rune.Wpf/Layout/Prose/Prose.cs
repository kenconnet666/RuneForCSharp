using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 长文的容器（vue-tsx-pro 的 Prose，article）：段落、标题、列表、代码块依次排下来时的间距、行长与行高。
/// <code>
/// &lt;rune:Prose&gt;
///   &lt;rune:H1&gt;介绍&lt;/rune:H1&gt;
///   &lt;rune:Paragraph&gt;…&lt;/rune:Paragraph&gt;
/// &lt;/rune:Prose&gt;
/// </code>
/// 是一个竖排的 Flex：块之间默认 space.lg（1em），子元素各自不带外边距；行高 relaxed（主题样式）；<see cref="LimitLineLength"/> 时
/// 最宽约 70 个字宽（70ch，没写 MaxWidth 时强制成它）。TS 的 measure 在这里叫 LimitLineLength（Measure 是布局的方法）。读屏里是文档。
/// </summary>
public class Prose : Flex
{
    /// <summary>限制行长（约 70 个字宽），默认 true；放在窄栏里时可以关掉。</summary>
    public static readonly DependencyProperty LimitLineLengthProperty = DependencyProperty.Register(
        nameof(LimitLineLength), typeof(bool), typeof(Prose), new FrameworkPropertyMetadata(true, (d, _) => d.CoerceValue(MaxWidthProperty)));

    static Prose()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Prose), new FrameworkPropertyMetadata(typeof(Prose)));
        MaxWidthProperty.OverrideMetadata(typeof(Prose), new FrameworkPropertyMetadata(double.PositiveInfinity, null, CoerceMaxWidth));
        TextElement.FontSizeProperty.OverrideMetadata(typeof(Prose), new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => d.CoerceValue(MaxWidthProperty)));
        TextElement.FontFamilyProperty.OverrideMetadata(typeof(Prose), new FrameworkPropertyMetadata(
            SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => d.CoerceValue(MaxWidthProperty)));
    }

    /// <summary>新建：先按默认字体算一次行长。</summary>
    public Prose() => CoerceValue(MaxWidthProperty);

    /// <summary>限制行长。</summary>
    public bool LimitLineLength { get => (bool)GetValue(LimitLineLengthProperty); set => SetValue(LimitLineLengthProperty, value); }

    /// <summary>没写 MaxWidth（无穷大）时是 70ch：字体里"0"的宽度的 70 倍。</summary>
    static object CoerceMaxWidth(DependencyObject d, object baseValue)
    {
        var prose = (Prose)d;
        if (!prose.LimitLineLength || baseValue is not double value || !double.IsPositiveInfinity(value)) return baseValue;
        var typeface = new Typeface(TextElement.GetFontFamily(prose), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var zero = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, TextElement.GetFontSize(prose), Brushes.Black, 1);
        return 70 * zero.WidthIncludingTrailingWhitespace;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ProseAutomationPeer(this);

    sealed class ProseAutomationPeer(Prose owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

        protected override string GetClassNameCore() => nameof(Prose);
    }
}
