namespace Rune.WinForms.Examples;

/// <summary>Stat 页的示例（vue-tsx-pro 文档站 display/Stat）。Stat、StatGroup 的示例等 Stat。</summary>
static class StatExamples
{
    public static Example[] All =>
    [
        new("Stat", "animated-number", "AnimatedNumber 单独使用", "值变化时从旧值滚到新值，等宽数字不抖动。Format 是 Intl.NumberFormat 的选项加上缩放 Scale（NumericFormat），金额不写币种时用语言包的币种；值是 decimal，中间帧按浮点数算，停下时按原值格式化，不丢精度。Trend 按正负着色；Prefix、Suffix 是数字前后的文字，图标写 PrefixIcon。", Numbers),
    ];

    static Control Numbers()
    {
        var random = new Random(7);
        decimal Between(double min, double max) => (decimal)(min + random.NextDouble() * (max - min));
        var balance = new AnimatedNumberControl { Value = 12880.35m, Format = new NumericFormat { Style = NumericStyle.Currency } };
        var growth = new AnimatedNumberControl { Value = 0.124m, Format = new NumericFormat { Style = NumericStyle.Percent, MaximumFractionDigits = 1 }, Trend = true };
        var downloads = new AnimatedNumberControl { Value = 9_007_199_254_740_993m };
        var rating = new AnimatedNumberControl
        {
            Value = 4.6m, Format = new NumericFormat { MinimumFractionDigits = 1 }, Suffix = " / 5",
            PrefixIcon = Lucide.Star, PrefixIconTone = TextTone.Warning, Duration = TimeSpan.FromMilliseconds(500),
        };
        var shuffle = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.RefreshCw, Text = "换一组数" };
        shuffle.Click += (_, _) =>
        {
            balance.Value += Math.Round(Between(-2000, 2000), 2);
            growth.Value = Math.Round(Between(-0.2, 0.3), 4);
            downloads.Value += Math.Round(Between(1000, 50_000));
            rating.Value = Math.Round(Between(3, 5), 1);
        };
        var values = new DescriptionsControl { Columns = 2 };
        foreach (var item in new DescriptionItem[]
        {
            new() { Label = "余额（decimal）", Value = balance },
            new() { Label = "环比", Value = growth },
            new() { Label = "累计下载", Value = downloads },
            new() { Label = "评分", Value = rating },
        }) values.Items.Add(item);
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch }, values, Build.Flex(new FlexControl(), shuffle));
    }
}
