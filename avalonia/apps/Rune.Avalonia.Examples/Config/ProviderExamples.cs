using Avalonia.Controls;
using Avalonia.Styling;

namespace Rune.Avalonia.Examples;

/// <summary>
/// Provider 页的示例（vue-tsx-pro 文档站 config/Provider）。Avalonia 的局部默认值是一层 Styles（样式的优先级低于本地值，显式写的属性仍然优先），
/// 局部主题是 ThemeVariantScope；Provider 自己只放平台没有的语言包与工作日历（DESIGN §4）。locales、rtl、services 之后补。
/// </summary>
static class ProviderExamples
{
    public static Example[] All =>
    [
        new("Provider", "scoped", "局部主题与默认值", "一层 Styles 改这片区域里组件的默认外观（按钮、标记、提示条），显式写的属性仍然优先；ThemeVariantScope 固定这一块的主题（RuneThemeVariants.Paper）。默认值的优先级：控件上显式写的 > 最近一层样式 > 应用的样式 > 组件自己的默认值。", Scoped),
    ];

    /// <summary>同一组组件：按钮（一个显式写了样式）、标记、开关、提示条。</summary>
    static Control Sample() => new Flex
    {
        Direction = FlexDirection.Column, Gap = Space.Sm,
        Children =
        {
            new Flex
            {
                Gap = Space.Sm, Wrap = true,
                Children = { new Button { Icon = Lucide.Check, Content = "保存" }, new Button { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "取消（显式写的 Variant）" } },
            },
            new Flex { Gap = Space.Sm, Align = FlexAlign.Center, Children = { new Badge { Tone = StatusTone.Success, Content = "已发货" }, new Switch { IsChecked = true, Content = "接收通知" } } },
            new Alert { Tone = StatusTone.Info, Icon = Lucide.Bell, Title = "新版本可用" },
        },
    };

    static Card Panel(string caption) => new()
    {
        Padding = Space.Sm,
        Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { new Small { Text = caption, Tone = TextTone.Muted }, Sample() } },
    };

    static Control Scoped()
    {
        // 一处统一改组件的默认外观：样式里的值低于控件上显式写的
        var rounded = Panel("Styles：按钮、标记、提示条换了默认样式");
        rounded.Styles.Add(new Style(x => x.OfType<Button>()) { Setters = { new Setter(Button.VariantProperty, ButtonVariant.Outline), new Setter(Button.RadiusProperty, Radius.Full) } });
        rounded.Styles.Add(new Style(x => x.OfType<Badge>()) { Setters = { new Setter(Badge.VariantProperty, StatusVariant.Solid) } });
        rounded.Styles.Add(new Style(x => x.OfType<Alert>()) { Setters = { new Setter(Alert.VariantProperty, AlertVariant.Outline) } });
        var paper = new ThemeVariantScope { RequestedThemeVariant = RuneThemeVariants.Paper, Child = Panel("ThemeVariantScope：这一块固定用纸张主题") };
        return new Grid { MinItemWidth = "16rem", Gap = Space.Md, Children = { Panel("外层：应用的主题与默认值"), rounded, paper } };
    }
}
