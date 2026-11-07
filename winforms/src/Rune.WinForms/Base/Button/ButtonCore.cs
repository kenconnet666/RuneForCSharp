using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 按钮家族（<see cref="Button"/>、<see cref="IconButton"/>）的内核（vue-tsx-pro 的 button-core 与 ButtonBase 的效果）：
/// 外观 = 样式 × 色调 × 字号档位 × 圆角，内部尺寸都是 em；外框、波纹、加载指示、焦点环在这里画，内容由各组件排。
/// 无窗口元素，放进窗体用 <see cref="ButtonControlBase{T}"/> 的子类；宿主负责鼠标、键盘、焦点，把悬停、按下、键盘聚焦告诉元素。
/// 颜色取生成的配方（<c>ButtonRecipe</c>，tools/recipe-gen 按调色板表生成）。效果同 TS：
/// 悬停时底色在 durations.sm 内过渡（<see cref="Animator"/>）；按下时从指针（键盘时从中心）扩散文字颜色 22% 的波纹，
/// 松开淡出；加载指示防闪烁（<see cref="DelayedLoading"/>）、转动读 <see cref="FrameClock"/>；键盘聚焦时外面一圈焦点环。
/// </summary>
public abstract class ButtonCore : Element
{
    readonly Spinner spinner = new();
    readonly Signal<Color> background = new(Color.Transparent);
    readonly List<RippleState> ripples = [];
    readonly DelayedLoading delayed;
    (Color? Background, Color Foreground, Color? Border) target;
    ButtonVariant variant;
    StatusTone tone;
    TextSize size;
    Radius radius;

    /// <summary>新建按钮。</summary>
    protected ButtonCore() => delayed = new DelayedLoading(_ => InvalidateMeasure());

    /// <summary>样式；不写时取作用域的默认值（<see cref="ButtonDefaults"/>），再没有时取组件的默认（<see cref="DefaultVariant"/>）。</summary>
    public ButtonVariant Variant { get => variant.Or(ScopeDefaults?.Variant, DefaultVariant); set { variant = value; InvalidateMeasure(); } }

    /// <summary>色调；不写时取作用域的默认值，再没有时取组件的默认（<see cref="DefaultTone"/>）。</summary>
    public StatusTone Tone { get => tone.Or(ScopeDefaults?.Tone, DefaultTone); set { tone = value; Invalidate(); } }

    /// <summary>字号档位，内部尺寸随之缩放；不写时取作用域的默认值，再没有时跟随宿主的字体。</summary>
    public TextSize Size { get => size.Or(ScopeDefaults?.Size, default); set { size = value; InvalidateMeasure(); } }

    /// <summary>圆角；不写时取作用域的默认值，再没有时 md。</summary>
    public Radius Radius { get => radius.Or(ScopeDefaults?.Radius, Radius.Md); set { radius = value; Invalidate(); } }

    /// <summary>外观覆盖（上游的 sx）：盖过作用域的 <see cref="ButtonSx"/> 与配方。</summary>
    public ButtonSx? Sx { get; set { field = value; InvalidateMeasure(); Invalidate(); } }

    /// <summary>实例上写过的档位（设计器只序列化这些；没写的跟着作用域与组件默认走）。</summary>
    internal (bool Variant, bool Tone, bool Size, bool Radius) Explicit => (variant.Value is not null, tone.Value is not null, size.Value is not null, radius.Value is not null);

    ButtonDefaults? ScopeDefaults => Scope?.ResolveDefaults<ButtonDefaults>();

    /// <summary>合并好的外观覆盖：作用域的在下，实例的在上。</summary>
    ButtonSx? ResolvedSx => Scope?.ResolveStyles(Sx) ?? Sx;

    /// <summary>加载中：显示旋转指示，宽度不变，点击无效（宿主拦下）。</summary>
    public bool IsLoading { get; set { field = value; delayed.Update(value); Invalidate(); } }

    /// <summary>按下时的波纹；不写时沿用 <see cref="WinForms.Ripple.Enabled"/>。</summary>
    public bool? Ripple { get; set; }

    /// <summary>图标（Button 里在文字前）。</summary>
    public IconData? Icon { get; set { field = value; RebuildIcon(); } }

    /// <summary>图标部件的属性。</summary>
    public IconProps? PropsIcon { get; set { field = value; RebuildIcon(); } }

    /// <summary>加载指示部件的属性（图标部件，同 Spinner 的 PropsIcon）。</summary>
    public IconProps? PropsSpinner { get => spinner.PropsIcon; set => spinner.PropsIcon = value; }

    /// <summary>指针在上面（宿主设）。</summary>
    public bool IsHovered { get; internal set { if (field == value) return; field = value; Retarget(animate: true); } }

    /// <summary>键盘聚焦（宿主按 ShowFocusCues 设，同 :focus-visible）。</summary>
    public bool IsFocusVisible { get; internal set { if (field == value) return; field = value; Invalidate(); } }

    /// <summary>可用（宿主的 Enabled）。</summary>
    public bool IsEnabled { get; internal set { if (field == value) return; field = value; Invalidate(); } } = true;

    /// <summary>加载指示正在显示（加载开始 200 毫秒后，至少 500 毫秒）。</summary>
    public bool ShowsSpinner => delayed.Shown;

    /// <summary>组件的默认样式（Button 是 solid，IconButton 是 ghost）。</summary>
    protected abstract ButtonVariant DefaultVariant { get; }

    /// <summary>组件的默认色调（Button 是 accent，IconButton 是 neutral）。</summary>
    protected abstract StatusTone DefaultTone { get; }

    /// <summary>宽高相等（IconButton）：宽度取控件高度（<see cref="ControlHeight"/>），左右没有内边距。</summary>
    protected virtual bool Square => false;

    /// <summary>合成好的图标部件。</summary>
    protected internal Icon? IconPart { get; private set; }

    /// <summary>正在显示的波纹个数（测试用）。</summary>
    internal int RippleCount => ripples.Count;

    /// <summary>不可用、加载中不出波纹。</summary>
    internal bool RippleOn => (Ripple ?? WinForms.Ripple.Enabled) && IsEnabled && !IsLoading;

    void RebuildIcon()
    {
        IconPart = Icon is null ? null : new Icon();
        if (IconPart is not null) new IconProps { Data = Icon }.Merge(PropsIcon).ApplyTo(IconPart);
        InvalidateMeasure();
    }

    /// <summary>内容（图标、文字）的大小，不含内边距与描边。</summary>
    /// <param name="inner">按钮里面的环境（档位的字号、字重，配方的文字颜色）。</param>
    protected abstract SizeF MeasureContent(ElementContext inner);

    /// <summary>在 <paramref name="bounds"/>（内容区，已扣掉内边距与描边）里画内容。</summary>
    /// <param name="graphics">画布。</param>
    /// <param name="inner">按钮里面的环境。</param>
    /// <param name="bounds">内容区。</param>
    protected abstract void PaintContent(Graphics graphics, ElementContext inner, Rectangle bounds);

    /// <summary>从 <paramref name="origin"/>（元素的坐标）开始一个波纹，返回松开时调用的淡出。</summary>
    internal Action StartRipple(PointF origin)
    {
        var (w, h) = ((float)Bounds.Width, (float)Bounds.Height);
        var radius = MathF.Sqrt(MathF.Pow(MathF.Max(origin.X, w - origin.X), 2) + MathF.Pow(MathF.Max(origin.Y, h - origin.Y), 2));
        var ripple = new RippleState(origin, radius, new Signal<double>(0), new Signal<double>(1));
        ripples.Add(ripple);
        Animator.Animate(ripple.Scale, 1, Tokens.Duration.Lg, Easing.Standard);
        Invalidate();
        return () =>
        {
            if (ripple.Released) return;
            ripple.Released = true;
            Animator.Animate(ripple.Alpha, 0, Tokens.Duration.Md, Easing.Exit);
        };
    }

    /// <summary>
    /// 悬停、主题、档位变了：目标颜色重算；悬停引起的底色在 durations.sm 内过渡，其余直接换。
    /// 不用另外失效：绘制读了底色信号，写它就让这个按钮重画。
    /// </summary>
    void Retarget(bool animate)
    {
        target = Colors((Scope?.Theme ?? RuneTheme.Current.Peek()).Colors);
        var back = target.Background ?? Color.Transparent;
        if (animate) Animator.Animate(background, back, Tokens.Duration.Sm);
        else background.Value = back;
    }

    /// <summary>按钮里面的环境：字号换成档位的、字重 medium，文字颜色换成配方的前景色。</summary>
    ElementContext Inner(ElementContext context, Color fore)
    {
        var host = context.Font;
        var points = Size.Pixels is { } pixels ? pixels * 72 / 96 : host.SizeInPoints;
        // GDI 只有常规与粗体：medium 画成常规（外面是粗体也不继承，同 TS 的 fontWeight: medium）
        var style = host.Style & ~FontStyle.Bold;
        return context with { Font = Fonts.Get(host.FontFamily.Name, points, style), ForeColor = fore };
    }

    /// <summary>配方的颜色，外观覆盖写了的换掉（写了底色时悬停也用它）。</summary>
    (Color? Background, Color Foreground, Color? Border) Colors(ThemeColors colors)
    {
        var recipe = ButtonRecipe.Colors(Variant.Value, Tone.Value, IsHovered && IsEnabled, colors);
        if (ResolvedSx is not { } sx) return recipe;
        return (sx.Background ?? recipe.Background, sx.Foreground ?? recipe.Foreground, sx.BorderColor ?? recipe.Border);
    }

    /// <summary>左右、上下的内边距：上下 0.25em；左右 1em，链接与方形按钮没有；外观覆盖写了的换掉。</summary>
    (float Inline, float Block) Padding(float em)
    {
        var sx = ResolvedSx;
        return ((sx?.PaddingInline ?? (Variant == ButtonVariant.Link || Square ? 0 : 1)) * em, (sx?.PaddingBlock ?? 0.25f) * em);
    }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var inner = Inner(context, context.ForeColor);
        var em = inner.Em;
        var border = Border(context);
        var content = MeasureContent(inner);
        var (inline, block) = Padding(em);
        var width = content.Width + 2 * (inline + border);
        var height = content.Height + 2 * (block + border);
        // 控件高度：最小高度（链接没有）与方形按钮的宽度
        var control = ControlHeight.Of(context, em, ControlHeight.Factor(Size));
        if (Variant != ButtonVariant.Link) height = Math.Max(height, control);
        if (Square) width = control;
        return new Size((int)MathF.Ceiling(width), (int)MathF.Ceiling(height));
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var colors = Colors(context.Theme.Colors);
        if (colors != target) Retarget(animate: false);
        var dim = IsEnabled ? 1f : Tokens.Opacity.Disabled;
        // 上游禁用时整个按钮半透明；文字是 GDI 画的，颜色里的不透明度不起作用：混进按钮背后的颜色（取主题的表面色）
        var inner = Inner(context, dim >= 1 ? colors.Foreground : Drawing.Mix(context.Theme.Colors.Surface, colors.Foreground, dim));
        var em = inner.Em;
        var border = Border(context);
        var bounds = new RectangleF(Bounds.X + border / 2, Bounds.Y + border / 2, Bounds.Width - border, Bounds.Height - border);
        var radius = MathF.Min(((ResolvedSx?.Radius ?? Radius).In(context.Theme.Radii) ?? 0) * em, bounds.Height / 2);
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var outline = Drawing.RoundedRectangle(bounds, radius);
            using (var fill = new SolidBrush(Fade(background.Value, dim))) graphics.FillPath(fill, outline);
            if (colors.Border is { } stroke)
            {
                using var pen = new Pen(Fade(stroke, dim), border);
                graphics.DrawPath(pen, outline);
            }
            PaintRipples(graphics, outline, colors.Foreground);
            if (delayed.Shown)
            {
                var size = spinner.Measure(inner, Bounds.Size);
                spinner.Bounds = new Rectangle(Bounds.X + (Bounds.Width - size.Width) / 2, Bounds.Y + (Bounds.Height - size.Height) / 2, size.Width, size.Height);
                spinner.Paint(graphics, inner);
            }
            else
            {
                var (inline, block) = Padding(em);
                var inset = Rectangle.Round(RectangleF.Inflate(Bounds, -(inline + border), -(block + border)));
                PaintContent(graphics, inner, inset);
            }
            if (IsFocusVisible)
            {
                // outline 宽 2、偏移 2（borderWidths.sm）：画在宿主留出的 Padding 里
                var gap = context.Scale(Tokens.BorderWidth.Sm);
                var ring = RectangleF.Inflate(bounds, gap + gap / 2, gap + gap / 2);
                using var ringPath = Drawing.RoundedRectangle(ring, radius + 1.5f * gap);
                using var pen = new Pen(context.Theme.Colors.FocusRing, gap);
                graphics.DrawPath(pen, ringPath);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>波纹裁在圆角里，颜色是文字色的 22%；淡出完的移除。</summary>
    void PaintRipples(Graphics graphics, GraphicsPath clip, Color color)
    {
        ripples.RemoveAll(r => r.Released && r.Alpha.Peek() <= 0);
        if (ripples.Count == 0) return;
        var region = graphics.Clip;
        graphics.SetClip(clip, CombineMode.Intersect);
        foreach (var ripple in ripples)
        {
            var r = ripple.Radius * (float)ripple.Scale.Value;
            using var brush = new SolidBrush(Color.FromArgb((int)(color.A * 0.22 * ripple.Alpha.Value), color));
            graphics.FillEllipse(brush, Bounds.X + ripple.Origin.X - r, Bounds.Y + ripple.Origin.Y - r, 2 * r, 2 * r);
        }
        graphics.Clip = region;
    }

    private protected static Color Fade(Color color, float opacity) => opacity >= 1 ? color : Color.FromArgb((int)(color.A * opacity), color);

    /// <summary>描边宽度：borderWidths.xs；没有描边色时同样占位。</summary>
    static float Border(ElementContext context) => context.Scale(Tokens.BorderWidth.Xs);

    /// <summary>一个波纹：圆心、最终半径、扩散进度与不透明度（Animator 驱动的信号，绘制时读，变了只重画这个按钮）。</summary>
    sealed record RippleState(PointF Origin, float Radius, Signal<double> Scale, Signal<double> Alpha)
    {
        public bool Released { get; set; }
    }
}

/// <summary>按钮的样式（vue-tsx-pro 的 ButtonVariants）。</summary>
/// <param name="Value">取值（solid、soft、surface、outline、ghost、link）。</param>
[TypeConverter(typeof(VariantConverter<ButtonVariant>))]
public readonly record struct ButtonVariant(string Value) : IVariant<ButtonVariant>
{
    /// <summary>实底：一组操作里的主要操作，每组最多一个。</summary>
    public static readonly ButtonVariant Solid = new("solid");

    /// <summary>浅色底：次要但仍需突出的操作。</summary>
    public static readonly ButtonVariant Soft = new("soft");

    /// <summary>浅色底加描边：表单、设置页里常用的次要操作。</summary>
    public static readonly ButtonVariant Surface = new("surface");

    /// <summary>只有描边：次要操作。</summary>
    public static readonly ButtonVariant Outline = new("outline");

    /// <summary>无底无边，悬停时才有底色：工具栏、行内操作、图标按钮。</summary>
    public static readonly ButtonVariant Ghost = new("ghost");

    /// <summary>看起来像链接（无底、无左右内边距、悬停下划线）：文字中间的操作。</summary>
    public static readonly ButtonVariant Link = new("link");

    /// <inheritdoc />
    public static IReadOnlyList<ButtonVariant> Known { get; } = [Solid, Soft, Surface, Outline, Ghost, Link];

    /// <inheritdoc />
    public static ButtonVariant Parse(string value)
    {
        var variant = new ButtonVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
