using System.Globalization;
using global::Avalonia;
using global::Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 作用域配置（vue-tsx-pro 的 Provider）：包住一片区域，里面的组件读这里的设置。样式与局部主题用 Avalonia 自己的
/// Styles、ThemeVariantScope（DESIGN §4），这里放平台没有的：语言包。
/// <code>
/// &lt;rune:Provider Lang="{x:Static rune:Lang.EnUs}"&gt;……&lt;/rune:Provider&gt;
/// &lt;Window rune:Provider.Lang="{x:Static rune:Lang.EnUs}"&gt;   &lt;!-- 也能直接挂在任何控件上 --&gt;
/// </code>
/// </summary>
public class Provider : Decorator
{
    /// <summary>语言包：沿逻辑树继承；哪里都没写时按界面文化取（<see cref="Rune.Avalonia.Lang.ForCulture"/>）。</summary>
    public static readonly AttachedProperty<Lang?> LangProperty = AvaloniaProperty.RegisterAttached<Provider, AvaloniaObject, Lang?>("Lang", inherits: true);

    /// <summary>这片区域的语言包。</summary>
    public Lang? Lang { get => GetValue(LangProperty); set => SetValue(LangProperty, value); }

    /// <summary>某个控件所在作用域的语言包；哪里都没写时按界面文化取。</summary>
    /// <param name="target">控件。</param>
    public static Lang GetLang(AvaloniaObject target) => target.GetValue(LangProperty) ?? Rune.Avalonia.Lang.ForCulture(CultureInfo.CurrentUICulture);

    /// <summary>给一个控件（及它里面的）设语言包。</summary>
    /// <param name="target">控件。</param>
    /// <param name="value">语言包；null 时沿用外面的。</param>
    public static void SetLang(AvaloniaObject target, Lang? value) => target.SetValue(LangProperty, value);
}
