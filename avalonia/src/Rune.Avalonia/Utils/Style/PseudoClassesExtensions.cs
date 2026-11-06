using Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 档位（Variant、Tone、Size）映射成伪类：<c>Variant="soft"</c> → <c>:soft</c>。
/// 伪类留给组件，Classes 留给使用方；ControlTheme 里写 <c>^:soft</c>，使用方覆盖时写 <c>rune|Button:soft</c>。
/// </summary>
public static class PseudoClassesExtensions
{
    /// <summary>
    /// 换一个档位的伪类：先加新的再去旧的。反过来会先回落到不带档位的基础样式，属性多变一次（多一次通知与重绘）。
    /// </summary>
    /// <param name="classes">控件的 PseudoClasses。</param>
    /// <param name="oldValue">旧的档位值（不带冒号），null 表示原来没有。</param>
    /// <param name="newValue">新的档位值（不带冒号），null 表示不再有。</param>
    public static void Swap(this IPseudoClasses classes, string? oldValue, string? newValue)
    {
        if (oldValue == newValue) return;
        if (newValue is not null) classes.Set(":" + newValue, true);
        if (oldValue is not null) classes.Set(":" + oldValue, false);
    }
}
