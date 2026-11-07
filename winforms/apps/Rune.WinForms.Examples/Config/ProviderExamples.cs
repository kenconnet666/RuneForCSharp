namespace Rune.WinForms.Examples;

/// <summary>ProviderControl 的示例（vue-tsx-pro 文档站 config/Provider）。locales、rtl、services 之后补；上游示例里的 Alert 还没做。</summary>
static class ProviderExamples
{
    public static Example[] All =>
    [
        new("Provider", "scoped", "局部主题与默认值", "ProviderControl 是一片作用域：Defaults 按组件给默认值（ButtonDefaults、BadgeDefaults、ChoiceDefaults……），Theme 固定这一块的主题；里面的组件沿 Parent 链取最近的一层，实例上显式写的属性仍然优先。", Scoped),
    ];

    /// <summary>同一组组件：按钮（一个显式写了样式）、标记、开关。</summary>
    static Control Sample() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm },
        Build.Flex(new FlexControl { Gap = Space.Sm, Wrap = true },
            new ButtonControl { Text = "保存", Icon = Lucide.Check },
            new ButtonControl { Text = "取消（显式写的样式）", Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral }),
        Build.Flex(new FlexControl { Gap = Space.Sm, Align = FlexAlign.Center },
            new BadgeControl { Text = "已发货", Tone = StatusTone.Success },
            new SwitchControl { Text = "接收通知", IsChecked = true }));

    static Control Panel(string caption) => Build.Card(new CardControl { CardPadding = Space.Sm },
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm }, new SmallControl { Text = caption, Tone = TextTone.Muted }, Sample()));

    /// <summary>作用域控件按内容高度：里面的卡片停靠在顶上。</summary>
    static ProviderControl Scope(ProviderControl scope, Control content)
    {
        scope.AutoSize = true;
        scope.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        content.Dock = DockStyle.Top;
        scope.Controls.Add(content);
        return scope;
    }

    static Control Scoped()
    {
        var rounded = new ProviderControl();
        // 一处统一改组件的默认外观：显式写的属性仍然优先
        rounded.Defaults.Set(new ButtonDefaults { Variant = ButtonVariant.Outline, Radius = Radius.Full });
        rounded.Defaults.Set(new BadgeDefaults { Variant = StatusVariant.Solid });
        rounded.Defaults.Set(new ChoiceDefaults { Tone = StatusTone.Success });
        return Build.Grid(new GridControl { MinItemWidth = "16rem", Gap = Space.Md },
            Panel("外层：应用的主题与默认值"),
            Scope(rounded, Panel("Defaults：按钮、标记、开关换了默认样式")),
            Scope(new ProviderControl { Theme = BuiltinThemes.Paper }, Panel("Theme：这一块固定用纸张主题")));
    }
}
