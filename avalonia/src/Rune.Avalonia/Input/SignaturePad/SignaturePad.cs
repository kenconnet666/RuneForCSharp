using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Rune.Avalonia;

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
/// 部件：<see cref="SignaturePadParts"/>。
/// </summary>
public class SignaturePad : Control, IFieldControl
{
    /// <summary>签名（透明底的 PNG 字节）；没签时 null。默认双向绑定。</summary>
    public static readonly StyledProperty<byte[]?> ValueProperty = AvaloniaProperty.Register<SignaturePad, byte[]?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>墨色；不写时是纸面的墨色。</summary>
    public static readonly StyledProperty<Color?> PenColorProperty = AvaloniaProperty.Register<SignaturePad, Color?>(nameof(PenColor));

    /// <summary>最细的笔宽，默认 0.75。</summary>
    public static readonly StyledProperty<double> PenMinWidthProperty = AvaloniaProperty.Register<SignaturePad, double>(nameof(PenMinWidth), 0.75);

    /// <summary>最粗的笔宽，默认 2.75。</summary>
    public static readonly StyledProperty<double> PenMaxWidthProperty = AvaloniaProperty.Register<SignaturePad, double>(nameof(PenMaxWidth), 2.75);

    /// <summary>只读：显示已签的图，不能写；不写时取所在 Field 的。</summary>
    public static readonly StyledProperty<bool?> IsReadOnlyProperty = AvaloniaProperty.Register<SignaturePad, bool?>(nameof(IsReadOnly));

    /// <summary>出错；不写时取所在 Field 的。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = AvaloniaProperty.Register<SignaturePad, bool?>(nameof(IsInvalid));

    /// <summary>圆角档位，默认 md。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<SignaturePad, Radius>(nameof(Radius), Radius.Md);

    /// <summary>字号：默认高度、圆角、按钮的位置按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = global::Avalonia.Controls.Documents.TextElement.FontSizeProperty.AddOwner<SignaturePad>();

    readonly SignatureStrokes strokes = new();
    readonly Canvas canvas;
    readonly Border shell = new() { Name = SignaturePadParts.Root, BorderThickness = new Thickness(1), ClipToBounds = true };
    readonly TextBlock placeholder = new() { Name = SignaturePadParts.Placeholder, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    readonly IconButton undo = Tool(Lucide.Undo2);
    readonly IconButton clear = Tool(Lucide.Eraser);
    readonly StackPanel toolbar = new() { Name = SignaturePadParts.Toolbar, Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    Bitmap? baseImage;
    byte[]? emitted;

    /// <summary>新建：画布在外框里，提示在中间，撤销、清空在右上角。</summary>
    public SignaturePad()
    {
        canvas = new Canvas(this) { Name = SignaturePadParts.Canvas };
        undo.Click += (_, _) => Undo();
        clear.Click += (_, _) => Clear();
        toolbar.Children.AddRange([undo, clear]);
        ActualThemeVariantChanged += (_, _) => Sync();
        shell.Child = new Panel { Children = { canvas, placeholder, toolbar } };
        LogicalChildren.Add(shell);
        VisualChildren.Add(shell);
        Sync();
    }

    /// <summary>签名（透明底的 PNG 字节）。</summary>
    public byte[]? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>墨色。</summary>
    public Color? PenColor { get => GetValue(PenColorProperty); set => SetValue(PenColorProperty, value); }

    /// <summary>最细的笔宽。</summary>
    public double PenMinWidth { get => GetValue(PenMinWidthProperty); set => SetValue(PenMinWidthProperty, value); }

    /// <summary>最粗的笔宽。</summary>
    public double PenMaxWidth { get => GetValue(PenMaxWidthProperty); set => SetValue(PenMaxWidthProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>出错。</summary>
    public bool? IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>圆角档位。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>画布（测试用）。</summary>
    internal Control CanvasPart => canvas;

    /// <summary>撤销、清空按钮（测试用）。</summary>
    internal (IconButton Undo, IconButton Clear) Tools => (undo, clear);

    /// <summary>提示文字（测试用）。</summary>
    internal TextBlock PlaceholderPart => placeholder;

    bool ReadOnly => IsReadOnly ?? Field.ReadOnlyOf(this);

    bool Invalid => IsInvalid ?? Field.Of(this)?.Invalid ?? false;

    /// <summary>能写：可用、不只读。</summary>
    bool Writable => IsEffectivelyEnabled && !ReadOnly;

    /// <summary>签了：有笔画或读回的旧签名。</summary>
    bool Signed => strokes.Strokes.Count > 0 || baseImage is not null;

    IBrush Ink => PenColor is { } color ? new SolidColorBrush(color) : Paper.Ink(this);

    /// <summary>透明底的 PNG（按屏幕的缩放出图）；没签时 null。</summary>
    public byte[]? ToPng()
    {
        if (!Signed || canvas.Bounds.Width <= 0 || canvas.Bounds.Height <= 0) return null;
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var size = canvas.Bounds.Size;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Round(size.Width * scale), (int)Math.Round(size.Height * scale)), new Vector(96 * scale, 96 * scale));
        using (var context = bitmap.CreateDrawingContext()) DrawInk(context, size);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    /// <summary>SVG 文字（笔画是路径，读回的旧签名是嵌入的图片）；没签时 null。</summary>
    public string? ToSvg()
    {
        var color = Ink is ISolidColorBrush solid ? $"#{solid.Color.R:x2}{solid.Color.G:x2}{solid.Color.B:x2}" : "#1c2024";
        return strokes.ToSvg(canvas.Bounds.Width, canvas.Bounds.Height, color, baseImage is null ? null : Value);
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

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) OnValueChanged();
        else if (change.Property == PenMinWidthProperty || change.Property == PenMaxWidthProperty)
        {
            strokes.MinWidth = PenMinWidth;
            strokes.MaxWidth = PenMaxWidth;
        }
        else if (change.Property == IsReadOnlyProperty || change.Property == IsInvalidProperty || change.Property == IsEffectivelyEnabledProperty
                 || change.Property == IsPointerOverProperty || change.Property == RadiusProperty || change.Property == FontSizeProperty
                 || change.Property == PenColorProperty || change.Property == Field.OwnerProperty) Sync();
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
                baseImage = new Bitmap(new MemoryStream(png));
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
        var lang = Provider.GetLang(this);
        var em = FontSize;
        var writable = Writable;
        var signed = Signed;
        shell.Background = Paper.Background(this);
        shell.CornerRadius = new CornerRadius(this.Resource<double>((Radius.Value is null ? Radius.Md : Radius).ResourceKey!) * em);
        shell.BorderBrush = this.Resource<IBrush>(Invalid ? ThemeKeys.Brush.Danger : ReadOnly ? ThemeKeys.Brush.BorderMuted : IsPointerOver && writable ? ThemeKeys.Brush.BorderStrong : ThemeKeys.Brush.Border);
        Opacity = IsEffectivelyEnabled ? 1 : this.Resource<double>(ThemeKeys.Opacity.Disabled);
        Cursor = writable ? new Cursor(StandardCursorType.Cross) : null;
        placeholder.IsVisible = !signed;
        placeholder.Text = writable ? lang.SignaturePlaceholder : lang.SignatureEmpty;
        placeholder.Foreground = this.Resource<IBrush>(ThemeKeys.Brush.TextPlaceholder);
        toolbar.IsVisible = writable && signed;
        toolbar.Margin = new Thickness(0, this.Resource<double>(ThemeKeys.Space.Xs) * em, this.Resource<double>(ThemeKeys.Space.Xs) * em, 0);
        AutomationProperties.SetName(undo, lang.SignatureUndo);
        AutomationProperties.SetName(clear, lang.SignatureClear);
        AutomationProperties.SetHelpText(canvas, signed ? lang.SignatureSigned : lang.SignatureEmpty);
        canvas.InvalidateVisual();
    }

    /// <summary>默认高 10em（写了 Height 用写的）。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        canvas.Height = double.IsNaN(Height) ? 10 * FontSize : double.NaN;
        shell.Measure(availableSize);
        return shell.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        shell.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>墨迹：读回的旧签名在最底下，笔画在上面（一个点的画成圆点）。</summary>
    void DrawInk(DrawingContext context, Size size)
    {
        if (baseImage is not null) context.DrawImage(baseImage, new Rect(baseImage.Size), new Rect(size));
        var ink = Ink;
        foreach (var stroke in strokes.Strokes) DrawStroke(context, stroke, ink);
        if (strokes.CurrentPoints is { Count: > 0 } points) DrawStroke(context, points, ink);
    }

    static void DrawStroke(DrawingContext context, IReadOnlyList<StrokePoint> stroke, IBrush ink)
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
                path.BeginFigure(new Point(from.X, from.Y), false);
                path.QuadraticBezierTo(new Point(control.X, control.Y), new Point(to.X, to.Y));
                path.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(ink, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
        }
    }

    /// <summary>落笔：画布每次整笔重画（笔画不多），不用只画新多出来的一段。</summary>
    void Begin(Point at, ulong timestamp)
    {
        strokes.Begin(at.X, at.Y, timestamp);
        canvas.InvalidateVisual();
    }

    void Extend(Point at, ulong timestamp)
    {
        if (!strokes.IsDrawing) return;
        strokes.Extend(at.X, at.Y, timestamp);
        canvas.InvalidateVisual();
    }

    void Finish()
    {
        if (strokes.End()) Commit();
    }

    static IconButton Tool(IconData icon) => new() { Icon = icon, Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };

    /// <summary>画布：画墨迹；按下开始一笔，移动接上，抬笔（或丢了指针）收下。</summary>
    sealed class Canvas(SignaturePad owner) : Control
    {
        public override void Render(DrawingContext context)
        {
            context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
            owner.DrawInk(context, Bounds.Size);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!owner.Writable || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            e.Pointer.Capture(this);
            owner.Begin(e.GetPosition(this), e.Timestamp);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            // 不取合并掉的中间点：它们没有各自的时刻，速度（笔宽）会算错
            owner.Extend(e.GetPosition(this), e.Timestamp);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            e.Pointer.Capture(null);
            owner.Finish();
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            owner.Finish();
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new CanvasPeer(this, owner);
    }

    /// <summary>画布对读屏是一张图片（TS 的 role="img"），名称是自己的名称、字段标签或“签名”。</summary>
    sealed class CanvasPeer(Control canvas, SignaturePad owner) : ControlAutomationPeer(canvas)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string? GetNameCore() =>
            AutomationProperties.GetName(owner) is { Length: > 0 } name ? name
            : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText
            : Provider.GetLang(owner).Signature;
    }
}

/// <summary>SignaturePad 的部件名。</summary>
public static class SignaturePadParts
{
    /// <summary>外框（TS 的 root）。</summary>
    public const string Root = "PART_Root";

    /// <summary>画布（TS 的 propsCanvas）。</summary>
    public const string Canvas = "PART_Canvas";

    /// <summary>没签时的提示（TS 的 placeholder）。</summary>
    public const string Placeholder = "PART_Placeholder";

    /// <summary>撤销、清空（TS 的 toolbar）。</summary>
    public const string Toolbar = "PART_Toolbar";
}
