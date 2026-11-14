using System.Globalization;

namespace Rune.WinForms.Examples;

/// <summary>Progress 的示例（vue-tsx-pro 文档站 feedback/Progress）。spinner 在 Spinner 页，overlay 要等 LoadingOverlay、Field。</summary>
static class ProgressExamples
{
    public static Example[] All =>
    [
        new("Progress", "progress", "进度条", "Value 与 Max（默认 100）算完成比例，超出时夹住；Tone 表示状态。Label 显示在上方，同时是读屏名称；右侧默认是按语言格式化的百分比，ValueLabel 换成自己的文字（只是显示），读屏念的用 ValueText 改，ShowValueLabel=false 不显示。取值是 decimal。Bands 是量表的用法（磁盘占用、配额、评分这类量出来的数）：值落在哪一段就用哪一段的色调；仪表盘样子的量表用 Gauge。", Bars),
        new("Progress", "rings", "环形与目标", "Shape=circle 是环形：中间显示数值，Label 在下方，直径随 TextSize 缩放；不写 Value 时一段弧一直转。Target 是目标值：条上画一条竖线、环上画一道刻度，读屏念成\"60%，目标 80%\"。", Rings),
        new("Progress", "shapes", "粗细与不确定进度", "TextSize 跟字号档位缩放粗细，Radius 改圆角；不写 Value 是不确定进度，循环滑动，系统关闭动画时变慢。", Shapes),
    ];

    static readonly (string Name, decimal Used, decimal Total, string Unit)[] Quotas =
    [
        ("存储空间", 38.6m, 50, "GB"),
        ("本月流量", 912, 1000, "GB"),
        ("团队成员", 7, 20, "人"),
        ("API 调用", 120_000, 100_000, "次"),
    ];

    static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

    /// <summary>用量是量出来的数（量表）：越接近上限越醒目——八成以内正常，到上限为止警告，超出为危险。</summary>
    static ProgressBand[] BandsOf(decimal total) => [new(total * 0.8m, StatusTone.Accent), new(total, StatusTone.Warning), new(decimal.MaxValue, StatusTone.Danger)];

    static string Number(decimal value) => value.ToString("#,0.##", Chinese);

    static Control Bars() => Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
    [
        new StrongControl { Text = "套餐用量" },
        .. Quotas.Select(quota => new ProgressControl
        {
            Value = quota.Used, Max = quota.Total, Bands = BandsOf(quota.Total), Label = quota.Name,
            ValueLabel = $"{Number(quota.Used)} / {Number(quota.Total)} {quota.Unit}",
            ValueText = (value, max) => $"{value} / {max} {quota.Unit}",
            // TS 的 propsValueLabel：超出时数值用危险色
            PropsValueLabel = new TextProps { Tone = quota.Used > quota.Total ? TextTone.Danger : TextTone.Muted },
        }),
        new ProgressControl { Value = 64, Label = "资料完善度" },
        // 只要标签、不要右侧的数值：ShowValueLabel = false
        new ProgressControl { Value = 3, Max = 5, Label = "新手任务", ShowValueLabel = false, Tone = StatusTone.Success },
    ]));

    static readonly (string Name, decimal Done, decimal Target, StatusTone Tone)[] Plants =
    [
        ("一车间", 86, 80, StatusTone.Success),
        ("二车间", 64, 80, StatusTone.Warning),
        ("三车间", 38, 75, StatusTone.Danger),
    ];

    static Control Rings() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
        Build.Grid(new GridControl { MinItemWidth = "9rem", Gap = Space.Md },
            [.. Plants.Select(plant => Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Xs },
                new ProgressControl { Shape = ProgressShape.Circle, TextSize = TextSize.Lg, Value = plant.Done, Target = plant.Target, Tone = plant.Tone, Label = $"{plant.Name} 产量完成率" },
                new SmallControl { Text = $"目标 {plant.Target}%", Tone = TextTone.Muted })))]),
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
            new ProgressControl { Value = 6.4m, Max = 10, Target = 8, Label = "年度产量（万件）", ValueLabel = "6.4 / 10" },
            new ProgressControl { Value = 92, Target = 85, Tone = StatusTone.Success, Label = "交付准时率" }),
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md },
            new ProgressControl { Shape = ProgressShape.Circle, TextSize = TextSize.Sm, AccessibleName = "正在同步" },
            new TextControl { Text = "不确定进度：弧线一直转", TextSize = TextSize.Sm, Tone = TextTone.Muted },
            new ProgressControl { Shape = ProgressShape.Circle, Value = 100, TextSize = TextSize.Sm, Tone = StatusTone.Success, AccessibleName = "已完成" }));

    static Control Shapes()
    {
        var rows = new[] { TextSize.Xs, TextSize.Sm, TextSize.Md, TextSize.Lg }.Select(size =>
        {
            var bar = new ProgressControl { Value = 62, TextSize = size, Radius = size == TextSize.Lg ? Radius.Sm : Radius.Full, AccessibleName = $"粗细 {size}" };
            var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md }, new SmallControl { Text = size.Value, Tone = TextTone.Muted }, bar);
            row.SetGrow(bar, true);
            return row;
        });
        var uploaded = 3;
        var status = new TextControl { TextSize = TextSize.Sm };
        var upload = new ButtonControl { Text = "再传一个", TextSize = TextSize.Sm, Variant = ButtonVariant.Soft };
        var files = new ProgressControl { Max = 8, ValueText = (value, _) => $"已上传 {value} 个文件，共 8 个", AccessibleName = "上传进度" };
        void Refresh()
        {
            status.Text = $"已上传 {uploaded} / 8 个文件";
            upload.Enabled = uploaded < 8;
            files.Value = uploaded;
            files.Tone = uploaded >= 8 ? StatusTone.Success : StatusTone.Accent;
        }
        upload.Click += (_, _) =>
        {
            uploaded++;
            Refresh();
        };
        Refresh();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
        [
            .. rows,
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
                Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm }, new SpinnerControl { IconSize = TextSize.Sm }, new TextControl { Text = "正在连接服务器…（不确定进度：不写 Value）", TextSize = TextSize.Sm }),
                new ProgressControl { AccessibleName = "正在连接服务器" }),
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
                Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between }, status, upload),
                files),
        ]);
    }
}
