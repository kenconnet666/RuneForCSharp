using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>Badge 的示例（vue-tsx-pro 文档站 base/Badge，StatusBadge、StatusDot、Tag 也在这页）。</summary>
static class BadgeExamples
{
    public static Example[] All =>
    [
        new("Badge", "matrix", "样式与色调", "Variant 是 solid、soft（默认）、surface、outline，Tone 是六种状态色调；颜色都取主题 token，每套主题都过对比度检查。", () => new BadgeMatrix()),
        new("Badge", "shapes", "尺寸、形状与图标", "Size 默认 xs；Radius=\"full\" 是胶囊，适合计数与在线状态；Icon 放在文字前。", () => new BadgeShapes()),
        new("Badge", "in-context", "表格里的状态", "状态列用胶囊形的 soft 标记：颜色区分状态，文字说明状态，不只靠颜色。StatusBadge 的名字与色调取自状态映射，文字前一个同色的圆点（Badge 的 Dot）。", InContext),
        new("Badge", "status-dot", "状态色点", "StatusDot 比 Badge 轻，标出未读、新到、退回、图例。色调与 Badge 同名，Color 给任意颜色；点本身就是信息时写 Label，否则对读屏隐藏。", StatusDots),
        new("Badge", "statuses", "状态映射", "StatusMap 把每种状态的名字、色调、图标与说明写在一处，定义的顺序就是阶段的顺序（Order）；Options() 给筛选的下拉选项。接口多给了不认识的状态时显示成中性色、名字就是 key，不报错。", Statuses),
        new("Badge", "tag", "Tag 标签", "Tag 的外观选项与 Badge 相同，作用域里 Badge 的默认值一起改两者；技能、分类、关键词这类成组出现的短文字用它。", TagBasic),
        new("Badge", "tag-removable", "可移除的标签", "订阅了 RemoveRequested 就有移除按钮：先淡出，离场结束才发；按钮的读屏名称是“移除 + 标签文字”，RemoveLabel 可以改。", TagRemovable),
        new("Badge", "tag-selectable", "可选中的标签", "写了 IsSelected 时整个标签是切换按钮，点一下切换：没选中时有描边，选中时实底色、前面一个对勾（不只靠颜色），不写色调时用 accent。筛选条件这类一排可选的短文字用它；要“只能选一个”的分段控件用 ToggleGroup。", TagSelectable),
    ];

    // 状态写在一处：表格、筛选、详情里都是同一个名字与颜色
    static readonly StatusMap OrderStatuses = new()
    {
        ["pending"] = new("待付款", StatusTone.Warning),
        ["packing"] = new("备货中", StatusTone.Info),
        ["shipped"] = new("已发货", StatusTone.Success),
        ["refunded"] = new("已退款", StatusTone.Danger),
        ["cancelled"] = new("已取消", StatusTone.Neutral),
    };

    static readonly (string Id, string Customer, string Amount, string Status)[] Orders =
    [
        ("#10234", "林小满", "¥1,280.00", "shipped"), ("#10233", "周一航", "¥459.00", "packing"), ("#10232", "陈默", "¥2,099.00", "pending"),
        ("#10231", "吴悠", "¥89.00", "refunded"), ("#10230", "赵晴", "¥320.00", "cancelled"),
    ];

    static Control InContext()
    {
        var head = new TableRow
        {
            Cells = { new TableHeaderCell { Content = "订单" }, new TableHeaderCell { Content = "客户" }, new TableHeaderCell { Content = "金额", Align = TableAlign.End }, new TableHeaderCell { Content = "状态" } },
        };
        var body = new TableBody();
        foreach (var (id, customer, amount, status) in Orders)
            body.Rows.Add(new TableRow
            {
                Cells =
                {
                    new TableCell { Content = new Strong { Text = id } },
                    new TableCell { Content = customer },
                    new TableCell { Align = TableAlign.End, Content = new Text { Tone = TextTone.Muted, Text = amount } },
                    new TableCell { Content = new StatusBadge { Statuses = OrderStatuses, Value = status, Radius = Radius.Full } },
                },
            });
        return new Card { Padding = Space.None, Content = new Table { Sections = { new TableHead { Rows = { head } }, body } } };
    }

    // 状态色点：六种色调、两种大小；点本身就是信息时写 Label（读屏念），否则只是装饰
    static Control StatusDots()
    {
        static Flex Dot(StatusDot dot, string text) => new() { Inline = true, Align = FlexAlign.Center, Gap = Space.Xs, Children = { dot, new Small { Text = text } } };
        var tones = new Flex { Gap = Space.Lg, Wrap = FlexWrap.Wrap, Align = FlexAlign.Center };
        foreach (var tone in new[] { StatusTone.Accent, StatusTone.Info, StatusTone.Success, StatusTone.Warning, StatusTone.Danger, StatusTone.Neutral })
            tones.Children.Add(Dot(new StatusDot { Tone = tone }, tone.ToString()));
        var more = new Flex
        {
            Gap = Space.Lg, Align = FlexAlign.Center,
            Children =
            {
                Dot(new StatusDot { Size = StatusDotSize.Md, Color = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#8e4ec6")) }, "Size=\"md\"，任意颜色（图例）"),
                Dot(new StatusDot { Tone = StatusTone.Danger, Label = "未读" }, "写了 Label：读屏念“未读”"),
            },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Children = { tones, more } };
    }

    static readonly StatusMap TaskStatuses = new()
    {
        ["pending"] = new("待启动", StatusTone.Neutral),
        ["designing"] = new("设计中", StatusTone.Info),
        ["verifying"] = new("测试中", StatusTone.Info),
        ["awaiting"] = new("待验收", StatusTone.Warning, Description: "等需求方出具验收报告"),
        ["accepted"] = new("已验收", StatusTone.Accent),
        ["settling"] = new("结算中", StatusTone.Warning, Description: "财务对账，30 天内回填结算金额"),
        ["settled"] = new("已结算", StatusTone.Success),
        ["closed"] = new("关闭", StatusTone.Success),
    };

    static readonly StatusMap Exceptions = new()
    {
        ["paused"] = new("暂停", StatusTone.Warning, Lucide.CirclePause),
        ["terminated"] = new("终止", StatusTone.Danger, Lucide.CircleSlash),
        ["queued"] = new("待排期", StatusTone.Neutral, Lucide.Hourglass),
    };

    // 状态映射：任务的八个阶段与三个异常状态写在一处，定义顺序就是阶段顺序；StatusBadge 按它显示名字与色调，筛选的下拉选项也从它来
    static Control Statuses()
    {
        var stages = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Wrap = FlexWrap.Wrap };
        for (var i = 0; i < TaskStatuses.Keys.Count; i++)
        {
            var stage = new Flex { Inline = true, Align = FlexAlign.Center, Gap = Space.Xs };
            if (i > 0) stage.Children.Add(new Icon { Data = Lucide.ChevronRight, Tone = TextTone.Muted });
            stage.Children.Add(new StatusBadge { Statuses = TaskStatuses, Value = TaskStatuses.Keys[i], Radius = Radius.Full });
            stages.Children.Add(stage);
        }
        var exceptions = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { new Small { Tone = TextTone.Muted, Text = "异常状态" } } };
        foreach (var key in Exceptions.Keys) exceptions.Children.Add(new StatusBadge { Statuses = Exceptions, Value = key, Variant = StatusVariant.Outline });
        var filter = new Field
        {
            Label = "按状态筛选", Description = "选项来自同一份状态映射，带说明",
            Content = new Select { ItemsSource = TaskStatuses.Options(), SelectionMode = SelectionMode.Multiple, Values = ["verifying", "awaiting"], Clearable = true },
        };
        var note = new Flex
        {
            Align = FlexAlign.Center, Gap = Space.Xs, Wrap = FlexWrap.Wrap,
            Children =
            {
                new Text { Size = TextSize.Sm, Tone = TextTone.Muted, Text = $"“测试中”排在第 {TaskStatuses.Order("verifying") + 1} 个阶段；不认识的状态显示成：" },
                new StatusBadge { Statuses = TaskStatuses, Value = "archived" },
            },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch, Children = { stages, exceptions, filter, note } };
    }

    static readonly string[] Conditions = ["加急", "待我审批", "已逾期", "本周到期", "有附件"];

    static Control TagSelectable()
    {
        var picked = new HashSet<string> { "加急", "已逾期" };
        var summary = new Small { Tone = TextTone.Muted };
        void Sync() => summary.Text = $"已选：{(picked.Count == 0 ? "（无）" : string.Join("、", Conditions.Where(picked.Contains)))}";
        var row = new Flex { Gap = Space.Xs, Wrap = FlexWrap.Wrap };
        global::Avalonia.Automation.AutomationProperties.SetName(row, "筛选");
        foreach (var option in Conditions)
        {
            var tag = new Tag { Content = option, IsSelected = picked.Contains(option) };
            tag.PropertyChanged += (_, e) =>
            {
                if (e.Property != Tag.IsSelectedProperty) return;
                if (tag.IsSelected == true) picked.Add(option);
                else picked.Remove(option);
                Sync();
            };
            row.Children.Add(tag);
        }
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { row, summary } };
    }

    static Control TagBasic()
    {
        static Flex Row(string label, params Tag[] tags)
        {
            var row = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = FlexWrap.Wrap };
            row.Children.Add(new Text { Size = TextSize.Xs, Tone = TextTone.Muted, Text = label });
            foreach (var tag in tags) row.Children.Add(tag);
            return row;
        }
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children =
            {
                Row("技能",
                    new Tag { Icon = Lucide.CodeXml, Content = "TypeScript" },
                    new Tag { Icon = Lucide.Palette, Tone = StatusTone.Accent, Content = "设计系统" },
                    new Tag { Icon = Lucide.Database, Tone = StatusTone.Info, Content = "PostgreSQL" },
                    new Tag { Icon = Lucide.Smartphone, Tone = StatusTone.Success, Content = "移动端" },
                    new Tag { Icon = Lucide.Globe, Tone = StatusTone.Warning, Content = "国际化" }),
                Row("样式",
                    new Tag { Variant = StatusVariant.Solid, Tone = StatusTone.Accent, Content = "solid" },
                    new Tag { Variant = StatusVariant.Soft, Tone = StatusTone.Accent, Content = "soft" },
                    new Tag { Variant = StatusVariant.Surface, Tone = StatusTone.Accent, Content = "surface" },
                    new Tag { Variant = StatusVariant.Outline, Tone = StatusTone.Accent, Content = "outline" },
                    new Tag { Radius = Radius.Full, Tone = StatusTone.Neutral, Content = "胶囊形" },
                    new Tag { Size = TextSize.Sm, Tone = StatusTone.Neutral, Variant = StatusVariant.Surface, Content = "size=sm" }),
            },
        };
    }

    static readonly (string Label, StatusTone Tone)[] Filters =
    [
        ("价格：100–500 元", StatusTone.Accent),
        ("品牌：小米", StatusTone.Neutral),
        ("品牌：华为", StatusTone.Neutral),
        ("有货", StatusTone.Success),
        ("包邮", StatusTone.Info),
        ("评分 4.5 以上", StatusTone.Warning),
    ];

    static Control TagRemovable()
    {
        var title = new Strong();
        var restore = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Icon = Lucide.RotateCcw, Content = "恢复" };
        var tags = new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap };
        var empty = new Text { Size = TextSize.Sm, Tone = TextTone.Muted, Text = "没有筛选条件，显示全部商品" };
        void Sync()
        {
            var count = tags.Children.OfType<Tag>().Count();
            title.Text = $"已选筛选条件（{count}）";
            restore.IsEnabled = count != Filters.Length;
            empty.IsVisible = count == 0;
        }
        void Fill()
        {
            tags.Children.Clear();
            foreach (var (label, tone) in Filters)
            {
                var tag = new Tag { Tone = tone, Radius = Radius.Full, Content = label };
                tag.RemoveRequested += (_, _) =>
                {
                    tags.Children.Remove(tag);
                    Sync();
                };
                tags.Children.Add(tag);
            }
            tags.Children.Add(empty);
            Sync();
        }
        restore.Click += (_, _) => Fill();
        Fill();
        return new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children =
                {
                    new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Children = { title, restore } },
                    tags,
                },
            },
        };
    }
}

/// <summary>Badge/matrix：样式与色调。</summary>
public partial class BadgeMatrix : UserControl
{
    /// <summary>加载示例。</summary>
    public BadgeMatrix() => InitializeComponent();
}

/// <summary>Badge/shapes：尺寸、形状与图标。</summary>
public partial class BadgeShapes : UserControl
{
    /// <summary>加载示例。</summary>
    public BadgeShapes() => InitializeComponent();
}
