using System.Windows.Input;

namespace Rune.Wpf;

/// <summary>平台的按键对上快捷键（<see cref="Hotkey"/> 的键名：字母、数字是单个字符，方向键是 ArrowUp……，回车是 Enter）。</summary>
static class HotkeyInput
{
    /// <summary>按下的键与修饰键是不是这个快捷键。</summary>
    /// <param name="hotkey">快捷键。</param>
    /// <param name="key">按下的键（Alt 组合时是 SystemKey）。</param>
    /// <param name="modifiers">按着的修饰键。</param>
    public static bool Matches(this Hotkey hotkey, Key key, ModifierKeys modifiers) =>
        hotkey.Matches(modifiers.HasFlag(ModifierKeys.Control), modifiers.HasFlag(ModifierKeys.Shift), modifiers.HasFlag(ModifierKeys.Alt), modifiers.HasFlag(ModifierKeys.Windows), Name(key));

    static string Name(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        Key.Up => "ArrowUp",
        Key.Down => "ArrowDown",
        Key.Left => "ArrowLeft",
        Key.Right => "ArrowRight",
        Key.Enter => "Enter",
        _ => key.ToString(),
    };
}
