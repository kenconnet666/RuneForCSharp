using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Rune.Wpf;

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
/// 图片原本的大小按像素算（不按图片里记的 DPI）。
/// </summary>
public class ImageCropper : CompositeControl
{
    /// <summary>图片地址（同 <see cref="Image.Source"/>）。</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(Uri), typeof(ImageCropper),
        new FrameworkPropertyMetadata(null, (d, _) => ((ImageCropper)d).OnSourceChanged()));

    /// <summary>裁剪框的宽高比（宽 ÷ 高），默认 1。</summary>
    public static readonly DependencyProperty AspectProperty = DependencyProperty.Register(nameof(Aspect), typeof(double), typeof(ImageCropper),
        new FrameworkPropertyMetadata(1d, (d, _) => ((ImageCropper)d).Restart()), value => value is double aspect && aspect > 0);

    /// <summary>裁剪框的形状，默认 rect。</summary>
    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(nameof(Shape), typeof(ImageCropShape), typeof(ImageCropper),
        new FrameworkPropertyMetadata(ImageCropShape.Rect, (d, _) => ((ImageCropper)d).viewport.InvalidateVisual()));

    /// <summary>最多放大到盖满裁剪框时的几倍，默认 4。</summary>
    public static readonly DependencyProperty MaxZoomProperty = DependencyProperty.Register(nameof(MaxZoom), typeof(double), typeof(ImageCropper),
        new FrameworkPropertyMetadata(4d, (d, _) => ((ImageCropper)d).Restart()), value => value is double zoom && zoom >= 1);

    /// <summary>区域的高度；不写（NaN）时 18em。</summary>
    public static readonly DependencyProperty ViewportHeightProperty = DependencyProperty.Register(nameof(ViewportHeight), typeof(double), typeof(ImageCropper),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>裁剪框离区域边缘至少多远，默认 24。</summary>
    public static readonly DependencyProperty FrameInsetProperty = DependencyProperty.Register(nameof(FrameInset), typeof(double), typeof(ImageCropper),
        new FrameworkPropertyMetadata(24d, (d, _) => ((ImageCropper)d).Restart()));

    readonly Viewport viewport;
    readonly IconButton zoomOut = Tool(Lucide.ZoomOut);
    readonly Slider slider = new() { Min = 1, Step = 0.01, Size = TextSize.Sm, ValueText = value => $"{Math.Round(value * 100)}%" };
    readonly IconButton zoomIn = Tool(Lucide.ZoomIn);
    readonly IconButton reset = Tool(Lucide.RotateCcw);
    BitmapSource? bitmap;
    int loading;
    bool syncing;

    static ImageCropper()
    {
        FocusableProperty.OverrideMetadata(typeof(ImageCropper), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(ImageCropper), new FrameworkPropertyMetadata(false));
        AutomationProperties.NameProperty.OverrideMetadata(typeof(ImageCropper), new FrameworkPropertyMetadata("", (d, _) => ((ImageCropper)d).Sync()));
    }

    /// <summary>新建：区域在上、工具条在下。</summary>
    public ImageCropper()
    {
        viewport = new Viewport(this);
        View = new PanZoom(() => (viewport.ActualWidth, viewport.ActualHeight))
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
        DependencyPropertyDescriptor.FromProperty(Slider.ValueProperty, typeof(Slider)).AddValueChanged(slider, (_, _) =>
        {
            if (!syncing) View.ZoomTo(CoverScale * slider.Value, FrameCenter);
        });
        Flex.SetGrow(slider, true);
        var toolbar = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { zoomOut, slider, zoomIn, reset } };
        Root = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Children = { viewport, toolbar } };
        Loaded += (_, _) => _ = Load();
        Sync();
    }

    /// <summary>图片地址。</summary>
    public Uri? Source { get => (Uri?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>裁剪框的宽高比。</summary>
    public double Aspect { get => (double)GetValue(AspectProperty); set => SetValue(AspectProperty, value); }

    /// <summary>裁剪框的形状。</summary>
    public ImageCropShape Shape { get => (ImageCropShape)GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    /// <summary>最多放大到盖满裁剪框时的几倍。</summary>
    public double MaxZoom { get => (double)GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    /// <summary>区域的高度。</summary>
    public double ViewportHeight { get => (double)GetValue(ViewportHeightProperty); set => SetValue(ViewportHeightProperty, value); }

    /// <summary>裁剪框离区域边缘至少多远。</summary>
    public double FrameInset { get => (double)GetValue(FrameInsetProperty); set => SetValue(FrameInsetProperty, value); }

    /// <summary>平移与缩放的状态（测试用）。</summary>
    internal PanZoom View { get; }

    /// <summary>图片原本的像素大小；读好之前 null。</summary>
    internal Size? Natural { get; private set; }

    /// <summary>缩小、放大、还原按钮与缩放滑块（测试用）。</summary>
    internal (IconButton ZoomOut, IconButton ZoomIn, IconButton Reset, Slider Zoom) Tools => (zoomOut, zoomIn, reset, slider);

    /// <summary>区域（测试用）。</summary>
    internal FrameworkElement ViewportPart => viewport;

    /// <summary>裁剪框（区域内坐标）。</summary>
    internal PanZoomRegion FrameRegion => ImageCropGeometry.Frame(viewport.ActualWidth, viewport.ActualHeight, Aspect, FrameInset);

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

    /// <summary>裁好的图（输出的像素大小见 <paramref name="output"/>）；图片还没读好时 null。圆形时四角透明。</summary>
    public BitmapSource? ToBitmap(ImageCropOutput output = default)
    {
        if (bitmap is null || Natural is not { } natural) return null;
        var source = ImageCropGeometry.Source(FrameRegion, View.State);
        var (width, height) = ImageCropGeometry.OutputSize(source, Aspect, output);
        var (kx, ky) = (width / source.Width, height / source.Height);
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var context = visual.RenderOpen())
        {
            var box = new Rect(0, 0, width, height);
            context.PushClip(Shape.IsRound ? new EllipseGeometry(box) : new RectangleGeometry(box));
            // 整张图按输出的比例画，裁剪框那一块正好落在 (0, 0, width, height)
            context.DrawImage(bitmap, new Rect(-source.X * kx, -source.Y * ky, natural.Width * kx, natural.Height * ky));
            context.Pop();
        }
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    /// <summary>裁好的图编码成 PNG 或 JPEG 的字节（交给上传、存文件）；图片还没读好时 null。</summary>
    public byte[]? Encode(ImageCropFormat? format = null)
    {
        format ??= new ImageCropFormat();
        if (ToBitmap(format.Size) is not { } cropped) return null;
        BitmapEncoder encoder = format.Type == ImageCropFileType.Jpeg
            ? new JpegBitmapEncoder { QualityLevel = format.Quality is { } quality ? Math.Clamp((int)Math.Round(quality * 100), 1, 100) : 92 }
            : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(cropped));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>回到初始：图片盖满裁剪框、居中。</summary>
    public void Reset() => View.Reset();

    void OnSourceChanged()
    {
        if (IsLoaded) _ = Load();
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
            Natural = new Size(loaded.PixelWidth, loaded.PixelHeight);
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
    protected override Size MeasureOverride(Size constraint)
    {
        viewport.Height = double.IsNaN(ViewportHeight) ? 18 * FontSize : ViewportHeight;
        return base.MeasureOverride(constraint);
    }

    static IconButton Tool(IconData icon) => new() { Icon = icon, Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };

    /// <summary>
    /// 区域：surfaceMuted 底、md 圆角，图片按平移与缩放画在里面；裁剪框四周压暗（黑 50%），框留一条白 80% 的细边（圆形时是椭圆）。
    /// 拖动平移（挪开 3 以内不算）、滚轮缩放、双击放大；能聚焦（键盘聚焦时外面一圈焦点环），+ / − / 0 / 方向键。
    /// </summary>
    sealed class Viewport : FrameworkElement
    {
        static readonly Brush Scrim = Frozen(new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)));
        static readonly Pen Outline = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromArgb(204, 255, 255, 255))), 1));

        readonly ImageCropper owner;
        Point? pressedAt;
        Point lastDrag;
        bool dragging;
        bool focusVisible;

        public Viewport(ImageCropper owner)
        {
            this.owner = owner;
            Focusable = true;
            FocusVisualStyle = null;
            Cursor = Cursors.Hand;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            SizeChanged += (_, _) => owner.Restart();
        }

        static T Frozen<T>(T freezable) where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }

        double Radius => this.Resource<double>(ThemeKeys.Radius.Md) * TextElement.GetFontSize(this);

        protected override void OnRender(DrawingContext context)
        {
            var bounds = new Rect(RenderSize);
            context.PushClip(new RectangleGeometry(bounds, Radius, Radius));
            context.DrawRectangle(this.Resource<Brush>(ThemeKeys.Brush.SurfaceMuted), null, bounds);
            if (owner.bitmap is { } bitmap && owner.Natural is { } natural)
            {
                var state = owner.View.State;
                context.DrawImage(bitmap, new Rect(state.X, state.Y, natural.Width * state.Scale, natural.Height * state.Scale));
            }
            var region = owner.FrameRegion;
            var frame = new Rect(region.X, region.Y, region.Width, region.Height);
            Geometry hole = owner.Shape.IsRound ? new EllipseGeometry(frame) : new RectangleGeometry(frame);
            context.DrawGeometry(Scrim, null, new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(bounds), hole));
            context.DrawGeometry(null, Outline, hole);
            context.Pop();
            // 键盘聚焦：外面一圈 2 宽、离开 2 的焦点环（focusRingStyles.visible）
            if (focusVisible && IsKeyboardFocused)
            {
                var ring = Rect.Inflate(bounds, 3, 3);
                context.DrawRoundedRectangle(null, new Pen(this.Resource<Brush>(ThemeKeys.Brush.FocusRing), 2), ring, Radius + 3, Radius + 3);
            }
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            focusVisible = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
            InvalidateVisual();
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            var point = e.GetPosition(this);
            if (e.ClickCount == 2)
            {
                owner.View.DoubleTap((point.X, point.Y));
                e.Handled = true;
                return;
            }
            pressedAt = lastDrag = point;
            CaptureMouse();
            e.Handled = true;
        }

        /// <summary>拖动按增量平移；挪开 3 以内不算拖（点一下、双击不会抖）。</summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (pressedAt is not { } from) return;
            var point = e.GetPosition(this);
            if (!dragging)
            {
                if ((point - from).Length < 3) return;
                dragging = true;
                Cursor = Cursors.SizeAll;
            }
            var step = point - lastDrag;
            lastDrag = point;
            owner.View.PanBy(step.X, step.Y);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            ReleaseMouseCapture();
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            pressedAt = null;
            dragging = false;
            Cursor = Cursors.Hand;
        }

        /// <summary>滚轮：一格（120）算 1。</summary>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            var point = e.GetPosition(this);
            owner.View.Wheel(-e.Delta / 120d, (point.X, point.Y));
            e.Handled = true;
        }

        /// <summary>+ / = 放大、− 缩小、0 还原、方向键微调。</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0) return;
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
    sealed class ViewportPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => "ImageCropperViewport";
    }
}
