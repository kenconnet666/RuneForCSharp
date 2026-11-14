using System.Globalization;

namespace Rune.WinForms.Examples;

/// <summary>Timeline 页的示例（vue-tsx-pro 文档站 display/Timeline）。操作记录里改了哪些字段用 ValueChange 列出（还没有 FieldCompare 的列表布局）。</summary>
static class TimelineExamples
{
    public static Example[] All =>
    [
        new("Timeline", "approval", "审批流转记录", "流程的流转事件每条一项：Actor 是处理人，标题写在哪个节点做了什么，处理结果按种类换 Icon 与 Tone（同意、退回、催办、超时自动处理），意见写在 Description，附件做成链接。Reverse 新的在前，Items 仍按时间先后给。", Approval),
        new("Timeline", "order", "订单进度", "每项一个节点（圆点或 Icon）与标题、时间、说明；DateTime 是机器可读的时间（时间的读屏描述）；Pending 是还在进行中的最后一项。", Order),
        new("Timeline", "releases", "版本记录：左右交替、小一号", "Alternate 让事件左右交替排布，Tone 给节点着色，TextSize 小一号；宽度不到 32em 时自动排回一侧。应用里统一的档位写在 ProviderControl 的 TimelineDefaults。", Releases),
        new("Timeline", "change-log", "操作记录：操作人、字段差异、收起", "项写 Actor 是操作人，显示在时间前面（Avatar 是头像，文字的操作人没有头像时显示首字母）；改了哪些字段放进 Description，长的内容放 Details，项里一个“查看详情”的按钮点开才显示（打开时才建）。MaxVisible 只显示最新的几项，其余收起在“展开其余 N 条”后面，按钮在旧的那一端（倒序时在最后），展开后变成“收起”；ExpandedChanged 报告展开与收起。", ChangeLog),
    ];

    static TimelineItem At(string title, string dateTime, object? actor = null, StatusTone tone = default, IconData? icon = null, object? description = null, Image? avatar = null)
    {
        var at = DateTimeOffset.Parse(dateTime + ":00+08:00", CultureInfo.InvariantCulture);
        // 时间写成"月-日 时:分"，机器可读的完整时间进 DateTime
        return new() { Title = title, Actor = actor, Avatar = avatar, Time = dateTime[5..16].Replace('T', ' '), DateTime = at, Tone = tone, Icon = icon, Description = description };
    }

    static Control Approval() => Build.Card(new CardControl(), new TimelineControl
    {
        Reverse = true,
        Items =
        [
            At("发起", "2026-10-14T09:02", "周宁", StatusTone.Accent, Lucide.CirclePlay),
            At("认领了 部门审批", "2026-10-14T10:15", "陆川", icon: Lucide.Hand),
            At("部门审批：退回修改", "2026-10-14T10:40", "陆川", StatusTone.Warning, Lucide.CornerUpLeft, "报价只有一家，请再比价一家后提交"),
            At("修改申请：重新提交", "2026-10-15T16:20", "周宁", StatusTone.Success, Lucide.Check,
                Build.Flex(new FlexControl { Gap = Space.Sm, Wrap = true }, new LinkControl { Text = "比价单.xlsx" }, new LinkControl { Text = "报价-乙公司.pdf" })),
            At("部门审批：同意", "2026-10-16T09:05", "陆川", StatusTone.Success, Lucide.Check),
            At("催办 预算核对", "2026-10-19T09:30", "周宁", StatusTone.Info, Lucide.BellRing, "供应商月底调价，麻烦尽快"),
            At("预算核对 没人认领，转负责人指定", "2026-10-21T09:00", "系统", StatusTone.Warning, Lucide.Hourglass),
            At("指定 方圆 处理 预算核对", "2026-10-21T09:12", "韩笑", StatusTone.Accent, Lucide.UserCheck),
            At("预算核对：核对无误（超时自动处理）", "2026-10-24T00:00", "系统", StatusTone.Info, Lucide.Clock, "超过时限，视为无异议"),
        ],
    });

    static Control Order() => Build.Card(new CardControl(), new TimelineControl
    {
        Pending = "正在派送，预计今天 18:00 前送达",
        Items =
        [
            At("已下单", "2026-10-18T09:12", icon: Lucide.CircleCheck),
            At("已付款", "2026-10-18T09:13", icon: Lucide.CreditCard, description: "微信支付 ¥1,280.00"),
            At("已出库", "2026-10-18T15:40", icon: Lucide.Package),
            At("运输中", "2026-10-19T06:20", tone: StatusTone.Accent, icon: Lucide.Truck, description: "快件已到达【上海徐汇营业部】"),
        ],
    });

    static Control Releases() => new TimelineControl
    {
        Alternate = true,
        TextSize = TextSize.Sm,
        Items =
        [
            new()
            {
                Title = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs },
                    new TextControl { Text = "v2.4.0", Weight = TextWeight.Medium }, new BadgeControl { Text = "最新", Tone = StatusTone.Accent, TextSize = TextSize.Xs }),
                Time = "2026-10-20", Tone = StatusTone.Accent, Description = "十二套内置主题、日期组件第 1 层、3D 效果。",
            },
            new() { Title = "v2.3.0", Time = "2026-09-02", Tone = StatusTone.Success, Description = "表单 schema 推出字段约束。" },
            new() { Title = "v2.2.1", Time = "2026-08-11", Tone = StatusTone.Warning, Description = "修复 Safari 下弹层闪烁。" },
            new() { Title = "v2.2.0", Time = "2026-07-25", Description = "虚拟列表与树组件。" },
            new() { Title = "v2.0.0", Time = "2026-05-30", Tone = StatusTone.Danger, Description = "破坏性变更：符文改名。" },
        ],
    };

    sealed record Quote(decimal UnitPrice, int Quantity, int LeadDays, string Supplier, string Remark);

    static readonly (string Header, Func<Quote, string> Text)[] QuoteFields =
    [
        ("单价", quote => quote.UnitPrice.ToString("¥#,0.00", CultureInfo.InvariantCulture)),
        ("数量", quote => quote.Quantity.ToString("#,0", CultureInfo.InvariantCulture)),
        ("交期（天）", quote => quote.LeadDays.ToString(CultureInfo.InvariantCulture)),
        ("供应商", quote => quote.Supplier),
        ("备注", quote => quote.Remark),
    ];

    /// <summary>一次修改：项里列改了的字段，详情里是全部字段的前后对比。</summary>
    static (Control Description, Control Details) Change(Quote before, Quote after)
    {
        var changed = new DescriptionsControl { Columns = 1, TextSize = TextSize.Sm };
        var all = new DescriptionsControl { Columns = 1, TextSize = TextSize.Sm };
        foreach (var (header, text) in QuoteFields)
        {
            var (from, to) = (text(before), text(after));
            if (from != to) changed.Items.Add(new() { Label = header, Value = new ValueChangeControl { Before = from, After = to } });
            all.Items.Add(new() { Label = header, Value = from == to ? to : new ValueChangeControl { Before = from, After = to } });
        }
        return (changed, all);
    }

    static Control ChangeLog()
    {
        var v1 = new Quote(86.5m, 2_000, 30, "华辰精密", "含税含运");
        var v2 = v1 with { UnitPrice = 82m, LeadDays = 25 };
        var v3 = v2 with { Quantity = 2_400, Remark = "含税含运，分两批交付" };
        var v4 = v3 with { UnitPrice = 79.8m, Supplier = "恒远五金" };
        // 陆川有头像，其他人显示首字母
        var luChuan = Photos.Members[1].Photo;
        TimelineItem Edit(string actor, string dateTime, Quote before, Quote after, string title = "修改报价")
        {
            var (description, details) = Change(before, after);
            var item = At(title, dateTime, actor, icon: Lucide.Pencil);
            return new() { Title = item.Title, Actor = item.Actor, Time = item.Time, DateTime = item.DateTime, Icon = item.Icon, Description = description, Details = details };
        }
        return Build.Card(new CardControl(), new TimelineControl
        {
            Reverse = true,
            MaxVisible = 4,
            Items =
            [
                At("创建报价单", "2026-10-14T09:02", "周宁", StatusTone.Accent, Lucide.FilePlusCorner),
                Edit("周宁", "2026-10-14T15:40", v1, v2),
                At("提交审批", "2026-10-15T10:05", "周宁", StatusTone.Info, Lucide.Send),
                At("退回修改", "2026-10-16T11:20", "陆川", StatusTone.Warning, Lucide.Undo2, "数量按 Q4 排产计划补到 2,400，备注写明分批交付", luChuan),
                Edit("周宁", "2026-10-16T16:48", v2, v3),
                Edit("方圆", "2026-10-18T09:30", v3, v4, "换供应商重新比价"),
                At("审批通过", "2026-10-18T14:12", "陆川", StatusTone.Success, Lucide.ShieldCheck, avatar: luChuan),
            ],
        });
    }
}
