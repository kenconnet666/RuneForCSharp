using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Rune.Avalonia.Examples;

/// <summary>Select 的示例（vue-tsx-pro 文档站 input/Select）。custom 的 renderValue（触发框里的头像叠放）不移植：多选的触发框是标签。</summary>
static class SelectExamples
{
    public static Example[] All =>
    [
        new("Select", "basic", "单选与多选", "ItemsSource 是 ListboxOption、字符串或任意数据加 ToOption；ItemTemplate 自定义选项内容（触发框里仍是文字）。多选时已选的在触发框里显示成可移除的标签，Backspace 移除最后一项。", Assign),
        new("Select", "tags", "多选的标签：收起、移除与新建", "MaxTagCount=\"responsive\" 时标签排成一行、放不下的收进“+N”；写数字时只显示前几个。Creatable 时输入没有的文字，选“新建”加进选项（OptionCreated 报告）。", Tags),
        new("Select", "custom", "异步加载与自定义显示", "Load(搜索词, 第几页, 取消) 在打开时请求（Searchable 时搜索词停下 300 ms 再请求），只采用最后一次，请求期间列表里是骨架行，失败时提示重试；ItemsSource 是第一次结果之前的选项（编辑页放上已选的那几项）。分组来自选项的 Group，ItemTemplate 自定义每个选项。", Custom),
        new("Select", "searchable", "可搜索", "选项多（供应商、人员、件号）时写 Searchable：弹出层顶部是搜索框，打开后焦点就在里面，输入即按显示文字过滤（忽略大小写与重音，Filter 可以换规则），↑↓ 移动、Enter 选中，Esc 关闭并把焦点还给触发框。已选的项被过滤掉、或换成了另一页的搜索结果时，触发框里照样显示它的文字。远程搜索给 Load（见下一个示例）。", Searchable),
        new("Select", "paged", "远程搜索与分页", "选项太多、要分页取时，Load 的第二个参数是第几页（从 1 开始），返回带 Next 的 OptionsPage：滚到列表末尾时带着 Next 取下一页，已有的选项留着、骨架接在后面；没有更多时 Next 为 null。换了搜索词从第一页重来，翻页时重复的项不会出现两次。", Paged),
        new("Select", "appearance", "图标、外观与状态", "前置图标、Variant、Radius、Size 与输入框同一套；在 Field 里沿用出错、必填、只读、不可用。只读时能聚焦、能看，不能打开。", Appearance),
    ];

    sealed record Member(string Id, string Name, string Team);

    static readonly Member[] Members =
    [
        new("zyh", "周一航", "设计"), new("xy", "许悠", "设计"), new("lxm", "林小满", "前端"), new("zhr", "赵浩然", "前端"), new("cm", "陈默", "后端"),
    ];

    static readonly Member[] Directory = [.. Members, new("wq", "王晴", "测试")];

    /// <summary>假的成员接口：900 毫秒后返回全部成员；关掉弹出层（请求被取消）时中止。</summary>
    static async Task<OptionsPage> FetchMembers(string query, int page, CancellationToken cancellation)
    {
        await Task.Delay(900, cancellation);
        return OptionsPage.All(Directory);
    }

    static Control Custom() => new Field
    {
        Label = "项目成员", Description = "打开时才加载成员列表",
        Content = new Select
        {
            Load = FetchMembers,
            ToOption = item => item is Member member ? new ListboxOption(member.Id, member.Name) { Group = member.Team } : throw new ArgumentException(null, nameof(item)),
            SelectionMode = SelectionMode.Multiple, Clearable = true, Placeholder = "添加成员",
            ItemTemplate = new FuncDataTemplate<Member>((member, _) => new Flex
            {
                Align = FlexAlign.Center, Gap = Space.Sm,
                Children =
                {
                    new Avatar { Size = TextSize.Sm, DisplayName = member.Name },
                    new Flex { Direction = FlexDirection.Column, Children = { new Text { Text = member.Name, Size = TextSize.Sm }, new Small { Text = member.Team, Tone = TextTone.Muted } } },
                },
            }),
        },
    };

    static readonly string[] Regions = ["华东", "华北", "华南", "西南", "东北", "海外"];

    static readonly string[] Kinds = ["塑胶", "五金", "钣金", "标准件", "密封件", "线束", "液压", "涂装", "铸锻", "电子"];

    /// <summary>六十家虚构的供应商，按地区分组。</summary>
    static readonly ListboxOption[] SupplierOptions =
    [
        .. Regions.SelectMany((region, r) => Kinds.Select((kind, k) => new ListboxOption($"S{r}{k}", $"{region}{kind}供应商{"甲乙丙丁戊己庚辛壬癸"[k]}")
        {
            Description = $"供应商代码 S-{202600 + r * 10 + k}", Group = region,
        })),
    ];

    static Control Searchable()
    {
        var supplier = new Select { ItemsSource = SupplierOptions, Value = "S00", Searchable = true };
        var value = new Small { Tone = TextTone.Muted };
        void Update() => value.Text = $"值：{(string.IsNullOrEmpty(supplier.Value) ? "—" : supplier.Value)}";
        supplier.PropertyChanged += (_, e) =>
        {
            if (e.Property == Select.ValueProperty) Update();
        };
        Update();
        var partners = new Select { ItemsSource = SupplierOptions, SelectionMode = SelectionMode.Multiple, Values = ["S13", "S51"], Searchable = true, Clearable = true };
        return new Grid
        {
            MinItemWidth = "16em", Gap = Space.Md,
            Children =
            {
                new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new Field { Label = "涉及供应商", Description = "六十个选项：打开后直接输入“密封”试试", Content = supplier }, value } },
                new Field { Label = "第二供应商（多选）", Content = partners },
            },
        };
    }

    sealed record Supplier(string Code, string Name);

    static readonly Supplier[] Suppliers =
    [
        .. new[] { "苏州", "宁波", "东莞", "成都", "西安", "沈阳", "青岛", "无锡" }.SelectMany((city, c) =>
            new[] { "恒", "华", "瑞", "鑫", "永" }.SelectMany((mark, m) =>
                new[] { "精工", "液压", "紧固件", "电子", "包装", "模具", "轴承", "铸造" }.Select((trade, t) =>
                    new Supplier($"S{c * 40 + m * 8 + t + 1:0000}", $"{city}{mark}{trade}有限公司")))),
    ];

    const int PageSize = 20;

    /// <summary>假的供应商接口：按名称或编码搜索，每页 20 条，500 毫秒后返回；被顶掉、关掉弹出层时中止。</summary>
    static async Task<OptionsPage> FetchSuppliers(string query, int page, CancellationToken cancellation)
    {
        await Task.Delay(500, cancellation);
        var keyword = query.Trim().ToUpperInvariant();
        var matched = Suppliers.Where(supplier => supplier.Name.Contains(keyword) || supplier.Code.Contains(keyword)).ToList();
        return new OptionsPage([.. matched.Skip((page - 1) * PageSize).Take(PageSize)], page * PageSize < matched.Count ? page + 1 : null);
    }

    static Control Paged() => new Field
    {
        Label = "供应商", Description = $"共 {Suppliers.Length} 家，每次取 {PageSize} 家",
        Content = new Select
        {
            Load = FetchSuppliers, Searchable = true, Clearable = true, Placeholder = "搜索名称或编码",
            ToOption = item => item is Supplier supplier ? new ListboxOption(supplier.Code, supplier.Name) : throw new ArgumentException(null, nameof(item)),
            ItemTemplate = new FuncDataTemplate<Supplier>((supplier, _) => new Flex
            {
                Align = FlexAlign.Center, Gap = Space.Xs,
                Children = { new Text { Text = supplier.Name }, new Small { Text = supplier.Code, Tone = TextTone.Muted, Mono = true } },
            }),
        },
    };

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
