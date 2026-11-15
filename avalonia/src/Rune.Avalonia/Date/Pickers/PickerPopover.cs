using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 日期、时间选择器共用的打开按钮与弹层（vue-tsx-pro 的 usePickerPopover：DatePicker、DateRangePicker、TimePicker）。
/// 输入框外壳是定位的锚点，后缀里的按钮打开（放进外壳的 Suffix），弹层是平台的 Popup（点外部关闭），里面 Esc 关闭；
/// 关闭时焦点在弹层里就还给按钮；分段里按 Alt + ↓ 打开（同原生 select）。开关状态由选择器自己的 IsOpen 管，这里只报告"想开、想关"。
/// </summary>
sealed class PickerPopover
{
    /// <summary>与外壳的距离（像素）：同 Select。</summary>
    const double Offset = 6;

    readonly SegmentedField owner;
    readonly Popup popup = new() { IsLightDismissEnabled = true, WindowManagerAddShadowHint = false };
    readonly Action<bool, OpenChangeReason> toggle;
    readonly Action shown;
    bool focusWasInside;

    /// <summary>接到选择器上：按钮的点击、弹层的开关与按键、分段里的 Alt + ↓。</summary>
    /// <param name="owner">选择器（输入框外壳就是它，弹层的定位锚点）。</param>
    /// <param name="icon">按钮的图标。</param>
    /// <param name="toggle">想开（true）或想关（false）与原因：写回选择器的 IsOpen 并报告。</param>
    /// <param name="shown">打开、排好版之后：把焦点交给弹层里合适的元素。</param>
    public PickerPopover(SegmentedField owner, IconData icon, Action<bool, OpenChangeReason> toggle, Action shown)
    {
        this.owner = owner;
        this.toggle = toggle;
        this.shown = shown;
        Button = new IconButton { Name = PickerParts.Button, Icon = icon, Radius = Radius.Full };
        Button.Click += (_, e) =>
        {
            toggle(!popup.IsOpen, OpenChangeReason.Trigger);
            e.Handled = true;
        };
        Panel.GotFocus += (_, _) => focusWasInside = true;
        Panel.AddHandler(InputElement.KeyDownEvent, OnPanelKeyDown);
        popup.Child = new Panel { Margin = new Thickness(0, Offset), Children = { Panel } };
        popup.PlacementTarget = owner;
        popup.Opened += (_, _) =>
        {
            focusWasInside = false;
            Dispatcher.UIThread.Post(shown, DispatcherPriority.Loaded);
        };
        popup.Closed += (_, _) =>
        {
            // 平台关了弹出层（点了外面）：写回选择器
            toggle(false, OpenChangeReason.Outside);
            ReturnFocus();
        };
        owner.AddHandler(InputElement.KeyDownEvent, OnOwnerKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>后缀里的打开按钮（选择器把它放进 Suffix）。</summary>
    public IconButton Button { get; }

    /// <summary>弹层的面板（选择器把内容放进去；样式 rune|SegmentedField Border#PART_Popover）。</summary>
    public Border Panel { get; } = new() { Name = PickerParts.Popover, Focusable = true };

    /// <summary>平台的弹出层（选择器把它加进逻辑树，样式才选得中面板）。</summary>
    public Popup Popup => popup;

    /// <summary>选择器的状态变了：开关、位置、按钮的名称与能不能点、弹层的字号。</summary>
    /// <param name="open">开着。</param>
    /// <param name="placement">弹出位置。</param>
    /// <param name="label">按钮与弹层的名称（"选择日期"）。</param>
    /// <param name="editable">能不能打开：不可用、只读时按钮也不可用。</param>
    public void Sync(bool open, FloatingPlacement placement, string label, bool editable)
    {
        AutomationProperties.SetName(Button, label);
        AutomationProperties.SetName(Panel, label);
        Button.IsEnabled = editable;
        Button.SetValue(Popover.TriggerExpandedProperty, open);
        popup.Placement = FloatingPopup.ToPlacementMode(placement, owner.FlowDirection == global::Avalonia.Media.FlowDirection.RightToLeft);
        // 弹层不在外壳里：字号、圆角按外壳的算
        var em = owner.FontSize;
        Panel.SetValue(global::Avalonia.Controls.Documents.TextElement.FontSizeProperty, em);
        Panel.CornerRadius = new CornerRadius(owner.Resource<double>(ThemeKeys.Radius.Lg) * em);
        Panel.Padding = new Thickness(owner.Resource<double>(ThemeKeys.Space.Sm) * em);
        // 平台的弹出层要锚点已经在窗口里：没进界面前不开（进了界面后选择器再同步一次）
        if (popup.IsOpen == open || open && !owner.IsAttachedToVisualTree()) return;
        popup.IsOpen = open;
        if (!open) ReturnFocus();
    }

    /// <summary>选好了、关上之后：焦点在弹层里就交给 <paramref name="target"/>（没有时还给按钮）。</summary>
    public void Picked(InputElement? target)
    {
        focusWasInside = false;
        (target ?? Button).Focus(NavigationMethod.Tab);
    }

    void ReturnFocus()
    {
        if (!focusWasInside) return;
        focusWasInside = false;
        Button.Focus(NavigationMethod.Tab);
    }

    /// <summary>Esc：关上，焦点回到按钮；不再往外传（不关掉外面的对话框）。日历先处理（取消选到一半的区间）。</summary>
    void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled) return;
        focusWasInside = true;
        toggle(false, OpenChangeReason.Escape);
        e.Handled = true;
    }

    /// <summary>分段里按 Alt + ↓：打开。</summary>
    void OnOwnerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down || (e.KeyModifiers & KeyModifiers.Alt) == 0 || !Button.IsEffectivelyEnabled) return;
        if (e.Source is Visual source && Panel.IsVisualAncestorOf(source)) return;
        toggle(true, OpenChangeReason.Trigger);
        e.Handled = true;
    }
}

/// <summary>
/// 弹层里的一个快捷选项（vue-tsx-pro 的 renderPickerPresets）：sm、中性色的按钮，文字靠左；当前值就是它时 soft 样式，读屏念"已按下"（TS 的 aria-pressed）。
/// </summary>
sealed class PickerPresetButton : Button
{
    public PickerPresetButton(string label, bool active)
    {
        Name = PickerParts.Preset;
        Content = label;
        IsActive = active;
        Size = TextSize.Sm;
        Variant = active ? ButtonVariant.Soft : ButtonVariant.Ghost;
        Tone = StatusTone.Neutral;
        HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Left;
    }

    /// <summary>当前值就是它。</summary>
    public bool IsActive { get; }

    protected override Type StyleKeyOverride => typeof(Button);

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    sealed class Peer(PickerPresetButton owner) : ButtonAutomationPeer(owner), IToggleProvider
    {
        public ToggleState ToggleState => owner.IsActive ? ToggleState.On : ToggleState.Off;

        // 切换就是点它：选中这个快捷值
        public void Toggle() => owner.OnClick();
    }
}

/// <summary>日期、时间选择器的部件名（TS 的部件）。</summary>
public static class PickerParts
{
    /// <summary>打开按钮（TS 的 propsButton）：IconButton，在外壳的后缀里。</summary>
    public const string Button = "PART_Button";

    /// <summary>弹层（TS 的 propsPopover）：Border。</summary>
    public const string Popover = "PART_Popover";

    /// <summary>快捷选项那一列（TS 的 propsPresets）：Border，靠日历那边一条分隔线。</summary>
    public const string Presets = "PART_Presets";

    /// <summary>一个快捷选项（TS 的 propsPreset）：Button。</summary>
    public const string Preset = "PART_Preset";

    /// <summary>"今天"按钮（TS 的 propsToday）：Button。</summary>
    public const string Today = "PART_Today";

    /// <summary>"今天"所在的页脚：Border，上边一条 borderMuted 分隔线。</summary>
    public const string Footer = "PART_PickerFooter";

    /// <summary>弹层里的日历（TS 的 propsCalendar）：Calendar 或 QuarterCalendar。</summary>
    public const string Calendar = "PART_Calendar";
}
