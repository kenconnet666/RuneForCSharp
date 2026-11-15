using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Rune.Wpf;

/// <summary>
/// 手写签名（vue-tsx-pro 的 SignaturePad）：审批签字、签收、合同确认。用指针（鼠标、笔、手指）写，笔画按速度粗细（写得快变细），可以撤销上一笔、清空。
/// <code>
/// &lt;rune:Field Label="签收人签字"&gt;&lt;rune:SignaturePad Value="{Binding Signature}" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:SignaturePad Value="{Binding Record.Signature}" IsReadOnly="True" /&gt;
/// var svg = Pad.ToSvg();
/// </code>
/// 值 <see cref="Value"/> 是透明底的 PNG（字节；上游是 data URL），每写完一笔更新，没签时 null，默认双向绑定；父级换了值（编辑页读回的旧签名）时把它画在底下，可以接着写或清空。
/// 外框同输入框（描边、悬停、出错、只读、不可用，跟所在 Field），底色与墨色是纸面（<see cref="Paper"/>：深色主题下仍是白底深色）。高 10em（写 Height 换）、宽度占满。
/// 写字只能靠指针（签名本身就是手写）：撤销、清空是按钮，键盘照常可用。读屏：画布是一张图片，名称是自己的名称（或字段标签，默认“签名”），说明“已签名 / 未签名”。
/// </summary>
public class SignaturePad : CompositeControl, IFieldControl
{
    /// <summary>签名（透明底的 PNG 字节）；没签时 null。默认双向绑定。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(byte[]), typeof(SignaturePad),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((SignaturePad)d).OnValueChanged()));

    /// <summary>墨色；不写时是纸面的墨色。</summary>
    public static readonly DependencyProperty PenColorProperty = DependencyProperty.Register(nameof(PenColor), typeof(Color?), typeof(SignaturePad),
        new FrameworkPropertyMetadata(null, (d, _) => ((SignaturePad)d).Sync()));

    /// <summary>最细的笔宽，默认 0.75。</summary>
    public static readonly DependencyProperty PenMinWidthProperty = DependencyProperty.Register(nameof(PenMinWidth), typeof(double), typeof(SignaturePad),
        new FrameworkPropertyMetadata(0.75, (d, e) => ((SignaturePad)d).strokes.MinWidth = (double)e.NewValue));

    /// <summary>最粗的笔宽，默认 2.75。</summary>
    public static readonly DependencyProperty PenMaxWidthProperty = DependencyProperty.Register(nameof(PenMaxWidth), typeof(double), typeof(SignaturePad),
        new FrameworkPropertyMetadata(2.75, (d, e) => ((SignaturePad)d).strokes.MaxWidth = (double)e.NewValue));

    /// <summary>只读：显示已签的图，不能写；不写时取所在 Field 的。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool?), typeof(SignaturePad),
        new FrameworkPropertyMetadata(null, (d, _) => ((SignaturePad)d).Sync()));

    /// <summary>出错；不写时取所在 Field 的。</summary>
    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(nameof(IsInvalid), typeof(bool?), typeof(SignaturePad),
        new FrameworkPropertyMetadata(null, (d, _) => ((SignaturePad)d).Sync()));

    /// <summary>圆角档位，默认 md。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(Radius), typeof(SignaturePad),
        new FrameworkPropertyMetadata(Radius.Md, (d, _) => ((SignaturePad)d).Sync()));

    readonly SignatureStrokes strokes = new();
    readonly Canvas canvas;
    readonly Border shell = new() { BorderThickness = new Thickness(1), ClipToBounds = true };
    readonly TextBlock placeholder = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    readonly IconButton undo = Tool(Lucide.Undo2);
    readonly IconButton clear = Tool(Lucide.Eraser);
    readonly StackPanel toolbar = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    BitmapSource? baseImage;
    byte[]? emitted;

    static SignaturePad()
    {
        FocusableProperty.OverrideMetadata(typeof(SignaturePad), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(SignaturePad), new FrameworkPropertyMetadata(false));
        IsEnabledProperty.OverrideMetadata(typeof(SignaturePad), new FrameworkPropertyMetadata(true, (d, _) => ((SignaturePad)d).Sync()));
    }

    /// <summary>新建：画布在外框里，提示在中间，撤销、清空在右上角。</summary>
    public SignaturePad()
    {
        canvas = new Canvas(this);
        undo.Click += (_, _) => Undo();
        clear.Click += (_, _) => Clear();
        toolbar.Children.Add(undo);
        toolbar.Children.Add(clear);
        var layers = new System.Windows.Controls.Grid();
        layers.Children.Add(canvas);
        layers.Children.Add(placeholder);
        layers.Children.Add(toolbar);
        shell.Child = layers;
        Root = shell;
        MouseEnter += (_, _) => Sync();
        MouseLeave += (_, _) => Sync();
        Loaded += (_, _) => Sync();
        Sync();
    }

    /// <summary>签名（透明底的 PNG 字节）。</summary>
    public byte[]? Value { get => (byte[]?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>墨色。</summary>
    public Color? PenColor { get => (Color?)GetValue(PenColorProperty); set => SetValue(PenColorProperty, value); }

    /// <summary>最细的笔宽。</summary>
    public double PenMinWidth { get => (double)GetValue(PenMinWidthProperty); set => SetValue(PenMinWidthProperty, value); }

    /// <summary>最粗的笔宽。</summary>
    public double PenMaxWidth { get => (double)GetValue(PenMaxWidthProperty); set => SetValue(PenMaxWidthProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => (bool?)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>出错。</summary>
    public bool? IsInvalid { get => (bool?)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>圆角档位。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>画布（测试用）。</summary>
    internal FrameworkElement CanvasPart => canvas;

    /// <summary>撤销、清空按钮（测试用）。</summary>
    internal (IconButton Undo, IconButton Clear) Tools => (undo, clear);

    /// <summary>撤销、清空所在的一排（测试用）。</summary>
    internal FrameworkElement ToolbarPart => toolbar;

    /// <summary>提示文字（测试用）。</summary>
    internal TextBlock PlaceholderPart => placeholder;

    bool ReadOnly => IsReadOnly ?? Field.Of(this)?.IsReadOnly ?? false;

    bool Invalid => IsInvalid ?? Field.Of(this)?.Invalid ?? false;

    /// <summary>能写：可用、不只读。</summary>
    bool Writable => IsEnabled && !ReadOnly;

    /// <summary>签了：有笔画或读回的旧签名。</summary>
    bool Signed => strokes.Strokes.Count > 0 || baseImage is not null;

    Brush Ink => PenColor is { } color ? new SolidColorBrush(color) : Paper.Ink(this);

    /// <summary>透明底的 PNG（按屏幕的缩放出图）；没签时 null。</summary>
    public byte[]? ToPng()
    {
        if (!Signed || canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0) return null;
        var dpi = VisualTreeHelper.GetDpi(this);
        var size = new Size(canvas.ActualWidth, canvas.ActualHeight);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen()) DrawInk(context, size);
        var bitmap = new RenderTargetBitmap((int)Math.Round(size.Width * dpi.DpiScaleX), (int)Math.Round(size.Height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>SVG 文字（笔画是路径，读回的旧签名是嵌入的图片）；没签时 null。</summary>
    public string? ToSvg()
    {
        var color = Ink is SolidColorBrush solid ? $"#{solid.Color.R:x2}{solid.Color.G:x2}{solid.Color.B:x2}" : "#1c2024";
        return strokes.ToSvg(canvas.ActualWidth, canvas.ActualHeight, color, baseImage is null ? null : Value);
    }

    /// <summary>撤销上一笔（没有笔画时去掉读回的旧签名）。</summary>
    public void Undo()
    {
        if (!strokes.Undo()) baseImage = null;
        Commit();
    }

    /// <summary>清空。</summary>
    public void Clear()
    {
        strokes.Clear();
        baseImage = null;
        Commit();
    }

    void IFieldControl.FieldChanged() => Sync();

    /// <summary>写完一笔、撤销、清空：交出新的值（自己交出的值回来时不当成“换了签名”）。</summary>
    void Commit()
    {
        emitted = ToPng();
        SetCurrentValue(ValueProperty, emitted);
        Sync();
    }

    /// <summary>父级换了签名（读回旧签名、在别处清空）：清掉笔画，旧签名画在底下。</summary>
    void OnValueChanged()
    {
        if (ReferenceEquals(Value, emitted)) return;
        emitted = Value;
        strokes.Clear();
        baseImage = null;
        if (Value is { Length: > 0 } png)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = new MemoryStream(png);
                image.EndInit();
                image.Freeze();
                baseImage = image;
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceWarning($"SignaturePad 读不了签名图片：{error.Message}");
            }
        }
        Sync();
    }

    /// <summary>外框（描边随状态）、提示、按钮、读屏的说明跟着状态；画布重画。</summary>
    void Sync()
    {
        if (canvas is null) return;
        var lang = Provider.GetLang(this);
        var em = TextElement.GetFontSize(this);
        var writable = Writable;
        var signed = Signed;
        shell.Background = Paper.Background(this);
        shell.CornerRadius = new CornerRadius((TryFindResource((Radius.Value is null ? Radius.Md : Radius).ResourceKey!) as double? ?? 0) * em);
        shell.SetResourceReference(Border.BorderBrushProperty, Invalid ? ThemeKeys.Brush.Danger : ReadOnly ? ThemeKeys.Brush.BorderMuted : IsMouseOver && writable ? ThemeKeys.Brush.BorderStrong : ThemeKeys.Brush.Border);
        Opacity = IsEnabled ? 1 : TryFindResource(ThemeKeys.Opacity.Disabled) as double? ?? 0.5;
        canvas.Cursor = writable ? Cursors.Cross : null;
        placeholder.Visibility = signed ? Visibility.Collapsed : Visibility.Visible;
        placeholder.Text = writable ? lang.SignaturePlaceholder : lang.SignatureEmpty;
        placeholder.SetResourceReference(TextBlock.ForegroundProperty, ThemeKeys.Brush.TextPlaceholder);
        toolbar.Visibility = writable && signed ? Visibility.Visible : Visibility.Collapsed;
        var xs = (TryFindResource(ThemeKeys.Space.Xs) as double? ?? 0.25) * em;
        toolbar.Margin = new Thickness(0, xs, xs, 0);
        AutomationProperties.SetName(undo, lang.SignatureUndo);
        AutomationProperties.SetName(clear, lang.SignatureClear);
        AutomationProperties.SetHelpText(canvas, signed ? lang.SignatureSigned : lang.SignatureEmpty);
        canvas.InvalidateVisual();
    }

    /// <summary>默认高 10em（写了 Height 用写的）。</summary>
    protected override Size MeasureOverride(Size constraint)
    {
        canvas.Height = double.IsNaN(Height) ? 10 * TextElement.GetFontSize(this) : double.NaN;
        return base.MeasureOverride(constraint);
    }

    /// <summary>墨迹：读回的旧签名在最底下，笔画在上面（一个点的画成圆点）。</summary>
    void DrawInk(DrawingContext context, Size size)
    {
        if (baseImage is not null) context.DrawImage(baseImage, new Rect(size));
        var ink = Ink;
        foreach (var stroke in strokes.Strokes) DrawStroke(context, stroke, ink);
        if (strokes.CurrentPoints is { Count: > 0 } points) DrawStroke(context, points, ink);
    }

    static void DrawStroke(DrawingContext context, IReadOnlyList<StrokePoint> stroke, Brush ink)
    {
        if (stroke.Count == 1)
        {
            context.DrawEllipse(ink, null, new Point(stroke[0].X, stroke[0].Y), stroke[0].Width / 2, stroke[0].Width / 2);
            return;
        }
        foreach (var (from, control, to, width) in SignatureStrokes.SegmentsOf(stroke))
        {
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                path.BeginFigure(new Point(from.X, from.Y), false, false);
                path.QuadraticBezierTo(new Point(control.X, control.Y), new Point(to.X, to.Y), true, true);
            }
            geometry.Freeze();
            context.DrawGeometry(null, new Pen(ink, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, geometry);
        }
    }

    /// <summary>写一笔（测试用：WPF 的鼠标位置模拟不了，直接喂画布里的坐标，每个点隔 16 毫秒）。</summary>
    internal void Write(IReadOnlyList<Point> points)
    {
        if (!Writable || points.Count == 0) return;
        Begin(points[0], 0);
        for (var i = 1; i < points.Count; i++) Extend(points[i], i * 16);
        Finish();
    }

    /// <summary>落笔：画布每次整笔重画（笔画不多），不用只画新多出来的一段。</summary>
    void Begin(Point at, double milliseconds)
    {
        strokes.Begin(at.X, at.Y, milliseconds);
        canvas.InvalidateVisual();
    }

    void Extend(Point at, double milliseconds)
    {
        if (!strokes.IsDrawing) return;
        strokes.Extend(at.X, at.Y, milliseconds);
        canvas.InvalidateVisual();
    }

    void Finish()
    {
        if (strokes.End()) Commit();
    }

    static IconButton Tool(IconData icon) => new() { Icon = icon, Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };

    /// <summary>画布：画墨迹；按下开始一笔，移动接上，抬笔（或丢了指针）收下。</summary>
    sealed class Canvas(SignaturePad owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext context)
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            owner.DrawInk(context, RenderSize);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (!owner.Writable) return;
            CaptureMouse();
            owner.Begin(e.GetPosition(this), e.Timestamp);
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed) owner.Extend(e.GetPosition(this), e.Timestamp);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            ReleaseMouseCapture();
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            owner.Finish();
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new CanvasPeer(this, owner);
    }

    /// <summary>画布对读屏是一张图片（TS 的 role="img"），名称是自己的名称、字段标签或“签名”。</summary>
    sealed class CanvasPeer(FrameworkElement canvas, SignaturePad owner) : FrameworkElementAutomationPeer(canvas)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetClassNameCore() => "SignatureCanvas";

        protected override string GetNameCore() =>
            AutomationProperties.GetName(owner) is { Length: > 0 } name ? name
            : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText ?? ""
            : Provider.GetLang(owner).Signature;
    }
}
