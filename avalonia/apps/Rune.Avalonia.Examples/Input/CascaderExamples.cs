using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Rune.Avalonia.Examples;

/// <summary>Cascader 的示例（vue-tsx-pro 文档站 input/Cascader）。region 的拼音匹配不移植（我们没有拼音表）。</summary>
static class CascaderExamples
{
    public static Example[] All =>
    [
        new("Cascader", "region", "省市区", "收货地址：全国省 / 市 / 区三级（Area.Tree() 的数据），只能选到区；Searchable 按整条路径搜索，Clearable 可清空。开票地区只到市（Area.Tree(depth: 2)）。ShowAllLevels=false 时触发框里只显示最后一级。", Region),
        new("Cascader", "change-on-select", "选到任意一级", "ChangeOnSelect 时大类、小类都能选（报表按哪一级汇总就选到哪一级）；IsItemDisabled 让停用的分类不能选；Separator 换路径的分隔。", Categories),
        new("Cascader", "display", "显示、搜索的匹配与状态", "RenderValue 自己画触发框里的文字（前面几级淡一些，一眼先看到选的是什么）；Filter 换搜索的匹配方式（路径上任何一级的名称或编号对得上都算）。外观与状态同其他输入框。", Display),
        new("Cascader", "lazy", "按级取回", "HasChildren 声明有下级，进入一个部门时 LoadChildren 才请求它的下级（模拟 600 ms 的接口），期间那一项转圈。编辑页把已选的那一枝放进初始数据，触发框一开始就显示得出文字。", Lazy),
    ];

    static Cascader Areas(int depth)
    {
        return new Cascader
        {
            ItemsSource = Area.Tree(depth),
            GetKey = (node, _) => Area.KeyOf(node),
            GetLabel = Area.LabelOf,
            GetChildren = Area.ChildrenOf,
        };
    }

    static Control Region()
    {
        var address = Areas(3);
        address.Values = ["330000", "330100", "330106"];
        address.Icon = Lucide.MapPin;
        address.Searchable = address.Clearable = true;
        var invoice = Areas(2);
        invoice.Placeholder = "省 / 市";
        invoice.Searchable = true;
        var allLevels = new Switch { IsChecked = true, Content = "显示整条路径" };
        allLevels.PropertyChanged += (_, e) =>
        {
            if (e.Property != Switch.IsCheckedProperty) return;
            address.ShowAllLevels = invoice.ShowAllLevels = allLevels.IsChecked;
        };
        var note = new Small { Tone = TextTone.Muted };
        void Update() => note.Text = address.Values is { Count: > 0 } path
            ? $"value：[{string.Join(",", path.Select(code => $"\"{code}\""))}]（{string.Join(" ", Area.Names(path))}）"
            : "value：null";
        address.PropertyChanged += (_, e) =>
        {
            if (e.Property == SelectBase.ValuesProperty) Update();
        };
        Update();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
            Children =
            {
                allLevels,
                new Grid { MinItemWidth = "17rem", Gap = Space.Md, Children = { new Field { Label = "收货地址", Content = address }, new Field { Label = "开票地区", Content = invoice } } },
                note,
            },
        };
    }

    sealed record Category(string Id, string Name, Category[]? Children = null, bool Retired = false);

    static Control Categories()
    {
        Category[] categories =
        [
            new("mech", "机械件",
            [
                new("bearing", "轴承", [new("deep-groove", "深沟球轴承"), new("tapered", "圆锥滚子轴承")]),
                new("fastener", "紧固件", [new("bolt", "螺栓")]),
                new("casting", "铸件（停用）", Retired: true),
            ]),
            new("elec", "电子件", [new("connector", "连接器"), new("sensor", "传感器", [new("temp", "温度传感器")])]),
        ];
        var cascader = new Cascader
        {
            ItemsSource = categories,
            GetKey = (item, _) => ((Category)item).Id,
            GetLabel = item => ((Category)item).Name,
            GetChildren = item => ((Category)item).Children,
            IsItemDisabled = item => ((Category)item).Retired,
            Values = ["mech", "bearing"],
            ChangeOnSelect = true,
            Separator = " › ",
            Clearable = true,
        };
        var level = new Badge { Size = TextSize.Xs, Variant = StatusVariant.Soft };
        void Update() => level.Content = (cascader.Values?.Count ?? 0).ToString();
        cascader.PropertyChanged += (_, e) =>
        {
            if (e.Property == SelectBase.ValuesProperty) Update();
        };
        Update();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children =
            {
                new Field { Label = "物料分类", Description = "大类、小类都能选：报表按哪一级汇总就选到哪一级", Content = cascader },
                new Flex
                {
                    Align = FlexAlign.Center, Gap = Space.Xs,
                    Children = { new Small { Tone = TextTone.Muted, Text = "汇总到第" }, level, new Small { Tone = TextTone.Muted, Text = "级" } },
                },
            },
        };
    }

    sealed record Org(string Code, string Name, Org[]? Children = null);

    static readonly Org[] Orgs =
    [
        new("MFG", "制造中心",
        [
            new("MFG-1", "一工厂", [new("MFG-1-ST", "冲压车间"), new("MFG-1-WD", "焊装车间")]),
            new("MFG-2", "二工厂", [new("MFG-2-AS", "总装车间")]),
        ]),
        new("RD", "研发中心", [new("RD-HW", "硬件部"), new("RD-SW", "软件部")]),
    ];

    static Cascader Department(params string[] path) => new()
    {
        ItemsSource = Orgs,
        GetKey = (org, _) => ((Org)org).Code,
        GetLabel = org => ((Org)org).Name,
        GetChildren = org => ((Org)org).Children,
        Values = path.Length > 0 ? path : null,
    };

    static Control Display()
    {
        var department = Department("MFG", "MFG-1", "MFG-1-WD");
        department.Searchable = department.Clearable = true;
        department.Filter = (nodes, query) => nodes.Cast<Org>().Any(org => org.Name.Contains(query) || org.Code.Contains(query.ToUpperInvariant()));
        department.RenderValue = (labels, _) =>
        {
            if (labels.Count == 1) return [new Run(labels[0])];
            var parents = new Run(string.Join(" / ", labels.Take(labels.Count - 1)) + " / ");
            parents.Bind(TextElement.ForegroundProperty, department.GetResourceObservable(ThemeKeys.Brush.TextMuted));
            return [parents, new Run(labels[^1])];
        };
        var cost = Department();
        cost.Variant = ControlVariant.Soft;
        cost.Radius = Radius.Full;
        var required = new Field { Label = "成本中心", IsRequired = true, Error = "请选择成本中心", Content = cost };
        cost.PropertyChanged += (_, e) =>
        {
            if (e.Property == SelectBase.ValuesProperty) required.Error = cost.Values is { Count: > 0 } ? null : "请选择成本中心";
        };
        return new Grid
        {
            MinItemWidth = "16rem", Gap = Space.Lg,
            Children =
            {
                new Field { Label = "所属部门", Description = "搜索认名称也认编号：试试“焊装”“RD”", Content = department },
                required,
                new Field { Label = "只读", IsReadOnly = true, Content = Department("RD", "RD-SW") },
                new Field { Label = "不可用", IsEnabled = false, Content = Department("RD", "RD-HW") },
            },
        };
    }

    /// <summary>组织架构的一个单位：后端标记的"有下级"，还没取回来时据此显示箭头。</summary>
    sealed class Unit(string id, string name, bool hasChildren, List<Unit>? children = null)
    {
        public string Id { get; } = id;

        public string Name { get; } = name;

        public bool HasChildren { get; } = hasChildren;

        public List<Unit>? Children { get; set; } = children;
    }

    /// <summary>模拟后端：每个部门的下级。</summary>
    static readonly Dictionary<string, (string Id, string Name, bool HasChildren)[]> Server = new()
    {
        ["hq"] = [("rd", "研发中心", true), ("mfg", "制造中心", true)],
        ["rd"] = [("rd-fe", "前端组", false), ("rd-be", "后端组", false)],
        ["mfg"] = [("mfg-1", "一车间", false), ("mfg-2", "二车间", false)],
    };

    static Control Lazy()
    {
        // 已选的那一枝（总部、制造中心的下级）随表单返回；研发中心进入时再取
        Unit[] units =
        [
            new("hq", "集团总部", true,
            [
                new("rd", "研发中心", true),
                new("mfg", "制造中心", true, [new("mfg-1", "一车间", false), new("mfg-2", "二车间", false)]),
            ]),
        ];
        var requests = 0;
        var note = new Small { Tone = TextTone.Muted, Text = "已请求 0 次" };
        var cascader = new Cascader
        {
            ItemsSource = units,
            GetKey = (item, _) => ((Unit)item).Id,
            GetLabel = item => ((Unit)item).Name,
            GetChildren = item => ((Unit)item).Children,
            HasChildren = item => ((Unit)item).HasChildren,
            LoadChildren = async item =>
            {
                note.Text = $"已请求 {++requests} 次";
                var unit = (Unit)item;
                await Task.Delay(600);
                unit.Children = [.. Server.GetValueOrDefault(unit.Id, []).Select(child => new Unit(child.Id, child.Name, child.HasChildren))];
            },
            Values = ["hq", "mfg", "mfg-2"],
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { new Field { Label = "所属部门", Description = "展开时才请求下级", Content = cascader }, note },
        };
    }
}
