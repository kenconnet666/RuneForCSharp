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

    static StackPanel Host(UIElement content)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(content);
        return host;
    }

    static void Invoke(UIElement button)
    {
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button).GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("Tabs", "可关闭：Delete 或关闭按钮关闭；关掉当前标签时选它后面的一个，焦点跟过去")]
    public Task Closing_selects_the_next_tab_and_moves_focus_there() => WpfUi.Run(() =>
    {
        var list = new TabList();
        System.Windows.Automation.AutomationProperties.SetName(list, "页面");
        foreach (var value in new[] { "a", "b", "c" })
        {
            var tab = new Tab { Value = value, Content = value.ToUpperInvariant() };
            tab.CloseRequested += (_, _) => list.Children.Remove(tab);
            list.Children.Add(tab);
        }
        var tabs = new Tabs { Value = "b" };
        tabs.Children.Add(list);
        var window = WpfUi.Show(Host(tabs));
        window.Activate();
        Tab Find(string text) => list.Children.OfType<Tab>().Single(tab => (string?)tab.Content == text);

        // 关闭按钮不在 Tab 顺序里、读屏看不到，名称带上标签的文字
        var close = Find("B").CloseButton!;
        Assert.Equal("关闭 B", close.Peer().GetName());
        Assert.False(close.Focusable);
        Assert.True(Find("B").IsClosable);
        Assert.DoesNotContain(Find("B").Peer().GetChildren() ?? [], peer => (peer as UIElementAutomationPeer)?.Owner == close);

        Find("B").Focus();
        WpfUi.Flush();
        Press(Find("B"), Key.Delete);
        Assert.Equal(["A", "C"], list.Children.OfType<Tab>().Select(tab => (string?)tab.Content));
        Assert.Equal("c", tabs.Value);
        Assert.True(Find("C").IsKeyboardFocused);

        // 关掉最后一个：选前面的
        Invoke(Find("C").CloseButton!);
        WpfUi.Flush();
        Assert.Equal("a", tabs.Value);
    });

    [Fact]
    [Upstream("Tabs", "新建按钮在标签栏里、tablist 外面；Alt + → 把标签往后挪一位并报告新顺序")]
    public Task Add_button_sits_outside_the_tabs_and_alt_arrow_reorders() => WpfUi.Run(() =>
    {
        var list = new TabList();
        foreach (var value in new[] { "a", "b", "c" }) list.Children.Add(new Tab { Value = value, Content = value });
        var added = 0;
        IReadOnlyList<string>? order = null;
        list.AddRequested += (_, _) => added++;
        list.Reordered += (_, e) => order = e.Values;
        var tabs = new Tabs();
        tabs.Children.Add(list);
        var window = WpfUi.Show(Host(tabs));
        window.Activate();

        var add = list.AddButton!;
        Assert.Equal("新建标签页", add.Peer().GetName());
        Assert.DoesNotContain(add, list.Children.OfType<UIElement>());
        Invoke(add);
        Assert.Equal(1, added);

        var first = (Tab)list.Children[0];
        first.Focus();
        WpfUi.Flush();
        Assert.True(list.OnReorderKey(first, Key.Right, ModifierKeys.Alt));
        WpfUi.Flush();
        Assert.Equal(["b", "a", "c"], order);
        Assert.Equal(["b", "a", "c"], list.Children.OfType<Tab>().Select(tab => tab.Value));
        Assert.True(first.IsKeyboardFocused);
        Assert.Equal("a移到第 2 个，共 3 个", list.Announcement);
        Assert.Equal("b", tabs.SelectedValue); // 挪动不算关闭：不另选
        Assert.False(list.OnReorderKey(first, Key.Right, ModifierKeys.None));
    });

    [Fact]
    public Task Overflowing_tabs_scroll_with_buttons_and_the_all_tabs_menu_switches() => WpfUi.Run(() =>
    {
        var list = new TabList { ShowAllMenu = true };
        foreach (var index in Enumerable.Range(1, 12)) list.Children.Add(new Tab { Value = $"t{index}", Content = $"标签页 {index}" });
        var tabs = new Tabs { Width = 360 };
        tabs.Children.Add(list);
        WpfUi.Show(Host(tabs));

        var (previous, next) = list.ScrollButtons;
        Assert.True(previous.IsVisible && next.IsVisible);
        Assert.False(previous.IsEnabled); // 在起始端：往前滚不可用
        Assert.NotNull(list.Scroller.OpacityMask); // 结束端渐隐
        Invoke(next);
        WpfUi.Flush();
        Assert.True(list.Scroller.HorizontalOffset > 0);
        Assert.True(previous.IsEnabled);

        // 菜单按显示的顺序列出标签，选一项切过去并滚进来
        var items = list.AllMenu!.Items.OfType<MenuRadioGroup>().Single().Items.OfType<MenuRadioItem>().ToArray();
        Assert.Equal(12, items.Length);
        Assert.Equal("标签页 12", items[^1].Content);
        Assert.Equal("全部标签页", list.AllMenu.Trigger!.Peer().GetName());
        items[^1].Run();
        WpfUi.Flush();
        Assert.Equal("t12", tabs.Value);
        Assert.Equal(list.Scroller.ScrollableWidth, list.Scroller.HorizontalOffset, 0.5);
        Assert.False(next.IsEnabled);
    });

    [Fact]
    public Task A_close_command_makes_the_tab_closable_and_respects_can_execute() => WpfUi.Run(() =>
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
