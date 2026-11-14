using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Timeline：有序列表的各项、时间带机器可读的帮助文字、pending 是最后一项（忙）；reverse 新的在前；操作人与首字母头像、详情点开才建；
/// maxVisible 收起旧的（按钮在旧的那一端）；应用的样式给默认值。
/// </summary>
public class TimelineTests
{
    /// <summary>一行显示的文字：按钮那一行是按钮的文字，其余是标题。</summary>
    static string Title(TimelineRow row) => row.Toggle is { } toggle ? toggle.Content!.ToString()! : row.Content.Children[0] is TextBlock text ? text.Text! : "";

    static void Click(Control button) => ((IInvokeProvider)button.Peer()).Invoke();

    [Fact]
    [Upstream("Timeline", "有序列表；时间写在 <time datetime>；pending 是最后一项")]
    public Task Ordered_list_time_and_pending() => HeadlessUi.Run(() =>
    {
        var timeline = new Timeline
        {
            Items = [new() { Title = "已下单", Time = "10-18 09:12", DateTime = new DateTimeOffset(2026, 10, 18, 9, 12, 0, TimeSpan.FromHours(8)) }, new() { Title = "已发货", Icon = Lucide.Truck, Tone = StatusTone.Success }],
            Pending = "正在派送",
        };
        HeadlessUi.Show(timeline);

        var rows = timeline.Rows;
        Assert.Equal(3, rows.Count);
        Assert.Equal(AutomationControlType.List, ((Control)rows[0].Parent!).Peer().GetAutomationControlType());
        Assert.Equal(AutomationControlType.ListItem, rows[0].Peer().GetAutomationControlType());
        Assert.Equal("2026-10-18T09:12:00.0000000+08:00", AutomationProperties.GetHelpText(rows[0].Time!));
        Assert.Equal(Lucide.Truck, ((Border)rows[1].Marker!).Child is Icon icon ? icon.Data : null);
        Assert.True(rows[2].IsPending);
        Assert.Equal("busy", AutomationProperties.GetItemStatus(rows[2]));
        Assert.Equal("正在派送", Title(rows[2]));
        Assert.IsType<Spinner>(rows[2].Marker);
    });

    [Fact]
    [Upstream("Timeline", "部件属性落到各自的节点上")]
    [Upstream("Timeline", "Timeline：根、每项")]
    public Task Parts_are_named_and_the_last_item_has_no_line() => HeadlessUi.Run(() =>
    {
        var timeline = new Timeline { Items = [new() { Title = "已下单", Time = "09:12", Description = "说明" }, new() { Title = "已发货" }] };
        HeadlessUi.Show(timeline);

        var first = timeline.Rows[0];
        Assert.Equal([true, false], timeline.Rows.Select(row => row.Line is not null));
        Assert.Equal(TimelineParts.Title, ((Control)first.Content.Children[0]).Name);
        Assert.Equal(TimelineParts.Time, first.Time!.Name);
        Assert.Equal("说明", first.Content.GetLogicalDescendants().OfType<TextBlock>().Single(text => text.Name == TimelineParts.Description).Text);
    });

    [Fact]
    [Upstream("Timeline", "reverse：新的在前，进行中的一项也在最前")]
    public Task Reverse_puts_the_newest_first() => HeadlessUi.Run(() =>
    {
        var timeline = new Timeline { Reverse = true, Items = [new() { Title = "已下单" }, new() { Title = "已发货" }], Pending = "正在派送" };
        HeadlessUi.Show(timeline);

        Assert.Equal(["正在派送", "已发货", "已下单"], timeline.Rows.Select(Title));
        // 连接线：最后一项（最旧的）没有
        Assert.Null(timeline.Rows[2].Line);
    });

    [Fact]
    [Upstream("Timeline", "actor 显示在时间前面")]
    public Task Actor_has_initials_and_details_open_on_demand() => HeadlessUi.Run(() =>
    {
        var timeline = new Timeline { Items = [new() { Title = "改了报价", Actor = "林小满", Time = "10-18 09:12", Details = new Paragraph { Text = "完整对比" } }] };
        var window = HeadlessUi.Show(timeline);

        var row = timeline.Rows[0];
        var meta = row.Content.Children.OfType<Flex>().Single(flex => flex.Name == TimelineParts.Meta);
        var avatar = meta.GetLogicalDescendants().OfType<Avatar>().Single();
        Assert.Equal(TimelineParts.Avatar, avatar.Name);
        Assert.Equal("满", Avatar.Initials(avatar.DisplayName!));
        Assert.Equal(["林小满", "10-18 09:12"], meta.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text).Where(text => text is "林小满" or "10-18 09:12"));
        Assert.DoesNotContain(row.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == "完整对比");
        var details = row.Content.Children.OfType<Collapsible>().Single();
        var trigger = details.Trigger!;
        Assert.Equal("查看详情", ((Button)trigger).Content);
        Assert.Equal(ExpandCollapseState.Collapsed, ((IExpandCollapseProvider)trigger.Peer()).ExpandCollapseState);
        Click(trigger);
        HeadlessUi.Flush(window);
        Assert.True(details.IsOpen);
        Assert.Contains(row.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == "完整对比");
    });

    [Fact]
    [Upstream("Timeline", "maxVisible：只显示最新的几项")]
    public Task Max_visible_folds_the_oldest() => HeadlessUi.Run(() =>
    {
        TimelineItem[] items = [.. new[] { "一", "二", "三", "四", "五" }.Select(title => new TimelineItem { Title = title })];
        var forward = new Timeline { Items = items, MaxVisible = 2 };
        var reversed = new Timeline { Items = items, MaxVisible = 2, Reverse = true, Pending = "处理中" };
        var window = HeadlessUi.Show(new StackPanel { Children = { forward, reversed } });

        Assert.Equal(["展开其余 3 条", "四", "五"], forward.Rows.Select(Title));
        var toggle = forward.Rows[0].Toggle!;
        Assert.Equal(ExpandCollapseState.Collapsed, ((IExpandCollapseProvider)toggle.Peer()).ExpandCollapseState);
        Click(toggle);
        HeadlessUi.Flush(window);
        Assert.True(forward.IsExpanded);
        Assert.Equal(["收起", "一", "二", "三", "四", "五"], forward.Rows.Select(Title));
        Assert.Equal(ExpandCollapseState.Expanded, ((IExpandCollapseProvider)forward.Rows[0].Toggle!.Peer()).ExpandCollapseState);

        Assert.Equal(["处理中", "五", "四", "展开其余 3 条"], reversed.Rows.Select(Title));
        // 最后一项仍连线到按钮
        Assert.NotNull(reversed.Rows[2].Line);
    });

    [Fact]
    [Upstream("Timeline", "项数不超过 maxVisible 时没有按钮")]
    public Task No_toggle_when_everything_fits() => HeadlessUi.Run(() =>
    {
        var timeline = new Timeline { Items = [new() { Title = "一" }], MaxVisible = 2 };
        HeadlessUi.Show(timeline);
        Assert.All(timeline.Rows, row => Assert.Null(row.Toggle));
    });

    [Fact]
    [Upstream("Timeline", "默认值与追加的样式")]
    public Task App_styles_give_defaults_and_own_values_win() => HeadlessUi.Run(() =>
    {
        var styled = new Timeline { Width = 800, Items = [new() { Title = "已下单", Time = "09:12" }] };
        var own = new Timeline { Width = 800, Alternate = false, Items = [new() { Title = "已发货" }] };
        var host = new StackPanel { Children = { styled, own } };
        host.Styles.Add(new Style(x => x.OfType<Timeline>()) { Setters = { new Setter(Timeline.AlternateProperty, true) } });
        var window = HeadlessUi.Show(host);
        HeadlessUi.Flush(window);

        Assert.True(styled.Alternate);
        Assert.Equal(3, styled.Rows[0].ColumnDefinitions.Count);
        Assert.False(own.Alternate);
        Assert.Equal(2, own.Rows[0].ColumnDefinitions.Count);
    });

    [Fact]
    public Task Alternate_falls_back_to_one_side_when_narrow() => HeadlessUi.Run(() =>
    {
        var timeline = new Timeline { Alternate = true, Width = 200, Items = [new() { Title = "一", Time = "09:12" }, new() { Title = "二" }] };
        var window = HeadlessUi.Show(timeline);
        HeadlessUi.Flush(window);

        Assert.Equal(2, timeline.Rows[0].ColumnDefinitions.Count);
        timeline.Width = 800;
        HeadlessUi.Flush(window);
        Assert.Equal(3, timeline.Rows[0].ColumnDefinitions.Count);
    });

    [Fact]
    [Upstream("Timeline", "操作人头像、进行中的加载指示也转发部件属性")]
    public Task Avatar_and_pending_are_named_parts() => HeadlessUi.Run(() =>
    {
        var timeline = new Timeline { Items = [new() { Title = "提交", Actor = "林小满" }], Pending = "审批中" };
        HeadlessUi.Show(timeline);

        Assert.Single(timeline.GetLogicalDescendants().OfType<Avatar>(), avatar => avatar.Name == TimelineParts.Avatar);
        Assert.Equal(TimelineParts.Pending, timeline.Rows[1].Marker!.Name);
    });

    [Fact]
    public Task Rebuilding_moves_the_callers_elements() => HeadlessUi.Run(() =>
    {
        var title = new Text { Text = "自己的标题" };
        var timeline = new Timeline { Items = [new() { Title = title, Actor = new Text { Text = "林小满" } }] };
        HeadlessUi.Show(timeline);

        timeline.Reverse = true;
        timeline.Pending = "处理中";
        Assert.Same(title, timeline.Rows[1].Content.Children[0]);
    });
}
