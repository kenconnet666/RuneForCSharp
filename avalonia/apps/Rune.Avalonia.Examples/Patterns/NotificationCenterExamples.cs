using System.Globalization;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>NotificationCenter 页的示例（vue-tsx-pro 文档站 patterns/NotificationCenter）。读过的状态由使用方保存：MarkedRead 里改 Items 再给回去。</summary>
static class NotificationCenterExamples
{
    public static Example[] All =>
    [
        new("NotificationCenter", "top-bar", "顶栏的铃铛", "未读数、按类型筛选、只看未读、点一条标为已读、全部标为已读；读过的状态由使用方保存。", TopBar),
        new("NotificationCenter", "inbox", "通知页面", "NotificationList 直接放在卡片里，不进弹出层；列表限高滚动，标题行换成卡片自己的标题。", Inbox),
        new("NotificationCenter", "widget", "工作台上的提醒", "只放要我处理的提醒（应用自己筛出来）：自定义标题、没有类型筛选，读过的就拿掉，底部链到收件箱。", Widget),
    ];

    static readonly DateTimeOffset Now = At(21, "10:00");

    static DateTimeOffset At(int day, string time) => DateTimeOffset.Parse($"2026-10-{day:00}T{time}:00+08:00", CultureInfo.InvariantCulture);

    static readonly NotificationType[] Types = [new("todo", "待办"), new("returned", "退回"), new("remind", "催办")];

    static NotificationItem[] Seed() =>
    [
        new("1", "新的待办：资料审核", At(21, "09:42")) { Description = "PO-2026-0187 · 办公椅供应商切换", Type = "todo" },
        new("2", "被退回：方案设计", At(21, "08:10")) { Description = "PO-2026-0214 · 验收报告缺少试验报告", Type = "returned", Tone = StatusTone.Warning },
        new("3", "催办：财务复核", At(20, "16:30")) { Description = "PO-2026-0233 · 请今天内验收", Type = "remind", Tone = StatusTone.Danger },
        new("4", "超时：结算已超过 30 天", At(20, "09:00")) { Description = "PO-2026-0102 · 供应商乙对账未回复", Type = "remind", Tone = StatusTone.Danger },
        new("5", "财务部收件箱有新的待办：费用核算", At(19, "11:00")) { Description = "PO-2026-0179 · 包装纸箱第二供应商", Type = "todo", IsRead = true },
        new("6", "计划调整已通过：货架紧固件减量", At(18, "15:20")) { Description = "PO-2026-0276 · 计划完成改为 2026-12-15", Type = "todo", IsRead = true },
    ];

    static NotificationItem[] MarkRead(IReadOnlyList<NotificationItem> items, IReadOnlyList<string> ids) => [.. items.Select(item => ids.Contains(item.Id) ? item with { IsRead = true } : item)];

    static Control TopBar()
    {
        var status = new Small { Tone = TextTone.Muted };
        var restore = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Content = "恢复演示数据" };
        var center = new NotificationCenter { Items = Seed(), Types = Types, Now = Now, Footer = restore };
        void Sync(string? clicked = null) => status.Text = clicked is not null ? $"点了：{clicked}" : $"{center.Items.Count(item => !item.IsRead)} 条未读，点铃铛打开";
        center.MarkedRead += (_, e) =>
        {
            center.Items = MarkRead(center.Items, e.Ids);
            Sync();
        };
        center.ItemClicked += (_, e) => Sync(e.Item.Title);
        restore.Click += (_, _) =>
        {
            center.Items = Seed();
            Sync();
        };
        Sync();
        var search = new TextField { Icon = Lucide.Search, Size = TextSize.Sm, PlaceholderText = "搜索编号、名称", MaxWidth = 320 };
        Flex.SetGrow(search, true);
        var bar = new Flex
        {
            Align = FlexAlign.Center,
            Gap = Space.Md,
            Children = { new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Icon { Data = Lucide.Plane }, new Strong { Text = "采购管理平台" } } }, search, center },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { new Card { Padding = Space.Sm, Content = bar }, status } };
    }

    static Control Inbox()
    {
        var list = new NotificationList { Items = Seed(), Types = Types, Now = Now, ShowTitle = false, ListMaxHeight = 352 };
        list.MarkedRead += (_, e) => list.Items = MarkRead(list.Items, e.Ids);
        return new Card { Padding = Space.None, Header = new H3 { Text = "收件箱", Size = TextSize.Sm }, Content = list };
    }

    static Control Widget()
    {
        var list = new NotificationList
        {
            Title = "要我处理的提醒",
            Now = Now,
            Empty = "都处理完了",
            Items =
            [
                new("r1", "催办：财务复核", At(20, "16:30")) { Description = "PO-2026-0233 · 请今天内验收", Tone = StatusTone.Danger },
                new("r2", "超时：结算已超过 30 天", At(20, "09:00")) { Description = "PO-2026-0102 · 供应商乙对账未回复", Tone = StatusTone.Danger },
                new("r3", "今天到期：资料审核", At(21, "08:00")) { Description = "PO-2026-0187 · 办公椅供应商切换", Tone = StatusTone.Warning },
            ],
            Footer = new Link { Href = new Uri("https://example.com/inbox"), Content = "打开收件箱" },
        };
        // 读过的就从提醒里拿掉
        list.MarkedRead += (_, e) => list.Items = [.. list.Items.Where(item => !e.Ids.Contains(item.Id))];
        return new Card { Padding = Space.None, Content = list, MaxWidth = 420 };
    }
}
