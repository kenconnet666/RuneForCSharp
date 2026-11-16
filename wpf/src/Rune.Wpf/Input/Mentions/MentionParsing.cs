namespace Rune.Wpf;

/// <summary>拆开的一段文字：普通文字，或提及（带它的选项）。</summary>
/// <param name="Text">这一段的文字（提及时含 @）。</param>
/// <param name="Mention">提及的选项；普通文字为 null。</param>
public sealed record MentionSegment(string Text, ListboxOption? Mention = null);

/// <summary>
/// 提及的拆分（vue-tsx-pro 的 mentionSegments、mentionedValues）：Mentions、MentionText 都用它。三个库各一份（孪生文件）。
/// "@名字"里的名字是选项的显示文字（有多个能匹配时取最长的）；@ 前面是字母、数字、下划线、点时不算（邮箱 name@example.com）。
/// </summary>
public static class MentionParsing
{
    /// <summary>@ 前面是这个字符时不算提及（邮箱里的 @）。</summary>
    public static bool IsWordBefore(char character) => char.IsAsciiLetterOrDigit(character) || character is '_' or '.';

    /// <summary>把文字拆成普通文字与提及。</summary>
    public static IReadOnlyList<MentionSegment> Segments(string text, IEnumerable<ListboxOption> options, string prefix = "@")
    {
        var byLength = options.Where(option => option.Label.Length > 0).OrderByDescending(option => option.Label.Length).ToList();
        var segments = new List<MentionSegment>();
        var plain = new System.Text.StringBuilder();
        var index = 0;
        while (index < text.Length)
        {
            var mention = text.AsSpan(index).StartsWith(prefix, StringComparison.Ordinal) && (index == 0 || !IsWordBefore(text[index - 1]))
                ? byLength.FirstOrDefault(option => text.AsSpan(index + prefix.Length).StartsWith(option.Label, StringComparison.Ordinal))
                : null;
            if (mention is null)
            {
                plain.Append(text[index]);
                index++;
                continue;
            }
            if (plain.Length > 0) segments.Add(new MentionSegment(plain.ToString()));
            plain.Clear();
            var length = prefix.Length + mention.Label.Length;
            segments.Add(new MentionSegment(text.Substring(index, length), mention));
            index += length;
        }
        if (plain.Length > 0) segments.Add(new MentionSegment(plain.ToString()));
        return segments;
    }

    /// <summary>文字里提到了哪些选项（值，按第一次出现的顺序，不重复）。</summary>
    public static IReadOnlyList<string> MentionedValues(string text, IEnumerable<ListboxOption> options, string prefix = "@") =>
        [.. Segments(text, options, prefix).Where(segment => segment.Mention is not null).Select(segment => segment.Mention!.Value).Distinct()];
}
