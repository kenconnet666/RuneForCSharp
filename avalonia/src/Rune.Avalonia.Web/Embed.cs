using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;

namespace Rune.Avalonia;

/// <summary>
/// 嵌入的页面（vue-tsx-pro 的 Embed）：地图、视频网站的播放器、在线文档、报表。网页内核是 Avalonia.Controls.WebView
/// （Windows 上是 WebView2，要装了 Edge WebView2 运行时）。
/// <code>
/// &lt;rune:Embed Source="https://player.example.com/embed/42" Title="产品演示视频" /&gt;
/// &lt;rune:Embed Source="{Binding ReportUrl}" Title="月度报表" Ratio="1.3333" /&gt;
/// </code>
/// 固定的宽高比（加载前就占好位置，默认 16 / 9；Ratio 写 null 时不定比例，自己给 Height）；加载完之前是骨架；外框 xs 描边、md 圆角、surface 底。
/// <see cref="Title"/> 是读屏的名称（必填：读屏靠它知道这一块是什么）。加载完发 <see cref="PageLoaded"/>（TS 的 onLoad）。
/// 内核是原生窗口：边角是直的，不跟着外框的圆角裁；放进页面就创建（TS 的 loading="lazy" 是滚到附近才加载，桌面上没有）。
/// </summary>
public class Embed : WebShell
{
    /// <summary>嵌入页面的地址。</summary>
    public static readonly StyledProperty<Uri?> SourceProperty = AvaloniaProperty.Register<Embed, Uri?>(nameof(Source));

    /// <summary>这一块是什么：读屏的名称。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Embed, string?>(nameof(Title));

    /// <summary>新建：外框 xs 描边、md 圆角、surface 底。</summary>
    public Embed()
    {
        Frame.BorderThickness = new Thickness(1);
        BindFrame("Rune.Brush.Surface", "Rune.Brush.Border");
        RadiusKey = ThemeKeys.Radius.Md;
    }

    /// <summary>嵌入页面的地址。</summary>
    public Uri? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>读屏的名称。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>页面加载完了（第一次导航结束，成功或失败）。</summary>
    public event EventHandler? PageLoaded;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty) Reload();
        else if (change.Property == TitleProperty) SetValue(AutomationProperties.NameProperty, Title, BindingPriority.Template);
    }

    /// <inheritdoc />
    protected override void Load(NativeWebView view)
    {
        if (Source is not { } source) return;
        // NativeWebView 导航到 data: 地址时不发 NavigationCompleted（页面照常显示），骨架就一直不撤：HTML 的 data: 地址解出来按字符串加载
        if (InlineHtml(source) is { } html) view.NavigateToString(html);
        else view.Navigate(source);
    }

    /// <summary>data: 地址里的 HTML（text/html 或不写类型；base64 或百分号编码）；别的地址为 null。</summary>
    internal static string? InlineHtml(Uri source)
    {
        if (source.Scheme != "data") return null;
        var text = source.OriginalString;
        var comma = text.IndexOf(',');
        if (comma < 0) return null;
        var meta = text[5..comma];
        var type = meta.Split(';')[0];
        if (type.Length > 0 && !type.Equals("text/html", StringComparison.OrdinalIgnoreCase)) return null;
        var data = text[(comma + 1)..];
        return meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
            ? Encoding.UTF8.GetString(Convert.FromBase64String(Uri.UnescapeDataString(data)))
            : Uri.UnescapeDataString(data);
    }

    /// <inheritdoc />
    protected override void OnPageLoaded(bool success) => PageLoaded?.Invoke(this, EventArgs.Empty);
}
