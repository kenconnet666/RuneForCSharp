using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 宽度调整手柄（vue-tsx-pro 的 ResizeHandle）：放在要调整的那个元素（一个 Panel，手柄叠在内容上）里，贴着它的结束边
/// （<see cref="Edge"/>=Start 时是起始边，如从结束边滑入的抽屉）的一条 space.sm 宽的细条，悬停、拖动时 borderStrong，键盘聚焦时强调色。
/// <code>
/// &lt;Panel Width="220"&gt;
///   &lt;rune:NavList … /&gt;
///   &lt;rune:ResizeHandle AutomationProperties.Name="调整列表宽度" MinLength="160" MaxLength="360" DefaultLength="220" /&gt;
/// &lt;/Panel&gt;
/// </code>
/// 拖动或聚焦后用 ← / →（拖动的键盘替代）调整，每次 <see cref="Step"/>，从右到左排版下方向自动镜像；写了 <see cref="DefaultLength"/> 时双击回到它。
/// 改的是父元素的 Width（<see cref="Orientation"/>=Horizontal 时是一条横线，贴着下边或上边，改 Height，用 ↑ / ↓），
/// 值已经夹在 <see cref="MinLength"/>–<see cref="MaxLength"/> 之间、取整像素；要记住时听 <see cref="LengthChanged"/>，或双向绑定父元素的 Width。
/// 读屏是分隔符，名称写在 <c>AutomationProperties.Name</c> 上，取值是父元素当前的宽度。
/// </summary>
public class ResizeHandle : Control, global::Avalonia.Rendering.ICustomHitTest
{
    /// <summary>最小长度（像素），默认 64。</summary>
    public static readonly StyledProperty<double> MinLengthProperty = AvaloniaProperty.Register<ResizeHandle, double>(nameof(MinLength), 64);

    /// <summary>最大长度（像素），默认不限。</summary>
    public static readonly StyledProperty<double> MaxLengthProperty = AvaloniaProperty.Register<ResizeHandle, double>(nameof(MaxLength), double.PositiveInfinity);

    /// <summary>双击回到的长度（像素）；不写时双击不做什么。</summary>
    public static readonly StyledProperty<double?> DefaultLengthProperty = AvaloniaProperty.Register<ResizeHandle, double?>(nameof(DefaultLength));

    /// <summary>键盘每按一次调整多少（像素），默认 16。</summary>
    public static readonly StyledProperty<double> StepProperty = AvaloniaProperty.Register<ResizeHandle, double>(nameof(Step), 16);

    /// <summary>手柄在父元素的哪一边，默认结束边；横线时起始是上边、结束是下边。</summary>
    public static readonly StyledProperty<PaneSide> EdgeProperty = AvaloniaProperty.Register<ResizeHandle, PaneSide>(nameof(Edge), PaneSide.End);

    /// <summary>Vertical（默认）：竖线，调整宽度；Horizontal：横线，调整高度。</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<ResizeHandle, Orientation>(nameof(Orientation), Orientation.Vertical);

    /// <summary>字号（继承）：手柄的粗细按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ResizeHandle>();

    bool focusVisible;
    bool dragging;
    double from;
    Point origin;

    static ResizeHandle()
    {
        FocusableProperty.OverrideDefaultValue<ResizeHandle>(true);
        AffectsMeasure<ResizeHandle>(OrientationProperty, FontSizeProperty);
    }

    /// <summary>新建：贴着父元素结束边的竖线。</summary>
    public ResizeHandle()
    {
        this.RedrawOnResourceChanges(this);
        Place();
    }

    /// <summary>最小长度。</summary>
    public double MinLength { get => GetValue(MinLengthProperty); set => SetValue(MinLengthProperty, value); }

    /// <summary>最大长度。</summary>
    public double MaxLength { get => GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }

    /// <summary>双击回到的长度。</summary>
    public double? DefaultLength { get => GetValue(DefaultLengthProperty); set => SetValue(DefaultLengthProperty, value); }

    /// <summary>键盘的步长。</summary>
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>贴着的边。</summary>
    public PaneSide Edge { get => GetValue(EdgeProperty); set => SetValue(EdgeProperty, value); }

    /// <summary>方向。</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>拖动、按键调整了父元素的长度（已经夹在范围里）。</summary>
    public event EventHandler<double>? LengthChanged;

    /// <summary>拖动中。</summary>
    public bool IsDragging => dragging;

    bool Horizontal => Orientation == Orientation.Horizontal;

    Layoutable? Target => Parent as Layoutable;

    /// <summary>调整的不是父元素时（Drawer 的面板宽度）：读当前长度、写新长度；拖动按手柄自己的父元素（弹出层的根，不动）量。</summary>
    internal (Func<double> Read, Action<double> Write)? Override { get; set; }

    /// <summary>
    /// 量拖动的坐标系：父元素的容器。父元素在拖动中变大变小（贴着起始边时它自己的原点也在动），容器不动；
    /// 容器从右到左排版时坐标本身就是镜像的，往结束方向拖总是正的。
    /// </summary>
    Visual? Container => Override is null ? Target?.GetVisualParent() ?? Target : Parent as Visual;

    /// <summary>父元素当前的长度：写了 Width（Height）时取它，否则量实际的大小。</summary>
    public double Length
    {
        get
        {
            if (Override is { } custom) return custom.Read();
            if (Target is not { } target) return MinLength;
            var set = Horizontal ? target.Height : target.Width;
            return double.IsNaN(set) ? Horizontal ? target.Bounds.Height : target.Bounds.Width : set;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty || change.Property == EdgeProperty) Place();
    }

    /// <summary>贴边：竖线在结束（起始）边、上下铺满；横线在下（上）边、左右铺满。光标跟着方向。</summary>
    void Place()
    {
        var start = Edge == PaneSide.Start;
        SetCurrentValue(HorizontalAlignmentProperty, Horizontal ? HorizontalAlignment.Stretch : start ? HorizontalAlignment.Left : HorizontalAlignment.Right);
        SetCurrentValue(VerticalAlignmentProperty, !Horizontal ? VerticalAlignment.Stretch : start ? VerticalAlignment.Top : VerticalAlignment.Bottom);
        Cursor = new Cursor(Horizontal ? StandardCursorType.SizeNorthSouth : StandardCursorType.SizeWestEast);
    }

    /// <summary>改父元素的长度（夹在范围里、取整），通知使用方。</summary>
    internal void Resize(double length)
    {
        var next = SplitterScale.FitLength(length, MinLength, MaxLength);
        if (Override is { } custom) custom.Write(next);
        else if (Target is { } target) target.SetCurrentValue(Horizontal ? HeightProperty : WidthProperty, next);
        else return;
        LengthChanged?.Invoke(this, next);
    }

    /// <summary>键盘：竖线 ← / →（从右到左排版时镜像）、横线 ↑ / ↓，每次一个 Step。</summary>
    internal bool Press(Key key)
    {
        double delta = (key, Horizontal) switch
        {
            (Key.Right, false) or (Key.Down, true) => Step,
            (Key.Left, false) or (Key.Up, true) => -Step,
            _ => 0,
        };
        if (delta == 0) return false;
        Resize(Length + SplitterScale.LengthDelta(delta, Edge, !Horizontal && FlowDirection == FlowDirection.RightToLeft));
        return true;
    }

    /// <summary>整块都能按（透明的部分也算）。</summary>
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var thickness = this.Resource<double>(ThemeKeys.Space.Sm) * FontSize;
        return Horizontal ? new LayoutSize(0, thickness) : new LayoutSize(thickness, 0);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var key = focusVisible ? ThemeKeys.Brush.Accent : IsPointerOver || dragging ? ThemeKeys.Brush.BorderStrong : null;
        if (key is not null) context.FillRectangle(this.Resource<IBrush>(key), new Rect(Bounds.Size));
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Container is not { } container) return;
        e.Handled = true;
        if (e.ClickCount >= 2)
        {
            if (DefaultLength is { } length) Resize(length);
            return;
        }
        origin = e.GetPosition(container);
        from = Length;
        dragging = true;
        e.Pointer.Capture(this);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!dragging || Container is not { } container) return;
        var point = e.GetPosition(container);
        var delta = Horizontal ? point.Y - origin.Y : point.X - origin.X;
        Resize(from + SplitterScale.LengthDelta(delta, Edge, rightToLeft: false));
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!dragging) return;
        e.Pointer.Capture(null);
        dragging = false;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        dragging = false;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && Press(e.Key)) e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        focusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        focusVisible = false;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ResizeHandlePeer(this);

    /// <summary>读屏：分隔符，取值是父元素的长度，范围是最小、最大长度；设值即改长度。</summary>
    sealed class ResizeHandlePeer(ResizeHandle handle) : ControlAutomationPeer(handle), IRangeValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;

        protected override string GetClassNameCore() => nameof(ResizeHandle);

        public bool IsReadOnly => !handle.IsEffectivelyEnabled;

        public double Value => handle.Length;

        public double Minimum => handle.MinLength;

        public double Maximum => handle.MaxLength;

        public double SmallChange => handle.Step;

        public double LargeChange => handle.Step;

        public void SetValue(double value) => handle.Resize(value);
    }
}
