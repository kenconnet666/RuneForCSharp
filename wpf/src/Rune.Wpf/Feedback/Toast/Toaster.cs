using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 通知的显示区域（vue-tsx-pro 的 Toaster）：放一个，交给它 <see cref="ToasterController"/>，在任何地方调控制器显示通知。
/// <code>
/// &lt;Grid&gt;…页面…&lt;rune:Toaster Controller="{x:Static local:App.Toaster}" Placement="bottom-end" /&gt;&lt;/Grid&gt;
/// </code>
/// 桌面上没有 position: fixed：放在盖住整个窗口的 Grid 里，通知停在它的一角（<see cref="Placement"/>，四周 lg 的边，宽 26em），
/// 空白处不挡点击。每条通知就是 Alert（surface 外观、lg 阴影），图标、标题、正文、操作、关闭按钮都复用它。
/// <list type="bullet">
/// <item>计时：指针在通知上、焦点在区域里、窗口不在前台时暂停。<see cref="Max"/>（默认 3）条以外的先不显示，前面的关闭后补上。</item>
/// <item>无障碍：区域是带名称的一组（默认“通知”），F8 跳到通知区域，焦点在一条通知里时 Esc 关闭它；新通知读屏礼貌地念，重要的立即念。
/// 关闭一条后焦点交给相邻的通知，没有时回到按 F8 之前的位置。</item>
/// <item>动画：进场淡入并从所在的边滑进来，离场淡出、高度收起后从控制器里移除；横向拖动超过宽度的 35% 就关闭，否则弹回。</item>
/// <item><see cref="Stack"/>：像 Sonner 一样叠成一摞，悬停或聚焦时展开；<see cref="Progress"/>：自动关闭的通知底部显示剩余时间，暂停时一起停。</item>
/// </list>
/// </summary>
public class Toaster : FrameworkElement
{
    /// <summary>通知控制器。</summary>
    public static readonly DependencyProperty ControllerProperty = DependencyProperty.Register(nameof(Controller), typeof(ToasterController), typeof(Toaster),
        new FrameworkPropertyMetadata(null, (d, _) => ((Toaster)d).OnControllerChanged()));

    /// <summary>显示位置，默认 bottom-end。</summary>
    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(nameof(Placement), typeof(ToastPlacement), typeof(Toaster),
        new FrameworkPropertyMetadata(ToastPlacement.BottomEnd, FrameworkPropertyMetadataOptions.AffectsArrange, (d, _) => ((Toaster)d).Sync()));

    /// <summary>最多同时显示几条，默认 3。</summary>
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(nameof(Max), typeof(int), typeof(Toaster),
        new FrameworkPropertyMetadata(3, (d, _) => ((Toaster)d).OnMaxChanged()), value => value is int max && max >= 1);

    /// <summary>叠成一摞，默认 false（平铺）。</summary>
    public static readonly DependencyProperty StackProperty = DependencyProperty.Register(nameof(Stack), typeof(bool), typeof(Toaster),
        new FrameworkPropertyMetadata(false, (d, _) => ((Toaster)d).Sync()));

    /// <summary>自动关闭的通知底部显示剩余时间，默认 false。</summary>
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(bool), typeof(Toaster),
        new FrameworkPropertyMetadata(false, (d, _) => ((Toaster)d).Sync()));

    readonly Region region;
    readonly Dictionary<string, ToastItem> items = [];
    ToasterController? subscribed;
    Window? window;
    bool hovering;
    bool focused;
    bool windowActive = true;
    IInputElement? outsideFocus;

    /// <summary>新建：区域停在一角，空白处不挡点击。</summary>
    public Toaster()
    {
        region = new Region(this);
        AddVisualChild(region);
        AddLogicalChild(region);
        region.MouseEnter += (_, _) => SetHover(true);
        region.MouseLeave += (_, _) => SetHover(false);
        region.IsKeyboardFocusWithinChanged += (_, _) => SetFocus(region.IsKeyboardFocusWithin);
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    /// <summary>通知控制器。</summary>
    public ToasterController? Controller { get => (ToasterController?)GetValue(ControllerProperty); set => SetValue(ControllerProperty, value); }

    /// <summary>显示位置。</summary>
    public ToastPlacement Placement { get => (ToastPlacement)GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>最多同时显示几条。</summary>
    public int Max { get => (int)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>叠成一摞。</summary>
    public bool Stack { get => (bool)GetValue(StackProperty); set => SetValue(StackProperty, value); }

    /// <summary>显示剩余时间。</summary>
    public bool Progress { get => (bool)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <summary>区域（测试用）。</summary>
    internal FrameworkElement RegionPart => region;

    /// <summary>显示着的通知，旧的在前（测试用）。</summary>
    internal IReadOnlyList<ToastItem> Items => [.. region.Children.OfType<ToastItem>()];

    /// <summary>叠放展开着（悬停或焦点在里面）。</summary>
    internal bool Expanded => hovering || focused;

    internal ToasterController? Subscribed => subscribed;

    internal double Em => TextElement.GetFontSize(this);

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => region;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new object[] { region }.GetEnumerator();

    void Attach()
    {
        window = Window.GetWindow(this);
        if (window is not null)
        {
            window.PreviewKeyDown += OnWindowKeyDown;
            window.Activated += OnActivated;
            window.Deactivated += OnDeactivated;
        }
        Subscribe(Controller);
    }

    void Detach()
    {
        if (window is not null)
        {
            window.PreviewKeyDown -= OnWindowKeyDown;
            window.Activated -= OnActivated;
            window.Deactivated -= OnDeactivated;
        }
        window = null;
        Subscribe(null);
    }

    void OnControllerChanged()
    {
        if (IsLoaded) Subscribe(Controller);
    }

    void OnMaxChanged()
    {
        if (subscribed is not null) subscribed.Limit = Max;
        Sync();
    }

    void OnActivated(object? sender, EventArgs e)
    {
        windowActive = true;
        UpdatePause();
    }

    void OnDeactivated(object? sender, EventArgs e)
    {
        windowActive = false;
        UpdatePause();
    }

    /// <summary>换控制器：只给显示出来的计时；离开界面时恢复不限条数、继续计时。</summary>
    void Subscribe(ToasterController? controller)
    {
        if (subscribed == controller) return;
        if (subscribed is not null)
        {
            subscribed.Changed -= OnChanged;
            subscribed.Limit = int.MaxValue;
            subscribed.Resume();
        }
        subscribed = controller;
        foreach (var item in items.Values) region.Children.Remove(item);
        items.Clear();
        if (controller is null) return;
        controller.Changed += OnChanged;
        controller.Limit = Max;
        Sync();
        UpdatePause();
    }

    void OnChanged(object? sender, EventArgs e) => Sync();

    /// <summary>
    /// 按控制器排出显示的通知（最新的 Max 条）：新的进场，变了的更新，不在了的拿掉。挤到 Max 以外又被关掉的没有显示，等不到离场结束的那次移除，这里直接移除。
    /// </summary>
    void Sync()
    {
        if (subscribed is not { } controller) return;
        var toasts = controller.Toasts;
        var visible = toasts.Skip(Math.Max(0, toasts.Count - Max)).ToList();
        foreach (var hidden in toasts.Take(toasts.Count - visible.Count).Where(toast => toast.Closing).ToList())
            Dispatcher.BeginInvoke(() => controller.Remove(hidden.Id));
        foreach (var id in items.Keys.Except(visible.Select(toast => toast.Id)).ToList())
        {
            region.Children.Remove(items[id]);
            items.Remove(id);
        }
        for (var index = 0; index < visible.Count; index++)
        {
            var toast = visible[index];
            if (!items.TryGetValue(toast.Id, out var item))
            {
                item = new ToastItem(this, toast.Id);
                items[toast.Id] = item;
            }
            var at = region.Children.IndexOf(item);
            if (at != index)
            {
                if (at >= 0) region.Children.RemoveAt(at);
                region.Children.Insert(index, item);
            }
            item.Update(toast);
        }
        UpdatePause();
        region.InvalidateMeasure();
    }

    void SetHover(bool value)
    {
        if (hovering == value) return;
        hovering = value;
        UpdatePause();
        region.InvalidateMeasure();
    }

    void SetFocus(bool value)
    {
        if (focused == value) return;
        focused = value;
        UpdatePause();
        region.InvalidateMeasure();
    }

    /// <summary>悬停、焦点在区域里、窗口不在前台时暂停计时。</summary>
    void UpdatePause()
    {
        if (subscribed is null) return;
        if (hovering || focused || !windowActive) subscribed.Pause();
        else subscribed.Resume();
        foreach (var item in items.Values) item.SyncProgress();
    }

    /// <summary>F8 跳到通知区域（有通知时；输入框里也能用），记下原来的焦点，最后一条关掉后还回去。</summary>
    void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F8 || items.Count == 0) return;
        if (region.IsKeyboardFocusWithin) return;
        outsideFocus = Keyboard.FocusedElement;
        Keyboard.Focus(region);
        e.Handled = true;
    }

    /// <summary>关掉一条：焦点在它里面时交给相邻的通知，没有时回到进入区域之前的位置。</summary>
    internal void Close(ToastItem item)
    {
        if (item.IsKeyboardFocusWithin)
        {
            var list = Items.Where(other => other == item || !other.Closing).ToList();
            var at = list.IndexOf(item);
            var neighbor = list.ElementAtOrDefault(at + 1) ?? list.ElementAtOrDefault(at - 1);
            if (neighbor is not null && neighbor != item) neighbor.FocusFirst();
            else if (outsideFocus is UIElement { IsVisible: true } element) Keyboard.Focus(element);
            else Keyboard.Focus(region);
        }
        subscribed?.Dismiss(item.Id);
    }

    /// <summary>离场结束：从控制器里移除。</summary>
    internal void Removed(ToastItem item) => subscribed?.Remove(item.Id);

    double Space(string key) => this.Resource<double>(key) * Em;

    /// <summary>区域宽 min(26em, 可用宽)，四周 lg 的边。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Min(26 * Em, double.IsInfinity(availableSize.Width) ? 26 * Em : availableSize.Width);
        region.Inset = Space(ThemeKeys.Space.Lg);
        region.Measure(new Size(width, availableSize.Height));
        return new Size(double.IsInfinity(availableSize.Width) ? region.DesiredSize.Width : 0, double.IsInfinity(availableSize.Height) ? region.DesiredSize.Height : 0);
    }

    /// <summary>区域停在 Placement 的那一角（从右到左排版时左右对调，由平台镜像）。</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = region.DesiredSize;
        var x = Placement.Inline switch
        {
            < 0 => 0,
            0 => (finalSize.Width - size.Width) / 2,
            _ => finalSize.Width - size.Width,
        };
        var y = Placement.FromTop ? 0 : finalSize.Height - size.Height;
        region.Arrange(new Rect(x, y, size.Width, size.Height));
        return finalSize;
    }

    /// <summary>
    /// 区域：带名称的一组，能聚焦（F8 落在这里）。通知排成一列（在顶部时最新的在最上面）；叠放且没展开时，最新的一条在原位，
    /// 更早的叠在它后面（每深一层错开 0.75em、缩小 5%，第三层之后隐藏）。
    /// </summary>
    sealed class Region : Panel
    {
        readonly Toaster owner;

        public Region(Toaster owner)
        {
            this.owner = owner;
            Focusable = true;
            FocusVisualStyle = null;
        }

        public double Inset { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var inner = new Size(Math.Max(0, availableSize.Width - 2 * Inset), double.PositiveInfinity);
            var height = 0d;
            var stacked = owner.Stack && !owner.Expanded;
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(inner);
                height += child.DesiredSize.Height;
            }
            // 叠放时高度是最新的那条（更早的叠在它后面，不占高度）
            if (stacked) height = InternalChildren.OfType<ToastItem>().LastOrDefault(item => !item.Closing)?.DesiredSize.Height ?? height;
            return new Size(availableSize.Width, InternalChildren.Count == 0 ? 0 : height + 2 * Inset);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = Math.Max(0, finalSize.Width - 2 * Inset);
            var fromTop = owner.Placement.FromTop;
            var stacked = owner.Stack && !owner.Expanded;
            var list = InternalChildren.OfType<ToastItem>().ToList();
            // 深度只数还在显示的：正在离场的不占一层
            var depth = list.Count(item => !item.Closing);
            var depths = list.Select(item => item.Closing ? depth : --depth).ToList();
            var front = list.LastOrDefault(item => !item.Closing);
            // 平铺：在底部时从下往上排，最新的贴着底边；在顶部时反过来
            var y = fromTop ? Inset : finalSize.Height - Inset;
            for (var index = list.Count - 1; index >= 0; index--)
            {
                var item = list[index];
                var height = item.DesiredSize.Height;
                double top;
                if (stacked && item != front && front is not null) top = fromTop ? Inset : finalSize.Height - Inset - height;
                else if (fromTop)
                {
                    top = y;
                    y += height;
                }
                else
                {
                    y -= height;
                    top = y;
                }
                item.Arrange(new Rect(Inset, top, width, height));
                item.Stack(stacked ? depths[index] : null, fromTop);
            }
            return finalSize;
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new RegionPeer(this, owner);
    }

    /// <summary>区域对读屏是一组（TS 的 section aria-label 是 landmark；UIA 在 WPF 里没有 landmark），名称默认“通知”。</summary>
    sealed class RegionPeer(FrameworkElement region, Toaster owner) : FrameworkElementAutomationPeer(region)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => "ToasterRegion";

        protected override string GetLocalizedControlTypeCore() => "region";

        protected override string GetNameCore() => AutomationProperties.GetName(owner) is { Length: > 0 } name ? name : Provider.GetLang(owner).ToasterLabel;
    }
}

/// <summary>
/// 显示区域里的一条通知（vue-tsx-pro 的 ToastItem）：Alert（surface、lg 阴影）加上下 xs 的间距；进场淡入并从所在的边滑进来，
/// 离场淡出、高度收起（间距一起收掉）后从控制器里移除。焦点在里面时 Esc 关闭；横向拖动超过宽度的 35% 关闭，否则弹回。
/// </summary>
public sealed class ToastItem : FrameworkElement
{
    /// <summary>展开的比例：离场时收到 0，下面的通知补上来。</summary>
    static readonly DependencyProperty OpennessProperty = DependencyProperty.Register(nameof(Openness), typeof(double), typeof(ToastItem),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>进场的位移、横向滑动的位移、叠放的位移与缩放。</summary>
    static readonly DependencyProperty EnterOffsetProperty = Transforming(nameof(EnterOffset), 0);

    static readonly DependencyProperty SwipeProperty = Transforming(nameof(Swipe), 0);

    static readonly DependencyProperty StackOffsetProperty = Transforming(nameof(StackOffset), 0);

    static readonly DependencyProperty StackScaleProperty = Transforming(nameof(StackScale), 1);

    readonly Toaster owner;
    readonly Alert alert = new() { Variant = AlertVariant.Surface };
    readonly Border progress = new() { IsHitTestVisible = false, CornerRadius = new CornerRadius(1), SnapsToDevicePixels = true, UseLayoutRounding = true };
    readonly ScaleTransform scale = new();
    readonly TranslateTransform translate = new();
    ToastRecord? toast;
    bool entered;
    bool removing;
    bool closeShown;
    Point? pressedAt;
    bool swiping;
    (int? Depth, bool FromTop)? stackTarget;
    double progressStart;
    long progressStamp;
    int progressCountdown = -1;
    bool ticking;

    internal ToastItem(Toaster owner, string id)
    {
        this.owner = owner;
        Id = id;
        alert.SetResourceReference(EffectProperty, ThemeKeys.Shadow.Lg);
        progress.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.TextMuted);
        progress.Opacity = 0.5;
        foreach (var child in new UIElement[] { alert, progress })
        {
            AddVisualChild(child);
            AddLogicalChild(child);
        }
        RenderTransform = new TransformGroup { Children = { scale, translate } };
        RenderTransformOrigin = new Point(0.5, 0.5);
        Opacity = 0;
        Unloaded += (_, _) => StopTicking();
    }

    static DependencyProperty Transforming(string name, double initial) => DependencyProperty.Register(name, typeof(double), typeof(ToastItem),
        new FrameworkPropertyMetadata(initial, (d, _) => ((ToastItem)d).ApplyTransform()));

    /// <summary>通知的编号。</summary>
    public string Id { get; }

    /// <summary>里面的 Alert（测试用）。</summary>
    internal Alert Alert => alert;

    /// <summary>剩余时间条（测试用）。</summary>
    internal Border ProgressBar => progress;

    /// <summary>正在离场。</summary>
    internal bool Closing => toast?.Closing == true;

    double Openness => (double)GetValue(OpennessProperty);

    double EnterOffset => (double)GetValue(EnterOffsetProperty);

    double Swipe => (double)GetValue(SwipeProperty);

    double StackOffset => (double)GetValue(StackOffsetProperty);

    double StackScale => (double)GetValue(StackScaleProperty);

    double Em => owner.Em;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 2;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index == 0 ? alert : progress;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new object[] { alert, progress }.GetEnumerator();

    TimeSpan Duration(string key) => Motion.Duration(this, key);

    IEasingFunction Easing(string key) => this.Resource<IEasingFunction>(key);

    /// <summary>基础值写成终点，动画只是从当前位置过去（结束后撤掉）；时长为 0 时直接到位。</summary>
    void Animate(DependencyProperty property, double to, TimeSpan duration, IEasingFunction? easing)
    {
        var from = (double)GetValue(property);
        BeginAnimation(property, null);
        SetValue(property, to);
        if (duration > TimeSpan.Zero && Math.Abs(from - to) > 0.001)
            BeginAnimation(property, new DoubleAnimation(from, to, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
    }

    /// <summary>按通知的内容更新 Alert；第一次进场，关闭了就离场。</summary>
    internal void Update(ToastRecord record)
    {
        toast = record;
        var options = record.Options;
        alert.Tone = record.Tone;
        alert.Title = options.Title;
        alert.Description = options.Description;
        alert.Action = options.Action;
        alert.IsLoading = record.Loading;
        if (options.ShowIcon == false) alert.Icon = null;
        else if (options.Icon is { } icon) alert.Icon = icon;
        else alert.ClearValue(Alert.IconProperty);
        // 重要的通知打断播报；其余礼貌地念
        alert.Live = record.Important ? AlertLive.Assertive : AlertLive.Polite;
        SyncClose(record.Closable && !record.Closing);
        SyncProgress();
        if (!entered) Enter();
        if (record.Closing) Leave();
    }

    /// <summary>能关闭时 Alert 有关闭按钮：Alert 订阅了 CloseRequested 才显示它，不能关闭时退订。</summary>
    void SyncClose(bool closable)
    {
        if (closable == closeShown) return;
        closeShown = closable;
        if (closable) alert.CloseRequested += OnCloseRequested;
        else alert.CloseRequested -= OnCloseRequested;
    }

    void OnCloseRequested(object? sender, EventArgs e) => Dismiss();

    /// <summary>进场：淡入并从所在的边滑进来（md、enter）。</summary>
    void Enter()
    {
        entered = true;
        SetValue(EnterOffsetProperty, (owner.Placement.FromTop ? -0.75 : 0.75) * Em);
        Dispatcher.BeginInvoke(() =>
        {
            var duration = Duration(ThemeKeys.Duration.Md);
            var easing = Easing(ThemeKeys.Easing.Enter);
            Animate(OpacityProperty, 1, duration, easing);
            Animate(EnterOffsetProperty, 0, duration, easing);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>离场：淡出（sm、exit），高度收起（md），结束后从控制器里移除。</summary>
    async void Leave()
    {
        if (removing) return;
        removing = true;
        Animate(OpacityProperty, 0, Duration(ThemeKeys.Duration.Sm), Easing(ThemeKeys.Easing.Exit));
        var collapse = Duration(ThemeKeys.Duration.Md);
        Animate(OpennessProperty, 0, collapse, Easing(ThemeKeys.Easing.Standard));
        await Task.Delay(collapse);
        owner.Removed(this);
    }

    /// <summary>关闭这一条（关闭按钮、Esc、滑走）。</summary>
    void Dismiss()
    {
        if (toast is not { Closable: true, Closing: false }) return;
        owner.Close(this);
    }

    /// <summary>焦点交给这一条：第一个能聚焦的（操作、关闭按钮）。</summary>
    internal void FocusFirst() => alert.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));

    /// <summary>叠放：按深度错开、缩小，第三层之后隐藏，深的在后面（目标变了才过渡）。</summary>
    internal void Stack(int? depth, bool fromTop)
    {
        if (stackTarget == (depth, fromTop)) return;
        stackTarget = (depth, fromTop);
        Panel.SetZIndex(this, depth is { } d ? 100 - d : 0);
        var duration = Duration(ThemeKeys.Duration.Md);
        var easing = Easing(ThemeKeys.Easing.Standard);
        Animate(StackOffsetProperty, depth is { } level ? (fromTop ? 0.75 : -0.75) * Em * level : 0, duration, easing);
        Animate(StackScaleProperty, depth is { } scaled ? 1 - scaled * 0.05 : 1, duration, easing);
        Visibility = depth is > 2 ? Visibility.Hidden : Visibility.Visible;
    }

    void ApplyTransform()
    {
        scale.ScaleX = scale.ScaleY = StackScale;
        translate.X = Swipe;
        translate.Y = EnterOffset + StackOffset;
    }

    double Gap => this.Resource<double>(ThemeKeys.Space.Xs) * Em;

    /// <summary>高度 =（Alert + 上下 xs）× 展开的比例；收起时裁掉。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        alert.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        progress.Measure(availableSize);
        ClipToBounds = Openness < 1;
        return new Size(alert.DesiredSize.Width, (alert.DesiredSize.Height + 2 * Gap) * Openness);
    }

    /// <summary>Alert 在上下 xs 之间；剩余时间条贴着通知底边，左右离 md、离底 xs 加描边。</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var gap = Gap;
        alert.Arrange(new Rect(0, gap, finalSize.Width, alert.DesiredSize.Height));
        var inset = this.Resource<double>(ThemeKeys.Space.Md) * Em;
        var height = Math.Max(1, this.Resource<double>(ThemeKeys.BorderWidth.Sm));
        progress.Arrange(new Rect(inset, gap + alert.DesiredSize.Height - height - this.Resource<double>(ThemeKeys.BorderWidth.Xs), Math.Max(0, finalSize.Width - 2 * inset), height));
        return finalSize;
    }

    /// <summary>剩余时间条：自动关闭、开了 Progress 才显示；每一轮计时从满开始，暂停时停住。</summary>
    internal void SyncProgress()
    {
        var countdown = toast?.Countdown;
        progress.Visibility = owner.Progress && countdown is not null ? Visibility.Visible : Visibility.Collapsed;
        if (countdown is not { } current)
        {
            StopTicking();
            return;
        }
        if (current.Id != progressCountdown)
        {
            progressCountdown = current.Id;
            progressStart = 1;
            progressStamp = TimeProvider.System.GetTimestamp();
        }
        if (owner.Subscribed?.Paused == true)
        {
            if (progressStamp != 0) progressStart = Fraction(current);
            progressStamp = 0;
        }
        else if (progressStamp == 0) progressStamp = TimeProvider.System.GetTimestamp();
        Paint();
        if (progressStamp != 0 && progress.Visibility == Visibility.Visible) StartTicking();
        else StopTicking();
    }

    double Fraction(ToastCountdown countdown) =>
        progressStamp == 0 ? progressStart : Math.Max(0, progressStart - TimeProvider.System.GetElapsedTime(progressStamp) / countdown.Duration);

    void Paint()
    {
        if (toast?.Countdown is not { } countdown) return;
        progress.RenderTransformOrigin = new Point(FlowDirection == FlowDirection.RightToLeft ? 1 : 0, 0.5);
        progress.RenderTransform = new ScaleTransform(Fraction(countdown), 1);
    }

    void StartTicking()
    {
        if (ticking) return;
        ticking = true;
        CompositionTarget.Rendering += OnFrame;
    }

    void StopTicking()
    {
        if (!ticking) return;
        ticking = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    void OnFrame(object? sender, EventArgs e)
    {
        Paint();
        if (toast?.Countdown is not { } countdown || Fraction(countdown) <= 0) StopTicking();
    }

    /// <summary>焦点在通知里时 Esc 关闭它（不能关闭的不处理）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || toast is not { Closable: true, Closing: false }) return;
        Dismiss();
        e.Handled = true;
    }

    /// <summary>拖动在通知上（按钮、输入框上的不算）。</summary>
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        if (toast is not { Closable: true, Closing: false }) return;
        for (var node = e.OriginalSource as DependencyObject; node is not null && node != this; node = VisualTreeHelper.GetParent(node))
            if (node is ButtonBase or TextBoxBase) return;
        pressedAt = e.GetPosition(this);
    }

    /// <summary>横向拖动跟手（位移、透明度随距离）；挪开 4 以内不算。</summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (pressedAt is not { } from || e.LeftButton != MouseButtonState.Pressed) return;
        var dx = e.GetPosition(this).X - from.X;
        if (!swiping)
        {
            if (Math.Abs(dx) < 4) return;
            swiping = true;
            CaptureMouse();
        }
        Animate(SwipeProperty, dx, TimeSpan.Zero, null);
        Animate(OpacityProperty, Math.Max(0, 1 - Math.Abs(dx) / Math.Max(1, ActualWidth)), TimeSpan.Zero, null);
    }

    /// <summary>松开：超过宽度的 35% 就沿滑动方向滑出再关闭，不够就弹回。</summary>
    protected override async void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        pressedAt = null;
        if (!swiping) return;
        swiping = false;
        ReleaseMouseCapture();
        var width = Math.Max(1, ActualWidth);
        if (Math.Abs(Swipe) <= width * 0.35)
        {
            var back = Duration(ThemeKeys.Duration.Md);
            Animate(SwipeProperty, 0, back, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 });
            Animate(OpacityProperty, 1, back, Easing(ThemeKeys.Easing.Standard));
            return;
        }
        var duration = Duration(ThemeKeys.Duration.Sm);
        Animate(SwipeProperty, Math.Sign(Swipe) * width, duration, Easing(ThemeKeys.Easing.Exit));
        Animate(OpacityProperty, 0, duration, Easing(ThemeKeys.Easing.Exit));
        await Task.Delay(duration);
        Dismiss();
    }
}
