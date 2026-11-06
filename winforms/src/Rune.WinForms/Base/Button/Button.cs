using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 按钮（vue-tsx-pro 的 Button）：外观 = 样式 × 色调 × 字号档位 × 圆角，内部尺寸都是 em。无窗口元素，放进窗体用 <see cref="ButtonControl"/>；
/// 宿主负责鼠标、键盘、焦点，把悬停、按下、键盘聚焦告诉元素。
/// 颜色取生成的配方（<c>ButtonRecipe</c>，tools/recipe-gen 按调色板表生成）。效果同 TS：
/// 悬停时底色、文字色在 durations.sm 内过渡（<see cref="Animator"/>）；按下时从指针（键盘时从中心）扩散文字颜色 22% 的波纹，
/// 松开淡出；加载指示防闪烁（<see cref="DelayedLoading"/>）、转动读 <see cref="FrameClock"/>；键盘聚焦时外面一圈焦点环。
/// </summary>
public class Button : Element
{
    readonly Text text = new() { Wrap = TextWrap.NoWrap };
    readonly Spinner spinner = new();
    readonly Signal<Color> background = new(Color.Transparent);
    readonly List<RippleState> ripples = [];
    readonly DelayedLoading delayed;
    Icon? icon;
    Icon? iconEnd;
    (Color? Background, Color Foreground, Color? Border) target;

    /// <summary>新建按钮。</summary>
    public Button() => delayed = new DelayedLoading(_ => InvalidateMeasure());

    /// <summary>文字内容（TS 的 children）；加载中仍保留宽度。</summary>
    public string Content { get => text.Content; set { text.Content = value; InvalidateMeasure(); } }

    /// <summary>样式；不写时 solid。</summary>
    public ButtonVariant Variant { get => field.Value is null ? ButtonVariant.Solid : field; set { field = value; InvalidateMeasure(); } }

    /// <summary>色调；不写时 accent。</summary>
    public StatusTone Tone { get => field.Value is null ? StatusTone.Accent : field; set { field = value; Invalidate(); } }

    /// <summary>字号档位，内部尺寸随之缩放；不写时跟随宿主的字体。</summary>
    public TextSize Size { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>圆角；不写时 md。</summary>
    public Radius Radius { get => field.Value is null ? Radius.Md : field; set { field = value; Invalidate(); } }

    /// <summary>加载中：显示旋转指示，宽度不变，点击无效（宿主拦下）。</summary>
    public bool IsLoading { get; set { field = value; delayed.Update(value); Invalidate(); } }

    /// <summary>按下时的波纹；不写时沿用 <see cref="WinForms.Ripple.Enabled"/>。</summary>
    public bool? Ripple { get; set; }

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get; set { field = value; Rebuild(); } }

    /// <summary>文字后的图标。</summary>
    public IconData? IconEnd { get; set { field = value; Rebuild(); } }

    /// <summary>占满宿主给的宽度。</summary>
    public bool FullWidth { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>前置图标部件的属性。</summary>
    public IconProps? PropsIcon { get; set { field = value; Rebuild(); } }

    /// <summary>后置图标部件的属性。</summary>
    public IconProps? PropsIconEnd { get; set { field = value; Rebuild(); } }

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

    /// <summary>合成好的前置图标部件（测试用）。</summary>
    internal Icon? IconPart => icon;

    /// <summary>正在显示的波纹个数（测试用）。</summary>
    internal int RippleCount => ripples.Count;

    /// <summary>不可用、加载中不出波纹。</summary>
    internal bool RippleOn => (Ripple ?? WinForms.Ripple.Enabled) && IsEnabled && !IsLoading;

    void Rebuild()
    {
        icon = Icon is null ? null : new Icon();
        if (icon is not null) new IconProps { Data = Icon }.Merge(PropsIcon).ApplyTo(icon);
        iconEnd = IconEnd is null ? null : new Icon();
        if (iconEnd is not null) new IconProps { Data = IconEnd }.Merge(PropsIconEnd).ApplyTo(iconEnd);
        InvalidateMeasure();
    }

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
        target = ButtonRecipe.Colors(Variant.Value, Tone.Value, IsHovered && IsEnabled, RuneTheme.Current.Peek().Colors);
        var back = target.Background ?? Color.Transparent;
        if (animate) Animator.Animate(background, back, Tokens.Duration.Sm);
        else background.Value = back;
    }

    /// <summary>按钮里面的环境：字号换成档位的、字重 semibold（链接 medium），文字颜色换成配方的前景色。</summary>
    ElementContext Inner(ElementContext context, Color fore)
    {
        var host = context.Font;
        var points = Size.Pixels is { } pixels ? pixels * 72 / 96 : host.SizeInPoints;
        // GDI 只有常规与粗体：semibold 画成粗体，链接的 medium 画成常规
        var style = Variant == ButtonVariant.Link ? host.Style & ~FontStyle.Bold : host.Style | FontStyle.Bold;
        return context with { Font = Fonts.Get(host.FontFamily.Name, points, style), ForeColor = fore };
    }

    IEnumerable<Element> Parts()
    {
        if (icon is not null) yield return icon;
        yield return text;
        if (iconEnd is not null) yield return iconEnd;
    }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var inner = Inner(context, context.ForeColor);
        var em = inner.Em;
        var link = Variant == ButtonVariant.Link;
        var unbounded = new Size(int.MaxValue, int.MaxValue);
        // 没有文字时文字部件不占宽度，但仍给出一行的高度
        var measured = Parts().Select(part => (Part: part, Size: part.Measure(inner, unbounded))).ToList();
        var shown = measured.Where(m => Visible(m.Part)).ToList();
        var width = shown.Sum(m => m.Size.Width) + Math.Max(0, shown.Count - 1) * 0.5f * em + (link ? 0 : 2 * (em + Border(context)));
        var height = measured.Max(m => m.Size.Height) + (link ? 0 : 2 * (0.25f * em + Border(context)));
        if (!link) height = Math.Max(height, Math.Max(2.25f * em, context.Scale(Tokens.Size.MinTarget)));
        if (FullWidth && available.Width < int.MaxValue) width = Math.Max(width, available.Width);
        return new Size((int)MathF.Ceiling(width), (int)MathF.Ceiling(height));
    }

    /// <summary>没有文字时文字部件不占位。</summary>
    bool Visible(Element part) => part != text || text.Content.Length > 0;

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var colors = ButtonRecipe.Colors(Variant.Value, Tone.Value, IsHovered && IsEnabled, context.Theme.Colors);
        if (colors != target) Retarget(animate: false);
        var dim = IsEnabled ? 1f : Tokens.Opacity.Disabled;
        var fore = Fade(colors.Foreground, dim);
        var inner = Inner(context, fore);
        var em = inner.Em;
        var border = Border(context);
        var bounds = new RectangleF(Bounds.X + border / 2, Bounds.Y + border / 2, Bounds.Width - border, Bounds.Height - border);
        var radius = MathF.Min((Radius.In(context.Theme.Radii) ?? 0) * em, bounds.Height / 2);
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var outline = RoundedRectangle(bounds, radius);
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
            else PaintContent(graphics, inner, em);
            if (IsFocusVisible)
            {
                // outline 宽 2、偏移 2（borderWidths.sm）：画在宿主留出的 Padding 里
                var gap = context.Scale(Tokens.BorderWidth.Sm);
                var ring = RectangleF.Inflate(bounds, gap + gap / 2, gap + gap / 2);
                using var ringPath = RoundedRectangle(ring, radius + 1.5f * gap);
                using var pen = new Pen(context.Theme.Colors.FocusRing, gap);
                graphics.DrawPath(pen, ringPath);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    void PaintContent(Graphics graphics, ElementContext inner, float em)
    {
        var unbounded = new Size(int.MaxValue, int.MaxValue);
        var parts = Parts().Where(Visible).Select(part => (Part: part, Size: part.Measure(inner, unbounded))).ToList();
        var width = parts.Sum(p => p.Size.Width) + Math.Max(0, parts.Count - 1) * 0.5f * em;
        var x = Bounds.X + (Bounds.Width - width) / 2;
        foreach (var (part, size) in parts)
        {
            part.Bounds = new Rectangle((int)MathF.Round(x), Bounds.Y + (Bounds.Height - size.Height) / 2, size.Width, size.Height);
            part.Paint(graphics, inner);
            x += size.Width + 0.5f * em;
        }
        if (Variant == ButtonVariant.Link && IsHovered)
        {
            // 链接样式悬停下划线（位置取文字底部往上一点，同 CSS 的 text-underline-offset 0.2em 量级）
            var underline = text.Bounds.Bottom - (int)(0.1f * em);
            using var pen = new Pen(inner.ForeColor, MathF.Max(1, 0.06f * em));
            graphics.DrawLine(pen, text.Bounds.Left, underline, text.Bounds.Right, underline);
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

    static Color Fade(Color color, float opacity) => opacity >= 1 ? color : Color.FromArgb((int)(color.A * opacity), color);

    /// <summary>描边宽度：borderWidths.xs；没有描边色时同样占位。</summary>
    static float Border(ElementContext context) => context.Scale(Tokens.BorderWidth.Xs);

    static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        var d = 2 * radius;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

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

    /// <summary>看起来像链接（无底、无内边距、悬停下划线）：文字中间的操作。</summary>
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
