using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// Embed、Video 共用的外壳：一块（可定比例的）框，里面放网页内核（<see cref="NativeWebView"/>）；第一次加载完之前显示骨架，
/// 内核先藏着（不少页面加载中是一片白，暗色主题里很刺眼）。内核是原生窗口，圆角裁不到它：边角是直的，外框的圆角画在它外面一圈。
/// 派生类给出外框的样子与要加载的内容（<see cref="Load"/>）。
/// Windows 上应用要有声明支持 Windows 10 的清单（app.manifest 的 supportedOS），不然 Avalonia 建不了原生子窗口（Avalonia 的应用模板自带）。
/// </summary>
public abstract class WebShell : Control
{
    /// <summary>宽高比（宽 ÷ 高），默认 16 / 9；null 时不定比例，高度由 Height 给。</summary>
    public static readonly StyledProperty<double?> RatioProperty = AvaloniaProperty.Register<WebShell, double?>(nameof(Ratio), 16d / 9);

    readonly Decorator host = new();
    readonly AspectRatio ratio = new();
    readonly Border frame = new() { ClipToBounds = true };
    readonly Skeleton placeholder = new() { Shape = SkeletonShape.Image };

    /// <summary>新建：骨架在下，内核在上（加载完才显示）。</summary>
    protected WebShell()
    {
        View.IsVisible = false;
        View.NavigationCompleted += (_, e) =>
        {
            if (PageReady) return;
            PageReady = true;
            View.IsVisible = true;
            placeholder.IsVisible = false;
            OnPageLoaded(e.IsSuccess);
        };
        frame.Child = new Panel { Children = { placeholder, View } };
        Place();
        LogicalChildren.Add(host);
        VisualChildren.Add(host);
    }

    /// <summary>宽高比。</summary>
    public double? Ratio { get => GetValue(RatioProperty); set => SetValue(RatioProperty, value); }

    /// <summary>网页内核。</summary>
    protected NativeWebView View { get; } = new();

    /// <summary>外框（派生类设边框、底色、圆角）。</summary>
    protected Border Frame => frame;

    /// <summary>加载完了（第一次导航结束，成功或失败）。</summary>
    internal bool PageReady { get; private set; }

    /// <summary>加载中的骨架（测试用）。</summary>
    internal Skeleton Placeholder => placeholder;

    /// <summary>重新加载：内容变了（换了地址）时回到骨架。</summary>
    protected void Reload()
    {
        PageReady = false;
        View.IsVisible = false;
        placeholder.IsVisible = true;
        Load(View);
    }

    /// <summary>让内核加载内容（导航到地址或页面）。</summary>
    protected abstract void Load(NativeWebView view);

    /// <summary>第一次加载完。</summary>
    protected virtual void OnPageLoaded(bool success)
    {
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RatioProperty) Place();
        else if (change.Property == global::Avalonia.Controls.Documents.TextElement.FontSizeProperty) ApplyRadius();
    }

    /// <summary>定比例时外框放进 AspectRatio；不定比例时直接放外框，高度取 Height。</summary>
    void Place()
    {
        ratio.Child = null;
        host.Child = null;
        if (Ratio is { } value)
        {
            ratio.Ratio = value;
            ratio.Child = frame;
            host.Child = ratio;
        }
        else host.Child = frame;
    }

    /// <inheritdoc />
    protected override global::Avalonia.Size MeasureOverride(global::Avalonia.Size availableSize)
    {
        host.Measure(availableSize);
        return host.DesiredSize;
    }

    /// <inheritdoc />
    protected override global::Avalonia.Size ArrangeOverride(global::Avalonia.Size finalSize)
    {
        host.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>外框的底色、描边从主题取（随主题切换）。</summary>
    protected void BindFrame(string? background, string? border)
    {
        if (background is not null) frame[!Border.BackgroundProperty] = frame.GetResourceObservable(background).ToBinding();
        if (border is not null) frame[!Border.BorderBrushProperty] = frame.GetResourceObservable(border).ToBinding();
    }

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

    void ApplyRadius()
    {
        if (RadiusKey is not { } key || VisualRoot is null) return;
        frame.CornerRadius = new CornerRadius(this.Resource<double>(key) * global::Avalonia.Controls.Documents.TextElement.GetFontSize(this));
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyRadius();
    }

    /// <summary>纯色的外框底（Video 的黑底）。</summary>
    protected void FillFrame(IBrush brush) => frame.Background = brush;

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new WebShellPeer(this);

    /// <summary>读屏：一个组，名称是使用方写的 AutomationProperties.Name（Embed 默认取 Title）。</summary>
    sealed class WebShellPeer(WebShell owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;
    }
}
