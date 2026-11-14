namespace Rune.WinForms;

/// <summary>
/// 名字的首字母（vue-tsx-pro 的 initials）：中文取最后一个字（通常是名），其他语言取第一个与最后一个词的首字母，大写；空白名字是空串。
/// 三个库各有一份，除命名空间外逐字相同（tools/twins）。
/// </summary>
static class AvatarInitials
{
    public static string Of(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) return "";
        // 中日韩统一表意文字（扩展 A 到基本区，同 TS 的 [㐀-鿿]）
        if (trimmed.Any(c => c is >= '㐀' and <= '鿿')) return Last(trimmed);
        var words = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return (First(words[0]) + (words.Length > 1 ? First(words[^1]) : "")).ToUpperInvariant();
    }

    // 按码点取：表情、扩展区的汉字是两个 UTF-16 码元，只取一个会显示成乱码
    static string First(string word) => word.Length > 1 && char.IsSurrogatePair(word[0], word[1]) ? word[..2] : word[..1];

    static string Last(string text) => text.Length > 1 && char.IsSurrogatePair(text[^2], text[^1]) ? text[^2..] : text[^1..];
}
