using System.Net;
using System.Text;

namespace Rune.Avalonia;

/// <summary>一种格式的视频源（vue-tsx-pro 的 VideoSource）：内核从上到下取第一个能播的。</summary>
/// <param name="Src">地址。</param>
/// <param name="Type">格式（"video/webm"、"video/mp4; codecs=…"）：内核不认的跳过。</param>
/// <param name="Media">媒体条件（"(max-width: 40rem)"）：窄的时候换一个小的文件。</param>
public sealed record VideoSource(string Src, string? Type = null, string? Media = null);

/// <summary>一条文本轨道（vue-tsx-pro 的 VideoTrack）：字幕、说明、章节。</summary>
/// <param name="Src">WebVTT 文件的地址。</param>
/// <param name="SrcLang">语言（BCP 47："zh"、"en"）。</param>
/// <param name="Label">菜单里显示的名字。</param>
/// <param name="Kind">种类：subtitles（默认，对白的翻译）、captions（还包括音效说明）、descriptions、chapters、metadata。</param>
/// <param name="Default">默认打开这一条。</param>
public sealed record VideoTrack(string Src, string SrcLang, string Label, string Kind = "subtitles", bool Default = false);

/// <summary>Video 放进网页内核里的页面：一个铺满、黑底、按 contain 放画面的 video 元素（vue-tsx-pro 的 Video 渲染的那个元素）。</summary>
public static class VideoPage
{
    /// <summary>页面的 HTML。</summary>
    /// <param name="src">视频地址；有 sources 时不用。</param>
    /// <param name="sources">几种格式的视频源。</param>
    /// <param name="poster">封面。</param>
    /// <param name="tracks">文本轨道。</param>
    /// <param name="controls">显示内核的控制条。</param>
    /// <param name="autoplay">自动播放（调用方已按"减少动态效果"关掉）：同时静音、行内播放。</param>
    /// <param name="loop">循环。</param>
    /// <param name="muted">静音。</param>
    /// <param name="label">读屏念的名称（video 的 aria-label）。</param>
    public static string Html(string? src, IReadOnlyList<VideoSource>? sources, string? poster, IReadOnlyList<VideoTrack>? tracks,
        bool controls, bool autoplay, bool loop, bool muted, string? label)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><style>")
            .Append("html,body{margin:0;height:100%;background:#000;overflow:hidden}")
            .Append("video{display:block;width:100%;height:100%;object-fit:contain;background:#000}")
            .Append("</style></head><body><video preload=\"metadata\" playsinline");
        if (sources is not { Count: > 0 } && src is not null) Attribute(html, "src", src);
        if (poster is not null) Attribute(html, "poster", poster);
        if (label is not null) Attribute(html, "aria-label", label);
        if (controls) html.Append(" controls");
        // 自动播放要静音才被内核允许
        if (autoplay) html.Append(" autoplay muted");
        else if (muted) html.Append(" muted");
        if (loop) html.Append(" loop");
        html.Append('>');
        foreach (var source in sources ?? [])
        {
            html.Append("<source");
            Attribute(html, "src", source.Src);
            if (source.Type is not null) Attribute(html, "type", source.Type);
            if (source.Media is not null) Attribute(html, "media", source.Media);
            html.Append('>');
        }
        foreach (var track in tracks ?? [])
        {
            html.Append("<track");
            Attribute(html, "kind", track.Kind);
            Attribute(html, "src", track.Src);
            Attribute(html, "srclang", track.SrcLang);
            Attribute(html, "label", track.Label);
            if (track.Default) html.Append(" default");
            html.Append('>');
        }
        return html.Append("</video></body></html>").ToString();
    }

    static void Attribute(StringBuilder html, string name, string value) =>
        html.Append(' ').Append(name).Append("=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
}
