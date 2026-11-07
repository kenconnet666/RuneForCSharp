using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Tabs：没有值时选第一个、只显示当前面板、面板的读屏名称是标签的文字；点击切换并写回、不可用的点不了、点击处理可以拦下；
/// automatic 时焦点到哪个切到哪个（方向键跳过不可用项），manual 时只移动焦点；line 的指示条对准当前标签。
/// </summary>
public class TabsTests
{
    static (Tabs Tabs, Tab[] Items, TabPanel[] Panels) Build(TabsActivation? activation = null, bool disableSecond = false)
    {
        var list = new TabList();
        var items = new[] { "all", "paid", "done" }.Select((value, index) => new Tab { Value = value, Content = $"标签{index + 1}", IsEnabled = !(disableSecond && index == 1) }).ToArray();
        foreach (var item in items) list.Children.Add(item);
        var panels = items.Select(item => new TabPanel { Value = item.Value, Content = $"面板 {item.Value}" }).ToArray();
        var tabs = new Tabs { Activation = activation ?? TabsActivation.Automatic, Children = { list } };
        foreach (var panel in panels) tabs.Children.Add(panel);
        return (tabs, items, panels);
    }

    static void Click(Window window, Control control)
    {
        var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
    }

    [Fact]
    [Upstream("Tabs", "没有选中值时选第一个；标签与面板互相关联；只显示当前面板")]
    public Task The_first_tab_is_selected_and_only_its_panel_shows() => HeadlessUi.Run(() =>
    {
        var (tabs, items, panels) = Build();
        HeadlessUi.Show(tabs);

        Assert.Null(tabs.Value);
        Assert.Equal("all", tabs.SelectedValue);
        Assert.Equal([true, false, false], items.Select(item => item.IsSelected));
        Assert.Equal([true, false, false], panels.Select(panel => panel.IsVisible));
        Assert.Equal("标签1", panels[0].Peer().GetName());
        Assert.Equal(AutomationControlType.TabItem, items[0].Peer().GetAutomationControlType());
        Assert.True(((ISelectionItemProvider)items[0].Peer()).IsSelected);
    });

    [Fact]
    [Upstream("Tabs", "点击切换并写回；不可用的标签点不了")]
    [Upstream("Tabs", "使用方的 onClick 先执行，preventDefault 可以拦下切换")]
    public Task Clicking_selects_and_click_handlers_can_cancel() => HeadlessUi.Run(() =>
    {
        var (tabs, items, panels) = Build(disableSecond: true);
        var window = HeadlessUi.Show(tabs);

        Click(window, items[2]);
        Assert.Equal("done", tabs.Value);
        Assert.True(panels[2].IsVisible);
        Assert.False(panels[0].IsVisible);

        Click(window, items[1]);
        Assert.Equal("done", tabs.Value); // 不可用的点不了

        items[0].Click += (_, e) => e.Handled = true;
        Click(window, items[0]);
        Assert.Equal("done", tabs.Value); // 点击处理拦下了切换
    });

    [Fact]
    [Upstream("Tabs", "automatic：焦点移到哪个就切换哪个（方向键跳过不可用项）；manual：只移动焦点")]
    public Task Arrow_keys_move_focus_and_select_in_automatic_mode() => HeadlessUi.Run(() =>
    {
        var (tabs, items, _) = Build(disableSecond: true);
        var window = HeadlessUi.Show(tabs);
        items[0].Focus(NavigationMethod.Tab);

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.True(items[2].IsFocused); // 跳过不可用的第二个
        Assert.Equal("done", tabs.Value);

        tabs.Activation = TabsActivation.Manual;
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.True(items[0].IsFocused); // 到头后从另一端继续
        Assert.Equal("done", tabs.Value); // manual：只移动焦点
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal("all", tabs.Value);
    });

    [Fact]
    public Task Variants_and_orientation_reach_the_list_and_tabs() => HeadlessUi.Run(() =>
    {
        var (tabs, items, _) = Build();
        HeadlessUi.Show(tabs);
        var list = (TabList)tabs.Children[0];
        Assert.Contains(":line", list.Classes);
        Assert.Contains(":selected", items[0].Classes);

        tabs.Variant = TabsVariant.Soft;
        tabs.Orientation = TabsOrientation.Vertical;
        Assert.Contains(":soft", items[0].Classes);
        Assert.Contains(":vertical", list.Classes);
        Assert.Equal(FlexDirection.Row, tabs.Direction);
        Assert.Equal(FlexDirection.Column, list.Direction);
    });
}
