using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

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
/// 部件是子元素（<see cref="SplitterParts"/>）：两栏的 ScrollArea、分隔条、收起按钮，用后代选择器改样式。
/// </summary>
public class Splitter : Control
{
    /// <summary>起始一栏（左或上）的内容。</summary>
    public static readonly StyledProperty<object?> StartProperty = AvaloniaProperty.Register<Splitter, object?>(nameof(Start));

    /// <summary>结束一栏（右或下）的内容。</summary>
    public static readonly StyledProperty<object?> EndProperty = AvaloniaProperty.Register<Splitter, object?>(nameof(End));

    /// <summary>两栏的排法，默认左右并排。</summary>
    public static readonly StyledProperty<SplitterOrientation> OrientationProperty = AvaloniaProperty.Register<Splitter, SplitterOrientation>(nameof(Orientation));

    /// <summary>起始一栏占的百分比（默认双向绑定），默认 50。</summary>
    public static readonly StyledProperty<double> SizeProperty = AvaloniaProperty.Register<Splitter, double>(nameof(Size), 50, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>起始一栏最少占多少（百分比），默认 20。</summary>
    public static readonly StyledProperty<double> MinSizeProperty = AvaloniaProperty.Register<Splitter, double>(nameof(MinSize), 20);

    /// <summary>起始一栏最多占多少（百分比），默认 80。</summary>
    public static readonly StyledProperty<double> MaxSizeProperty = AvaloniaProperty.Register<Splitter, double>(nameof(MaxSize), 80);

    /// <summary>哪一侧可以收起；null（默认）时不能收起。</summary>
    public static readonly StyledProperty<PaneSide?> CollapsibleProperty = AvaloniaProperty.Register<Splitter, PaneSide?>(nameof(Collapsible));

    /// <summary>可收起的那一侧收着（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsCollapsedProperty = AvaloniaProperty.Register<Splitter, bool>(nameof(IsCollapsed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>分隔条的读屏名称；不写时取语言包（调整分栏）。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Splitter, string?>(nameof(Label));

    /// <summary>外面一圈边框、圆角 lg 与表面色：独立成块（收件箱的列表与预览）。</summary>
    public static readonly StyledProperty<bool> BorderedProperty = AvaloniaProperty.Register<Splitter, bool>(nameof(Bordered));

    /// <summary>表面色（Bordered 时主题给 surface）。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<Splitter>();

    /// <summary>边框色（Bordered 时主题给 border）。</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty = Border.BorderBrushProperty.AddOwner<Splitter>();

    /// <summary>字号（继承）：分隔条的宽度、圆角按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Splitter>();

    readonly Border surface = new() { ClipToBounds = true };
    readonly SplitterPanel panel;
    readonly ScrollArea startPane = new() { Name = SplitterParts.Start, Axis = ScrollAreaAxis.Both };
    readonly ScrollArea endPane = new() { Name = SplitterParts.End, Axis = ScrollAreaAxis.Both };
    readonly IconButton toggle = new() { Name = SplitterParts.Toggle, Size = TextSize.Xs, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Focusable = false };
    double? initialSize;

    static Splitter()
    {
        AffectsMeasure<Splitter>(OrientationProperty, SizeProperty, CollapsibleProperty, IsCollapsedProperty, FontSizeProperty);
    }

    /// <summary>新建：左右并排、各占一半。</summary>
    public Splitter()
    {
        Handle = new SplitterHandle(this);
        panel = new SplitterPanel(this);
        // 收起按钮最后加：叠在分隔线与两栏上面
        panel.Children.AddRange([startPane, endPane, Handle, toggle]);
        surface.Bind(Border.BackgroundProperty, this.GetObservable(BackgroundProperty));
        surface.Bind(Border.BorderBrushProperty, this.GetObservable(BorderBrushProperty));
        surface.Child = panel;
        LogicalChildren.Add(surface);
        VisualChildren.Add(surface);
        toggle.Click += (_, _) => SetCurrentValue(IsCollapsedProperty, !IsCollapsed);
        Update();
    }

    /// <summary>起始一栏的内容。</summary>
    public object? Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <summary>结束一栏的内容。</summary>
    public object? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>两栏的排法。</summary>
    public SplitterOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>起始一栏占的百分比。</summary>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>起始一栏最少占多少。</summary>
    public double MinSize { get => GetValue(MinSizeProperty); set => SetValue(MinSizeProperty, value); }

    /// <summary>起始一栏最多占多少。</summary>
    public double MaxSize { get => GetValue(MaxSizeProperty); set => SetValue(MaxSizeProperty, value); }

    /// <summary>哪一侧可以收起。</summary>
    public PaneSide? Collapsible { get => GetValue(CollapsibleProperty); set => SetValue(CollapsibleProperty, value); }

    /// <summary>可收起的那一侧收着。</summary>
    public bool IsCollapsed { get => GetValue(IsCollapsedProperty); set => SetValue(IsCollapsedProperty, value); }

    /// <summary>分隔条的读屏名称。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>外面一圈边框。</summary>
    public bool Bordered { get => GetValue(BorderedProperty); set => SetValue(BorderedProperty, value); }

    /// <summary>表面色。</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>边框色。</summary>
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

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

    /// <summary>分隔条的粗细：space.md。</summary>
    internal double TrackThickness => this.Resource<double>(ThemeKeys.Space.Md) * FontSize;

    /// <summary>双击回到的比例：第一次排版时的比例（TS 的初始 size）。</summary>
    internal double InitialSize => initialSize ?? Size;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StartProperty) startPane.Content = Start;
        else if (change.Property == EndProperty) endPane.Content = End;
        else if (change.Property == OrientationProperty || change.Property == CollapsibleProperty || change.Property == IsCollapsedProperty
                 || change.Property == BorderedProperty || change.Property == FontSizeProperty) Update();
        if (change.Property == SizeProperty || change.Property == MinSizeProperty || change.Property == MaxSizeProperty
            || change.Property == IsCollapsedProperty || change.Property == CollapsibleProperty || change.Property == LabelProperty) Handle.Refresh();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        initialSize ??= Size;
        Update();
    }

    /// <summary>收起的一栏不显示；收起按钮的箭头指向收起的方向、名称是"收起""展开"；Bordered 时边框与圆角。</summary>
    void Update()
    {
        startPane.IsVisible = Hidden != PaneSide.Start;
        endPane.IsVisible = Hidden != PaneSide.End;
        toggle.IsVisible = Collapsible is not null;
        if (Collapsible is { } side)
        {
            var toStart = SplitterScale.TogglePointsToStart(side, IsCollapsed);
            toggle.Icon = Vertical ? toStart ? Lucide.ChevronUp : Lucide.ChevronDown : toStart ? Lucide.ChevronLeft : Lucide.ChevronRight;
            var lang = Provider.GetLang(this);
            AutomationProperties.SetName(toggle, IsCollapsed ? lang.SplitterExpand : lang.SplitterCollapse);
        }
        PseudoClasses.Set(":collapsed", IsCollapsed && Collapsible is not null);
        surface.BorderThickness = new Thickness(Bordered ? this.Resource<double>(ThemeKeys.BorderWidth.Xs) : 0);
        surface.CornerRadius = new CornerRadius(Bordered ? this.Resource<double>(ThemeKeys.Radius.Lg) * FontSize : 0);
        Handle.Refresh();
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
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        surface.Measure(availableSize);
        return surface.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
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

        LayoutSize Along(double main, double cross) => owner.Vertical ? new LayoutSize(cross, main) : new LayoutSize(main, cross);

        static double Main(LayoutSize size, bool vertical) => vertical ? size.Height : size.Width;

        static double Cross(LayoutSize size, bool vertical) => vertical ? size.Width : size.Height;

        protected override LayoutSize MeasureOverride(LayoutSize availableSize)
        {
            var vertical = owner.Vertical;
            var main = Main(availableSize, vertical);
            var cross = Cross(availableSize, vertical);
            owner.toggle.Measure(LayoutSize.Infinity);
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

        protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
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
            owner.toggle.Arrange(new Rect(trackRect.Center.X - button.Width / 2, trackRect.Center.Y - button.Height / 2, button.Width, button.Height));
            return finalSize;
        }
    }
}

/// <summary>
/// 分栏的分隔条（TS 的 role="separator"）：可聚焦，能拖；看得见的是中间一条细线，平时 borderMuted、border 宽，
/// 悬停、拖动时 borderStrong、加粗到 md，键盘聚焦时强调色、md（粗细变化有过渡）。
/// 读屏是分隔符，带当前比例（收起起始一栏时 0、收起结束一栏时 100）与范围。
/// </summary>
public sealed class SplitterHandle : Control, global::Avalonia.Rendering.ICustomHitTest
{
    static readonly StyledProperty<double> LineProperty = AvaloniaProperty.Register<SplitterHandle, double>("Line");

    readonly Splitter owner;
    bool focusVisible;
    bool dragging;
    double from;
    Point origin;

    static SplitterHandle() => AffectsRender<SplitterHandle>(LineProperty);

    internal SplitterHandle(Splitter owner)
    {
        this.owner = owner;
        Name = SplitterParts.Handle;
        Focusable = true;
        this.RedrawOnResourceChanges(this);
    }

    /// <summary>读屏念的当前值。</summary>
    public double Value => SplitterScale.ValueNow(owner.Size, owner.Hidden);

    /// <summary>拖动中（指针离开了分隔条也保持加粗变深）。</summary>
    public bool IsDragging => dragging;

    /// <summary>键盘聚焦。</summary>
    internal bool ShowsFocusRing => focusVisible;

    /// <summary>比例、名称、方向变了：光标与线的样子跟着。</summary>
    internal void Refresh()
    {
        Cursor = new Cursor(owner.Vertical ? StandardCursorType.SizeNorthSouth : StandardCursorType.SizeWestEast);
        UpdateLine();
    }

    /// <summary>整块（space.md 宽）都能按：看得见的线很细，只按画出来的部分命中就很难按中。</summary>
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <summary>线的粗细：平时 xs，悬停、拖动、键盘聚焦时 md。</summary>
    void UpdateLine()
    {
        var strong = IsPointerOver || dragging || focusVisible;
        SetCurrentValue(LineProperty, this.Resource<double>(strong ? ThemeKeys.BorderWidth.Md : ThemeKeys.BorderWidth.Xs));
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var duration = Motion.Duration(this, ThemeKeys.Duration.Sm);
        Transitions = duration > TimeSpan.Zero
            ? [new DoubleTransition { Property = LineProperty, Duration = duration, Easing = this.Resource<global::Avalonia.Animation.Easings.Easing>(ThemeKeys.Easing.Standard) }]
            : null;
        Refresh();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var key = focusVisible ? ThemeKeys.Brush.Accent : IsPointerOver || dragging ? ThemeKeys.Brush.BorderStrong : ThemeKeys.Brush.BorderMuted;
        var line = GetValue(LineProperty);
        var size = Bounds.Size;
        var rect = owner.Vertical ? new Rect(0, (size.Height - line) / 2, size.Width, line) : new Rect((size.Width - line) / 2, 0, line, size.Height);
        context.FillRectangle(this.Resource<IBrush>(key), rect);
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateLine();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        UpdateLine();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // 只认主键
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        if (e.ClickCount >= 2)
        {
            owner.SetCurrentValue(Splitter.SizeProperty, SplitterScale.Fit(owner.InitialSize, owner.MinSize, owner.MaxSize));
            return;
        }
        // 按分栏的坐标量（分栏不随拖动移动；从右到左排版时整个镜像，往起始方向拖仍是负的）
        origin = e.GetPosition(owner);
        from = owner.Size;
        dragging = true;
        e.Pointer.Capture(this);
        UpdateLine();
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!dragging) return;
        var point = e.GetPosition(owner);
        var vertical = owner.Vertical;
        var delta = vertical ? point.Y - origin.Y : point.X - origin.X;
        var total = vertical ? owner.Bounds.Height : owner.Bounds.Width;
        owner.Resize(SplitterScale.Drag(from, delta, total, owner.MinSize, owner.MaxSize));
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!dragging) return;
        e.Pointer.Capture(null);
        EndDrag();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndDrag();
    }

    void EndDrag()
    {
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
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        focusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        UpdateLine();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        focusVisible = false;
        UpdateLine();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new HandlePeer(this);

    /// <summary>读屏：分隔符，名称是 Label 或语言包；取值是起始一栏的比例，范围是最小、最大比例；设值即改比例。</summary>
    sealed class HandlePeer(SplitterHandle handle) : ControlAutomationPeer(handle), IRangeValueProvider
    {
        Splitter Splitter => handle.owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;

        protected override string GetClassNameCore() => nameof(Splitter);

        protected override string? GetNameCore() => Splitter.Label ?? Provider.GetLang(Splitter).SplitterResize;

        public bool IsReadOnly => !Splitter.IsEffectivelyEnabled;

        public double Value => handle.Value;

        public double Minimum => Splitter.MinSize;

        public double Maximum => Splitter.MaxSize;

        public double SmallChange => SplitterScale.Step;

        public double LargeChange => SplitterScale.Step;

        public void SetValue(double value) => Splitter.Resize(value);
    }
}

/// <summary>Splitter 的部件名：样式用 <c>rune|Splitter rune|ScrollArea#PART_Start</c> 这样的后代选择器。</summary>
public static class SplitterParts
{
    /// <summary>起始一栏（ScrollArea，TS 的 propsStart 与 propsScroll）。</summary>
    public const string Start = "PART_Start";

    /// <summary>结束一栏（ScrollArea，TS 的 propsEnd 与 propsScroll）。</summary>
    public const string End = "PART_End";

    /// <summary>分隔条（<see cref="SplitterHandle"/>，TS 的 propsHandle）。</summary>
    public const string Handle = "PART_Handle";

    /// <summary>收起、展开按钮（IconButton，TS 的 propsToggle）。</summary>
    public const string Toggle = "PART_Toggle";
}
