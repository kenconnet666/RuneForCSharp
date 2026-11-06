using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 骨架屏（vue-tsx-pro 的 Skeleton）：内容加载时的占位，一道光带从起始边扫过。被占位的控件加成子控件：加载时它照常决定大小但隐藏、
/// 不可交互，加载完换成内容时布局不跳；没有子控件时用 <see cref="Shape"/> 选常用形状。
/// <code>
/// var name = new SkeletonControl();
/// name.Controls.Add(new TextControl { Text = user.Name });
/// // 加载完：name.Loading = false;
/// </code>
/// 底色与光带都是半透明（9%）的正文色；圆角 radii.sm（圆 full，按钮、图片 md）。扫光 2 秒一轮（1.4 秒扫过、0.6 秒停顿，先慢后快再慢），
/// 读 <see cref="FrameClock"/>：只在画的时候扫，隐藏了就停；系统关闭动画时不扫。从右到左时反向。加载时读屏里没有角色。
/// </summary>
public class SkeletonControl : Panel
{
    readonly SkeletonEngine engine;
    readonly Effect paint;

    /// <summary>新建：大小跟着内容或形状，自己画占位。</summary>
    public SkeletonControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        engine = new SkeletonEngine(this);
        paint = new Effect(Invalidate);
        UpdateLoading();
    }

    /// <summary>是否在加载（默认 true）；false 时显示内容。</summary>
    [Category("Behavior")]
    [Description("是否在加载；false 时显示被占位的子控件。")]
    [DefaultValue(true)]
    public bool Loading { get; set { field = value; UpdateLoading(); } } = true;

    /// <summary>形状；没有子控件时用它决定大小。</summary>
    [Category("Appearance")]
    [Description("形状：text、heading、circle、button、image；没有子控件时决定大小。")]
    public SkeletonShape Shape { get; set { field = value; PerformLayout(); Invalidate(); } }

    /// <summary>扫光的延迟（毫秒）：几块骨架依次扫过、连成一道波时用。</summary>
    [Category("Appearance")]
    [Description("扫光的延迟（毫秒）。")]
    [DefaultValue(0)]
    public int Delay { get; set; }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    bool ShouldSerializeShape() => Shape.Value is not null;

    void ResetShape() => Shape = default;

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    void UpdateLoading()
    {
        foreach (Control child in Controls)
        {
            child.Visible = !Loading;
            child.Enabled = !Loading;
        }
        AccessibleRole = Loading ? AccessibleRole.None : AccessibleRole.Default;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        e.Control!.Visible = !Loading;
        e.Control.Enabled = !Loading;
    }

    /// <summary>形状决定的大小；没有形状时为 null（跟着子控件）。</summary>
    Size? ShapeSize(int width)
    {
        var em = Em;
        return Shape.Value switch
        {
            "text" => new Size(width, (int)MathF.Round(em)),
            "heading" => new Size((int)MathF.Round(0.4f * width), (int)MathF.Round(1.5f * em)),
            "circle" => new Size((int)MathF.Round(2.5f * em), (int)MathF.Round(2.5f * em)),
            "button" => new Size((int)MathF.Round(6 * em), (int)MathF.Round(ControlHeight.Of(new ElementContext(DeviceDpi, Font, ForeColor, false), em, Tokens.Size.Control))),
            "image" => new Size(width, width * 9 / 16),
            _ => null,
        };
    }

    /// <summary>光带的位置（-1 在起始边外，1 在结束边外）：2 秒一轮，前 1.4 秒按正弦缓动扫过，之后停在外面。</summary>
    double Phase()
    {
        var t = FrameClock.Shared.Now.TotalSeconds - Delay / 1000d;
        if (t < 0) return -1;
        t %= 2;
        return t >= 1.4 ? 1 : -1 + (1 - Math.Cos(Math.PI * t / 1.4));
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!Loading || Width <= 0 || Height <= 0) return;
        using var _ = paint.Track();
        var theme = RuneTheme.Current.Value;
        var tint = Color.FromArgb((int)Math.Round(255 * 0.09), theme.Colors.Text);
        var radius = Shape.Value == "circle" ? Height / 2f : Math.Min((Shape.Value is "button" or "image" ? theme.Radii.Md : theme.Radii.Sm) * Em, Height / 2f);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), radius);
        using (var fill = new SolidBrush(tint)) graphics.FillPath(fill, shape);
        if (!Motion.Enabled) return;
        var phase = Phase();
        if (RightToLeft == RightToLeft.Yes) phase = -phase;
        var band = new RectangleF((float)(phase * Width), 0, Width, Height);
        using var brush = new LinearGradientBrush(band, Color.Transparent, Color.Transparent, LinearGradientMode.Horizontal)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.Transparent, Color.Transparent, tint, Color.Transparent, Color.Transparent],
                Positions = [0f, 0.15f, 0.5f, 0.85f, 1f],
            },
        };
        var state = graphics.Save();
        graphics.SetClip(shape);
        graphics.FillRectangle(brush, band);
        graphics.Restore(state);
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => engine.Measure(proposedSize);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>排版：子控件在左上角、保持自己的大小；骨架的大小是形状的，或子控件的（隐藏着也按它算）。</summary>
    sealed class SkeletonEngine(SkeletonControl owner) : LayoutEngine
    {
        public Size Measure(Size proposed)
        {
            var width = proposed.Width is > 1 and < int.MaxValue ? proposed.Width : owner.Width;
            if (owner.Loading && owner.ShapeSize(width) is { } shape) return shape;
            var child = owner.Controls.Count > 0 ? owner.Controls[0] : null;
            return child is null ? Size.Empty : child.AutoSize ? child.GetPreferredSize(proposed) : child.Size;
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            // 子控件保持自己的大小（自动大小的按想要的大小）：骨架的大小跟着它，反过来撑它会互相影响
            foreach (Control child in owner.Controls)
            {
                var size = child.AutoSize ? child.GetPreferredSize(new Size(owner.ClientSize.Width, 0)) : child.Size;
                child.SetBounds(0, 0, size.Width, size.Height);
            }
            return owner.AutoSize;
        }
    }
}

/// <summary>骨架的常用形状（vue-tsx-pro 的 SkeletonShapes）。</summary>
/// <param name="Value">取值（text、heading、circle、button、image）。</param>
[TypeConverter(typeof(VariantConverter<SkeletonShape>))]
public readonly record struct SkeletonShape(string Value) : IVariant<SkeletonShape>
{
    /// <summary>一行文字：高 1em，占满宽度。</summary>
    public static readonly SkeletonShape Text = new("text");

    /// <summary>标题：高 1.5em，四成宽度。</summary>
    public static readonly SkeletonShape Heading = new("heading");

    /// <summary>圆：头像，边长 2.5em（与同字号的 Avatar 一样大）。</summary>
    public static readonly SkeletonShape Circle = new("circle");

    /// <summary>按钮：控件高度，宽 6em。</summary>
    public static readonly SkeletonShape Button = new("button");

    /// <summary>图片：占满宽度，16:9。</summary>
    public static readonly SkeletonShape Image = new("image");

    /// <inheritdoc />
    public static IReadOnlyList<SkeletonShape> Known { get; } = [Text, Heading, Circle, Button, Image];

    /// <inheritdoc />
    public static SkeletonShape Parse(string value)
    {
        var shape = new SkeletonShape(value);
        Variants.WarnIfUnknown(shape);
        return shape;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
