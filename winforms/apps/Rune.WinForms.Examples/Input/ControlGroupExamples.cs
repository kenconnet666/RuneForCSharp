namespace Rune.WinForms.Examples;

/// <summary>ControlGroupControl 的示例（vue-tsx-pro 文档站 input/ControlGroup）。上游的搜索放在 form role="search" 里，桌面上是回车与按钮都搜索。</summary>
static class ControlGroupExamples
{
    public static Example[] All =>
    [
        new("ControlGroup", "amount", "币种与金额", "组里的输入框与按钮去掉相邻一侧的圆角、边框叠在一起，聚焦的那个浮到上面。输入框分掉剩余宽度，选择框与按钮按内容宽。每个控件写自己的 AccessibleName；放在 FieldControl 里时标签、说明给组，点标签聚焦第一个控件。只有文字的单位、币种符号用输入框自己的 Prefix / Suffix，不必拆成两个控件。", Amount),
        new("ControlGroup", "search", "搜索框、地址与竖排", "回车与按钮都能搜索。复制按钮这类图标按钮同样相连。Orientation=vertical 时上下相连。", Search),
    ];

    static ControlGroupControl Group(params Control[] controls)
    {
        var group = new ControlGroupControl();
        group.Controls.AddRange(controls);
        return group;
    }

    static readonly Dictionary<string, decimal> Rates = new() { ["CNY"] = 1, ["USD"] = 7.12m, ["EUR"] = 7.71m, ["JPY"] = 0.047m };

    static Control Amount()
    {
        var currency = new SelectControl { ItemsSource = Rates.Keys.ToArray(), Value = "USD", AccessibleName = "币种" };
        var amount = new NumberFieldControl { Value = 1200, Min = 0, AccessibleName = "金额" };
        var result = new SmallControl { Tone = TextTone.Muted, Text = "点“换算”看人民币金额" };
        var convert = new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Text = "换算" };
        convert.Click += (_, _) => result.Text = $"≈ {(amount.Value ?? 0) * Rates[currency.Value ?? "CNY"]:#,0.##} 元";
        var unit = new SelectControl { ItemsSource = new[] { new ListboxOption("piece", "每件"), new ListboxOption("kg", "每千克") }, Value = "piece", AccessibleName = "计价单位" };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
            new FieldControl { Label = "合同金额", Description = "按当天中间价换算成人民币", Content = Group(currency, amount, convert) },
            result,
            new FieldControl
            {
                Label = "单价区间",
                Content = Group(new NumberFieldControl { Value = 10, Prefix = "¥", AccessibleName = "最低单价" }, new NumberFieldControl { Value = 80, Prefix = "¥", AccessibleName = "最高单价" }, unit),
            });
    }

    static readonly ListboxOption[] Scopes = [new("all", "全部"), new("title", "标题"), new("owner", "负责人")];

    static Control Search()
    {
        var scope = new SelectControl { ItemsSource = Scopes, Value = "all", AccessibleName = "搜索范围" };
        var keyword = new TextFieldControl { Icon = Lucide.Search, PlaceholderText = "任务名称、编号或负责人", AccessibleName = "关键词" };
        var searched = new SmallControl { Tone = TextTone.Muted, Visible = false };
        void Run()
        {
            searched.Text = $"在“{Scopes.First(item => item.Value == scope.Value).Label}”里搜索：{keyword.Text}";
            searched.Visible = !string.IsNullOrEmpty(keyword.Text);
        }
        var search = new ButtonControl { Text = "搜索" };
        search.Click += (_, _) => Run();
        keyword.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            Run();
        };
        var searchGroup = Group(scope, keyword, search);
        searchGroup.AccessibleName = "搜索任务";

        var protocol = new SelectControl { ItemsSource = new[] { "https://", "http://" }, Value = "https://", AccessibleName = "协议" };
        var host = new TextFieldControl { Text = "hooks.example.com", AccessibleName = "域名" };
        var copy = new CopyButtonControl { Variant = ButtonVariant.Outline, ValueFactory = () => Task.FromResult($"{protocol.Value}{host.Text}"), AccessibleName = "复制地址" };

        var address = Group(new TextFieldControl { Text = "浙江省杭州市西湖区", AccessibleName = "省市区" }, new TextFieldControl { Text = "文三路 90 号 3 幢 501", AccessibleName = "详细地址" });
        address.Orientation = AttachedOrientation.Vertical;
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
            searchGroup,
            searched,
            new FieldControl { Label = "回调地址", Content = Group(protocol, host, copy) },
            new FieldControl { Label = "收货地址", Description = "Orientation=vertical：上下相连", Content = address });
    }
}
