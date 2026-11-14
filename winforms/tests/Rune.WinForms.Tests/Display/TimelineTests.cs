namespace Rune.WinForms.Tests;

/// <summary>
/// TimelineControl：有序列表的各项、时间带机器可读的描述、pending 是最后一项（忙）；reverse 新的在前；操作人与首字母头像、详情点开才建；
/// maxVisible 收起旧的（按钮在旧的那一端）；作用域给默认值。
/// </summary>
public class TimelineTests
{
    /// <summary>一行显示的文字：按钮那一行是按钮的文字，其余是标题。</summary>
    static string Title(TimelineRowControl row) => row.Toggle?.Text ?? row.Content.Controls[0].Text;

    static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(child => Descendants(child).Prepend(child));

    [Fact]
    [Upstream("Timeline", "有序列表；时间写在 <time datetime>；pending 是最后一项")]
    public Task Ordered_list_time_and_pending() => WinFormsUi.Run(() =>
    {
        var timeline = new TimelineControl
        {
            Items = [new() { Title = "已下单", Time = "10-18 09:12", DateTime = new DateTimeOffset(2026, 10, 18, 9, 12, 0, TimeSpan.FromHours(8)) }, new() { Title = "已发货", Icon = Lucide.Truck, Tone = StatusTone.Success }],
            Pending = "正在派送",
        };
        using var form = WinFormsUi.Show(timeline);

        var rows = timeline.Rows;
        Assert.Equal(3, rows.Count);
        Assert.Equal(AccessibleRole.List, timeline.AccessibilityObject.Role);
        Assert.Equal(AccessibleRole.ListItem, rows[0].AccessibilityObject.Role);
        Assert.Equal("2026-10-18T09:12:00.0000000+08:00", rows[0].Time!.AccessibleDescription);
        Assert.Equal(Lucide.Truck, rows[1].Item!.Icon);
        Assert.True(rows[2].IsPending);
        Assert.True(rows[2].AccessibilityObject.State.HasFlag(AccessibleStates.Busy));
        Assert.Equal("正在派送", Title(rows[2]));
        Assert.NotNull(rows[2].Spinner);
        Assert.True(rows[0].Height > 0);
    });

    [Fact]
    [Upstream("Timeline", "部件属性落到各自的节点上")]
    public Task The_last_item_has_no_line() => WinFormsUi.Run(() =>
    {
        var timeline = new TimelineControl { Items = [new() { Title = "已下单", Time = "09:12", Description = "说明" }, new() { Title = "已发货" }] };
        using var form = WinFormsUi.Show(timeline);

        Assert.Equal([true, false], timeline.Rows.Select(row => row.HasLine));
        Assert.Equal("09:12", timeline.Rows[0].Time!.Text);
        Assert.Contains(timeline.Rows[0].Content.Controls.Cast<Control>(), control => control.Text == "说明");
    });

    [Fact]
    [Upstream("Timeline", "reverse：新的在前，进行中的一项也在最前")]
    public Task Reverse_puts_the_newest_first() => WinFormsUi.Run(() =>
    {
        var timeline = new TimelineControl { Reverse = true, Items = [new() { Title = "已下单" }, new() { Title = "已发货" }], Pending = "正在派送" };
        using var form = WinFormsUi.Show(timeline);

        Assert.Equal(["正在派送", "已发货", "已下单"], timeline.Rows.Select(Title));
        Assert.False(timeline.Rows[2].HasLine);
    });

    [Fact]
    [Upstream("Timeline", "actor 显示在时间前面")]
    public Task Actor_has_initials_and_details_open_on_demand() => WinFormsUi.Run(() =>
    {
        var timeline = new TimelineControl { Items = [new() { Title = "改了报价", Actor = "林小满", Time = "10-18 09:12", Details = new ParagraphControl { Text = "完整对比" } }] };
        using var form = WinFormsUi.Show(timeline);

        var row = timeline.Rows[0];
        Assert.Equal("满", Descendants(row.Meta!).OfType<AvatarControl>().Single().Fallback);
        Assert.Equal(["林小满", "10-18 09:12"], Descendants(row.Meta!).Select(control => control.Text).Where(text => text is "林小满" or "10-18 09:12"));
        Assert.DoesNotContain(Descendants(row), control => control.Text == "完整对比");
        var trigger = (ButtonControl)row.Details!.Trigger!;
        Assert.Equal("查看详情", trigger.Text);
        Assert.False(trigger.Expanded);
        trigger.PerformClick();
        WinFormsUi.Pump();
        Assert.True(row.Details.IsOpen);
        Assert.True(trigger.Expanded);
        Assert.Contains(Descendants(row), control => control.Text == "完整对比");
    });

    [Fact]
    [Upstream("Timeline", "maxVisible：只显示最新的几项")]
    public Task Max_visible_folds_the_oldest() => WinFormsUi.Run(() =>
    {
        TimelineItem[] items = [.. new[] { "一", "二", "三", "四", "五" }.Select(title => new TimelineItem { Title = title })];
        var forward = new TimelineControl { Items = items, MaxVisible = 2 };
        var reversed = new TimelineControl { Items = items, MaxVisible = 2, Reverse = true, Pending = "处理中" };
        using var form = WinFormsUi.Show(forward, reversed);

        Assert.Equal(["展开其余 3 条", "四", "五"], forward.Rows.Select(Title));
        Assert.False(forward.Rows[0].Toggle!.Expanded);
        var expanded = 0;
        forward.ExpandedChanged += (_, _) => expanded++;
        forward.Rows[0].Toggle!.PerformClick();
        WinFormsUi.Pump();
        Assert.True(forward.IsExpanded);
        Assert.Equal(1, expanded);
        Assert.Equal(["收起", "一", "二", "三", "四", "五"], forward.Rows.Select(Title));
        Assert.True(forward.Rows[0].Toggle!.Expanded);

        Assert.Equal(["处理中", "五", "四", "展开其余 3 条"], reversed.Rows.Select(Title));
        Assert.True(reversed.Rows[2].HasLine);
    });

    [Fact]
    [Upstream("Timeline", "项数不超过 maxVisible 时没有按钮")]
    public Task No_toggle_when_everything_fits() => WinFormsUi.Run(() =>
    {
        var timeline = new TimelineControl { Items = [new() { Title = "一" }], MaxVisible = 2 };
        using var form = WinFormsUi.Show(timeline);
        Assert.All(timeline.Rows, row => Assert.Null(row.Toggle));
    });

    [Fact]
    [Upstream("Timeline", "默认值与追加的样式")]
    public Task Scope_defaults_and_own_values_win() => WinFormsUi.Run(() =>
    {
        var styled = new TimelineControl { Items = [new() { Title = "已下单", Time = "09:12" }] };
        var own = new TimelineControl { Alternate = false, Items = [new() { Title = "已发货" }] };
        var scope = new ProviderControl { Dock = DockStyle.Fill };
        scope.Defaults.Set(new TimelineDefaults { Alternate = true });
        var stack = new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Dock = DockStyle.Fill };
        stack.Controls.AddRange([styled, own]);
        scope.Controls.Add(stack);
        using var form = WinFormsUi.Show(scope);

        Assert.True(styled.Alternate);
        // 交替排布：时间在对侧，是这一行直接的子控件
        Assert.Same(styled.Rows[0], styled.Rows[0].Time!.Parent);
        Assert.False(own.Alternate);
    });

    [Fact]
    public Task Rebuilding_moves_the_callers_controls() => WinFormsUi.Run(() =>
    {
        var title = new TextControl { Text = "自己的标题" };
        var timeline = new TimelineControl { Items = [new() { Title = title, Actor = new TextControl { Text = "林小满" } }] };
        using var form = WinFormsUi.Show(timeline);

        timeline.Reverse = true;
        timeline.Pending = "处理中";
        Assert.Same(title, timeline.Rows[1].Content.Controls[0]);
        Assert.False(title.IsDisposed);
    });
}
