using System.Collections.ObjectModel;

namespace Rune.WinForms.Examples;

/// <summary>KanbanBoardControl 的示例（vue-tsx-pro 文档站 patterns/KanbanBoard）。上游的“放下后先确认”要用 FormDialog，还没做。</summary>
static class KanbanBoardExamples
{
    public static Example[] All =>
    [
        new("KanbanBoard", "flow", "只能挪到相邻的列，列内排序", "看板不自己挪卡片：放下时报告 Moved（带上在目标列里的位置，落点处有一条插入线），使用方改了数据卡片才换列。订阅了 Reordered 时列内也能拖动排序，数据立即重排。CanMove 写出不能移动的原因：拖动时那一列标红并写出原因，放下时弹回、报告 Rejected。", Flow),
    ];

    sealed record Ticket(string Id, string Title, string Stage, string Owner, string Priority, DateOnly Due);

    static readonly DateOnly Today = new(2026, 10, 21);

    static readonly KanbanColumn[] Columns =
    [
        new("todo", "待处理"),
        new("doing", "处理中"),
        new("review", "待验收", StatusTone.Warning),
        new("done", "已完成", StatusTone.Success),
    ];

    static (string Label, StatusTone Tone) PriorityOf(string priority) => priority switch
    {
        "high" => ("高", StatusTone.Danger),
        "medium" => ("中", StatusTone.Warning),
        _ => ("低", StatusTone.Neutral),
    };

    // 工单看板：只能挪到相邻的列（CanMove 写出原因），放下时报告 Moved，改了数据卡片才换列；卡片带优先级、处理人与时限
    static Control Flow()
    {
        var tickets = new ObservableCollection<Ticket>
        {
            new("T-101", "登录页验证码不刷新", "todo", "林小满", "high", new(2026, 10, 22)),
            new("T-102", "导出 Excel 列宽错乱", "todo", "周一航", "medium", new(2026, 10, 28)),
            new("T-104", "收件箱批量认领后计数不刷新", "todo", "韩笑", "medium", new(2026, 10, 30)),
            new("T-105", "台账筛选条件刷新后丢失", "todo", "陆川", "low", new(2026, 11, 6)),
            new("T-098", "首页统计卡片加载慢", "doing", "宋知行", "high", Today),
            new("T-099", "审批意见附件预览空白", "doing", "方圆", "medium", new(2026, 10, 24)),
            new("T-100", "流程图在窄屏下重叠", "doing", "许悠", "low", new(2026, 10, 19)),
            new("T-095", "通知中心未读数不准", "review", "许悠", "high", new(2026, 10, 20)),
            new("T-096", "暗色主题下图表网格看不清", "review", "周宁", "low", new(2026, 10, 27)),
            new("T-090", "表格列设置记不住", "done", "林小满", "medium", new(2026, 10, 17)),
            new("T-091", "日期选择器跨年显示错月", "done", "周一航", "high", new(2026, 10, 15)),
        };
        var order = Columns.Select(column => column.Key).ToList();
        var message = new SmallControl { Tone = TextTone.Muted, Text = "只能往后挪一列，或者退回上一列" };
        var board = new KanbanBoardControl
        {
            Columns = Columns,
            ItemsSource = tickets,
            GetKey = ticket => ((Ticket)ticket).Id,
            GetColumn = ticket => ((Ticket)ticket).Stage,
            GetLabel = ticket => $"{((Ticket)ticket).Id} {((Ticket)ticket).Title}",
            CanMove = (ticket, to) => Math.Abs(order.IndexOf(to) - order.IndexOf(((Ticket)ticket).Stage)) == 1 ? null : "只能挪到相邻的一列",
            ColumnWidth = 14 * 16,
            ColumnMaxHeight = 32 * 16,
            ItemFactory = item =>
            {
                var ticket = (Ticket)item;
                var (label, tone) = PriorityOf(ticket.Priority);
                return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, AccessibleName = ticket.Title },
                    Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm },
                        new TextControl { Mono = true, TextSize = TextSize.Xs, Tone = TextTone.Muted, Text = ticket.Id },
                        new BadgeControl { TextSize = TextSize.Xs, Variant = StatusVariant.Soft, Tone = tone, Text = label }),
                    new TextControl { TextSize = TextSize.Sm, Weight = TextWeight.Medium, Text = ticket.Title, Wrap = TextWrap.Wrap },
                    Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm },
                        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs }, new AvatarControl { TextSize = TextSize.Xs, DisplayName = ticket.Owner }, new SmallControl { Tone = TextTone.Muted, Text = ticket.Owner, Wrap = TextWrap.NoWrap }),
                        ticket.Stage == "done" ? new SmallControl { Tone = TextTone.Muted, Text = "已关闭" } : new DeadlineBadgeControl { Due = ticket.Due, Today = Today }));
            },
            AccessibleName = "工单看板",
        };
        board.Moved += (_, e) =>
        {
            // 看板不自己挪：改数据，卡片才换列（真实页面里是调接口、刷新）。插到落点：目标列第 Index 张的前面
            var ticket = (Ticket)e.Item;
            tickets.Remove(ticket);
            var anchor = tickets.Where(item => item.Stage == e.To).ElementAtOrDefault(e.Index);
            tickets.Insert(anchor is null ? tickets.Count : tickets.IndexOf(anchor), ticket with { Stage = e.To });
            message.Text = $"{ticket.Id} 移到了“{Columns.First(column => column.Key == e.To).Label}”第 {e.Index + 1} 位";
        };
        board.Reordered += (_, _) => { };
        board.Rejected += (_, e) => message.Text = $"{((Ticket)e.Item).Id} 没有移动：{e.Reason}";
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Dock = DockStyle.Top }, board, message);
    }
}
