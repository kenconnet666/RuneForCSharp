namespace Rune.WinForms.Tests;

/// <summary>EmbedControl、VideoControl（可选包 Rune.WinForms.Web）：加载完之前骨架盖在内核上面、读屏名称、定比例；Video 的页面：自动播放必静音、Sources 代替 Src、字幕轨道、属性转义。</summary>
public class WebTests
{
    [Upstream("Video", "sources、tracks 渲染成")]
    [Upstream("Video", "autoplay：静音")]
    [Fact]
    public void Video_page_mutes_autoplay_and_lists_sources_and_tracks()
    {
        var html = VideoPage.Html("a.mp4", [new VideoSource("b.webm", "video/webm"), new VideoSource("c.mp4")], "cover.jpg",
            [new VideoTrack("zh.vtt", "zh", "中文", Default: true)], controls: true, autoplay: true, loop: false, muted: false, label: "演示 \"1\"");

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
        var html = VideoPage.Html("a.mp4", null, null, null, controls: false, autoplay: false, loop: true, muted: true, label: null);
        Assert.Contains("src=\"a.mp4\"", html);
        Assert.DoesNotContain(" controls", html);
        Assert.DoesNotContain("autoplay", html);
        Assert.Contains(" muted", html);
        Assert.Contains(" loop", html);
        Assert.Contains("preload=\"metadata\" playsinline", html);
    }

    // 不给地址：测试里不起浏览器进程，只看外壳
    [Upstream("Embed", "加载完之前有骨架")]
    [Fact]
    public Task Embed_covers_the_view_with_a_skeleton_and_is_named_by_its_title() => WinFormsUi.Run(() =>
    {
        var embed = new EmbedControl { Title = "月度报表", Width = 320 };
        using var form = WinFormsUi.Show(embed);

        Assert.False(embed.PageReady);
        Assert.True(embed.Placeholder.Visible);
        Assert.Equal(0, embed.Controls.GetChildIndex(embed.Placeholder)); // 骨架在最上面
        Assert.Equal(AccessibleRole.Grouping, embed.AccessibilityObject.Role);
        Assert.Equal("月度报表", embed.AccessibilityObject.Name);
        Assert.Equal(new Size(320, 180), embed.GetPreferredSize(new Size(320, 0))); // 16 / 9 定比例

        embed.Ratio = null;
        Assert.False(embed.AutoSize); // 不定比例：高度自己给
    });

    [Upstream("Video", "减少动态效果")]
    [Fact]
    public Task Reduced_motion_never_autoplays() => WinFormsUi.Run(() =>
    {
        using var video = new VideoControl { Autoplay = true, ShowControls = false };
        Assert.Contains(Motion.Enabled ? "autoplay muted" : " controls", video.Html);
    });
}
