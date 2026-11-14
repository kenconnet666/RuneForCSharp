using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>Combobox、Autocomplete 页的示例（vue-tsx-pro 文档站 input/Combobox、input/Autocomplete）。</summary>
static class ComboboxExamples
{
    public static Example[] All =>
    [
        new("Combobox", "basic", "本地筛选与远程搜索", "默认本地筛选（忽略大小写与重音），输入法组合期间不拿拼音去筛选。远程搜索给 Load(输入, 页, 取消)：输入停下 300 毫秒后请求，只采用最后一次，被顶掉的请求经取消令牌中止；自己管请求时关掉 LocalFilter，按 Text 换 ItemsSource、配合 IsLoading。", Cities),
        new("Combobox", "freeform", "自由输入、图标与状态", "AllowCustomValue 允许输入选项里没有的文字：失焦时保留，Value 为 null、文字在 Text 里。Icon、Variant、Radius 与只读、不可用同其他输入框。", Freeform),
        new("Combobox", "submit", "提交的值与自定义匹配", "Value 是选中项的值（编号），不是输入框里显示的文字。Filter 自己决定哪些项匹配（按编号、拼音、别名）；选项可以是任意数据，ToOption 映射成值、文字与说明。ShowToggle=False 去掉展开按钮；必填没选时把错误交给外面的 Field。", Submit),
        new("Autocomplete", "email", "邮箱后缀", "Text 就是值，不选建议也行；要从固定的选项里选一个（值是选项的 value）时用 Combobox。建议可以随输入算出来，配 LocalFilter=False 不再本地过滤；没有可选的建议时不弹出列表。", Email),
        new("Autocomplete", "options", "对象数据、外观与状态", "建议可以是任意数据，ToOption 把它映射成选项：Label 是填进输入框的文字，Description、Icon、Group 照常显示在列表里。Variant、Size、Radius 与其他输入框同一套；必填、出错、只读、不可用可以写在外面的 Field 上。", Options),
        new("Autocomplete", "remote", "远程建议", "Load(输入, 页, 取消) 在输入停下 300 毫秒后请求，只采用最后一次（同 Combobox、Select）。系统里还没有的名称直接写全称，提交时再建。", Remote),
    ];

    /// <summary>依赖属性变了时调用（WPF 没有统一的属性变化事件）。</summary>
    static void Watch(DependencyObject target, DependencyProperty property, Action changed) =>
        DependencyPropertyDescriptor.FromProperty(property, target.GetType()).AddValueChanged(target, (_, _) => changed());

    static readonly string[] CityNames =
    [
        "北京", "上海", "广州", "深圳", "杭州", "南京", "苏州", "成都", "重庆", "武汉", "西安", "天津", "长沙", "郑州", "青岛",
        "厦门", "宁波", "合肥", "福州", "昆明", "大连", "沈阳", "哈尔滨", "济南", "无锡", "东莞", "佛山", "珠海", "南宁", "贵阳",
    ];

    static readonly string[] Users = ["zhouyihang", "linxiaoman", "chenmo", "xuyou", "wangqing", "zhaohaoran", "sunsiyuan", "liyutong"];

    /// <summary>假的远程搜索：300 毫秒后返回包含关键词的用户名；被后来的请求顶掉时中止。</summary>
    static async Task<OptionsPage> SearchUsers(string keyword, int page, CancellationToken cancellation)
    {
        await Task.Delay(300, cancellation);
        return OptionsPage.All(keyword.Length == 0 ? [] : Users.Where(user => user.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
    }

    static FrameworkElement Cities()
    {
        var city = new Combobox { ItemsSource = CityNames, Clearable = true, PlaceholderText = "输入或选择" };
        var user = new Combobox { Load = SearchUsers, PlaceholderText = "输入用户名", Empty = "输入关键词开始搜索" };
        var summary = new Small { Tone = TextTone.Muted };
        void Update() => summary.Text = $"城市：{city.Value ?? "未选"} · 成员：{user.Value ?? "未选"}";
        foreach (var box in new[] { city, user }) Watch(box, Combobox.ValueProperty, Update);
        user.TextChanged += (_, _) => user.Empty = string.IsNullOrEmpty(user.Text) ? "输入关键词开始搜索" : "没有匹配的成员";
        Update();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children =
            {
                new Field { Label = "城市", Description = "本地过滤：输入“州”试试", Content = city },
                new Field { Label = "成员", Description = "远程搜索：Load 在输入停下 300 毫秒后请求，只采用最后一次", Content = user },
                summary,
            },
        };
    }

    static readonly string[] Titles = ["产品经理", "交互设计师", "视觉设计师", "前端工程师", "后端工程师", "测试工程师"];

    static FrameworkElement Freeform()
    {
        var title = new Combobox { ItemsSource = Titles, AllowCustomValue = true, Icon = Lucide.BriefcaseBusiness, Clearable = true, PlaceholderText = "选择或输入" };
        var field = new Field { Label = "职位", Content = title };
        void Update() => field.Description = title.Value is { } value ? $"选中了 {value}" : $"自由输入：{(string.IsNullOrEmpty(title.Text) ? "（空）" : title.Text)}，Value 为 null";
        Watch(title, Combobox.ValueProperty, Update);
        title.TextChanged += (_, _) => Update();
        Update();
        return new Grid
        {
            MinItemWidth = "15rem", Gap = Space.Lg,
            Children =
            {
                field,
                new Field { Label = "Variant=soft Radius=full", Content = new Combobox { ItemsSource = Titles, Icon = Lucide.Search, Variant = ControlVariant.Soft, Radius = Radius.Full, PlaceholderText = "搜索职位" } },
                new Field { Label = "只读", IsReadOnly = true, Content = new Combobox { ItemsSource = Titles, Value = "前端工程师" } },
                new Field { Label = "不可用", IsEnabled = false, Content = new Combobox { ItemsSource = Titles, Value = "测试工程师" } },
            },
        };
    }

    sealed record Material(string Code, string Name, string Spec);

    static readonly Material[] Materials =
    [
        new("B-6204", "深沟球轴承", "20 × 47 × 14"),
        new("B-6205", "深沟球轴承", "25 × 52 × 15"),
        new("F-M8-30", "六角螺栓", "M8 × 30，8.8 级"),
        new("S-OR-25", "O 形密封圈", "内径 25，线径 2.65"),
        new("H-V-10", "液压阀组", "10 通径，24 V"),
    ];

    static FrameworkElement Submit()
    {
        var material = new Combobox
        {
            ItemsSource = Materials, Clearable = true, PlaceholderText = "编号或名称",
            ToOption = item => item is Material m ? new ListboxOption(m.Code, $"{m.Name} {m.Spec}") { Description = m.Code } : throw new ArgumentException("不是物料"),
            Filter = (item, query) => item is Material m && (m.Code.Contains(query, StringComparison.OrdinalIgnoreCase) || m.Name.Contains(query)),
        };
        var materialField = new Field { Label = "物料", IsRequired = true, Description = "按编号或名称搜：输入 6204、螺栓 都行", Content = material };
        var unit = new Combobox { ItemsSource = new[] { "件", "套", "箱", "千克" }, Value = "件", ShowToggle = false };
        var result = new Small { Tone = TextTone.Muted, Text = "还没提交" };
        var submit = new Button { Size = TextSize.Sm, Content = "提交" };
        submit.Click += (_, _) =>
        {
            materialField.Error = material.Value is null ? "请选择物料" : null;
            if (material.Value is null) return;
            result.Text = $"提交的是值：{{ material: {material.Value}, unit: {unit.Value} }}";
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children = { materialField, new Field { Label = "单位", Content = unit }, new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { submit, result } } },
        };
    }

    static readonly string[] Domains = ["qq.com", "163.com", "foxmail.com", "gmail.com", "outlook.com", "company.cn"];

    /// <summary>按已输入的 @ 后面部分筛后缀：lin@q → lin@qq.com。</summary>
    static string[] Suggest(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var at = text.IndexOf('@');
        var (name, typed) = at < 0 ? (text, "") : (text[..at], text[(at + 1)..]);
        return [.. Domains.Where(domain => domain.StartsWith(typed, StringComparison.Ordinal)).Select(domain => $"{name}@{domain}")];
    }

    /// <summary>建议跟着输入算：Text 变了换 ItemsSource（建议已经按输入算好，不再本地过滤）。</summary>
    static Autocomplete EmailBox(string text)
    {
        var box = new Autocomplete { Text = text, LocalFilter = false, Icon = Lucide.Mail };
        box.ItemsSource = Suggest(text);
        box.TextChanged += (_, _) => box.ItemsSource = Suggest(box.Text);
        return box;
    }

    static FrameworkElement Email()
    {
        var email = EmailBox("");
        email.Clearable = true;
        email.PlaceholderText = "输入邮箱";
        var field = new Field { Label = "联系邮箱", Description = "值就是文字：（空）", Content = email };
        email.TextChanged += (_, _) => field.Description = $"值就是文字：{(string.IsNullOrEmpty(email.Text) ? "（空）" : email.Text)}";
        return new Grid
        {
            MinItemWidth = "15rem", Gap = Space.Lg,
            Children = { field, new Field { Label = "抄送", Description = "已有值时照常显示；建议只在输入时出现", Content = EmailBox("finance@company.cn") } },
        };
    }

    sealed record Address(string Id, string Text, string Note);

    static readonly Address[] Addresses =
    [
        new("hz", "杭州市余杭区文一西路 969 号", "总部 · 工作日 9:00–18:00 收货"),
        new("nb", "宁波市北仑区明州西路 88 号", "一号仓 · 全天收货"),
        new("sz", "苏州市工业园区星湖街 328 号", "研发中心 · 前台代收"),
    ];

    static FrameworkElement Options()
    {
        string[] cities = ["杭州", "宁波", "苏州", "无锡", "成都"];
        var address = new Autocomplete
        {
            ItemsSource = Addresses, Icon = Lucide.MapPin, Clearable = true, PlaceholderText = "省市区、街道、门牌号",
            ToOption = item => item is Address a ? new ListboxOption(a.Id, a.Text) { Description = a.Note } : throw new ArgumentException("不是地址"),
        };
        var required = new Autocomplete { ItemsSource = cities, PlaceholderText = "城市" };
        var requiredField = new Field { Label = "必填", IsRequired = true, Error = "请填写城市", Content = required };
        required.TextChanged += (_, _) => requiredField.Error = string.IsNullOrEmpty(required.Text) ? "请填写城市" : null;
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                new Field { Label = "收货地址", Description = "常用地址作建议：输入“杭州”“路”试试，也可以自己写一个新的", Content = address },
                new Grid
                {
                    MinItemWidth = "11rem", Gap = Space.Md,
                    Children =
                    {
                        new Field { Label = "Variant=soft Radius=full", Content = new Autocomplete { ItemsSource = cities, Variant = ControlVariant.Soft, Radius = Radius.Full, PlaceholderText = "城市" } },
                        new Field { Label = "Size=sm", Content = new Autocomplete { ItemsSource = cities, Size = TextSize.Sm, PlaceholderText = "城市" } },
                        requiredField,
                        new Field { Label = "只读", Content = new Autocomplete { ItemsSource = cities, Text = "杭州", IsReadOnly = true } },
                        new Field { Label = "不可用", Content = new Autocomplete { ItemsSource = cities, Text = "杭州", IsEnabled = false } },
                    },
                },
            },
        };
    }

    static readonly string[] Suppliers =
    [
        "杭州远山精密机械有限公司", "杭州青禾研发中心有限公司", "宁波海川液压件有限公司", "苏州明德电子科技有限公司",
        "无锡恒通密封件有限公司", "上海星澜涂装工程有限公司", "成都锐川钣金制造有限公司",
    ];

    /// <summary>假的接口：400 毫秒后返回包含输入的供应商（没输入时不给建议）；新的输入到来时经取消令牌中止。</summary>
    static async Task<OptionsPage> SearchSuppliers(string text, int page, CancellationToken cancellation)
    {
        if (text.Length == 0) return OptionsPage.All(Array.Empty<string>());
        await Task.Delay(400, cancellation);
        return OptionsPage.All(Suppliers.Where(name => name.Contains(text)));
    }

    static FrameworkElement Remote()
    {
        var supplier = new Autocomplete { Load = SearchSuppliers, Icon = Lucide.BuildingComplex, Clearable = true, PlaceholderText = "供应商全称" };
        var status = new Small { Tone = TextTone.Muted, Text = "还没有填" };
        supplier.TextChanged += (_, _) => status.Text = string.IsNullOrEmpty(supplier.Text) ? "还没有填" : $"将提交：{supplier.Text}";
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Xs,
            Children = { new Field { Label = "供应商", Description = "输入“杭州”“液压”试试；选不到时直接写全称，提交后会新建", Content = supplier }, status },
        };
    }
}
