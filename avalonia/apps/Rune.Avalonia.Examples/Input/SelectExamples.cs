using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Rune.Avalonia.Examples;

/// <summary>Select 的示例（vue-tsx-pro 文档站 input/Select）。searchable、custom 要等远程选项（load）。</summary>
static class SelectExamples
{
    public static Example[] All =>
    [
        new("Select", "basic", "单选与多选", "ItemsSource 是 ListboxOption、字符串或任意数据加 ToOption；ItemTemplate 自定义选项内容（触发框里仍是文字）。多选时已选的在触发框里显示成可移除的标签，Backspace 移除最后一项。", Assign),
        new("Select", "tags", "多选的标签：收起、移除与新建", "MaxTagCount=\"responsive\" 时标签排成一行、放不下的收进“+N”；写数字时只显示前几个。Creatable 时输入没有的文字，选“新建”加进选项（OptionCreated 报告）。", Tags),
        new("Select", "appearance", "图标、外观与状态", "前置图标、Variant、Radius、Size 与输入框同一套；在 Field 里沿用出错、必填、只读、不可用。只读时能聚焦、能看，不能打开。", Appearance),
    ];

    sealed record Member(string Id, string Name, string Team);

    static readonly Member[] Members =
    [
        new("zyh", "周一航", "设计"), new("xy", "许悠", "设计"), new("lxm", "林小满", "前端"), new("zhr", "赵浩然", "前端"), new("cm", "陈默", "后端"),
    ];

    static Control Assign()
    {
        ListboxOption[] types =
        [
            new("bug", "缺陷") { Icon = Lucide.Bug, Description = "功能不符合预期" },
            new("feature", "需求") { Icon = Lucide.Lightbulb, Description = "新功能或改进" },
            new("chore", "杂项") { Icon = Lucide.Wrench, Description = "依赖升级、重构" },
            new("question", "咨询") { Icon = Lucide.CircleDot, IsDisabled = true },
        ];
        var type = new Select { ItemsSource = types, Value = "bug" };
        var assignee = new Select
        {
            ItemsSource = Members,
            ToOption = item => item is Member member ? new ListboxOption(member.Id, member.Name) { Group = member.Team } : throw new ArgumentException(null, nameof(item)),
            Value = "lxm",
            Placeholder = "未分配",
            Clearable = true,
            ItemTemplate = new FuncDataTemplate<Member>((member, _) => new Flex
            {
                Align = FlexAlign.Center, Gap = Space.Sm,
                Children = { new Avatar { Size = TextSize.Xs, DisplayName = member.Name }, new Text { Text = member.Name } },
            }),
        };
        var labels = new Select { ItemsSource = new[] { "紧急", "前端", "后端", "设计", "文档", "性能", "无障碍" }, SelectionMode = SelectionMode.Multiple, Values = ["前端", "无障碍"], Clearable = true };
        var summary = new Small { Tone = TextTone.Muted };
        void Update() => summary.Text = $"{type.Value} · {assignee.Value ?? "未分配"} · {(labels.Values is { Count: > 0 } picked ? string.Join("、", picked) : "无标签")}";
        foreach (var select in new[] { type, assignee, labels }) select.PropertyChanged += (_, e) =>
        {
            if (e.Property == Select.ValueProperty || e.Property == Select.ValuesProperty) Update();
        };
        Update();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children =
            {
                new Grid { MinItemWidth = "13em", Gap = Space.Md, Children = { new Field { Label = "类型", Content = type }, new Field { Label = "负责人", Content = assignee } } },
                new Field { Label = "标签", Description = "多选：已选的在触发框里显示成标签", Content = labels },
                summary,
            },
        };
    }

    static Control Tags()
    {
        var cc = new Select
        {
            ItemsSource = new[] { "周一航", "许悠", "林小满", "赵浩然", "陈默", "韩笑", "宋知遥", "顾北辰", "沈清禾" },
            SelectionMode = SelectionMode.Multiple, Values = ["许悠", "林小满", "赵浩然", "陈默", "韩笑", "宋知遥", "顾北辰", "沈清禾"],
            MaxTagCount = TagLimit.Responsive, Searchable = true, Clearable = true,
        };
        var fits = new Select
        {
            ItemsSource = new[] { "X3 标准版", "X3 长续航", "X5 四驱", "X5 越野", "M7 商务", "M7 豪华" },
            SelectionMode = SelectionMode.Multiple, Values = ["X3 标准版", "X3 长续航", "X5 四驱"], MaxTagCount = 2,
        };
        var labels = new List<string> { "紧急", "前端", "后端", "设计", "性能" };
        var picked = new Select { ItemsSource = labels.ToArray(), SelectionMode = SelectionMode.Multiple, Values = ["紧急"], Creatable = true, Placeholder = "选择或新建标签" };
        picked.OptionCreated += (_, e) =>
        {
            labels.Add(e.Text);
            picked.ItemsSource = labels.ToArray();
        };
        var summary = new Small { Tone = TextTone.Muted };
        void Update() => summary.Text = $"抄送 {cc.Values?.Count ?? 0} 人 · 车型 {fits.Values?.Count ?? 0} 个 · 标签：{(picked.Values is { Count: > 0 } values ? string.Join("、", values) : "无")}";
        foreach (var select in new[] { cc, fits, picked }) select.PropertyChanged += (_, e) =>
        {
            if (e.Property == Select.ValuesProperty) Update();
        };
        Update();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children =
            {
                new Field { Label = "抄送", Description = "一行放不下的收进“+N”", Content = cc },
                new Field { Label = "适用车型", Description = "只显示前两个", Content = fits },
                new Field { Label = "工单标签", Description = "输入没有的标签，选“新建”加进选项", Content = picked },
                summary,
            },
        };
    }

    static Control Appearance()
    {
        ListboxOption[] languages = [new("zh-CN", "简体中文"), new("en-US", "English"), new("ja-JP", "日本語")];
        string[] cities = ["北京", "上海", "广州", "深圳", "杭州"];
        var required = new Field { Label = "必填", IsRequired = true, Error = "请选择城市", Content = new Select { ItemsSource = cities, Placeholder = "选择城市" } };
        var soft = new Select { ItemsSource = cities, Icon = Lucide.MapPin, Variant = ControlVariant.Soft, Radius = Radius.Full, Clearable = true, Placeholder = "选择城市" };
        var city = (Select)required.Content!;
        void Sync(object? sender, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != Select.ValueProperty || sender is not Select source) return;
            var other = source == soft ? city : soft;
            if (other.Value != source.Value) other.Value = source.Value;
            required.Error = string.IsNullOrEmpty(source.Value) ? "请选择城市" : null;
        }
        soft.PropertyChanged += Sync;
        city.PropertyChanged += Sync;
        return new Grid
        {
            MinItemWidth = "14em", Gap = Space.Lg,
            Children =
            {
                new Field { Label = "界面语言", Content = new Select { ItemsSource = languages, Value = "zh-CN", Icon = Lucide.Globe } },
                new Field { Label = "variant=\"soft\" radius=\"full\"", Content = soft },
                required,
                new Field { Label = "size=\"sm\"", Content = new Select { ItemsSource = languages, Value = "zh-CN", Size = TextSize.Sm } },
                new Field { Label = "只读", IsReadOnly = true, Description = "能聚焦、能看，不能打开", Content = new Select { ItemsSource = languages, Value = "en-US", Icon = Lucide.Globe } },
                new Field { Label = "不可用", IsEnabled = false, Content = new Select { ItemsSource = languages, Value = "ja-JP" } },
            },
        };
    }
}
