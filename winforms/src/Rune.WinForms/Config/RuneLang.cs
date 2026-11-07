using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 应用级的语言包（WinForms 没有可继承的附加属性，同 <see cref="RuneTheme"/> 是信号）：组件取文案时读 <see cref="Current"/>。
/// <code>
/// RuneLang.Current.Value = Lang.EnUs;
/// </code>
/// 这是最外层；局部的语言包写在 <see cref="ProviderControl.Lang"/>。
/// </summary>
public static class RuneLang
{
    /// <summary>当前的语言包，默认按界面文化取（<see cref="Lang.ForCulture"/>）。</summary>
    public static Signal<Lang> Current { get; } = new(Lang.ForCulture(CultureInfo.CurrentUICulture));
}
