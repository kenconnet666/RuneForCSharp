using System.Windows;

namespace Rune.Wpf.Examples;

/// <summary>Stat 页的示例（vue-tsx-pro 文档站 display/Stat）。Stat、StatGroup 的示例等 Stat。</summary>
static class StatExamples
{
    public static Example[] All =>
    [
        new("Stat", "animated-number", "AnimatedNumber 单独使用", "值变化时从旧值滚到新值，等宽数字不抖动。Format 是 Intl.NumberFormat 的选项加上缩放 Scale（NumericFormat），金额不写币种时用语言包的币种；值是 decimal，中间帧按浮点数算，停下时按原值格式化，不丢精度。Trend 按正负着色；Prefix、Suffix 放在数字前后（图标、单位）。", Numbers),
    ];

    static FrameworkElement Numbers()
    {
        var random = new Random(7);
        decimal Between(double min, double max) => (decimal)(min + random.NextDouble() * (max - min));
        var balance = new AnimatedNumber { Value = 12880.35m, Format = new NumericFormat { Style = NumericStyle.Currency } };
        var growth = new AnimatedNumber { Value = 0.124m, Format = new NumericFormat { Style = NumericStyle.Percent, MaximumFractionDigits = 1 }, Trend = true };
        var downloads = new AnimatedNumber { Value = 9_007_199_254_740_993m };
        var rating = new AnimatedNumber
        {
            Value = 4.6m, Format = new NumericFormat { MinimumFractionDigits = 1 }, Suffix = " / 5",
            Prefix = new Icon { Data = Lucide.Star, Tone = TextTone.Warning }, Duration = TimeSpan.FromMilliseconds(500),
        };
        var shuffle = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.RefreshCw, Content = "换一组数" };
        shuffle.Click += (_, _) =>
        {
            balance.Value += Math.Round(Between(-2000, 2000), 2);
            growth.Value = Math.Round(Between(-0.2, 0.3), 4);
            downloads.Value += Math.Round(Between(1000, 50_000));
            rating.Value = Math.Round(Between(3, 5), 1);
        };
        var values = new Descriptions { Columns = 2 };
        foreach (var item in new DescriptionItem[]
        {
            new DescriptionItem { Label = "余额（decimal）", Value = balance },
            new DescriptionItem { Label = "环比", Value = growth },
            new DescriptionItem { Label = "累计下载", Value = downloads },
            new DescriptionItem { Label = "评分", Value = rating },
        }) values.Items.Add(item);
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { values, shuffle } };
    }
}
