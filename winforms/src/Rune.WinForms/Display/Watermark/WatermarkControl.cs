using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 水印（vue-tsx-pro 的 Watermark）：在内容上（或整个窗体）铺满斜着的文字或图片，截图、拍屏时能看出来源（查看人、时间、密级）。
/// <code>
/// var report = new WatermarkControl { Text = "林小满\n2026-10-24", Dock = DockStyle.Fill };
/// report.Controls.Add(reportView);
/// form.Controls.Add(new WatermarkControl { Text = "内部资料", Fullscreen = true });   // 整个窗体
/// </code>
/// 子控件各有自己的窗口，容器画不到它们上面：水印画在一个属于窗体的分层窗口里，盖住这个控件看得见的部分（整个窗体时盖住窗体的客户区），
/// 跟着控件移动、缩放、显示隐藏。分层窗口点击全部穿透、不置顶（切到别的程序时不挡别人），读屏里没有它。
/// 默认颜色是半透明的灰，亮色、暗色主题上都看得见。多行文字用换行分开（TS 的 content 数组）。尺寸是 96 DPI 下的像素，按显示器 DPI 放大。
/// 只是提示性的：真正的保密要靠服务端（导出时打水印、权限）。
/// </summary>
[ToolboxItem(true)]
[Description("水印：在内容或整个窗体上铺满斜着的文字或图片。")]
public class WatermarkControl : Panel
{
    readonly LayeredWindow overlay = new(topmost: false, clickThrough: true);
    readonly List<Control> watched = [];
    readonly VerticalStack layout = new(() => 0);
    Form? form;

    /// <summary>新建：透明底，子控件竖着排（同网页里包着内容的 div），大小跟着内容。</summary>
    public WatermarkControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => layout;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => layout.Measure(this, proposedSize);

    /// <summary>文字：多行用换行分开（姓名、日期）。</summary>
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [Category("Appearance")]
    [Description("水印的文字：多行用换行分开。")]
    [AllowNull]
    public override string Text { get => base.Text; set => base.Text = value; }

    /// <summary>图片：给了就画图片，不画文字。</summary>
    [Category("Appearance")]
    [Description("水印的图片：给了就画图片，不画文字。")]
    [DefaultValue(null)]
    public Image? Image { get; set => Change(ref field, value); }

    /// <summary>图片的宽（px），默认 120。</summary>
    [Category("Appearance")]
    [DefaultValue(120f)]
    public float ImageWidth { get; set => Change(ref field, value); } = (float)WatermarkTiles.ImageWidth;

    /// <summary>图片的高（px），默认 40。</summary>
    [Category("Appearance")]
    [DefaultValue(40f)]
    public float ImageHeight { get; set => Change(ref field, value); } = (float)WatermarkTiles.ImageHeight;

    /// <summary>旋转角度（度），默认 -22。</summary>
    [Category("Appearance")]
    [DefaultValue(-22f)]
    public float Rotate { get; set => Change(ref field, value); } = (float)WatermarkTiles.Rotate;

    /// <summary>每格之间的空隙（横、竖，px），默认 120 × 100。</summary>
    [Category("Appearance")]
    public Size Gap { get; set => Change(ref field, value); } = new((int)WatermarkTiles.GapX, (int)WatermarkTiles.GapY);

    bool ShouldSerializeGap() => Gap != new Size((int)WatermarkTiles.GapX, (int)WatermarkTiles.GapY);

    void ResetGap() => Gap = new Size((int)WatermarkTiles.GapX, (int)WatermarkTiles.GapY);

    /// <summary>字号（px），默认 14：固定值，不跟着控件的 Font。</summary>
    [Category("Appearance")]
    [DefaultValue(14f)]
    public float FontSize { get; set => Change(ref field, value); } = (float)WatermarkTiles.FontSize;

    /// <summary>颜色，默认半透明的灰。</summary>
    [Category("Appearance")]
    public Color Color { get; set => Change(ref field, value); } = Color.FromArgb(unchecked((int)WatermarkTiles.Color));

    bool ShouldSerializeColor() => Color.ToArgb() != unchecked((int)WatermarkTiles.Color);

    void ResetColor() => Color = Color.FromArgb(unchecked((int)WatermarkTiles.Color));

    /// <summary>盖住整个窗体（客户区），而不只是这个控件。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Fullscreen { get; set => Change(ref field, value); }

    /// <summary>画水印的分层窗口（测试用）。</summary>
    internal LayeredWindow Overlay => overlay;

    float DpiScale => DeviceDpi / 96f;

    void Change<T>(ref T field, T value)
    {
        field = value;
        Present();
    }

    /// <summary>一份内容的大小（设备像素；图片时是图片的大小）；没有内容时为 null。</summary>
    internal SizeF? ContentSize(Graphics graphics)
    {
        var scale = DpiScale;
        if (Image is not null) return new SizeF(ImageWidth * scale, ImageHeight * scale);
        var lines = WatermarkTiles.Lines(Text);
        if (lines.Length == 0) return null;
        using var font = WatermarkFont();
        var width = lines.Max(line => graphics.MeasureString(line, font, PointF.Empty, StringFormat.GenericTypographic).Width);
        return new SizeF(Math.Max(1, width), lines.Length * (float)WatermarkTiles.LineHeight(FontSize) * scale);
    }

    Font WatermarkFont() => new(Font.FontFamily, FontSize * DpiScale, GraphicsUnit.Pixel);

    /// <summary>
    /// 画一块区域的水印：<paramref name="area"/> 是要画的范围（屏幕坐标），平铺从 <paramref name="origin"/>（控件或窗体客户区的左上角）起算，
    /// 控件只露出一部分时水印不跟着错位。
    /// </summary>
    internal Bitmap Render(Rectangle area, Point origin)
    {
        var bitmap = LayeredWindow.Canvas(area.Size);
        using var graphics = Graphics.FromImage(bitmap);
        if (ContentSize(graphics) is not { } content) return bitmap;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        // 分层窗口是透明底：ClearType 要不透明的底，只能用灰度抗锯齿
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var scale = DpiScale;
        var (offsetX, offsetY) = (origin.X - area.X, origin.Y - area.Y);
        var lines = WatermarkTiles.Lines(Text);
        var lineHeight = WatermarkTiles.LineHeight(FontSize) * scale;
        using var font = WatermarkFont();
        using var brush = new SolidBrush(Color);
        using var centered = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        // 铺满的范围从平铺原点量到区域的右下角
        foreach (var tile in WatermarkTiles.Layout(area.Right - origin.X, area.Bottom - origin.Y, content.Width + Gap.Width * scale, content.Height + Gap.Height * scale))
        {
            var unit = new RectangleF((float)tile.X + offsetX, (float)tile.Y + offsetY, (float)tile.Width, (float)tile.Height);
            if (!unit.IntersectsWith(new RectangleF(0, 0, area.Width, area.Height))) continue;
            graphics.SetClip(unit);
            foreach (var (x, y) in new[] { (tile.FirstX, tile.FirstY), (tile.SecondX, tile.SecondY) })
            {
                graphics.ResetTransform();
                graphics.TranslateTransform((float)x + offsetX, (float)y + offsetY);
                graphics.RotateTransform(Rotate);
                if (Image is { } image) graphics.DrawImage(image, -content.Width / 2, -content.Height / 2, content.Width, content.Height);
                else
                    for (var i = 0; i < lines.Length; i++)
                        graphics.DrawString(lines[i], font, brush, new PointF(0, (float)WatermarkTiles.LineOffset(i, lines.Length, lineHeight)), centered);
            }
        }
        return bitmap;
    }

    /// <summary>水印窗口盖住控件看得见的部分（整个窗体时是窗体的客户区）；看不见时藏起来。</summary>
    void Present()
    {
        var empty = Image is null && WatermarkTiles.Lines(Text).Length == 0;
        if (empty || form is not { Visible: true, IsHandleCreated: true } || form.WindowState == FormWindowState.Minimized || !Visible || !IsHandleCreated)
        {
            overlay.Dismiss();
            return;
        }
        var bounds = Fullscreen ? form.RectangleToScreen(form.ClientRectangle) : RectangleToScreen(ClientRectangle);
        var area = Fullscreen ? bounds : VisibleArea(bounds);
        if (area.Width <= 0 || area.Height <= 0)
        {
            overlay.Dismiss();
            return;
        }
        if (overlay.Owner != form) overlay.Owner = form;
        overlay.Present(Render(area, bounds.Location), area.Location, Rectangle.Empty, null, TimeSpan.Zero);
    }

    /// <summary>控件在屏幕上露出来的部分：被滚出去、被外层裁掉的不算。</summary>
    Rectangle VisibleArea(Rectangle bounds)
    {
        for (var parent = Parent; parent is not null; parent = parent.Parent)
            bounds.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
        return bounds;
    }

    /// <summary>重新盯住自己与各层父控件、所在窗体：位置、大小、显示隐藏变了都要挪水印窗口。</summary>
    void Watch()
    {
        Unwatch();
        for (Control? control = this; control is not null; control = control.Parent)
        {
            control.Move += OnMoved;
            control.Resize += OnMoved;
            control.VisibleChanged += OnMoved;
            if (control != this) control.ParentChanged += OnReparented;
            watched.Add(control);
        }
        form = FindForm();
        if (form is not null) form.Shown += OnMoved;
    }

    void Unwatch()
    {
        foreach (var control in watched)
        {
            control.Move -= OnMoved;
            control.Resize -= OnMoved;
            control.VisibleChanged -= OnMoved;
            control.ParentChanged -= OnReparented;
        }
        watched.Clear();
        if (form is not null) form.Shown -= OnMoved;
        form = null;
    }

    void OnMoved(object? sender, EventArgs e) => Present();

    void OnReparented(object? sender, EventArgs e)
    {
        Watch();
        Present();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Watch();
        Present();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Present();
    }

    /// <inheritdoc />
    protected override void OnHandleDestroyed(EventArgs e)
    {
        overlay.Dismiss();
        base.OnHandleDestroyed(e);
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Present();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Present();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Present();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Unwatch();
            overlay.Dispose();
        }
        base.Dispose(disposing);
    }
}
