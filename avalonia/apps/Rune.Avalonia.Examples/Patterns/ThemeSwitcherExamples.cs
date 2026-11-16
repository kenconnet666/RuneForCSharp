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
        new("ThemeSwitcher", "customizer", "主题编辑器", "ThemeCustomizer 调品牌色、中性色、圆角、缩放、字体与动效，实时检查对比度，可以导出 XAML；设置交给 ThemeCustomization.Apply 套到某个元素上（这里只作用于右边的预览，套在窗口或 Provider 上就是整个应用）。", Customizer),
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

    static Control Customizer()
    {
        var editor = new ThemeCustomizer { Settings = new ThemeSettings(Accent: "teal", Radius: ThemeRadius.Large), Base = ThemeVariant.Light };
        var panel = new ScrollArea { Height = 26 * 16, Content = editor };
        AutomationProperties.SetName(panel, "主题设置");
        var preview = new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
                Children =
                {
                    new Strong { Text = "预览" },
                    new Small { Tone = TextTone.Muted, Text = "左边改的设置只作用于这块" },
                    new Progress { Value = 64, Label = "本月目标" },
                    new Switch { IsChecked = true, Content = "自动续费" },
                    new Flex { Gap = Space.Sm, Children = { new Button { Size = TextSize.Sm, Content = "确定" }, new Button { Size = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Content = "取消" } } },
                },
            },
        };
        // 设置只作用于预览：覆盖放进它的 Resources（对比度也读它）
        editor.ContrastTarget = preview;
        void Apply() => ThemeCustomization.Apply(preview, editor.Settings, ThemeVariant.Light);
        editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == ThemeCustomizer.SettingsProperty) Apply();
        };
        Apply();
        // 预览按亮色主题定制（同上游 customizeTheme(lightTheme, …)）：暗色页面里也是亮色的一块
        return new Grid { MinItemWidth = new Length("18rem"), Gap = Space.Lg, Children = { panel, new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Light, Child = preview } } };
    }
}
