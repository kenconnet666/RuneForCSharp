using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 悬停卡片（vue-tsx-pro 的 HoverCard）：指针停在触发器（通常是链接、用户名）上、或用键盘聚焦它时，弹出一张富内容卡片（用户资料、链接预览）。
/// <code>
/// &lt;rune:HoverCard&gt;
///   &lt;rune:HoverCard.Trigger&gt;&lt;rune:Link Href="https://example.com/u/ada"&gt;@ada&lt;/rune:Link&gt;&lt;/rune:HoverCard.Trigger&gt;
///   …资料…
/// &lt;/rune:HoverCard&gt;
/// </code>
/// 开关时机：指针停留 <see cref="Delay"/>（默认 700 毫秒）后打开，离开 <see cref="CloseDelay"/>（默认 300 毫秒）后关闭，中间可以移到卡片上；
/// 所有悬停卡片一组，同一时间只开一个，刚关掉一个时下一个直接打开。键盘聚焦触发器时立即打开，Tab 进卡片不关，焦点移到别处时关闭；
/// Esc、按下触发器关闭。打开时焦点不动（卡片只是预览），点外面不关。定位、外观、箭头同 <see cref="Popover"/>（默认在下方居中），最宽 22em。
/// 卡片只是预览：里面的信息要在别处也能拿到（触屏没有悬停）。
/// </summary>
public class HoverCard : Popover
{
    /// <summary>指针停留多久后打开，默认 700 毫秒。</summary>
    public static readonly DependencyProperty DelayProperty = DependencyProperty.Register(nameof(Delay), typeof(TimeSpan), typeof(HoverCard), new PropertyMetadata(TimeSpan.FromMilliseconds(700)));

    /// <summary>指针离开多久后关闭，默认 300 毫秒。</summary>
    public static readonly DependencyProperty CloseDelayProperty = DependencyProperty.Register(nameof(CloseDelay), typeof(TimeSpan), typeof(HoverCard), new PropertyMetadata(TimeSpan.FromMilliseconds(300)));

    /// <summary>不打开（触发器照常显示）。</summary>
    public static readonly DependencyProperty DisabledProperty = DependencyProperty.Register(nameof(Disabled), typeof(bool), typeof(HoverCard), new PropertyMetadata(false));

    /// <summary>所有悬停卡片一组：同一时间只开一个。</summary>
    static readonly HoverGroup Cards = new();

    readonly HoverIntent intent;

    static HoverCard() => PlacementProperty.OverrideMetadata(typeof(HoverCard), new FrameworkPropertyMetadata(FloatingPlacement.Bottom));

    /// <summary>新建：悬停、键盘聚焦打开；点外面不关。</summary>
    public HoverCard()
    {
        intent = new HoverIntent(Cards, () => (Delay, CloseDelay, Disabled), (open, reason) => SetOpen(open, reason));
        PopupHost.StaysOpen = true;
        TriggerHost.MouseEnter += (_, _) => intent.PointerEntered();
        TriggerHost.MouseLeave += (_, _) => intent.PointerExited();
        TriggerHost.PreviewMouseDown += (_, _) => intent.CloseNow(OpenChangeReason.Trigger);
        TriggerHost.GotKeyboardFocus += (_, _) =>
        {
            // 只在键盘聚焦时打开：点击聚焦（按下触发器）时不开
            if (InputManager.Current.MostRecentInputDevice is KeyboardDevice) intent.KeyboardFocused();
        };
        TriggerHost.LostKeyboardFocus += (_, e) => CloseWhenFocusLeaves(e.NewFocus);
        Panel.MouseEnter += (_, _) => intent.PanelEntered();
        Panel.MouseLeave += (_, _) => intent.PointerExited();
        Panel.LostKeyboardFocus += (_, e) => CloseWhenFocusLeaves(e.NewFocus);
        Unloaded += (_, _) =>
        {
            intent.Dispose();
            SetCurrentValue(IsOpenProperty, false);
        };
    }

    /// <summary>指针停留多久后打开。</summary>
    public TimeSpan Delay { get => (TimeSpan)GetValue(DelayProperty); set => SetValue(DelayProperty, value); }

    /// <summary>指针离开多久后关闭。</summary>
    public TimeSpan CloseDelay { get => (TimeSpan)GetValue(CloseDelayProperty); set => SetValue(CloseDelayProperty, value); }

    /// <summary>不打开。</summary>
    public bool Disabled { get => (bool)GetValue(DisabledProperty); set => SetValue(DisabledProperty, value); }

    /// <summary>悬停的时机（测试用）。</summary>
    internal HoverIntent Intent => intent;

    private protected override bool FocusOnOpen => false;

    // 按下触发器已经关了（PreviewMouseDown）；链接照常跳转
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

    /// <summary>焦点移到了触发器与卡片以外的元素上时关闭（Tab 进卡片不关；没有新焦点时不算移走）。</summary>
    void CloseWhenFocusLeaves(IInputElement? next)
    {
        if (next is not DependencyObject target) return;
        bool Inside(DependencyObject root) => target == root || (target is Visual visual && root is Visual ancestor && ancestor.IsAncestorOf(visual));
        if (!Inside(TriggerHost) && !Inside(Panel)) intent.CloseNow(OpenChangeReason.Focus);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != IsOpenProperty) return;
        if (IsOpen) intent.Opened();
        else intent.Closed();
    }
}
