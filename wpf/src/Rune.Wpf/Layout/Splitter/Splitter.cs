using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 分栏（vue-tsx-pro 的 Splitter）：两栏并排（或上下），中间的分隔条拖动调整比例，一侧可以收起。收件箱（左列表、右预览）、
/// 主从视图、编辑器与预览这类"两块都要看、比例因人而异"的布局。
/// <code>
/// &lt;rune:Splitter Size="{Binding ListSize}" Collapsible="End" IsCollapsed="{Binding PreviewHidden}"
///                Start="{…列表}" End="{…预览}" /&gt;
/// </code>
/// <see cref="Size"/> 是起始一栏占的百分比（双向绑定），拖动时夹在 <see cref="MinSize"/>–<see cref="MaxSize"/> 之间；双击分隔条回到初始比例。
/// 分隔条可聚焦（读屏是分隔符，带当前比例与范围）：←/→（上下排列时 ↑/↓）每次 5%，Home / End 到最小 / 最大，
/// Enter 收起或展开可收起的那一侧；从右到左排版下左右键自动镜像。
/// 写了 <see cref="Collapsible"/> 时分隔条中间有收起、展开的小按钮（不进 Tab 顺序：键盘在分隔条上按 Enter）；收起的一栏不显示，另一栏占满；
/// 拖动或用键盘调比例时自动展开。两栏各自滚动，是浮层滚动条的 <see cref="ScrollArea"/>（两个方向）；外层要给高度。
/// 分隔条占 space.md 宽，看得见的是中间一条 borderMuted 的细线，悬停、拖动时加粗变深，键盘聚焦时加粗变成强调色。
/// 部件样式键见 <see cref="SplitterParts"/>。
/// </summary>
public class Splitter : FrameworkElement
{
    /// <summary>起始一栏（左或上）的内容。</summary>
    public static readonly DependencyProperty StartProperty = DependencyProperty.Register(nameof(Start), typeof(object), typeof(Splitter),
        new FrameworkPropertyMetadata(null, (d, e) => ((Splitter)d).startPane.Content = e.NewValue));

    /// <summary>结束一栏（右或下）的内容。</summary>
    public static readonly DependencyProperty EndProperty = DependencyProperty.Register(nameof(End), typeof(object), typeof(Splitter),
        new FrameworkPropertyMetadata(null, (d, e) => ((Splitter)d).endPane.Content = e.NewValue));

    /// <summary>两栏的排法，默认左右并排。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(SplitterOrientation), typeof(Splitter),
        new FrameworkPropertyMetadata(default(SplitterOrientation), FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutChanged));

    /// <summary>起始一栏占的百分比（默认双向绑定），默认 50。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(double), typeof(Splitter),
        new FrameworkPropertyMetadata(50d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutChanged));

    /// <summary>起始一栏最少占多少（百分比），默认 20。</summary>
    public static readonly DependencyProperty MinSizeProperty = DependencyProperty.Register(nameof(MinSize), typeof(double), typeof(Splitter), new FrameworkPropertyMetadata(20d));

    /// <summary>起始一栏最多占多少（百分比），默认 80。</summary>
    public static readonly DependencyProperty MaxSizeProperty = DependencyProperty.Register(nameof(MaxSize), typeof(double), typeof(Splitter), new FrameworkPropertyMetadata(80d));

    /// <summary>哪一侧可以收起；null（默认）时不能收起。</summary>
    public static readonly DependencyProperty CollapsibleProperty = DependencyProperty.Register(nameof(Collapsible), typeof(PaneSide?), typeof(Splitter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutChanged));

    /// <summary>可收起的那一侧收着（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsCollapsedProperty = DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(Splitter),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutChanged));

    /// <summary>分隔条的读屏名称；不写时取语言包（调整分栏）。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Splitter), new FrameworkPropertyMetadata(null));

    /// <summary>外面一圈边框、圆角 lg 与表面色：独立成块（收件箱的列表与预览）。</summary>
    public static readonly DependencyProperty BorderedProperty = DependencyProperty.Register(nameof(Bordered), typeof(bool), typeof(Splitter),
        new FrameworkPropertyMetadata(false, OnLayoutChanged));

    /// <summary>表面色（Bordered 时主题给 surface）。</summary>
    public static readonly DependencyProperty BackgroundProperty = Border.BackgroundProperty.AddOwner(typeof(Splitter));

    /// <summary>边框色（Bordered 时主题给 border）。</summary>
    public static readonly DependencyProperty BorderBrushProperty = Border.BorderBrushProperty.AddOwner(typeof(Splitter));

    readonly Border surface = new() { ClipToBounds = true };
    readonly SplitterPanel panel;
    readonly ScrollArea startPane = new() { Axis = ScrollAreaAxis.Both };
    readonly ScrollArea endPane = new() { Axis = ScrollAreaAxis.Both };
    readonly IconButton toggle = new() { Size = TextSize.Xs, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Focusable = false };
    double? initialSize;

    static Splitter()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Splitter), new FrameworkPropertyMetadata(typeof(Splitter)));
        TextElement.FontSizeProperty.OverrideMetadata(typeof(Splitter), new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutChanged));
    }

    /// <summary>新建：左右并排、各占一半。</summary>
    public Splitter()
    {
        Handle = new SplitterHandle(this);
        panel = new SplitterPanel(this);
        startPane.SetResourceReference(StyleProperty, SplitterParts.Start);
        endPane.SetResourceReference(StyleProperty, SplitterParts.End);
        Handle.SetResourceReference(StyleProperty, SplitterParts.Handle);
        toggle.SetResourceReference(StyleProperty, SplitterParts.Toggle);
        // 收起按钮最后加：叠在分隔线与两栏上面
        panel.Children.Add(startPane);
        panel.Children.Add(endPane);
        panel.Children.Add(Handle);
        panel.Children.Add(toggle);
        Bind(Border.BackgroundProperty, nameof(Background));
        Bind(Border.BorderBrushProperty, nameof(BorderBrush));
        surface.Child = panel;
        AddVisualChild(surface);
        AddLogicalChild(surface);
        toggle.Click += (_, _) => SetCurrentValue(IsCollapsedProperty, !IsCollapsed);
        Loaded += (_, _) =>
        {
            initialSize ??= Size;
            Update();
        };
        Update();

        void Bind(DependencyProperty property, string path) =>
            System.Windows.Data.BindingOperations.SetBinding(surface, property, new System.Windows.Data.Binding(path) { Source = this });
    }

    /// <summary>起始一栏的内容。</summary>
    public object? Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <summary>结束一栏的内容。</summary>
    public object? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>两栏的排法。</summary>
    public SplitterOrientation Orientation { get => (SplitterOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>起始一栏占的百分比。</summary>
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>起始一栏最少占多少。</summary>
    public double MinSize { get => (double)GetValue(MinSizeProperty); set => SetValue(MinSizeProperty, value); }

    /// <summary>起始一栏最多占多少。</summary>
    public double MaxSize { get => (double)GetValue(MaxSizeProperty); set => SetValue(MaxSizeProperty, value); }

    /// <summary>哪一侧可以收起。</summary>
    public PaneSide? Collapsible { get => (PaneSide?)GetValue(CollapsibleProperty); set => SetValue(CollapsibleProperty, value); }

    /// <summary>可收起的那一侧收着。</summary>
    public bool IsCollapsed { get => (bool)GetValue(IsCollapsedProperty); set => SetValue(IsCollapsedProperty, value); }

    /// <summary>分隔条的读屏名称。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>外面一圈边框。</summary>
    public bool Bordered { get => (bool)GetValue(BorderedProperty); set => SetValue(BorderedProperty, value); }

    /// <summary>表面色。</summary>
    public Brush? Background { get => (Brush?)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>边框色。</summary>
    public Brush? BorderBrush { get => (Brush?)GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>分隔条。</summary>
    public SplitterHandle Handle { get; }

    /// <summary>起始一栏（测试用）。</summary>
    internal ScrollArea StartPane => startPane;

    /// <summary>结束一栏（测试用）。</summary>
    internal ScrollArea EndPane => endPane;

    /// <summary>收起、展开按钮（测试用）。</summary>
    internal IconButton Toggle => toggle;

    internal bool Vertical => Orientation.Value == "vertical";

    /// <summary>收起着的那一栏。</summary>
    internal PaneSide? Hidden => IsCollapsed ? Collapsible : null;

    double Em => TextElement.GetFontSize(this);

    /// <summary>分隔条的粗细：space.md。</summary>
    internal double TrackThickness => this.Resource<double>(ThemeKeys.Space.Md) * Em;

    /// <summary>双击回到的比例：第一次显示时的比例（TS 的初始 size）。</summary>
    internal double InitialSize => initialSize ?? Size;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => surface;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { surface }.GetEnumerator();

    static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Splitter)d).Update();

    /// <summary>收起的一栏不显示；收起按钮的箭头指向收起的方向、名称是"收起""展开"；Bordered 时边框与圆角。</summary>
    void Update()
    {
        startPane.Visibility = Hidden == PaneSide.Start ? Visibility.Collapsed : Visibility.Visible;
        endPane.Visibility = Hidden == PaneSide.End ? Visibility.Collapsed : Visibility.Visible;
        toggle.Visibility = Collapsible is null ? Visibility.Collapsed : Visibility.Visible;
        if (Collapsible is { } side)
        {
            var toStart = SplitterScale.TogglePointsToStart(side, IsCollapsed);
            toggle.Icon = Vertical ? toStart ? Lucide.ChevronUp : Lucide.ChevronDown : toStart ? Lucide.ChevronLeft : Lucide.ChevronRight;
            var lang = Provider.GetLang(this);
            AutomationProperties.SetName(toggle, IsCollapsed ? lang.SplitterExpand : lang.SplitterCollapse);
        }
        surface.BorderThickness = new Thickness(Bordered ? this.Resource<double>(ThemeKeys.BorderWidth.Xs) : 0);
        surface.CornerRadius = new CornerRadius(Bordered ? this.Resource<double>(ThemeKeys.Radius.Lg) * Em : 0);
        Handle.Refresh();
        panel.InvalidateMeasure();
    }

    /// <summary>改比例：夹在范围里；收着的一侧被拖出来、用键盘调比例时展开。</summary>
    internal void Resize(double size)
    {
        SetCurrentValue(SizeProperty, SplitterScale.Fit(size, MinSize, MaxSize));
        if (IsCollapsed) SetCurrentValue(IsCollapsedProperty, false);
    }

    /// <summary>键盘：Enter 收起或展开；方向键（从右到左排版时左右镜像）、Home、End 调比例。</summary>
    internal bool Press(Key key)
    {
        if (key == Key.Enter && Collapsible is not null)
        {
            SetCurrentValue(IsCollapsedProperty, !IsCollapsed);
            return true;
        }
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        SplitterKey? action = (key, Vertical) switch
        {
            (Key.Right, false) => rtl ? SplitterKey.Shrink : SplitterKey.Grow,
            (Key.Left, false) => rtl ? SplitterKey.Grow : SplitterKey.Shrink,
            (Key.Down, true) => SplitterKey.Grow,
            (Key.Up, true) => SplitterKey.Shrink,
            (Key.Home, _) => SplitterKey.Home,
            (Key.End, _) => SplitterKey.End,
            _ => null,
        };
        if (action is not { } pressed) return false;
        Resize(SplitterScale.Target(pressed, Size, MinSize, MaxSize));
        return true;
    }

    /// <inheritdoc />
    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        surface.Measure(availableSize);
        return surface.DesiredSize;
    }

    /// <inheritdoc />
    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        surface.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>两栏与分隔条的排版：分隔条占 space.md，剩下的按比例分（收起的一栏不占位置）；收起按钮在分隔条中间。</summary>
    sealed class SplitterPanel(Splitter owner) : Panel
    {
        (double Start, double Track, double End) Lengths(double main)
        {
            var track = owner.TrackThickness;
            var (start, end) = SplitterScale.Panes(main - track, owner.Size, owner.Hidden);
            return (start, track, end);
        }

        System.Windows.Size Along(double main, double cross) => owner.Vertical ? new System.Windows.Size(cross, main) : new System.Windows.Size(main, cross);

        static double Main(System.Windows.Size size, bool vertical) => vertical ? size.Height : size.Width;

        static double Cross(System.Windows.Size size, bool vertical) => vertical ? size.Width : size.Height;

        protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
        {
            var vertical = owner.Vertical;
            var main = Main(availableSize, vertical);
            var cross = Cross(availableSize, vertical);
            owner.toggle.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            if (double.IsInfinity(main))
            {
                // 没给长度（放在能无限伸展的容器里）：两栏按内容
                owner.startPane.Measure(Along(double.PositiveInfinity, cross));
                owner.endPane.Measure(Along(double.PositiveInfinity, cross));
                owner.Handle.Measure(Along(owner.TrackThickness, cross));
                main = Main(owner.startPane.DesiredSize, vertical) + owner.TrackThickness + Main(owner.endPane.DesiredSize, vertical);
            }
            else
            {
                var (start, track, end) = Lengths(main);
                owner.startPane.Measure(Along(start, cross));
                owner.endPane.Measure(Along(end, cross));
                owner.Handle.Measure(Along(track, cross));
            }
            var tallest = Math.Max(Cross(owner.startPane.DesiredSize, vertical), Cross(owner.endPane.DesiredSize, vertical));
            return Along(main, double.IsInfinity(cross) ? tallest : Math.Min(cross, tallest));
        }

        protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
        {
            var vertical = owner.Vertical;
            var cross = Cross(finalSize, vertical);
            var (start, track, end) = Lengths(Main(finalSize, vertical));
            Rect Slot(double offset, double length) => vertical ? new Rect(0, offset, cross, length) : new Rect(offset, 0, length, cross);
            owner.startPane.Arrange(Slot(0, start));
            var trackRect = Slot(start, track);
            owner.Handle.Arrange(trackRect);
            owner.endPane.Arrange(Slot(start + track, end));
            var button = owner.toggle.DesiredSize;
            owner.toggle.Arrange(new Rect(trackRect.X + (trackRect.Width - button.Width) / 2, trackRect.Y + (trackRect.Height - button.Height) / 2, button.Width, button.Height));
            return finalSize;
        }
    }
}

/// <summary>
/// 分栏的分隔条（TS 的 role="separator"）：可聚焦，能拖；看得见的是中间一条细线，平时 borderMuted、border 宽，
/// 悬停、拖动时 borderStrong、加粗到 md，键盘聚焦时强调色、md（粗细变化有过渡）。
/// 读屏是分隔符，带当前比例（收起起始一栏时 0、收起结束一栏时 100）与范围。
/// </summary>
public sealed class SplitterHandle : FrameworkElement
{
    static readonly DependencyProperty LineProperty = DependencyProperty.Register("Line", typeof(double), typeof(SplitterHandle),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    // 线的颜色按状态换资源键（引用资源：换主题时跟着重画）
    static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register("LineBrush", typeof(Brush), typeof(SplitterHandle),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    readonly Splitter owner;
    bool focusVisible;
    bool dragging;
    double from;
    Point origin;

    internal SplitterHandle(Splitter owner)
    {
        this.owner = owner;
        Focusable = true;
        FocusVisualStyle = null;
    }

    /// <summary>读屏念的当前值。</summary>
    public double Value => SplitterScale.ValueNow(owner.Size, owner.Hidden);

    /// <summary>拖动中（指针离开了分隔条也保持加粗变深）。</summary>
    public bool IsDragging => dragging;

    /// <summary>比例、方向变了：光标与线的样子跟着。</summary>
    internal void Refresh()
    {
        Cursor = owner.Vertical ? Cursors.SizeNS : Cursors.SizeWE;
        UpdateLine();
    }

    /// <summary>线的粗细与颜色：平时 xs、borderMuted，悬停、拖动 md、borderStrong，键盘聚焦 md、强调色。</summary>
    void UpdateLine()
    {
        var strong = IsMouseOver || dragging || focusVisible;
        SetResourceReference(LineBrushProperty, focusVisible ? ThemeKeys.Brush.Accent : strong ? ThemeKeys.Brush.BorderStrong : ThemeKeys.Brush.BorderMuted);
        var to = this.Resource<double>(strong ? ThemeKeys.BorderWidth.Md : ThemeKeys.BorderWidth.Xs);
        var duration = Motion.Duration(this, ThemeKeys.Duration.Sm);
        if (duration <= TimeSpan.Zero || !IsLoaded) SetValue(LineProperty, to);
        else BeginAnimation(LineProperty, new DoubleAnimation(to, duration) { EasingFunction = this.Resource<IEasingFunction>(ThemeKeys.Easing.Standard) });
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext context)
    {
        // 整块（space.md 宽）都能按：看得见的线很细
        context.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var line = (double)GetValue(LineProperty);
        var size = RenderSize;
        var rect = owner.Vertical ? new Rect(0, (size.Height - line) / 2, size.Width, line) : new Rect((size.Width - line) / 2, 0, line, size.Height);
        context.DrawRectangle((Brush?)GetValue(LineBrushProperty) ?? this.Resource<Brush>(ThemeKeys.Brush.BorderMuted), null, rect);
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateLine();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateLine();
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        e.Handled = true;
        if (e.ClickCount >= 2)
        {
            Restore();
            return;
        }
        PointerDown(e.GetPosition(owner));
        CaptureMouse();
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        PointerMove(e.GetPosition(owner));
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

    /// <summary>双击：回到初始比例。</summary>
    internal void Restore() => owner.SetCurrentValue(Splitter.SizeProperty, SplitterScale.Fit(owner.InitialSize, owner.MinSize, owner.MaxSize));

    /// <summary>按下（分栏的坐标：分栏不随拖动移动；WPF 从右到左排版时坐标也镜像，往起始方向拖仍是负的）。</summary>
    internal void PointerDown(Point point)
    {
        origin = point;
        from = owner.Size;
        dragging = true;
        UpdateLine();
    }

    internal void PointerMove(Point point)
    {
        if (!dragging) return;
        var vertical = owner.Vertical;
        var delta = vertical ? point.Y - origin.Y : point.X - origin.X;
        var total = vertical ? owner.ActualHeight : owner.ActualWidth;
        owner.Resize(SplitterScale.Drag(from, delta, total, owner.MinSize, owner.MaxSize));
    }

    internal void PointerUp()
    {
        if (!dragging) return;
        dragging = false;
        UpdateLine();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && owner.Press(e.Key)) e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        focusVisible = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
        UpdateLine();
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        focusVisible = false;
        UpdateLine();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new HandlePeer(this);

    /// <summary>读屏：分隔符，名称是 Label 或语言包；取值是起始一栏的比例，范围是最小、最大比例；设值即改比例。</summary>
    sealed class HandlePeer(SplitterHandle handle) : FrameworkElementAutomationPeer(handle), IRangeValueProvider
    {
        Splitter Splitter => handle.owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;

        protected override string GetClassNameCore() => nameof(Splitter);

        protected override string GetNameCore() => Splitter.Label ?? Provider.GetLang(Splitter).SplitterResize;

        protected override AutomationOrientation GetOrientationCore() => Splitter.Vertical ? AutomationOrientation.Horizontal : AutomationOrientation.Vertical;

        protected override bool IsKeyboardFocusableCore() => true;

        public override object GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);

        public bool IsReadOnly => !Splitter.IsEnabled;

        public double Value => handle.Value;

        public double Minimum => Splitter.MinSize;

        public double Maximum => Splitter.MaxSize;

        public double SmallChange => SplitterScale.Step;

        public double LargeChange => SplitterScale.Step;

        public void SetValue(double value) => Splitter.Resize(value);
    }
}

/// <summary>Splitter 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:SplitterParts.Toggle}" TargetType="rune:IconButton"&gt;</c>。</summary>
public static class SplitterParts
{
    /// <summary>起始一栏（ScrollArea，TS 的 propsStart 与 propsScroll）。</summary>
    public static readonly PartKey Start = new(typeof(Splitter), nameof(Start), typeof(ScrollArea));

    /// <summary>结束一栏（ScrollArea，TS 的 propsEnd 与 propsScroll）。</summary>
    public static readonly PartKey End = new(typeof(Splitter), nameof(End), typeof(ScrollArea));

    /// <summary>分隔条（<see cref="SplitterHandle"/>，TS 的 propsHandle）。</summary>
    public static readonly PartKey Handle = new(typeof(Splitter), nameof(Handle), typeof(SplitterHandle));

    /// <summary>收起、展开按钮（IconButton，TS 的 propsToggle）。</summary>
    public static readonly PartKey Toggle = new(typeof(Splitter), nameof(Toggle), typeof(IconButton));
}
