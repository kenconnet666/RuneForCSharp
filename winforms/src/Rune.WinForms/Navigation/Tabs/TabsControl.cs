using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 标签页（vue-tsx-pro 的 Tabs）：TabsControl（状态）里放 <see cref="TabListControl"/>（一排 <see cref="TabItemControl"/>）与各标签对应的 <see cref="TabPanelControl"/>。
/// <code>
/// var tabs = new TabsControl();
/// var list = new TabListControl { AccessibleName = "订单" };
/// list.Controls.Add(new TabItemControl { Value = "all", Text = "全部" });
/// tabs.Controls.AddRange([list, new TabPanelControl { Value = "all" }]);
/// </code>
/// 平台自带 TabControl / TabPage（系统画的选项卡），这里照上游拆成四层、外观照上游。没有选中值时选第一个标签；只显示当前标签的面板。
/// 键盘：标签之间用方向键移动（整组一个 Tab 停靠点），Home / End 到两端；<see cref="Activation"/> 为 automatic（默认）时焦点到哪个就切到哪个，
/// manual 时按 Enter / 空格才切换。<see cref="Variant"/>：line（下划线）、soft（浅色底）；<see cref="Orientation"/>：horizontal、vertical。
/// 没做：可关闭、新建、拖动排序、"全部标签页"菜单（等 Menu）。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[Description("标签页：标签列表加对应的面板，只显示当前的。")]
public class TabsControl : FlexControl
{
    /// <summary>新建：竖排、间距 space.md。</summary>
    public TabsControl()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Md;
        AutoSize = true;
    }

    /// <summary>当前标签变了。</summary>
    [Category("Action")]
    public event EventHandler? ValueChanged;

    /// <summary>当前标签的值；不写时是第一个标签。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>方向，默认 horizontal。</summary>
    [Category("Appearance")]
    public TabsOrientation Orientation
    {
        get => field.Value is null ? TabsOrientation.Horizontal : field;
        set
        {
            field = value;
            var vertical = value == TabsOrientation.Vertical;
            Direction = vertical ? FlexDirection.Row : FlexDirection.Column;
            Align = vertical ? FlexAlign.Start : default;
            Sync();
        }
    }

    /// <summary>焦点移动时是否同时切换，默认 automatic。</summary>
    [Category("Behavior")]
    public TabsActivation Activation { get => field.Value is null ? TabsActivation.Automatic : field; set => field = value; }

    /// <summary>样式，默认 line。</summary>
    [Category("Appearance")]
    public TabsVariant Variant
    {
        get => field.Value is null ? TabsVariant.Line : field;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>字号档位；不写时跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get;
        set
        {
            field = value;
            if (value.Pixels is { } pixels) Font = Fonts.Get(Font.FamilyName(), pixels * 72 / 96, Font.Style);
            else ResetFont();
        }
    }

    bool ShouldSerializeOrientation() => Orientation != TabsOrientation.Horizontal;

    bool ShouldSerializeActivation() => Activation != TabsActivation.Automatic;

    bool ShouldSerializeVariant() => Variant != TabsVariant.Line;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    /// <summary>实际选中的值：写了的，没写时第一个标签的。</summary>
    [Browsable(false)]
    public string? SelectedValue => Value ?? Members<TabItemControl>().FirstOrDefault()?.Value;

    /// <summary>属于这组的成员（不进到里面另一组 TabsControl）。</summary>
    internal IEnumerable<T> Members<T>() where T : Control => Descendants(this).OfType<T>();

    static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            if (child is TabsControl) continue;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    /// <summary>元素属于哪组 TabsControl（父链上最近的一个）。</summary>
    internal static TabsControl? Owner(Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
            if (parent is TabsControl tabs) return tabs;
        return null;
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        Sync();
    }

    /// <summary>选中状态、外观交给标签、列表与面板。</summary>
    internal void Sync()
    {
        var selected = SelectedValue;
        foreach (var tab in Members<TabItemControl>()) tab.Apply(this, tab.Value == selected);
        foreach (var list in Members<TabListControl>()) list.Apply(this);
        foreach (var panel in Members<TabPanelControl>())
        {
            panel.Visible = panel.Value == selected;
            // 竖排时面板占满标签列表右边的宽度；横排时高度按内容
            SetGrow(panel, Orientation == TabsOrientation.Vertical);
        }
        PerformLayout();
    }
}

/// <summary>标签面板：只显示当前标签的那一个；读屏名称是对应标签的文字。</summary>
[ToolboxItem(true)]
[Description("标签面板：对应标签选中时显示。")]
public class TabPanelControl : FlexControl
{
    /// <summary>新建：竖排。</summary>
    public TabPanelControl()
    {
        Direction = FlexDirection.Column;
        AutoSize = true;
        AccessibleRole = AccessibleRole.PropertyPage;
    }

    /// <summary>对应标签的值。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value { get; set; }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new PanelAccessibleObject(this);

    sealed class PanelAccessibleObject(TabPanelControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name
            : TabsControl.Owner(owner)?.Members<TabItemControl>().FirstOrDefault(tab => tab.Value == owner.Value)?.AccessibilityObject.Name;
    }
}
