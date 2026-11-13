using System.Windows;
using System.Windows.Documents;

namespace Rune.Wpf.Examples;

/// <summary>Empty 页的示例（vue-tsx-pro 文档站 feedback/Empty：空状态与结果）。</summary>
static class EmptyExamples
{
    public static Example[] All =>
    [
        new("Empty", "empty", "空状态", "列表、表格、搜索结果没有内容时用 Empty：说清楚为什么空、接下来能做什么；Size 跟字号档位缩放，下拉选项里用 sm。", Empties),
        new("Empty", "result", "结果", "Result 是提交、支付这类操作后的整块反馈：Tone 决定图标与颜色，图标加载时一笔画出（Draw），系统关闭动画时直接显示。", Results),
        new("Empty", "status", "403、404、500", "Status 给出常见出错页的图标、色调与状态码，标题、说明取语言包（Lang.ResultStatus），显式写的优先：403 补一句缺的是什么权限，比通用的说明更有用。状态码只是装饰，读屏念标题。", Statuses),
    ];

    static readonly (string Tone, string Label, string Title, string Description, string Action)[] Outcomes =
    [
        ("success", "成功", "提交成功", "审核结果会在 1 个工作日内通过邮件通知你。", "返回列表"),
        ("danger", "失败", "支付失败", "银行卡余额不足，订单已保留 30 分钟。", "换一张卡"),
        ("warning", "警告", "需要补充资料", "营业执照照片不清晰，请重新上传。", "去上传"),
        ("info", "信息", "正在审核", "提交后通常 2 小时内完成审核。", "查看进度"),
    ];

    /// <summary>上面一排切换，下面的卡片里是 <paramref name="build"/> 建的结果；每次切换都新建，图标重新画一遍。</summary>
    static FrameworkElement Switcher(string name, (string Value, string Label)[] items, string initial, Func<string, Result> build)
    {
        var group = new ToggleGroup { IsRequired = true, Size = TextSize.Sm };
        foreach (var (value, label) in items) group.Children.Add(new ToggleItem { Value = value, Content = label });
        group.Value = initial;
        System.Windows.Automation.AutomationProperties.SetName(group, name);
        var card = new Card { Content = build(initial) };
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ToggleGroup.ValueProperty, typeof(ToggleGroup)).AddValueChanged(group, (_, _) =>
        {
            if (group.Value is { } value) card.Content = build(value);
        });
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { new Flex { Children = { group } }, card } };
    }

    static Flex Actions(params UIElement[] buttons)
    {
        var row = new Flex { Gap = Space.Sm, Justify = FlexJustify.Center };
        foreach (var button in buttons) row.Children.Add(button);
        return row;
    }

    static FrameworkElement Results() => Switcher("结果的色调", [.. Outcomes.Select(outcome => (outcome.Tone, outcome.Label))], "success", tone =>
    {
        var outcome = Outcomes.First(item => item.Tone == tone);
        return new Result
        {
            Tone = new StatusTone(tone), Title = outcome.Title, Description = outcome.Description,
            Content = new Text { Inlines = { new Code { Inlines = { new Run($"Tone=\"{tone}\"") } } } },
            Action = Actions(new Button { Size = TextSize.Sm, Content = outcome.Action }, new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Content = "帮助" }),
        };
    });

    static FrameworkElement Statuses() => Switcher("状态", [("403", "403 没有权限"), ("404", "404 找不到"), ("500", "500 服务出错")], "404", status => new Result
    {
        Status = new ResultStatus(status),
        // 403 补一句具体缺的权限：显式写的说明优先于语言包的默认说明
        Description = status == "403" ? "查看项目台账需要“财务查看”权限，可以联系你的部门管理员开通。" : null,
        Action = Actions(
            status == "500" ? new Button { Size = TextSize.Sm, Content = "重试" } : new LinkButton { Size = TextSize.Sm, Href = new Uri("https://example.com/"), Content = "回到首页" },
            new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Content = status == "403" ? "申请权限" : "联系管理员" }),
    });

    static FrameworkElement Empties() => new Grid
    {
        MinItemWidth = "14em",
        Gap = Space.Md,
        Children =
        {
            new Card
            {
                Content = new Empty
                {
                    Icon = Lucide.Inbox, Title = "还没有订单", Description = "顾客下单后会显示在这里，也可以先手动录入一张。",
                    Action = new Button { Content = "新建订单", Size = TextSize.Sm, Icon = Lucide.Plus },
                },
            },
            new Card
            {
                Content = new Empty
                {
                    Icon = Lucide.SearchX, Size = TextSize.Sm, Title = "没有找到“vue-tsx”", Description = "换个关键词，或者检查拼写。",
                    Action = new Text { Size = TextSize.Sm, Inlines = { new Run("试试 "), new Link { Inlines = { new Run("button") } } } },
                },
            },
            new Card { Content = new Empty { Icon = Lucide.FolderOpen, Size = TextSize.Sm, Title = "文件夹是空的" } },
        },
    };
}
