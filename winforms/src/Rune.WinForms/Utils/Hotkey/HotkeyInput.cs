namespace Rune.WinForms;

/// <summary>平台的按键对上快捷键（<see cref="Hotkey"/> 的键名：字母、数字是单个字符，方向键是 ArrowUp……，回车是 Enter）。</summary>
static class HotkeyInput
{
    /// <summary>按下的键与修饰键是不是这个快捷键。</summary>
    /// <param name="hotkey">快捷键。</param>
    /// <param name="keyData">按键与修饰键。</param>
    /// <param name="win">按着 Win（Keys 里没有它）。</param>
    public static bool Matches(this Hotkey hotkey, Keys keyData, bool win = false) =>
        hotkey.Matches(keyData.HasFlag(Keys.Control), keyData.HasFlag(Keys.Shift), keyData.HasFlag(Keys.Alt), win, Name(keyData & Keys.KeyCode));

    static string Name(Keys code) => code switch
    {
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (code - Keys.D0))).ToString(),
        Keys.Up => "ArrowUp",
        Keys.Down => "ArrowDown",
        Keys.Left => "ArrowLeft",
        Keys.Right => "ArrowRight",
        Keys.Enter => "Enter",
        _ => code.ToString(),
    };
}
