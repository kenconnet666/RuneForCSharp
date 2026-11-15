using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>
/// 日期、时间选择器共用的打开按钮与弹层（vue-tsx-pro 的 usePickerPopover：DatePicker、DateRangePicker、TimePicker）。
/// 输入框外壳是定位的锚点，后缀里的按钮打开（放进外壳的 Suffix），弹层是平台的 Popup（点外部关闭，同 Select：四周留透明边给阴影），里面 Esc 关闭；
/// 关闭时焦点在弹层里就还给按钮；分段里按 Alt + ↓ 打开（同原生 select）。开关状态由选择器自己的 IsOpen 管，这里只报告"想开、想关"。
/// </summary>
sealed class PickerPopover
{
    /// <summary>与外壳的距离（像素）：同 Select。</summary>
    const double Offset = 6;

    /// <summary>弹出层四周给阴影留的透明边（像素）。</summary>
    const double Overhang = 16;

    readonly SegmentedFieldBase owner;
    readonly Popup popup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Custom };
    readonly Border shadow = new();
    readonly Action<bool, OpenChangeReason> toggle;
    FloatingPlacement placement = FloatingPlacement.BottomStart;
    bool focusWasInside;
    // 选好了时焦点要去的元素（带时间时是"时"那一段）：推迟的还焦点按它，不抢回按钮
    UIElement? returnTo;

    /// <summary>接到选择器上：按钮的点击、弹层的开关与按键、分段里的 Alt + ↓。</summary>
    /// <param name="owner">选择器（输入框外壳就是它，弹层的定位锚点）。</param>
    /// <param name="icon">按钮的图标。</param>
    /// <param name="toggle">想开（true）或想关（false）与原因：写回选择器的 IsOpen 并报告。</param>
    /// <param name="shown">打开、排好版之后：把焦点交给弹层里合适的元素。</param>
    public PickerPopover(SegmentedFieldBase owner, IconData icon, Action<bool, OpenChangeReason> toggle, Action shown)
    {
        this.owner = owner;
        this.toggle = toggle;
        Button = new IconButton { Name = "PART_Button", Icon = icon, Radius = Radius.Full };
        Button.SetResourceReference(FrameworkElement.StyleProperty, PickerParts.Button);
        Button.Click += (_, e) =>
        {
            toggle(!popup.IsOpen, OpenChangeReason.Trigger);
            e.Handled = true;
        };
        Panel.SetResourceReference(FrameworkElement.StyleProperty, PickerParts.Popover);
        shadow.SetResourceReference(FrameworkElement.StyleProperty, PickerParts.PopoverShadow);
        Panel.GotKeyboardFocus += (_, _) => focusWasInside = true;
        Panel.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnPanelKeyDown));
        var layer = new LayoutGrid { Margin = new Thickness(Overhang) };
        layer.Children.Add(shadow);
        layer.Children.Add(Panel);
        popup.Child = layer;
        popup.PlacementTarget = owner;
        popup.CustomPopupPlacementCallback = Place;
        popup.Opened += (_, _) =>
        {
            focusWasInside = false;
            returnTo = null;
            owner.Dispatcher.BeginInvoke(shown, DispatcherPriority.Loaded);
        };
        popup.Closed += (_, _) =>
        {
            // 平台关了弹出层（点了外面）：写回选择器
            toggle(false, OpenChangeReason.Outside);
            ReturnFocus();
        };
        owner.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnOwnerKeyDown));
    }

    /// <summary>后缀里的打开按钮（选择器把它放进 Suffix）。</summary>
    public IconButton Button { get; }

    /// <summary>弹层的面板（选择器把内容放进去）。</summary>
    public Border Panel { get; } = new() { Focusable = true, FocusVisualStyle = null };

    /// <summary>平台的弹出层（选择器把它加进逻辑树）。</summary>
    public Popup Popup => popup;

    /// <summary>选择器的状态变了：开关、位置、按钮的名称与能不能点、弹层的字号与 em 尺寸。</summary>
    /// <param name="open">开着。</param>
    /// <param name="where">弹出位置。</param>
    /// <param name="label">按钮与弹层的名称（"选择日期"）。</param>
    /// <param name="editable">能不能打开：不可用、只读时按钮也不可用。</param>
    public void Sync(bool open, FloatingPlacement where, string label, bool editable)
    {
        placement = where;
        AutomationProperties.SetName(Button, label);
        AutomationProperties.SetName(Panel, label);
        Button.IsEnabled = editable;
        var em = owner.FontSize;
        TextInputBase.FitInnerButton(Button, em);
        // 弹层不在外壳里：字号、圆角、内边距按外壳的算
        Panel.SetValue(TextElement.FontSizeProperty, em);
        Panel.CornerRadius = shadow.CornerRadius = new CornerRadius(owner.Resource<double>(ThemeKeys.Radius.Lg) * em);
        Panel.Padding = new Thickness(owner.Resource<double>(ThemeKeys.Space.Sm) * em);
        if (popup.IsOpen == open || open && !owner.IsLoaded) return;
        popup.IsOpen = open;
        if (!open) ReturnFocus();
    }

    /// <summary>选好了、关上之后：焦点交给 <paramref name="target"/>（没有时还给按钮）。</summary>
    public void Picked(UIElement? target)
    {
        returnTo = target;
        (target ?? Button).Focus();
    }

    void ReturnFocus()
    {
        if (!focusWasInside) return;
        focusWasInside = false;
        // 弹出层关上时 WPF 还要收拾焦点（里面的元素刚被拿掉）：等它收拾完再还给按钮（选好了时是选择器指定的元素，Picked 在这之后才调）
        owner.Dispatcher.BeginInvoke(() => (returnTo ?? Button).Focus(), DispatcherPriority.Input);
    }

    /// <summary>贴着外壳的上边或下边，start / end 对齐外壳的两端；放不下时平台换到第二个位置（另一边）。</summary>
    CustomPopupPlacement[] Place(Size popupSize, Size targetSize, Point offset)
    {
        var scale = owner.ActualWidth > 0 ? targetSize.Width / owner.ActualWidth : 1;
        var margin = Overhang * scale;
        var gap = Offset * scale;
        var startIsLeft = owner.FlowDirection == FlowDirection.LeftToRight;
        var x = (placement.Align, startIsLeft) switch
        {
            ("start", true) or ("end", false) => -margin,
            ("end", true) or ("start", false) => targetSize.Width - popupSize.Width + margin,
            _ => (targetSize.Width - popupSize.Width) / 2,
        };
        var below = new CustomPopupPlacement(new Point(x, targetSize.Height + gap - margin), PopupPrimaryAxis.Horizontal);
        var above = new CustomPopupPlacement(new Point(x, -gap - popupSize.Height + margin), PopupPrimaryAxis.Horizontal);
        return placement.Side == "top" ? [above, below] : [below, above];
    }

    /// <summary>Esc 冒泡回来没人处理（日历先处理：取消选到一半的区间）：关上，焦点回到按钮；不再往外传（不关掉外面的对话框）。</summary>
    void OnPanelKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled) return;
        focusWasInside = true;
        toggle(false, OpenChangeReason.Escape);
        e.Handled = true;
    }

    /// <summary>分段里按 Alt + ↓：打开（Alt 组合键在 WPF 里是 SystemKey）。</summary>
    void OnOwnerKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key != Key.Down || (Keyboard.Modifiers & ModifierKeys.Alt) == 0 || !Button.IsEnabled) return;
        if (e.OriginalSource is DependencyObject source && Panel.IsAncestorOf(source)) return;
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
        Content = label;
        IsActive = active;
        Size = TextSize.Sm;
        Variant = active ? ButtonVariant.Soft : ButtonVariant.Ghost;
        Tone = StatusTone.Neutral;
        HorizontalContentAlignment = HorizontalAlignment.Left;
        SetResourceReference(StyleProperty, PickerParts.Preset);
    }

    /// <summary>当前值就是它。</summary>
    public bool IsActive { get; }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    sealed class Peer(PickerPresetButton owner) : ButtonAutomationPeer(owner), IToggleProvider
    {
        public ToggleState ToggleState => owner.IsActive ? ToggleState.On : ToggleState.Off;

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Toggle ? this : base.GetPattern(patternInterface);

        // 切换就是点它：选中这个快捷值
        public void Toggle() => ((IInvokeProvider)this).Invoke();

        protected override string GetClassNameCore() => nameof(Button);
    }
}

/// <summary>日期、时间选择器的部件样式键（TS 的部件）。</summary>
public static class PickerParts
{
    /// <summary>打开按钮（TS 的 propsButton）：IconButton，在外壳的后缀里。</summary>
    public static readonly PartKey Button = new(typeof(DatePicker), nameof(Button), typeof(IconButton));

    /// <summary>弹层（TS 的 propsPopover）：Border。</summary>
    public static readonly PartKey Popover = new(typeof(DatePicker), nameof(Popover), typeof(Border));

    /// <summary>弹层的阴影：面板后面同样大小的一层（WPF 的阴影效果画在面板上会把内容一起糊掉）。</summary>
    public static readonly PartKey PopoverShadow = new(typeof(DatePicker), nameof(PopoverShadow), typeof(Border));

    /// <summary>快捷选项那一列（TS 的 propsPresets）：Border，靠日历那边一条分隔线。</summary>
    public static readonly PartKey Presets = new(typeof(DatePicker), nameof(Presets), typeof(Border));

    /// <summary>一个快捷选项（TS 的 propsPreset）：Button。</summary>
    public static readonly PartKey Preset = new(typeof(DatePicker), nameof(Preset), typeof(Button));

    /// <summary>"今天"按钮（TS 的 propsToday）：Button。</summary>
    public static readonly PartKey Today = new(typeof(DatePicker), nameof(Today), typeof(Button));

    /// <summary>"今天"所在的页脚：Border，上边一条 borderMuted 分隔线。</summary>
    public static readonly PartKey Footer = new(typeof(DatePicker), nameof(Footer), typeof(Border));
}
