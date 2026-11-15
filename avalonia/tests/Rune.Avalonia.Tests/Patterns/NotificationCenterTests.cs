using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>NotificationCenter、NotificationList：未读数、按类型与只看未读筛选、点一条与全部标为已读、有地址的是链接、Enter 与点击一样。</summary>
public class NotificationCenterTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T10:00:00+08:00", System.Globalization.CultureInfo.InvariantCulture);

    static readonly NotificationItem[] Items =
    [
        new("1", "新的待办：资料审核", DateTimeOffset.Parse("2026-10-21T09:30:00+08:00", System.Globalization.CultureInfo.InvariantCulture)) { Description = "PO-2026-0012", Type = "todo" },
        new("2", "被退回：方案设计", DateTimeOffset.Parse("2026-10-20T16:00:00+08:00", System.Globalization.CultureInfo.InvariantCulture)) { Type = "returned", Tone = StatusTone.Warning },
        new("3", "催办", DateTimeOffset.Parse("2026-10-18T10:00:00+08:00", System.Globalization.CultureInfo.InvariantCulture)) { Type = "remind", IsRead = true },
    ];

    static readonly NotificationType[] Types = [new("todo", "待办"), new("returned", "退回"), new("remind", "催办")];

    static NotificationCenter Show(NotificationCenter center)
    {
        center.Now = Now;
        Provider.SetLang(center, Lang.ZhCn);
        var window = HeadlessUi.Show(center);
        HeadlessUi.Flush(window);
        return center;
    }

    static IEnumerable<bool> States(NotificationList list) => list.Rows.Select(row => row.Item.IsRead);

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    [Upstream("NotificationCenter", "铃铛的名称带未读数；打开后新的在前，未读的给读屏标“未读”，时间写成相对现在")]
    public Task Bell_names_the_unread_count_and_rows_mark_unread() => HeadlessUi.Run(() =>
    {
        var center = Show(new NotificationCenter { Items = Items, Types = Types });
        Assert.Equal("通知，未读 2 条", AutomationProperties.GetName(center.Trigger));
        Assert.Equal([false, false, true], States(center.List));
        Assert.StartsWith("未读 新的待办：资料审核", AutomationProperties.GetName(center.List.Rows.First()));
        var time = center.List.Rows.First().GetLogicalDescendants().OfType<RelativeTime>().Single();
        Assert.Equal("30分钟前", time.Text);
    });

    [Fact]
    [Upstream("NotificationCenter", "点一条：报告读过了、交给 onItemClick；全部标为已读报告全部未读的")]
    public Task Clicking_reports_read_and_mark_all_reports_every_unread() => HeadlessUi.Run(() =>
    {
        var center = Show(new NotificationCenter { Items = Items, IsOpen = true });
        var read = new List<IReadOnlyList<string>>();
        NotificationItem? clicked = null;
        center.MarkedRead += (_, e) => read.Add(e.Ids);
        center.ItemClicked += (_, e) => clicked = e.Item;
        Click(center.List.Rows.First());
        Assert.Equal(["1"], read.Single());
        Assert.Same(Items[0], clicked);
        // 点了一条就收起
        Assert.False(center.IsOpen);
        read.Clear();
        Click(center.List.Filters.MarkAll);
        Assert.Equal(["1", "2"], read.Single());
    });

    [Fact]
    [Upstream("NotificationCenter", "按类型筛选、只看未读；筛完没有时显示“没有符合条件的通知”")]
    public Task Filters_by_type_and_unread() => HeadlessUi.Run(() =>
    {
        var center = Show(new NotificationCenter { Items = Items, Types = Types });
        var (types, unreadOnly, _) = center.List.Filters;
        types.Value = "remind";
        Assert.Equal([true], States(center.List));
        unreadOnly.IsChecked = true;
        Assert.Empty(center.List.Rows);
        // 有通知、只是筛掉了：不说"没有通知"
        Assert.Contains(center.List.GetLogicalDescendants().OfType<Empty>(), empty => empty.Title == "没有符合条件的通知");
    });

    [Fact]
    [Upstream("NotificationList", "放在页面里（不用铃铛）：有 href 的是链接，没有的是按钮，Enter 与点击一样报告读过了")]
    public Task Rows_with_an_address_are_links_and_enter_reports_read() => HeadlessUi.Run(() =>
    {
        var list = new NotificationList
        {
            Now = Now,
            Items = [.. Items, new("4", "查看报表", DateTimeOffset.Parse("2026-10-21T09:50:00+08:00", System.Globalization.CultureInfo.InvariantCulture)) { Href = new Uri("https://example.com/reports") }],
        };
        Provider.SetLang(list, Lang.ZhCn);
        var window = HeadlessUi.Show(list);
        HeadlessUi.Flush(window);
        var read = new List<IReadOnlyList<string>>();
        list.MarkedRead += (_, e) => read.Add(e.Ids);
        Assert.Equal([AutomationControlType.Button, AutomationControlType.Button, AutomationControlType.Button, AutomationControlType.Hyperlink],
            list.Rows.Select(row => row.Peer().GetAutomationControlType()));
        var todo = list.Rows.First();
        todo.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(["1"], read.Single());
    });

    [Fact]
    [Upstream("NotificationCenter", "没有 href 的通知：Enter、Space 标为已读")]
    public Task Space_marks_a_notification_read() => HeadlessUi.Run(() =>
    {
        var list = new NotificationList { Items = Items, Now = Now };
        var window = HeadlessUi.Show(list);
        HeadlessUi.Flush(window);
        var read = new List<IReadOnlyList<string>>();
        list.MarkedRead += (_, e) => read.Add(e.Ids);
        list.Rows.ElementAt(1).Focus();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Assert.Equal(["2"], read.Single());
    });

    [Fact]
    [Upstream("NotificationCenter", "铃铛打开面板，Tab 进到通知；Esc 关闭，焦点回到铃铛")]
    public Task Bell_opens_the_panel_and_escape_returns_focus() => HeadlessUi.Run(() =>
    {
        var center = Show(new NotificationCenter { Items = Items });
        var window = (Window)TopLevel.GetTopLevel(center)!;
        center.Trigger.Focus();
        Click(center.Trigger);
        HeadlessUi.Flush(window);
        Assert.True(center.IsOpen);
        center.List.Rows.First().Focus();
        Assert.True(center.List.Rows.First().IsFocused);
        center.List.Rows.First().RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        HeadlessUi.Flush(window);
        Assert.False(center.IsOpen);
        Assert.True(center.Trigger.IsFocused);
    });
}
