namespace Rune.Wpf;

/// <summary>
/// 文件是否符合 accept（vue-tsx-pro 的 matchesAccept）：<c>image/*</c>、<c>.pdf</c>、<c>application/json</c> 这样的逗号分隔列表，不写时都符合。
/// 浏览器的文件带 MIME 类型；桌面上的文件只有名字，类型按扩展名推（常见的图片、音视频、文档、压缩包），推不出的只能用扩展名规则匹配。
/// </summary>
public static class FileAccept
{
    static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".webp"] = "image/webp",
        [".bmp"] = "image/bmp", [".svg"] = "image/svg+xml", [".ico"] = "image/x-icon", [".avif"] = "image/avif", [".tif"] = "image/tiff", [".tiff"] = "image/tiff",
        [".mp4"] = "video/mp4", [".webm"] = "video/webm", [".mov"] = "video/quicktime", [".avi"] = "video/x-msvideo", [".mkv"] = "video/x-matroska",
        [".mp3"] = "audio/mpeg", [".wav"] = "audio/wav", [".ogg"] = "audio/ogg", [".flac"] = "audio/flac", [".m4a"] = "audio/mp4", [".aac"] = "audio/aac",
        [".txt"] = "text/plain", [".csv"] = "text/csv", [".md"] = "text/markdown", [".html"] = "text/html", [".htm"] = "text/html", [".css"] = "text/css", [".xml"] = "text/xml",
        [".pdf"] = "application/pdf", [".json"] = "application/json", [".zip"] = "application/zip", [".rtf"] = "application/rtf",
        [".doc"] = "application/msword", [".xls"] = "application/vnd.ms-excel", [".ppt"] = "application/vnd.ms-powerpoint",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    };

    /// <summary>按扩展名推的 MIME 类型；推不出时空字符串（同浏览器认不出的文件）。</summary>
    /// <param name="fileName">文件名。</param>
    public static string TypeOf(string fileName) => Types.GetValueOrDefault(Path.GetExtension(fileName), "");

    /// <summary>文件名是否符合 accept。</summary>
    /// <param name="fileName">文件名（带扩展名）。</param>
    /// <param name="accept">逗号分隔的规则；null 或空时都符合。</param>
    public static bool Matches(string fileName, string? accept)
    {
        var rules = (accept ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (rules.Length == 0) return true;
        var type = TypeOf(fileName);
        return rules.Any(rule => rule.StartsWith('.') ? fileName.EndsWith(rule, StringComparison.OrdinalIgnoreCase)
            : rule.EndsWith("/*", StringComparison.Ordinal) ? type.StartsWith(rule[..^1], StringComparison.OrdinalIgnoreCase)
            : type.Equals(rule, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>扩展名的规则（对话框的文件类型筛选用）：<c>.pdf</c> 原样，<c>image/*</c> 换成已知的图片扩展名，确切类型换成它的扩展名。</summary>
    /// <param name="accept">逗号分隔的规则。</param>
    public static IReadOnlyList<string> Extensions(string? accept) =>
        [.. (accept ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(rule => rule.StartsWith('.') ? [rule.ToLowerInvariant()] : Types.Where(pair => rule.EndsWith("/*", StringComparison.Ordinal)
                ? pair.Value.StartsWith(rule[..^1], StringComparison.OrdinalIgnoreCase)
                : pair.Value.Equals(rule, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key))
            .Distinct()];
}
