using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 状态色点（vue-tsx-pro 的 StatusDot）：一个小圆点标出状态或类别（未读、新到、退回、图例），比 Badge 更轻。
/// 无窗口元素，放进窗体用 <see cref="StatusDotControl"/>；列表、图例里数量大时直接用元素。
/// 颜色：写了 <see cref="Color"/> 时用它，否则按色调取主题的实色（中性是 textMuted）。
/// </summary>
public class StatusDot : Element
{
    /// <summary>色调；不写时 accent。</summary>
    public StatusTone Tone { get => field.Value is null ? StatusTone.Accent : field; set { field = value; Invalidate(); } }

    /// <summary>任意颜色；写了时不看色调。</summary>
    public Color? Color { get; set { field = value; Invalidate(); } }

    /// <summary>大小；不写时 sm。</summary>
    public StatusDotSize Size { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>当前主题里的颜色。</summary>
    Color Resolve(ElementContext context)
    {
        if (Color is { } color) return color;
        var c = context.Theme.Colors;
        return Tone.Value switch
        {
            "neutral" => c.TextMuted,
            "info" => c.Info,
            "success" => c.Success,
            "warning" => c.Warning,
            "danger" => c.Danger,
            _ => c.Accent,
        };
    }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var d = (int)MathF.Ceiling(context.Scale(Size.Pixels));
        return new Size(d, d);
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var d = context.Scale(Size.Pixels);
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Resolve(context));
            graphics.FillEllipse(brush, Bounds.X + (Bounds.Width - d) / 2, Bounds.Y + (Bounds.Height - d) / 2, d, d);
        }
        finally
        {
            graphics.Restore(state);
        }
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
    public float Pixels => Value == "md" ? 10 : 8;

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

/// <summary>放进窗体的状态色点（单元素宿主）。写了 AccessibleName（TS 的 label）时读屏念它，否则是装饰。</summary>
public class StatusDotControl : ElementControl<StatusDot>
{
    /// <summary>新建：不能获得焦点。</summary>
    public StatusDotControl() => SetStyle(ControlStyles.Selectable, false);

    /// <summary>色调。</summary>
    [Category("Appearance")]
    [Description("色调：neutral、accent、info、success、warning、danger，默认 accent。")]
    public StatusTone Tone { get => Element.Tone; set => Element.Tone = value; }

    /// <summary>任意颜色；写了时不看色调。</summary>
    [Category("Appearance")]
    [Description("任意颜色；写了时不看色调。")]
    [DefaultValue(null)]
    public Color? Color { get => Element.Color; set => Element.Color = value; }

    /// <summary>大小。</summary>
    [Category("Appearance")]
    [Description("大小：sm（8px）、md（10px），默认 sm。")]
    public StatusDotSize DotSize { get => Element.Size; set => Element.Size = value; }

    bool ShouldSerializeTone() => Tone != StatusTone.Accent;

    void ResetTone() => Tone = default;

    bool ShouldSerializeDotSize() => DotSize.Value is not null;

    void ResetDotSize() => DotSize = default;

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new StatusDotAccessibleObject(this);

    /// <summary>有名字时是图像，否则是装饰（同 TS 的 aria-hidden）。</summary>
    sealed class StatusDotAccessibleObject(StatusDotControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => string.IsNullOrEmpty(owner.AccessibleName) ? AccessibleRole.None : AccessibleRole.Graphic;
    }
}
