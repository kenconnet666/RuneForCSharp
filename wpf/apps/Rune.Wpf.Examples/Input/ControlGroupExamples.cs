using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace Rune.Wpf.Examples;

/// <summary>ControlGroup 的示例（vue-tsx-pro 文档站 input/ControlGroup）。上游的搜索放在 form role="search" 里，桌面上是回车与按钮都搜索。</summary>
static class ControlGroupExamples
{
    public static Example[] All =>
    [
        new("ControlGroup", "amount", "币种与金额", "组里的输入框与按钮去掉相邻一侧的圆角、边框叠在一起，聚焦的那个浮到上面，焦点环完整。输入框分掉剩余宽度，选择框与按钮按内容宽。每个控件写自己的 AutomationProperties.Name；放在 Field 里时标签、说明给组，点标签聚焦第一个控件。只有文字的单位、币种符号用输入框自己的 Prefix / Suffix，不必拆成两个控件。", Amount),
        new("ControlGroup", "search", "搜索框、地址与竖排", "回车与按钮都能搜索。复制按钮这类图标按钮同样相连。Orientation=vertical 时上下相连。", Search),
    ];

    static T Named<T>(T control, string name) where T : FrameworkElement
    {
        AutomationProperties.SetName(control, name);
        return control;
    }

    static ControlGroup Group(params UIElement[] controls)
    {
        var group = new ControlGroup();
        foreach (var control in controls) group.Children.Add(control);
        return group;
    }

    static readonly Dictionary<string, decimal> Rates = new() { ["CNY"] = 1, ["USD"] = 7.12m, ["EUR"] = 7.71m, ["JPY"] = 0.047m };

    static FrameworkElement Amount()
    {
        var currency = Named(new Select { ItemsSource = Rates.Keys.ToArray(), Value = "USD" }, "币种");
        var amount = Named(new NumberField { Value = 1200, Min = 0 }, "金额");
        var result = new Small { Tone = TextTone.Muted, Text = "点“换算”看人民币金额" };
        var convert = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Content = "换算" };
        convert.Click += (_, _) => result.Text = $"≈ {(amount.Value ?? 0) * Rates[currency.Value ?? "CNY"]:#,0.##} 元";
        AutomationProperties.SetLiveSetting(result, AutomationLiveSetting.Polite);
        var unit = Named(new Select { ItemsSource = new[] { new ListboxOption("piece", "每件"), new ListboxOption("kg", "每千克") }, Value = "piece" }, "计价单位");
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                new Field { Label = "合同金额", Description = "按当天中间价换算成人民币", Content = Group(currency, amount, convert) },
                result,
                new Field
                {
                    Label = "单价区间",
                    Content = Group(Named(new NumberField { Value = 10, Prefix = "¥" }, "最低单价"), Named(new NumberField { Value = 80, Prefix = "¥" }, "最高单价"), unit),
                },
            },
        };
    }

    static readonly ListboxOption[] Scopes = [new("all", "全部"), new("title", "标题"), new("owner", "负责人")];

    static FrameworkElement Search()
    {
        var scope = Named(new Select { ItemsSource = Scopes, Value = "all" }, "搜索范围");
        var keyword = Named(new TextField { Icon = Lucide.Search, PlaceholderText = "任务名称、编号或负责人" }, "关键词");
        var searched = new Small { Tone = TextTone.Muted, Visibility = Visibility.Collapsed };
        void Run()
        {
            searched.Text = $"在“{Scopes.First(item => item.Value == scope.Value).Label}”里搜索：{keyword.Text}";
            searched.Visibility = string.IsNullOrEmpty(keyword.Text) ? Visibility.Collapsed : Visibility.Visible;
        }
        var search = new Button { Content = "搜索" };
        search.Click += (_, _) => Run();
        keyword.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Run();
        };
        var searchGroup = Named(Group(scope, keyword, search), "搜索任务");

        var protocol = Named(new Select { ItemsSource = new[] { "https://", "http://" }, Value = "https://" }, "协议");
        var host = Named(new TextField { Text = "hooks.example.com" }, "域名");
        var copy = Named(new CopyButton { Variant = ButtonVariant.Outline, ValueFactory = () => Task.FromResult($"{protocol.Value}{host.Text}") }, "复制地址");

        var address = new ControlGroup { Orientation = AttachedOrientation.Vertical };
        address.Children.Add(Named(new TextField { Text = "浙江省杭州市西湖区" }, "省市区"));
        address.Children.Add(Named(new TextField { Text = "文三路 90 号 3 幢 501" }, "详细地址"));
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                searchGroup,
                searched,
                new Field { Label = "回调地址", Content = Group(protocol, host, copy) },
                new Field { Label = "收货地址", Description = "Orientation=vertical：上下相连", Content = address },
            },
        };
    }
}
