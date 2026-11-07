using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 标记（vue-tsx-pro 的 Badge）：状态、计数、分类等短文字，只展示、不可交互。无窗口元素，放进窗体用 <see cref="BadgeControl"/>；
/// 列表、表格里数量大时直接用元素。
/// <code>
/// new BadgeControl { Text = "已发货", Tone = StatusTone.Success };
/// new BadgeControl { Text = "12", Variant = StatusVariant.Solid, Tone = StatusTone.Danger, Radius = Radius.Full };
/// </code>
/// 底色、描边、圆点自己画，图标与文字是组合进来的 Icon、Text 元素；图标部件按 <see cref="PropsIcon"/> 逐字段合并（DESIGN §7）。
/// 档位不写（default）时取组件的默认值：soft、neutral、xs、sm。尺寸都是 em：内边距 0.5em、间距 0.25em、最小高度 1.75em、圆点 0.5em。
/// </summary>
public class Badge : Element
{
    readonly Text text = new() { Weight = TextWeight.Medium, Wrap = TextWrap.NoWrap };
    Icon? icon;
    StatusVariant variant;
    StatusTone tone;
    TextSize size;
    Radius radius;

    /// <summary>文字内容（TS 的 children）。</summary>
    public string Content { get => text.Content; set { text.Content = value; InvalidateMeasure(); } }

    /// <summary>样式；不写时取作用域的默认值（<see cref="BadgeDefaults"/>），再没有时 soft。</summary>
    public StatusVariant Variant { get => variant.Value is not null ? variant : ScopeDefaults?.Variant ?? StatusVariant.Soft; set { variant = value; Invalidate(); } }

    /// <summary>色调；不写时取作用域的默认值，再没有时 neutral。</summary>
    public StatusTone Tone { get => tone.Value is not null ? tone : ScopeDefaults?.Tone ?? StatusTone.Neutral; set { tone = value; Invalidate(); } }

    /// <summary>字号档位；不写时取作用域的默认值，再没有时 xs。</summary>
    public TextSize Size { get => size.Value is not null ? size : ScopeDefaults?.Size ?? TextSize.Xs; set { size = value; InvalidateMeasure(); } }

    /// <summary>圆角；不写时取作用域的默认值，再没有时 sm。表格、密集列表里的状态常用 none。</summary>
    public Radius Radius { get => radius.Value is not null ? radius : ScopeDefaults?.Radius ?? Radius.Sm; set { radius = value; Invalidate(); } }

    /// <summary>外观覆盖（上游的 sx）：盖过作用域的 <see cref="BadgeSx"/> 与配方。</summary>
    public BadgeSx? Sx { get; set { field = value; Invalidate(); } }

    /// <summary>实例上写过的档位（设计器只序列化这些）。</summary>
    internal (bool Variant, bool Tone, bool Size, bool Radius) Explicit => (variant.Value is not null, tone.Value is not null, size.Value is not null, radius.Value is not null);

    BadgeDefaults? ScopeDefaults => Scope?.ResolveDefaults<BadgeDefaults>();

    /// <summary>合并好的外观覆盖：作用域的在下，实例的在上。</summary>
    BadgeSx? ResolvedSx => Scope?.ResolveStyles<BadgeSx>() is { } scoped ? scoped.Merge(Sx) : Sx;

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get; set { field = value; Rebuild(); } }

    /// <summary>文字前一个同色的圆点（没有图标时）。</summary>
    public bool Dot { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>图标部件的属性：写了的字段盖住 Badge 给的图标。</summary>
    public IconProps? PropsIcon { get; set { field = value; Rebuild(); } }

    /// <summary>圆点部件的属性：写了的字段盖住 Badge 给的（前景色、0.5em）。</summary>
    public DotProps? PropsDot { get; set { field = value; InvalidateMeasure(); } }

    float DotSize => PropsDot?.Size ?? 0.5f;

    /// <summary>合并好的图标部件；没有图标时为 null（测试用）。</summary>
    internal Icon? IconPart => icon;

    void Rebuild()
    {
        icon = Icon is null ? null : new Icon();
        if (icon is not null) new IconProps { Data = Icon }.Merge(PropsIcon).ApplyTo(icon);
        InvalidateMeasure();
    }

    /// <summary>Badge 里面的环境：字体换成 Badge 的字号，文字颜色换成样式 × 色调的前景色。</summary>
    (ElementContext Inner, (Color? Background, Color Foreground, Color? Border) Colors) Resolve(ElementContext context)
    {
        var colors = BadgeRecipe.Colors(Variant.Value, Tone.Value, hover: false, context.Theme.Colors);
        if (ResolvedSx is { } sx) colors = (sx.Background ?? colors.Background, sx.Foreground ?? colors.Foreground, sx.BorderColor ?? colors.Border);
        var points = (Size.Pixels ?? Tokens.FontSize.Xs) * 72 / 96;
        var font = Fonts.Get(context.Font.FontFamily.Name, points, context.Font.Style);
        return (context with { Font = font, ForeColor = colors.Foreground }, colors);
    }

    IEnumerable<Element> Parts() => icon is null ? [text] : [icon, text];

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var (inner, _) = Resolve(context);
        var em = inner.Em;
        var unbounded = new Size(int.MaxValue, int.MaxValue);
        var sizes = Parts().Select(part => part.Measure(inner, unbounded)).ToList();
        var leading = icon is null && Dot ? DotSize * em + 0.25f * em : 0;
        var width = sizes.Sum(size => size.Width) + (sizes.Count - 1) * 0.25f * em + leading + 2 * (0.5f * em + BorderWidth(inner));
        var height = Math.Max(1.75f * em, sizes.Max(size => size.Height) + 2 * BorderWidth(inner));
        return new Size((int)MathF.Ceiling(width), (int)MathF.Ceiling(height));
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var (inner, colors) = Resolve(context);
        var em = inner.Em;
        var border = BorderWidth(inner);
        var bounds = new RectangleF(Bounds.X + border / 2, Bounds.Y + border / 2, Bounds.Width - border, Bounds.Height - border);
        var radius = Math.Min(((ResolvedSx?.Radius ?? Radius).In(inner.Theme.Radii) ?? 0) * em, bounds.Height / 2);
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var outline = Drawing.RoundedRectangle(bounds, radius);
            if (colors.Background is { } background)
            {
                using var brush = new SolidBrush(background);
                graphics.FillPath(brush, outline);
            }
            if (colors.Border is { } stroke)
            {
                using var pen = new Pen(stroke, border);
                graphics.DrawPath(pen, outline);
            }
            var x = Bounds.X + border + 0.5f * em;
            if (icon is null && Dot)
            {
                var dot = DotSize * em;
                using var brush = new SolidBrush(PropsDot?.Color ?? colors.Foreground);
                graphics.FillEllipse(brush, x, Bounds.Y + (Bounds.Height - dot) / 2, dot, dot);
                x += dot + 0.25f * em;
            }
            foreach (var part in Parts())
            {
                var size = part.Measure(inner, new Size(int.MaxValue, int.MaxValue));
                part.Bounds = new Rectangle((int)MathF.Round(x), Bounds.Y + (Bounds.Height - size.Height) / 2, size.Width, size.Height);
                part.Paint(graphics, inner);
                x += size.Width + 0.25f * em;
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>描边宽度：borderWidths.xs（设备无关像素）。没有描边色时同样占位，各样式等高。</summary>
    static float BorderWidth(ElementContext context) => context.Scale(Tokens.BorderWidth.Xs);
}

/// <summary>Badge 圆点部件的属性（TS 的 propsDot）：字段可空，null 是"不改"。圆点是画出来的，能改的只有颜色与大小。</summary>
public sealed record DotProps
{
    /// <summary>颜色；不写时同文字的颜色。</summary>
    public Color? Color { get; init; }

    /// <summary>直径（em 系数）；不写时 0.5em。</summary>
    public float? Size { get; init; }
}

/// <summary>
/// 放进窗体、设计器里用的 <see cref="Badge"/>：文字内容就是控件的 <see cref="Control.Text"/>，字号档位叫 <see cref="TextSize"/>
/// （控件有自己的 Size）。属性等于组件的默认值时不写进设计器代码；读屏读成静态文字。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Text))]
[Description("标记：状态、计数、分类等短文字。")]
public class BadgeControl : ElementControl<Badge>
{
    /// <summary>新建：标记不能获得焦点。</summary>
    public BadgeControl() => SetStyle(ControlStyles.Selectable, false);

    /// <summary>样式。</summary>
    [Category("Appearance")]
    [Description("样式：solid、soft、surface、outline。")]
    public StatusVariant Variant { get => Element.Variant; set => Element.Variant = value; }

    /// <summary>色调。</summary>
    [Category("Appearance")]
    [Description("色调：neutral、accent、info、success、warning、danger。")]
    public StatusTone Tone { get => Element.Tone; set => Element.Tone = value; }

    /// <summary>字号档位。</summary>
    [Category("Appearance")]
    [Description("字号：xs–xl 或像素数，默认 xs。")]
    public TextSize TextSize { get => Element.Size; set => Element.Size = value; }

    /// <summary>圆角。</summary>
    [Category("Appearance")]
    [Description("圆角：none、xs、sm、md、lg、xl、full，默认 sm。")]
    public Radius Radius { get => Element.Radius; set => Element.Radius = value; }

    /// <summary>文字前的图标。</summary>
    [Category("Appearance")]
    [Description("文字前的图标：Lucide 的图标名。")]
    [DefaultValue(null)]
    public IconData? Icon { get => Element.Icon; set => Element.Icon = value; }

    /// <summary>文字前的圆点。</summary>
    [Category("Appearance")]
    [Description("文字前一个同色的圆点（没有图标时）。")]
    [DefaultValue(false)]
    public bool Dot { get => Element.Dot; set => Element.Dot = value; }

    /// <summary>图标部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsIcon { get => Element.PropsIcon; set => Element.PropsIcon = value; }

    /// <summary>外观覆盖（代码里设，上游的 sx）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public BadgeSx? Sx { get => Element.Sx; set => Element.Sx = value; }

    /// <summary>圆点部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DotProps? PropsDot { get => Element.PropsDot; set => Element.PropsDot = value; }

    bool ShouldSerializeVariant() => Element.Explicit.Variant;

    void ResetVariant() => Variant = default;

    bool ShouldSerializeTone() => Element.Explicit.Tone;

    void ResetTone() => Tone = default;

    bool ShouldSerializeTextSize() => Element.Explicit.Size;

    void ResetTextSize() => TextSize = default;

    bool ShouldSerializeRadius() => Element.Explicit.Radius;

    void ResetRadius() => Radius = default;

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Element.Content = Text;
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new BadgeAccessibleObject(this);

    sealed class BadgeAccessibleObject(Control owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => Owner is { AccessibleRole: not AccessibleRole.Default } control ? control.AccessibleRole : AccessibleRole.StaticText;
    }
}

/// <summary>Badge 在作用域里的默认值（上游的 <c>defaults.Badge</c>）：<c>scope.Defaults.Set(new BadgeDefaults { Radius = Radius.Full });</c></summary>
public sealed record BadgeDefaults : IScopeRecord<BadgeDefaults>
{
    /// <summary>样式。</summary>
    public StatusVariant? Variant { get; init; }

    /// <summary>色调。</summary>
    public StatusTone? Tone { get; init; }

    /// <summary>字号档位。</summary>
    public TextSize? Size { get; init; }

    /// <summary>圆角。</summary>
    public Radius? Radius { get; init; }

    /// <inheritdoc />
    public BadgeDefaults Merge(BadgeDefaults? over) => over is null ? this : new()
    {
        Variant = over.Variant ?? Variant,
        Tone = over.Tone ?? Tone,
        Size = over.Size ?? Size,
        Radius = over.Radius ?? Radius,
    };
}

/// <summary>Badge 的外观覆盖（上游的 <c>sx</c> 与 <c>components.Badge.styles</c>）：作用域的在下，实例的 Sx 在上，盖过配方。</summary>
public sealed record BadgeSx : IScopeRecord<BadgeSx>
{
    /// <summary>底色。</summary>
    public Color? Background { get; init; }

    /// <summary>文字、图标、圆点的颜色。</summary>
    public Color? Foreground { get; init; }

    /// <summary>描边的颜色。</summary>
    public Color? BorderColor { get; init; }

    /// <summary>圆角。</summary>
    public Radius? Radius { get; init; }

    /// <inheritdoc />
    public BadgeSx Merge(BadgeSx? over) => over is null ? this : new()
    {
        Background = over.Background ?? Background,
        Foreground = over.Foreground ?? Foreground,
        BorderColor = over.BorderColor ?? BorderColor,
        Radius = over.Radius ?? Radius,
    };
}
