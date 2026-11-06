using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 正文文字（vue-tsx-pro 的 Text）：字号、字重、色调、对齐、截断。<see cref="Paragraph"/>、<see cref="Strong"/>、
/// <see cref="Em"/>、<see cref="Small"/> 是它的子类，各有自己的默认值（写在主题里，使用方的样式能改）。
/// <code>
/// &lt;rune:Text Tone="muted" Size="sm"&gt;更新于 3 分钟前&lt;/rune:Text&gt;
/// &lt;rune:Paragraph LineClamp="2"&gt;很长的描述…&lt;/rune:Paragraph&gt;
/// &lt;rune:Text Mono="True" Size="sm"&gt;PO-2026-0187&lt;/rune:Text&gt;    编号、件号：等宽字体
/// &lt;rune:Text Numeric="True" Tone="danger"&gt;超时 3 天&lt;/rune:Text&gt;  金额、计数：等宽数字
/// </code>
/// 不写的属性都继承外面（字号、字重、颜色是继承属性）。文字用得多，档位不写成主题里的嵌套样式（DESIGN §9：
/// 嵌套样式多了创建明显变慢），而是写了才在代码里绑定主题资源（本地值）；对齐、换行、截断写了才设到 TextBlock 上。
/// 换某个色调的颜色改主题资源（<c>Rune.Brush.TextMuted</c>），不用写样式。
/// </summary>
public class Text : TextBlock
{
    /// <summary>字号：档位（xs–xl，取主题字号）或像素数；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Text, TextSize>(nameof(Size));

    /// <summary>字重档位；不写时继承。</summary>
    public static readonly StyledProperty<TextWeight> WeightProperty = AvaloniaProperty.Register<Text, TextWeight>(nameof(Weight));

    /// <summary>色调；不写时继承外面的文字颜色。</summary>
    public static readonly StyledProperty<Tone> ToneProperty = AvaloniaProperty.Register<Text, Tone>(nameof(Tone));

    /// <summary>水平对齐（按书写方向）；不写时继承。</summary>
    public static readonly StyledProperty<TextAlign?> AlignProperty = AvaloniaProperty.Register<Text, TextAlign?>(nameof(Align));

    /// <summary>单行省略。</summary>
    public static readonly StyledProperty<bool> TruncateProperty = AvaloniaProperty.Register<Text, bool>(nameof(Truncate));

    /// <summary>最多显示几行，超出省略。</summary>
    public static readonly StyledProperty<int?> LineClampProperty = AvaloniaProperty.Register<Text, int?>(nameof(LineClamp));

    /// <summary>换行方式；不写时换行（同浏览器里的文字）。</summary>
    public static readonly StyledProperty<TextWrap?> WrapProperty = AvaloniaProperty.Register<Text, TextWrap?>(nameof(Wrap));

    /// <summary>数字等宽（OpenType 的 tnum）：金额、计数、日期上下对齐，变化时不跳动。</summary>
    public static readonly StyledProperty<bool> NumericProperty = AvaloniaProperty.Register<Text, bool>(nameof(Numeric));

    /// <summary>等宽字体（主题的 fonts.mono）：编号、件号、代码片段。</summary>
    public static readonly StyledProperty<bool> MonoProperty = AvaloniaProperty.Register<Text, bool>(nameof(Mono));

    static readonly FontFeatureCollection TabularNumbers = FontFeatureCollection.Parse("tnum");

    readonly Dictionary<AvaloniaProperty, IDisposable> resourceBindings = [];

    /// <summary>字号。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>字重。</summary>
    public TextWeight Weight { get => GetValue(WeightProperty); set => SetValue(WeightProperty, value); }

    /// <summary>色调。</summary>
    public Tone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>水平对齐。</summary>
    public TextAlign? Align { get => GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>单行省略。</summary>
    public bool Truncate { get => GetValue(TruncateProperty); set => SetValue(TruncateProperty, value); }

    /// <summary>最多显示几行。</summary>
    public int? LineClamp { get => GetValue(LineClampProperty); set => SetValue(LineClampProperty, value); }

    /// <summary>换行方式。</summary>
    public TextWrap? Wrap { get => GetValue(WrapProperty); set => SetValue(WrapProperty, value); }

    /// <summary>数字等宽。</summary>
    public bool Numeric { get => GetValue(NumericProperty); set => SetValue(NumericProperty, value); }

    /// <summary>等宽字体。</summary>
    public bool Mono { get => GetValue(MonoProperty); set => SetValue(MonoProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            if (change.GetOldValue<TextSize>().Pixels is not null) ClearValue(FontSizeProperty);
            BindResource(FontSizeProperty, Size.ResourceKey);
            if (Size.Pixels is { } pixels) SetValue(FontSizeProperty, pixels);
        }
        else if (change.Property == WeightProperty) BindResource(FontWeightProperty, Weight.ResourceKey);
        else if (change.Property == ToneProperty) BindResource(ForegroundProperty, Tone.BrushKey);
        else if (change.Property == MonoProperty) BindResource(FontFamilyProperty, Mono ? "Rune.Font.Mono" : null);
        else if (change.Property == NumericProperty) Set(FontFeaturesProperty, Numeric ? TabularNumbers : null);
        else if (change.Property == AlignProperty) SetStruct(TextAlignmentProperty, Align switch
        {
            TextAlign.Start => TextAlignment.Start,
            TextAlign.Center => TextAlignment.Center,
            TextAlign.End => TextAlignment.End,
            _ => null,
        });
        else if (change.Property == TruncateProperty || change.Property == LineClampProperty || change.Property == WrapProperty) UpdateWrapping();
    }

    /// <summary>truncate 优先于 lineClamp，二者都没有时看 wrap。</summary>
    void UpdateWrapping()
    {
        var (wrapping, trimming, lines) = (Truncate, LineClamp, Wrap) switch
        {
            (true, _, _) => (TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, (int?)null),
            (_, { } count, _) => (TextWrapping.Wrap, TextTrimming.CharacterEllipsis, count),
            (_, _, TextWrap.NoWrap) => (TextWrapping.NoWrap, null, null),
            (_, _, { }) => (TextWrapping.Wrap, null, null),
            _ => ((TextWrapping?)null, (TextTrimming?)null, (int?)null),
        };
        SetStruct(TextWrappingProperty, wrapping);
        Set(TextTrimmingProperty, trimming);
        SetStruct(MaxLinesProperty, lines);
    }

    /// <summary>绑定到主题资源（本地值）；key 为 null 时解绑，回到继承或主题里的值。</summary>
    void BindResource(AvaloniaProperty property, string? key)
    {
        if (resourceBindings.Remove(property, out var old))
        {
            old.Dispose();
            ClearValue(property);
        }
        if (key is not null) resourceBindings[property] = Bind(property, this.GetResourceObservable(key));
    }

    void Set<T>(StyledProperty<T> property, T? value) where T : class?
    {
        if (value is null) ClearValue(property);
        else SetValue(property, value);
    }

    void SetStruct<T>(StyledProperty<T> property, T? value) where T : struct
    {
        if (value is { } set) SetValue(property, set);
        else ClearValue(property);
    }
}

/// <summary>段落（vue-tsx-pro 的 Paragraph）：默认换行（TS 的 pretty），段落之间的距离交给布局。</summary>
public class Paragraph : Text;

/// <summary>重要的内容（vue-tsx-pro 的 Strong）：默认字重 semibold。</summary>
public class Strong : Text;

/// <summary>语气上的强调（vue-tsx-pro 的 Em）：斜体。</summary>
public class Em : Text;

/// <summary>附注、版权、细则（vue-tsx-pro 的 Small）：默认字号 xs。</summary>
public class Small : Text;
