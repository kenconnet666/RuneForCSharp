using System.Runtime.InteropServices;

namespace Rune.Avalonia;

/// <summary>动效的全局开关。</summary>
public static partial class Motion
{
    const uint GetClientAreaAnimation = 0x1042;

    /// <summary>
    /// 系统是否允许动画（Windows"设置 → 辅助功能 → 视觉效果 → 动画效果"，同 WPF 的 SystemParameters.ClientAreaAnimation）。
    /// 关闭时组件的过渡与动画直接到终值，对应 vue-tsx-pro 里"减少动态效果"时时长 token 为 0。其他系统与读取失败时按允许处理。
    /// </summary>
    public static bool Enabled => !OperatingSystem.IsWindows() || !SystemParametersInfo(GetClientAreaAnimation, 0, out var enabled, 0) || enabled;

    /// <summary>系统关闭动画时为 0，否则原样。</summary>
    /// <param name="duration">时长 token。</param>
    public static TimeSpan Duration(TimeSpan duration) => Enabled ? duration : TimeSpan.Zero;

    /// <summary>
    /// 从控件所在作用域查时长 token（Provider 改了时长、局部关闭动效都按最近的一层）；系统关闭了动画时为 0。同 WPF 的 Motion.Duration。
    /// </summary>
    /// <param name="control">从这个控件往上查。</param>
    /// <param name="key">时长 token 的资源键。</param>
    /// <param name="fallback">查不到时的时长。</param>
    public static TimeSpan Duration(global::Avalonia.Controls.Control control, string key, TimeSpan fallback) => Duration(control.Resource(key, fallback));

    // 用 DllImport 而不是 LibraryImport：后者要开 AllowUnsafeBlocks，为一个调用不值得
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);
}
