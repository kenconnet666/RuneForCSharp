namespace Rune.WinForms.Examples;

/// <summary>Select 的示例（vue-tsx-pro 文档站 input/Select）。searchable、custom 要等远程选项（load）；WinForms 的选项没有自定义内容。</summary>
static class SelectExamples
{
    public static Example[] All =>
    [
        new("Select", "basic", "单选与多选", "ItemsSource 是 ListboxOption、字符串或任意数据加 ToOption（WinForms 的选项是画出来的，没有自定义内容）。多选时已选的在触发框里显示成可移除的标签，Backspace 移除最后一项。", Assign),
        new("Select", "tags", "多选的标签：收起、移除与新建", "MaxTagCount = TagLimit.Responsive 时标签排成一行、放不下的收进“+N”；写数字时只显示前几个。Creatable 时输入没有的文字，选“新建”加进选项（OptionCreated 报告）。", Tags),
        new("Select", "appearance", "图标、外观与状态", "前置图标、Variant、Radius、TextSize 与输入框同一套；在 FieldControl 里沿用出错、必填、只读、不可用。只读时能聚焦、能看，不能打开。", Appearance),
    ];

    sealed record Member(string Id, string Name, string Team);

    static readonly Member[] Members =
    [
        new("zyh", "周一航", "设计"), new("xy", "许悠", "设计"), new("lxm", "林小满", "前端"), new("zhr", "赵浩然", "前端"), new("cm", "陈默", "后端"),
    ];

    static void OnChanged(SelectControl select, Action changed)
    {
        select.ValueChanged += (_, _) => changed();
        select.ValuesChanged += (_, _) => changed();
    }

    static Control Assign()
    {
        ListboxOption[] types =
        [
            new("bug", "缺陷") { Icon = Lucide.Bug, Description = "功能不符合预期" },
            new("feature", "需求") { Icon = Lucide.Lightbulb, Description = "新功能或改进" },
            new("chore", "杂项") { Icon = Lucide.Wrench, Description = "依赖升级、重构" },
            new("question", "咨询") { Icon = Lucide.CircleDot, IsDisabled = true },
        ];
        var type = new SelectControl { ItemsSource = types, Value = "bug" };
        var assignee = new SelectControl
        {
            ItemsSource = Members,
            ToOption = item => item is Member member ? new ListboxOption(member.Id, member.Name) { Group = member.Team } : throw new ArgumentException(null, nameof(item)),
            Value = "lxm",
            PlaceholderText = "未分配",
            Clearable = true,
        };
        var labels = new SelectControl { ItemsSource = new[] { "紧急", "前端", "后端", "设计", "文档", "性能", "无障碍" }, SelectionMode = SelectionMode.Multiple, Values = ["前端", "无障碍"], Clearable = true };
        var summary = new SmallControl { Tone = TextTone.Muted };
        void Update() => summary.Text = $"{type.Value} · {assignee.Value ?? "未分配"} · {(labels.Values.Count > 0 ? string.Join("、", labels.Values) : "无标签")}";
        foreach (var select in new[] { type, assignee, labels }) OnChanged(select, Update);
        Update();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
            Build.Grid(new GridControl { MinItemWidth = "13em", Gap = Space.Md },
                new FieldControl { Label = "类型", Content = type },
                new FieldControl { Label = "负责人", Content = assignee }),
            new FieldControl { Label = "标签", Description = "多选：已选的在触发框里显示成标签", Content = labels },
            summary);
    }

    static Control Tags()
    {
        var cc = new SelectControl
        {
            ItemsSource = new[] { "周一航", "许悠", "林小满", "赵浩然", "陈默", "韩笑", "宋知遥", "顾北辰", "沈清禾" },
            SelectionMode = SelectionMode.Multiple, Values = ["许悠", "林小满", "赵浩然", "陈默", "韩笑", "宋知遥", "顾北辰", "沈清禾"],
            MaxTagCount = TagLimit.Responsive, Searchable = true, Clearable = true,
        };
        var fits = new SelectControl
        {
            ItemsSource = new[] { "X3 标准版", "X3 长续航", "X5 四驱", "X5 越野", "M7 商务", "M7 豪华" },
            SelectionMode = SelectionMode.Multiple, Values = ["X3 标准版", "X3 长续航", "X5 四驱"], MaxTagCount = 2,
        };
        var labels = new List<string> { "紧急", "前端", "后端", "设计", "性能" };
        var picked = new SelectControl { ItemsSource = labels.ToArray(), SelectionMode = SelectionMode.Multiple, Values = ["紧急"], Creatable = true, PlaceholderText = "选择或新建标签" };
        picked.OptionCreated += (_, e) =>
        {
            labels.Add(e.Text);
            picked.ItemsSource = labels.ToArray();
        };
        var summary = new SmallControl { Tone = TextTone.Muted };
        void Update() => summary.Text = $"抄送 {cc.Values.Count} 人 · 车型 {fits.Values.Count} 个 · 标签：{(picked.Values.Count > 0 ? string.Join("、", picked.Values) : "无")}";
        foreach (var select in new[] { cc, fits, picked }) OnChanged(select, Update);
        Update();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
            new FieldControl { Label = "抄送", Description = "一行放不下的收进“+N”", Content = cc },
            new FieldControl { Label = "适用车型", Description = "只显示前两个", Content = fits },
            new FieldControl { Label = "工单标签", Description = "输入没有的标签，选“新建”加进选项", Content = picked },
            summary);
    }

    static Control Appearance()
    {
        ListboxOption[] languages = [new("zh-CN", "简体中文"), new("en-US", "English"), new("ja-JP", "日本語")];
        string[] cities = ["北京", "上海", "广州", "深圳", "杭州"];
        var city = new SelectControl { ItemsSource = cities, PlaceholderText = "选择城市" };
        var required = new FieldControl { Label = "必填", IsRequired = true, Error = "请选择城市", Content = city };
        var soft = new SelectControl { ItemsSource = cities, Icon = Lucide.MapPin, Variant = ControlVariant.Soft, Radius = Radius.Full, Clearable = true, PlaceholderText = "选择城市" };
        void Sync(SelectControl source)
        {
            var other = source == soft ? city : soft;
            if (other.Value != source.Value) other.Value = source.Value;
            required.Error = string.IsNullOrEmpty(source.Value) ? "请选择城市" : null;
        }
        soft.ValueChanged += (_, _) => Sync(soft);
        city.ValueChanged += (_, _) => Sync(city);
        return Build.Grid(new GridControl { MinItemWidth = "14em", Gap = Space.Lg },
            new FieldControl { Label = "界面语言", Content = new SelectControl { ItemsSource = languages, Value = "zh-CN", Icon = Lucide.Globe } },
            new FieldControl { Label = "variant=\"soft\" radius=\"full\"", Content = soft },
            required,
            new FieldControl { Label = "size=\"sm\"", Content = new SelectControl { ItemsSource = languages, Value = "zh-CN", TextSize = TextSize.Sm } },
            new FieldControl { Label = "只读", IsReadOnly = true, Description = "能聚焦、能看，不能打开", Content = new SelectControl { ItemsSource = languages, Value = "en-US", Icon = Lucide.Globe } },
            new FieldControl { Label = "不可用", Enabled = false, Content = new SelectControl { ItemsSource = languages, Value = "ja-JP" } });
    }
}
