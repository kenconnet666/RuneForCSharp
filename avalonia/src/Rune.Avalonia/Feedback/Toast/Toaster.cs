using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 通知的显示区域（vue-tsx-pro 的 Toaster）：放一个，交给它 <see cref="ToasterController"/>，在任何地方调控制器显示通知。
/// <code>
/// &lt;Grid&gt;…页面…&lt;rune:Toaster Controller="{x:Static local:App.Toaster}" Placement="bottom-end" /&gt;&lt;/Grid&gt;
/// </code>
/// 桌面上没有 position: fixed：放在盖住整个窗口的 Grid（或 Panel）里，通知停在它的一角（<see cref="Placement"/>，四周 lg 的边，宽 26em），
/// 空白处不挡点击。每条通知就是 Alert（surface 外观、lg 阴影），图标、标题、正文、操作、关闭按钮都复用它。
/// <list type="bullet">
/// <item>计时：指针在通知上、焦点在区域里、窗口不在前台时暂停。<see cref="Max"/>（默认 3）条以外的先不显示，前面的关闭后补上。</item>
/// <item>无障碍：区域是带名称的 landmark（默认“通知”），F8 跳到通知区域，焦点在一条通知里时 Esc 关闭它；新通知读屏礼貌地念，重要的立即念。
/// 关闭一条后焦点交给相邻的通知，没有时回到按 F8 之前的位置。</item>
/// <item>动画：进场淡入并从所在的边滑进来，离场淡出、高度收起后从控制器里移除；横向拖动超过宽度的 35% 就关闭，否则弹回。</item>
/// <item><see cref="Stack"/>：像 Sonner 一样叠成一摞，悬停或聚焦时展开；<see cref="Progress"/>：自动关闭的通知底部显示剩余时间，暂停时一起停。</item>
/// </list>
/// </summary>
public class Toaster : Control
{
    /// <summary>通知控制器。</summary>
    public static readonly StyledProperty<ToasterController?> ControllerProperty = AvaloniaProperty.Register<Toaster, ToasterController?>(nameof(Controller));

    /// <summary>显示位置，默认 bottom-end。</summary>
    public static readonly StyledProperty<ToastPlacement> PlacementProperty = AvaloniaProperty.Register<Toaster, ToastPlacement>(nameof(Placement), ToastPlacement.BottomEnd);

    /// <summary>最多同时显示几条，默认 3。</summary>
    public static readonly StyledProperty<int> MaxProperty = AvaloniaProperty.Register<Toaster, int>(nameof(Max), 3, validate: value => value >= 1);

    /// <summary>叠成一摞，默认 false（平铺）。</summary>
    public static readonly StyledProperty<bool> StackProperty = AvaloniaProperty.Register<Toaster, bool>(nameof(Stack));

    /// <summary>自动关闭的通知底部显示剩余时间，默认 false。</summary>
    public static readonly StyledProperty<bool> ProgressProperty = AvaloniaProperty.Register<Toaster, bool>(nameof(Progress));

    /// <summary>字号：宽度、间距、位移按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = global::Avalonia.Controls.Documents.TextElement.FontSizeProperty.AddOwner<Toaster>();

    readonly Region region;
    readonly Dictionary<string, ToastItem> items = [];
    ToasterController? subscribed;
    TopLevel? top;
    bool hovering;
    bool focused;
    bool windowActive = true;
    IInputElement? outsideFocus;

    /// <summary>新建：区域停在一角，空白处不挡点击。</summary>
    public Toaster()
    {
        region = new Region(this);
        LogicalChildren.Add(region);
        VisualChildren.Add(region);
        region.PointerEntered += (_, _) => SetHover(true);
        region.PointerExited += (_, _) => SetHover(false);
        region.GotFocus += (_, _) => SetFocus(true);
        region.LostFocus += (_, _) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => SetFocus(region.IsKeyboardFocusWithin));
    }

    /// <summary>通知控制器。</summary>
    public ToasterController? Controller { get => GetValue(ControllerProperty); set => SetValue(ControllerProperty, value); }

    /// <summary>显示位置。</summary>
    public ToastPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>最多同时显示几条。</summary>
    public int Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>叠成一摞。</summary>
    public bool Stack { get => GetValue(StackProperty); set => SetValue(StackProperty, value); }

    /// <summary>显示剩余时间。</summary>
    public bool Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>区域（测试用）。</summary>
    internal Control RegionPart => region;

    /// <summary>显示着的通知，旧的在前（测试用）。</summary>
    internal IReadOnlyList<ToastItem> Items => [.. region.Children.OfType<ToastItem>()];

    /// <summary>叠放展开着（悬停或焦点在里面）。</summary>
    internal bool Expanded => hovering || focused;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        top = TopLevel.GetTopLevel(this);
        top?.AddHandler(KeyDownEvent, OnTopKeyDown, RoutingStrategies.Tunnel);
        if (top is WindowBase window)
        {
            window.Activated += OnActivated;
            window.Deactivated += OnDeactivated;
        }
        Subscribe(Controller);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        top?.RemoveHandler(KeyDownEvent, OnTopKeyDown);
        if (top is WindowBase window)
        {
            window.Activated -= OnActivated;
            window.Deactivated -= OnDeactivated;
        }
        top = null;
        Subscribe(null);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ControllerProperty)
        {
            if (VisualRoot is not null) Subscribe(Controller);
        }
        else if (change.Property == MaxProperty)
        {
            if (subscribed is not null) subscribed.Limit = Max;
            Sync();
        }
        else if (change.Property == PlacementProperty || change.Property == StackProperty || change.Property == ProgressProperty) Sync();
        else if (change.Property == FontSizeProperty) InvalidateMeasure();
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
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => controller.Remove(hidden.Id));
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
        region.InvalidateArrange();
    }

    void SetFocus(bool value)
    {
        if (focused == value) return;
        focused = value;
        UpdatePause();
        region.InvalidateArrange();
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
    void OnTopKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F8 || items.Count == 0) return;
        var current = top?.FocusManager?.GetFocusedElement();
        if (current is Visual visual && visual.FindAncestorOfType<Toaster>() == this) return;
        outsideFocus = current;
        region.Focus(NavigationMethod.Tab);
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
            else if (outsideFocus is InputElement element && TopLevel.GetTopLevel(element) is not null) element.Focus(NavigationMethod.Tab);
            else region.Focus(NavigationMethod.Tab);
        }
        subscribed?.Dismiss(item.Id);
    }

    /// <summary>离场结束：从控制器里移除。投递到之后再做：关闭动画时离场是同步结束的，不能在 Sync 排列表的中途改列表。</summary>
    internal void Removed(ToastItem item) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => subscribed?.Remove(item.Id));

    internal ToasterController? Subscribed => subscribed;

    double Em => FontSize;

    double Space(string key) => this.Resource<double>(key) * Em;

    /// <summary>区域宽 min(26em, 可用宽)，四周 lg 的边。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Min(26 * Em, double.IsInfinity(availableSize.Width) ? 26 * Em : availableSize.Width);
        region.Padding = new Thickness(Space(ThemeKeys.Space.Lg));
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
    /// 区域：带名称的 landmark，能聚焦（F8 落在这里）。通知排成一列（在顶部时最新的在最上面）；叠放且没展开时，最新的一条在原位，
    /// 更早的叠在它后面（每深一层错开 0.75em、缩小 5%，第三层之后隐藏）。
    /// </summary>
    sealed class Region : Panel
    {
        readonly Toaster owner;

        public Region(Toaster owner)
        {
            this.owner = owner;
            Focusable = true;
            AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Region);
        }

        public Thickness Padding { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var inner = new Size(Math.Max(0, availableSize.Width - Padding.Left - Padding.Right), double.PositiveInfinity);
            var height = 0d;
            var stacked = owner.Stack && !owner.Expanded;
            foreach (var child in Children)
            {
                child.Measure(inner);
                height = stacked ? child.DesiredSize.Height : height + child.DesiredSize.Height;
            }
            // 叠放时高度是最新的那条（更早的叠在它后面，不占高度）
            if (stacked) height = Children.OfType<ToastItem>().LastOrDefault(item => !item.Closing)?.DesiredSize.Height ?? height;
            return new Size(availableSize.Width, Children.Count == 0 ? 0 : height + Padding.Top + Padding.Bottom);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = Math.Max(0, finalSize.Width - Padding.Left - Padding.Right);
            var fromTop = owner.Placement.FromTop;
            var stacked = owner.Stack && !owner.Expanded;
            var list = Children.OfType<ToastItem>().ToList();
            // 深度只数还在显示的：正在离场的不占一层
            var depth = list.Count(item => !item.Closing);
            var depths = list.Select(item => item.Closing ? depth : --depth).ToList();
            var front = list.LastOrDefault(item => !item.Closing);
            var frontHeight = front?.DesiredSize.Height ?? 0;
            // 平铺：在底部时从下往上排，最新的贴着底边；在顶部时反过来
            var y = fromTop ? Padding.Top : finalSize.Height - Padding.Bottom;
            for (var index = list.Count - 1; index >= 0; index--)
            {
                var item = list[index];
                var height = item.DesiredSize.Height;
                double top;
                if (stacked && item != front && front is not null)
                    top = fromTop ? Padding.Top : finalSize.Height - Padding.Bottom - height;
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
                item.Place(new Rect(Padding.Left, top, width, height), stacked ? depths[index] : null, fromTop, frontHeight);
            }
            return finalSize;
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new RegionPeer(this, owner);
    }

    /// <summary>区域对读屏是一组 landmark（TS 的 section aria-label），名称默认“通知”。</summary>
    sealed class RegionPeer(Control region, Toaster owner) : ControlAutomationPeer(region)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => AutomationProperties.GetName(owner) is { Length: > 0 } name ? name : Provider.GetLang(owner).ToasterLabel;
    }
}

/// <summary>
/// 显示区域里的一条通知（vue-tsx-pro 的 ToastItem）：Alert（surface、lg 阴影）加上下 xs 的间距；进场淡入并从所在的边滑进来，
/// 离场淡出、高度收起（间距一起收掉）后从控制器里移除。焦点在里面时 Esc 关闭；横向拖动超过宽度的 35% 关闭，否则弹回。
/// </summary>
public sealed class ToastItem : Control
{
    /// <summary>展开的比例：离场时收到 0，下面的通知补上来。</summary>
    internal static readonly StyledProperty<double> OpennessProperty = AvaloniaProperty.Register<ToastItem, double>(nameof(Openness), 1);

    /// <summary>进场的位移（从所在的边滑进来）。</summary>
    internal static readonly StyledProperty<double> EnterOffsetProperty = AvaloniaProperty.Register<ToastItem, double>(nameof(EnterOffset));

    /// <summary>横向滑动的位移。</summary>
    internal static readonly StyledProperty<double> SwipeProperty = AvaloniaProperty.Register<ToastItem, double>(nameof(Swipe));

    /// <summary>叠放的位移。</summary>
    internal static readonly StyledProperty<double> StackOffsetProperty = AvaloniaProperty.Register<ToastItem, double>(nameof(StackOffset));

    /// <summary>叠放的缩放。</summary>
    internal static readonly StyledProperty<double> StackScaleProperty = AvaloniaProperty.Register<ToastItem, double>(nameof(StackScale), 1);

    readonly Toaster owner;
    readonly Alert alert = new() { Variant = AlertVariant.Surface };
    readonly Border progress = new() { Name = ToasterParts.Progress, IsHitTestVisible = false };
    ToastRecord? toast;
    bool entered;
    bool removing;
    Point? pressedAt;
    bool swiping;
    double progressStart;
    long progressStamp;
    int progressCountdown = -1;

    static ToastItem() => AffectsMeasure<ToastItem>(OpennessProperty);

    internal ToastItem(Toaster owner, string id)
    {
        this.owner = owner;
        Id = id;
        LogicalChildren.AddRange([alert, progress]);
        VisualChildren.AddRange([alert, progress]);
        Opacity = 0;
        // 拖动在通知上（按钮、链接、输入框上的不算）
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>通知的编号。</summary>
    public string Id { get; }

    /// <summary>里面的 Alert（测试用）。</summary>
    internal Alert Alert => alert;

    /// <summary>剩余时间条（测试用）。</summary>
    internal Border ProgressBar => progress;

    /// <summary>正在离场。</summary>
    internal bool Closing => toast?.Closing == true;

    double Openness { get => GetValue(OpennessProperty); set => SetValue(OpennessProperty, value); }

    double EnterOffset { get => GetValue(EnterOffsetProperty); set => SetValue(EnterOffsetProperty, value); }

    double Swipe { get => GetValue(SwipeProperty); set => SetValue(SwipeProperty, value); }

    double StackOffset { get => GetValue(StackOffsetProperty); set => SetValue(StackOffsetProperty, value); }

    double StackScale { get => GetValue(StackScaleProperty); set => SetValue(StackScaleProperty, value); }

    double Em => owner.FontSize;

    TimeSpan Duration(string key) => Motion.Duration(this, key);

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

    bool closeShown;

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
        EnterOffset = (owner.Placement.FromTop ? -0.75 : 0.75) * Em;
        var duration = Duration(ThemeKeys.Duration.Md);
        var easing = this.Resource<Easing>(ThemeKeys.Easing.Enter);
        Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = duration, Easing = easing },
            new DoubleTransition { Property = EnterOffsetProperty, Duration = duration, Easing = easing },
            new DoubleTransition { Property = OpennessProperty, Duration = duration, Easing = this.Resource<Easing>(ThemeKeys.Easing.Standard) },
            new DoubleTransition { Property = StackOffsetProperty, Duration = duration, Easing = this.Resource<Easing>(ThemeKeys.Easing.Standard) },
            new DoubleTransition { Property = StackScaleProperty, Duration = duration, Easing = this.Resource<Easing>(ThemeKeys.Easing.Standard) },
        ];
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Opacity = 1;
            EnterOffset = 0;
        }, global::Avalonia.Threading.DispatcherPriority.Render);
    }

    /// <summary>离场：淡出（sm、exit），高度收起（md），结束后从控制器里移除。</summary>
    async void Leave()
    {
        if (removing) return;
        removing = true;
        Opacity = 0;
        Openness = 0;
        await Task.Delay(Duration(ThemeKeys.Duration.Md));
        owner.Removed(this);
    }

    /// <summary>关闭这一条（关闭按钮、Esc、滑走）。</summary>
    void Dismiss()
    {
        if (toast is not { Closable: true, Closing: false }) return;
        owner.Close(this);
    }

    /// <summary>焦点交给这一条：第一个能聚焦的（操作、关闭按钮）。</summary>
    internal void FocusFirst()
    {
        var first = this.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(element => element.Focusable && element.IsEffectivelyVisible && element.IsEffectivelyEnabled);
        first?.Focus(NavigationMethod.Tab);
    }

    /// <summary>排在区域里：叠放时按深度错开、缩小，第三层之后隐藏，深的在后面。</summary>
    internal void Place(Rect rect, int? depth, bool fromTop, double frontHeight)
    {
        Arrange(rect);
        ZIndex = depth is { } d ? 100 - d : 0;
        StackOffset = depth is { } level ? (fromTop ? 0.75 : -0.75) * Em * level : 0;
        StackScale = depth is { } scaled ? 1 - scaled * 0.05 : 1;
        IsVisible = depth is not > 2;
        ApplyTransform();
    }

    void ApplyTransform()
    {
        var builder = new TransformOperations.Builder(2);
        builder.AppendScale(StackScale, StackScale);
        builder.AppendTranslate(Swipe, EnterOffset + StackOffset);
        RenderTransform = builder.Build();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EnterOffsetProperty || change.Property == SwipeProperty || change.Property == StackOffsetProperty || change.Property == StackScaleProperty)
            ApplyTransform();
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
        var full = Math.Max(0, finalSize.Width - 2 * inset);
        progress.Arrange(new Rect(inset, gap + alert.DesiredSize.Height - height - this.Resource<double>(ThemeKeys.BorderWidth.Xs), full, height));
        return finalSize;
    }

    /// <summary>剩余时间条：自动关闭、开了 Progress 才显示；每一轮计时从满开始，暂停时停住。</summary>
    internal void SyncProgress()
    {
        var countdown = toast?.Countdown;
        progress.IsVisible = owner.Progress && countdown is not null;
        if (countdown is not { } current) return;
        if (current.Id != progressCountdown)
        {
            progressCountdown = current.Id;
            progressStart = 1;
            progressStamp = TimeProvider.System.GetTimestamp();
        }
        else if (owner.Subscribed?.Paused == true)
        {
            // 暂停：记下停住时的比例
            progressStart = Fraction(current);
            progressStamp = 0;
        }
        else if (progressStamp == 0) progressStamp = TimeProvider.System.GetTimestamp();
        Tick();
    }

    double Fraction(ToastCountdown countdown) =>
        progressStamp == 0 ? progressStart : Math.Max(0, progressStart - TimeProvider.System.GetElapsedTime(progressStamp) / countdown.Duration);

    /// <summary>每一帧缩短（从起始边往结束边收）；暂停、隐藏、离开界面时不再要帧。</summary>
    void Tick(TimeSpan _ = default)
    {
        if (toast?.Countdown is not { } countdown || !progress.IsVisible || VisualRoot is null) return;
        var fraction = Fraction(countdown);
        progress.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
        progress.RenderTransform = new ScaleTransform(fraction, 1);
        if (progressStamp != 0 && fraction > 0) TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Tick);
    }

    /// <summary>焦点在通知里时 Esc 关闭它（不能关闭的不处理）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || toast is not { Closable: true, Closing: false }) return;
        Dismiss();
        e.Handled = true;
    }

    void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (toast is not { Closable: true, Closing: false } || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().TakeWhile(visual => visual != this).Any(visual => visual is ButtonBase or Button or TextBox)) return;
        pressedAt = e.GetPosition(this);
    }

    /// <summary>横向拖动跟手（位移、透明度随距离）；挪开 4 以内不算。</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (pressedAt is not { } from) return;
        var dx = e.GetPosition(this).X - from.X;
        if (!swiping)
        {
            if (Math.Abs(dx) < 4) return;
            swiping = true;
            e.Pointer.Capture(this);
            Transitions = null;
        }
        Swipe = dx;
        Opacity = Math.Max(0, 1 - Math.Abs(dx) / Math.Max(1, Bounds.Width));
    }

    /// <summary>松开：超过宽度的 35% 就沿滑动方向滑出再关闭，不够就弹回。</summary>
    protected override async void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        pressedAt = null;
        if (!swiping) return;
        swiping = false;
        e.Pointer.Capture(null);
        var width = Math.Max(1, Bounds.Width);
        var outward = Math.Abs(Swipe) > width * 0.35;
        var duration = Duration(outward ? ThemeKeys.Duration.Sm : ThemeKeys.Duration.Md);
        var easing = this.Resource<Easing>(outward ? ThemeKeys.Easing.Exit : ThemeKeys.Easing.Standard);
        Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = duration, Easing = easing },
            new DoubleTransition { Property = SwipeProperty, Duration = duration, Easing = outward ? easing : new BackEaseOut() },
            new DoubleTransition { Property = OpennessProperty, Duration = Duration(ThemeKeys.Duration.Md), Easing = this.Resource<Easing>(ThemeKeys.Easing.Standard) },
        ];
        if (!outward)
        {
            Swipe = 0;
            Opacity = 1;
            return;
        }
        Swipe = Math.Sign(Swipe) * width;
        Opacity = 0;
        await Task.Delay(duration);
        Dismiss();
    }
}

/// <summary>Toaster 的部件名。</summary>
public static class ToasterParts
{
    /// <summary>剩余时间条（TS 的 propsProgress）。</summary>
    public const string Progress = "PART_Progress";
}
