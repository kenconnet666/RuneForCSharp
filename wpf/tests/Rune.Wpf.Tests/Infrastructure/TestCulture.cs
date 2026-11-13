using System.Globalization;
using System.Runtime.CompilerServices;

namespace Rune.Wpf.Tests;

/// <summary>
/// 测试按中文语言包写（同上游的默认语言），而组件库没指定语言包时跟随系统语言：整个测试程序集一载入就按 zh-CN 跑，
/// 不然在英文系统上（CI 的 Windows 机器）读屏名称、提示文字都成了英文。
/// </summary>
static class TestCulture
{
    [ModuleInitializer]
    internal static void UseChinese()
    {
        var chinese = CultureInfo.GetCultureInfo("zh-CN");
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = chinese;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = chinese;
    }
}
