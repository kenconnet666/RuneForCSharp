using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>为什么开、关（vue-tsx-pro 的 OpenChangeReason）：只报告用户的操作，代码里（父级、绑定）改 IsOpen 时不报告。</summary>
public enum OpenChangeReason
{
    /// <summary>点了触发器。</summary>
    Trigger,

    /// <summary>点了浮层外面。</summary>
    Outside,

    /// <summary>按了 Esc。</summary>
    Escape,

    /// <summary>点了关闭按钮、取消按钮。</summary>
    Close,

    /// <summary>点了确认按钮（对话框）。</summary>
    Confirm,

    /// <summary>选中了一项（单选的下拉选中后关闭）。</summary>
    Select,

    /// <summary>焦点离开了（Tab 移走）。</summary>
    Focus,

    /// <summary>代码调用：快捷键打开（命令面板）、命令式打开的对话框。</summary>
    Imperative,
}

/// <summary>浮层开关变了的事件参数。</summary>
/// <param name="isOpen">现在开着。</param>
/// <param name="reason">为什么。</param>
public sealed class OpenChangedEventArgs(bool isOpen, OpenChangeReason reason) : EventArgs
{
    /// <summary>现在开着。</summary>
    public bool IsOpen { get; } = isOpen;

    /// <summary>为什么。</summary>
    public OpenChangeReason Reason { get; } = reason;
}

/// <summary>
/// 弹出框（vue-tsx-pro 的 Popover，非模态）：点触发器在旁边弹出一块内容（筛选条件、简短表单、详情），点外面或 Esc 关闭。
/// <code>
/// &lt;rune:Popover IsOpen="{Binding Sharing}" Placement="bottom-start" Arrow="True"&gt;
///   &lt;rune:Popover.Trigger&gt;&lt;rune:Button Icon="{x:Static rune:Lucide.Share2}"&gt;分享&lt;/rune:Button&gt;&lt;/rune:Popover.Trigger&gt;
///   …内容…
/// &lt;/rune:Popover&gt;
/// </code>
/// 触发器照常排在原处，点它（里面按钮的 Click）开关；浮层是平台的弹出层（Popup），位置 <see cref="Placement"/>（12 种，默认 bottom-start，空间不够时翻到另一边），
/// 与触发器隔 <see cref="Offset"/>，<see cref="Arrow"/> 时有指向触发器的箭头。打开时焦点移到里面第一个能聚焦的元素，关上后回到触发器。
/// 点外面关闭（那一下不传给下面的控件，同 WinUI 的 Flyout）、Esc 关闭；<see cref="OpenChanged"/> 报告原因。面板 surfaceRaised、xs 描边、lg 圆角、lg 阴影、space.md 内边距。
/// 读屏：浮层以触发器为名称（TS 的 aria-labelledby）。部件名在 <see cref="PopoverParts"/>。
/// </summary>
[PseudoClasses(":open")]
public class Popover : Control
{
    /// <summary>触发器（一般是按钮）：照常排在原处，点它开关。</summary>
    public static readonly StyledProperty<Control?> TriggerProperty = AvaloniaProperty.Register<Popover, Control?>(nameof(Trigger));

    /// <summary>浮层里的内容。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Popover, object?>(nameof(Content));

    /// <summary>开着（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<Popover, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    public static readonly StyledProperty<FloatingPlacement> PlacementProperty = AvaloniaProperty.Register<Popover, FloatingPlacement>(nameof(Placement), FloatingPlacement.BottomStart);

    /// <summary>与触发器的距离（像素），默认 6；有箭头时另加箭头伸出的一段。</summary>
    public static readonly StyledProperty<double> OffsetProperty = AvaloniaProperty.Register<Popover, double>(nameof(Offset), 6);

    /// <summary>显示指向触发器的箭头。</summary>
    public static readonly StyledProperty<bool> ArrowProperty = AvaloniaProperty.Register<Popover, bool>(nameof(Arrow));

    /// <summary>
    /// 触发器上的展开状态（TS 交给触发器的 aria-expanded）：是弹出框、菜单的触发器时是开没开，不是时为 null。按钮的读屏按它报展开、收起。
    /// </summary>
    internal static readonly AttachedProperty<bool?> TriggerExpandedProperty = AvaloniaProperty.RegisterAttached<Popover, Control, bool?>("TriggerExpanded");

    readonly ContentPresenter trigger = new();
    readonly Popup popup = new() { IsLightDismissEnabled = true, WindowManagerAddShadowHint = false };
    readonly PopoverLayout layout;
    readonly Border panel = new() { Name = PopoverParts.Panel, Focusable = true };
    readonly ContentPresenter content = new();
    readonly Canvas arrowLayer = new() { IsHitTestVisible = false };
    readonly Border arrow = new() { Name = PopoverParts.Arrow, IsVisible = false };
    OpenChangeReason? pending;
    bool focusWasInside;
    bool anchoredElsewhere;

    /// <summary>新建：触发器、弹出层（面板与箭头）。</summary>
    public Popover()
    {
        layout = new PopoverLayout(this);
        panel.Child = content;
        arrowLayer.Children.Add(arrow);
        layout.Children.AddRange([panel, arrowLayer]);
        popup.Child = layout;
        popup.PlacementTarget = trigger;
        LogicalChildren.AddRange([trigger, popup]);
        VisualChildren.AddRange([trigger, popup]);
        trigger.AddHandler(Button.ClickEvent, (_, _) => SetOpen(!IsOpen, OpenChangeReason.Trigger));
        trigger.AddHandler(KeyDownEvent, (_, e) => OnTriggerKeyDown(e));
        popup.Opened += (_, _) => OnOpened();
        popup.Closed += (_, _) => OnClosed();
        panel.AddHandler(KeyDownEvent, (_, e) => OnPanelKeyDown(e));
        panel.GotFocus += (_, _) => focusWasInside = true;
    }

    /// <summary>触发器。</summary>
    public Control? Trigger { get => GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }

    /// <summary>内容。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>弹出位置。</summary>
    public FloatingPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>与触发器的距离。</summary>
    public double Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

    /// <summary>箭头。</summary>
    public bool Arrow { get => GetValue(ArrowProperty); set => SetValue(ArrowProperty, value); }

    /// <summary>用户开、关了（触发器、点外面、Esc）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>浮层的面板（测试用）。</summary>
    internal Border Panel => panel;

    /// <summary>箭头的边长（0.625em，按面板的字号）。</summary>
    internal double ArrowSize => 0.625 * TextElement.GetFontSize(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TriggerProperty)
        {
            (change.OldValue as Control)?.ClearValue(TriggerExpandedProperty);
            Trigger?.SetValue(TriggerExpandedProperty, IsOpen);
            trigger.Content = Trigger;
            if (Trigger is { } control) AutomationProperties.SetLabeledBy(panel, control);
            else panel.ClearValue(AutomationProperties.LabeledByProperty);
        }
        else if (change.Property == ContentProperty) content.Content = Content;
        else if (change.Property == IsOpenProperty) Toggle();
        else if (change.Property == PlacementProperty || change.Property == FlowDirectionProperty)
            popup.Placement = FloatingPopup.ToPlacementMode(Placement, FlowDirection == global::Avalonia.Media.FlowDirection.RightToLeft);
        else if (change.Property == ArrowProperty || change.Property == OffsetProperty) layout.InvalidateMeasure();
        else if (change.Property == TextElement.FontSizeProperty) UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        popup.Placement = FloatingPopup.ToPlacementMode(Placement, FlowDirection == global::Avalonia.Media.FlowDirection.RightToLeft);
        UpdateMetrics();
    }

    /// <summary>按 em 的尺寸（Border 上没有字号，主题里的 em 算不出来）：面板 lg 圆角、space.md 内边距，箭头 0.625em。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        panel.CornerRadius = new CornerRadius(this.Resource("Rune.Radius.Lg", 0.5) * em);
        panel.Padding = PanelPadding(em);
        arrow.Width = arrow.Height = ArrowSize;
    }

    /// <summary>面板的内边距：space.md。菜单的面板不留（内边距在里面的内容层上，滚动条贴着边缘）。</summary>
    /// <param name="em">字号。</param>
    private protected virtual Thickness PanelPadding(double em) => new(this.Resource("Rune.Space.Md", 0.75) * em);

    /// <summary>打开后聚焦哪个元素：里面第一个能聚焦的；没有时聚焦面板。</summary>
    private protected virtual InputElement? FocusTarget() =>
        panel.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(element => element.Focusable && element.IsEffectivelyEnabled && element.IsEffectivelyVisible);

    /// <summary>触发器上按了键（菜单：↓ / ↑ 打开）。</summary>
    /// <param name="e">按键。</param>
    private protected virtual void OnTriggerKeyDown(KeyEventArgs e)
    {
    }

    /// <summary>关上，焦点不还给触发器（菜单里按 Tab：焦点照常往后走）。</summary>
    /// <param name="reason">为什么。</param>
    private protected void CloseKeepingFocus(OpenChangeReason reason)
    {
        focusWasInside = false;
        SetOpen(false, reason);
    }

    /// <summary>以别的元素为锚点打开（右键菜单：指针所在处，或获得焦点的元素）；关上后恢复以触发器为锚点。</summary>
    /// <param name="target">锚点。</param>
    /// <param name="mode">平台弹出层的位置（Pointer 是指针所在处）。</param>
    /// <param name="reason">为什么。</param>
    private protected void OpenAt(Control target, PlacementMode mode, OpenChangeReason reason)
    {
        anchoredElsewhere = true;
        popup.PlacementTarget = target;
        popup.Placement = mode;
        SetOpen(true, reason);
    }

    /// <summary>改开关并记下原因（属性变化时报告）。</summary>
    private protected void SetOpen(bool open, OpenChangeReason reason)
    {
        pending = reason;
        SetCurrentValue(IsOpenProperty, open);
        pending = null;
    }

    void Toggle()
    {
        var reason = pending;
        PseudoClasses.Set(":open", IsOpen);
        Trigger?.SetValue(TriggerExpandedProperty, IsOpen);
        if (popup.IsOpen != IsOpen) popup.IsOpen = IsOpen;
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(IsOpen, known));
    }

    /// <summary>打开：等排好版、定好位置后放箭头、焦点移到第一个能聚焦的元素（没有时到面板上）。</summary>
    void OnOpened()
    {
        focusWasInside = false;
        Dispatcher.UIThread.Post(() =>
        {
            arrow.IsVisible = Arrow;
            if (Arrow) FloatingPopup.PlaceArrow(arrow, arrowLayer, trigger);
            (FocusTarget() ?? panel).Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>平台关了弹出层（点了外面）：写回 IsOpen；焦点在里面时回到触发器。</summary>
    void OnClosed()
    {
        if (IsOpen) SetOpen(false, OpenChangeReason.Outside);
        if (anchoredElsewhere)
        {
            anchoredElsewhere = false;
            popup.PlacementTarget = trigger;
            popup.Placement = FloatingPopup.ToPlacementMode(Placement, FlowDirection == global::Avalonia.Media.FlowDirection.RightToLeft);
        }
        if (focusWasInside && Trigger is { } control) control.Focus(NavigationMethod.Tab);
        focusWasInside = false;
    }

    /// <summary>Esc：关上，焦点回到触发器；Esc 不再往外传（不关掉外面的对话框）。</summary>
    /// <param name="e">按键。</param>
    private protected virtual void OnPanelKeyDown(KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled) return;
        focusWasInside = true;
        SetOpen(false, OpenChangeReason.Escape);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        trigger.Measure(availableSize);
        popup.Measure(availableSize);
        return trigger.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        trigger.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>
/// 弹出层的根：在主轴两侧（上下或左右）留出与触发器的距离（Offset，有箭头时加上箭头伸出的一段：边长的 0.7 倍），面板与箭头层排在中间。
/// 距离做成两侧都留的透明边，翻到另一边时照样对（平台弹出层的偏移量翻转时不跟着反向）；对齐的那条轴不留，start / end 对齐贴着触发器的边。
/// </summary>
sealed class PopoverLayout(Popover owner) : Panel
{
    Thickness Gap()
    {
        var gap = owner.Offset + (owner.Arrow ? 0.7 * owner.ArrowSize : 0);
        return owner.Placement.IsVertical ? new Thickness(0, gap) : new Thickness(gap, 0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var gap = Gap();
        var inner = base.MeasureOverride(availableSize.Deflate(gap));
        return inner.Inflate(gap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize).Deflate(Gap());
        foreach (var child in Children) child.Arrange(rect);
        return finalSize;
    }
}

/// <summary>弹出框的部件名：样式里用 <c>rune|Popover Border#PART_Panel</c> 选中（面板在弹出层里，逻辑上仍是弹出框的后代）。</summary>
public static class PopoverParts
{
    /// <summary>面板（TS 的弹出层 div：表面、描边、圆角、阴影、内边距）。</summary>
    public const string Panel = "PART_Panel";

    /// <summary>箭头（TS 的 propsArrow）：旋转 45° 的小方块，只画右、下两条边。</summary>
    public const string Arrow = "PART_Arrow";
}
