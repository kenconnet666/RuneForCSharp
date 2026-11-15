using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Rune.Avalonia.Examples;

/// <summary>
/// Provider 页的示例（vue-tsx-pro 文档站 config/Provider）。Avalonia 的局部默认值是一层 Styles（样式的优先级低于本地值，显式写的属性仍然优先），
/// 局部主题是 ThemeVariantScope；Provider 自己只放平台没有的语言包、工作日历与权限判断（DESIGN §4）。locales、rtl、services 之后补。
/// </summary>
static class ProviderExamples
{
    public static Example[] All =>
    [
        new("Provider", "permissions", "按权限点显示", "权限由应用接入（这里用 Provider 的 Permissions 模拟）：Can 有权限才显示，没有时显示 Fallback；要自己判断时用 Provider.Can（批量操作要新建或删除任一权限）。换角色后换一个新的判断函数，按钮跟着变。", Permissions),
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

    static readonly Dictionary<string, string[]> Roles = new()
    {
        ["viewer"] = [],
        ["editor"] = ["cost:project:add"],
        ["admin"] = ["cost:project:add", "cost:project:remove"],
    };

    static Control Permissions()
    {
        var role = new ToggleGroup
        {
            IsRequired = true,
            Value = "editor",
            Size = TextSize.Sm,
            Children = { new ToggleItem { Value = "viewer", Content = "只读" }, new ToggleItem { Value = "editor", Content = "编辑" }, new ToggleItem { Value = "admin", Content = "管理员" } },
        };
        AutomationProperties.SetName(role, "当前用户的角色");
        var batch = new Button { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "批量操作" };
        var toolbar = new Flex
        {
            Gap = Space.Sm,
            Align = FlexAlign.Center,
            Wrap = true,
            Children =
            {
                new Can { Permission = "cost:project:add", Content = new Button { Icon = Lucide.Plus, Content = "新建" }, Fallback = new Small { Text = "没有新建权限", Tone = TextTone.Muted } },
                new Can { Permission = "cost:project:remove", Content = new Button { Icon = Lucide.Trash, Variant = ButtonVariant.Outline, Tone = StatusTone.Danger, Content = "删除" } },
                batch,
            },
        };
        var provider = new Provider { Child = toolbar };
        void Sync()
        {
            var granted = Roles[role.Value ?? "viewer"];
            provider.Permissions = permission => granted.Contains(permission);
            batch.IsEnabled = Provider.Can(batch, "cost:project:add", "cost:project:remove");
        }
        role.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleGroup.ValueProperty) Sync();
        };
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { role, provider } };
    }
}
