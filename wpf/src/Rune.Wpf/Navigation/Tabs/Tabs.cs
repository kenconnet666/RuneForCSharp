using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 标签页（vue-tsx-pro 的 Tabs，WAI-ARIA 的 Tabs 模式）：Tabs（状态）里放 <see cref="TabList"/>（一排 <see cref="Tab"/>）与各标签对应的 <see cref="TabPanel"/>。
/// <code>
/// &lt;rune:Tabs Value="{Binding Tab}"&gt;
///   &lt;rune:TabList AutomationProperties.Name="订单"&gt;
///     &lt;rune:Tab Value="all"&gt;全部&lt;/rune:Tab&gt;
///   &lt;/rune:TabList&gt;
///   &lt;rune:TabPanel Value="all"&gt;…&lt;/rune:TabPanel&gt;
/// &lt;/rune:Tabs&gt;
/// </code>
/// 没有选中值时选第一个标签；只显示当前标签的面板。键盘：标签之间用方向键移动（整组一个 Tab 停靠点），Home / End 到两端；
/// <see cref="Activation"/> 为 automatic（默认）时焦点到哪个就切到哪个，manual 时按 Enter / 空格才切换。
/// 样式 <see cref="Variant"/>：line（默认，当前标签下方一条品牌色的线，切换时滑过去）、soft（浅色底）。<see cref="Orientation"/>：horizontal、vertical。
/// 没做：可关闭、新建、拖动排序、"全部标签页"菜单（等 Menu）。
/// </summary>
public class Tabs : Flex
{
    /// <summary>当前标签的值；不写时是第一个标签。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(Tabs),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Tabs)d).Sync()));

    /// <summary>方向，默认 horizontal。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(TabsOrientation), typeof(Tabs),
        new FrameworkPropertyMetadata(TabsOrientation.Horizontal, (d, _) => ((Tabs)d).ApplyOrientation()));

    /// <summary>焦点移动时是否同时切换，默认 automatic。</summary>
    public static readonly DependencyProperty ActivationProperty = DependencyProperty.Register(nameof(Activation), typeof(TabsActivation), typeof(Tabs),
        new FrameworkPropertyMetadata(TabsActivation.Automatic));

    /// <summary>样式，默认 line。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(TabsVariant), typeof(Tabs),
        new FrameworkPropertyMetadata(TabsVariant.Line, (d, _) => ((Tabs)d).Sync()));

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Tabs),
        new FrameworkPropertyMetadata(default(TextSize), (d, _) => ((Tabs)d).ApplySize()));

    /// <summary>标签按下时的波纹；不写时沿用全局开关。</summary>
    public static readonly DependencyProperty RippleProperty = DependencyProperty.Register(nameof(Ripple), typeof(bool?), typeof(Tabs),
        new FrameworkPropertyMetadata(null, (d, _) => ((Tabs)d).Sync()));

    /// <summary>新建：竖排列表、间距 space.md。</summary>
    public Tabs()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Md;
        Loaded += (_, _) => Sync();
    }

    /// <summary>当前标签的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>方向。</summary>
    public TabsOrientation Orientation { get => (TabsOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>焦点移动时是否同时切换。</summary>
    public TabsActivation Activation { get => (TabsActivation)GetValue(ActivationProperty); set => SetValue(ActivationProperty, value); }

    /// <summary>样式。</summary>
    public TabsVariant Variant { get => (TabsVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>波纹。</summary>
    public bool? Ripple { get => (bool?)GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    /// <summary>实际选中的值：写了的，没写时第一个标签的。</summary>
    public string? SelectedValue => Value ?? Members<Tab>().FirstOrDefault()?.Value;

    /// <summary>属于这组的成员（不进到里面另一组 Tabs）。</summary>
    internal IEnumerable<T> Members<T>() where T : DependencyObject => Descendants(this).OfType<T>().Where(member => Owner(member) == this);

    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    /// <summary>元素属于哪组 Tabs（逻辑树上最近的一个）。</summary>
    internal static Tabs? Owner(DependencyObject element)
    {
        for (var parent = LogicalTreeHelper.GetParent(element); parent is not null; parent = LogicalTreeHelper.GetParent(parent))
            if (parent is Tabs tabs) return tabs;
        return null;
    }

    /// <summary>切到某个标签（标签的点击、自动切换时的聚焦）。</summary>
    internal void Select(string value) => SetCurrentValue(ValueProperty, value);

    void ApplyOrientation()
    {
        var vertical = Orientation == TabsOrientation.Vertical;
        Direction = vertical ? FlexDirection.Row : FlexDirection.Column;
        Align = vertical ? FlexAlign.Start : default;
        Sync();
    }

    void ApplySize()
    {
        if (Size.Pixels is { } pixels) TextElement.SetFontSize(this, pixels);
        else if (Size.ResourceKey is { } key) SetResourceReference(TextElement.FontSizeProperty, key);
        else ClearValue(TextElement.FontSizeProperty);
    }

    /// <summary>选中状态、外观交给标签、列表与面板（标签、面板挂上来时也会调）。</summary>
    internal void Sync()
    {
        var selected = SelectedValue;
        foreach (var tab in Members<Tab>()) tab.Apply(this, tab.Value == selected);
        foreach (var list in Members<TabList>()) list.Apply(this);
        foreach (var panel in Members<TabPanel>())
        {
            panel.Visibility = panel.Value == selected ? Visibility.Visible : Visibility.Collapsed;
            // 竖排时面板占满标签列表右边的宽度；横排时高度按内容
            SetGrow(panel, Orientation == TabsOrientation.Vertical);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabsPeer(this);

    sealed class TabsPeer(Tabs owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Tabs);
    }
}

/// <summary>标签面板（role="tabpanel"）：只显示当前标签的那一个；可以聚焦（键盘从标签 Tab 进来），sm 圆角的焦点环。读屏名称是对应标签的文字。</summary>
public class TabPanel : ContentControl
{
    /// <summary>对应标签的值。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(TabPanel));

    static TabPanel() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TabPanel), new FrameworkPropertyMetadata(typeof(TabPanel)));

    /// <summary>新建：挂上来时告诉所在的 Tabs。</summary>
    public TabPanel() => Loaded += (_, _) => Tabs.Owner(this)?.Sync();

    /// <summary>对应标签的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabPanelPeer(this);

    sealed class TabPanelPeer(TabPanel owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override string GetClassNameCore() => nameof(TabPanel);

        /// <summary>写了名称时用它；否则是对应标签的文字（TS 的 aria-labelledby），不用面板里的内容。</summary>
        protected override string GetNameCore() => System.Windows.Automation.AutomationProperties.GetName(owner) is { Length: > 0 } name ? name
            : Tabs.Owner(owner)?.Members<Tab>().FirstOrDefault(tab => tab.Value == owner.Value)?.Content?.ToString() ?? base.GetNameCore();
    }
}
