using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf.Examples;

/// <summary>DateTime 页的示例（vue-tsx-pro 文档站 date/DateTime）：DateTimeText、RelativeTime、Countdown 与工作日时限。</summary>
static class DateTimeExamples
{
    public static Example[] All =>
    [
        new("DateTime", "activity", "相对时间", "RelativeTime 按相差多久自动取单位，措辞是\"昨天\"而不是\"1 天前\"，随时间更新（秒级每秒，分钟级每 10 秒，更远的更慢，不在界面上时停下）。Threshold 以外直接写日期；悬停看完整的日期时间。Format 是详略：Long（默认）、Short、Narrow。", Activity),
        new("DateTime", "countdowns", "倒计时", "Countdown 默认显示\"时:分:秒\"，超过一天时前面加天数；Variant=Blocks 是一格一格的数字块（限时抢购），字号随上下文；Formatter 拿到拆好的天、时、分、秒，返回要显示的文字。读屏的条目类型是 timer，到点时 Ended 触发一次（发送验证码后恢复按钮）。几个计时器对齐到整秒一起跳；Precision=Minute 只到分、每分钟更新一次，适合以天计的期限。", Countdowns),
        new("DateTime", "formats", "日期时间的写法", "DateTimeText 按语言包的语言与时区写，MachineValue 是机器可读的值。不写格式时只有日期的值显示日期，带时间的再加上时分；值可以是 DateTimeOffset、DateTime、DateOnly 或 ISO 字符串。Format 是 .NET 的格式字符串，按语言包的文化写。", Formats),
        new("DateTime", "deadlines", "工作日时限", "\"验收完成后 10 个工作日内付款\"这类时限：WorkCalendar.DeadlineOf 由开始日加工作日（或直接给截止日）算出截止日、剩余工作日与状态（还早、快到期、今天到期、超期、已完成）。工作日按 Provider 的 WorkCalendar 算：带上法定节假日与调休补班；不配置时只按周末。WorkdaysBetween、AddWorkdays 是同一套计算。", Deadlines),
    ];

    static FrameworkElement Activity()
    {
        // 演示数据相对打开的时刻
        var opened = DateTimeOffset.Now;
        (string Who, string What, DateTimeOffset At)[] events =
        [
            ("林小满", "评论了\"首页改版\"", opened.AddSeconds(-20)),
            ("周一航", "合并了 #1024", opened.AddMinutes(-7)),
            ("陈默", "把\"支付回调\"标为已解决", opened.AddHours(-3)),
            ("许悠", "创建了里程碑\"v2.0\"", opened.AddHours(-26)),
            ("王晴", "上传了设计稿", opened.AddDays(-12)),
        ];
        var times = events.Select(e => new RelativeTime { Value = e.At, Threshold = TimeSpan.FromDays(7), Tone = TextTone.Muted, Size = TextSize.Sm }).ToList();
        var list = new ItemList();
        for (var i = 0; i < events.Length; i++)
            list.Children.Add(new ItemListItem
            {
                Align = ItemListAlign.Start,
                Start = new Avatar { Size = TextSize.Sm, DisplayName = events[i].Who },
                Title = Title(events[i].Who, events[i].What),
                Description = times[i],
            });
        var format = new ToggleGroup
        {
            IsRequired = true, Size = TextSize.Sm, Value = "Long",
            Children = { new ToggleItem { Value = "Long", Content = "Long" }, new ToggleItem { Value = "Short", Content = "Short" }, new ToggleItem { Value = "Narrow", Content = "Narrow" } },
        };
        AutomationProperties.SetName(format, "详略");
        DependencyPropertyDescriptor.FromProperty(ToggleGroup.ValueProperty, typeof(ToggleGroup)).AddValueChanged(format, (_, _) =>
        {
            if (format.Value is not { } value) return;
            foreach (var time in times) time.Format = Enum.Parse<RelativeTimeStyle>(value);
        });
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { format, new Card { Padding = Space.Sm, Content = list } } };

        static Text Title(string who, string what)
        {
            var title = new Text();
            title.Inlines.Add(new Run(who) { FontWeight = FontWeights.Medium });
            title.Inlines.Add(new Run(" " + what));
            return title;
        }
    }

    static FrameworkElement Countdowns()
    {
        // 演示数据：抢购在打开的 27 小时后开始，订单 15 分钟内不付就取消
        var now = DateTimeOffset.Now;
        var sale = new Countdown { To = now.AddMinutes(27 * 60 + 15), Variant = CountdownVariant.Blocks };
        AutomationProperties.SetName(sale, "距抢购开始");
        var send = new Button { Variant = ButtonVariant.Soft, Content = "发送验证码" };
        send.Click += (_, _) =>
        {
            // 60 秒后才能重发：到点时恢复按钮
            var resend = new Countdown { To = DateTimeOffset.Now.AddSeconds(60), Formatter = left => $"{Math.Ceiling(left.Total.TotalSeconds)}" };
            resend.Ended += (_, _) =>
            {
                send.Content = "发送验证码";
                send.IsEnabled = true;
            };
            send.Content = new Flex { Align = FlexAlign.Center, Gap = Space.Xxs, Children = { resend, new Text { Text = "秒后重发" } } };
            send.IsEnabled = false;
        };
        return new Grid
        {
            MinItemWidth = "14em", Balanced = true, Gap = Space.Md,
            Children =
            {
                Panel(new Small { Text = "双十一抢购距开始", Tone = TextTone.Muted }, Large(sale)),
                Panel(
                    new Small { Text = "订单待支付，超时自动取消", Tone = TextTone.Muted },
                    new Flex
                    {
                        Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm,
                        Children = { Large(new Countdown { To = now.AddMinutes(15) }, semibold: true), new Button { Size = TextSize.Sm, Content = "去支付" } },
                    }),
                Panel(
                    new Small { Text = "满 200 减 30 优惠券", Tone = TextTone.Muted },
                    new Flex { Align = FlexAlign.Center, Gap = Space.Xxs, Children = { new Countdown { To = now.AddMinutes((2 * 24 + 3) * 60 + 15), Precision = CountdownPrecision.Minute }, new Text { Text = "后过期" } } }),
                new Card
                {
                    Padding = Space.Md,
                    Content = new Flex
                    {
                        Align = FlexAlign.End, Gap = Space.Sm,
                        Children = { new Field { Label = "手机号", Content = new TextField { PlaceholderText = "138 0000 0000" } }, send },
                    },
                },
            },
        };

        static Card Panel(FrameworkElement caption, FrameworkElement content) =>
            new() { Padding = Space.Md, Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { caption, content } } };

        // 倒计时的字号、字重随上下文（继承），也可以直接写：这里给主题的 lg 字号
        static FrameworkElement Large(Countdown countdown, bool semibold = false)
        {
            countdown.SetResourceReference(Control.FontSizeProperty, ThemeKeys.FontSize.Lg);
            if (semibold) countdown.SetResourceReference(Control.FontWeightProperty, ThemeKeys.FontWeight.Semibold);
            return countdown;
        }
    }

    static FrameworkElement Formats()
    {
        const string at = "2026-10-20T14:30";
        (string Label, Func<DateTimeText> Create)[] rows =
        [
            ("默认", () => new DateTimeText { Value = at }),
            ("`DateStyle=Full` `TimeStyle=Short`", () => new DateTimeText { Value = at, DateStyle = DateStyle.Full, TimeStyle = TimeStyle.Short }),
            ("`DateStyle=Short`", () => new DateTimeText { Value = at, DateStyle = DateStyle.Short }),
            ("`Format=\"M\"`", () => new DateTimeText { Value = at, Format = "M" }),
            ("`Format=\"dddd HH:mm\"`", () => new DateTimeText { Value = at, Format = "dddd HH:mm" }),
        ];
        var head = new TableRow { Cells = { new TableHeaderCell { Content = "写法" }, new TableHeaderCell { Content = "简体中文" }, new TableHeaderCell { Content = "English" } } };
        var body = new TableBody();
        foreach (var (label, create) in rows)
            body.Rows.Add(new TableRow
            {
                Cells =
                {
                    new TableHeaderCell { Content = new CodeText { Source = label } },
                    new TableCell { Nowrap = true, Content = In(Lang.ZhCn, create()) },
                    new TableCell { Nowrap = true, Content = In(Lang.EnUs, create()) },
                },
            });
        var table = new Table { Density = TableDensity.Compact, Sections = { new TableHead { Rows = { head } }, body } };
        AutomationProperties.SetName(table, "日期时间的写法");
        return new Card { Padding = Space.None, Content = table };

        static FrameworkElement In(Lang lang, FrameworkElement content)
        {
            Provider.SetLang(content, lang);
            return content;
        }
    }

    /// <summary>国庆放假与调休（应用里写在根上的 Provider.WorkCalendar，所有页面共用）。</summary>
    static readonly WorkCalendar Holidays = new(holidays: [(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 8))], workdays: [new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 10)]);

    static readonly StatusMap DeadlineStates = new()
    {
        [nameof(DeadlineState.Pending)] = new("还早", StatusTone.Neutral),
        [nameof(DeadlineState.Soon)] = new("快到期", StatusTone.Warning),
        [nameof(DeadlineState.Due)] = new("今天到期", StatusTone.Warning),
        [nameof(DeadlineState.Overdue)] = new("超期", StatusTone.Danger),
        [nameof(DeadlineState.Done)] = new("已完成", StatusTone.Success),
    };

    static FrameworkElement Deadlines()
    {
        // 示例里的"今天"固定下来，状态每次都一样
        var today = new DateOnly(2026, 10, 21);
        (string Name, DateOnly Start, int Workdays, DateOnly? Done)[] tasks =
        [
            // 跨国庆：放假的日子不算、10 月 10 日（周六）调休算，10 个工作日后是 10 月 21 日
            ("财务复核", new(2026, 9, 30), 10, null),
            ("回填结算金额", new(2026, 10, 9), 5, null),
            ("工程评估", new(2026, 10, 19), 4, null),
            ("供应商报价", new(2026, 10, 9), 3, new(2026, 10, 12)),
            ("方案评审", new(2026, 10, 15), 10, null),
        ];
        var head = new TableRow
        {
            Cells = { new TableHeaderCell { Content = "事项" }, new TableHeaderCell { Content = "开始" }, new TableHeaderCell { Content = "时限" }, new TableHeaderCell { Content = "截止" }, new TableHeaderCell { Content = "剩余" }, new TableHeaderCell { Content = "状态" } },
        };
        var body = new TableBody();
        foreach (var task in tasks)
        {
            var (due, remaining, state) = Holidays.DeadlineOf(today, start: task.Start, workdays: task.Workdays, done: task.Done);
            var left = state == DeadlineState.Done
                ? remaining > 0 ? $"提前 {remaining} 天" : remaining == 0 ? "按时" : $"晚了 {-remaining} 天"
                : remaining < 0 ? $"超 {-remaining} 天" : $"{remaining} 天";
            body.Rows.Add(new TableRow
            {
                Cells =
                {
                    new TableCell { Content = task.Name },
                    new TableCell { Content = new DateTimeText { Value = task.Start } },
                    new TableCell { Content = $"{task.Workdays} 个工作日" },
                    new TableCell { Content = new DateTimeText { Value = due } },
                    new TableCell { Content = new Small { Text = left, Tone = remaining < 0 ? TextTone.Danger : TextTone.Muted } },
                    new TableCell { Content = new StatusBadge { Statuses = DeadlineStates, Value = state.ToString(), Radius = Radius.Full } },
                },
            });
        }
        var table = new Table { Sections = { new TableHead { Rows = { head } }, body } };
        AutomationProperties.SetName(table, "审批时限");
        Provider.SetWorkCalendar(table, Holidays);
        return table;
    }
}
