using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 悬停卡片（vue-tsx-pro 的 HoverCard）：指针停在触发器（通常是链接、用户名）上、或用键盘聚焦它时，弹出一张富内容卡片（用户资料、链接预览）。
/// <code>
/// &lt;rune:HoverCard&gt;
///   &lt;rune:HoverCard.Trigger&gt;&lt;rune:Link Href="https://example.com/u/ada"&gt;@ada&lt;/rune:Link&gt;&lt;/rune:HoverCard.Trigger&gt;
///   …资料…
/// &lt;/rune:HoverCard&gt;
/// </code>
/// 开关时机：指针停留 <see cref="Delay"/>（默认 700 毫秒）后打开，离开 <see cref="CloseDelay"/>（默认 300 毫秒）后关闭，中间可以移到卡片上；
/// 所有悬停卡片一组，同一时间只开一个，刚关掉一个时下一个直接打开。键盘聚焦触发器时立即打开，Tab 进卡片不关，焦点离开触发器与卡片时关闭；
/// Esc、按下触发器关闭。打开时焦点不动（卡片只是预览），点外面不关。定位、外观、箭头同 <see cref="Popover"/>（默认在下方居中），最宽 22em。
/// 卡片只是预览：里面的信息要在别处也能拿到（触屏没有悬停）。
/// </summary>
public class HoverCard : Popover
{
    /// <summary>指针停留多久后打开，默认 700 毫秒。</summary>
    public static readonly StyledProperty<TimeSpan> DelayProperty = AvaloniaProperty.Register<HoverCard, TimeSpan>(nameof(Delay), TimeSpan.FromMilliseconds(700));

    /// <summary>指针离开多久后关闭，默认 300 毫秒。</summary>
    public static readonly StyledProperty<TimeSpan> CloseDelayProperty = AvaloniaProperty.Register<HoverCard, TimeSpan>(nameof(CloseDelay), TimeSpan.FromMilliseconds(300));

    /// <summary>不打开（触发器照常显示）。</summary>
    public static readonly StyledProperty<bool> DisabledProperty = AvaloniaProperty.Register<HoverCard, bool>(nameof(Disabled));

    /// <summary>所有悬停卡片一组：同一时间只开一个。</summary>
    static readonly HoverGroup Cards = new();

    readonly HoverIntent intent;

    static HoverCard() => PlacementProperty.OverrideDefaultValue<HoverCard>(FloatingPlacement.Bottom);

    /// <summary>新建：悬停、键盘聚焦打开；点外面不关。</summary>
    public HoverCard()
    {
        intent = new HoverIntent(Cards, () => (Delay, CloseDelay, Disabled), (open, reason) => SetOpen(open, reason));
        PopupHost.IsLightDismissEnabled = false;
        TriggerHost.PointerEntered += (_, _) => intent.PointerEntered();
        TriggerHost.PointerExited += (_, _) => intent.PointerExited();
        TriggerHost.AddHandler(PointerPressedEvent, (_, _) => intent.CloseNow(OpenChangeReason.Trigger), RoutingStrategies.Tunnel, handledEventsToo: true);
        TriggerHost.AddHandler(GotFocusEvent, (_, e) =>
        {
            // 只在键盘聚焦时打开：点击聚焦（按下触发器）时不开
            if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional) intent.KeyboardFocused();
        });
        TriggerHost.AddHandler(LostFocusEvent, (_, _) => CloseWhenFocusLeaves());
        Panel.PointerEntered += (_, _) => intent.PanelEntered();
        Panel.PointerExited += (_, _) => intent.PointerExited();
        Panel.AddHandler(LostFocusEvent, (_, _) => CloseWhenFocusLeaves());
    }

    /// <summary>指针停留多久后打开。</summary>
    public TimeSpan Delay { get => GetValue(DelayProperty); set => SetValue(DelayProperty, value); }

    /// <summary>指针离开多久后关闭。</summary>
    public TimeSpan CloseDelay { get => GetValue(CloseDelayProperty); set => SetValue(CloseDelayProperty, value); }

    /// <summary>不打开。</summary>
    public bool Disabled { get => GetValue(DisabledProperty); set => SetValue(DisabledProperty, value); }

    /// <summary>悬停的时机（测试用）。</summary>
    internal HoverIntent Intent => intent;

    private protected override bool FocusOnOpen => false;

    // 按下触发器已经关了（PointerPressed）；链接照常跳转
    private protected override void OnTriggerClick()
    {
    }

    private protected override void OnTriggerKeyDown(KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !IsOpen) return;
        intent.CloseNow(OpenChangeReason.Escape);
        e.Handled = true;
    }

    private protected override void OnMetrics(double em) => Panel.MaxWidth = 22 * em;

    /// <summary>焦点离开触发器或卡片：等焦点落定，落到两者以外的元素上时关闭（Tab 进卡片不关）。</summary>
    void CloseWhenFocusLeaves() => Dispatcher.UIThread.Post(() =>
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var popupFocused = TopLevel.GetTopLevel(Panel)?.FocusManager?.GetFocusedElement() as Visual;
        bool Inside(Visual? element) => element is not null && (TriggerHost.IsVisualAncestorOf(element) || Panel.IsVisualAncestorOf(element) || element == Panel);
        // 焦点落到了别的元素上才关；没有焦点（弹出层的窗口出现、切到别的程序时窗口失去激活）不算移走
        if (focused is null && popupFocused is null) return;
        if (!Inside(focused) && !Inside(popupFocused)) intent.CloseNow(OpenChangeReason.Focus);
    });

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsOpenProperty) return;
        if (IsOpen) intent.Opened();
        else intent.Closed();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        intent.Dispose();
        SetCurrentValue(IsOpenProperty, false);
    }
}
