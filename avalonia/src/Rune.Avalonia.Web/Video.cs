using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 视频（vue-tsx-pro 的 Video）：网页内核里的 video 元素——内核自己的控制条，加上固定的宽高比（加载前就占好位置）、封面、多种格式、字幕轨道。
/// <code>
/// &lt;rune:Video Source="https://example.com/demo.mp4" Poster="https://example.com/cover.jpg" AutomationProperties.Name="产品演示" /&gt;
/// &lt;rune:Video Source="{Binding Loop}" Autoplay="True" Loop="True" ShowControls="False" AutomationProperties.Name="背景动画" /&gt;
/// </code>
/// <list type="bullet">
/// <item>自动播放：默认静音（内核不允许带声音的自动播放）；系统关掉动画（减少动态效果）时不自动播放，并显示控制条。</item>
/// <item>画面按 contain 放进框里（不裁切），四周黑底、md 圆角。<see cref="WebShell.Ratio"/> 写 null 时不定比例，自己给 Height。</item>
/// <item>多种格式用 <see cref="Sources"/>（内核取第一个能播的），字幕用 <see cref="Tracks"/>；默认只预加载元数据。</item>
/// <item>本地文件也能播：页面写在临时文件夹里再打开（直接给内核一段 HTML 时，它不让页面读本地文件）。</item>
/// </list>
/// 自绘的控制条（倍速、清晰度、弹幕）不在这里，要的话用专门的播放器。内核是原生窗口，边角是直的。
/// </summary>
public class Video : WebShell
{
    /// <summary>视频地址；有几种格式时用 Sources。</summary>
    public static readonly StyledProperty<Uri?> SourceProperty = AvaloniaProperty.Register<Video, Uri?>(nameof(Source));

    /// <summary>几种格式的视频源（写了它就不用 Source）。</summary>
    public static readonly StyledProperty<IReadOnlyList<VideoSource>?> SourcesProperty = AvaloniaProperty.Register<Video, IReadOnlyList<VideoSource>?>(nameof(Sources));

    /// <summary>封面：开始播放前显示。</summary>
    public static readonly StyledProperty<Uri?> PosterProperty = AvaloniaProperty.Register<Video, Uri?>(nameof(Poster));

    /// <summary>文本轨道：字幕、说明。</summary>
    public static readonly StyledProperty<IReadOnlyList<VideoTrack>?> TracksProperty = AvaloniaProperty.Register<Video, IReadOnlyList<VideoTrack>?>(nameof(Tracks));

    /// <summary>显示内核的控制条，默认 true（自动播放的装饰视频可以关掉；减少动态效果时仍会显示）。</summary>
    public static readonly StyledProperty<bool> ShowControlsProperty = AvaloniaProperty.Register<Video, bool>(nameof(ShowControls), true);

    /// <summary>自动播放：静音；减少动态效果时不自动播放。</summary>
    public static readonly StyledProperty<bool> AutoplayProperty = AvaloniaProperty.Register<Video, bool>(nameof(Autoplay));

    /// <summary>循环播放。</summary>
    public static readonly StyledProperty<bool> LoopProperty = AvaloniaProperty.Register<Video, bool>(nameof(Loop));

    /// <summary>静音。</summary>
    public static readonly StyledProperty<bool> MutedProperty = AvaloniaProperty.Register<Video, bool>(nameof(Muted));

    // 这个实例的页面文件：换内容时覆盖，离开界面时删掉
    readonly string page = Path.Combine(Path.GetTempPath(), "Rune.Video", $"{Guid.NewGuid():N}.html");

    /// <summary>新建：黑底、md 圆角。</summary>
    public Video()
    {
        FillFrame(Brushes.Black);
        RadiusKey = ThemeKeys.Radius.Md;
    }

    /// <summary>视频地址。</summary>
    public Uri? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>几种格式的视频源。</summary>
    public IReadOnlyList<VideoSource>? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <summary>封面。</summary>
    public Uri? Poster { get => GetValue(PosterProperty); set => SetValue(PosterProperty, value); }

    /// <summary>文本轨道。</summary>
    public IReadOnlyList<VideoTrack>? Tracks { get => GetValue(TracksProperty); set => SetValue(TracksProperty, value); }

    /// <summary>显示控制条。</summary>
    public bool ShowControls { get => GetValue(ShowControlsProperty); set => SetValue(ShowControlsProperty, value); }

    /// <summary>自动播放。</summary>
    public bool Autoplay { get => GetValue(AutoplayProperty); set => SetValue(AutoplayProperty, value); }

    /// <summary>循环播放。</summary>
    public bool Loop { get => GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>静音。</summary>
    public bool Muted { get => GetValue(MutedProperty); set => SetValue(MutedProperty, value); }

    /// <summary>放进内核的页面（测试用）。</summary>
    internal string Html
    {
        get
        {
            // 减少动态效果：不自动播放，显示控制条，交给人自己点
            var plays = Autoplay && Motion.Enabled;
            return VideoPage.Html(Source?.AbsoluteUri, Sources, Poster?.AbsoluteUri, Tracks, ShowControls || (Autoplay && !plays), plays, Loop, Muted,
                AutomationProperties.GetName(this));
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == SourcesProperty || change.Property == PosterProperty || change.Property == TracksProperty
            || change.Property == ShowControlsProperty || change.Property == AutoplayProperty || change.Property == LoopProperty || change.Property == MutedProperty)
            Reload();
    }

    /// <inheritdoc />
    protected override void Load(NativeWebView view)
    {
        if (Source is null && Sources is not { Count: > 0 }) return;
        Directory.CreateDirectory(Path.GetDirectoryName(page)!);
        File.WriteAllText(page, Html);
        view.Navigate(new Uri(page));
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        File.Delete(page);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!File.Exists(page)) Reload();
    }
}
