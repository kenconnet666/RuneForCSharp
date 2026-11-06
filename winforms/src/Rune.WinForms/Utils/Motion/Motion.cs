using System.Runtime.InteropServices;

namespace Rune.WinForms;

/// <summary>动效的全局开关。</summary>
public static class Motion
{
    const uint GetClientAreaAnimation = 0x1042;

    /// <summary>
    /// 系统是否允许动画（Windows“设置 → 辅助功能 → 视觉效果 → 动画效果”，WPF 的 SystemParameters.ClientAreaAnimation
    /// 读的是同一个设置）。关闭时 <see cref="Animator"/> 直接写终值，对应 vue-tsx-pro 里“减少动态效果”时时长为 0。
    /// 读取失败时按允许处理。
    /// </summary>
    public static bool Enabled => !SystemParametersInfo(GetClientAreaAnimation, 0, out var enabled, 0) || enabled;

    // 用 DllImport 而不是 LibraryImport：后者要开 AllowUnsafeBlocks，为一个调用不值得
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);
}
