using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using AvaloniaGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia.Examples;

/// <summary>CalendarBoard 的示例（vue-tsx-pro 文档站 date/CalendarBoard）。上游的 renderDay 在 C# 里是 DayContent（返回控件），renderEvent 是 EventTemplate。</summary>
static class CalendarBoardExamples
{
    public static Example[] All =>
    [
        new("CalendarBoard", "schedule", "项目日程", "会议、评审、出差（跨天）铺在格子里，每天最多三条；页头的筛选在 Toolbar 里；点一天，右边列出那天的全部日程。", Schedule),
        new("CalendarBoard", "roster", "车间排班", "每天早、中、夜三个班的值班人（DayContent），节假日与调休写在日期旁边（GetDayNote），休息日不排班；只读。", Roster),
    ];

    sealed record Meeting(string Id, string Title, DateOnly Date, string Kind, DateOnly? End = null);

    static readonly Dictionary<string, (string Label, StatusTone Tone)> Kinds = new()
    {
        ["meeting"] = ("会议", StatusTone.Accent),
        ["review"] = ("评审", StatusTone.Warning),
        ["trip"] = ("出差", StatusTone.Info),
        ["deadline"] = ("截止", StatusTone.Danger),
    };

    static DateOnly October(int day) => new(2026, 10, day);

    static readonly Meeting[] Meetings =
    [
        new("1", "09:30 周会", October(12), "meeting"),
        new("2", "14:00 供应商比价评审", October(12), "review"),
        new("3", "杭州工厂审核", October(14), "trip", October(16)),
        new("4", "09:30 周会", October(19), "meeting"),
        new("5", "10:30 改进方案对齐", October(19), "meeting"),
        new("6", "15:00 模具保养计划", October(19), "review"),
        new("7", "17:00 预算复盘", October(19), "meeting"),
        new("8", "季度报表提交", October(23), "deadline"),
        new("9", "09:30 周会", October(26), "meeting"),
        new("10", "工艺变更评审", October(28), "review"),
        new("11", "月度销售复盘", October(30), "meeting"),
    ];

    static Control Schedule()
    {
        var culture = CultureInfo.GetCultureInfo("zh-CN");
        var kind = new Select
        {
            ItemsSource = new[] { new ListboxOption("all", "全部类型") }.Concat(Kinds.Select(pair => new ListboxOption(pair.Key, pair.Value.Label))).ToArray(),
            Value = "all",
            Size = TextSize.Sm,
            MinWidth = 120,
        };
        AutomationProperties.SetName(kind, "日程类型");
        var board = new CalendarBoard
        {
            GetEventDate = item => ((Meeting)item!).Date,
            GetEventEnd = item => ((Meeting)item!).End ?? ((Meeting)item!).Date,
            GetEventLabel = item => ((Meeting)item!).Title,
            GetEventTone = item => Kinds[((Meeting)item!).Kind].Tone,
            Value = "2026-10-19",
            Month = "2026-10",
            Toolbar = kind,
        };
        AutomationProperties.SetName(board, "项目日程");
        var list = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm };
        var card = new Card { Padding = Space.Sm, Content = list, MinWidth = 220, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16, 0, 0, 0) };
        IEnumerable<Meeting> Shown() => Meetings.Where(meeting => kind.Value is null or "all" || meeting.Kind == kind.Value);
        void Sync()
        {
            board.ItemsSource = Shown().ToArray();
            list.Children.Clear();
            if (board.Date is not { } day)
            {
                list.Children.Add(new Text { Text = "选一天", Weight = TextWeight.Semibold });
                return;
            }
            list.Children.Add(new Text { Text = day.ToString("M月d日 ddd", culture), Weight = TextWeight.Semibold });
            var onDay = Shown().Where(meeting => meeting.Date <= day && day <= (meeting.End ?? meeting.Date)).ToList();
            if (onDay.Count == 0) list.Children.Add(new Small { Text = "这天没有日程", Tone = TextTone.Muted });
            foreach (var meeting in onDay)
                list.Children.Add(new Flex
                {
                    Align = FlexAlign.Center,
                    Gap = Space.Sm,
                    Children =
                    {
                        new Badge { Size = TextSize.Xs, Variant = StatusVariant.Soft, Tone = Kinds[meeting.Kind].Tone, Content = Kinds[meeting.Kind].Label },
                        new Text { Text = meeting.Title, Size = TextSize.Sm },
                    },
                });
        }
        board.PropertyChanged += (_, e) =>
        {
            if (e.Property == CalendarBoard.ValueProperty) Sync();
        };
        kind.PropertyChanged += (_, e) =>
        {
            if (e.Property == Select.ValueProperty) Sync();
        };
        Sync();
        var layout = new AvaloniaGrid { ColumnDefinitions = new ColumnDefinitions("3*,*") };
        AvaloniaGrid.SetColumn(card, 1);
        layout.Children.AddRange([board, card]);
        return layout;
    }

    /// <summary>2026 年 10 月的节假日与调休（演示数据）。</summary>
    static readonly Dictionary<DateOnly, string> Notes = new()
    {
        [October(1)] = "国庆节",
        [October(2)] = "休",
        [October(3)] = "休",
        [October(4)] = "休",
        [October(5)] = "休",
        [October(6)] = "休",
        [October(7)] = "休",
        [October(10)] = "班",
        [October(29)] = "重阳节",
    };

    static readonly string[] People = ["林小满", "周一航", "许悠", "韩笑", "陈默", "宋知遥"];

    static readonly (string Name, TextTone Tone)[] Shifts = [("早", TextTone.Success), ("中", TextTone.Warning), ("夜", TextTone.Accent)];

    static Control Roster()
    {
        // 放假的日子：国庆节与"休"
        var holidays = Notes.Where(pair => pair.Value is "国庆节" or "休").Select(pair => pair.Key).ToHashSet();
        var board = new CalendarBoard
        {
            Month = "2026-10",
            IsReadOnly = true,
            GetDayNote = date => Notes.GetValueOrDefault(date),
            DayContent = (date, state) =>
            {
                // 补齐的上下月、放假的日子、周末（调休上班的除外）不排班
                if (state.Outside || holidays.Contains(date) || state.Weekend && Notes.GetValueOrDefault(date) != "班") return null;
                var column = new Flex { Direction = FlexDirection.Column };
                // 按日期轮换值班人：同一天的三个班不重复
                for (var index = 0; index < Shifts.Length; index++)
                    column.Children.Add(new Flex
                    {
                        Align = FlexAlign.Center,
                        Gap = Space.Xs,
                        Children =
                        {
                            new Text { Text = Shifts[index].Name, Size = TextSize.Xs, Weight = TextWeight.Semibold, Tone = Shifts[index].Tone },
                            new Small { Text = People[(date.Day + index * 2) % People.Length] },
                        },
                    });
                return column;
            },
        };
        AutomationProperties.SetName(board, "车间排班");
        return board;
    }
}
