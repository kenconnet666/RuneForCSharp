using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Menu：触发器打开，↓ 聚焦第一项、↑ 聚焦最后一项；方向键循环移动、按字母跳；执行一项后关闭（原因 select），复选项不关；Esc 关闭并回到触发器，Tab 关闭往后走；
/// 快捷键在菜单关着时也执行；说明是帮助文字；面板以触发器为名称（写了名称时用它）；分组以标题为名称，单选组跟着 Value 勾选、点了写回。
/// </summary>
public class MenuTests
{
    static void Press(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None, string? symbol = null) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, KeySymbol = symbol, Source = target });

    static Control? Focused(Visual visual) => TopLevel.GetTopLevel(visual)?.FocusManager?.GetFocusedElement() as Control;

    [Fact]
    [Upstream("Menu 的说明", "有说明时名称指向文字、描述指向说明；没有说明时两者都不加")]
    public Task Descriptions_are_help_text_not_names() => HeadlessUi.Run(() =>
    {
        var document = new MenuItem { Content = "文档", Description = "空白的文字文档" };
        var code = new MenuCheckboxItem { Content = "代码视图", Description = "显示行号与折叠" };
        var help = new MenuLinkItem { Content = "帮助", Description = "使用说明", Href = new Uri("https://example.com/help") };
        var folder = new MenuItem { Content = "文件夹" };
        HeadlessUi.Show(new Menu { Trigger = new Button { Content = "新建" }, Items = { document, code, help, folder }, IsOpen = true });

        Assert.Equal(
            new[] { ("文档", "空白的文字文档"), ("代码视图", "显示行号与折叠"), ("帮助", "使用说明"), ("文件夹", "") }, // 没有说明时帮助文字是空的
            new MenuItemBase[] { document, code, help, folder }.Select(item => (item.Peer().GetName(), item.Peer().GetHelpText())));
        Assert.Equal(AutomationControlType.MenuItem, document.Peer().GetAutomationControlType());
    });

    [Fact]
    [Upstream("弹层的名称", "默认以触发器的文字为名称；写了 aria-label 时不再加 aria-labelledby（它会盖过 aria-label）")]
    public Task The_panel_is_named_by_the_trigger_unless_named() => HeadlessUi.Run(() =>
    {
        var byTrigger = new Menu { Trigger = new Button { Content = "更多" }, Items = { new MenuItem { Content = "复制" } } };
        var named = new Menu { Trigger = new Button { Content = "…" }, Items = { new MenuItem { Content = "删除" } } };
        AutomationProperties.SetName(named, "行操作");
        HeadlessUi.Show(new StackPanel { Children = { byTrigger, named } });

        Assert.Equal(AutomationControlType.Menu, byTrigger.Panel.Peer().GetAutomationControlType());
        Assert.Equal("更多", byTrigger.Panel.Peer().GetName());
        Assert.Null(AutomationProperties.GetLabeledBy(named.Panel));
        Assert.Equal("行操作", named.Panel.Peer().GetName());
    });

    [Fact]
    [Upstream("MenuGroup、MenuRadioGroup", "分组以标题为名称；单选项跟着 _value 勾选，点另一项写回")]
    public Task Groups_are_named_and_radio_items_follow_the_value() => HeadlessUi.Run(() =>
    {
        var refresh = new MenuGroup { Label = "操作", Items = { new MenuItem { Content = "刷新" } } };
        var byName = new MenuRadioItem { Content = "名称", Value = "name" };
        var byDate = new MenuRadioItem { Content = "日期", Value = "date" };
        var sort = new MenuRadioGroup { Label = "排序", Value = "name", Items = { byName, byDate } };
        HeadlessUi.Show(new Menu { Trigger = new Button { Content = "视图" }, Items = { refresh, sort }, IsOpen = true });

        Assert.Equal(["操作", "排序"], new MenuGroup[] { refresh, sort }.Select(group => group.Peer().GetName()));
        Assert.Equal(AutomationControlType.Group, sort.Peer().GetAutomationControlType());
        ToggleState State(MenuRadioItem item) => ((IToggleProvider)item.Peer()).ToggleState;
        Assert.Equal([ToggleState.On, ToggleState.Off], new[] { byName, byDate }.Select(State));
        ((IInvokeProvider)byDate.Peer()).Invoke();
        Assert.Equal("date", sort.Value);
        Assert.Equal([ToggleState.Off, ToggleState.On], new[] { byName, byDate }.Select(State));
    });

    [Fact]
    [Upstream("Popover 与 Menu 的 onOpenChange", "Menu：执行一项是 select")]
    public Task Running_an_item_closes_with_select_but_checkboxes_stay() => HeadlessUi.Run(() =>
    {
        var ran = 0;
        var rename = new MenuItem { Content = "重命名" };
        rename.Click += (_, _) => ran++;
        var grid = new MenuCheckboxItem { Content = "显示网格" };
        var menu = new Menu { Trigger = new Button { Content = "操作" }, Items = { grid, rename } };
        var reasons = new List<(bool, OpenChangeReason)>();
        menu.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        var window = HeadlessUi.Show(menu);
        menu.IsOpen = true;
        HeadlessUi.Flush(window);

        ((IInvokeProvider)grid.Peer()).Invoke();
        Assert.True(grid.IsChecked);
        Assert.True(menu.IsOpen); // 复选项默认不关
        ((IInvokeProvider)rename.Peer()).Invoke();
        Assert.Equal(1, ran);
        Assert.False(menu.IsOpen);
        Assert.Equal([(false, OpenChangeReason.Select)], reasons);
    });

    [Fact]
    public Task Keyboard_opens_moves_types_ahead_and_closes() => HeadlessUi.Run(() =>
    {
        var trigger = new Button { Content = "操作" };
        var (copy, disabled, export, remove) = (new MenuItem { Content = "复制" }, new MenuItem { Content = "粘贴", IsEnabled = false }, new MenuItem { Content = "导出" }, new MenuItem { Content = "删除", Tone = MenuItemTone.Danger });
        var menu = new Menu { Trigger = trigger, Items = { copy, disabled, export, remove } };
        var reasons = new List<(bool, OpenChangeReason)>();
        menu.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        var window = HeadlessUi.Show(new StackPanel { Children = { menu, new Button { Content = "下一个" } } });
        trigger.Focus();

        Press(trigger, Key.Up); // ↑ 打开并聚焦最后一项
        HeadlessUi.Flush(window);
        HeadlessUi.Until(() => Focused(trigger) == remove);
        Press(remove, Key.Down); // 循环到第一项
        Assert.Same(copy, Focused(trigger));
        Press(copy, Key.Down); // 跳过不可用的
        Assert.Same(export, Focused(trigger));
        Press(export, Key.None, symbol: "删"); // 按字（母）跳到以它开头的项
        Assert.Same(remove, Focused(trigger));
        Assert.Equal(remove.ThemeColor("Rune.Brush.DangerText"), Lookup.ColorOf(remove.Foreground)); // 破坏性操作是危险色

        Press(remove, Key.Escape); // Esc 关闭，焦点回到触发器
        HeadlessUi.Flush(window);
        Assert.False(menu.IsOpen);
        HeadlessUi.Until(() => Focused(trigger) == trigger);

        Press(trigger, Key.Down); // ↓ 打开并聚焦第一项
        HeadlessUi.Flush(window);
        HeadlessUi.Until(() => Focused(trigger) == copy);
        Press(copy, Key.Tab); // Tab 关闭，焦点往后走
        HeadlessUi.Flush(window);
        Assert.False(menu.IsOpen);
        Assert.Equal("下一个", (Focused(trigger) as Button)?.Content);
        Assert.Equal([(true, OpenChangeReason.Trigger), (false, OpenChangeReason.Escape), (true, OpenChangeReason.Trigger), (false, OpenChangeReason.Focus)], reasons);
    });

    [Fact]
    public Task Shortcuts_run_items_while_the_menu_is_closed() => HeadlessUi.Run(() =>
    {
        var copies = 0;
        var copy = new MenuItem { Content = "复制", Shortcut = "Ctrl+D" };
        copy.Click += (_, _) => copies++;
        var grid = new MenuCheckboxItem { Content = "显示网格", Shortcut = "Ctrl+G", IsEnabled = false };
        var menu = new Menu { Trigger = new Button { Content = "操作" }, Items = { copy, grid } };
        var window = HeadlessUi.Show(menu);

        Press(window, Key.D, KeyModifiers.Control);
        Press(window, Key.G, KeyModifiers.Control); // 不可用的不执行
        Assert.Equal((1, false, false), (copies, grid.IsChecked, menu.IsOpen));
        Assert.Equal("Control+D", AutomationProperties.GetAcceleratorKey(copy)); // 读屏从快捷键念
    });

    [Fact]
    [Upstream("MenuSub", "是带子菜单的项：aria-haspopup=\"menu\"，点了展开")]
    public Task Submenus_expand_and_keys_go_in_and_out() => HeadlessUi.Run(() =>
    {
        var pdf = new MenuItem { Content = "PDF" };
        var excel = new MenuItem { Content = "Excel" };
        var export = new MenuSub { Label = "导出为", Items = { pdf, excel } };
        var copy = new MenuItem { Content = "复制" };
        var menu = new Menu { Trigger = new Button { Content = "更多" }, Items = { copy, export } };
        var window = HeadlessUi.Show(menu);
        menu.IsOpen = true;
        HeadlessUi.Flush(window);
        var item = export.Trigger;
        var expand = (IExpandCollapseProvider)item.Peer();

        Assert.Equal((AutomationControlType.MenuItem, "导出为", ExpandCollapseState.Collapsed), (item.Peer().GetAutomationControlType(), item.Peer().GetName(), expand.ExpandCollapseState));
        ((IInvokeProvider)item.Peer()).Invoke(); // 点了展开，焦点进到第一项；外面的菜单不关
        HeadlessUi.Flush(window);
        Assert.Equal(ExpandCollapseState.Expanded, expand.ExpandCollapseState);
        Assert.True(menu.IsOpen);
        HeadlessUi.Until(() => Focused(item) == pdf);
        Assert.Equal("导出为", export.Panel.Peer().GetName()); // 子菜单以这一项为名称

        Press(pdf, Key.Left); // ← 只关子菜单，回到这一项
        HeadlessUi.Flush(window);
        Assert.Equal((false, true), (export.IsOpen, menu.IsOpen));
        HeadlessUi.Until(() => Focused(item) == item);

        Press(item, Key.Right); // → 打开并聚焦第一项
        HeadlessUi.Flush(window);
        HeadlessUi.Until(() => Focused(item) == pdf);
        ((IInvokeProvider)excel.Peer()).Invoke(); // 子菜单里执行一项：整串都关
        Assert.Equal((false, false), (export.IsOpen, menu.IsOpen));
    });

    static void RequestContext(Control source) => source.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent, Source = source });

    [Fact]
    [Upstream("ContextMenu", "在区域里右键打开菜单（阻止浏览器自己的）；在菜单上右键不再打开")]
    public Task Context_menus_open_on_request_and_give_focus_back() => HeadlessUi.Run(() =>
    {
        var file = new Button { Content = "文件" };
        var rename = new MenuItem { Content = "重命名" };
        var region = new ContextMenu { Content = file, Items = { rename } };
        var reasons = new List<(bool, OpenChangeReason)>();
        region.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        var window = HeadlessUi.Show(region);
        file.Focus();

        RequestContext(file); // 键盘的菜单键：从获得焦点的元素左下角弹出
        HeadlessUi.Flush(window);
        Assert.True(region.IsOpen);
        Assert.Equal("更多操作", region.Menu.Panel.Peer().GetName()); // 菜单的名称默认取语言包
        HeadlessUi.Until(() => Focused(file) == rename);
        RequestContext(rename); // 在菜单上右键不再打开
        Assert.Equal([(true, OpenChangeReason.Trigger)], reasons);

        Press(rename, Key.Escape);
        HeadlessUi.Flush(window);
        Assert.False(region.IsOpen);
        HeadlessUi.Until(() => Focused(file) == file); // 关闭后焦点回到打开前的元素
    });

    [Fact]
    [Upstream("ContextMenu", "disabled 时不弹出，浏览器的右键菜单照常")]
    public Task Disabled_context_menus_stay_closed() => HeadlessUi.Run(() =>
    {
        var file = new TextBlock { Text = "文件" };
        var region = new ContextMenu { Content = file, Items = { new MenuItem { Content = "重命名" } }, IsEnabled = false };
        HeadlessUi.Show(region);
        var args = new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent, Source = file };
        file.RaiseEvent(args);
        Assert.False(region.IsOpen);
        Assert.False(args.Handled); // 没拦下：平台自己的右键菜单照常
    });
}
