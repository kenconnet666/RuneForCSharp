using System.Windows;

namespace Rune.Wpf.Examples;

/// <summary>
/// Provider 页的示例（vue-tsx-pro 文档站 config/Provider）。局部默认值是 Provider.Styles 里的类型键样式（叠在外层之上，显式写的属性仍然优先），
/// 局部主题是子树 Resources 里再并一份 RuneTheme。locales、rtl、services 之后补。
/// </summary>
static class ProviderExamples
{
    public static Example[] All =>
    [
        new("Provider", "scoped", "局部主题与默认值", "Provider.Styles 里的类型键样式改这片区域里组件的默认外观（按钮、标记、提示条），显式写的属性仍然优先；子树的 Resources 并一份 RuneTheme { Theme = \"paper\" } 固定这一块的主题。默认值的优先级：控件上显式写的 > 最近一层 Provider > 应用资源 > 组件自己的默认值。", Scoped),
    ];

    /// <summary>同一组组件：按钮（一个显式写了样式）、标记、开关、提示条。</summary>
    static FrameworkElement Sample() => new Flex
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

    static FrameworkElement Scoped()
    {
        // 一处统一改组件的默认外观：样式里的值低于控件上显式写的
        var rounded = new Provider
        {
            Styles =
            {
                [typeof(Button)] = new Style(typeof(Button)) { Setters = { new Setter(Button.VariantProperty, ButtonVariant.Outline), new Setter(Button.RadiusProperty, Radius.Full) } },
                [typeof(Badge)] = new Style(typeof(Badge)) { Setters = { new Setter(Badge.VariantProperty, StatusVariant.Solid) } },
                [typeof(Alert)] = new Style(typeof(Alert)) { Setters = { new Setter(Alert.VariantProperty, AlertVariant.Outline) } },
            },
            Child = Panel("Provider.Styles：按钮、标记、提示条换了默认样式"),
        };
        var paper = Panel("RuneTheme：这一块固定用纸张主题");
        paper.Resources.MergedDictionaries.Add(new RuneTheme { Theme = "paper" });
        return new Grid { MinItemWidth = "16rem", Gap = Space.Md, Children = { Panel("外层：应用的主题与默认值"), rounded, paper } };
    }
}
