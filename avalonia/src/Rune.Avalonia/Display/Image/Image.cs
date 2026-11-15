using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using AvaloniaImage = Avalonia.Controls.Image;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 图片（vue-tsx-pro 的 Image）：按地址读图，读的时候是扫光的占位、读完淡入，读不了时显示占位与 alt；写了 <see cref="Preview"/> 时点击（或 Enter、空格）
/// 看大图（<see cref="Lightbox"/>）。同一个 <see cref="ImageGroup"/> 里的图共用一个预览，大图里左右切换。
/// <code>
/// &lt;rune:Image Source="photos/1-thumb.jpg" Alt="支架正面" Width="160" Height="120" Preview="True" PreviewSource="photos/1.jpg" /&gt;
/// </code>
/// 地址是 <see cref="Uri"/>：avares://、file://、http(s)://、base64 的 data:，相对地址按程序所在目录（要有读的状态与失败的占位，所以不收已经解码的 IImage）。
/// 框的大小用 Width、Height（不写时按图片），图片在框里怎么放用 <see cref="Fit"/>（默认 cover），圆角档位 <see cref="Radius"/>（默认 md），底色 fill。
/// 可以预览时是按钮：名称“查看大图：alt”，键盘能到，悬停时略微压暗。读屏：不能预览时是一张图片、名称是 alt；读不了时另有说明“图片加载失败”。
/// 部件：<c>rune|Image Image#PART_Image</c>、<c>ContentPresenter#PART_Fallback</c>。
/// </summary>
public class Image : Control
{
    /// <summary>图片地址。</summary>
    public static readonly StyledProperty<Uri?> SourceProperty = AvaloniaProperty.Register<Image, Uri?>(nameof(Source));

    /// <summary>图片的说明：读屏念它，读不了时也显示。</summary>
    public static readonly StyledProperty<string?> AltProperty = AvaloniaProperty.Register<Image, string?>(nameof(Alt));

    /// <summary>图片在框里怎么放，默认 cover。</summary>
    public static readonly StyledProperty<ImageFit> FitProperty = AvaloniaProperty.Register<Image, ImageFit>(nameof(Fit), ImageFit.Cover);

    /// <summary>圆角档位，默认 md。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<Image, Radius>(nameof(Radius), Radius.Md);

    /// <summary>点击看大图。</summary>
    public static readonly StyledProperty<bool> PreviewProperty = AvaloniaProperty.Register<Image, bool>(nameof(Preview));

    /// <summary>大图的地址；不写时用 <see cref="Source"/>。</summary>
    public static readonly StyledProperty<Uri?> PreviewSourceProperty = AvaloniaProperty.Register<Image, Uri?>(nameof(PreviewSource));

    /// <summary>大图下方的说明。</summary>
    public static readonly StyledProperty<object?> CaptionProperty = AvaloniaProperty.Register<Image, object?>(nameof(Caption));

    /// <summary>大图下载时建议的文件名；不写时取地址里的文件名。</summary>
    public static readonly StyledProperty<string?> DownloadNameProperty = AvaloniaProperty.Register<Image, string?>(nameof(DownloadName));

    /// <summary>大图里显示下载按钮，默认 true。</summary>
    public static readonly StyledProperty<bool> DownloadableProperty = AvaloniaProperty.Register<Image, bool>(nameof(Downloadable), true);

    /// <summary>读不了时显示的内容；默认是图标与 alt。</summary>
    public static readonly StyledProperty<object?> FallbackProperty = AvaloniaProperty.Register<Image, object?>(nameof(Fallback));

    /// <summary>加载状态。</summary>
    public static readonly DirectProperty<Image, ImageStatus> StatusProperty = AvaloniaProperty.RegisterDirect<Image, ImageStatus>(nameof(Status), image => image.Status);

    /// <summary>字号（继承），圆角按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Image>();

    static readonly StyledProperty<double> RadiusFactorProperty = AvaloniaProperty.Register<Image, double>("RadiusFactor");

    readonly Border root = new() { ClipToBounds = true };
    readonly Panel layers = new();
    readonly Skeleton shimmer = new();
    readonly AvaloniaImage picture = new() { Name = ImageParts.Image, Opacity = 0 };
    readonly ContentPresenter fallback = new() { Name = ImageParts.Fallback, IsVisible = false };
    readonly Border hover = new() { Background = new SolidColorBrush(Colors.Black, 0.1), Opacity = 0, IsHitTestVisible = false };
    readonly Border focusRing = new() { Name = ImageParts.FocusRing, IsHitTestVisible = false };
    readonly TemplateResources resources;
    Lightbox? lightbox;
    int loading;
    bool pressed;

    static Image()
    {
        AffectsArrange<Image>(FontSizeProperty, RadiusFactorProperty, RadiusProperty);
        FocusableProperty.OverrideDefaultValue<Image>(false);
    }

    /// <summary>新建：占位、图片、失败的占位叠在一起。</summary>
    public Image()
    {
        resources = new TemplateResources(this);
        resources.Bind(RadiusFactorProperty, Radius.ResourceKey);
        layers.Children.AddRange([shimmer, picture, fallback, hover, focusRing]);
        root.Child = layers;
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        UpdateFit();
    }

    /// <summary>图片地址。</summary>
    public Uri? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>图片的说明。</summary>
    public string? Alt { get => GetValue(AltProperty); set => SetValue(AltProperty, value); }

    /// <summary>图片在框里怎么放。</summary>
    public ImageFit Fit { get => GetValue(FitProperty); set => SetValue(FitProperty, value); }

    /// <summary>圆角档位。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>点击看大图。</summary>
    public bool Preview { get => GetValue(PreviewProperty); set => SetValue(PreviewProperty, value); }

    /// <summary>大图的地址。</summary>
    public Uri? PreviewSource { get => GetValue(PreviewSourceProperty); set => SetValue(PreviewSourceProperty, value); }

    /// <summary>大图下方的说明。</summary>
    public object? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <summary>大图下载时建议的文件名。</summary>
    public string? DownloadName { get => GetValue(DownloadNameProperty); set => SetValue(DownloadNameProperty, value); }

    /// <summary>大图里显示下载按钮。</summary>
    public bool Downloadable { get => GetValue(DownloadableProperty); set => SetValue(DownloadableProperty, value); }

    /// <summary>读不了时显示的内容。</summary>
    public object? Fallback { get => GetValue(FallbackProperty); set => SetValue(FallbackProperty, value); }

    /// <summary>加载状态。</summary>
    public ImageStatus Status
    {
        get;
        private set
        {
            if (!SetAndRaise(StatusProperty, ref field, value)) return;
            PseudoClasses.Set(":loading", value == ImageStatus.Loading);
            PseudoClasses.Set(":error", value == ImageStatus.Error);
            UpdateStatus();
        }
    }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>可以点开看大图（写了 Preview、没有读失败）。</summary>
    internal bool Previewable => Preview && Status != ImageStatus.Error;

    /// <summary>自己的预览（不在 ImageGroup 里时；测试用）。</summary>
    internal Lightbox? OwnLightbox => lightbox;

    /// <summary>大图里的这一张。</summary>
    internal LightboxItem Item => new(PreviewSource ?? Source ?? new Uri("about:blank"), Alt ?? "")
    {
        Caption = Caption,
        DownloadName = DownloadName,
        Downloadable = Downloadable,
    };

    /// <summary>打开大图：在 ImageGroup 里时交给组（从这一张开始），否则用自己的 Lightbox。</summary>
    public void OpenPreview()
    {
        if (!Previewable) return;
        if (this.FindLogicalAncestorOfType<ImageGroup>() is { } group)
        {
            group.Open(this);
            return;
        }
        lightbox ??= new Lightbox();
        if (lightbox.IsOpen) return;
        lightbox.Items = [Item];
        lightbox.Index = 0;
        _ = lightbox.ShowAsync(this);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Status != ImageStatus.Loaded) _ = Load();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            if (VisualRoot is not null) _ = Load();
        }
        else if (change.Property == RadiusProperty) resources.Bind(RadiusFactorProperty, Radius.ResourceKey);
        else if (change.Property == FitProperty) UpdateFit();
        else if (change.Property == PreviewProperty || change.Property == AltProperty || change.Property == FallbackProperty) UpdateStatus();
    }

    /// <summary>读图：读的时候占位扫光；换了地址时只认最后一次。</summary>
    async Task Load()
    {
        var version = ++loading;
        Status = ImageStatus.Loading;
        if (Source is not { } source)
        {
            Status = ImageStatus.Error;
            return;
        }
        try
        {
            var bitmap = await ImageLoader.LoadAsync(source);
            if (version != loading) return;
            picture.Source = bitmap;
            Status = ImageStatus.Loaded;
        }
        catch (Exception error)
        {
            if (version != loading) return;
            System.Diagnostics.Trace.TraceWarning($"Image 读不了 {source}：{error.Message}");
            picture.Source = null;
            Status = ImageStatus.Error;
        }
    }

    /// <summary>cover → UniformToFill，contain → Uniform，fill → Fill，none → None，scale-down → 只缩小的 Uniform。</summary>
    void UpdateFit()
    {
        picture.Stretch = Fit.Value switch
        {
            "contain" or "scale-down" => Stretch.Uniform,
            "fill" => Stretch.Fill,
            "none" => Stretch.None,
            _ => Stretch.UniformToFill,
        };
        picture.StretchDirection = Fit == ImageFit.ScaleDown ? StretchDirection.DownOnly : StretchDirection.Both;
    }

    /// <summary>占位、图片、失败的占位按状态显示；能预览时可以聚焦、指针是手形。</summary>
    void UpdateStatus()
    {
        shimmer.IsVisible = Status == ImageStatus.Loading;
        picture.IsVisible = Status != ImageStatus.Error;
        // 读完淡入（同上游 shimmerStyles.reveal）；系统关闭动画时直接显示
        var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
        picture.Transitions = Status == ImageStatus.Loaded && duration > TimeSpan.Zero ? [new DoubleTransition { Property = OpacityProperty, Duration = duration }] : null;
        picture.Opacity = Status == ImageStatus.Loaded ? 1 : 0;
        fallback.IsVisible = Status == ImageStatus.Error;
        if (Status == ImageStatus.Error)
            fallback.Content = Fallback ?? new Flex
            {
                Direction = FlexDirection.Column,
                Align = FlexAlign.Center,
                Justify = FlexJustify.Center,
                Gap = Space.Xs,
                Children =
                {
                    new Icon { Data = Lucide.ImageOff },
                    new Text { Text = Alt, Size = TextSize.Xs, Align = TextAlign.Center },
                },
            };
        Focusable = Previewable;
        Cursor = Previewable ? new Cursor(StandardCursorType.Hand) : null;
        if (!Previewable) hover.Opacity = 0;
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (Previewable) hover.Opacity = 1;
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        hover.Opacity = 0;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!Previewable || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        pressed = true;
        e.Handled = true;
    }

    /// <summary>在图片上按下又抬起才打开（同按钮：按下后移出去再抬起不算）。</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!pressed) return;
        pressed = false;
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) OpenPreview();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Previewable || e.Key is not (Key.Enter or Key.Space)) return;
        OpenPreview();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        var radius = Radius.Value == "full" ? Math.Min(finalSize.Width, finalSize.Height) / 2 : GetValue(RadiusFactorProperty) * FontSize;
        root.CornerRadius = focusRing.CornerRadius = new CornerRadius(radius);
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ImagePeer(this);

    /// <summary>能预览时是按钮（名称“查看大图：alt”，可以调用），否则是一张图片（名称是 alt）；读不了时说明“图片加载失败”。里面的元素不另念。</summary>
    sealed class ImagePeer(Image owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.Previewable ? AutomationControlType.Button : AutomationControlType.Image;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name
            : owner.Previewable ? Provider.GetLang(owner).ImagePreview(owner.Alt ?? "") : owner.Alt;

        protected override string? GetHelpTextCore() => owner.Status == ImageStatus.Error ? Provider.GetLang(owner).ImageError : base.GetHelpTextCore();

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        public void Invoke() => owner.OpenPreview();
    }
}

/// <summary>Image 的部件名。</summary>
public static class ImageParts
{
    /// <summary>图片（TS 的 img）。</summary>
    public const string Image = "PART_Image";

    /// <summary>读不了时的占位（TS 的 propsFallback）。</summary>
    public const string Fallback = "PART_Fallback";

    /// <summary>焦点环（能预览时键盘聚焦显示）。</summary>
    public const string FocusRing = "PART_FocusRing";
}

/// <summary>
/// 一组图片（vue-tsx-pro 的 ImageGroup）：换行排列（间距 sm），里面能预览的 <see cref="Image"/> 共用一个 <see cref="Lightbox"/>，从点的那张开始，大图里左右切换。
/// <code>
/// &lt;rune:ImageGroup Loop="True"&gt;&lt;rune:Image … Preview="True" /&gt;…&lt;/rune:ImageGroup&gt;
/// </code>
/// 图片可以夹在别的内容里（图片加名称的一格）：打开时按界面里的先后顺序收集组里能预览的图。共用的预览是 <see cref="Lightbox"/>（TS 的 propsLightbox：改它的属性、听它的事件）。
/// </summary>
public class ImageGroup : Flex
{
    /// <summary>到头之后从另一头继续，默认 false。</summary>
    public static readonly StyledProperty<bool> LoopProperty = AvaloniaProperty.Register<ImageGroup, bool>(nameof(Loop));

    /// <summary>新建：换行排列、间距 sm。</summary>
    public ImageGroup()
    {
        Wrap = FlexWrap.Wrap;
        Gap = Space.Sm;
    }

    /// <summary>到头之后从另一头继续。</summary>
    public bool Loop { get => GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>共用的预览。</summary>
    public Lightbox Lightbox { get; } = new();

    /// <summary>从 <paramref name="image"/> 那张开始预览组里能预览的图。</summary>
    internal void Open(Image image)
    {
        if (Lightbox.IsOpen) return;
        var images = this.GetLogicalDescendants().OfType<Image>().Where(item => item.Preview).ToList();
        Lightbox.Items = [.. images.Select(item => item.Item)];
        Lightbox.Index = Math.Max(0, images.IndexOf(image));
        Lightbox.Loop = Loop;
        _ = Lightbox.ShowAsync(image);
    }
}
