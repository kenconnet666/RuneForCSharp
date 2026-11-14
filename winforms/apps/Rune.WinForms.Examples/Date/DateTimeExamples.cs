namespace Rune.WinForms.Examples;

/// <summary>DateTime 页的示例（vue-tsx-pro 文档站 date/DateTime）：DateTimeTextControl、RelativeTimeControl、CountdownControl 与工作日时限。</summary>
static class DateTimeExamples
{
    public static Example[] All =>
    [
        new("DateTime", "activity", "相对时间", "RelativeTimeControl 按相差多久自动取单位，措辞是\"昨天\"而不是\"1 天前\"，随时间更新（秒级每秒，分钟级每 10 秒，更远的更慢，没有窗口句柄时停下）。Threshold 以外直接写日期；悬停看完整的日期时间。Format 是详略：Long（默认）、Short、Narrow。列表项的说明用 DescriptionContent 放控件。", Activity),
        new("DateTime", "countdowns", "倒计时", "CountdownControl 默认显示\"时:分:秒\"，超过一天时前面加天数；Variant=Blocks 是一格一格的数字块（限时抢购），字号随控件的字体；Formatter 拿到拆好的天、时、分、秒，返回要显示的文字。到点时 Ended 触发一次（发送验证码后恢复按钮）。几个计时器对齐到整秒一起跳；Precision=Minute 只到分、每分钟更新一次，适合以天计的期限。", Countdowns),
        new("DateTime", "formats", "日期时间的写法", "DateTimeTextControl 按语言包的语言与时区写，MachineValue 是机器可读的值。不写格式时只有日期的值显示日期，带时间的再加上时分；值可以是 DateTimeOffset、DateTime、DateOnly 或 ISO 字符串。Format 是 .NET 的格式字符串，按语言包的文化写。", Formats),
        new("DateTime", "deadlines", "工作日时限", "\"验收完成后 10 个工作日内付款\"这类时限：WorkCalendar.DeadlineOf 由开始日加工作日（或直接给截止日）算出截止日、剩余工作日与状态（还早、快到期、今天到期、超期、已完成）。工作日按 ProviderControl 的 WorkCalendar 算：带上法定节假日与调休补班；不配置时只按周末。WorkdaysBetween、AddWorkdays 是同一套计算。", Deadlines),
    ];

    static Control Activity()
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
        var times = events.Select(e => new RelativeTimeControl { Value = e.At, Threshold = TimeSpan.FromDays(7), Tone = TextTone.Muted, TextSize = TextSize.Xs }).ToList();
        var list = new ItemListControl { Dock = DockStyle.Top };
        for (var i = 0; i < events.Length; i++)
            list.Controls.Add(new ItemListItemControl
            {
                Align = ItemListAlign.Start,
                Start = new AvatarControl { TextSize = TextSize.Sm, DisplayName = events[i].Who },
                Text = $"{events[i].Who} {events[i].What}",
                DescriptionContent = times[i],
            });
        var format = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, AccessibleName = "详略" };
        format.Controls.AddRange([.. Enum.GetNames<RelativeTimeStyle>().Select(name => new ToggleItemControl { Value = name, Text = name })]);
        format.Value = nameof(RelativeTimeStyle.Long);
        format.ValueChanged += (_, _) =>
        {
            if (format.Value is not { } value) return;
            foreach (var time in times) time.Format = Enum.Parse<RelativeTimeStyle>(value);
        };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch },
            format, Build.Card(new CardControl { CardPadding = Space.Sm }, list));
    }

    static Control Countdowns()
    {
        // 演示数据：抢购在打开的 27 小时后开始，订单 15 分钟内不付就取消
        var now = DateTimeOffset.Now;
        var sale = Build.Styled(new CountdownControl { To = now.AddMinutes(27 * 60 + 15), Variant = CountdownVariant.Blocks, AccessibleName = "距抢购开始" }, TextSize.Lg);
        var send = new ButtonControl { Variant = ButtonVariant.Soft, Text = "发送验证码" };
        var resend = new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm };
        resend.Controls.Add(send);
        send.Click += (_, _) =>
        {
            // 60 秒后才能重发：按钮旁边倒数，到点时恢复按钮
            var left = new CountdownControl { To = DateTimeOffset.Now.AddSeconds(60), Formatter = remaining => $"{Math.Ceiling(remaining.Total.TotalSeconds)} 秒后重发", ForeColor = TextTone.Muted.ColorIn(ElementContext.Of(resend)) };
            left.Ended += (_, _) =>
            {
                send.Enabled = true;
                resend.Controls.Remove(left);
                left.Dispose();
            };
            send.Enabled = false;
            resend.Controls.Add(left);
        };
        var field = new FieldControl { Label = "手机号", Content = new TextFieldControl { PlaceholderText = "138 0000 0000" } };
        var phone = Build.Flex(new FlexControl { Align = FlexAlign.End, Gap = Space.Sm, Dock = DockStyle.Top }, field, resend);
        phone.SetGrow(field, true);
        return Build.Grid(new GridControl { MinItemWidth = "14em", Balanced = true, Gap = Space.Md },
            Panel(new SmallControl { Text = "双十一抢购距开始", Tone = TextTone.Muted }, sale),
            Panel(
                new SmallControl { Text = "订单待支付，超时自动取消", Tone = TextTone.Muted },
                Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm },
                    Build.Styled(new CountdownControl { To = now.AddMinutes(15) }, TextSize.Lg), new ButtonControl { TextSize = TextSize.Sm, Text = "去支付" })),
            Panel(
                new SmallControl { Text = "满 200 减 30 优惠券", Tone = TextTone.Muted },
                Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xxs },
                    new CountdownControl { To = now.AddMinutes((2 * 24 + 3) * 60 + 15), Precision = CountdownPrecision.Minute }, new TextControl { Text = "后过期" })),
            Build.Card(new CardControl { CardPadding = Space.Md }, phone));

        static CardControl Panel(Control caption, Control content) =>
            Build.Card(new CardControl { CardPadding = Space.Md }, Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Dock = DockStyle.Top }, caption, content));
    }

    static Control Formats()
    {
        const string at = "2026-10-20T14:30";
        (string Label, Func<DateTimeTextControl> Create)[] rows =
        [
            ("默认", () => new DateTimeTextControl { Value = at }),
            ("`DateStyle=Full` `TimeStyle=Short`", () => new DateTimeTextControl { Value = at, DateStyle = DateStyle.Full, TimeStyle = TimeStyle.Short }),
            ("`DateStyle=Short`", () => new DateTimeTextControl { Value = at, DateStyle = DateStyle.Short }),
            ("`Format=\"M\"`", () => new DateTimeTextControl { Value = at, Format = "M" }),
            ("`Format=\"dddd HH:mm\"`", () => new DateTimeTextControl { Value = at, Format = "dddd HH:mm" }),
        ];
        var head = new TableRowControl { Controls = { new TableHeaderCellControl { Text = "写法" }, new TableHeaderCellControl { Text = "简体中文" }, new TableHeaderCellControl { Text = "English" } } };
        var body = new TableBodyControl();
        foreach (var (label, create) in rows)
        {
            var name = new TableHeaderCellControl();
            name.Controls.Add(new CodeTextControl { Source = label });
            body.Controls.Add(new TableRowControl { Controls = { name, In(Lang.ZhCn, create()), In(Lang.EnUs, create()) } });
        }
        var table = new TableControl { Density = TableDensity.Compact, AccessibleName = "日期时间的写法", Controls = { new TableHeadControl { Controls = { head } }, body } };
        return Build.Card(new CardControl { CardPadding = Space.None }, table);

        // 同一个值放进不同语言包的作用域
        static TableCellControl In(Lang lang, Control content)
        {
            var scope = new ProviderControl { Lang = lang, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            scope.Controls.Add(content);
            var cell = new TableCellControl { Nowrap = true };
            cell.Controls.Add(scope);
            return cell;
        }
    }

    /// <summary>国庆放假与调休（应用里写在根上的 ProviderControl.WorkCalendar，所有页面共用）。</summary>
    static readonly WorkCalendar Holidays = new(holidays: [(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 8))], workdays: [new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 10)]);

    static readonly StatusMap DeadlineStates = new()
    {
        [nameof(DeadlineState.Pending)] = new("还早", StatusTone.Neutral),
        [nameof(DeadlineState.Soon)] = new("快到期", StatusTone.Warning),
        [nameof(DeadlineState.Due)] = new("今天到期", StatusTone.Warning),
        [nameof(DeadlineState.Overdue)] = new("超期", StatusTone.Danger),
        [nameof(DeadlineState.Done)] = new("已完成", StatusTone.Success),
    };

    static Control Deadlines()
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
        var head = new TableRowControl();
        head.Controls.AddRange([.. new[] { "事项", "开始", "时限", "截止", "剩余", "状态" }.Select(text => new TableHeaderCellControl { Text = text })]);
        var body = new TableBodyControl();
        foreach (var task in tasks)
        {
            var (due, remaining, state) = Holidays.DeadlineOf(today, start: task.Start, workdays: task.Workdays, done: task.Done);
            var left = state == DeadlineState.Done
                ? remaining > 0 ? $"提前 {remaining} 天" : remaining == 0 ? "按时" : $"晚了 {-remaining} 天"
                : remaining < 0 ? $"超 {-remaining} 天" : $"{remaining} 天";
            body.Controls.Add(new TableRowControl
            {
                Controls =
                {
                    new TableCellControl { Text = task.Name },
                    Cell(new DateTimeTextControl { Value = task.Start }),
                    new TableCellControl { Text = $"{task.Workdays} 个工作日" },
                    Cell(new DateTimeTextControl { Value = due }),
                    Cell(new SmallControl { Text = left, Tone = remaining < 0 ? TextTone.Danger : TextTone.Muted }),
                    Cell(new StatusBadgeControl { Statuses = DeadlineStates, Value = state.ToString(), Radius = Radius.Full }),
                },
            });
        }
        var table = new TableControl { AccessibleName = "审批时限", Dock = DockStyle.Top, Controls = { new TableHeadControl { Controls = { head } }, body } };
        var scope = new ProviderControl { WorkCalendar = Holidays, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        scope.Controls.Add(table);
        return scope;

        static TableCellControl Cell(Control content)
        {
            var cell = new TableCellControl();
            cell.Controls.Add(content);
            return cell;
        }
    }
}
