using System.Windows;

namespace Rune.Wpf;

/// <summary>
/// 外壳顶栏的品牌（vue-tsx-pro 的 AppShellBrand）：品牌色的标志与产品名，放在 <see cref="AppShell.Brand"/> 里。
/// <code>
/// &lt;rune:AppShellBrand Icon="{x:Static rune:Lucide.Box}" Command="{Binding GoHome}"&gt;采购协同&lt;/rune:AppShellBrand&gt;
/// </code>
/// 产品名就是按钮的名称；点了回首页（桌面上没有路由：TS 的 href 换成 Click / Command）。自己的标志图写在 <see cref="Logo"/>，写了时不画 <see cref="Icon"/>。
/// 侧栏收起成图标栏时品牌格只放得下标志：产品名不显示，仍是读屏的名称（外壳在品牌格上设 <see cref="IsCompactProperty"/>，往下继承）。
/// </summary>
public class AppShellBrand : ButtonBase
{
    /// <summary>标志的图标（accentText）。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(AppShellBrand),
        new FrameworkPropertyMetadata(null, (d, _) => ((AppShellBrand)d).UpdateMark()));

    /// <summary>标志的图片等，写了时不画图标。</summary>
    public static readonly DependencyProperty LogoProperty = DependencyProperty.Register(nameof(Logo), typeof(object), typeof(AppShellBrand),
        new FrameworkPropertyMetadata(null, (d, _) => ((AppShellBrand)d).UpdateMark()));

    /// <summary>只显示标志（侧栏收成了图标栏）：沿元素树继承，AppShell 设在品牌格上。</summary>
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.RegisterAttached("IsCompact", typeof(bool), typeof(AppShellBrand),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    static AppShellBrand() => DefaultStyleKeyProperty.OverrideMetadata(typeof(AppShellBrand), new FrameworkPropertyMetadata(typeof(AppShellBrand)));

    /// <summary>标志的图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>标志图。</summary>
    public object? Logo { get => GetValue(LogoProperty); set => SetValue(LogoProperty, value); }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateMark();
    }

    /// <summary>写了标志图时画它，否则画图标（有的话）。</summary>
    void UpdateMark()
    {
        if (GetTemplateChild("PART_Icon") is UIElement icon) icon.Visibility = Logo is null && Icon is not null ? Visibility.Visible : Visibility.Collapsed;
        if (GetTemplateChild("PART_Logo") is UIElement logo) logo.Visibility = Logo is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>是否只显示标志。</summary>
    public static bool GetIsCompact(DependencyObject element) => (bool)element.GetValue(IsCompactProperty);

    /// <summary>只显示标志。</summary>
    public static void SetIsCompact(DependencyObject element, bool value) => element.SetValue(IsCompactProperty, value);
}
