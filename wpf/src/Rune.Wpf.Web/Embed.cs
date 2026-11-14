using System.Windows;
using System.Windows.Automation;
using Microsoft.Web.WebView2.Wpf;

namespace Rune.Wpf;

/// <summary>
/// 嵌入的页面（vue-tsx-pro 的 Embed）：地图、视频网站的播放器、在线文档、报表。网页内核是 WebView2（要装了 Edge WebView2 运行时）。
/// <code>
/// &lt;rune:Embed Source="https://player.example.com/embed/42" Title="产品演示视频" /&gt;
/// &lt;rune:Embed Source="{Binding ReportUrl}" Title="月度报表" Ratio="1.3333" /&gt;
/// </code>
/// 固定的宽高比（加载前就占好位置，默认 16 / 9；Ratio 写 null 时不定比例，自己给 Height）；加载完之前是骨架；外框 xs 描边、md 圆角、surface 底。
/// <see cref="Title"/> 是读屏的名称（必填：读屏靠它知道这一块是什么）。加载完发 <see cref="PageLoaded"/>（TS 的 onLoad）。
/// 内核是原生窗口：边角是直的，盖在别的 WPF 内容上面；放进页面就创建（TS 的 loading="lazy" 是滚到附近才加载，桌面上没有）。
/// </summary>
public class Embed : WebShell
{
    /// <summary>嵌入页面的地址。</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(Uri), typeof(Embed), new PropertyMetadata(null, (d, _) => ((Embed)d).Reload()));

    /// <summary>这一块是什么：读屏的名称。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(Embed), new PropertyMetadata(null, (d, _) => AutomationProperties.SetName(d, ((Embed)d).Title ?? "")));

    /// <summary>新建：外框 xs 描边、md 圆角、surface 底。</summary>
    public Embed()
    {
        Frame.BorderThickness = new Thickness(1);
        Frame.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, ThemeKeys.Brush.Surface);
        Frame.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, ThemeKeys.Brush.Border);
        RadiusKey = ThemeKeys.Radius.Md;
    }

    /// <summary>嵌入页面的地址。</summary>
    public Uri? Source { get => (Uri?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>读屏的名称。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>页面加载完了（第一次导航结束，成功或失败）。</summary>
    public event EventHandler? PageLoaded;

    /// <inheritdoc />
    protected override void Load(WebView2 view)
    {
        if (Source is { } source) view.Source = source;
    }

    /// <inheritdoc />
    protected override void OnPageLoaded(bool success) => PageLoaded?.Invoke(this, EventArgs.Empty);
}
