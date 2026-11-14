using System.Collections;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Rune.Wpf;

/// <summary>
/// Embed、Video 共用的外壳：一块（可定比例的）框，里面放网页内核（WebView2）；第一次加载完之前显示骨架，内核先藏着
/// （不少页面加载中是一片白，暗色主题里很刺眼）。内核是原生窗口：盖在 WPF 的内容上面（airspace），圆角裁不到它，边角是直的。
/// 内核的用户数据放在 %LocalAppData%\Rune\WebView2（默认放在程序旁边，装在 Program Files 里时写不进去）。
/// </summary>
public abstract class WebShell : FrameworkElement
{
    /// <summary>宽高比（宽 ÷ 高），默认 16 / 9；null 时不定比例，高度由 Height 给。</summary>
    public static readonly DependencyProperty RatioProperty = DependencyProperty.Register(
        nameof(Ratio), typeof(double?), typeof(WebShell), new FrameworkPropertyMetadata(16d / 9, FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((WebShell)d).Place()));

    static readonly string DataFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rune", "WebView2");

    readonly Decorator host = new();
    readonly AspectRatio ratio = new();
    readonly Border frame = new() { ClipToBounds = true };
    readonly Skeleton placeholder = new() { Shape = SkeletonShape.Image };

    static WebShell() => TextElement.FontSizeProperty.OverrideMetadata(typeof(WebShell), new FrameworkPropertyMetadata(
        SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((WebShell)d).ApplyRadius()));

    /// <summary>新建：骨架在下，内核在上（加载完才显示）。</summary>
    protected WebShell()
    {
        View = new WebView2 { Visibility = Visibility.Collapsed, CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = DataFolder } };
        View.NavigationCompleted += (_, e) =>
        {
            if (PageReady) return;
            PageReady = true;
            View.Visibility = Visibility.Visible;
            placeholder.Visibility = Visibility.Collapsed;
            OnPageLoaded(e.IsSuccess);
        };
        frame.Child = new System.Windows.Controls.Grid { Children = { placeholder, View } };
        Place();
        AddLogicalChild(host);
        AddVisualChild(host);
        Loaded += (_, _) => ApplyRadius();
    }

    /// <summary>宽高比。</summary>
    public double? Ratio { get => (double?)GetValue(RatioProperty); set => SetValue(RatioProperty, value); }

    /// <summary>网页内核。</summary>
    protected WebView2 View { get; }

    /// <summary>外框（派生类设边框、底色）。</summary>
    protected Border Frame => frame;

    /// <summary>外框的圆角档位（主题的 em 系数，按字号算成像素）；null 时直角。</summary>
    protected string? RadiusKey
    {
        get;
        set
        {
            field = value;
            ApplyRadius();
        }
    }

    /// <summary>加载完了（第一次导航结束，成功或失败）。</summary>
    internal bool PageReady { get; private set; }

    /// <summary>加载中的骨架（测试用）。</summary>
    internal Skeleton Placeholder => placeholder;

    /// <summary>重新加载：内容变了（换了地址）时回到骨架。</summary>
    protected void Reload()
    {
        PageReady = false;
        View.Visibility = Visibility.Collapsed;
        placeholder.Visibility = Visibility.Visible;
        Load(View);
    }

    /// <summary>让内核加载内容（导航到地址）。</summary>
    protected abstract void Load(WebView2 view);

    /// <summary>第一次加载完。</summary>
    protected virtual void OnPageLoaded(bool success)
    {
    }

    void ApplyRadius()
    {
        if (RadiusKey is not { } key || TryFindResource(key) is not double factor) return;
        frame.CornerRadius = new CornerRadius(factor * TextElement.GetFontSize(this));
    }

    /// <summary>定比例时外框放进 AspectRatio；不定比例时直接放外框，高度取 Height。</summary>
    void Place()
    {
        ratio.Child = null!;
        host.Child = null!;
        if (Ratio is { } value)
        {
            ratio.Ratio = value;
            ratio.Child = frame;
            host.Child = ratio;
        }
        else host.Child = frame;
    }

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { host }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => host;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        host.Measure(availableSize);
        return host.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        host.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new WebShellPeer(this);

    /// <summary>读屏：一个组，名称是 AutomationProperties.Name（Embed 默认取 Title）。</summary>
    sealed class WebShellPeer(WebShell owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => owner.GetType().Name;
    }
}
