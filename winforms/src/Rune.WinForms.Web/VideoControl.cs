using System.ComponentModel;
using Microsoft.Web.WebView2.WinForms;

namespace Rune.WinForms;

/// <summary>
/// 视频（vue-tsx-pro 的 Video）：网页内核里的 video 元素——内核自己的控制条，加上固定的宽高比（加载前就占好位置）、封面、多种格式、字幕轨道。
/// <code>
/// var demo = new VideoControl { Source = new Uri("https://example.com/demo.mp4"), Poster = new Uri("https://example.com/cover.jpg"), AccessibleName = "产品演示" };
/// var custom = new VideoControl { Source = demoUrl, ControlBar = VideoControlBar.Custom, AccessibleName = "产品演示" };   // 自绘的控制条：倍速、字幕、全屏
/// var loop = new VideoControl { Source = loopUrl, Autoplay = true, Loop = true, ControlBar = VideoControlBar.None, AccessibleName = "背景动画" };
/// </code>
/// <list type="bullet">
/// <item>自动播放：默认静音（内核不允许带声音的自动播放）；系统关掉动画（减少动态效果）时不自动播放，并显示控制条。</item>
/// <item>画面按 contain 放进框里（不裁切），四周黑底、md 圆角。<see cref="WebShellControl.Ratio"/> 为 null 时不定比例，自己给 Height。</item>
/// <item>多种格式用 <see cref="Sources"/>（内核取第一个能播的），字幕用 <see cref="Tracks"/>；默认只预加载元数据。</item>
/// <item>本地文件也能播：页面写在临时文件夹里再打开（直接给内核一段 HTML 时，它不让页面读本地文件）。</item>
/// </list>
/// <item><see cref="ControlBar"/> 为 Custom 时是自绘的控制条（同上游 controls="custom"，画在页面里）：播放 / 暂停、时间与进度、静音与音量、字幕菜单（有 Tracks 时）、
/// 倍速菜单（<see cref="Speeds"/>）、全屏；播放中鼠标离开画面或 2.5 秒不动收起；点画面切换播放；键盘 Space / K、← →、↑ ↓、M、F。颜色、文案取页面加载时的主题与语言包。</item>
/// TS 的 controls 在这里叫 ControlBar（Control.Controls 是子控件集合）。
/// </summary>
[ToolboxItem(true)]
[Description("视频：网页内核的 video，定比例的黑底框。")]
public class VideoControl : WebShellControl
{
    // 这个实例的页面文件：换内容时覆盖，窗口销毁时删掉
    readonly string page = Path.Combine(Path.GetTempPath(), "Rune.Video", $"{Guid.NewGuid():N}.html");

    /// <summary>视频地址；有几种格式时用 Sources。</summary>
    [Category("Behavior")]
    [Description("视频地址；有几种格式时用 Sources。")]
    [DefaultValue(null)]
    public Uri? Source { get; set => Change(ref field, value); }

    /// <summary>几种格式的视频源（写了它就不用 Source）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<VideoSource>? Sources { get; set => Change(ref field, value); }

    /// <summary>封面：开始播放前显示。</summary>
    [Category("Appearance")]
    [Description("封面：开始播放前显示。")]
    [DefaultValue(null)]
    public Uri? Poster { get; set => Change(ref field, value); }

    /// <summary>文本轨道：字幕、说明。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<VideoTrack>? Tracks { get; set => Change(ref field, value); }

    /// <summary>控制条：内核自己的（默认）、自绘的、不要（自动播放的装饰视频；减少动态效果时仍显示内核的）。</summary>
    [Category("Appearance")]
    [Description("控制条：内核自己的、自绘的、不要。")]
    [DefaultValue(VideoControlBar.Native)]
    public VideoControlBar ControlBar { get; set => Change(ref field, value); }

    /// <summary>倍速菜单里的选项（自绘控制条），默认 0.5、0.75、1、1.25、1.5、2。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<double>? Speeds { get; set => Change(ref field, value); }

    /// <summary>自动播放：静音；减少动态效果时不自动播放。</summary>
    [Category("Behavior")]
    [Description("自动播放（静音）；减少动态效果时不自动播放。")]
    [DefaultValue(false)]
    public bool Autoplay { get; set => Change(ref field, value); }

    /// <summary>循环播放。</summary>
    [Category("Behavior")]
    [Description("循环播放。")]
    [DefaultValue(false)]
    public bool Loop { get; set => Change(ref field, value); }

    /// <summary>静音。</summary>
    [Category("Behavior")]
    [Description("静音。")]
    [DefaultValue(false)]
    public bool Muted { get; set => Change(ref field, value); }

    /// <summary>放进内核的页面（测试用）。</summary>
    internal string Html
    {
        get
        {
            // 减少动态效果：不自动播放，显示控制条，交给人自己点
            var plays = Autoplay && Motion.Enabled;
            var bar = ControlBar == VideoControlBar.None && Autoplay && !plays ? VideoControlBar.Native : ControlBar;
            return VideoPage.Html(Source?.AbsoluteUri, Sources, Poster?.AbsoluteUri, Tracks, bar, plays, Loop, Muted,
                string.IsNullOrEmpty(AccessibleName) ? null : AccessibleName, bar == VideoControlBar.Custom ? Chrome() : null);
        }
    }

    /// <summary>自绘控制条的外观与文案：取自所在作用域的主题、语言包；字号是 96 DPI 下的像素（网页内核自己按 DPI 放大）。</summary>
    VideoChrome Chrome()
    {
        var colors = ProviderControl.ThemeOf(this).Colors;
        static string Css(Color color) => VideoChrome.Css(color.A, color.R, color.G, color.B);
        return new VideoChrome(ProviderControl.LangOf(this), Font.SizeInPoints * 96 / 72, Speeds ?? VideoChrome.DefaultSpeeds,
            Css(colors.OnScrim), Css(colors.ScrimStrong), Css(colors.SurfaceRaised), Css(colors.Text), Css(colors.Border), Css(colors.FillHover), Css(colors.FocusRing));
    }

    void Change<T>(ref T field, T value)
    {
        field = value;
        Reload();
    }

    /// <inheritdoc />
    protected override void Load(WebView2 view)
    {
        if (Source is null && Sources is not { Count: > 0 }) return;
        Directory.CreateDirectory(Path.GetDirectoryName(page)!);
        File.WriteAllText(page, Html);
        view.Source = new Uri(page);
    }

    /// <inheritdoc />
    protected override (Color Fill, Color Border) FrameColors(ThemeColors colors) => (Color.Black, Color.Black);

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!File.Exists(page)) Reload();
    }

    /// <inheritdoc />
    protected override void OnHandleDestroyed(EventArgs e)
    {
        File.Delete(page);
        base.OnHandleDestroyed(e);
    }
}
