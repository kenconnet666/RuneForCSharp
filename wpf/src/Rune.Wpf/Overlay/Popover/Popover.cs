using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

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
/// 触发器照常排在原处，点它（里面按钮的 Click）开关；浮层是平台的弹出层（Popup，不留在原处时关闭），位置 <see cref="Placement"/>（12 种，默认 bottom-start，
/// 空间不够时翻到另一边），与触发器隔 <see cref="Offset"/>，<see cref="Arrow"/> 时有指向触发器的箭头。打开时焦点移到里面第一个能聚焦的元素，关上后回到触发器。
/// 点外面、Esc 关闭；<see cref="OpenChanged"/> 报告原因。面板 surfaceRaised、xs 描边、lg 圆角、lg 阴影、space.md 内边距。读屏：浮层以触发器为名称。
/// 部件样式键：<see cref="PopoverParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class Popover : FrameworkElement
{
    /// <summary>触发器（一般是按钮）：照常排在原处，点它开关。</summary>
    public static readonly DependencyProperty TriggerProperty = DependencyProperty.Register(nameof(Trigger), typeof(UIElement), typeof(Popover),
        new FrameworkPropertyMetadata(null, (d, e) => ((Popover)d).OnTriggerChanged((UIElement?)e.OldValue, (UIElement?)e.NewValue)));

    /// <summary>浮层里的内容。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(Popover),
        new FrameworkPropertyMetadata(null, (d, _) => { ((Popover)d).content.Content = ((Popover)d).Content; }));

    /// <summary>开着（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Popover),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Popover)d).Toggle()));

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(nameof(Placement), typeof(FloatingPlacement), typeof(Popover),
        new FrameworkPropertyMetadata(FloatingPlacement.BottomStart));

    /// <summary>与触发器的距离（像素），默认 6；有箭头时另加箭头伸出的一段。</summary>
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(nameof(Offset), typeof(double), typeof(Popover), new FrameworkPropertyMetadata(6.0));

    /// <summary>显示指向触发器的箭头。</summary>
    public static readonly DependencyProperty ArrowProperty = DependencyProperty.Register(nameof(Arrow), typeof(bool), typeof(Popover), new FrameworkPropertyMetadata(false));

    /// <summary>浮层四周给阴影、箭头留的透明边（像素）。</summary>
    const double Overhang = 16;

    readonly ContentPresenter trigger = new();
    readonly Popup popup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Custom };
    readonly LayoutGrid layer = new() { Margin = new Thickness(Overhang) };
    readonly Border shadow = new();
    readonly PopoverPanel panel = new() { Focusable = true, FocusVisualStyle = null };
    readonly ContentPresenter content = new();
    readonly Canvas arrowLayer = new() { IsHitTestVisible = false };
    readonly Border arrow = new() { Visibility = Visibility.Collapsed };
    OpenChangeReason? pending;
    long closedAt = long.MinValue / 2;
    bool focusWasInside;
    bool anchoredElsewhere;

    static Popover() => TextElement.FontSizeProperty.OverrideMetadata(typeof(Popover),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((Popover)d).UpdateMetrics()));

    /// <summary>新建：触发器、弹出层（阴影、面板与箭头）。</summary>
    public Popover()
    {
        shadow.SetResourceReference(StyleProperty, PopoverParts.Shadow);
        panel.SetResourceReference(StyleProperty, PopoverParts.Panel);
        arrow.SetResourceReference(StyleProperty, PopoverParts.Arrow);
        panel.Child = content;
        arrowLayer.Children.Add(arrow);
        layer.Children.Add(shadow);
        layer.Children.Add(panel);
        layer.Children.Add(arrowLayer);
        popup.Child = layer;
        popup.PlacementTarget = trigger;
        popup.CustomPopupPlacementCallback = Place;
        AddLogicalChild(trigger);
        AddVisualChild(trigger);
        AddLogicalChild(popup);
        trigger.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, _) => OnTriggerClick()));
        trigger.AddHandler(KeyDownEvent, new KeyEventHandler((_, e) => OnTriggerKeyDown(e)));
        popup.Opened += (_, _) => OnOpened();
        popup.Closed += (_, _) => OnClosed();
        panel.PreviewKeyDown += (_, e) => OnPanelKeyDown(e);
        panel.IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if ((bool)e.NewValue) focusWasInside = true;
        };
        Loaded += (_, _) => UpdateMetrics();
    }

    /// <summary>触发器。</summary>
    public UIElement? Trigger { get => (UIElement?)GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }

    /// <summary>内容。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>弹出位置。</summary>
    public FloatingPlacement Placement { get => (FloatingPlacement)GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>与触发器的距离。</summary>
    public double Offset { get => (double)GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

    /// <summary>箭头。</summary>
    public bool Arrow { get => (bool)GetValue(ArrowProperty); set => SetValue(ArrowProperty, value); }

    /// <summary>用户开、关了（触发器、点外面、Esc）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>浮层的面板（测试用）。</summary>
    internal Border Panel => panel;

    double ArrowSize => 0.625 * TextElement.GetFontSize(this);

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { trigger, popup }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => trigger;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        trigger.Measure(availableSize);
        return trigger.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        trigger.Arrange(new Rect(finalSize));
        return finalSize;
    }

    void OnTriggerChanged(UIElement? old, UIElement? next)
    {
        trigger.Content = next;
        if (next is not null) AutomationProperties.SetLabeledBy(panel, next);
        else panel.ClearValue(AutomationProperties.LabeledByProperty);
    }

    /// <summary>
    /// 点了触发器：开关。浮层开着时点触发器，平台先因为"点了外面"把它关了，紧接着这次点击又会把它打开：刚被点外面关掉的那一下不再打开。
    /// </summary>
    void OnTriggerClick()
    {
        if (!IsOpen && Environment.TickCount64 - closedAt < 300) return;
        SetOpen(!IsOpen, OpenChangeReason.Trigger);
    }

    /// <summary>按 em 的尺寸：面板 lg 圆角、space.md 内边距，箭头 0.625em。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        var radius = new CornerRadius((TryFindResource("Rune.Radius.Lg") as double? ?? 0.5) * em);
        panel.CornerRadius = shadow.CornerRadius = radius;
        panel.Padding = PanelPadding(em);
        arrow.Width = arrow.Height = ArrowSize;
    }

    /// <summary>面板的内边距：space.md。菜单的面板不留（内边距在里面的内容层上，滚动条贴着边缘）。</summary>
    /// <param name="em">字号。</param>
    private protected virtual Thickness PanelPadding(double em) => new((TryFindResource("Rune.Space.Md") as double? ?? 0.75) * em);

    /// <summary>打开后聚焦哪个元素：里面第一个能聚焦的；没有时聚焦面板。</summary>
    private protected virtual UIElement? FocusTarget() => FirstFocusable(panel);

    /// <summary>触发器上按了键（菜单：↓ / ↑ 打开）。</summary>
    /// <param name="e">按键。</param>
    private protected virtual void OnTriggerKeyDown(KeyEventArgs e)
    {
    }

    /// <summary>面板的读屏类型：弹出框是窗格（念"对话框"），菜单是菜单。</summary>
    internal AutomationControlType PanelRole { get => panel.Role; set => panel.Role = value; }

    /// <summary>关上，焦点不还给触发器（菜单里按 Tab：焦点照常往后走）。</summary>
    /// <param name="reason">为什么。</param>
    private protected void CloseKeepingFocus(OpenChangeReason reason)
    {
        focusWasInside = false;
        SetOpen(false, reason);
    }

    /// <summary>以别的元素为锚点打开（右键菜单：指针所在处，或获得焦点的元素）；关上后恢复以触发器为锚点。</summary>
    /// <param name="target">锚点。</param>
    /// <param name="mode">平台弹出层的位置（MousePoint 是指针所在处）。</param>
    /// <param name="reason">为什么。</param>
    private protected void OpenAt(UIElement target, PlacementMode mode, OpenChangeReason reason)
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
        if (popup.IsOpen != IsOpen) popup.IsOpen = IsOpen;
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(IsOpen, known));
    }

    /// <summary>
    /// 平台的位置：先放在 <see cref="Placement"/> 那一边，放不下时翻到对面；对齐 start / end 时贴着触发器的边（从右到左排版时镜像）。
    /// 弹出层四周有透明边（阴影、箭头），面板与触发器隔开 Offset 加箭头伸出的一段。
    /// </summary>
    CustomPopupPlacement[] Place(Size popupSize, Size targetSize, Point offset)
    {
        var scale = trigger.ActualWidth > 0 ? targetSize.Width / trigger.ActualWidth : 1;
        var margin = Overhang * scale;
        var gap = (Offset + (Arrow ? 0.7 * ArrowSize : 0)) * scale;
        var placement = Placement;
        var startIsLeft = FlowDirection == FlowDirection.LeftToRight;
        double Cross(string? align, double popupLength, double anchorLength, bool horizontal) => (align, horizontal && !startIsLeft) switch
        {
            ("start", false) or ("end", true) => -margin,
            ("end", false) or ("start", true) => anchorLength - popupLength + margin,
            _ => (anchorLength - popupLength) / 2,
        };
        CustomPopupPlacement At(string side) => side switch
        {
            "bottom" => new(new Point(Cross(placement.Align, popupSize.Width, targetSize.Width, true), targetSize.Height + gap - margin), PopupPrimaryAxis.Horizontal),
            "left" => new(new Point(-gap - popupSize.Width + margin, Cross(placement.Align, popupSize.Height, targetSize.Height, false)), PopupPrimaryAxis.Vertical),
            "right" => new(new Point(targetSize.Width + gap - margin, Cross(placement.Align, popupSize.Height, targetSize.Height, false)), PopupPrimaryAxis.Vertical),
            _ => new(new Point(Cross(placement.Align, popupSize.Width, targetSize.Width, true), -gap - popupSize.Height + margin), PopupPrimaryAxis.Horizontal),
        };
        var opposite = placement.Side switch { "bottom" => "top", "left" => "right", "right" => "left", _ => "bottom" };
        return [At(placement.Side), At(opposite)];
    }

    /// <summary>打开：定好位置后放箭头，焦点移到第一个能聚焦的元素（没有时到面板上）。</summary>
    void OnOpened()
    {
        focusWasInside = false;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            PlaceArrow();
            (FocusTarget() ?? panel).Focus();
        });
    }

    /// <summary>可视树里第一个能聚焦（可用、看得见）的元素。</summary>
    static UIElement? FirstFocusable(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            if (VisualTreeHelper.GetChild(parent, index) is not UIElement child || !child.IsVisible || !child.IsEnabled) continue;
            if (child.Focusable) return child;
            if (FirstFocusable(child) is { } found) return found;
        }
        return null;
    }

    /// <summary>平台关了弹出层（点了外面）：写回 IsOpen；焦点在里面时回到触发器。</summary>
    void OnClosed()
    {
        closedAt = Environment.TickCount64;
        if (IsOpen) SetOpen(false, OpenChangeReason.Outside);
        if (anchoredElsewhere)
        {
            anchoredElsewhere = false;
            popup.PlacementTarget = trigger;
            popup.Placement = PlacementMode.Custom;
        }
        if (focusWasInside) Trigger?.Focus();
        focusWasInside = false;
    }

    /// <summary>Esc：关上，焦点回到触发器；Esc 不再往外传（不关掉外面的对话框）。</summary>
    /// <param name="e">按键。</param>
    private protected virtual void OnPanelKeyDown(KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        focusWasInside = true;
        SetOpen(false, OpenChangeReason.Escape);
        e.Handled = true;
    }

    /// <summary>箭头贴着朝向触发器的那条边、对准触发器的中心（离面板两端至少一个箭头宽），尖角朝向触发器。</summary>
    void PlaceArrow()
    {
        arrow.Visibility = Arrow ? Visibility.Visible : Visibility.Collapsed;
        if (!Arrow || PresentationSource.FromVisual(panel) is null || PresentationSource.FromVisual(trigger) is null) return;
        var box = new Rect(Tooltip.Screen(panel, new Point()), Tooltip.Screen(panel, new Point(panel.ActualWidth, panel.ActualHeight)));
        var anchor = new Rect(Tooltip.Screen(trigger, new Point()), Tooltip.Screen(trigger, new Point(trigger.ActualWidth, trigger.ActualHeight)));
        var size = ArrowSize;
        var side = box.Bottom <= anchor.Top + 0.5 ? "top" : box.Top >= anchor.Bottom - 0.5 ? "bottom" : box.Right <= anchor.Left + 0.5 ? "left" : "right";
        var vertical = side is "top" or "bottom";
        var cross = vertical
            ? Math.Clamp(anchor.Left + anchor.Width / 2 - box.Left - size / 2, size, Math.Max(size, box.Width - 2 * size))
            : Math.Clamp(anchor.Top + anchor.Height / 2 - box.Top - size / 2, size, Math.Max(size, box.Height - 2 * size));
        var (left, top, angle) = side switch
        {
            "top" => (cross, box.Height - size / 2, 45.0),
            "bottom" => (cross, -size / 2, 225.0),
            "left" => (box.Width - size / 2, cross, -45.0),
            _ => (-size / 2, cross, 135.0),
        };
        Canvas.SetLeft(arrow, left);
        Canvas.SetTop(arrow, top);
        arrow.RenderTransformOrigin = new Point(0.5, 0.5);
        arrow.RenderTransform = new RotateTransform(angle);
    }

    /// <summary>浮层的面板：读屏是一个对话框（TS 的 role="dialog"），名称取自触发器。</summary>
    sealed class PopoverPanel : Border
    {
        /// <summary>读屏类型。</summary>
        public AutomationControlType Role { get; set; } = AutomationControlType.Pane;

        protected override AutomationPeer OnCreateAutomationPeer() => new PanelAutomationPeer(this);

        sealed class PanelAutomationPeer(PopoverPanel owner) : FrameworkElementAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => owner.Role;

            protected override string GetClassNameCore() => nameof(Popover);

            protected override string GetLocalizedControlTypeCore() => owner.Role == AutomationControlType.Pane ? "对话框" : base.GetLocalizedControlTypeCore();
        }
    }
}

/// <summary>弹出框的部件样式键（DESIGN §7）。</summary>
public static class PopoverParts
{
    /// <summary>面板（TS 的弹出层 div：表面、描边、圆角、内边距）。</summary>
    public static readonly PartKey Panel = new(typeof(Popover), nameof(Panel), typeof(Border));

    /// <summary>面板后面画阴影的一层（DropShadowEffect 不放在面板上：面板里的文字不走软件渲染）。</summary>
    public static readonly PartKey Shadow = new(typeof(Popover), nameof(Shadow), typeof(Border));

    /// <summary>箭头（TS 的 propsArrow）：旋转 45° 的小方块，只画右、下两条边。</summary>
    public static readonly PartKey Arrow = new(typeof(Popover), nameof(Arrow), typeof(Border));
}
