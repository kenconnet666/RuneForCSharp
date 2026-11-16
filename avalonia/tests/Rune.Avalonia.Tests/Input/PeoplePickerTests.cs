using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// 人员与组织选择器：PeopleSource.Local（下级、搜索、角色）；面板的下钻与面包屑、点行勾选、不能选的部门点行进去、全选、Max 换掉或勾满、
/// 移除与清空、搜索、"最近"；字段的对话框（确定写回、取消放弃）、Backspace、只选一个时显示名字。
/// </summary>
public class PeoplePickerTests
{
    static PeopleNode User(string id, string name, string description) => new(id, PeopleTypes.User, name, description);

    static readonly PeopleNode[] Org =
    [
        new("buy", PeopleTypes.Department, "采购部", Children:
        [
            new("source", PeopleTypes.Department, "寻源组", Children: [User("zhao", "赵一凡", "寻源组")]),
            User("lin", "林晓", "采购部 · 经理"),
            User("wu", "吴昊", "采购部 · 专员"),
        ]),
        new("fin", PeopleTypes.Department, "财务部", Children: [User("wang", "王建国", "财务部 · 经理")]),
    ];

    static readonly PeopleEntry[] Roles = [new("11", PeopleTypes.Role, "采购专员")];

    static readonly PeopleSource Source = PeopleSource.Local(Org, Roles);

    static ItemListItem Row(PeoplePickerPanel panel, string name) => panel.BrowseRows.First(row => (string?)row.Title == name);

    static T Named<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => AutomationProperties.GetName(control) == name);

    static void Click(Control control) => control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static List<string> Names(IReadOnlyList<PeopleEntry> entries) => [.. entries.Select(entry => entry.Name)];

    static void Wait(Window window) => HeadlessUi.Pump(TimeSpan.FromMilliseconds(30));

    [Fact]
    [Upstream("PeoplePicker", "下级：最上层与部门的下级，部门带 hasChildren；搜索按名字与说明；角色单独给")]
    public async Task Local_source_lists_children_searches_and_gives_roles()
    {
        var cancel = TestContext.Current.CancellationToken;
        var top = await Source.Children(null, cancel);
        Assert.Equal([("采购部", (bool?)true), ("财务部", true)], top.Select(entry => (entry.Name, entry.HasChildren)));
        Assert.IsNotType<PeopleNode>(top[0]);
        Assert.Equal(["寻源组", "林晓", "吴昊"], Names(await Source.Children(top[0], cancel)));
        Assert.Equal(["林晓", "王建国"], Names(await Source.Search!("经理", cancel)));
        Assert.Contains("采购专员", Names(await Source.Search!("采购", cancel)));
        Assert.Equal(Roles, await Source.Roles!(cancel));
    }

    static (PeoplePickerPanel Panel, Window Window) Panel(Action<PeoplePickerPanel>? setup = null)
    {
        var panel = new PeoplePickerPanel { Source = Source };
        setup?.Invoke(panel);
        var window = HeadlessUi.Show(new StackPanel { Width = 800, Children = { panel } });
        Wait(window);
        return (panel, window);
    }

    [Fact]
    [Upstream("PeoplePickerPanel", "下钻：“下级”进到部门，面包屑点“全部”回到最上层；只能选人员时点部门的整行也是进去")]
    public Task Drilling_down_and_back_up() => HeadlessUi.Run(() =>
    {
        var (panel, window) = Panel();
        Assert.Contains(panel.BrowseRows, row => (string?)row.Title == "采购部");
        Click(Named<Button>(panel, "下级：采购部"));
        Wait(window);
        Assert.Equal("采购部", panel.Trail.Items![^1].Label);
        Assert.Contains(panel.BrowseRows, row => (string?)row.Title == "林晓");

        Click(Row(panel, "寻源组"));
        Wait(window);
        Assert.Equal(["全部", "采购部", "寻源组"], panel.Trail.Items!.Select(item => item.Label));
        var root = panel.Trail.GetVisualDescendants().OfType<Button>().First(button => button.Name == BreadcrumbsParts.Button);
        Click(root);
        Wait(window);
        Assert.Contains(panel.BrowseRows, row => (string?)row.Title == "财务部");
    });

    [Fact]
    [Upstream("PeoplePickerPanel", "点行勾选、再点取消；全选只管这一页能勾的；右边移除、清空")]
    public Task Rows_select_all_remove_and_clear() => HeadlessUi.Run(() =>
    {
        var (panel, window) = Panel();
        Click(Named<Button>(panel, "下级：采购部"));
        Wait(window);
        Click(Row(panel, "林晓"));
        Wait(window);
        Assert.Equal(["林晓"], Names(panel.Entries));
        Click(Row(panel, "林晓"));
        Wait(window);
        Assert.Empty(panel.Entries);

        panel.GetVisualDescendants().OfType<Checkbox>().First(box => box.Name == PeoplePickerPanelParts.SelectAll).IsChecked = true;
        Wait(window);
        Assert.Equal(["林晓", "吴昊"], Names(panel.Entries));
        Click(Named<IconButton>(panel, "移除林晓"));
        Wait(window);
        Assert.Equal(["吴昊"], Names(panel.Entries));
        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == PeoplePickerPanelParts.Clear));
        Wait(window);
        Assert.Empty(panel.Entries);
    });

    [Fact]
    [Upstream("PeoplePickerPanel", "max=1 勾一个就换掉原来的；max=2 勾满之后其余的勾选框不可用")]
    public Task Max_replaces_or_caps() => HeadlessUi.Run(() =>
    {
        var (single, window) = Panel(panel => panel.Max = 1);
        Click(Named<Button>(single, "下级：采购部"));
        Wait(window);
        Click(Row(single, "林晓"));
        Click(Row(single, "吴昊"));
        Wait(window);
        Assert.Equal(["吴昊"], Names(single.Entries));

        var (capped, window2) = Panel(panel =>
        {
            panel.Max = 2;
            panel.Types = [PeopleTypes.User, PeopleTypes.Department];
        });
        Click(Row(capped, "采购部"));
        Click(Row(capped, "财务部"));
        Wait(window2);
        Assert.Equal(["采购部", "财务部"], Names(capped.Entries));
        Assert.Contains(capped.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "已选 2 / 2");
    });

    [Fact]
    [Upstream("PeoplePickerPanel", "搜索：输入停下后列出结果，有字时不显示浏览方式与面包屑；勾过的记进“最近”")]
    public Task Search_lists_results_and_remembers_recent() => HeadlessUi.Run(() =>
    {
        var (panel, window) = Panel(panel => panel.Recent = []);
        panel.SearchBox.Text = "王";
        HeadlessUi.Flush(window);
        // 防抖等待中请求还没发出：不先说"没有找到"
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "没有找到");
        HeadlessUi.Until(() => panel.BrowseRows.Any(row => (string?)row.Title == "王建国"));
        Assert.False(panel.Trail.IsVisible);
        Assert.False(panel.Pages.IsVisible);
        Click(Row(panel, "王建国"));
        Wait(window);
        Assert.Equal(["王建国"], Names(panel.Entries));

        panel.SearchBox.Text = "";
        Wait(window);
        panel.Pages.Value = "recent";
        Wait(window);
        Assert.Contains(panel.BrowseRows, row => (string?)row.Title == "王建国");
        Assert.Equal([new PeopleEntry("wang", PeopleTypes.User, "王建国", "财务部 · 经理")], panel.Recent);
    });

    [Fact]
    [Upstream("PeoplePicker", "对话框里改的是草稿：确定才写回，取消放弃；触发按钮上 Backspace 移除最后一个")]
    public Task Dialog_edits_a_draft() => HeadlessUi.Run(() =>
    {
        var picker = new PeoplePicker { Source = Source, Entries = [new("lin", PeopleTypes.User, "林晓")] };
        var window = HeadlessUi.Show(picker);
        ((IExpandCollapseProvider)picker.Peer()).Expand();
        HeadlessUi.Until(() => picker.Panel is not null);
        Wait(window);
        var panel = picker.Panel!;
        Click(Named<IconButton>(panel, "移除林晓"));
        Wait(window);
        Click(picker.OpenDialog!.Actions.Cancel);
        HeadlessUi.Until(() => picker.OpenDialog is null);
        Assert.Equal(["林晓"], Names(picker.Entries));

        ((IExpandCollapseProvider)picker.Peer()).Expand();
        HeadlessUi.Until(() => picker.Panel is not null);
        Wait(window);
        panel = picker.Panel!;
        Click(Named<Button>(panel, "下级：财务部"));
        Wait(window);
        Click(Row(panel, "王建国"));
        Wait(window);
        Click(picker.OpenDialog!.Actions.Confirm);
        HeadlessUi.Until(() => picker.OpenDialog is null);
        Assert.Equal(["林晓", "王建国"], Names(picker.Entries));

        picker.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Back, Source = picker });
        Assert.Equal(["林晓"], Names(picker.Entries));
    });

    [Fact]
    [Upstream("PeoplePicker", "只选一个时触发框里直接是名字，没有标签")]
    public Task Single_shows_the_name() => HeadlessUi.Run(() =>
    {
        var picker = new PeoplePicker { Source = Source, Entries = [new("lin", PeopleTypes.User, "林晓")], Max = 1 };
        HeadlessUi.Show(picker);
        Assert.Equal("林晓", picker.GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == SelectParts.Value).Text);
        Assert.False(picker.TagRow!.IsVisible);
    });

    [Fact]
    [Upstream("PeoplePickerPanel 的部件属性（P12）", "头像、行勾选框、下级、全选、移除按钮都转发部件属性")]
    public Task Parts_are_named() => HeadlessUi.Run(() =>
    {
        var (panel, _) = Panel(panel => panel.Types = [PeopleTypes.User, PeopleTypes.Department]);
        var names = panel.GetVisualDescendants().OfType<Control>().Select(control => control.Name).ToHashSet();
        foreach (var part in new[] { PeoplePickerPanelParts.Avatar, PeoplePickerPanelParts.Checkbox, PeoplePickerPanelParts.Drill, PeoplePickerPanelParts.SelectAll })
            Assert.Contains(part, names);
    });

    [Fact]
    [Upstream("PeoplePicker.browser", "↓ 打开对话框，Esc 关闭；Backspace 删掉最后一个已选")]
    public Task Keyboard_opens_closes_and_removes() => HeadlessUi.Run(() =>
    {
        var picker = new PeoplePicker { Source = Source, Entries = [new("lin", PeopleTypes.User, "林晓"), new("buy", PeopleTypes.Department, "采购部")] };
        var window = HeadlessUi.Show(picker);
        picker.Focus();
        picker.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down, Source = picker });
        HeadlessUi.Until(() => picker.Panel is not null);
        Wait(window);
        picker.Panel!.SearchBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = picker.Panel.SearchBox });
        HeadlessUi.Until(() => picker.OpenDialog is null);
        // 没按确定：已选不变
        Assert.Equal(["林晓", "采购部"], Names(picker.Entries));

        picker.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Back, Source = picker });
        Assert.Equal(["林晓"], Names(picker.Entries));
    });
}
