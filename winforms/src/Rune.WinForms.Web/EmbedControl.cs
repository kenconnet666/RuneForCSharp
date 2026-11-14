using System.ComponentModel;
using Microsoft.Web.WebView2.WinForms;

namespace Rune.WinForms;

/// <summary>
/// 嵌入的页面（vue-tsx-pro 的 Embed）：地图、视频网站的播放器、在线文档、报表。网页内核是 WebView2（要装了 Edge WebView2 运行时）。
/// <code>
/// var demo = new EmbedControl { Source = new Uri("https://player.example.com/embed/42"), Title = "产品演示视频" };
/// var report = new EmbedControl { Source = reportUrl, Title = "月度报表", Ratio = 4d / 3 };
/// </code>
/// 固定的宽高比（加载前就占好位置，默认 16 / 9；Ratio 为 null 时不定比例，自己给 Height）；加载完之前是骨架；外框 xs 描边、md 圆角、surface 底。
/// <see cref="Title"/> 是读屏的名称（必填：读屏靠它知道这一块是什么）。加载完发 <see cref="PageLoaded"/>（TS 的 onLoad）。
/// 放进窗体就创建内核（TS 的 loading="lazy" 是滚到附近才加载，桌面上没有）。
/// </summary>
[ToolboxItem(true)]
[Description("嵌入的页面：定比例的外框，加载完之前是骨架。")]
public class EmbedControl : WebShellControl
{
    /// <summary>新建：外框 xs 描边、md 圆角、surface 底。</summary>
    public EmbedControl()
    {
        Outlined = true;
    }

    /// <summary>嵌入页面的地址。</summary>
    [Category("Behavior")]
    [Description("嵌入页面的地址。")]
    [DefaultValue(null)]
    public Uri? Source
    {
        get;
        set
        {
            field = value;
            Reload();
        }
    }

    /// <summary>这一块是什么：读屏的名称（AccessibleName 没写时取它）。</summary>
    [Category("Accessibility")]
    [Description("这一块是什么：读屏的名称。")]
    [DefaultValue("")]
    public string Title
    {
        get;
        set
        {
            field = value ?? "";
            AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        }
    } = "";

    /// <summary>页面加载完了（第一次导航结束，成功或失败）。</summary>
    public event EventHandler? PageLoaded;

    /// <inheritdoc />
    protected override void Load(WebView2 view)
    {
        if (Source is { } source) view.Source = source;
    }

    /// <inheritdoc />
    protected override void OnPageLoaded(bool success) => PageLoaded?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    protected override (Color Fill, Color Border) FrameColors(ThemeColors colors) => (colors.Surface, colors.Border);

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new EmbedAccessibleObject(this);

    /// <summary>读屏：一个组，名称是 Title。</summary>
    sealed class EmbedAccessibleObject(EmbedControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.Title : owner.AccessibleName; set => owner.AccessibleName = value; }
    }
}
