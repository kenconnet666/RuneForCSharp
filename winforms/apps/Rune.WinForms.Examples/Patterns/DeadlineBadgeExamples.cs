using System.Globalization;

namespace Rune.WinForms.Examples;

/// <summary>DeadlineBadge 页的示例（vue-tsx-pro 文档站 patterns/DeadlineBadge）。</summary>
static class DeadlineBadgeExamples
{
    public static Example[] All =>
    [
        new("DeadlineBadge", "states", "状态", "Due 是截止日。还早是中性色；剩下的工作日不多于 Soon（默认 2）和今天到期是警示色；超时是危险色。写了 Done 按完成那天算，显示按时或超时完成。悬停看截止日，读屏也会念。演示固定了 Today，业务以服务器日期为准时也这样传。", States),
        new("DeadlineBadge", "todos", "待办列表", "最常见的用法：列表每条的结束边（ItemListItemControl 的 End）放一个时限标记，超时、快到期的一眼看出来；已完成的写 Done，显示按时还是超时完成。", Todos),
        new("DeadlineBadge", "calendar", "开始日加工作日、节假日", "Start + Workdays 算截止日。工作日按 ProviderControl 的 WorkCalendar（带上法定节假日与调休补班），没给时只按周末；算法见 WorkCalendar.DeadlineOf。", Calendar),
    ];

    static readonly DateOnly Today = new(2026, 10, 21);

    static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static Control States()
    {
        (string Label, DateOnly Due, DateOnly? Done)[] rows =
        [
            ("还早", new(2026, 11, 4), null),
            ("快到期（2 个工作日内）", new(2026, 10, 23), null),
            ("今天到期", Today, null),
            ("超时", new(2026, 10, 15), null),
            ("按时完成", new(2026, 10, 20), new(2026, 10, 19)),
            ("超时完成", new(2026, 10, 14), new(2026, 10, 20)),
        ];
        var list = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
        foreach (var (label, due, done) in rows)
            list.Controls.Add(Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md },
                new SmallControl { Text = $"{label} · 截止 {Iso(due)}", Tone = TextTone.Muted }, new DeadlineBadgeControl { Due = due, Done = done, Today = Today }));
        return list;
    }

    static Control Todos()
    {
        (string Code, string Name, string Owner, DateOnly Due, DateOnly? Done)[] todos =
        [
            ("PO-2026-0233", "财务复核", "韩笑", new(2026, 10, 16), null),
            ("PO-2026-0187", "资料审核：办公椅供应商切换", "林小满", Today, null),
            ("PO-2026-0214", "方案评审：门店陈列方案", "周宁", new(2026, 10, 22), null),
            ("PO-2026-0179", "费用核算：包装纸箱第二供应商", "陆川", new(2026, 10, 30), null),
            ("PO-2026-0308", "试点实施：线束卡箍合并规格", "方圆", new(2026, 10, 19), new(2026, 10, 19)),
        ];
        var list = new ItemListControl { Divided = true, Inset = Space.Md, Dock = DockStyle.Top };
        foreach (var todo in todos)
            list.Controls.Add(new ItemListItemControl
            {
                Start = new AvatarControl { DisplayName = todo.Owner, TextSize = TextSize.Sm },
                Text = todo.Name, Href = new Uri($"https://example.com/todos/{todo.Code}"),
                Description = $"{todo.Code} · 主办 {todo.Owner}",
                End = new DeadlineBadgeControl { Due = todo.Due, Done = todo.Done, Today = Today },
            });
        return Build.Card(new CardControl { CardPadding = Space.None }, list);
    }

    static Control Calendar()
    {
        var today = new DateOnly(2026, 10, 9);
        var holidays = new WorkCalendar(holidays: [(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 8))], workdays: [new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 10)]);
        (string Name, string Code, DateOnly Start, int Workdays)[] rows =
        [
            ("资料审核", "PO-2026-0187", new(2026, 9, 28), 3),
            ("财务复核", "PO-2026-0233", new(2026, 9, 28), 10),
            ("方案评审", "PO-2026-0214", new(2026, 9, 25), 5),
            ("供应商报价回复", "PO-2026-0102", new(2026, 9, 30), 2),
            ("验收结论确认", "PO-2026-0308", new(2026, 10, 9), 1),
            ("计划调整审批", "PO-2026-0276", new(2026, 10, 8), 5),
        ];
        var head = new TableRowControl
        {
            Controls =
            {
                new TableHeaderCellControl { Text = "事项" }, new TableHeaderCellControl { Text = "开始" }, new TableHeaderCellControl { Text = "时限", Align = TableAlign.End },
                new TableHeaderCellControl { Text = "只按周末" }, new TableHeaderCellControl { Text = "按工作日历" },
            },
        };
        var body = new TableBodyControl();
        foreach (var row in rows)
        {
            // 截止日与标记放进带工作日历的作用域：标记按所在作用域的日历算
            TableCellControl Due(WorkCalendar calendar)
            {
                var scope = new ProviderControl { WorkCalendar = calendar, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
                scope.Controls.Add(Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs },
                    new SmallControl { Text = Iso(calendar.AddWorkdays(row.Start, row.Workdays)), Tone = TextTone.Muted },
                    new DeadlineBadgeControl { Start = row.Start, Workdays = row.Workdays, Today = today }));
                return Cell(scope, nowrap: true);
            }
            body.Controls.Add(new TableRowControl
            {
                Controls =
                {
                    Cell(Build.Flex(new FlexControl { Direction = FlexDirection.Column }, new TextControl { Text = row.Name }, new SmallControl { Text = row.Code, Tone = TextTone.Muted })),
                    Cell(new TextControl { Text = Iso(row.Start), Numeric = true }, nowrap: true),
                    new TableCellControl { Text = $"{row.Workdays} 个工作日", Align = TableAlign.End, Nowrap = true },
                    Due(WorkCalendar.Weekdays),
                    Due(holidays),
                },
            });
        }
        return new TableControl { Density = TableDensity.Compact, AccessibleName = "国庆前后开始的事项", Controls = { new TableHeadControl { Controls = { head } }, body } };

        static TableCellControl Cell(Control content, bool nowrap = false)
        {
            var cell = new TableCellControl { Nowrap = nowrap };
            cell.Controls.Add(content);
            return cell;
        }
    }
}
