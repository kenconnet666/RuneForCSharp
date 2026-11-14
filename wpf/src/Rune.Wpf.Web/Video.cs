using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;

namespace Rune.Wpf;

/// <summary>
/// 视频（vue-tsx-pro 的 Video）：网页内核里的 video 元素——内核自己的控制条，加上固定的宽高比（加载前就占好位置）、封面、多种格式、字幕轨道。
/// <code>
/// &lt;rune:Video Source="https://example.com/demo.mp4" Poster="https://example.com/cover.jpg" AutomationProperties.Name="产品演示" /&gt;
/// &lt;rune:Video Source="{Binding Demo}" ControlBar="Custom" AutomationProperties.Name="产品演示" /&gt;      自绘的控制条：倍速、字幕、全屏
/// &lt;rune:Video Source="{Binding Loop}" Autoplay="True" Loop="True" ControlBar="None" AutomationProperties.Name="背景动画" /&gt;
/// </code>
/// <list type="bullet">
/// <item>自动播放：默认静音（内核不允许带声音的自动播放）；系统关掉动画（减少动态效果）时不自动播放，并显示控制条。</item>
/// <item>画面按 contain 放进框里（不裁切），四周黑底、md 圆角。<see cref="WebShell.Ratio"/> 写 null 时不定比例，自己给 Height。</item>
/// <item>多种格式用 <see cref="Sources"/>（内核取第一个能播的），字幕用 <see cref="Tracks"/>；默认只预加载元数据。</item>
/// <item>本地文件也能播：页面写在临时文件夹里再打开（直接给内核一段 HTML 时，它不让页面读本地文件）。</item>
/// </list>
/// <item><see cref="ControlBar"/> 为 Custom 时是自绘的控制条（同上游 controls="custom"，画在页面里）：播放 / 暂停、时间与进度、静音与音量、字幕菜单（有 Tracks 时）、
/// 倍速菜单（<see cref="Speeds"/>）、全屏；播放中鼠标离开画面或 2.5 秒不动收起，键盘聚焦、菜单开着、鼠标停在控制条上时留着；点画面切换播放；
/// 键盘 Space / K、← →（±5 秒）、↑ ↓（音量）、M、F。颜色、文案取页面加载时的主题与语言包。</item>
/// 内核是原生窗口，边角是直的。
/// </summary>
public class Video : WebShell
{
    /// <summary>视频地址；有几种格式时用 Sources。</summary>
    public static readonly DependencyProperty SourceProperty = Register(nameof(Source), typeof(Uri), null);

    /// <summary>几种格式的视频源（写了它就不用 Source）。</summary>
    public static readonly DependencyProperty SourcesProperty = Register(nameof(Sources), typeof(IReadOnlyList<VideoSource>), null);

    /// <summary>封面：开始播放前显示。</summary>
    public static readonly DependencyProperty PosterProperty = Register(nameof(Poster), typeof(Uri), null);

    /// <summary>文本轨道：字幕、说明。</summary>
    public static readonly DependencyProperty TracksProperty = Register(nameof(Tracks), typeof(IReadOnlyList<VideoTrack>), null);

    /// <summary>控制条：内核自己的（默认）、自绘的、不要（自动播放的装饰视频；减少动态效果时仍显示内核的）。</summary>
    public static readonly DependencyProperty ControlBarProperty = Register(nameof(ControlBar), typeof(VideoControlBar), VideoControlBar.Native);

    /// <summary>倍速菜单里的选项（自绘控制条），默认 0.5、0.75、1、1.25、1.5、2。</summary>
    public static readonly DependencyProperty SpeedsProperty = Register(nameof(Speeds), typeof(IReadOnlyList<double>), null);

    /// <summary>自动播放：静音；减少动态效果时不自动播放。</summary>
    public static readonly DependencyProperty AutoplayProperty = Register(nameof(Autoplay), typeof(bool), false);

    /// <summary>循环播放。</summary>
    public static readonly DependencyProperty LoopProperty = Register(nameof(Loop), typeof(bool), false);

    /// <summary>静音。</summary>
    public static readonly DependencyProperty MutedProperty = Register(nameof(Muted), typeof(bool), false);

    // 这个实例的页面文件：换内容时覆盖，卸下时删掉
    readonly string page = Path.Combine(Path.GetTempPath(), "Rune.Video", $"{Guid.NewGuid():N}.html");

    static DependencyProperty Register(string name, Type type, object? defaultValue) =>
        DependencyProperty.Register(name, type, typeof(Video), new PropertyMetadata(defaultValue, (d, _) => ((Video)d).Reload()));

    /// <summary>新建：黑底、md 圆角。</summary>
    public Video()
    {
        Frame.Background = Brushes.Black;
        RadiusKey = ThemeKeys.Radius.Md;
        Unloaded += (_, _) => File.Delete(page);
        Loaded += (_, _) =>
        {
            if (!File.Exists(page)) Reload();
        };
    }

    /// <summary>视频地址。</summary>
    public Uri? Source { get => (Uri?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>几种格式的视频源。</summary>
    public IReadOnlyList<VideoSource>? Sources { get => (IReadOnlyList<VideoSource>?)GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <summary>封面。</summary>
    public Uri? Poster { get => (Uri?)GetValue(PosterProperty); set => SetValue(PosterProperty, value); }

    /// <summary>文本轨道。</summary>
    public IReadOnlyList<VideoTrack>? Tracks { get => (IReadOnlyList<VideoTrack>?)GetValue(TracksProperty); set => SetValue(TracksProperty, value); }

    /// <summary>控制条。</summary>
    public VideoControlBar ControlBar { get => (VideoControlBar)GetValue(ControlBarProperty); set => SetValue(ControlBarProperty, value); }

    /// <summary>倍速菜单里的选项。</summary>
    public IReadOnlyList<double>? Speeds { get => (IReadOnlyList<double>?)GetValue(SpeedsProperty); set => SetValue(SpeedsProperty, value); }

    /// <summary>自动播放。</summary>
    public bool Autoplay { get => (bool)GetValue(AutoplayProperty); set => SetValue(AutoplayProperty, value); }

    /// <summary>循环播放。</summary>
    public bool Loop { get => (bool)GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>静音。</summary>
    public bool Muted { get => (bool)GetValue(MutedProperty); set => SetValue(MutedProperty, value); }

    /// <summary>放进内核的页面（测试用）。</summary>
    internal string Html
    {
        get
        {
            // 减少动态效果：不自动播放，显示控制条，交给人自己点
            var plays = Autoplay && Motion.Enabled;
            var bar = ControlBar == VideoControlBar.None && Autoplay && !plays ? VideoControlBar.Native : ControlBar;
            var label = AutomationProperties.GetName(this);
            return VideoPage.Html(Source?.AbsoluteUri, Sources, Poster?.AbsoluteUri, Tracks, bar, plays, Loop, Muted,
                string.IsNullOrEmpty(label) ? null : label, bar == VideoControlBar.Custom ? Chrome() : null);
        }
    }

    /// <summary>自绘控制条的外观与文案：取自所在作用域的主题、语言包。</summary>
    VideoChrome Chrome()
    {
        string Css(string key) => TryFindResource(key) is Color color ? VideoChrome.Css(color.A, color.R, color.G, color.B) : "";
        return new VideoChrome(Provider.GetLang(this), TextElement.GetFontSize(this), Speeds ?? VideoChrome.DefaultSpeeds,
            Css(ThemeKeys.Color.OnScrim), Css(ThemeKeys.Color.ScrimStrong), Css(ThemeKeys.Color.SurfaceRaised), Css(ThemeKeys.Color.Text),
            Css(ThemeKeys.Color.Border), Css(ThemeKeys.Color.FillHover), Css(ThemeKeys.Color.FocusRing));
    }

    /// <inheritdoc />
    protected override void Load(WebView2 view)
    {
        if (Source is null && Sources is not { Count: > 0 }) return;
        Directory.CreateDirectory(Path.GetDirectoryName(page)!);
        File.WriteAllText(page, Html);
        view.Source = new Uri(page);
    }
}
