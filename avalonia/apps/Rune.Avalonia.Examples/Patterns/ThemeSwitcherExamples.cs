using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Rune.Avalonia.Examples;

/// <summary>
/// ThemeSwitcher 页的示例（vue-tsx-pro 文档站 patterns/ThemeSwitcher）。上游示例里的主题偏好不记进 localStorage、只作用于下面一块：
/// 这里切换器不设到应用（AppliesToApplication = false），选择交给一个 ThemeVariantScope。customizer 要等 ThemeCustomizer。
/// </summary>
static class ThemeSwitcherExamples
{
    public static Example[] All =>
    [
        new("ThemeSwitcher", "scoped", "只作用于一块的主题切换", "图标按钮弹出菜单：跟随系统、亮色、暗色、纸张；按钮的图标表示当前的选择。这里不设到应用，Value 交给下面的 ThemeVariantScope。", Scoped),
        new("ThemeSwitcher", "gallery", "选主题的卡片", "一组单选卡片，每张是 ThemeThumbnail：用那套主题的底色、表面、文字与品牌色画的缩略图（设置页、引导流程）。", Gallery),
    ];

    static Control Scoped()
    {
        var switcher = new ThemeSwitcher { Themes = [ThemeVariant.Light, ThemeVariant.Dark, RuneThemeVariants.Paper], AppliesToApplication = false };
        var current = new Small { Tone = TextTone.Muted };
        var card = new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column,
                Gap = Space.Md,
                Children =
                {
                    new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new Strong { Text = "通知设置" }, new Small { Text = "这块区域用的是上面选的主题", Tone = TextTone.Muted } } },
                    new Switch { IsChecked = true, Content = "新评论时提醒我" },
                    new Flex { Gap = Space.Sm, Children = { new Button { Size = TextSize.Sm, Icon = Lucide.Bell, Content = "保存" }, new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral, Content = "取消" } } },
                },
            },
        };
        var scope = new ThemeVariantScope { Child = card, HorizontalAlignment = HorizontalAlignment.Left };
        void Sync()
        {
            scope.RequestedThemeVariant = switcher.Value;
            current.Text = "当前：" + ThemeSwitcher.BuiltinLabel(Lang.ZhCn, switcher.Value);
        }
        switcher.PropertyChanged += (_, e) =>
        {
            if (e.Property == ThemeSwitcher.ValueProperty) Sync();
        };
        Sync();
        return new Flex
        {
            Direction = FlexDirection.Column,
            Gap = Space.Md,
            Align = FlexAlign.Start,
            Children = { new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { switcher, current } }, scope },
        };
    }

    static Control Gallery()
    {
        var lang = Lang.ZhCn;
        var group = new RadioGroup { Card = true, Orientation = Orientation.Horizontal, MinItemWidth = Length.Parse("10rem"), Value = RuneThemeVariants.Paper.Key.ToString() };
        AutomationProperties.SetName(group, "选择主题");
        foreach (var variant in RuneThemeVariants.All)
        {
            var key = variant.Key.ToString() ?? "";
            group.Items.Add(new Radio
            {
                Value = key,
                Content = new Flex
                {
                    Direction = FlexDirection.Column,
                    Gap = Space.Xs,
                    Children = { new ThemeThumbnail { ThemeVariant = variant }, new Small { Text = ThemeSwitcher.BuiltinLabel(lang, variant) ?? key } },
                },
            });
        }
        return group;
    }
}
