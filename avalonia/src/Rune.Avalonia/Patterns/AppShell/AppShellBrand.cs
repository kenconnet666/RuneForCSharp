using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;

namespace Rune.Avalonia;

/// <summary>
/// 外壳顶栏的品牌（vue-tsx-pro 的 AppShellBrand）：品牌色的标志与产品名，放在 <see cref="AppShell.Brand"/> 里。
/// <code>
/// &lt;rune:AppShellBrand Icon="{x:Static rune:Lucide.Box}" Command="{Binding GoHome}"&gt;采购协同&lt;/rune:AppShellBrand&gt;
/// </code>
/// 产品名就是按钮的名称；点了回首页（桌面上没有路由：TS 的 href 换成 Click / Command）。自己的标志图写在 <see cref="Logo"/>，写了时不画 <see cref="Icon"/>。
/// 侧栏收起成图标栏时品牌格只放得下标志：产品名不显示，仍是读屏的名称（<c>rune|AppShell:rail</c> 的样式）。
/// 部件（模板里的名字，见 <see cref="AppShellBrandParts"/>）：标志图标、标志图、产品名。
/// </summary>
[TemplatePart(AppShellBrandParts.Icon, typeof(Icon))]
[TemplatePart(AppShellBrandParts.Logo, typeof(ContentPresenter))]
public class AppShellBrand : ButtonBase
{
    /// <summary>标志的图标（accentText）。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<AppShellBrand, IconData?>(nameof(Icon));

    /// <summary>标志的图片等，写了时不画图标。</summary>
    public static readonly StyledProperty<object?> LogoProperty = AvaloniaProperty.Register<AppShellBrand, object?>(nameof(Logo));

    Icon? icon;
    ContentPresenter? logo;

    /// <summary>标志的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>标志图。</summary>
    public object? Logo { get => GetValue(LogoProperty); set => SetValue(LogoProperty, value); }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        icon = e.NameScope.Find<Icon>(AppShellBrandParts.Icon);
        logo = e.NameScope.Find<ContentPresenter>(AppShellBrandParts.Logo);
        UpdateMark();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty || change.Property == LogoProperty) UpdateMark();
    }

    void UpdateMark()
    {
        icon?.IsVisible = Logo is null && Icon is not null;
        logo?.IsVisible = Logo is not null;
    }
}

/// <summary>品牌的部件名：样式里用 <c>rune|AppShellBrand /template/ rune|Icon#PART_Icon</c> 选中。</summary>
public static class AppShellBrandParts
{
    /// <summary>标志的图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>标志图。</summary>
    public const string Logo = "PART_Logo";

    /// <summary>产品名。</summary>
    public const string Name = "PART_ContentPresenter";
}
