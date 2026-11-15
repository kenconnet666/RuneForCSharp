using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Rune.Avalonia;

/// <summary>
/// 裁剪图片（vue-tsx-pro 的 ImageCropper）：头像、封面上传前裁一块。裁剪框固定在中间（按 <see cref="Aspect"/> 的宽高比，<see cref="Shape"/> 为 round 时是圆的），
/// 图片在底下拖动、缩放，框里永远是图；裁好的用 <see cref="ToBitmap"/>、<see cref="Encode"/> 拿（可以指定输出的像素大小）。
/// <code>
/// &lt;rune:ImageCropper x:Name="Avatar" Source="{Binding Picked}" Shape="round" /&gt;
/// var png = Avatar.Encode(new ImageCropFormat { Size = new(256) });
/// </code>
/// 建在 <see cref="PanZoom"/> 上（Frame 是裁剪框，Cover 让缩放下限正好盖满它）：拖动平移、滚轮以指针为中心缩放、双击放大；
/// 区域能聚焦，+ / − 缩放、0 还原、方向键微调。下面的工具条：缩小、缩放滑块、放大、还原。
/// 区域高 <see cref="ViewportHeight"/>（默认 18em），裁剪框离边至少 <see cref="FrameInset"/>。读屏：区域是一组，名称是 AutomationProperties.Name（默认“裁剪图片”），说明是操作提示。
/// 部件：<see cref="ImageCropperParts"/>（图片、遮罩与裁剪框画在区域里）。
/// </summary>
public class ImageCropper : Control
{
    /// <summary>图片地址（同 <see cref="Image.Source"/>）。</summary>
    public static readonly StyledProperty<Uri?> SourceProperty = AvaloniaProperty.Register<ImageCropper, Uri?>(nameof(Source));

    /// <summary>裁剪框的宽高比（宽 ÷ 高），默认 1。</summary>
    public static readonly StyledProperty<double> AspectProperty = AvaloniaProperty.Register<ImageCropper, double>(nameof(Aspect), 1, validate: value => value > 0);

    /// <summary>裁剪框的形状，默认 rect。</summary>
    public static readonly StyledProperty<ImageCropShape> ShapeProperty = AvaloniaProperty.Register<ImageCropper, ImageCropShape>(nameof(Shape), ImageCropShape.Rect);

    /// <summary>最多放大到盖满裁剪框时的几倍，默认 4。</summary>
    public static readonly StyledProperty<double> MaxZoomProperty = AvaloniaProperty.Register<ImageCropper, double>(nameof(MaxZoom), 4, validate: value => value >= 1);

    /// <summary>区域的高度；不写（NaN）时 18em。</summary>
    public static readonly StyledProperty<double> ViewportHeightProperty = AvaloniaProperty.Register<ImageCropper, double>(nameof(ViewportHeight), double.NaN);

    /// <summary>裁剪框离区域边缘至少多远，默认 24。</summary>
    public static readonly StyledProperty<double> FrameInsetProperty = AvaloniaProperty.Register<ImageCropper, double>(nameof(FrameInset), 24);

    /// <summary>字号：区域的默认高度、圆角、间距按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = global::Avalonia.Controls.Documents.TextElement.FontSizeProperty.AddOwner<ImageCropper>();

    readonly Viewport viewport;
    readonly IconButton zoomOut = Tool(Lucide.ZoomOut);
    readonly Slider slider = new() { Min = 1, Step = 0.01, Size = TextSize.Sm, ValueText = value => $"{Math.Round(value * 100)}%" };
    readonly IconButton zoomIn = Tool(Lucide.ZoomIn);
    readonly IconButton reset = Tool(Lucide.RotateCcw);
    readonly Flex toolbar = new() { Name = ImageCropperParts.Toolbar, Align = FlexAlign.Center, Gap = Space.Xs };
    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
    Bitmap? bitmap;
    int loading;
    bool syncing;

    /// <summary>新建：区域在上、工具条在下。</summary>
    public ImageCropper()
    {
        viewport = new Viewport(this) { Name = ImageCropperParts.Viewport };
        View = new PanZoom(() => (viewport.Bounds.Width, viewport.Bounds.Height))
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
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty && !syncing) View.ZoomTo(CoverScale * slider.Value, FrameCenter);
        };
        Flex.SetGrow(slider, true);
        toolbar.Children.AddRange([zoomOut, slider, zoomIn, reset]);
        root.Children.AddRange([viewport, toolbar]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Sync();
    }

    /// <summary>图片地址。</summary>
    public Uri? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>裁剪框的宽高比。</summary>
    public double Aspect { get => GetValue(AspectProperty); set => SetValue(AspectProperty, value); }

    /// <summary>裁剪框的形状。</summary>
    public ImageCropShape Shape { get => GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    /// <summary>最多放大到盖满裁剪框时的几倍。</summary>
    public double MaxZoom { get => GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    /// <summary>区域的高度。</summary>
    public double ViewportHeight { get => GetValue(ViewportHeightProperty); set => SetValue(ViewportHeightProperty, value); }

    /// <summary>裁剪框离区域边缘至少多远。</summary>
    public double FrameInset { get => GetValue(FrameInsetProperty); set => SetValue(FrameInsetProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>平移与缩放的状态（测试用）。</summary>
    internal PanZoom View { get; }

    /// <summary>图片原本的大小；读好之前 null。</summary>
    internal Size? Natural { get; private set; }

    /// <summary>缩小、放大、还原按钮与缩放滑块（测试用）。</summary>
    internal (IconButton ZoomOut, IconButton ZoomIn, IconButton Reset, Slider Zoom) Tools => (zoomOut, zoomIn, reset, slider);

    /// <summary>区域（测试用）。</summary>
    internal Control ViewportPart => viewport;

    /// <summary>裁剪框（区域内坐标）。</summary>
    internal PanZoomRegion FrameRegion => ImageCropGeometry.Frame(viewport.Bounds.Width, viewport.Bounds.Height, Aspect, FrameInset);

    (double X, double Y) FrameCenter
    {
        get
        {
            var frame = FrameRegion;
            return (frame.X + frame.Width / 2, frame.Y + frame.Height / 2);
        }
    }

    /// <summary>正好盖满裁剪框的缩放。</summary>
    double CoverScale => ImageCropGeometry.CoverScale(FrameRegion, Natural is { } size ? (size.Width, size.Height) : null);

    /// <summary>相对盖满时放大了几倍（滑块的值）。</summary>
    internal double Zoom => View.State.Scale / CoverScale;

    /// <summary>裁好的图（输出的像素大小见 <paramref name="output"/>）；图片还没读好时 null。圆形时四角透明。</summary>
    public Bitmap? ToBitmap(ImageCropOutput output = default)
    {
        if (bitmap is null || Natural is null) return null;
        var source = ImageCropGeometry.Source(FrameRegion, View.State);
        var (width, height) = ImageCropGeometry.OutputSize(source, Aspect, output);
        var target = new RenderTargetBitmap(new PixelSize(width, height));
        using var context = target.CreateDrawingContext();
        using var quality = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality });
        var box = new Rect(0, 0, width, height);
        using var clip = Shape.IsRound ? context.PushGeometryClip(new EllipseGeometry(box)) : default(DrawingContext.PushedState?);
        context.DrawImage(bitmap, new Rect(source.X, source.Y, source.Width, source.Height), box);
        return target;
    }

    /// <summary>裁好的图编码成 PNG 或 JPEG 的字节（交给上传、存文件）；图片还没读好时 null。</summary>
    public byte[]? Encode(ImageCropFormat? format = null)
    {
        format ??= new ImageCropFormat();
        using var cropped = ToBitmap(format.Size);
        if (cropped is null) return null;
        using var stream = new MemoryStream();
        BitmapEncoderOptions options = format.Type == ImageCropFileType.Jpeg
            ? format.Quality is { } quality ? new JpegBitmapEncoderOptions { Quality = (int)Math.Round(Math.Clamp(quality, 0, 1) * 100) } : JpegBitmapEncoderOptions.Default
            : PngBitmapEncoderOptions.Default;
        cropped.Save(stream, options);
        return stream.ToArray();
    }

    /// <summary>回到初始：图片盖满裁剪框、居中。</summary>
    public void Reset() => View.Reset();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _ = Load();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            if (VisualRoot is not null) _ = Load();
        }
        else if (change.Property == AspectProperty || change.Property == FrameInsetProperty || change.Property == MaxZoomProperty) Restart();
        else if (change.Property == ShapeProperty) viewport.InvalidateVisual();
        else if (change.Property == ViewportHeightProperty || change.Property == FontSizeProperty) InvalidateMeasure();
        else if (change.Property == AutomationProperties.NameProperty) Sync();
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
            if (version != loading) return;
            bitmap = loaded;
            Natural = loaded.Size;
            Restart();
        }
        catch (Exception error)
        {
            if (version == loading) System.Diagnostics.Trace.TraceWarning($"ImageCropper 读不了图片 {source}：{error.Message}");
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
        var lang = Provider.GetLang(this);
        viewport.InvalidateVisual();
        var ready = Natural is not null;
        var zoom = Zoom;
        syncing = true;
        slider.Max = MaxZoom;
        slider.Value = Math.Clamp(zoom, 1, MaxZoom);
        syncing = false;
        slider.IsEnabled = reset.IsEnabled = ready;
        zoomOut.IsEnabled = ready && zoom > 1.001;
        zoomIn.IsEnabled = ready && zoom < MaxZoom - 0.001;
        AutomationProperties.SetName(zoomOut, lang.ZoomOut);
        AutomationProperties.SetName(zoomIn, lang.ZoomIn);
        AutomationProperties.SetName(reset, lang.ResetView);
        AutomationProperties.SetName(slider, lang.CropZoom);
        AutomationProperties.SetName(viewport, AutomationProperties.GetName(this) is { Length: > 0 } name ? name : lang.CropLabel);
        AutomationProperties.SetHelpText(viewport, lang.CropHint);
    }

    /// <summary>区域高 ViewportHeight（默认 18em）。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        viewport.Height = double.IsNaN(ViewportHeight) ? 18 * FontSize : ViewportHeight;
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    static IconButton Tool(IconData icon) => new() { Icon = icon, Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };

    /// <summary>
    /// 区域：surfaceMuted 底、md 圆角，图片按平移与缩放画在里面；裁剪框四周压暗（黑 50%），框留一条白 80% 的细边（圆形时是椭圆）。
    /// 拖动平移（挪开 3 以内不算）、滚轮缩放、双击放大；能聚焦（键盘聚焦时外面一圈焦点环），+ / − / 0 / 方向键。
    /// </summary>
    sealed class Viewport : Control
    {
        static readonly IBrush Scrim = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));
        static readonly IPen Outline = new Pen(new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)));

        readonly ImageCropper owner;
        Point? pressedAt;
        Point lastDrag;
        bool dragging;

        public Viewport(ImageCropper owner)
        {
            this.owner = owner;
            Focusable = true;
            ClipToBounds = false;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            owner.Restart();
        }

        double Radius => this.Resource<double>(ThemeKeys.Radius.Md) * owner.FontSize;

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            var shape = new RoundedRect(bounds, Radius);
            using (context.PushClip(shape))
            {
                context.DrawRectangle(this.Resource<IBrush>(ThemeKeys.Brush.SurfaceMuted), null, bounds);
                if (owner.bitmap is { } bitmap && owner.Natural is { } natural)
                {
                    var state = owner.View.State;
                    using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
                        context.DrawImage(bitmap, new Rect(natural), new Rect(state.X, state.Y, natural.Width * state.Scale, natural.Height * state.Scale));
                }
                var region = owner.FrameRegion;
                var frame = new Rect(region.X, region.Y, region.Width, region.Height);
                Geometry hole = owner.Shape.IsRound ? new EllipseGeometry(frame) : new RectangleGeometry(frame);
                context.DrawGeometry(Scrim, null, new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(bounds), hole));
                context.DrawGeometry(null, Outline, hole);
            }
            // 键盘聚焦：外面一圈 2 宽、离开 2 的焦点环（focusRingStyles.visible）
            if (Classes.Contains(":focus-visible"))
                context.DrawRectangle(null, new Pen(this.Resource<IBrush>(ThemeKeys.Brush.FocusRing), 2), new RoundedRect(bounds.Inflate(3), Radius + 3));
        }

        protected override void OnGotFocus(FocusChangedEventArgs e)
        {
            base.OnGotFocus(e);
            InvalidateVisual();
        }

        protected override void OnLostFocus(FocusChangedEventArgs e)
        {
            base.OnLostFocus(e);
            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            Focus(NavigationMethod.Pointer);
            pressedAt = lastDrag = e.GetPosition(this);
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        /// <summary>拖动按增量平移；挪开 3 以内不算拖（点一下、双击不会抖）。</summary>
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (pressedAt is not { } from) return;
            var point = e.GetPosition(this);
            if (!dragging)
            {
                var moved = point - from;
                if (Math.Sqrt(moved.X * moved.X + moved.Y * moved.Y) < 3) return;
                dragging = true;
                Cursor = new Cursor(StandardCursorType.SizeAll);
            }
            var step = point - lastDrag;
            lastDrag = point;
            owner.View.PanBy(step.X, step.Y);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            Release();
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            Release();
        }

        void Release()
        {
            pressedAt = null;
            dragging = false;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        protected override void OnDoubleTapped(TappedEventArgs e)
        {
            base.OnDoubleTapped(e);
            var point = e.GetPosition(this);
            owner.View.DoubleTap((point.X, point.Y));
            e.Handled = true;
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            var point = e.GetPosition(this);
            owner.View.Wheel(-e.Delta.Y, (point.X, point.Y));
            e.Handled = true;
        }

        /// <summary>+ / = 放大、− 缩小、0 还原、方向键微调。</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return;
            PanZoomKey? key = e.Key switch
            {
                Key.OemPlus or Key.Add => PanZoomKey.ZoomIn,
                Key.OemMinus or Key.Subtract => PanZoomKey.ZoomOut,
                Key.D0 or Key.NumPad0 => PanZoomKey.Reset,
                Key.Left => PanZoomKey.Left,
                Key.Right => PanZoomKey.Right,
                Key.Up => PanZoomKey.Up,
                Key.Down => PanZoomKey.Down,
                _ => null,
            };
            if (key is not { } pressed) return;
            owner.View.Press(pressed);
            e.Handled = true;
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new ViewportPeer(this);
    }

    /// <summary>区域对读屏是一组（TS 的 role="group"），名称与说明写在区域上。</summary>
    sealed class ViewportPeer(Control owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

/// <summary>ImageCropper 的部件名。</summary>
public static class ImageCropperParts
{
    /// <summary>区域（TS 的 propsViewport）。</summary>
    public const string Viewport = "PART_Viewport";

    /// <summary>工具条（TS 的 propsToolbar）。</summary>
    public const string Toolbar = "PART_Toolbar";
}
