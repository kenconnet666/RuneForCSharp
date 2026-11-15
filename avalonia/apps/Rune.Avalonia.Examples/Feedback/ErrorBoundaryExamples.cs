using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Rune.Avalonia.Examples;

/// <summary>
/// ErrorBoundary 的示例（vue-tsx-pro 文档站 feedback/ErrorBoundary）。上游在渲染函数里抛错；桌面上接得住的是建内容时与排版时的异常，
/// 这里的"出错"在建内容时抛出（读不到字段的档案、空的金额）。
/// </summary>
static class ErrorBoundaryExamples
{
    public static Example[] All =>
    [
        new("ErrorBoundary", "dashboard", "工作台的一块出错", "只有这块换成出错提示，旁边的指标照常；ErrorCaught 上报；点\"重试\"时这块重新建。", Dashboard),
        new("ErrorBoundary", "fallback", "自己画出错时的内容", "Fallback 拿到错误与重试；ResetKey 变了（换了要看的供应商）就自动恢复。", Fallback),
    ];

    static Control Dashboard()
    {
        var failNext = false;
        var reports = 0;
        var reported = new Small { Tone = TextTone.Muted };
        void ShowReports() => reported.Text = $"ErrorCaught 已上报 {reports} 次";
        var boundary = new ErrorBoundary
        {
            ContentTemplate = new FuncDataTemplate<object?>((_, _) =>
            {
                var broken = failNext;
                failNext = false;
                // 模拟读接口数据的卡片：打开 failNext 时下一次建的时候出错
                if (broken) throw new InvalidOperationException("接口返回的金额字段是空的（revenue.total）");
                return new Stat { Label = "本月销售额", Value = 1_284_000, Unit = "元", Change = 0.124m };
            }),
        };
        boundary.ErrorCaught += (_, _) =>
        {
            reports++;
            ShowReports();
        };
        ShowReports();
        var trigger = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Content = "让第一块出错" };
        trigger.Click += (_, _) =>
        {
            failNext = true;
            boundary.ResetKey = Guid.NewGuid();
        };
        return new Grid
        {
            MinItemWidth = "16rem",
            Balanced = true,
            Gap = Space.Md,
            Children =
            {
                new Card { Content = boundary },
                new Card { Content = new Stat { Label = "待我审批", Value = 6, Unit = "件" } },
                new Card { Content = new Flex { Direction = FlexDirection.Column, Align = FlexAlign.Start, Gap = Space.Sm, Children = { new Strong { Text = "模拟接口出错" }, reported, trigger } } },
            },
        };
    }

    sealed record Supplier(string Name, string Grade, string Contact);

    static readonly Dictionary<string, Supplier?> Suppliers = new()
    {
        ["hc"] = new("华辰精密机械有限公司", "A 级", "林小满 · 138 0013 8000"),
        ["qh"] = new("青禾线缆", "B 级", "周一航 · 139 0022 1100"),
        // 档案缺失：建的时候读不到字段
        ["ys"] = null,
    };

    static Control Fallback()
    {
        var id = new ToggleGroup
        {
            IsRequired = true,
            Value = "ys",
            Size = TextSize.Sm,
            Children = { new ToggleItem { Value = "hc", Content = "华辰精密" }, new ToggleItem { Value = "qh", Content = "青禾线缆" }, new ToggleItem { Value = "ys", Content = "远山钣金（档案缺失）" } },
        };
        AutomationProperties.SetName(id, "供应商");
        var boundary = new ErrorBoundary
        {
            ResetKey = id.Value,
            ContentTemplate = new FuncDataTemplate<object?>((_, _) =>
            {
                var supplier = Suppliers[id.Value ?? "ys"] ?? throw new InvalidOperationException("没有读到这家供应商的档案");
                var profile = new Descriptions { Columns = 1 };
                profile.Items.AddRange([
                    new DescriptionItem { Label = "供应商", Value = supplier.Name },
                    new DescriptionItem { Label = "等级", Value = supplier.Grade },
                    new DescriptionItem { Label = "联系人", Value = supplier.Contact },
                ]);
                return profile;
            }),
            Fallback = (error, retry) =>
            {
                var again = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "重试" };
                again.Click += (_, _) => retry();
                return new Alert { Tone = StatusTone.Warning, Title = "供应商档案没能显示", Action = again, Content = $"{error.Message}。换一家供应商会自动恢复。" };
            },
        };
        id.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleGroup.ValueProperty) boundary.ResetKey = id.Value;
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { id, boundary } };
    }
}
