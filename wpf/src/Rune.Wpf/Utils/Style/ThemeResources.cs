using System.Windows;

namespace Rune.Wpf;

/// <summary>在代码里取主题资源（token）：从元素所在的作用域往上查，Provider 改了的按最近一层。</summary>
static class ThemeResources
{
    /// <summary>查一个资源；查不到（没合并 RuneTheme）或类型不对时取 <see cref="ThemeKeys.Fallback"/>，再没有就是 default。</summary>
    /// <param name="element">从这个元素往上查。</param>
    /// <param name="key">资源键（<see cref="ThemeKeys"/> 的常量；使用方给的键可以不是字符串，这时没有默认值）。</param>
    public static T Resource<T>(this FrameworkElement element, object key) =>
        element.TryFindResource(key) is T value ? value : key is string name && ThemeKeys.Fallback(name) is T fallback ? fallback : default!;
}
