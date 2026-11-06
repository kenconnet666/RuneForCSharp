using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 应用级的语言包（WinForms 没有可继承的附加属性，同 <see cref="RuneTheme"/> 是信号）：组件取文案时读 <see cref="Current"/>。
/// <code>
/// RuneLang.Current.Value = Lang.EnUs;
/// </code>
/// 局部的语言包（Provider 作用域）做 Provider 时再加。
/// </summary>
public static class RuneLang
{
    /// <summary>当前的语言包，默认按界面文化取（<see cref="Lang.ForCulture"/>）。</summary>
    public static Signal<Lang> Current { get; } = new(Lang.ForCulture(CultureInfo.CurrentUICulture));
}
