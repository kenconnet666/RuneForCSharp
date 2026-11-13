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

    static void Raise(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [args]);

    [Fact]
    [Upstream("Tabs", "可关闭：Delete 或关闭按钮关闭；关掉当前标签时选它后面的一个，焦点跟过去")]
    public Task Closing_selects_the_next_tab_and_moves_focus_there() => WinFormsUi.Run(() =>
    {
        var tabs = new TabsControl { Value = "b" };
        var list = new TabListControl { AccessibleName = "页面" };
        foreach (var value in new[] { "a", "b", "c" })
        {
            var tab = new TabItemControl { Value = value, Text = value.ToUpperInvariant() };
            tab.CloseRequested += (_, _) => list.Controls.Remove(tab);
            list.Controls.Add(tab);
        }
        tabs.Controls.Add(list);
        using var form = WinFormsUi.Show(tabs);
        TabItemControl Find(string text) => list.Controls.OfType<TabItemControl>().Single(tab => tab.Text == text);

        // 关闭按钮不在 Tab 顺序里，名称带上标签的文字；标签给它留出位置
        var close = Find("B").CloseButton!;
        Assert.Equal("关闭 B", close.AccessibleName);
        Assert.False(close.TabStop);
        Assert.True(close.Right - close.Padding.Right <= Find("B").Width); // 看得见的按钮在标签里（四周是焦点环的位置）
        Assert.Equal("Delete", Find("B").AccessibilityObject.KeyboardShortcut);

        Find("B").Focus();
        Raise(Find("B"), "OnKeyDown", new KeyEventArgs(Keys.Delete));
        WinFormsUi.Pump();
        Assert.Equal(["A", "C"], list.Controls.OfType<TabItemControl>().Select(tab => tab.Text));
        Assert.Equal("c", tabs.Value);
        Assert.True(Find("C").Focused);

        // 关掉最后一个：选前面的；中键也关
        Find("C").CloseButton!.PerformClick();
        WinFormsUi.Pump();
        Assert.Equal("a", tabs.Value);
        var closed = false;
        Find("A").CloseRequested += (_, _) => closed = true;
        Raise(Find("A"), "OnMouseClick", new MouseEventArgs(MouseButtons.Middle, 1, 4, 4, 0));
        Assert.True(closed);
    });

    [Fact]
    [Upstream("Tabs", "新建按钮在标签栏里、tablist 外面；Alt + → 把标签往后挪一位并报告新顺序")]
    public Task Add_button_sits_outside_the_tabs_and_alt_arrow_reorders() => WinFormsUi.Run(() =>
    {
        var tabs = new TabsControl();
        var list = new TabListControl();
        list.Controls.AddRange([.. new[] { "a", "b", "c" }.Select(value => new TabItemControl { Value = value, Text = value })]);
        var added = 0;
        IReadOnlyList<string>? order = null;
        list.AddRequested += (_, _) => added++;
        list.Reordered += (_, e) => order = e.Values;
        tabs.Controls.Add(list);
        using var form = WinFormsUi.Show(tabs);

        var add = list.AddButton!;
        Assert.Equal("新建标签页", add.AccessibleName);
        Assert.True(add.Left >= list.Controls.OfType<TabItemControl>().Max(tab => tab.Right)); // 在标签后面
        add.PerformClick();
        Assert.Equal(1, added);

        var first = list.Controls.OfType<TabItemControl>().First();
        first.Focus();
        Assert.True(Press(list, Keys.Alt | Keys.Right));
        Assert.Equal(["b", "a", "c"], order);
        Assert.Equal(["b", "a", "c"], list.Controls.OfType<TabItemControl>().Select(tab => tab.Value));
        Assert.True(first.Focused);
        Assert.Equal("a移到第 2 个，共 3 个", list.Announcement);
        Assert.Equal("b", tabs.SelectedValue); // 挪动不算关闭：不另选
    });

    [Fact]
    public Task Dragging_a_tab_past_the_next_ones_center_swaps_them() => WinFormsUi.Run(() =>
    {
        var (tabs, list, items, _) = Build();
        IReadOnlyList<string>? order = null;
        list.Reordered += (_, e) => order = e.Values;
        using var form = WinFormsUi.Show(tabs);

        // 标签按下时接管指针：移动的坐标都相对第一个标签
        Raise(items[0], "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0));
        Raise(items[0], "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, items[1].Left - items[0].Left + 2, 4, 0)); // 还没过中线
        Assert.Null(order);
        Assert.True(items[0].IsDragging);
        Raise(items[0], "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, items[1].Right - items[0].Left - 2, 4, 0));
        Assert.Equal(["paid", "all", "done"], order);
        Raise(items[0], "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0));
        Assert.False(items[0].IsDragging);
    });

    [Fact]
    public Task Overflowing_tabs_scroll_with_buttons_and_the_all_tabs_menu_switches() => WinFormsUi.Run(() =>
    {
        var tabs = new TabsControl { AutoSize = false, Width = 360, Height = 200 };
        var list = new TabListControl { ShowAllMenu = true };
        list.Controls.AddRange([.. Enumerable.Range(1, 12).Select(index => new TabItemControl { Value = $"t{index}", Text = $"标签页 {index}" })]);
        tabs.Controls.Add(list);
        using var form = WinFormsUi.Show(tabs);

        var (previous, next) = list.ScrollButtons;
        Assert.True(previous.Visible && next.Visible);
        Assert.False(previous.Enabled); // 在起始端：往前滚不可用
        Assert.True(next.Right <= list.Width);
        next.PerformClick();
        Assert.True(list.ScrollOffset > 0);
        Assert.True(previous.Enabled);

        // 菜单按显示的顺序列出标签，选一项切过去并滚进来
        var items = list.AllMenu!.Items.OfType<MenuRadioGroupControl>().Single().Controls.OfType<MenuRadioItemControl>().ToArray();
        Assert.Equal(12, items.Length);
        Assert.Equal("标签页 12", items[^1].Text);
        Assert.Equal("全部标签页", list.AllMenu.Trigger!.AccessibleName);
        items[^1].PerformClick();
        WinFormsUi.Pump();
        Assert.Equal("t12", tabs.Value);
        Assert.False(next.Enabled); // 滚到了尽头
        var last = list.Controls.OfType<TabItemControl>().Last();
        Assert.True(last.Right <= next.Left);
    });
}
