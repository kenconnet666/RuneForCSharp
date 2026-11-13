namespace Rune.WinForms.Examples;

/// <summary>Select 的示例（vue-tsx-pro 文档站 input/Select）。WinForms 的选项没有自定义内容（选项是画出来的）：成员的小组、供应商的编码放在选项的说明里。</summary>
static class SelectExamples
{
    public static Example[] All =>
    [
        new("Select", "basic", "单选与多选", "ItemsSource 是 ListboxOption、字符串或任意数据加 ToOption（WinForms 的选项是画出来的，没有自定义内容）。多选时已选的在触发框里显示成可移除的标签，Backspace 移除最后一项。", Assign),
        new("Select", "tags", "多选的标签：收起、移除与新建", "MaxTagCount = TagLimit.Responsive 时标签排成一行、放不下的收进“+N”；写数字时只显示前几个。Creatable 时输入没有的文字，选“新建”加进选项（OptionCreated 报告）。", Tags),
        new("Select", "custom", "异步加载与自定义显示", "Load(搜索词, 第几页, 取消) 在打开时请求（Searchable 时搜索词停下 300 ms 再请求），只采用最后一次，请求期间列表里是骨架行，失败时提示重试；ItemsSource 是第一次结果之前的选项（编辑页放上已选的那几项）。分组来自选项的 Group。", Custom),
        new("Select", "searchable", "可搜索", "选项多（供应商、人员、件号）时写 Searchable：弹出层顶部是搜索框，打开后焦点就在里面，输入即按显示文字过滤（忽略大小写与重音，Filter 可以换规则），↑↓ 移动、Enter 选中，Esc 关闭并把焦点还给触发框。已选的项被过滤掉、或换成了另一页的搜索结果时，触发框里照样显示它的文字。远程搜索给 Load（见下一个示例）。", Searchable),
        new("Select", "paged", "远程搜索与分页", "选项太多、要分页取时，Load 的第二个参数是第几页（从 1 开始），返回带 Next 的 OptionsPage：滚到列表末尾时带着 Next 取下一页，已有的选项留着、骨架接在后面；没有更多时 Next 为 null。换了搜索词从第一页重来，翻页时重复的项不会出现两次。", Paged),
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

    static readonly Member[] Directory = [.. Members, new("wq", "王晴", "测试")];

    /// <summary>假的成员接口：900 毫秒后返回全部成员；关掉弹出层（请求被取消）时中止。</summary>
    static async Task<OptionsPage> FetchMembers(string query, int page, CancellationToken cancellation)
    {
        await Task.Delay(900, cancellation);
        return OptionsPage.All(Directory);
    }

    static Control Custom() => new FieldControl
    {
        Label = "项目成员", Description = "打开时才加载成员列表",
        Content = new SelectControl
        {
            Load = FetchMembers,
            ToOption = item => item is Member member ? new ListboxOption(member.Id, member.Name) { Group = member.Team } : throw new ArgumentException(null, nameof(item)),
            SelectionMode = SelectionMode.Multiple, Clearable = true, PlaceholderText = "添加成员",
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
        var supplier = new SelectControl { ItemsSource = SupplierOptions, Value = "S00", Searchable = true };
        var value = new SmallControl { Tone = TextTone.Muted };
        void Update() => value.Text = $"值：{(string.IsNullOrEmpty(supplier.Value) ? "—" : supplier.Value)}";
        OnChanged(supplier, Update);
        Update();
        var partners = new SelectControl { ItemsSource = SupplierOptions, SelectionMode = SelectionMode.Multiple, Values = ["S13", "S51"], Searchable = true, Clearable = true };
        return Build.Grid(new GridControl { MinItemWidth = "16em", Gap = Space.Md },
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
                new FieldControl { Label = "涉及供应商", Description = "六十个选项：打开后直接输入“密封”试试", Content = supplier }, value),
            new FieldControl { Label = "第二供应商（多选）", Content = partners });
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

    static Control Paged() => new FieldControl
    {
        Label = "供应商", Description = $"共 {Suppliers.Length} 家，每次取 {PageSize} 家",
        Content = new SelectControl
        {
            Load = FetchSuppliers, Searchable = true, Clearable = true, PlaceholderText = "搜索名称或编码",
            ToOption = item => item is Supplier supplier ? new ListboxOption(supplier.Code, supplier.Name) { Description = supplier.Code } : throw new ArgumentException(null, nameof(item)),
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
