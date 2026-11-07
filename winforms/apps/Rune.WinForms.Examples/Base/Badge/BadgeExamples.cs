namespace Rune.WinForms.Examples;

/// <summary>Badge 的示例（vue-tsx-pro 文档站 base/Badge）。in-context 要等 Table，statuses 要等 Field、Select。</summary>
static class BadgeExamples
{
    static readonly (StatusTone Tone, string Label)[] Tones =
    [
        (StatusTone.Neutral, "草稿"), (StatusTone.Accent, "新功能"), (StatusTone.Info, "处理中"),
        (StatusTone.Success, "已完成"), (StatusTone.Warning, "即将到期"), (StatusTone.Danger, "已失败"),
    ];

    public static Example[] All =>
    [
        new("Badge", "matrix", "样式与色调", "Variant 是 solid、soft（默认）、surface、outline，Tone 是六种状态色调；颜色都取主题 token，每套主题都过对比度检查。", Matrix),
        new("Badge", "shapes", "尺寸、形状与图标", "TextSize 默认 xs；Radius = full 是胶囊，计数常用实底；Icon 放在文字前，Dot 是同色的小圆点，表格里比整块底色更轻。", Shapes),
        new("Badge", "tag", "Tag 标签", "TagControl 的外观选项与 BadgeControl 相同，作用域里 Badge 的默认值一起改两者；技能、分类、关键词这类成组出现的短文字用它。", TagBasic),
        new("Badge", "tag-removable", "可移除的标签", "订阅了 RemoveRequested 就有移除按钮（WinForms 没有离场的淡出，点了就请求移除）；按钮的读屏名称是“移除 + 标签文字”，RemoveLabel 可以改。", TagRemovable),
        new("Badge", "tag-selectable", "可选中的标签", "写了 IsSelected 时整个标签是切换按钮，点一下切换：没选中时有描边，选中时实底色、前面一个对勾（不只靠颜色），不写色调时用 accent。筛选条件这类一排可选的短文字用它；要“只能选一个”的分段控件用 ToggleGroup。", TagSelectable),
    ];

    static readonly string[] Conditions = ["加急", "待我审批", "已逾期", "本周到期", "有附件"];

    static Control TagSelectable()
    {
        var picked = new HashSet<string> { "加急", "已逾期" };
        var summary = new SmallControl { Tone = TextTone.Muted };
        void Sync() => summary.Text = $"已选：{(picked.Count == 0 ? "（无）" : string.Join("、", Conditions.Where(picked.Contains)))}";
        var row = new FlexControl { Gap = Space.Xs, Wrap = true, AccessibleName = "筛选" };
        foreach (var option in Conditions)
        {
            var tag = new TagControl { Text = option, IsSelected = picked.Contains(option) };
            tag.IsSelectedChanged += (_, _) =>
            {
                if (tag.IsSelected == true) picked.Add(option);
                else picked.Remove(option);
                Sync();
            };
            row.Controls.Add(tag);
        }
        Sync();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm }, row, summary);
    }

    /// <summary>一行一种样式、一列一种色调：第一列是样式名。</summary>
    static Control Matrix() => Build.Grid(new GridControl { Columns = 7, Align = GridAlign.Center, Justify = GridAlign.Start, RowGap = Space.Sm, ColumnGap = Space.Md },
    [
        .. StatusVariant.Known.SelectMany(variant => (Control[])
        [
            Label(variant.Value),
            .. Tones.Select(item => new BadgeControl { Text = item.Label, Variant = variant, Tone = item.Tone }),
        ]),
    ]);

    static Control Shapes() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
        Row("字号", [.. new[] { TextSize.Xs, TextSize.Sm, TextSize.Md, TextSize.Lg }.Select(size => new BadgeControl { Text = $"size={size.Value}", TextSize = size, Tone = StatusTone.Accent })]),
        Row("胶囊与计数",
            new BadgeControl { Text = "12", Radius = Radius.Full, Variant = StatusVariant.Solid, Tone = StatusTone.Danger },
            new BadgeControl { Text = "99+", Radius = Radius.Full, Variant = StatusVariant.Solid },
            new BadgeControl { Text = "在线", Radius = Radius.Full, Tone = StatusTone.Success },
            new BadgeControl { Text = "v2.4.0", Radius = Radius.Full, Variant = StatusVariant.Outline }),
        Row("图标",
            new BadgeControl { Text = "已通过", Icon = Lucide.CircleCheck, Tone = StatusTone.Success },
            new BadgeControl { Text = "待审核", Icon = Lucide.Clock, Tone = StatusTone.Warning },
            new BadgeControl { Text = "3 个错误", Icon = Lucide.TriangleExclamationPoint, Tone = StatusTone.Danger, Variant = StatusVariant.Surface },
            new BadgeControl { Text = "Beta", Icon = Lucide.Sparkles, Tone = StatusTone.Accent, Variant = StatusVariant.Solid },
            new BadgeControl { Text = "热门", Icon = Lucide.Flame, Tone = StatusTone.Warning, Variant = StatusVariant.Outline, Radius = Radius.Full }),
        Row("圆点",
            new BadgeControl { Text = "运行中", Dot = true, Tone = StatusTone.Success, Radius = Radius.None },
            new BadgeControl { Text = "待审核", Dot = true, Tone = StatusTone.Warning, Radius = Radius.None },
            new BadgeControl { Text = "已停止", Dot = true, Tone = StatusTone.Danger, Variant = StatusVariant.Outline }));

    static Control TagBasic() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
        Row("技能",
            new TagControl { Text = "TypeScript", Icon = Lucide.CodeXml },
            new TagControl { Text = "设计系统", Icon = Lucide.Palette, Tone = StatusTone.Accent },
            new TagControl { Text = "PostgreSQL", Icon = Lucide.Database, Tone = StatusTone.Info },
            new TagControl { Text = "移动端", Icon = Lucide.Smartphone, Tone = StatusTone.Success },
            new TagControl { Text = "国际化", Icon = Lucide.Globe, Tone = StatusTone.Warning }),
        Row("样式",
            new TagControl { Text = "solid", Variant = StatusVariant.Solid, Tone = StatusTone.Accent },
            new TagControl { Text = "soft", Variant = StatusVariant.Soft, Tone = StatusTone.Accent },
            new TagControl { Text = "surface", Variant = StatusVariant.Surface, Tone = StatusTone.Accent },
            new TagControl { Text = "outline", Variant = StatusVariant.Outline, Tone = StatusTone.Accent },
            new TagControl { Text = "胶囊形", Radius = Radius.Full, Tone = StatusTone.Neutral },
            new TagControl { Text = "size=sm", TextSize = TextSize.Sm, Tone = StatusTone.Neutral, Variant = StatusVariant.Surface }));

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
        var title = new StrongControl();
        var restore = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Icon = Lucide.RotateCcw, Text = "恢复" };
        var tags = new FlexControl { Gap = Space.Sm, Wrap = true };
        var empty = new TextControl { TextSize = TextSize.Sm, Tone = TextTone.Muted, Text = "没有筛选条件，显示全部商品" };
        void Sync()
        {
            var count = tags.Controls.OfType<TagControl>().Count();
            title.Text = $"已选筛选条件（{count}）";
            restore.Enabled = count != Filters.Length;
            empty.Visible = count == 0;
        }
        void Fill()
        {
            foreach (var old in tags.Controls.OfType<TagControl>().ToList()) old.Dispose();
            tags.Controls.Clear();
            foreach (var (label, tone) in Filters)
            {
                var tag = new TagControl { Text = label, Tone = tone, Radius = Radius.Full };
                tag.RemoveRequested += (_, _) =>
                {
                    tags.Controls.Remove(tag);
                    tag.Dispose();
                    Sync();
                };
                tags.Controls.Add(tag);
            }
            tags.Controls.Add(empty);
            Sync();
        }
        restore.Click += (_, _) => Fill();
        Fill();
        var header = Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm }, title, restore);
        return Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md }, header, tags));
    }

    /// <summary>一行：左边是 xs、muted 的名字。</summary>
    static FlexControl Row(string label, params Control[] items) =>
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true }, [Label(label), .. items]);

    static TextControl Label(string text) => new() { Text = text, TextSize = TextSize.Xs, Tone = TextTone.Muted };
}
