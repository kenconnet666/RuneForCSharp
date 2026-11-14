using System.ComponentModel;
using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Automation.Peers;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using LayoutSize = global::Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 状态色点（vue-tsx-pro 的 StatusDot）：一个小圆点标出状态或类别（未读、新到、退回、图例），比 Badge 更轻。
/// <code>
/// &lt;rune:StatusDot Tone="success" Label="在线" /&gt;
/// &lt;rune:StatusDot Color="#8e4ec6" /&gt;   &lt;!-- 图例里的任意颜色 --&gt;
/// </code>
/// 颜色：写了 <see cref="Color"/> 时用它，否则按色调取主题的实色（中性是 textMuted）。写了 <see cref="Label"/> 时读屏念它，
/// 不写时色点只是装饰，不在控件视图里。数量大的轻量部件：自己画（DESIGN §9）。
/// </summary>
public class StatusDot : Control
{
    /// <summary>色调，默认 accent。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<StatusDot, StatusTone>(nameof(Tone));

    /// <summary>任意颜色；写了时不看色调。</summary>
    public static readonly StyledProperty<IBrush?> ColorProperty = AvaloniaProperty.Register<StatusDot, IBrush?>(nameof(Color));

    /// <summary>大小，默认 sm（0.5rem）。</summary>
    public static readonly StyledProperty<StatusDotSize> SizeProperty = AvaloniaProperty.Register<StatusDot, StatusDotSize>(nameof(Size));

    /// <summary>读屏念的含义；不写时色点只是装饰。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<StatusDot, string?>(nameof(Label));

    /// <summary>色调解析出的画刷（主题资源，主题换了跟着变）。</summary>
    static readonly StyledProperty<IBrush?> ToneBrushProperty = AvaloniaProperty.Register<StatusDot, IBrush?>("ToneBrush");

    static StatusDot()
    {
        AffectsRender<StatusDot>(ColorProperty, ToneBrushProperty);
        AffectsMeasure<StatusDot>(SizeProperty);
    }

    /// <summary>新建色点：先按默认色调（accent）取色。</summary>
    public StatusDot() => Resolved.Bind(this, ToneBrushProperty, BrushKey(default));

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>任意颜色。</summary>
    public IBrush? Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>大小。</summary>
    public StatusDotSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>读屏念的含义。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty) Resolved.Bind(this, ToneBrushProperty, BrushKey(Tone));
        else if (change.Property == LabelProperty) AutomationProperties.SetName(this, Label);
    }

    /// <summary>色调的实色：中性用 textMuted，其余用色调的实色；不写时 accent。</summary>
    internal static string BrushKey(StatusTone tone) => tone.Value switch
    {
        null => ThemeKeys.Brush.Accent,
        "neutral" => ThemeKeys.Brush.TextMuted,
        var name => $"Rune.Brush.{char.ToUpperInvariant(name[0])}{name[1..]}",
    };

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize) => new(Size.Pixels, Size.Pixels);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var d = Size.Pixels;
        context.DrawEllipse(Color ?? GetValue(ToneBrushProperty), null, new Point(Bounds.Width / 2, Bounds.Height / 2), d / 2, d / 2);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new StatusDotAutomationPeer(this);

    /// <summary>写了含义时是一个图像（读屏念含义），否则是装饰，不在控件视图里。</summary>
    sealed class StatusDotAutomationPeer(StatusDot owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsControlElementCore() => owner.Label is not null;

        protected override bool IsContentElementCore() => owner.Label is not null;
    }
}

/// <summary>状态色点的大小（vue-tsx-pro 的 StatusDotSizes）。</summary>
/// <param name="Value">取值（sm、md）。</param>
[TypeConverter(typeof(VariantConverter<StatusDotSize>))]
public readonly record struct StatusDotSize(string Value) : IVariant<StatusDotSize>
{
    /// <summary>0.5rem（8px）：默认。</summary>
    public static readonly StatusDotSize Sm = new("sm");

    /// <summary>0.625rem（10px）。</summary>
    public static readonly StatusDotSize Md = new("md");

    /// <inheritdoc />
    public static IReadOnlyList<StatusDotSize> Known { get; } = [Sm, Md];

    /// <summary>直径（设备无关像素，rem 按根字号 16 换算）；不写时 sm。</summary>
    public double Pixels => Value == "md" ? 10 : 8;

    /// <inheritdoc />
    public static StatusDotSize Parse(string value)
    {
        var size = new StatusDotSize(value);
        Variants.WarnIfUnknown(size);
        return size;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
