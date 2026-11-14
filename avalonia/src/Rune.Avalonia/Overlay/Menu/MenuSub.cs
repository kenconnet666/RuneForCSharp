using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 子菜单（vue-tsx-pro 的 MenuSub，WAI-ARIA 的子菜单）：菜单里的一项，打开另一列菜单（往书写方向的结束边弹出）。
/// <code>
/// &lt;rune:MenuSub Label="移动到" Icon="{x:Static rune:Lucide.FolderInput}"&gt;
///   &lt;rune:MenuItem&gt;项目资料&lt;/rune:MenuItem&gt;
/// &lt;/rune:MenuSub&gt;
/// </code>
/// →（从右到左排版时 ←）、Enter、空格打开并聚焦第一项；子菜单里 ←（→）或 Esc 关闭并回到这一项，不关外面的菜单。
/// 指针停 100 毫秒打开；离开后 400 毫秒内移进子菜单就不关（上游的安全三角在这里简化成宽限时间）。子菜单里执行一项后，整串菜单都关闭。
/// 读屏：这一项是可展开的菜单项，子菜单以它为名称。
/// </summary>
public class MenuSub : Control, IMenuHost
{
    /// <summary>这一项的文字。</summary>
    public static readonly StyledProperty<object?> LabelProperty = AvaloniaProperty.Register<MenuSub, object?>(nameof(Label));

    /// <summary>文字前的图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = MenuItemBase.IconProperty.AddOwner<MenuSub>();

    /// <summary>文字下方的说明。</summary>
    public static readonly StyledProperty<object?> DescriptionProperty = MenuItemBase.DescriptionProperty.AddOwner<MenuSub>();

    /// <summary>子菜单开着（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<MenuSub, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>指针停在这一项上多久后打开。</summary>
    static readonly TimeSpan OpenDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>指针离开后，最多等多久移进子菜单。</summary>
    static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(400);

    readonly SubTrigger trigger;
    readonly Popup popup = new() { IsLightDismissEnabled = false, WindowManagerAddShadowHint = false };
    readonly Border panel = new() { Name = MenuParts.Panel, Focusable = true };
    readonly StackPanel items = new() { Name = MenuParts.Content };
    readonly DispatcherTimer openTimer = new() { Interval = OpenDelay };
    readonly DispatcherTimer graceTimer = new() { Interval = Grace };
    OpenChangeReason? pending;
    bool focusOnOpen;

    /// <summary>新建：这一项与它的子菜单。</summary>
    public MenuSub()
    {
        trigger = new SubTrigger(this);
        panel.Child = new ScrollArea { IsViewportFocusable = false, Content = items };
        popup.Child = panel;
        popup.PlacementTarget = trigger;
        LogicalChildren.AddRange([trigger, popup]);
        VisualChildren.AddRange([trigger, popup]);
        AutomationProperties.SetControlTypeOverride(panel, AutomationControlType.Menu);
        AutomationProperties.SetLabeledBy(panel, trigger);
        _ = new RovingFocus(items, control => control is MenuItemBase) { Orientation = "vertical", Typeahead = true, TextOf = control => ((MenuItemBase)control).LabelText };
        trigger.AddHandler(KeyDownEvent, OnTriggerKeyDown);
        // 竖着排的项会吃掉左右键（漫游焦点里另一个方向的方向键不动）：已处理的也要接，← 照样关这一层
        panel.AddHandler(KeyDownEvent, OnPanelKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        trigger.PointerEntered += (_, e) =>
        {
            graceTimer.Stop();
            if (trigger.IsEffectivelyEnabled && e.Pointer.Type != PointerType.Touch) openTimer.Start();
        };
        trigger.PointerExited += (_, _) =>
        {
            openTimer.Stop();
            if (IsOpen) graceTimer.Start();
        };
        panel.PointerEntered += (_, _) => graceTimer.Stop();
        openTimer.Tick += (_, _) =>
        {
            openTimer.Stop();
            SetOpen(true, OpenChangeReason.Trigger);
        };
        graceTimer.Tick += (_, _) =>
        {
            graceTimer.Stop();
            SetOpen(false, OpenChangeReason.Outside);
        };
        popup.Opened += (_, _) => OnOpened();
    }

    /// <summary>这一项的文字。</summary>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>子菜单开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>子菜单里的项。</summary>
    [Content]
    public Controls Items => items.Children;

    /// <summary>用户开、关了子菜单；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>打开子菜单的那一项（测试用）。</summary>
    internal MenuItemBase Trigger => trigger;

    /// <summary>子菜单的面板（测试用）。</summary>
    internal Border Panel => panel;

    /// <summary>所在的那一列菜单。</summary>
    IMenuHost? ParentHost => this.GetLogicalAncestors().OfType<IMenuHost>().FirstOrDefault();

    /// <inheritdoc />
    public bool? Ripple => ParentHost?.Ripple;

    /// <inheritdoc />
    public void CloseFromItem()
    {
        SetOpen(false, OpenChangeReason.Select);
        ParentHost?.CloseFromItem();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty) trigger.Content = Label;
        else if (change.Property == IconProperty) trigger.Icon = Icon;
        else if (change.Property == DescriptionProperty) trigger.Description = Description;
        else if (change.Property == IsOpenProperty) Toggle();
        else if (change.Property == FlowDirectionProperty)
            popup.Placement = FloatingPopup.ToPlacementMode(FlowDirection == global::Avalonia.Media.FlowDirection.RightToLeft ? FloatingPlacement.LeftStart : FloatingPlacement.RightStart, false);
        else if (change.Property == TextElement.FontSizeProperty) UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        popup.Placement = FloatingPopup.ToPlacementMode(FlowDirection == global::Avalonia.Media.FlowDirection.RightToLeft ? FloatingPlacement.LeftStart : FloatingPlacement.RightStart, false);
        UpdateMetrics();
    }

    /// <summary>外面的菜单关了（这一项离开界面）：子菜单跟着关。</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        openTimer.Stop();
        graceTimer.Stop();
        SetCurrentValue(IsOpenProperty, false);
    }

    /// <summary>面板 lg 圆角、至少 12em 宽，内容层 space.xs（同菜单）；与这一项隔 2 像素。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        panel.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Lg) * em);
        panel.MinWidth = 12 * em;
        panel.Margin = new Thickness(2, 0);
        items.Margin = new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * em);
    }

    /// <summary>改开关并记下原因（属性变化时报告）。</summary>
    void SetOpen(bool open, OpenChangeReason reason)
    {
        if (open && !trigger.IsEffectivelyEnabled) return;
        pending = reason;
        SetCurrentValue(IsOpenProperty, open);
        pending = null;
    }

    void Toggle()
    {
        var reason = pending;
        trigger.Expanded = IsOpen;
        if (popup.IsOpen != IsOpen) popup.IsOpen = IsOpen;
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(IsOpen, known));
    }

    /// <summary>键盘打开的：等排好版后聚焦第一项。</summary>
    void OnOpened()
    {
        if (!focusOnOpen) return;
        focusOnOpen = false;
        Dispatcher.UIThread.Post(FocusFirst, DispatcherPriority.Loaded);
    }

    void FocusFirst() =>
        items.GetLogicalDescendants().OfType<MenuItemBase>().FirstOrDefault(item => item.Host == this && item.IsEffectivelyEnabled && !item.IsDisabled)?.Focus(NavigationMethod.Directional);

    /// <summary>点了这一项、Enter、空格、→：打开并聚焦第一项（已经开着时直接把焦点移进去）。</summary>
    internal void OpenWithFocus()
    {
        if (IsOpen)
        {
            FocusFirst();
            return;
        }
        focusOnOpen = true;
        SetOpen(true, OpenChangeReason.Trigger);
    }

    bool RightToLeft => FlowDirection == global::Avalonia.Media.FlowDirection.RightToLeft;

    void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != (RightToLeft ? Key.Left : Key.Right)) return;
        OpenWithFocus();
        e.Handled = true;
    }

    /// <summary>子菜单里 ←（→）或 Esc：只关这一层，焦点回到这一项；不往外传（不关外面的菜单）。</summary>
    void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        var back = e.Key == (RightToLeft ? Key.Right : Key.Left);
        if (!back && (e.Key != Key.Escape || e.Handled)) return;
        SetOpen(false, OpenChangeReason.Escape);
        trigger.Focus(NavigationMethod.Directional);
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

    /// <summary>子菜单的那一项：外观同菜单项，结束边一个箭头；属于外面那一列菜单（方向键在外面那一列里移动）。</summary>
    sealed class SubTrigger(MenuSub owner) : MenuItemBase
    {
        bool expanded;

        /// <summary>子菜单开着（读屏的展开状态）。</summary>
        public bool Expanded
        {
            get => expanded;
            set
            {
                expanded = value;
                PseudoClasses.Set(":open", value);
            }
        }

        /// <summary>所属的子菜单。</summary>
        public MenuSub Sub => owner;

        internal override IMenuHost? Host => owner.ParentHost;

        protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnAttachedToLogicalTree(e);
            PseudoClasses.Set(":submenu", true);
        }

        // 打开子菜单不是执行：不关外面的菜单
        protected override void OnClick()
        {
            if (!IsLoading && !IsDisabled) owner.OpenWithFocus();
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new SubTriggerPeer(this);

        sealed class SubTriggerPeer(SubTrigger owner) : ButtonBaseAutomationPeer(owner), IExpandCollapseProvider
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

            protected override string GetClassNameCore() => nameof(MenuSub);

            protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.LabelText;

            public ExpandCollapseState ExpandCollapseState => owner.Expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

            public bool ShowsMenu => true;

            public void Expand() => owner.OnClick();

            public void Collapse() => owner.Sub.SetCurrentValue(IsOpenProperty, false);
        }
    }
}
