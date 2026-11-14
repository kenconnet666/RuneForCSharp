using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Rating 页的示例（vue-tsx-pro 文档站 input/Rating）。</summary>
static class RatingExamples
{
    public static Example[] All =>
    [
        new("Rating", "review", "供应商评价", "可编辑时是单选组：每个分数是一个单选项，读屏的名称是“3 星”，Tab 落在已选的分数上，方向键换分数。指针悬停时预览将要选的分数，再点一次已选的分数清空（Clearable，默认开）。AllowHalf 每颗星左右各一半。IsReadOnly 只显示：整排是一张图，读“4.5 星，满分 5 星”。", Review),
        new("Rating", "variants", "图标、颜色与只读列表", "Icon 换图标（心、火焰），Tone 换选中的颜色，默认金黄（warning）；Count 改星数，Size 跟字号档位缩放。评论列表里用只读的小号评分，旁边写上分数或文字。", Variants),
    ];

    static Control Review()
    {
        (string Label, string Hint, double? Score)[] aspects = [("质量", "来料合格率、返工次数", 4.5), ("交期", "准时交付、紧急插单的响应", 3), ("服务", "沟通、售后、资料齐全", null)];
        var ratings = aspects.Select(aspect => new Rating { Value = aspect.Score, AllowHalf = true }).ToArray();
        var average = new Rating { IsReadOnly = true, AllowHalf = true, Size = TextSize.Sm };
        global::Avalonia.Automation.AutomationProperties.SetName(average, "综合");
        var averageText = new Small { Tone = TextTone.Muted };
        void Sync()
        {
            var given = ratings.Select(rating => rating.Value).OfType<double>().ToArray();
            // 综合分取到半星
            average.Value = given.Length > 0 ? Math.Round(given.Average() * 2, MidpointRounding.AwayFromZero) / 2 : null;
            averageText.Text = average.Value?.ToString() ?? "还没打分";
        }
        foreach (var rating in ratings)
            rating.PropertyChanged += (_, e) =>
            {
                if (e.Property == Rating.ValueProperty) Sync();
            };
        Sync();
        var grid = new Grid { MinItemWidth = "13rem", Gap = Space.Md };
        for (var i = 0; i < aspects.Length; i++) grid.Children.Add(new Field { Label = aspects[i].Label, Description = aspects[i].Hint, Content = ratings[i] });
        return new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children =
                {
                    new Text { Text = "杭州远山精密机械 · 2026 年第三季度评价", Weight = TextWeight.Semibold },
                    grid,
                    new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Small { Text = "综合", Tone = TextTone.Muted }, average, averageText } },
                },
            },
        };
    }

    static Control Variants()
    {
        (string Name, double Score, string Text)[] reviews = [("林小满", 5, "交货快，包装很仔细"), ("周一航", 3.5, "质量可以，资料晚了两天"), ("许悠", 2, "第二批有划伤，已退换")];
        var list = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs };
        foreach (var review in reviews)
        {
            var score = new Rating { Value = review.Score, IsReadOnly = true, AllowHalf = true, Size = TextSize.Xs };
            global::Avalonia.Automation.AutomationProperties.SetName(score, review.Name);
            list.Children.Add(new Flex
            {
                Align = FlexAlign.Center, Gap = Space.Sm,
                Children = { score, new Small { Text = review.Name }, new Small { Text = review.Text, Tone = TextTone.Muted } },
            });
        }
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                new Flex
                {
                    Gap = Space.Lg, Wrap = FlexWrap.Wrap,
                    Children =
                    {
                        new Field { Label = "喜欢程度", Description = "Icon=Heart Tone=danger", Content = new Rating { Value = 3, Icon = Lucide.Heart, Tone = StatusTone.Danger } },
                        new Field { Label = "紧急程度", Description = "Count=3，Size=lg", Content = new Rating { Value = 2, Icon = Lucide.Flame, Tone = StatusTone.Accent, Count = 3, Size = TextSize.Lg } },
                        new Field { Label = "不可用", IsEnabled = false, Content = new Rating { Value = 4 } },
                    },
                },
                list,
            },
        };
    }
}
