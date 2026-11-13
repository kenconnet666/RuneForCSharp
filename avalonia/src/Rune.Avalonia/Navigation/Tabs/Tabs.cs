using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 标签页（vue-tsx-pro 的 Tabs，WAI-ARIA 的 Tabs 模式）：Tabs（状态）里放 <see cref="TabList"/>（一排 <see cref="Tab"/>）与各标签对应的 <see cref="TabPanel"/>。
/// <code>
/// &lt;rune:Tabs Value="{Binding Tab}"&gt;
///   &lt;rune:TabList AutomationProperties.Name="订单"&gt;
///     &lt;rune:Tab Value="all"&gt;全部&lt;/rune:Tab&gt;
///     &lt;rune:Tab Value="paid"&gt;已付款&lt;/rune:Tab&gt;
///   &lt;/rune:TabList&gt;
///   &lt;rune:TabPanel Value="all"&gt;…&lt;/rune:TabPanel&gt;
///   &lt;rune:TabPanel Value="paid"&gt;…&lt;/rune:TabPanel&gt;
/// &lt;/rune:Tabs&gt;
/// </code>
/// 没有选中值时选第一个标签；只显示当前标签的面板。键盘：标签之间用方向键移动（整组一个 Tab 停靠点），Home / End 到两端；
/// <see cref="Activation"/> 为 automatic（默认）时焦点到哪个就切到哪个，manual 时按 Enter / 空格才切换。
/// 样式 <see cref="Variant"/>：line（默认，当前标签下方一条品牌色的线，切换时滑过去）、soft（浅色底，像分段控件）。
/// 排列 <see cref="Orientation"/>：horizontal（默认）、vertical（标签在左侧，上下键切换）。<see cref="Size"/> 是整组的字号档位。
/// 没做：可关闭、新建、拖动排序、"全部标签页"菜单（等 Menu）。
/// </summary>
public class Tabs : Flex
{
    /// <summary>当前标签的值；不写时是第一个标签。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<Tabs, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>方向，默认 horizontal。</summary>
    public static readonly StyledProperty<TabsOrientation> OrientationProperty = AvaloniaProperty.Register<Tabs, TabsOrientation>(nameof(Orientation), TabsOrientation.Horizontal);

    /// <summary>焦点移动时是否同时切换，默认 automatic。</summary>
    public static readonly StyledProperty<TabsActivation> ActivationProperty = AvaloniaProperty.Register<Tabs, TabsActivation>(nameof(Activation), TabsActivation.Automatic);

    /// <summary>样式，默认 line。</summary>
    public static readonly StyledProperty<TabsVariant> VariantProperty = AvaloniaProperty.Register<Tabs, TabsVariant>(nameof(Variant), TabsVariant.Line);

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Tabs, TextSize>(nameof(Size));

    /// <summary>标签按下时的波纹；不写时沿用全局开关。</summary>
    public static readonly StyledProperty<bool?> RippleProperty = AvaloniaProperty.Register<Tabs, bool?>(nameof(Ripple));

    /// <summary>新建：竖排列表、间距 space.md。</summary>
    public Tabs()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Md;
    }

    /// <summary>当前标签的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>方向。</summary>
    public TabsOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>焦点移动时是否同时切换。</summary>
    public TabsActivation Activation { get => GetValue(ActivationProperty); set => SetValue(ActivationProperty, value); }

    /// <summary>样式。</summary>
    public TabsVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>波纹。</summary>
    public bool? Ripple { get => GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    /// <summary>实际选中的值：写了的，没写时第一个标签的。</summary>
    public string? SelectedValue => Value ?? Tabs_().FirstOrDefault()?.Value;

    /// <summary>属于这组的标签（不进到里面另一组 Tabs）。</summary>
    internal IEnumerable<Tab> Tabs_() => Members<Tab>();

    /// <summary>属于这组的面板。</summary>
    internal IEnumerable<TabPanel> Panels() => Members<TabPanel>();

    IEnumerable<T> Members<T>() where T : Control => this.GetLogicalDescendants().OfType<T>().Where(member => member.FindLogicalAncestorOfType<Tabs>() == this);

    /// <summary>切到某个标签（标签的点击、自动切换时的聚焦）。</summary>
    internal void Select(string value) => SetCurrentValue(ValueProperty, value);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty)
        {
            var vertical = Orientation == TabsOrientation.Vertical;
            Direction = vertical ? FlexDirection.Row : FlexDirection.Column;
            Align = vertical ? FlexAlign.Start : default;
            Sync();
        }
        else if (change.Property == SizeProperty)
        {
            ControlHeight.Apply(this, Size);
            if (Size.Pixels is { } pixels) SetValue(TextElement.FontSizeProperty, pixels);
            else if (Size.ResourceKey is { } key) Bind(TextElement.FontSizeProperty, this.GetResourceObservable(key));
            else ClearValue(TextElement.FontSizeProperty);
        }
        else if (change.Property == ValueProperty || change.Property == VariantProperty) Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Sync();
    }

    /// <summary>选中状态、外观交给标签、列表与面板（标签、面板挂上来时也会调）。</summary>
    internal void Sync()
    {
        var selected = SelectedValue;
        foreach (var tab in Tabs_()) tab.Apply(this, tab.Value == selected);
        foreach (var list in Members<TabList>()) list.Apply(this);
        foreach (var panel in Panels())
        {
            panel.Apply(panel.Value == selected);
            // 竖排时面板占满标签列表右边的宽度；横排时高度按内容（在不限高的容器里不能往下撑）
            Flex.SetGrow(panel, Orientation == TabsOrientation.Vertical);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabsPeer(this);

    sealed class TabsPeer(Tabs owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

/// <summary>标签面板（role="tabpanel"）：只显示当前标签的那一个；可以聚焦（键盘从标签 Tab 进来），sm 圆角的焦点环。读屏名称是对应标签的文字。</summary>
public class TabPanel : ContentControl
{
    /// <summary>对应标签的值。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<TabPanel, string?>(nameof(Value));

    /// <summary>对应标签的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TabPanel);

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        this.FindLogicalAncestorOfType<Tabs>()?.Sync();
    }

    /// <summary>当前标签的面板才显示（看不见的面板不排版、不绘制）。</summary>
    internal void Apply(bool selected) => IsVisible = selected;

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabPanelPeer(this);

    sealed class TabPanelPeer(TabPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name
            : owner.FindLogicalAncestorOfType<Tabs>()?.Tabs_().FirstOrDefault(tab => tab.Value == owner.Value)?.Content?.ToString();
    }
}
