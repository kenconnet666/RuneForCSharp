using Avalonia;
using Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 菜单数据的一项（vue-tsx-pro 的 NavMenuItem 与 NavBarItem）：同一份菜单数据可以同时给顶部的 <see cref="NavBar"/> 和侧栏的 <see cref="NavMenu"/>。
/// 有 Href 是链接，有 Children 是一组（NavBar 是下拉菜单，只认一层；NavMenu 可以多层）。
/// </summary>
/// <param name="Label">文字。</param>
public sealed record NavMenuItem(string Label)
{
    /// <summary>地址：等于导航的 CurrentHref 时是当前页。</summary>
    public string? Href { get; init; }

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get; init; }

    /// <summary>子项：有可显示的子项时这一项是一组。</summary>
    public IReadOnlyList<NavMenuItem>? Children { get; init; }

    /// <summary>计数角标（待办、未读），0 不显示。</summary>
    public int Badge { get; init; }

    /// <summary>不可用。</summary>
    public bool Disabled { get; init; }

    /// <summary>是否是当前页；不写时按 CurrentHref 判断（NavMenu 用；TS 的 active）。</summary>
    public bool? Active { get; init; }

    /// <summary>这一项（或它的子孙）有没有能显示的页面：没有的目录不画（后端返回的目录下没有该用户能看的页面时，目录自然消失）。</summary>
    public bool HasPages => Children is { Count: > 0 } children ? children.Any(child => child.HasPages) : Href is not null;
}

/// <summary>
/// 数据驱动的侧边导航（vue-tsx-pro 的 NavMenu）：菜单是一棵树（后端按角色返回、或配置文件里写的），直接交给 NavMenu 渲染，不用自己拼 NavItem。
/// <code>
/// menu.Items = [new("首页") { Href = "/", Icon = Lucide.House }, new("降本管理") { Children = [new("降本项目") { Href = "/projects", Badge = 3 }] }];
/// </code>
/// 本身就是 <see cref="NavList"/>（CurrentHref、Navigate、紧凑同 NavList）。有子项的项是一组：顶层的显示成带小标题、一直展开的小节（<see cref="TopLevel"/> 为 Section，默认）
/// 或可收起的分组（Group），更深的层级都是可收起的分组（含当前页的组自动展开）；没有子项的项是链接。没有 Href 又没有可显示子项的项不显示。
/// </summary>
public class NavMenu : NavList
{
    /// <summary>菜单树。</summary>
    public static readonly StyledProperty<IReadOnlyList<NavMenuItem>> ItemsProperty = AvaloniaProperty.Register<NavMenu, IReadOnlyList<NavMenuItem>>(nameof(Items), []);

    /// <summary>顶层有子项的项怎么显示，默认 Section。</summary>
    public static readonly StyledProperty<NavMenuTopLevel> TopLevelProperty = AvaloniaProperty.Register<NavMenu, NavMenuTopLevel>(nameof(TopLevel));

    /// <summary>菜单树。</summary>
    public IReadOnlyList<NavMenuItem> Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>顶层有子项的项怎么显示。</summary>
    public NavMenuTopLevel TopLevel { get => GetValue(TopLevelProperty); set => SetValue(TopLevelProperty, value); }

    /// <summary>NavList 的类型键：NavMenu 用 NavList 的主题。</summary>
    protected override Type StyleKeyOverride => typeof(NavList);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ItemsProperty && change.Property != TopLevelProperty) return;
        Children.Clear();
        foreach (var item in Items) if (Node(item, 0) is { } node) Children.Add(node);
    }

    Control? Node(NavMenuItem item, int depth)
    {
        if (!item.HasPages) return null;
        if (item.Children is not { Count: > 0 } children)
            return new NavItem { Href = item.Href, Icon = item.Icon, Badge = item.Badge, IsEnabled = !item.Disabled, Active = item.Active, Content = item.Label };
        var nodes = children.Select(child => Node(child, depth + 1)).OfType<Control>();
        if (depth == 0 && TopLevel == NavMenuTopLevel.Section)
        {
            var section = new NavSection { Label = item.Label, Icon = item.Icon };
            section.Items.AddRange(nodes);
            return section;
        }
        var group = new NavGroup { Label = item.Label, Icon = item.Icon };
        group.Items.AddRange(nodes);
        return group;
    }
}

/// <summary>NavMenu 顶层有子项的项怎么显示。</summary>
public enum NavMenuTopLevel
{
    /// <summary>带小标题的小节，一直展开（默认）。</summary>
    Section,

    /// <summary>可收起的分组。</summary>
    Group,
}
