using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Rune.WinForms;

/// <summary>
/// 裁剪图片（vue-tsx-pro 的 ImageCropper）：头像、封面上传前裁一块。裁剪框固定在中间（按 <see cref="Aspect"/> 的宽高比，<see cref="Shape"/> 为 round 时是圆的），
/// 图片在底下拖动、缩放，框里永远是图；裁好的用 <see cref="ToBitmap"/>、<see cref="Encode"/> 拿（可以指定输出的像素大小）。
/// <code>
/// var avatar = new ImageCropperControl { Source = new Uri(path), Shape = ImageCropShape.Round, AccessibleName = "裁剪头像" };
/// var png = avatar.Encode(new ImageCropFormat { Size = new(256) });
/// </code>
/// 建在 <see cref="PanZoom"/> 上（Frame 是裁剪框，Cover 让缩放下限正好盖满它）：拖动平移、滚轮以指针为中心缩放、双击放大；
/// 区域能聚焦，+ / − 缩放、0 还原、方向键微调。下面的工具条：缩小、缩放滑块、放大、还原。
/// 区域高 <see cref="ViewportHeight"/>（逻辑像素，默认 18em），裁剪框离边至少 <see cref="FrameInset"/>（逻辑像素，按 DPI 放大）。
/// 读屏：区域是一组，名称是 AccessibleName（默认“裁剪图片”），说明是操作提示。图片原本的大小按像素算。
/// </summary>
[Description("裁剪图片：固定的裁剪框，图片在底下拖动缩放。")]
public class ImageCropperControl : Control
{
    readonly Viewport viewport;
    readonly IconButtonControl zoomOut = Tool(Lucide.ZoomOut);
    readonly SliderControl slider = new() { Min = 1, Step = 0.01, TextSize = TextSize.Sm, ValueText = value => $"{Math.Round(value * 100)}%" };
    readonly IconButtonControl zoomIn = Tool(Lucide.ZoomIn);
    readonly IconButtonControl reset = Tool(Lucide.RotateCcw);
    readonly FlexControl toolbar = new() { Align = FlexAlign.Center, Gap = Space.Xs, AutoSize = true };
    Bitmap? bitmap;
    int loading;
    bool syncing;

    /// <summary>新建：区域在上、工具条在下。</summary>
    public ImageCropperControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        TabStop = false;
        AutoSize = true;
        viewport = new Viewport(this);
        View = new PanZoom(() => (viewport.Width, viewport.Height))
        {
            Step = 1.1,
            Cover = true,
            Bounds = () => Natural is { } size ? (size.Width, size.Height) : null,
            Frame = () => FrameRegion,
        };
        View.Changed += Sync;
        zoomOut.Click += (_, _) => View.ZoomOut(FrameCenter);
        zoomIn.Click += (_, _) => View.ZoomIn(FrameCenter);
        reset.Click += (_, _) => View.Reset();
        slider.ValueChanged += (_, _) =>
        {
            if (!syncing) View.ZoomTo(CoverScale * slider.Value, FrameCenter);
        };
        toolbar.SetGrow(slider, true);
        toolbar.Controls.AddRange([zoomOut, slider, zoomIn, reset]);
        Controls.AddRange([viewport, toolbar]);
        Sync();
    }

    /// <summary>图片地址（同 <see cref="ImageControl.Source"/>）。</summary>
    [Category("Appearance")]
    [Description("图片地址。")]
    [DefaultValue(null)]
    public Uri? Source
    {
        get;
        set
        {
            field = value;
            if (IsHandleCreated) _ = Load();
        }
    }

    /// <summary>裁剪框的宽高比（宽 ÷ 高），默认 1。</summary>
    [Category("Behavior")]
    [Description("裁剪框的宽高比（宽 ÷ 高）。")]
    [DefaultValue(1d)]
    public double Aspect
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
            Restart();
        }
    } = 1;

    /// <summary>裁剪框的形状，默认 rect。</summary>
    [Category("Appearance")]
    [Description("裁剪框的形状：rect、round（输出的图四角透明）。")]
    public ImageCropShape Shape { get; set { field = value; viewport.Invalidate(); } } = ImageCropShape.Rect;

    bool ShouldSerializeShape() => Shape != ImageCropShape.Rect;

    void ResetShape() => Shape = ImageCropShape.Rect;

    /// <summary>最多放大到盖满裁剪框时的几倍，默认 4。</summary>
    [Category("Behavior")]
    [Description("最多放大到盖满裁剪框时的几倍。")]
    [DefaultValue(4d)]
    public double MaxZoom
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
            Restart();
        }
    } = 4;

    /// <summary>区域的高度（逻辑像素）；不写时 18em。</summary>
    [Category("Layout")]
    [Description("区域的高度（逻辑像素）；不写时 18em。")]
    [DefaultValue(null)]
    public int? ViewportHeight { get; set { field = value; Relayout(); } }

    /// <summary>裁剪框离区域边缘至少多远（逻辑像素），默认 24。</summary>
    [Category("Layout")]
    [Description("裁剪框离区域边缘至少多远（逻辑像素）。")]
    [DefaultValue(24)]
    public int FrameInset { get; set { field = value; Restart(); } } = 24;

    /// <summary>平移与缩放的状态（测试用）。</summary>
    internal PanZoom View { get; }

    /// <summary>图片原本的像素大小；读好之前 null。</summary>
    internal Size? Natural { get; private set; }

    /// <summary>缩小、放大、还原按钮与缩放滑块（测试用）。</summary>
    internal (IconButtonControl ZoomOut, IconButtonControl ZoomIn, IconButtonControl Reset, SliderControl Zoom) Tools => (zoomOut, zoomIn, reset, slider);

    /// <summary>区域（测试用）。</summary>
    internal Control ViewportPart => viewport;

    /// <summary>裁剪框（区域内坐标）。</summary>
    internal PanZoomRegion FrameRegion => ImageCropGeometry.Frame(viewport.Width, viewport.Height, Aspect, LogicalToDeviceUnits(FrameInset));

    (double X, double Y) FrameCenter
    {
        get
        {
            var frame = FrameRegion;
            return (frame.X + frame.Width / 2, frame.Y + frame.Height / 2);
        }
    }

    double CoverScale => ImageCropGeometry.CoverScale(FrameRegion, Natural is { } size ? (size.Width, size.Height) : null);

    /// <summary>相对盖满时放大了几倍（滑块的值）。</summary>
    internal double Zoom => View.State.Scale / CoverScale;

    /// <summary>裁好的图（输出的像素大小见 <paramref name="output"/>，用完由调用方释放）；图片还没读好时 null。圆形时四角透明（边缘抗锯齿）。</summary>
    public Bitmap? ToBitmap(ImageCropOutput output = default)
    {
        if (bitmap is null || Natural is null) return null;
        var source = ImageCropGeometry.Source(FrameRegion, View.State);
        var (width, height) = ImageCropGeometry.OutputSize(source, Aspect, output);
        var cropped = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(cropped))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var from = new RectangleF((float)source.X, (float)source.Y, (float)source.Width, (float)source.Height);
            if (!Shape.IsRound)
            {
                DrawCrop(graphics, bitmap, width, height, from);
                return cropped;
            }
            // 圆形：先裁成矩形，再用它当画刷填一个抗锯齿的椭圆（裁剪区域没有抗锯齿）
            using var square = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var inner = Graphics.FromImage(square)) DrawCrop(inner, bitmap, width, height, from);
            using var brush = new TextureBrush(square);
            graphics.FillEllipse(brush, 0, 0, width, height);
        }
        return cropped;
    }

    /// <summary>
    /// 把图片的 from 那一块画满 width × height。高质量插值在边上会取到图片外面（透明）的像素，四角半透明；
    /// 镜像平铺让边外取到的是图片自己。
    /// </summary>
    static void DrawCrop(Graphics graphics, Bitmap bitmap, int width, int height, RectangleF from)
    {
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        graphics.DrawImage(bitmap, new Rectangle(0, 0, width, height), from.X, from.Y, from.Width, from.Height, GraphicsUnit.Pixel, attributes);
    }

    /// <summary>裁好的图编码成 PNG 或 JPEG 的字节（交给上传、存文件）；图片还没读好时 null。</summary>
    public byte[]? Encode(ImageCropFormat? format = null)
    {
        format ??= new ImageCropFormat();
        using var cropped = ToBitmap(format.Size);
        if (cropped is null) return null;
        using var stream = new MemoryStream();
        if (format.Type == ImageCropFileType.Jpeg)
        {
            var codec = ImageCodecInfo.GetImageEncoders().First(encoder => encoder.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)Math.Round(Math.Clamp(format.Quality ?? 0.92, 0, 1) * 100));
            cropped.Save(stream, codec, parameters);
        }
        else cropped.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    /// <summary>回到初始：图片盖满裁剪框、居中。</summary>
    public void Reset() => View.Reset();

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _ = Load();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 语言包、字号取自外层：放进界面后重新生成名称、算尺寸
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    /// <summary>区域的名称（读屏念区域）；不写时“裁剪图片”。</summary>
    [Category("Accessibility")]
    [Description("区域的名称；不写时“裁剪图片”。")]
    [DefaultValue(null)]
    public new string? AccessibleName
    {
        get => base.AccessibleName;
        set
        {
            base.AccessibleName = value;
            Sync();
        }
    }

    /// <summary>读图：读到之前区域是空的、导出 null。换得快时只认最后一次。</summary>
    async Task Load()
    {
        var version = ++loading;
        bitmap = null;
        Natural = null;
        Restart();
        if (Source is not { } source) return;
        try
        {
            var loaded = await ImageLoader.LoadAsync(source);
            if (version != loading || IsDisposed) return;
            bitmap = loaded;
            Natural = loaded.Size;
            Restart();
        }
        catch (Exception error)
        {
            if (version == loading) System.Diagnostics.Trace.TraceWarning($"ImageCropperControl 读不了图片 {source}：{error.Message}");
        }
    }

    /// <summary>换了图、区域大小、宽高比：缩放范围跟着盖满的比例，回到初始（盖满、居中）。</summary>
    internal void Restart()
    {
        var cover = CoverScale;
        View.Min = cover;
        View.Max = cover * MaxZoom;
        View.Initial = ImageCropGeometry.Initial(FrameRegion, Natural is { } size ? (size.Width, size.Height) : null);
        View.Reset();
        Sync();
    }

    /// <summary>区域重画；滑块与两头的按钮跟着缩放（盖满时不能再缩小，到 MaxZoom 不能再放大）。</summary>
    void Sync()
    {
        var lang = ProviderControl.LangOf(this);
        viewport.Invalidate();
        var ready = Natural is not null;
        var zoom = Zoom;
        syncing = true;
        slider.Max = MaxZoom;
        slider.Value = Math.Clamp(zoom, 1, MaxZoom);
        syncing = false;
        slider.Enabled = reset.Enabled = ready;
        zoomOut.Enabled = ready && zoom > 1.001;
        zoomIn.Enabled = ready && zoom < MaxZoom - 0.001;
        zoomOut.AccessibleName = lang.ZoomOut;
        zoomIn.AccessibleName = lang.ZoomIn;
        reset.AccessibleName = lang.ResetView;
        slider.AccessibleName = lang.CropZoom;
        viewport.AccessibleName = AccessibleName is { Length: > 0 } name ? name : lang.CropLabel;
        viewport.AccessibleDescription = lang.CropHint;
    }

    void Relayout()
    {
        Sync();
        if (Parent is { } parent) parent.PerformLayout(this, nameof(Bounds));
        PerformLayout();
    }

    int ViewportPixels => ViewportHeight is { } height ? LogicalToDeviceUnits(height) : (int)Math.Round(18 * this.EmPixels());

    int GapPixels => (int)Math.Round(Tokens.Space.Sm * this.EmPixels());

    /// <summary>宽度取布局给的，高度是区域加间距加工具条。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = VerticalStack.Limit(this, proposedSize);
        if (width == int.MaxValue) width = Width;
        return new Size(width, ViewportPixels + GapPixels + toolbar.GetPreferredSize(new Size(width, 0)).Height);
    }

    /// <summary>区域在上（高 ViewportHeight），间距 sm，工具条在下、撑满宽度。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var height = ViewportPixels;
        viewport.Bounds = new Rectangle(0, 0, Width, height);
        var bar = toolbar.GetPreferredSize(new Size(Width, 0)).Height;
        toolbar.Bounds = new Rectangle(0, height + GapPixels, Width, bar);
    }

    static IconButtonControl Tool(IconData icon) => new() { Icon = icon, TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };

    /// <summary>
    /// 区域：surfaceMuted 底、md 圆角，图片按平移与缩放画在里面；裁剪框四周压暗（黑 50%），框留一条白 80% 的细边（圆形时是椭圆）。
    /// 拖动平移（挪开 3 以内不算）、滚轮缩放、双击放大；能聚焦（键盘聚焦时一圈焦点环），+ / − / 0 / 方向键。
    /// </summary>
    sealed class Viewport : Control
    {
        readonly ImageCropperControl owner;
        Point? pressedAt;
        Point lastDrag;
        bool dragging;

        public Viewport(ImageCropperControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            TabStop = true;
            AccessibleRole = AccessibleRole.Grouping;
            Cursor = Cursors.Hand;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            owner.Restart();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var colors = ElementContext.Of(this).Theme.Colors;
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var em = this.EmPixels();
            var radius = ElementContext.Of(this).Theme.Radii.Md * em;
            var ring = Focused && ShowFocusCues ? Math.Max(2, LogicalToDeviceUnits(2)) : 0;
            // 焦点环画在框里面的一圈（子控件画不到自己外面）
            var bounds = new RectangleF(ring * 2, ring * 2, Width - ring * 4, Height - ring * 4);
            using var shape = Drawing.RoundedRectangle(bounds, radius);
            graphics.SetClip(shape);
            using (var fill = new SolidBrush(colors.SurfaceMuted)) graphics.FillPath(fill, shape);
            if (owner.bitmap is { } bitmap && owner.Natural is { } natural)
            {
                var state = owner.View.State;
                graphics.InterpolationMode = dragging ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(bitmap, (float)state.X, (float)state.Y, (float)(natural.Width * state.Scale), (float)(natural.Height * state.Scale));
            }
            var region = owner.FrameRegion;
            var frame = new RectangleF((float)region.X, (float)region.Y, (float)region.Width, (float)region.Height);
            using var hole = new GraphicsPath();
            if (owner.Shape.IsRound) hole.AddEllipse(frame);
            else hole.AddRectangle(frame);
            using (var outside = new Region(bounds))
            {
                outside.Exclude(hole);
                using var scrim = new SolidBrush(Color.FromArgb(128, 0, 0, 0));
                graphics.FillRegion(scrim, outside);
            }
            using (var outline = new Pen(Color.FromArgb(204, 255, 255, 255), Math.Max(1, LogicalToDeviceUnits(1)))) graphics.DrawPath(outline, hole);
            graphics.ResetClip();
            if (ring == 0) return;
            using var focus = Drawing.RoundedRectangle(new RectangleF(ring / 2f, ring / 2f, Width - ring, Height - ring), radius + ring * 1.5f);
            using var pen = new Pen(colors.FocusRing, ring);
            graphics.DrawPath(pen, focus);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            pressedAt = lastDrag = e.Location;
        }

        /// <summary>拖动按增量平移；挪开 3 以内不算拖（点一下、双击不会抖）。</summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (pressedAt is not { } from) return;
            if (!dragging)
            {
                if (Math.Sqrt(Math.Pow(e.X - from.X, 2) + Math.Pow(e.Y - from.Y, 2)) < LogicalToDeviceUnits(3)) return;
                dragging = true;
                Cursor = Cursors.SizeAll;
            }
            var (dx, dy) = (e.X - lastDrag.X, e.Y - lastDrag.Y);
            lastDrag = e.Location;
            owner.View.PanBy(dx, dy);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            pressedAt = null;
            if (!dragging) return;
            dragging = false;
            Cursor = Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left) owner.View.DoubleTap((e.X, e.Y));
        }

        /// <summary>滚轮：一格（120）算 1。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            owner.View.Wheel(-e.Delta / 120d, (e.X, e.Y));
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
        }

        protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

        /// <summary>+ / = 放大、− 缩小、0 还原、方向键微调。</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control || e.Alt) return;
            PanZoomKey? key = e.KeyCode switch
            {
                Keys.Oemplus or Keys.Add => PanZoomKey.ZoomIn,
                Keys.OemMinus or Keys.Subtract => PanZoomKey.ZoomOut,
                Keys.D0 or Keys.NumPad0 => PanZoomKey.Reset,
                Keys.Left => PanZoomKey.Left,
                Keys.Right => PanZoomKey.Right,
                Keys.Up => PanZoomKey.Up,
                Keys.Down => PanZoomKey.Down,
                _ => null,
            };
            if (key is not { } pressed) return;
            owner.View.Press(pressed);
            e.Handled = true;
        }
    }
}
