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
}
