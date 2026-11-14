using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

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
    [Upstream("Tabs", "值里有中文、空格、下划线时 id 也各不相同，标签与面板照样关联")]
    [Upstream("Tabs", "整组卸载时标签一个个注销，不另选、不写回（使用方据此改地址时不会把离开的导航拽回来）")]
    public Task Any_values_pair_tabs_and_panels_and_unloading_writes_nothing_back() => HeadlessUi.Run(() =>
    {
        string[] values = ["行程", "费用", "a b", "a_b"];
        var list = new TabList();
        foreach (var value in values) list.Children.Add(new Tab { Value = value, Content = $"标签 {value}" });
        var tabs = new Tabs { Value = "费用", Children = { list } };
        var panels = values.Select(value => new TabPanel { Value = value, Content = value }).ToArray();
        foreach (var panel in panels) tabs.Children.Add(panel);
        var host = new StackPanel { Children = { tabs } };
        var window = HeadlessUi.Show(host);

        Assert.Equal(values.Select(value => $"标签 {value}"), panels.Select(panel => panel.Peer().GetName()));
        var writes = 0;
        tabs.PropertyChanged += (_, e) => { if (e.Property == Tabs.ValueProperty) writes++; };
        host.Children.Remove(tabs); // 整组卸载
        HeadlessUi.Flush(window);
        Assert.Equal(("费用", 0), (tabs.Value, writes));
    });

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

    [Fact]
    [Upstream("Tabs", "可关闭：Delete 或关闭按钮关闭；关掉当前标签时选它后面的一个，焦点跟过去")]
    public Task Closing_selects_the_next_tab_and_moves_focus_there() => HeadlessUi.Run(() =>
    {
        var list = new TabList { [AutomationProperties.NameProperty] = "页面" };
        foreach (var value in new[] { "a", "b", "c" })
        {
            var tab = new Tab { Value = value, Content = value.ToUpperInvariant() };
            tab.CloseRequested += (_, _) => list.Children.Remove(tab);
            list.Children.Add(tab);
        }
        var tabs = new Tabs { Value = "b", Children = { list } };
        var window = HeadlessUi.Show(tabs);
        Tab Find(string text) => list.Children.OfType<Tab>().Single(tab => (string?)tab.Content == text);
        IconButton CloseOf(Tab tab) => tab.GetVisualDescendants().OfType<IconButton>().Single(button => button.Name == TabParts.Close);

        // 关闭按钮不在 Tab 顺序里，名称带上标签的文字
        var close = CloseOf(Find("B"));
        Assert.Equal("关闭 B", close.Peer().GetName());
        Assert.False(close.Focusable);
        Assert.Contains(":closable", Find("B").Classes);

        Find("B").Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        HeadlessUi.Flush(window);
        Assert.Equal(["A", "C"], list.Children.OfType<Tab>().Select(tab => (string?)tab.Content));
        Assert.Equal("c", tabs.Value);
        Assert.True(Find("C").IsFocused);

        // 关掉最后一个：选前面的
        Click(window, CloseOf(Find("C")));
        HeadlessUi.Flush(window);
        Assert.Equal("a", tabs.Value);
    });

    [Fact]
    public Task Middle_click_closes_and_pressing_the_close_button_does_not_take_focus() => HeadlessUi.Run(() =>
    {
        var (tabs, items, _) = Build();
        var closed = new List<string?>();
        foreach (var item in items) item.CloseRequested += (_, _) => closed.Add(item.Value);
        var window = HeadlessUi.Show(tabs);
        items[0].Focus(NavigationMethod.Tab);

        var at = items[2].TranslatePoint(new Point(4, items[2].Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Middle);
        window.MouseUp(at, MouseButton.Middle);
        Assert.Equal(["done"], closed);

        Click(window, items[1].GetVisualDescendants().OfType<IconButton>().Single());
        Assert.Equal(["done", "paid"], closed);
        Assert.True(items[0].IsFocused); // 按下关闭按钮不把焦点挪到那个标签上
        Assert.Equal("all", tabs.SelectedValue);
    });

    [Fact]
    [Upstream("Tabs", "新建按钮在标签栏里、tablist 外面；Alt + → 把标签往后挪一位并报告新顺序")]
    public Task Add_button_sits_outside_the_tabs_and_alt_arrow_reorders() => HeadlessUi.Run(() =>
    {
        var list = new TabList { [AutomationProperties.NameProperty] = "页面" };
        foreach (var value in new[] { "a", "b", "c" }) list.Children.Add(new Tab { Value = value, Content = value });
        var added = 0;
        IReadOnlyList<string>? order = null;
        list.AddRequested += (_, _) => added++;
        list.Reordered += (_, e) => order = e.Values;
        var window = HeadlessUi.Show(new Tabs { Children = { list } });

        var add = list.AddButton!;
        Assert.Equal("新建标签页", add.Peer().GetName());
        Assert.DoesNotContain(add, list.Children);
        Click(window, add);
        Assert.Equal(1, added);

        var first = (Tab)list.Children[0];
        first.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Alt);
        Assert.Equal(["b", "a", "c"], order);
        Assert.Equal(["b", "a", "c"], list.Children.OfType<Tab>().Select(tab => tab.Value));
        Assert.True(first.IsFocused);
        Assert.Equal("a移到第 2 个，共 3 个", list.Announcement);
    });

    [Fact]
    public Task Dragging_a_tab_past_the_next_ones_center_swaps_them() => HeadlessUi.Run(() =>
    {
        var (tabs, items, _) = Build();
        var list = (TabList)tabs.Children[0];
        IReadOnlyList<string>? order = null;
        list.Reordered += (_, e) => order = e.Values;
        var window = HeadlessUi.Show(tabs);

        Point At(Control control, double x) => control.TranslatePoint(new Point(x, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(At(items[0], 4), MouseButton.Left);
        window.MouseMove(At(items[1], 2)); // 还没过中线
        Assert.Null(order);
        Assert.Contains(":dragging", items[0].Classes);
        window.MouseMove(At(items[1], items[1].Bounds.Width - 2));
        Assert.Equal(["paid", "all", "done"], order);
        window.MouseUp(At(items[0], 4), MouseButton.Left);
        Assert.DoesNotContain(":dragging", items[0].Classes);
    });

    [Fact]
    public Task Overflowing_tabs_scroll_with_buttons_and_the_all_tabs_menu_switches() => HeadlessUi.Run(() =>
    {
        var list = new TabList { ShowAllMenu = true };
        foreach (var index in Enumerable.Range(1, 12)) list.Children.Add(new Tab { Value = $"t{index}", Content = $"标签页 {index}" });
        var tabs = new Tabs { Width = 360, Children = { list } };
        var window = HeadlessUi.Show(tabs);

        var (previous, next) = list.ScrollButtons;
        Assert.True(previous.IsVisible && next.IsVisible);
        Assert.False(previous.IsEnabled); // 在起始端：往前滚不可用
        Assert.NotNull(list.Scroller.OpacityMask); // 结束端渐隐
        Click(window, next);
        HeadlessUi.Flush(window);
        Assert.True(list.Scroller.Offset.X > 0);
        Assert.True(previous.IsEnabled);

        // 菜单按显示的顺序列出标签，选一项切过去并滚进来
        var items = list.AllMenu!.Items.OfType<MenuRadioGroup>().Single().Items.OfType<MenuRadioItem>().ToArray();
        Assert.Equal(12, items.Length);
        Assert.Equal("标签页 12", items[^1].Content);
        Assert.Equal("全部标签页", list.AllMenu.Trigger!.Peer().GetName());
        items[^1].Run();
        HeadlessUi.Flush(window);
        Assert.Equal("t12", tabs.Value);
        Assert.Equal(list.Scroller.Extent.Width - list.Scroller.Viewport.Width, list.Scroller.Offset.X, 0.5);
        Assert.False(next.IsEnabled);
    });

    [Fact]
    public Task A_close_command_makes_the_tab_closable_and_respects_can_execute() => HeadlessUi.Run(() =>
    {
        var allowed = false;
        var command = new TestCommand(_ => allowed);
        var tab = new Tab { Value = "a", Content = "A", CloseCommand = command, CloseCommandParameter = "a" };

        Assert.True(tab.IsClosable);
        tab.RequestClose();
        Assert.Empty(command.Executed); // 不能执行：不调
        allowed = true;
        tab.RequestClose();
        Assert.Equal(["a"], command.Executed);
    });
}
