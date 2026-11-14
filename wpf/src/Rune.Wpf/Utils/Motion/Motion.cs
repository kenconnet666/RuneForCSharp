using System.Windows;

namespace Rune.Wpf;

/// <summary>动效的全局开关与时长 token 的取法。</summary>
public static class Motion
{
    /// <summary>
    /// 系统是否允许动画（Windows“设置 → 辅助功能 → 视觉效果 → 动画效果”）。关闭时组件的过渡与动画立即到终值，
    /// 对应 vue-tsx-pro 里“减少动态效果”时时长 token 为 0。
    /// </summary>
    public static bool Enabled => SystemParameters.ClientAreaAnimation;

    /// <summary>
    /// 从元素所在作用域查时长 token：Provider 改了时长、局部关闭动效（设成 0）都按最近的一层生效。
    /// 系统关闭了动画、或时长不为正时返回 <see cref="TimeSpan.Zero"/>。
    /// </summary>
    /// <param name="element">从这个元素向上查资源。</param>
    /// <param name="key">时长 token 的资源键（<see cref="ThemeKeys.Duration"/>）。</param>
    public static TimeSpan Duration(FrameworkElement element, object key)
    {
        if (!Enabled) return TimeSpan.Zero;
        var value = element.Resource<TimeSpan>(key);
        return value > TimeSpan.Zero ? value : TimeSpan.Zero;
    }
}
