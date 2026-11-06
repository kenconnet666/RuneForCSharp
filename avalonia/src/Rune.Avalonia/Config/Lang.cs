using System.Globalization;

namespace Rune.Avalonia;

public sealed partial record Lang
{
    /// <summary>
    /// 按界面文化选内置语言包：中文（zh-*）用 <see cref="ZhCn"/>，其余用 <see cref="EnUs"/>。
    /// 没有指定语言包的地方都按 <see cref="CultureInfo.CurrentUICulture"/> 取（TS 版固定默认中文，桌面应用跟随系统语言）。
    /// </summary>
    /// <param name="culture">界面文化。</param>
    public static Lang ForCulture(CultureInfo culture) => culture.TwoLetterISOLanguageName == "zh" ? ZhCn : EnUs;
}
