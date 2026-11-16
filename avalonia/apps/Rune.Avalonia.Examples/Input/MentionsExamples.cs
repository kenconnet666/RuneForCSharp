using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;

namespace Rune.Avalonia.Examples;

/// <summary>Mentions 的示例（vue-tsx-pro 文档站 input/Mentions）。</summary>
static class MentionsExamples
{
    public static Example[] All =>
    [
        new("Mentions", "approval", "审批意见", "输入 @ 弹出人员（按部门分组、带岗位说明），接着输入按名字过滤，Enter 或 Tab 选中；Mentioned 是文字里提到的人的工号——删掉“@名字”就不再通知他。下面的预览用 MentionText 高亮提及。", Approval),
        new("Mentions", "remote", "远程搜索", "全公司的人太多，按 @ 后面的文字远程搜索（Load，停下 300 ms 后请求，只采用最后一次）；ItemTemplate 自己画选项：头像、姓名与部门。数据是任意结构，用 ToOption 映射成选项。", Remote),
        new("Mentions", "prefix", "换触发字符、字数与显示", "换一个触发字符：# 引用单据。Prefix 两边要一致（输入框与显示）；没有匹配时列表里给一句提示；字数、清空、外观同 Textarea。显示时用 RenderMention 把引用做成链接。", Topics),
    ];

    static Control Approval()
    {
        ListboxOption[] people =
        [
            new("E1024", "周宁") { Description = "采购专员", Group = "采购部" },
            new("E1031", "陆川") { Description = "采购经理", Group = "采购部" },
            new("E2007", "方圆") { Description = "财务分析师", Group = "财务部" },
            new("E2015", "韩笑") { Description = "财务部负责人", Group = "财务部" },
            new("E3002", "林小满") { Description = "质量工程师", Group = "质量部" },
            new("E3009", "吴悠") { Description = "供应商质量", Group = "质量部" },
        ];
        var mentions = new Mentions { ItemsSource = people, Text = "同意按 V3 报价执行，@方圆 请把测算表更新到最新版本，", Rows = 3, MaxRows = 8 };
        var preview = new MentionText { Value = mentions.Text, Options = people };
        var notified = new Flex { Gap = Space.Xs, Wrap = FlexWrap.Wrap, Align = FlexAlign.Center };
        void Update()
        {
            preview.Value = mentions.Text;
            notified.Children.Clear();
            notified.Children.Add(new Small { Tone = TextTone.Muted, Text = "将通知：" });
            if (mentions.Mentioned.Count == 0) notified.Children.Add(new Small { Tone = TextTone.Muted, Text = "还没有提及任何人" });
            foreach (var value in mentions.Mentioned)
                notified.Children.Add(new Tag { Size = TextSize.Sm, Content = $"{people.First(person => person.Value == value).Label}（{value}）" });
        }
        mentions.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty || e.Property == Mentions.MentionedProperty) Update();
        };
        Update();
        return new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
                Children =
                {
                    new Field { Label = "审批意见", Description = "输入 @ 提及需要知会的人，提交后会通知他们", Content = mentions },
                    notified,
                    new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch, Children = { new Small { Tone = TextTone.Muted, Text = "预览" }, preview } },
                },
            },
        };
    }

    sealed record Employee(string Id, string Name, string Department);

    static readonly Employee[] Employees =
    [
        .. "王 李 张 刘 陈 杨 黄 赵 周 吴 徐 孙 马 朱 胡 郭 何 林 罗 高".Split(' ').SelectMany((surname, row) =>
            new[] { "小满", "一航", "思远", "可欣", "子墨", "雨桐", "浩然", "静怡", "宇轩", "梦洁" }.Select((given, column) =>
                new Employee($"E{row * 10 + column + 1:0000}", surname + given, new[] { "采购部", "财务部", "质量部", "工程部", "财务部" }[(row + column) % 5]))),
    ];

    /// <summary>模拟接口：按姓名或工号搜索，最多 20 条，延迟 400 毫秒。</summary>
    static async Task<OptionsPage> Search(string query, int page, CancellationToken cancellation)
    {
        await Task.Delay(400, cancellation);
        return OptionsPage.All(Employees.Where(employee => employee.Name.Contains(query) || employee.Id.Contains(query)).Take(20));
    }

    static Control Remote()
    {
        var mentions = new Mentions
        {
            Load = Search,
            ToOption = item => item is Employee employee ? new ListboxOption(employee.Id, employee.Name) { Description = employee.Department } : new ListboxOption(item?.ToString() ?? "", item?.ToString() ?? ""),
            Rows = 2,
            ItemTemplate = new FuncDataTemplate<Employee>((employee, _) => new Flex
            {
                Gap = Space.Sm, Align = FlexAlign.Center,
                Children =
                {
                    new Avatar { DisplayName = employee.Name, Size = TextSize.Xs },
                    new Text { Text = employee.Name },
                    new Small { Tone = TextTone.Muted, Text = $"{employee.Department} · {employee.Id}" },
                },
            }),
        };
        var note = new Small { Tone = TextTone.Muted, Text = "提及的工号：无" };
        mentions.PropertyChanged += (_, e) =>
        {
            if (e.Property == Mentions.MentionedProperty) note.Text = "提及的工号：" + (mentions.Mentioned.Count > 0 ? string.Join("、", mentions.Mentioned) : "无");
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { new Field { Label = "留言", Description = $"共 {Employees.Length} 人；试试输入 @林 或 @E001", Content = mentions }, note },
        };
    }

    static Control Topics()
    {
        ListboxOption[] orders =
        [
            new("PO-2024-0318", "PO-2024-0318") { Description = "液压阀组 · 华辰精密" },
            new("PO-2024-0325", "PO-2024-0325") { Description = "密封件 · 恒通密封" },
            new("QC-2024-0102", "QC-2024-0102") { Description = "来料检验 · 不合格 3 件" },
            new("RT-2024-0044", "RT-2024-0044") { Description = "退货申请 · 待审批" },
        ];
        var mentions = new Mentions
        {
            ItemsSource = orders, Prefix = "#", Empty = "没有这个单号；只能引用近 30 天的单据", Text = "#QC-2024-0102 的不合格品走退货，关联 ",
            Rows = 3, MaxLength = 200, ShowCount = true, Clearable = true, Variant = ControlVariant.Soft,
        };
        var shown = new MentionText
        {
            Value = mentions.Text, Options = orders, Prefix = "#",
            RenderMention = (order, text) => new InlineUIContainer(new Link { Content = text }),
        };
        var cited = new Small { Tone = TextTone.Muted };
        void Update()
        {
            shown.Value = mentions.Text;
            cited.Text = $"引用了 {mentions.Mentioned.Count} 张单据";
        }
        mentions.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty || e.Property == Mentions.MentionedProperty) Update();
        };
        Update();
        return new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
                Children =
                {
                    new Field { Label = "处理说明", Description = "输入 # 引用单据：按单号过滤，引用过的单据会挂在这条说明下面", Content = mentions },
                    new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch, Children = { new Small { Tone = TextTone.Muted, Text = "显示出来（引用是链接）" }, shown } },
                    cited,
                    new Field
                    {
                        Label = "只读（已提交的说明）",
                        Content = new Mentions { ItemsSource = orders, Prefix = "#", Text = "#PO-2024-0318 已到货，按 #QC-2024-0102 复检", IsReadOnly = true },
                    },
                },
            },
        };
    }
}
