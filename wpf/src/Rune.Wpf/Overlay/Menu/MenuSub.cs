using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;

namespace Rune.Wpf;

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
[ContentProperty(nameof(Items))]
public class MenuSub : FrameworkElement, IMenuHost
{
    /// <summary>这一项的文字。</summary>
    // 回调写成语句体：默认值是 null 时，有返回值的表达式 lambda 会选中 (PropertyChangedCallback, CoerceValueCallback) 重载，被当成强制回调
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(object), typeof(MenuSub),
        new FrameworkPropertyMetadata(null, (d, _) => { ((MenuSub)d).trigger.Content = ((MenuSub)d).Label; }));

    /// <summary>文字前的图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(MenuSub),
        new FrameworkPropertyMetadata(null, (d, _) => { ((MenuSub)d).trigger.Icon = ((MenuSub)d).Icon; }));

    /// <summary>文字下方的说明。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(object), typeof(MenuSub),
        new FrameworkPropertyMetadata(null, (d, _) => { ((MenuSub)d).trigger.Description = ((MenuSub)d).Description; }));

    /// <summary>子菜单开着（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(MenuSub),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((MenuSub)d).Toggle()));

    /// <summary>浮层四周给阴影留的空（同 Popover）。</summary>
    const double Overhang = 16;

    /// <summary>子菜单与这一项的距离（像素）。</summary>
    const double Gap = 2;

    readonly SubTrigger trigger;
    readonly Popup popup = new() { StaysOpen = true, AllowsTransparency = true, Placement = PlacementMode.Custom };
    readonly Border shadow = new();
    readonly MenuPanel panel = new() { Focusable = true, FocusVisualStyle = null };
    readonly StackPanel items = new();
    readonly DispatcherTimer openTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    readonly DispatcherTimer graceTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    OpenChangeReason? pending;
    bool focusOnOpen;

    static MenuSub() => TextElement.FontSizeProperty.OverrideMetadata(typeof(MenuSub),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((MenuSub)d).UpdateMetrics()));

    /// <summary>新建：这一项与它的子菜单。</summary>
    public MenuSub()
    {
        trigger = new SubTrigger(this);
        shadow.SetResourceReference(StyleProperty, PopoverParts.Shadow);
        panel.SetResourceReference(StyleProperty, PopoverParts.Panel);
        panel.Child = new ScrollArea { IsViewportFocusable = false, Content = items };
        var layer = new Grid { Margin = new Thickness(Overhang) };
        layer.Children.Add(shadow);
        layer.Children.Add(panel);
        popup.Child = layer;
        popup.PlacementTarget = trigger;
        popup.CustomPopupPlacementCallback = Place;
        AddLogicalChild(trigger);
        AddVisualChild(trigger);
        AddLogicalChild(popup);
        AutomationProperties.SetLabeledBy(panel, trigger);
        _ = new RovingFocus(items, element => element is MenuItemBase) { Orientation = "vertical", Typeahead = true, TextOf = element => ((MenuItemBase)element).LabelText };
        trigger.KeyDown += OnTriggerKeyDown;
        panel.PreviewKeyDown += OnPanelKeyDown;
        trigger.MouseEnter += (_, _) =>
        {
            graceTimer.Stop();
            if (trigger.IsEnabled) openTimer.Start();
        };
        trigger.MouseLeave += (_, _) =>
        {
            openTimer.Stop();
            if (IsOpen) graceTimer.Start();
        };
        panel.MouseEnter += (_, _) => graceTimer.Stop();
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
        // 外面的菜单关了（这一项离开界面）：子菜单跟着关
        Unloaded += (_, _) =>
        {
            openTimer.Stop();
            graceTimer.Stop();
            SetCurrentValue(IsOpenProperty, false);
        };
        Loaded += (_, _) => UpdateMetrics();
    }

    /// <summary>这一项的文字。</summary>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>子菜单开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>子菜单里的项。</summary>
    public UIElementCollection Items => items.Children;

    /// <summary>用户开、关了子菜单；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary><see cref="OpenChanged"/> 的路由事件（直接：不往上传，嵌套的浮层发的同名事件不会串到外层）。</summary>
    public static readonly RoutedEvent OpenChangedEvent = EventManager.RegisterRoutedEvent(nameof(OpenChanged), RoutingStrategy.Direct, typeof(EventHandler<OpenChangedEventArgs>), typeof(MenuSub));

    /// <summary>打开子菜单的那一项（测试用）。</summary>
    internal MenuItemBase Trigger => trigger;

    /// <summary>子菜单的面板（测试用）。</summary>
    internal Border Panel => panel;

    /// <summary>所在的那一列菜单。</summary>
    IMenuHost? ParentHost => MenuItemBase.Ancestors(this).OfType<IMenuHost>().FirstOrDefault();

    /// <inheritdoc />
    public bool? Ripple => ParentHost?.Ripple;

    /// <inheritdoc />
    public void CloseFromItem()
    {
        SetOpen(false, OpenChangeReason.Select);
        ParentHost?.CloseFromItem();
    }

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { trigger, popup }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override System.Windows.Media.Visual GetVisualChild(int index) => trigger;

    /// <summary>面板 lg 圆角、至少 12em 宽，内容层 space.xs（同菜单）。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        panel.CornerRadius = shadow.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Lg) * em);
        panel.MinWidth = 12 * em;
        items.Margin = new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * em);
    }

    /// <summary>往书写方向的结束边弹出，顶边对齐这一项；放不下时翻到另一边。浮层四周的阴影空白抵掉。</summary>
    CustomPopupPlacement[] Place(Size popupSize, Size targetSize, Point offset)
    {
        var scale = trigger.ActualWidth > 0 ? targetSize.Width / trigger.ActualWidth : 1;
        var margin = Overhang * scale;
        var gap = Gap * scale;
        var after = new CustomPopupPlacement(new Point(targetSize.Width + gap - margin, -margin), PopupPrimaryAxis.Vertical);
        var before = new CustomPopupPlacement(new Point(-popupSize.Width - gap + margin, -margin), PopupPrimaryAxis.Vertical);
        return FlowDirection == FlowDirection.RightToLeft ? [before, after] : [after, before];
    }

    /// <summary>改开关并记下原因（属性变化时报告）。</summary>
    void SetOpen(bool open, OpenChangeReason reason)
    {
        if (open && !trigger.IsEnabled) return;
        pending = reason;
        SetCurrentValue(IsOpenProperty, open);
        pending = null;
    }

    void Toggle()
    {
        var reason = pending;
        trigger.Expanded = IsOpen;
        if (popup.IsOpen != IsOpen) popup.IsOpen = IsOpen;
        if (reason is { } known) RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, IsOpen, known));
    }

    /// <summary>键盘打开的：等排好版后聚焦第一项。</summary>
    void OnOpened()
    {
        if (!focusOnOpen) return;
        focusOnOpen = false;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FocusFirst);
    }

    void FocusFirst() => MenuItemBase.ItemsIn(items).FirstOrDefault(item => item.Host == this && item.IsEnabled && !item.IsDisabled)?.Focus();

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

    bool RightToLeft => FlowDirection == FlowDirection.RightToLeft;

    void OnTriggerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != (RightToLeft ? Key.Left : Key.Right)) return;
        OpenWithFocus();
        e.Handled = true;
    }

    /// <summary>子菜单里 ←（→）或 Esc：只关这一层，焦点回到这一项；不往外传（不关外面的菜单）。预览阶段接，先于项的漫游焦点。</summary>
    void OnPanelKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape && e.Key != (RightToLeft ? Key.Right : Key.Left)) return;
        SetOpen(false, OpenChangeReason.Escape);
        trigger.Focus();
        e.Handled = true;
    }

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

    /// <summary>子菜单的面板：读屏是菜单，名称取自这一项。</summary>
    sealed class MenuPanel : Border
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new PanelPeer(this);

        sealed class PanelPeer(MenuPanel owner) : FrameworkElementAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu;

            protected override string GetClassNameCore() => nameof(MenuSub);

            // 名称取自那一项（LabeledBy）：平台只找已经建好的对等体，这里替它建（同 Popover 的面板）
            protected override string GetNameCore() =>
                base.GetNameCore() is { Length: > 0 } name ? name
                : AutomationProperties.GetLabeledBy(owner) is UIElement label ? CreatePeerForElement(label)?.GetName() ?? "" : "";
        }
    }

    /// <summary>子菜单的那一项：外观同菜单项，结束边一个箭头；属于外面那一列菜单（方向键在外面那一列里移动）。</summary>
    sealed class SubTrigger : MenuItemBase
    {
        readonly MenuSub owner;

        public SubTrigger(MenuSub owner)
        {
            this.owner = owner;
            IsSubmenu = true;
        }

        /// <summary>子菜单开着（读屏的展开状态）。</summary>
        public bool Expanded { get; set; }

        internal override IMenuHost? Host => owner.ParentHost;

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

            protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.LabelText;

            public override object? GetPattern(PatternInterface patternInterface) =>
                patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);

            public ExpandCollapseState ExpandCollapseState => owner.Expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

            public void Expand() => owner.owner.OpenWithFocus();

            public void Collapse() => owner.owner.SetCurrentValue(IsOpenProperty, false);
        }
    }
}
