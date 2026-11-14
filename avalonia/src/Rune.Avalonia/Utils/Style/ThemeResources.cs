using Avalonia;
using Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>在代码里取主题资源（token）：从控件所在的作用域往上、按控件当前的主题变体查，Provider 改了的按最近一层。</summary>
static class ThemeResources
{
    /// <summary>查一个资源；查不到（没合并 RuneTheme）或类型不对时取 <see cref="ThemeKeys.Fallback"/>，再没有就是 default。</summary>
    /// <param name="control">从这个控件往上查。</param>
    /// <param name="key">资源键（<see cref="ThemeKeys"/> 的常量）。</param>
    public static T Resource<T>(this Control control, string key) =>
        control.TryFindResource(key, control.ActualThemeVariant, out var value) && value is T typed ? typed
        : ThemeKeys.Fallback(key) is T fallback ? fallback : default!;

    /// <summary>
    /// 在 Render 里查资源的控件：资源变了就重画。Render 里查的资源不经过绑定，Avalonia 不会因为它们变了而重画；
    /// 换主题只触发 ActualThemeVariantChanged，作用域里改了 token（对应上游 sx 改 CSS 变量）只触发 ResourcesChanged，
    /// 后者沿逻辑树往下传，所以订阅在逻辑树里的 <paramref name="host"/> 上（自绘的部件常常只是视觉子元素）。
    /// </summary>
    /// <param name="target">要重画的控件。</param>
    /// <param name="host">在逻辑树里的组件。</param>
    public static void RedrawOnResourceChanges(this Visual target, StyledElement host)
    {
        host.ActualThemeVariantChanged += (_, _) => target.InvalidateVisual();
        host.ResourcesChanged += (_, _) => target.InvalidateVisual();
    }
}
