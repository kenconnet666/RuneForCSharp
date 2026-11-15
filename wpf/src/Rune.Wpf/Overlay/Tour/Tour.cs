using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>引导的一步（vue-tsx-pro 的 TourStep）。</summary>
public sealed class TourStep
{
    /// <summary>高亮的目标；不写（或还没显示出来）时卡片居中、整窗遮罩。</summary>
    public FrameworkElement? Target { get; init; }

    /// <summary>按需取目标（目标在打开引导后才建出来时）；写了时代替 Target。</summary>
    public Func<FrameworkElement?>? TargetFactory { get; init; }

    /// <summary>标题（卡片的读屏名称）。</summary>
    public required string Title { get; init; }

    /// <summary>说明：文字或元素。</summary>
    public object? Content { get; init; }

    /// <summary>标题上方的封面：插图、截图。</summary>
    public object? Cover { get; init; }

    /// <summary>卡片在目标的哪一侧，默认下方；空间不够时翻转。</summary>
    public FloatingPlacement? Placement { get; init; }
}

/// <summary>
/// 新手引导（vue-tsx-pro 的 Tour）：一步一步高亮窗口里的元素并说明。遮罩盖住整个窗口，目标处镂空；步骤卡片贴着目标显示。
/// <code>
/// var tour = new Tour { Steps = [new() { Target = search, Title = "搜索", Content = "按名称或编号查找订单" }, new() { Title = "就这些" }] };
/// tour.IsOpen = true;
/// </code>
/// <list type="bullet">
/// <item>模态：盖住窗口的客户区，Esc 关闭，焦点在卡片里循环，关闭后还给打开前的元素（同 Dialog）。</item>
/// <item>目标：元素（或按需取的函数）；换步骤时滚进视口，窗口大小、排版变化时镂空与卡片跟着动；没有目标时卡片居中、整窗遮罩。</item>
/// <item>卡片：封面、标题、说明、步数、上一步 / 下一步（最后一步是“完成”）、跳过。键盘 → / ← 下一步 / 上一步（从右到左时对调，焦点在输入框里时不处理）。</item>
/// <item>IsOpen、Current 默认双向绑定；点“完成”时发 <see cref="Finished"/>；<see cref="OpenChanged"/> 报告关闭的原因（跳过是 close，完成是 confirm，Esc 是 escape）。</item>
/// </list>
/// 引导写在哪里都行（自己不可见、不参与排版）。
/// </summary>
public class Tour : FrameworkElement
{
    /// <summary>各步。</summary>
    public static readonly DependencyProperty StepsProperty = DependencyProperty.Register(nameof(Steps), typeof(IReadOnlyList<TourStep>), typeof(Tour),
        new PropertyMetadata(Array.Empty<TourStep>(), (sender, _) => ((Tour)sender).Sync()));

    /// <summary>开着（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Tour),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (sender, _) => ((Tour)sender).Toggle()));

    /// <summary>当前第几步，从 0 开始（默认双向绑定）。</summary>
    public static readonly DependencyProperty CurrentProperty = DependencyProperty.Register(nameof(Current), typeof(int), typeof(Tour),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (sender, _) => ((Tour)sender).Sync()));

    /// <summary>镂空比目标大出多少，默认 6。</summary>
    public static readonly DependencyProperty SpotlightPaddingProperty = DependencyProperty.Register(nameof(SpotlightPadding), typeof(double), typeof(Tour), new PropertyMetadata(6.0));

    /// <summary>下一步可用，默认是：完成某个操作后才能继续时先设为 false。</summary>
    public static readonly DependencyProperty CanGoNextProperty = DependencyProperty.Register(nameof(CanGoNext), typeof(bool), typeof(Tour),
        new PropertyMetadata(true, (sender, _) => ((Tour)sender).Sync()));

    readonly Popup popup = new() { StaysOpen = true, AllowsTransparency = true, Placement = PlacementMode.Center };
    readonly TourLayer layer;
    readonly TourSpotlight spotlight = new();
    // 阴影画在单独的一层上：DropShadowEffect 套在卡片上会连文字一起投影（同 Dialog）
    readonly Border shadow = new();
    readonly Border panel = new() { Focusable = true, ClipToBounds = true };
    readonly ContentPresenter cover = new();
    readonly H2 title = new() { Size = TextSize.Md };
    readonly ContentPresenter content = new();
    readonly Flex footer = new() { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true };
    readonly Text progress = new() { Size = TextSize.Sm, Tone = TextTone.Muted };
    readonly Button skip = new() { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Size = TextSize.Sm };
    readonly Button previous = new() { Variant = ButtonVariant.Outline, Size = TextSize.Sm };
    readonly Button next = new() { Size = TextSize.Sm };
    OpenChangeReason? closing;
    IInputElement? returnFocus;
    Window? host;
    FrameworkElement? target;

    /// <summary>新建：弹出层（遮罩、卡片：封面、标题、说明、底栏）。</summary>
    public Tour()
    {
        layer = new TourLayer(this);
        Flex.SetGrow(progress, true);
        footer.Children.Add(progress);
        footer.Children.Add(skip);
        footer.Children.Add(previous);
        footer.Children.Add(next);
        panel.Child = new StackPanel { Children = { cover, title, content, footer } };
        KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.Cycle);
        AutomationProperties.SetLabeledBy(panel, title);
        layer.Children.Add(spotlight);
        layer.Children.Add(shadow);
        layer.Children.Add(panel);
        popup.Child = layer;
        AddLogicalChild(popup);
        skip.Click += (_, _) => Close(OpenChangeReason.Close);
        previous.Click += (_, _) => Go(Current - 1);
        next.Click += (_, _) => Next();
        layer.PreviewKeyDown += OnKeyDown;
        // 自己不参与排版：引导画在弹出层里
        Visibility = Visibility.Collapsed;
    }

    /// <summary>各步。</summary>
    public IReadOnlyList<TourStep> Steps { get => (IReadOnlyList<TourStep>)GetValue(StepsProperty); set => SetValue(StepsProperty, value); }

    /// <summary>开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>当前第几步。</summary>
    public int Current { get => (int)GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }

    /// <summary>镂空比目标大出多少。</summary>
    public double SpotlightPadding { get => (double)GetValue(SpotlightPaddingProperty); set => SetValue(SpotlightPaddingProperty, value); }

    /// <summary>下一步可用。</summary>
    public bool CanGoNext { get => (bool)GetValue(CanGoNextProperty); set => SetValue(CanGoNextProperty, value); }

    /// <summary>点了最后一步的“完成”。</summary>
    public event EventHandler? Finished;

    /// <summary>用户关了（跳过、完成、Esc）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>卡片与它的部件（测试用）。</summary>
    internal (Border Panel, ContentPresenter Cover, H2 Title, ContentPresenter Content, Text Progress, Button Skip, Button Previous, Button Next) Parts =>
        (panel, cover, title, content, progress, skip, previous, next);

    /// <summary>镂空的位置（相对窗口的内容；没有目标时为 null，测试用）。</summary>
    internal Rect? Spotlight => spotlight.Hole;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { popup }.GetEnumerator();

    TourStep? Step => Steps.Count == 0 ? null : Steps[Math.Clamp(Current, 0, Steps.Count - 1)];

    bool Last => Current >= Steps.Count - 1;

    Size Area => host?.Content is FrameworkElement root ? new Size(root.ActualWidth, root.ActualHeight) : default;

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new HiddenPeer(this);

    void Go(int step) => SetCurrentValue(CurrentProperty, Math.Clamp(step, 0, Math.Max(0, Steps.Count - 1)));

    /// <summary>下一步；最后一步时完成：关闭并发 Finished。</summary>
    void Next()
    {
        if (!Last)
        {
            Go(Current + 1);
            return;
        }
        Close(OpenChangeReason.Confirm);
        Finished?.Invoke(this, EventArgs.Empty);
    }

    void Close(OpenChangeReason reason)
    {
        closing = reason;
        SetCurrentValue(IsOpenProperty, false);
    }

    /// <summary>打开：盖住窗口、记下焦点来源；关上：还回焦点，报告原因。</summary>
    void Toggle()
    {
        if (IsOpen)
        {
            host = Window.GetWindow(this);
            returnFocus = Keyboard.FocusedElement;
            popup.PlacementTarget = host?.Content as UIElement;
            if (host is not null) host.LayoutUpdated += OnHostLayout;
            popup.IsOpen = true;
            Sync();
            return;
        }
        if (host is not null) host.LayoutUpdated -= OnHostLayout;
        host = null;
        popup.IsOpen = false;
        returnFocus?.Focus();
        returnFocus = null;
        var reason = closing;
        closing = null;
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(Dialog.OpenChangedEvent, false, known));
    }

    /// <summary>换了一步：卡片的内容与按钮，目标滚进视口，焦点移到卡片上。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        var step = Step;
        cover.Content = step?.Cover;
        cover.Visibility = step?.Cover is null ? Visibility.Collapsed : Visibility.Visible;
        title.Text = step?.Title;
        content.Content = step?.Content is string text ? new Paragraph { Text = text, Tone = TextTone.Muted } : step?.Content;
        content.Visibility = step?.Content is null ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetHelpText(panel, step?.Content as string ?? "");
        progress.Text = lang.TourProgress(Current + 1, Steps.Count);
        skip.Content = lang.TourSkip;
        skip.Visibility = Last ? Visibility.Collapsed : Visibility.Visible;
        previous.Content = lang.TourPrevious;
        previous.Visibility = Current > 0 ? Visibility.Visible : Visibility.Collapsed;
        next.Content = Last ? lang.TourFinish : lang.TourNext;
        next.IsEnabled = CanGoNext;
        UpdateMetrics();
        if (!IsOpen) return;
        target = step?.TargetFactory?.Invoke() ?? step?.Target;
        target?.BringIntoView();
        layer.InvalidateMeasure();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => panel.Focus());
    }

    /// <summary>卡片：surfaceRaised 底、border 细边、lg 圆角、lg 阴影，宽 min(22em, 窗口宽 − 2em)，四周 lg、各部分之间 sm；封面贴着卡片的上、左、右边。</summary>
    void UpdateMetrics()
    {
        var em = this.Resource<double>(ThemeKeys.FontSize.Md);
        double Space(string key) => this.Resource<double>(key) * em;
        var (xs, sm, lg) = (Space(ThemeKeys.Space.Xs), Space(ThemeKeys.Space.Sm), Space(ThemeKeys.Space.Lg));
        panel.Padding = new Thickness(lg);
        panel.BorderThickness = new Thickness(1);
        panel.CornerRadius = shadow.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Lg) * em);
        panel.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.SurfaceRaised);
        panel.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.Brush.Border);
        shadow.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.SurfaceRaised);
        shadow.SetResourceReference(EffectProperty, ThemeKeys.Shadow.Lg);
        cover.Margin = new Thickness(-lg, -lg, -lg, xs + sm);
        title.Margin = new Thickness(0, 0, 0, sm);
        content.Margin = new Thickness(0, 0, 0, sm);
        footer.Margin = new Thickness(0, xs, 0, 0);
        spotlight.Radius = this.Resource<double>(ThemeKeys.Radius.Md) * em;
        spotlight.SetResourceReference(TourSpotlight.FillProperty, ThemeKeys.Brush.Overlay);
        TextElement.SetFontSize(layer, em);
        layer.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.Brush.Text);
    }

    /// <summary>窗口排版变了（滚动、缩放、目标变大小）：镂空与卡片跟着动。</summary>
    void OnHostLayout(object? sender, EventArgs e)
    {
        if (TargetBox() != spotlight.Hole) layer.InvalidateArrange();
    }

    /// <summary>目标在窗口内容里的位置（加上镂空的留白）；没有目标或目标不在界面上时为 null。</summary>
    internal Rect? TargetBox()
    {
        if (target is not { IsVisible: true } || host?.Content is not Visual root || !target.IsDescendantOf(root)) return null;
        var box = new Rect(target.TranslatePoint(default, (UIElement)root), target.RenderSize);
        box.Inflate(SpotlightPadding, SpotlightPadding);
        return box;
    }

    /// <summary>→ / ← 切换步骤（从右到左时对调）；焦点在输入框里时留给它，下一步不可用时不前进。Esc 关闭（测试也从这里进来）。</summary>
    internal void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(OpenChangeReason.Escape);
            return;
        }
        if (e.Key is not (Key.Right or Key.Left) || Keyboard.Modifiers != ModifierKeys.None || e.OriginalSource is TextBoxBase) return;
        var forward = (e.Key == Key.Right) != (panel.FlowDirection == FlowDirection.RightToLeft);
        if (forward && CanGoNext) Next();
        else if (!forward && Current > 0) Go(Current - 1);
        else return;
        e.Handled = true;
    }

    /// <summary>铺满窗口的一层：遮罩（镂空在目标处），卡片贴着目标（默认下方，放不下时翻转），没有目标时居中。</summary>
    sealed class TourLayer(Tour owner) : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var area = owner.Area;
            var em = owner.Resource<double>(ThemeKeys.FontSize.Md);
            owner.spotlight.Measure(area);
            owner.panel.Width = owner.shadow.Width = Math.Min(22 * em, Math.Max(0, area.Width - 2 * em));
            owner.panel.Measure(area);
            owner.shadow.Measure(area);
            return area;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var hole = owner.TargetBox();
            owner.spotlight.Hole = hole;
            owner.spotlight.Arrange(new Rect(finalSize));
            var size = owner.panel.DesiredSize;
            Rect box;
            if (hole is { } around)
            {
                var (_, placed) = FloatingGeometry.Place(owner.Step?.Placement ?? FloatingPlacement.Bottom, owner.FlowDirection == FlowDirection.RightToLeft,
                    new FloatingBox(around.X, around.Y, around.Width, around.Height), size.Width, size.Height, 8, new FloatingBox(0, 0, finalSize.Width, finalSize.Height), crossAxisFallback: true);
                box = new Rect(placed.X, placed.Y, placed.Width, placed.Height);
            }
            else box = new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height);
            owner.shadow.Arrange(box);
            owner.panel.Arrange(box);
            return finalSize;
        }
    }

    /// <summary>引导自己不占位置，读屏看不到它（卡片才是对话框）。</summary>
    sealed class HiddenPeer(Tour owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>遮罩：铺满整层的 overlay 色，目标处挖掉一个圆角矩形（上游用镂空盒子外一圈巨大的阴影画）；没有目标时整层遮罩。</summary>
sealed class TourSpotlight : FrameworkElement
{
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(TourSpotlight),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public double Radius { get; set; }

    public Rect? Hole
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        Geometry whole = new RectangleGeometry(new Rect(RenderSize));
        var shape = Hole is { } hole ? new CombinedGeometry(GeometryCombineMode.Exclude, whole, new RectangleGeometry(hole, Radius, Radius)) : whole;
        drawingContext.DrawGeometry(Fill, null, shape);
    }
}
