using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Rune.WinForms;

/// <summary>
/// 手写签名（vue-tsx-pro 的 SignaturePad）：审批签字、签收、合同确认。用指针（鼠标、笔、手指）写，笔画按速度粗细（写得快变细），可以撤销上一笔、清空。
/// <code>
/// var pad = new SignaturePadControl { Dock = DockStyle.Top };
/// pad.ValueChanged += (_, _) => confirm.Enabled = pad.Value is not null;
/// var svg = pad.ToSvg();
/// </code>
/// 值 <see cref="Value"/> 是透明底的 PNG（字节；上游是 data URL），每写完一笔更新、报告 <see cref="ValueChanged"/>，没签时 null；代码里换了值（编辑页读回的旧签名）时把它画在底下，
/// 可以接着写或清空。外框同输入框（描边、悬停、出错、只读、不可用，跟所在 FieldControl），底色与墨色是纸面（<see cref="Paper"/>：深色主题下仍是白底深色）。
/// 高 10em（自动大小；关掉 AutoSize 自己给高度），宽度跟着布局。笔画按逻辑像素记（96 DPI），画的时候按 DPI 放大，导出的 SVG 与另两个平台一致。
/// 写字只能靠指针：撤销、清空是按钮，键盘照常可用。读屏：控件是一张图片，名称是 AccessibleName（或字段标签，默认“签名”），说明“已签名 / 未签名”。
/// </summary>
[DefaultEvent(nameof(ValueChanged))]
[Description("手写签名：用指针写，可以撤销、清空，值是 PNG。")]
public class SignaturePadControl : Control, IFieldControl
{
    readonly SignatureStrokes strokes = new();
    readonly IconButtonControl undo = Tool(Lucide.Undo2);
    readonly IconButtonControl clear = Tool(Lucide.Eraser);
    Bitmap? baseImage;
    bool hovered;

    /// <summary>新建：高 10em，撤销、清空在右上角（写过之后才有）。</summary>
    public SignaturePadControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        TabStop = false;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Graphic;
        undo.Click += (_, _) => Undo();
        clear.Click += (_, _) => Clear();
        Controls.AddRange([undo, clear]);
        Sync();
    }

    /// <summary>签名（透明底的 PNG 字节）；没签时 null。代码里换了值时把它画在底下。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public byte[]? Value
    {
        get;
        set
        {
            if (ReferenceEquals(field, value)) return;
            field = value;
            Load(value);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>墨色；不写时是纸面的墨色。</summary>
    [Category("Appearance")]
    [Description("墨色；不写时是纸面的墨色。")]
    [DefaultValue(null)]
    public Color? PenColor { get; set { field = value; Invalidate(); } }

    /// <summary>最细的笔宽（逻辑像素），默认 0.75。</summary>
    [Category("Appearance")]
    [Description("最细的笔宽（逻辑像素）。")]
    [DefaultValue(0.75)]
    public double PenMinWidth { get => strokes.MinWidth; set => strokes.MinWidth = value; }

    /// <summary>最粗的笔宽（逻辑像素），默认 2.75。</summary>
    [Category("Appearance")]
    [Description("最粗的笔宽（逻辑像素）。")]
    [DefaultValue(2.75)]
    public double PenMaxWidth { get => strokes.MaxWidth; set => strokes.MaxWidth = value; }

    /// <summary>只读：显示已签的图，不能写；不写时取所在 FieldControl 的。</summary>
    [Category("Behavior")]
    [Description("只读：显示已签的图，不能写；不写时取所在字段的。")]
    [DefaultValue(null)]
    public bool? IsReadOnly { get; set { field = value; Sync(); } }

    /// <summary>出错；不写时取所在 FieldControl 的。</summary>
    [Category("Behavior")]
    [Description("出错；不写时取所在字段的。")]
    [DefaultValue(null)]
    public bool? IsInvalid { get; set { field = value; Invalidate(); } }

    /// <summary>圆角档位，默认 md。</summary>
    [Category("Appearance")]
    public Radius Radius { get; set { field = value; Invalidate(); } } = Radius.Md;

    bool ShouldSerializeRadius() => Radius != Radius.Md;

    void ResetRadius() => Radius = Radius.Md;

    /// <summary>值变了（写完一笔、撤销、清空、代码里换了值）。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>撤销、清空按钮（测试用）。</summary>
    internal (IconButtonControl Undo, IconButtonControl Clear) Tools => (undo, clear);

    /// <summary>提示文字（测试用）；签了时 null。</summary>
    internal string? PlaceholderText => Signed ? null : Writable ? ProviderControl.LangOf(this).SignaturePlaceholder : ProviderControl.LangOf(this).SignatureEmpty;

    bool ReadOnly => IsReadOnly ?? FieldControl.Of(this)?.IsReadOnly ?? false;

    bool Invalid => IsInvalid ?? FieldControl.Of(this)?.Invalid ?? false;

    /// <summary>能写：可用、不只读。</summary>
    bool Writable => Enabled && !ReadOnly;

    /// <summary>签了：有笔画或读回的旧签名。</summary>
    bool Signed => strokes.Strokes.Count > 0 || baseImage is not null;

    /// <summary>逻辑像素换成设备像素的倍数。</summary>
    float DpiScale => DeviceDpi / 96f;

    Color Ink => PenColor ?? Paper.Ink(this);

    /// <summary>透明底的 PNG（按 DPI 出图）；没签时 null。</summary>
    public byte[]? ToPng()
    {
        if (!Signed || Width <= 0 || Height <= 0) return null;
        using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) DrawInk(graphics);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    /// <summary>SVG 文字（逻辑像素；笔画是路径，读回的旧签名是嵌入的图片）；没签时 null。</summary>
    public string? ToSvg() => strokes.ToSvg(Width / DpiScale, Height / DpiScale, $"#{Ink.R:x2}{Ink.G:x2}{Ink.B:x2}", baseImage is null ? null : Value);

    /// <summary>撤销上一笔（没有笔画时去掉读回的旧签名）。</summary>
    public void Undo()
    {
        if (!strokes.Undo()) DropBase();
        Commit();
    }

    /// <summary>清空。</summary>
    public void Clear()
    {
        strokes.Clear();
        DropBase();
        Commit();
    }

    void IFieldControl.FieldChanged() => Sync();

    void DropBase()
    {
        baseImage?.Dispose();
        baseImage = null;
    }

    /// <summary>写完一笔、撤销、清空：交出新的值；自己交出的值不重新读成底图。</summary>
    void Commit()
    {
        committing = true;
        Value = ToPng();
        committing = false;
        Sync();
    }

    bool committing;

    /// <summary>代码里换了签名（读回旧签名、在别处清空）：清掉笔画，旧签名画在底下。</summary>
    void Load(byte[]? png)
    {
        if (committing) return;
        strokes.Clear();
        DropBase();
        if (png is { Length: > 0 })
        {
            try
            {
                using var stream = new MemoryStream(png);
                using var decoded = new Bitmap(stream);
                baseImage = new Bitmap(decoded);
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceWarning($"SignaturePadControl 读不了签名图片：{error.Message}");
            }
        }
        Sync();
    }

    /// <summary>按钮跟着状态（能写、签了才有）；名称、说明、指针；重画。</summary>
    void Sync()
    {
        var lang = ProviderControl.LangOf(this);
        undo.Visible = clear.Visible = Writable && Signed;
        undo.AccessibleName = lang.SignatureUndo;
        clear.AccessibleName = lang.SignatureClear;
        AccessibleDescription = Signed ? lang.SignatureSigned : lang.SignatureEmpty;
        Cursor = Writable ? Cursors.Cross : Cursors.Default;
        PerformLayout();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 语言包、字号取自外层：放进界面后重新生成名称、算尺寸
        Sync();
    }

    /// <summary>宽度跟着布局，高 10em。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = VerticalStack.Limit(this, proposedSize);
        return new Size(width == int.MaxValue ? Width : width, (int)Math.Round(10 * this.EmPixels()));
    }

    /// <summary>撤销、清空靠右上角，离边 xs。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var xs = (int)MathF.Round(Tokens.Space.Xs * this.EmPixels());
        var x = Width - xs;
        foreach (var button in new[] { clear, undo })
        {
            var size = button.GetPreferredSize(Size.Empty);
            x -= size.Width;
            button.Bounds = new Rectangle(x, xs, size.Width, size.Height);
        }
    }

    /// <summary>纸面底、圆角外框（描边随状态：出错 danger、只读 borderMuted、悬停 borderStrong），墨迹，没签时中间的提示。不可用时半淡。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        var colorsTheme = ElementContext.Of(this).Theme;
        var colors = colorsTheme.Colors;
        var em = this.EmPixels();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var border = Math.Max(1, LogicalToDeviceUnits(1));
        using var shape = Drawing.RoundedRectangle(new RectangleF(border / 2f, border / 2f, Width - border, Height - border), (Radius.In(colorsTheme.Radii) ?? 0) * em);
        using (var fill = new SolidBrush(Paper.Background(this))) graphics.FillPath(fill, shape);
        var state = Invalid ? colors.Danger : ReadOnly ? colors.BorderMuted : hovered && Writable ? colors.BorderStrong : colors.Border;
        using (var pen = new Pen(Dim(state), border)) graphics.DrawPath(pen, shape);
        graphics.SetClip(shape);
        DrawInk(graphics);
        graphics.ResetClip();
        if (PlaceholderText is { } text)
            TextRenderer.DrawText(graphics, text, Font, ClientRectangle, Dim(colors.TextPlaceholder), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    /// <summary>不可用时颜色减半（控件不能半透明）。</summary>
    Color Dim(Color color) => Enabled ? color : Color.FromArgb(color.A / 2, color);

    /// <summary>墨迹：读回的旧签名铺满，笔画按 DPI 放大（一个点的画成圆点）。</summary>
    void DrawInk(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (baseImage is not null) graphics.DrawImage(baseImage, new Rectangle(0, 0, Width, Height));
        var state = graphics.Save();
        graphics.ScaleTransform(DpiScale, DpiScale);
        using var ink = new SolidBrush(Dim(Ink));
        foreach (var stroke in strokes.Strokes) DrawStroke(graphics, stroke, ink);
        if (strokes.CurrentPoints is { Count: > 0 } points) DrawStroke(graphics, points, ink);
        graphics.Restore(state);
    }

    /// <summary>二次贝塞尔换成三次（GDI+ 只有三次）：控制点各取两端到原控制点的 2/3。</summary>
    static void DrawStroke(Graphics graphics, IReadOnlyList<StrokePoint> stroke, SolidBrush ink)
    {
        if (stroke.Count == 1)
        {
            var r = (float)stroke[0].Width / 2;
            graphics.FillEllipse(ink, (float)stroke[0].X - r, (float)stroke[0].Y - r, 2 * r, 2 * r);
            return;
        }
        using var pen = new Pen(ink.Color) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        foreach (var (from, control, to, width) in SignatureStrokes.SegmentsOf(stroke))
        {
            pen.Width = (float)width;
            PointF P(double x, double y) => new((float)x, (float)y);
            graphics.DrawBezier(pen, P(from.X, from.Y), P(from.X + 2.0 / 3 * (control.X - from.X), from.Y + 2.0 / 3 * (control.Y - from.Y)),
                P(to.X + 2.0 / 3 * (control.X - to.X), to.Y + 2.0 / 3 * (control.Y - to.Y)), P(to.X, to.Y));
        }
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovered = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = false;
        Invalidate();
    }

    /// <summary>按下开始一笔（坐标换成逻辑像素）。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Writable || e.Button != MouseButtons.Left) return;
        strokes.Begin(e.X / DpiScale, e.Y / DpiScale, Environment.TickCount64);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!strokes.IsDrawing) return;
        strokes.Extend(e.X / DpiScale, e.Y / DpiScale, Environment.TickCount64);
        Invalidate();
    }

    /// <summary>抬笔（或丢了指针）：收下这一笔。</summary>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (strokes.End()) Commit();
    }

    /// <inheritdoc />
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture && strokes.End()) Commit();
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new SignatureAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) DropBase();
        base.Dispose(disposing);
    }

    static IconButtonControl Tool(IconData icon) => new() { Icon = icon, TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };

    /// <summary>读屏：一张图片（TS 的 role="img"），名称是自己的名称、字段标签或“签名”。</summary>
    sealed class SignatureAccessibleObject(SignaturePadControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Graphic;

        public override string? Name
        {
            get => owner.AccessibleName is { Length: > 0 } name ? name
                : FieldControl.Of(owner) is { } scope && scope.Owns(owner) ? scope.Label
                : ProviderControl.LangOf(owner).Signature;
            set => owner.AccessibleName = value;
        }
    }
}
