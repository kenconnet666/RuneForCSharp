namespace Rune.WinForms;

/// <summary>
/// 快捷键（vue-tsx-pro 的 hotkeys：'Mod+K'、'Ctrl+Shift+P'）：修饰键加一个键，Mod 在桌面上（Windows）是 Ctrl。
/// 解析写法、给出显示的文字（Kbd：Ctrl+K）与读屏的写法（aria-keyshortcuts：Control+K）；各平台把按键事件换成键名后用 <see cref="Matches"/> 比对。
/// </summary>
/// <param name="Ctrl">Ctrl（Mod）。</param>
/// <param name="Shift">Shift。</param>
/// <param name="Alt">Alt。</param>
/// <param name="Win">Win（Meta）。</param>
/// <param name="Key">键名：单个字母、数字大写（K、7），别的照写（Enter、Escape、ArrowUp、F2）。</param>
public sealed record Hotkey(bool Ctrl, bool Shift, bool Alt, bool Win, string Key)
{
    /// <summary>解析写法：修饰键（Mod、Ctrl、Control、Shift、Alt、Meta、Win、Cmd）与一个键用 + 连起来，不分大小写。</summary>
    /// <param name="combo">写法，如 Mod+K。</param>
    public static Hotkey Parse(string combo)
    {
        var parts = combo.Trim().Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new FormatException("快捷键是空的。");
        bool ctrl = false, shift = false, alt = false, win = false;
        foreach (var part in parts[..^1])
        {
            switch (part.ToLowerInvariant())
            {
                case "mod" or "ctrl" or "control": ctrl = true; break;
                case "shift": shift = true; break;
                case "alt" or "option": alt = true; break;
                case "meta" or "win" or "cmd" or "command": win = true; break;
                default: throw new FormatException($"不认识的修饰键 \"{part}\"（{combo}）。");
            }
        }
        var key = parts[^1];
        return new Hotkey(ctrl, shift, alt, win, key.Length == 1 ? key.ToUpperInvariant() : key);
    }

    /// <summary>显示的文字（Kbd 里）：Ctrl+Shift+K。</summary>
    public string Text => string.Join("+", Modifiers(("Ctrl", "Shift", "Alt", "Win")).Append(Key));

    /// <summary>读屏的写法（aria-keyshortcuts）：Control+Shift+K。</summary>
    public string AccessKey => string.Join("+", Modifiers(("Control", "Shift", "Alt", "Meta")).Append(Key));

    /// <summary>按下的键是不是它：修饰键要一样（多按了也不算），键名不分大小写。</summary>
    /// <param name="ctrl">按着 Ctrl。</param>
    /// <param name="shift">按着 Shift。</param>
    /// <param name="alt">按着 Alt。</param>
    /// <param name="win">按着 Win。</param>
    /// <param name="key">键名（平台的按键换成的，字母、数字是单个字符）。</param>
    public bool Matches(bool ctrl, bool shift, bool alt, bool win, string key) =>
        ctrl == Ctrl && shift == Shift && alt == Alt && win == Win && string.Equals(key, Key, StringComparison.OrdinalIgnoreCase);

    IEnumerable<string> Modifiers((string Ctrl, string Shift, string Alt, string Win) names)
    {
        if (Ctrl) yield return names.Ctrl;
        if (Shift) yield return names.Shift;
        if (Alt) yield return names.Alt;
        if (Win) yield return names.Win;
    }

    /// <inheritdoc />
    public override string ToString() => Text;
}
