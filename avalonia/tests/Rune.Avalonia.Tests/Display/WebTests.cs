using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>Embed、Video（可选包 Rune.Avalonia.Web）：加载完之前是骨架、读屏名称；Video 的页面：自动播放必静音、Sources 代替 Src、字幕轨道、属性转义。</summary>
public class WebTests
{
    [Upstream("Video", "sources、tracks 渲染成")]
    [Upstream("Video", "autoplay：静音")]
    [Fact]
    public void Video_page_mutes_autoplay_and_lists_sources_and_tracks()
    {
        var html = VideoPage.Html("a.mp4", [new VideoSource("b.webm", "video/webm"), new VideoSource("c.mp4")], "cover.jpg",
            [new VideoTrack("zh.vtt", "zh", "中文", Default: true)], controls: VideoControlBar.Native, autoplay: true, loop: false, muted: false, label: "演示 \"1\"");

        Assert.DoesNotContain("src=\"a.mp4\"", html); // 有 Sources 时不用 Src
        Assert.Contains("<source src=\"b.webm\" type=\"video/webm\">", html);
        Assert.Contains("autoplay muted", html);
        Assert.Contains(" controls", html);
        Assert.Contains("<track kind=\"subtitles\" src=\"zh.vtt\" srclang=\"zh\" label=\"中文\" default>", html);
        Assert.Contains("aria-label=\"演示 &quot;1&quot;\"", html);
        Assert.Contains("poster=\"cover.jpg\"", html);
    }

    [Upstream("Video", "默认：有控制条")]
    [Fact]
    public void Video_page_without_controls_or_autoplay()
    {
        var html = VideoPage.Html("a.mp4", null, null, null, controls: VideoControlBar.None, autoplay: false, loop: true, muted: true, label: null);
        Assert.Contains("src=\"a.mp4\"", html);
        Assert.DoesNotContain(" controls", html);
        Assert.DoesNotContain("autoplay", html);
        Assert.Contains(" muted", html);
        Assert.Contains(" loop", html);
        Assert.Contains("preload=\"metadata\" playsinline", html);
    }

    [Upstream("Embed", "加载完之前有骨架")]
    [Fact]
    public Task Embed_shows_a_skeleton_until_loaded_and_is_named_by_its_title() => HeadlessUi.Run(() =>
    {
        var embed = new Embed { Source = new Uri("https://example.com"), Title = "月度报表", Width = 320 };
        HeadlessUi.Show(embed);

        Assert.False(embed.PageReady);
        Assert.True(embed.Placeholder.IsVisible);
        Assert.Equal("月度报表", AutomationProperties.GetName(embed));
        Assert.Equal(180, embed.DesiredSize.Height, 1); // 16 / 9 定比例

        var free = new Embed { Ratio = null, Height = 100, Width = 320 };
        HeadlessUi.Show(free);
        Assert.Equal(100, free.DesiredSize.Height, 1); // 不定比例：高度取 Height
    });

    [Fact]
    public void Html_data_addresses_load_as_strings()
    {
        Assert.Equal("<b>a&b</b>", Embed.InlineHtml(new Uri("data:text/html;charset=utf-8," + Uri.EscapeDataString("<b>a&b</b>"))));
        Assert.Equal("<b>中</b>", Embed.InlineHtml(new Uri("data:text/html;base64," + Convert.ToBase64String("<b>中</b>"u8.ToArray()))));
        Assert.Equal("hi", Embed.InlineHtml(new Uri("data:,hi")));
        Assert.Null(Embed.InlineHtml(new Uri("data:image/png;base64,AAAA")));
        Assert.Null(Embed.InlineHtml(new Uri("https://example.com")));
    }

    [Upstream("Video", "减少动态效果")]
    [Fact]
    public Task Reduced_motion_never_autoplays() => HeadlessUi.Run(() =>
    {
        var video = new Video { Source = new Uri("https://example.com/a.mp4"), Autoplay = true, ControlBar = VideoControlBar.None };
        Assert.Contains(Motion.Enabled ? "autoplay muted" : " controls", video.Html);
    });

    [Fact]
    [Upstream("Video", "controls=")]
    public void Custom_bar_replaces_the_native_controls_with_themed_buttons_and_menus()
    {
        var chrome = new VideoChrome(Lang.ZhCn, 14, [1, 1.5], "rgba(255,255,255,1)", "rgba(0,0,0,0.6)", "#fff", "#111", "#ddd", "#eee", "#33f");
        var html = VideoPage.Html("a.mp4", null, null, [new VideoTrack("zh.vtt", "zh", "中文")], VideoControlBar.Custom, autoplay: false, loop: false, muted: false, label: "演示", chrome);

        Assert.DoesNotContain(" controls", html); // 不要内核的控制条
        Assert.Contains("aria-label=\"播放\"", html);
        Assert.Contains("aria-label=\"播放进度\"", html);
        Assert.Contains("data-kind=\"captions\"", html); // 有字幕轨道时才有字幕菜单
        Assert.Contains("data-value=\"1.5\"", html); // 倍速菜单的选项
        Assert.Contains("--on:rgba(255,255,255,1)", html);
        Assert.DoesNotContain("data-kind=\"captions\"", VideoPage.Html("a.mp4", null, null, null, VideoControlBar.Custom, false, false, false, null, chrome));
        Assert.Throws<ArgumentNullException>(() => VideoPage.Html("a.mp4", null, null, null, VideoControlBar.Custom, false, false, false, null));
        Assert.Equal("rgba(10,20,30,0.502)", VideoChrome.Css(128, 10, 20, 30));
    }
}
