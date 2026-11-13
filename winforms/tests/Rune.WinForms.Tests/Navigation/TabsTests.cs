using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>
/// TabsControl：没有值时选第一个、只显示当前面板、面板的读屏名称是标签的文字；点击切换、不可用的点不了、TabClick 可以拦下；
/// automatic 时焦点到哪个切到哪个（方向键跳过不可用项），manual 时只移动焦点。
/// </summary>
public class TabsTests
{
    static (TabsControl Tabs, TabListControl List, TabItemControl[] Items, TabPanelControl[] Panels) Build(bool disableSecond = false)
    {
        var tabs = new TabsControl();
        var list = new TabListControl();
        var items = new[] { "all", "paid", "done" }.Select((value, index) => new TabItemControl { Value = value, Text = $"标签{index + 1}", Enabled = !(disableSecond && index == 1) }).ToArray();
        list.Controls.AddRange(items);
        var panels = items.Select(item => new TabPanelControl { Value = item.Value }).ToArray();
        tabs.Controls.Add(list);
        tabs.Controls.AddRange(panels);
        return (tabs, list, items, panels);
    }

    static bool Press(Control control, Keys key) =>
        (bool)typeof(Control).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [key])!;

    [Fact]
    [Upstream("Tabs", "值里有中文、空格、下划线时 id 也各不相同，标签与面板照样关联")]
    [Upstream("Tabs", "整组卸载时标签一个个注销，不另选、不写回（使用方据此改地址时不会把离开的导航拽回来）")]
    public Task Any_values_pair_tabs_and_panels_and_unloading_writes_nothing_back() => WinFormsUi.Run(() =>
    {
        string[] values = ["行程", "费用", "a b", "a_b"];
        var tabs = new TabsControl { Value = "费用" };
        var list = new TabListControl();
        list.Controls.AddRange([.. values.Select(value => new TabItemControl { Value = value, Text = $"标签 {value}" })]);
        var panels = values.Select(value => new TabPanelControl { Value = value }).ToArray();
        tabs.Controls.Add(list);
        tabs.Controls.AddRange(panels);
        var host = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add(tabs);
        using var form = WinFormsUi.Show(host);

        Assert.Equal(values.Select(value => $"标签 {value}"), panels.Select(panel => panel.AccessibilityObject.Name));
        var writes = 0;
        tabs.ValueChanged += (_, _) => writes++;
        host.Controls.Remove(tabs); // 整组卸载
        tabs.Dispose();
        Assert.Equal(("费用", 0), (tabs.Value, writes));
    });

    [Fact]
    [Upstream("Tabs", "没有选中值时选第一个；标签与面板互相关联；只显示当前面板")]
    public Task The_first_tab_is_selected_and_only_its_panel_shows() => WinFormsUi.Run(() =>
    {
        var (tabs, _, items, panels) = Build();
        using var form = WinFormsUi.Show(tabs);

        Assert.Null(tabs.Value);
        Assert.Equal("all", tabs.SelectedValue);
        Assert.Equal([true, false, false], items.Select(item => item.IsSelected));
        Assert.Equal([true, false, false], panels.Select(panel => panel.Visible));
        Assert.Equal("标签1", panels[0].AccessibilityObject.Name);
        Assert.Equal(AccessibleRole.PageTab, items[0].AccessibilityObject.Role);
        Assert.True(items[0].AccessibilityObject.State.HasFlag(AccessibleStates.Selected));
    });

    [Fact]
    [Upstream("Tabs", "点击切换并写回；不可用的标签点不了")]
    [Upstream("Tabs", "使用方的 onClick 先执行，preventDefault 可以拦下切换")]
    public Task Clicking_selects_and_click_handlers_can_cancel() => WinFormsUi.Run(() =>
    {
        var (tabs, _, items, panels) = Build(disableSecond: true);
        using var form = WinFormsUi.Show(tabs);
        string? changed = null;
        tabs.ValueChanged += (_, _) => changed = tabs.Value;

        items[2].Activate();
        Assert.Equal(("done", "done"), (tabs.Value, changed));
        Assert.True(panels[2].Visible);
        Assert.False(panels[0].Visible);

        items[1].Activate();
        Assert.Equal("done", tabs.Value); // 不可用的点不了

        items[0].TabClick += (_, e) => e.Cancel = true;
        items[0].Activate();
        Assert.Equal("done", tabs.Value); // TabClick 拦下了切换
    });

    [Fact]
    [Upstream("Tabs", "automatic：焦点移到哪个就切换哪个（方向键跳过不可用项）；manual：只移动焦点")]
    public Task Arrow_keys_move_focus_and_select_in_automatic_mode() => WinFormsUi.Run(() =>
    {
        var (tabs, list, items, _) = Build(disableSecond: true);
        using var form = WinFormsUi.Show(tabs);
        items[0].Focus();

        Assert.True(Press(list, Keys.Right));
        Assert.True(items[2].Focused); // 跳过不可用的第二个
        Assert.Equal("done", tabs.Value);
        Assert.Equal([false, false, true], items.Select(item => item.TabStop)); // 整组一个 Tab 停靠点

        tabs.Activation = TabsActivation.Manual;
        Assert.True(Press(list, Keys.Right));
        Assert.True(items[0].Focused); // 到头后从另一端继续
        Assert.Equal("done", tabs.Value); // manual：只移动焦点
    });
}
