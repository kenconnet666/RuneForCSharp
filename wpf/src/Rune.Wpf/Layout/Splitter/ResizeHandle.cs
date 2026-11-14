using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 宽度调整手柄（vue-tsx-pro 的 ResizeHandle）：放在要调整的那个元素（一个 Panel，如 Grid，手柄叠在内容上）里，贴着它的结束边
/// （<see cref="Edge"/>=Start 时是起始边，如从结束边滑入的抽屉）的一条 space.sm 宽的细条，悬停、拖动时 borderStrong，键盘聚焦时强调色。
/// <code>
/// &lt;Grid Width="220"&gt;
///   &lt;rune:NavList … /&gt;
///   &lt;rune:ResizeHandle AutomationProperties.Name="调整列表宽度" MinLength="160" MaxLength="360" DefaultLength="220" /&gt;
/// &lt;/Grid&gt;
/// </code>
/// 拖动或聚焦后用 ← / →（拖动的键盘替代）调整，每次 <see cref="Step"/>，从右到左排版下方向自动镜像；写了 <see cref="DefaultLength"/> 时双击回到它。
/// 改的是父元素的 Width（<see cref="Orientation"/>=Horizontal 时是一条横线，贴着下边或上边，改 Height，用 ↑ / ↓），
/// 值已经夹在 <see cref="MinLength"/>–<see cref="MaxLength"/> 之间、取整像素；要记住时听 <see cref="LengthChanged"/>，或双向绑定父元素的 Width。
/// 读屏是分隔符，名称写在 <c>AutomationProperties.Name</c> 上，取值是父元素当前的宽度。
/// </summary>
public class ResizeHandle : FrameworkElement
{
    /// <summary>最小长度（像素），默认 64。</summary>
    public static readonly DependencyProperty MinLengthProperty = DependencyProperty.Register(nameof(MinLength), typeof(double), typeof(ResizeHandle), new FrameworkPropertyMetadata(64d));

    /// <summary>最大长度（像素），默认不限。</summary>
    public static readonly DependencyProperty MaxLengthProperty = DependencyProperty.Register(nameof(MaxLength), typeof(double), typeof(ResizeHandle), new FrameworkPropertyMetadata(double.PositiveInfinity));

    /// <summary>双击回到的长度（像素）；不写时双击不做什么。</summary>
    public static readonly DependencyProperty DefaultLengthProperty = DependencyProperty.Register(nameof(DefaultLength), typeof(double?), typeof(ResizeHandle), new FrameworkPropertyMetadata(null));

    /// <summary>键盘每按一次调整多少（像素），默认 16。</summary>
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(ResizeHandle), new FrameworkPropertyMetadata(16d));

    /// <summary>手柄在父元素的哪一边，默认结束边；横线时起始是上边、结束是下边。</summary>
    public static readonly DependencyProperty EdgeProperty = DependencyProperty.Register(nameof(Edge), typeof(PaneSide), typeof(ResizeHandle),
        new FrameworkPropertyMetadata(PaneSide.End, (d, _) => ((ResizeHandle)d).Place()));

    /// <summary>Vertical（默认）：竖线，调整宽度；Horizontal：横线，调整高度。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(ResizeHandle),
        new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((ResizeHandle)d).Place()));

    // 底色按状态换资源键（引用资源：换主题时跟着重画）；平时透明
    static readonly DependencyProperty FillProperty = DependencyProperty.Register("Fill", typeof(Brush), typeof(ResizeHandle),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    bool focusVisible;
    bool dragging;
    double from;
    Point origin;

    static ResizeHandle()
    {
        FocusableProperty.OverrideMetadata(typeof(ResizeHandle), new FrameworkPropertyMetadata(true));
        FocusVisualStyleProperty.OverrideMetadata(typeof(ResizeHandle), new FrameworkPropertyMetadata(null));
        TextElement.FontSizeProperty.OverrideMetadata(typeof(ResizeHandle), new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));
    }

    /// <summary>新建：贴着父元素结束边的竖线。</summary>
    public ResizeHandle() => Place();

    /// <summary>最小长度。</summary>
    public double MinLength { get => (double)GetValue(MinLengthProperty); set => SetValue(MinLengthProperty, value); }

    /// <summary>最大长度。</summary>
    public double MaxLength { get => (double)GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }

    /// <summary>双击回到的长度。</summary>
    public double? DefaultLength { get => (double?)GetValue(DefaultLengthProperty); set => SetValue(DefaultLengthProperty, value); }

    /// <summary>键盘的步长。</summary>
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>贴着的边。</summary>
    public PaneSide Edge { get => (PaneSide)GetValue(EdgeProperty); set => SetValue(EdgeProperty, value); }

    /// <summary>方向。</summary>
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>拖动、按键调整了父元素的长度（已经夹在范围里）。</summary>
    public event EventHandler<double>? LengthChanged;

    /// <summary>拖动中。</summary>
    public bool IsDragging => dragging;

    bool Horizontal => Orientation == Orientation.Horizontal;

    FrameworkElement? Target => VisualTreeHelper.GetParent(this) as FrameworkElement;

    /// <summary>
    /// 量拖动的坐标系：父元素的容器。父元素在拖动中变大变小（贴着起始边时它自己的原点也在动），容器不动；
    /// 容器从右到左排版时坐标本身就是镜像的，往结束方向拖总是正的。
    /// </summary>
    internal Visual? Container => Target is { } target ? VisualTreeHelper.GetParent(target) as Visual ?? target : null;

    /// <summary>父元素当前的长度：写了 Width（Height）时取它，否则量实际的大小。</summary>
    public double Length
    {
        get
        {
            if (Target is not { } target) return MinLength;
            var set = Horizontal ? target.Height : target.Width;
            return double.IsNaN(set) ? Horizontal ? target.ActualHeight : target.ActualWidth : set;
        }
    }

    /// <summary>贴边：竖线在结束（起始）边、上下铺满；横线在下（上）边、左右铺满。光标跟着方向。</summary>
    void Place()
    {
        var start = Edge == PaneSide.Start;
        SetCurrentValue(HorizontalAlignmentProperty, Horizontal ? HorizontalAlignment.Stretch : start ? HorizontalAlignment.Left : HorizontalAlignment.Right);
        SetCurrentValue(VerticalAlignmentProperty, !Horizontal ? VerticalAlignment.Stretch : start ? VerticalAlignment.Top : VerticalAlignment.Bottom);
        Cursor = Horizontal ? Cursors.SizeNS : Cursors.SizeWE;
    }

    /// <summary>改父元素的长度（夹在范围里、取整），通知使用方。</summary>
    internal void Resize(double length)
    {
        if (Target is not { } target) return;
        var next = SplitterScale.FitLength(length, MinLength, MaxLength);
        target.SetCurrentValue(Horizontal ? HeightProperty : WidthProperty, next);
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

    void UpdateFill()
    {
        if (focusVisible) SetResourceReference(FillProperty, ThemeKeys.Brush.Accent);
        else if (IsMouseOver || dragging) SetResourceReference(FillProperty, ThemeKeys.Brush.BorderStrong);
        else ClearValue(FillProperty);
    }

    /// <inheritdoc />
    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        var thickness = this.Resource<double>(ThemeKeys.Space.Sm) * TextElement.GetFontSize(this);
        return Horizontal ? new System.Windows.Size(0, thickness) : new System.Windows.Size(thickness, 0);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext context) =>
        // 透明的部分也能按
        context.DrawRectangle((Brush?)GetValue(FillProperty) ?? Brushes.Transparent, null, new Rect(RenderSize));

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateFill();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateFill();
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Container is not { } container) return;
        e.Handled = true;
        if (e.ClickCount >= 2)
        {
            Restore();
            return;
        }
        PointerDown(e.GetPosition((IInputElement)container));
        CaptureMouse();
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging && Container is IInputElement container) PointerMove(e.GetPosition(container));
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!dragging) return;
        ReleaseMouseCapture();
        PointerUp();
    }

    /// <inheritdoc />
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        PointerUp();
    }

    /// <summary>双击：回到 DefaultLength。</summary>
    internal void Restore()
    {
        if (DefaultLength is { } length) Resize(length);
    }

    /// <summary>按下（容器的坐标，见 <see cref="Container"/>）。</summary>
    internal void PointerDown(Point point)
    {
        origin = point;
        from = Length;
        dragging = true;
        UpdateFill();
    }

    internal void PointerMove(Point point)
    {
        if (!dragging) return;
        var delta = Horizontal ? point.Y - origin.Y : point.X - origin.X;
        Resize(from + SplitterScale.LengthDelta(delta, Edge, rightToLeft: false));
    }

    internal void PointerUp()
    {
        if (!dragging) return;
        dragging = false;
        UpdateFill();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && Press(e.Key)) e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        focusVisible = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
        UpdateFill();
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        focusVisible = false;
        UpdateFill();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ResizeHandlePeer(this);

    /// <summary>读屏：分隔符，取值是父元素的长度，范围是最小、最大长度；设值即改长度。</summary>
    sealed class ResizeHandlePeer(ResizeHandle handle) : FrameworkElementAutomationPeer(handle), IRangeValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;

        protected override string GetClassNameCore() => nameof(ResizeHandle);

        protected override bool IsKeyboardFocusableCore() => true;

        public override object GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);

        public bool IsReadOnly => !handle.IsEnabled;

        public double Value => handle.Length;

        public double Minimum => handle.MinLength;

        public double Maximum => handle.MaxLength;

        public double SmallChange => handle.Step;

        public double LargeChange => handle.Step;

        public void SetValue(double value) => handle.Resize(value);
    }
}
