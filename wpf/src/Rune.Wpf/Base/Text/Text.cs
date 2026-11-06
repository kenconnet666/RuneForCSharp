using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf;

/// <summary>
/// 正文文字（vue-tsx-pro 的 Text）：字号、字重、色调、对齐、截断。<see cref="Paragraph"/>、<see cref="Strong"/>、
/// <see cref="Em"/>、<see cref="Small"/> 是它的子类，各有自己的默认值（写在主题样式里，使用方的样式能改）。
/// <code>
/// &lt;rune:Text Tone="muted" Size="sm"&gt;更新于 3 分钟前&lt;/rune:Text&gt;
/// &lt;rune:Paragraph LineClamp="2"&gt;很长的描述…&lt;/rune:Paragraph&gt;
/// &lt;rune:Text Mono="True" Size="sm"&gt;PO-2026-0187&lt;/rune:Text&gt;    编号、件号：等宽字体
/// &lt;rune:Text Numeric="True" Tone="danger"&gt;超时 3 天&lt;/rune:Text&gt;  金额、计数：等宽数字
/// </code>
/// 从 TextBlock 派生（ROADMAP 决定 19）：行内内容、换行、截断、读屏都现成。不写的属性都继承外面；
/// 写了的档位在代码里设成主题资源的引用（本地值，同 DynamicResource），清掉时回到继承。换某个色调的颜色改主题资源。
/// </summary>
public class Text : TextBlock
{
    /// <summary>字号：档位（xs–xl，取主题字号）或像素数；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(Text), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    /// <summary>字重档位；不写时继承。</summary>
    public static readonly DependencyProperty WeightProperty = DependencyProperty.Register(
        nameof(Weight), typeof(TextWeight), typeof(Text), new FrameworkPropertyMetadata(default(TextWeight), (d, _) => ((Text)d).ReferenceResource(FontWeightProperty, ((Text)d).Weight.ResourceKey)));

    /// <summary>色调；不写时继承外面的文字颜色。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(Tone), typeof(Text), new FrameworkPropertyMetadata(default(Tone), (d, _) => ((Text)d).ReferenceResource(ForegroundProperty, ((Text)d).Tone.BrushKey)));

    /// <summary>水平对齐（按书写方向）；不写时继承。</summary>
    public static readonly DependencyProperty AlignProperty = DependencyProperty.Register(
        nameof(Align), typeof(TextAlign?), typeof(Text), new FrameworkPropertyMetadata(null, (d, _) => ((Text)d).UpdateAlignment()));

    /// <summary>单行省略。</summary>
    public static readonly DependencyProperty TruncateProperty = DependencyProperty.Register(
        nameof(Truncate), typeof(bool), typeof(Text), new FrameworkPropertyMetadata(false, (d, _) => ((Text)d).UpdateWrapping()));

    /// <summary>最多显示几行，超出省略。</summary>
    public static readonly DependencyProperty LineClampProperty = DependencyProperty.Register(
        nameof(LineClamp), typeof(int?), typeof(Text), new FrameworkPropertyMetadata(null, (d, _) => ((Text)d).UpdateWrapping()));

    /// <summary>换行方式；不写时换行（同浏览器里的文字）。</summary>
    public static readonly DependencyProperty WrapProperty = DependencyProperty.Register(
        nameof(Wrap), typeof(TextWrap?), typeof(Text), new FrameworkPropertyMetadata(null, (d, _) => ((Text)d).UpdateWrapping()));

    /// <summary>数字等宽（Typography.NumeralAlignment = Tabular）：金额、计数、日期上下对齐，变化时不跳动。</summary>
    public static readonly DependencyProperty NumericProperty = DependencyProperty.Register(
        nameof(Numeric), typeof(bool), typeof(Text), new FrameworkPropertyMetadata(false, OnNumericChanged));

    /// <summary>等宽字体（主题的 fonts.mono）：编号、件号、代码片段。</summary>
    public static readonly DependencyProperty MonoProperty = DependencyProperty.Register(
        nameof(Mono), typeof(bool), typeof(Text), new FrameworkPropertyMetadata(false, (d, _) => ((Text)d).ReferenceResource(FontFamilyProperty, ((Text)d).Mono ? "Rune.Font.Mono" : null)));

    static Text()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Text), new FrameworkPropertyMetadata(typeof(Text)));
        // 限制行数的高度按行高算，字号、字体、行高变了要重算（TextBlock 的 OnPropertyChanged 是密封的，补在元数据的回调里；回调与基类的合并）
        PropertyChangedCallback relayout = (d, _) =>
        {
            if (((Text)d).LineClamp is not null) ((Text)d).UpdateWrapping();
        };
        FontSizeProperty.OverrideMetadata(typeof(Text), new FrameworkPropertyMetadata(relayout));
        FontFamilyProperty.OverrideMetadata(typeof(Text), new FrameworkPropertyMetadata(relayout));
        LineHeightProperty.OverrideMetadata(typeof(Text), new FrameworkPropertyMetadata(relayout));
    }

    /// <summary>字号。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>字重。</summary>
    public TextWeight Weight { get => (TextWeight)GetValue(WeightProperty); set => SetValue(WeightProperty, value); }

    /// <summary>色调。</summary>
    public Tone Tone { get => (Tone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>水平对齐。</summary>
    public TextAlign? Align { get => (TextAlign?)GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>单行省略。</summary>
    public bool Truncate { get => (bool)GetValue(TruncateProperty); set => SetValue(TruncateProperty, value); }

    /// <summary>最多显示几行。</summary>
    public int? LineClamp { get => (int?)GetValue(LineClampProperty); set => SetValue(LineClampProperty, value); }

    /// <summary>换行方式。</summary>
    public TextWrap? Wrap { get => (TextWrap?)GetValue(WrapProperty); set => SetValue(WrapProperty, value); }

    /// <summary>数字等宽。</summary>
    public bool Numeric { get => (bool)GetValue(NumericProperty); set => SetValue(NumericProperty, value); }

    /// <summary>等宽字体。</summary>
    public bool Mono { get => (bool)GetValue(MonoProperty); set => SetValue(MonoProperty, value); }

    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var text = (Text)d;
        text.ReferenceResource(FontSizeProperty, text.Size.ResourceKey);
        if (text.Size.Pixels is { } pixels) text.SetValue(FontSizeProperty, pixels);
    }

    static void OnNumericChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue) d.SetValue(Typography.NumeralAlignmentProperty, FontNumeralAlignment.Tabular);
        else d.ClearValue(Typography.NumeralAlignmentProperty);
    }

    /// <summary>设成主题资源的引用；key 为 null 时清掉本地值，回到继承或样式里的值。</summary>
    void ReferenceResource(DependencyProperty property, string? key)
    {
        if (key is null) ClearValue(property);
        else SetResourceReference(property, key);
    }

    /// <summary>TextAlignment 的 Left、Right 跟着 FlowDirection 镜像，就是起始边、结束边。</summary>
    void UpdateAlignment()
    {
        if (Align is { } align) TextAlignment = align switch { TextAlign.Center => TextAlignment.Center, TextAlign.End => TextAlignment.Right, _ => TextAlignment.Left };
        else ClearValue(TextAlignmentProperty);
    }

    /// <summary>
    /// truncate 优先于 lineClamp，二者都没有时看 wrap。TextBlock 没有最大行数：按行高限制 MaxHeight，
    /// 换行加 CharacterEllipsis 时最后一行放不下的部分变成省略号。
    /// </summary>
    void UpdateWrapping()
    {
        ClearValue(TextWrappingProperty);
        ClearValue(TextTrimmingProperty);
        ClearValue(MaxHeightProperty);
        if (Truncate)
        {
            TextWrapping = TextWrapping.NoWrap;
            TextTrimming = TextTrimming.CharacterEllipsis;
        }
        else if (LineClamp is { } lines)
        {
            TextWrapping = TextWrapping.Wrap;
            TextTrimming = TextTrimming.CharacterEllipsis;
            var lineHeight = double.IsNaN(LineHeight) ? FontFamily.LineSpacing * FontSize : LineHeight;
            MaxHeight = Math.Ceiling(lines * lineHeight);
        }
        else if (Wrap is { } wrap)
        {
            TextWrapping = wrap == TextWrap.NoWrap ? TextWrapping.NoWrap : TextWrapping.Wrap;
        }
    }
}

/// <summary>段落（vue-tsx-pro 的 Paragraph）：默认换行（TS 的 pretty），段落之间的距离交给布局。</summary>
public class Paragraph : Text
{
    static Paragraph() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Paragraph), new FrameworkPropertyMetadata(typeof(Paragraph)));
}

/// <summary>重要的内容（vue-tsx-pro 的 Strong）：默认字重 semibold。</summary>
public class Strong : Text
{
    static Strong() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Strong), new FrameworkPropertyMetadata(typeof(Strong)));
}

/// <summary>语气上的强调（vue-tsx-pro 的 Em）：斜体。</summary>
public class Em : Text
{
    static Em() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Em), new FrameworkPropertyMetadata(typeof(Em)));
}

/// <summary>附注、版权、细则（vue-tsx-pro 的 Small）：默认字号 xs。</summary>
public class Small : Text
{
    static Small() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Small), new FrameworkPropertyMetadata(typeof(Small)));
}
