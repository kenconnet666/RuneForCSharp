using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>
/// Tabs：没有值时选第一个、只显示当前面板、面板的读屏名称是标签的文字；点击切换并写回、不可用的点不了、点击处理可以拦下；
/// automatic 时焦点到哪个切到哪个（方向键跳过不可用项），manual 时只移动焦点。
/// </summary>
public class TabsTests
{
    static (Tabs Tabs, Tab[] Items, TabPanel[] Panels, StackPanel Host) Build(bool disableSecond = false)
    {
        var list = new TabList();
        var items = new[] { "all", "paid", "done" }.Select((value, index) => new Tab { Value = value, Content = $"标签{index + 1}", IsEnabled = !(disableSecond && index == 1) }).ToArray();
        foreach (var item in items) list.Children.Add(item);
        var panels = items.Select(item => new TabPanel { Value = item.Value, Content = $"面板 {item.Value}" }).ToArray();
        var tabs = new Tabs();
        tabs.Children.Add(list);
        foreach (var panel in panels) tabs.Children.Add(panel);
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(tabs);
        return (tabs, items, panels, host);
    }

    [Fact]
    [Upstream("Tabs", "值里有中文、空格、下划线时 id 也各不相同，标签与面板照样关联")]
    [Upstream("Tabs", "整组卸载时标签一个个注销，不另选、不写回（使用方据此改地址时不会把离开的导航拽回来）")]
    public Task Any_values_pair_tabs_and_panels_and_unloading_writes_nothing_back() => WpfUi.Run(() =>
    {
        string[] values = ["行程", "费用", "a b", "a_b"];
        var list = new TabList();
        foreach (var value in values) list.Children.Add(new Tab { Value = value, Content = $"标签 {value}" });
        var tabs = new Tabs { Value = "费用" };
        tabs.Children.Add(list);
        var panels = values.Select(value => new TabPanel { Value = value, Content = value }).ToArray();
        foreach (var panel in panels) tabs.Children.Add(panel);
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(tabs);
        WpfUi.Show(host);

        Assert.Equal(values.Select(value => $"标签 {value}"), panels.Select(panel => panel.Peer().GetName()));
        var writes = 0;
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Tabs.ValueProperty, typeof(Tabs)).AddValueChanged(tabs, (_, _) => writes++);
        host.Children.Remove(tabs); // 整组卸载
        WpfUi.Flush();
        Assert.Equal(("费用", 0), (tabs.Value, writes));
    });

    static void Click(Tab tab) => ((IInvokeProvider)tab.Peer().GetPattern(PatternInterface.Invoke)).Invoke();

    static void Press(UIElement owner, Key key)
    {
        owner.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(owner), 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("Tabs", "没有选中值时选第一个；标签与面板互相关联；只显示当前面板")]
    public Task The_first_tab_is_selected_and_only_its_panel_shows() => WpfUi.Run(() =>
    {
        var (tabs, items, panels, host) = Build();
        WpfUi.Show(host);

        Assert.Null(tabs.Value);
        Assert.Equal("all", tabs.SelectedValue);
        Assert.Equal([true, false, false], items.Select(item => item.IsSelected));
        Assert.Equal([Visibility.Visible, Visibility.Collapsed, Visibility.Collapsed], panels.Select(panel => panel.Visibility));
        Assert.Equal("标签1", panels[0].Peer().GetName());
        Assert.Equal(AutomationControlType.TabItem, items[0].Peer().GetAutomationControlType());
        Assert.True(((ISelectionItemProvider)items[0].Peer().GetPattern(PatternInterface.SelectionItem)).IsSelected);
    });

    [Fact]
    [Upstream("Tabs", "点击切换并写回；不可用的标签点不了")]
    [Upstream("Tabs", "使用方的 onClick 先执行，preventDefault 可以拦下切换")]
    public Task Clicking_selects_and_click_handlers_can_cancel() => WpfUi.Run(() =>
    {
        var (tabs, items, panels, host) = Build(disableSecond: true);
        WpfUi.Show(host);

        Click(items[2]);
        WpfUi.Flush();
        Assert.Equal("done", tabs.Value);
        Assert.Equal(Visibility.Visible, panels[2].Visibility);
        Assert.Equal(Visibility.Collapsed, panels[0].Visibility);

        items[0].Click += (_, e) => e.Handled = true;
        Click(items[0]);
        WpfUi.Flush();
        Assert.Equal("done", tabs.Value); // 点击处理拦下了切换
    });

    [Fact]
    [Upstream("Tabs", "automatic：焦点移到哪个就切换哪个（方向键跳过不可用项）；manual：只移动焦点")]
    public Task Arrow_keys_move_focus_and_select_in_automatic_mode() => WpfUi.Run(() =>
    {
        var (tabs, items, _, host) = Build(disableSecond: true);
        var window = WpfUi.Show(host);
        window.Activate();
        items[0].Focus();
        WpfUi.Flush();

        Press(items[0], Key.Right);
        Assert.True(items[2].IsKeyboardFocused); // 跳过不可用的第二个
        Assert.Equal("done", tabs.Value);

        tabs.Activation = TabsActivation.Manual;
        Press(items[2], Key.Right);
        Assert.True(items[0].IsKeyboardFocused); // 到头后从另一端继续
        Assert.Equal("done", tabs.Value); // manual：只移动焦点
    });

    [Fact]
    public Task Variants_and_orientation_reach_the_list_and_tabs() => WpfUi.Run(() =>
    {
        var (tabs, items, _, host) = Build();
        WpfUi.Show(host);
        var list = (TabList)tabs.Children[0];

        tabs.Variant = TabsVariant.Soft;
        tabs.Orientation = TabsOrientation.Vertical;
        Assert.Equal(TabsVariant.Soft, items[0].Variant);
        Assert.Equal(TabsOrientation.Vertical, items[0].Orientation);
        Assert.Equal(FlexDirection.Row, tabs.Direction);
        Assert.Equal(FlexDirection.Column, list.Direction);
        Assert.Equal(host.ThemeColor("Rune.Brush.Surface"), Lookup.ColorOf(items[0].Background)); // soft 的选中标签 surface 底
    });
}
