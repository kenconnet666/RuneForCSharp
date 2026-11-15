using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LayoutGrid = System.Windows.Controls.Grid;
using WpfImage = System.Windows.Controls.Image;

namespace Rune.Wpf;

/// <summary>
/// 图片（vue-tsx-pro 的 Image）：按地址读图，读的时候是扫光的占位、读完淡入，读不了时显示占位与 alt；写了 <see cref="Preview"/> 时点击（或 Enter、空格）
/// 看大图（<see cref="Lightbox"/>）。同一个 <see cref="ImageGroup"/> 里的图共用一个预览，大图里左右切换。
/// <code>
/// &lt;rune:Image Source="photos/1-thumb.jpg" Alt="支架正面" Width="160" Height="120" Preview="True" PreviewSource="photos/1.jpg" /&gt;
/// </code>
/// 地址是 <see cref="Uri"/>：pack://、file://、http(s)://、base64 的 data:，相对地址按程序所在目录（要有读的状态与失败的占位，所以不收已经解码的 ImageSource）。
/// 框的大小用 Width、Height（不写时按图片），图片在框里怎么放用 <see cref="Fit"/>（默认 cover），圆角档位 <see cref="Radius"/>（默认 md），底色 fill。
/// 可以预览时是按钮：名称“查看大图：alt”，键盘能到，悬停时略微压暗。读屏：不能预览时是一张图片、名称是 alt；读不了时另有说明“图片加载失败”。
/// 部件样式键见 <see cref="ImageParts"/>。
/// </summary>
public class Image : CompositeControl
{
    /// <summary>图片地址。</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(Uri), typeof(Image),
        new FrameworkPropertyMetadata(null, (d, _) => ((Image)d).Reload()));

    /// <summary>图片的说明：读屏念它，读不了时也显示。</summary>
    public static readonly DependencyProperty AltProperty = DependencyProperty.Register(nameof(Alt), typeof(string), typeof(Image),
        new FrameworkPropertyMetadata(null, (d, _) => ((Image)d).UpdateStatus()));

    /// <summary>图片在框里怎么放，默认 cover。</summary>
    public static readonly DependencyProperty FitProperty = DependencyProperty.Register(nameof(Fit), typeof(ImageFit), typeof(Image),
        new FrameworkPropertyMetadata(ImageFit.Cover, (d, _) => ((Image)d).UpdateFit()));

    /// <summary>圆角档位，默认 md。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(Radius), typeof(Image),
        new FrameworkPropertyMetadata(Radius.Md, (d, _) => ((Image)d).BindRadius()));

    /// <summary>点击看大图。</summary>
    public static readonly DependencyProperty PreviewProperty = DependencyProperty.Register(nameof(Preview), typeof(bool), typeof(Image),
        new FrameworkPropertyMetadata(false, (d, _) => ((Image)d).UpdateStatus()));

    /// <summary>大图的地址；不写时用 <see cref="Source"/>。</summary>
    public static readonly DependencyProperty PreviewSourceProperty = DependencyProperty.Register(nameof(PreviewSource), typeof(Uri), typeof(Image));

    /// <summary>大图下方的说明。</summary>
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(object), typeof(Image));

    /// <summary>大图下载时建议的文件名；不写时取地址里的文件名。</summary>
    public static readonly DependencyProperty DownloadNameProperty = DependencyProperty.Register(nameof(DownloadName), typeof(string), typeof(Image));

    /// <summary>大图里显示下载按钮，默认 true。</summary>
    public static readonly DependencyProperty DownloadableProperty = DependencyProperty.Register(nameof(Downloadable), typeof(bool), typeof(Image), new FrameworkPropertyMetadata(true));

    /// <summary>读不了时显示的内容；默认是图标与 alt。</summary>
    public static readonly DependencyProperty FallbackProperty = DependencyProperty.Register(nameof(Fallback), typeof(object), typeof(Image),
        new FrameworkPropertyMetadata(null, (d, _) => ((Image)d).UpdateStatus()));

    static readonly DependencyPropertyKey StatusKey = DependencyProperty.RegisterReadOnly(nameof(Status), typeof(ImageStatus), typeof(Image),
        new FrameworkPropertyMetadata(ImageStatus.Loading, (d, _) => ((Image)d).UpdateStatus()));

    /// <summary>加载状态。</summary>
    public static readonly DependencyProperty StatusProperty = StatusKey.DependencyProperty;

    static readonly DependencyProperty RadiusFactorProperty = DependencyProperty.Register("RadiusFactor", typeof(double), typeof(Image),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsArrange));

    static readonly DependencyProperty FocusRingBrushProperty = DependencyProperty.Register("FocusRingBrush", typeof(Brush), typeof(Image),
        new FrameworkPropertyMetadata(null, (d, e) => ((Image)d).focusRing.BorderBrush = (Brush?)e.NewValue));

    readonly Border root = new();
    readonly LayoutGrid layers = new();
    readonly Skeleton shimmer = new();
    // 居中：cover 裁两边、none 露出中间（同 object-position 的默认 50% 50%）；WPF 的 Image 比格子大时默认从左上角排
    readonly WpfImage picture = new() { Opacity = 0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly ContentPresenter fallback = new() { Visibility = Visibility.Collapsed };
    readonly Border hover = new() { Background = new SolidColorBrush(Color.FromArgb(0x1A, 0, 0, 0)), Opacity = 0, IsHitTestVisible = false };
    readonly Border focusRing = new() { BorderThickness = new Thickness(2), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    Lightbox? lightbox;
    int loading;
    bool pressed;

    static Image()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Image), new FrameworkPropertyMetadata(typeof(Image)));
        FocusableProperty.OverrideMetadata(typeof(Image), new FrameworkPropertyMetadata(false));
    }

    /// <summary>新建：占位、图片、失败的占位叠在一起。</summary>
    public Image()
    {
        picture.SetResourceReference(StyleProperty, ImageParts.Image);
        fallback.SetResourceReference(StyleProperty, ImageParts.Fallback);
        SetResourceReference(FocusRingBrushProperty, ThemeKeys.Brush.FocusRing);
        root.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { Source = this });
        foreach (var layer in new UIElement[] { shimmer, picture, fallback, hover, focusRing }) layers.Children.Add(layer);
        root.Child = layers;
        Root = root;
        BindRadius();
        UpdateFit();
        UpdateStatus();
        Loaded += (_, _) =>
        {
            if (Status != ImageStatus.Loaded) _ = Load();
        };
    }

    /// <summary>图片地址。</summary>
    public Uri? Source { get => (Uri?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>图片的说明。</summary>
    public string? Alt { get => (string?)GetValue(AltProperty); set => SetValue(AltProperty, value); }

    /// <summary>图片在框里怎么放。</summary>
    public ImageFit Fit { get => (ImageFit)GetValue(FitProperty); set => SetValue(FitProperty, value); }

    /// <summary>圆角档位。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>点击看大图。</summary>
    public bool Preview { get => (bool)GetValue(PreviewProperty); set => SetValue(PreviewProperty, value); }

    /// <summary>大图的地址。</summary>
    public Uri? PreviewSource { get => (Uri?)GetValue(PreviewSourceProperty); set => SetValue(PreviewSourceProperty, value); }

    /// <summary>大图下方的说明。</summary>
    public object? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <summary>大图下载时建议的文件名。</summary>
    public string? DownloadName { get => (string?)GetValue(DownloadNameProperty); set => SetValue(DownloadNameProperty, value); }

    /// <summary>大图里显示下载按钮。</summary>
    public bool Downloadable { get => (bool)GetValue(DownloadableProperty); set => SetValue(DownloadableProperty, value); }

    /// <summary>读不了时显示的内容。</summary>
    public object? Fallback { get => GetValue(FallbackProperty); set => SetValue(FallbackProperty, value); }

    /// <summary>加载状态。</summary>
    public ImageStatus Status { get => (ImageStatus)GetValue(StatusProperty); private set => SetValue(StatusKey, value); }

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

    /// <inheritdoc />
    protected override CompositeChrome OwnChrome => CompositeChrome.Background;

    /// <summary>打开大图：在 ImageGroup 里时交给组（从这一张开始），否则用自己的 Lightbox。</summary>
    public void OpenPreview()
    {
        if (!Previewable) return;
        for (DependencyObject? node = this; node is not null; node = LogicalTreeHelper.GetParent(node) ?? VisualTreeHelper.GetParent(node))
            if (node is ImageGroup group)
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

    void BindRadius()
    {
        if (Radius.ResourceKey is { } key) SetResourceReference(RadiusFactorProperty, key);
        else ClearValue(RadiusFactorProperty);
    }

    void Reload()
    {
        if (IsLoaded) _ = Load();
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
        shimmer.Visibility = Status == ImageStatus.Loading ? Visibility.Visible : Visibility.Collapsed;
        picture.Visibility = Status == ImageStatus.Error ? Visibility.Collapsed : Visibility.Visible;
        // 读完淡入（同上游 shimmerStyles.reveal）；系统关闭动画时直接显示
        var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
        if (Status == ImageStatus.Loaded && duration > TimeSpan.Zero) picture.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { FillBehavior = FillBehavior.Stop });
        picture.Opacity = Status == ImageStatus.Loaded ? 1 : 0;
        fallback.Visibility = Status == ImageStatus.Error ? Visibility.Visible : Visibility.Collapsed;
        fallback.Margin = new Thickness(this.Resource<double>(ThemeKeys.Space.Sm) * FontSize);
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
                    new Text { Text = Alt ?? "", Size = TextSize.Xs, Align = TextAlign.Center },
                },
            };
        Focusable = Previewable;
        Cursor = Previewable ? Cursors.Hand : null;
        if (!Previewable) hover.Opacity = 0;
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (Previewable) hover.Opacity = 1;
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        hover.Opacity = 0;
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!Previewable) return;
        pressed = true;
        CaptureMouse();
        e.Handled = true;
    }

    /// <summary>在图片上按下又抬起才打开（同按钮：按下后移出去再抬起不算）。</summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!pressed) return;
        pressed = false;
        ReleaseMouseCapture();
        if (new Rect(RenderSize).Contains(e.GetPosition(this))) OpenPreview();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Previewable || e.Key is not (Key.Enter or Key.Space)) return;
        OpenPreview();
        e.Handled = true;
    }

    /// <summary>键盘聚焦时显示焦点环（鼠标点的不显示，同 :focus-visible）。</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        focusRing.Visibility = InputManager.Current.MostRecentInputDevice is KeyboardDevice ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        focusRing.Visibility = Visibility.Collapsed;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var size = base.ArrangeOverride(arrangeBounds);
        var radius = Radius.Value == "full" ? Math.Min(size.Width, size.Height) / 2 : (double)GetValue(RadiusFactorProperty) * FontSize;
        root.CornerRadius = focusRing.CornerRadius = new CornerRadius(radius);
        // 图片按圆角裁剪（同 CSS 的 overflow: hidden）
        layers.Clip = new RectangleGeometry(new Rect(size), radius, radius);
        return size;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ImagePeer(this);

    /// <summary>能预览时是按钮（名称“查看大图：alt”，可以调用），否则是一张图片（名称是 alt）；读不了时说明“图片加载失败”。里面的元素不另念。</summary>
    sealed class ImagePeer(Image owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
    {
        protected override string GetClassNameCore() => nameof(Image);

        protected override AutomationControlType GetAutomationControlTypeCore() => owner.Previewable ? AutomationControlType.Button : AutomationControlType.Image;

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name
            : owner.Previewable ? Provider.GetLang(owner).ImagePreview(owner.Alt ?? "") : owner.Alt ?? "";

        protected override string GetHelpTextCore() => owner.Status == ImageStatus.Error ? Provider.GetLang(owner).ImageError : base.GetHelpTextCore();

        protected override List<AutomationPeer>? GetChildrenCore() => null;

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Invoke && owner.Previewable ? this : base.GetPattern(patternInterface);

        public void Invoke() => owner.OpenPreview();
    }
}

/// <summary>Image 的部件样式键。</summary>
public static class ImageParts
{
    /// <summary>图片（TS 的 img）。</summary>
    public static readonly PartKey Image = new(typeof(Image), nameof(Image), typeof(WpfImage));

    /// <summary>读不了时的占位（TS 的 propsFallback）。</summary>
    public static readonly PartKey Fallback = new(typeof(Image), nameof(Fallback), typeof(ContentPresenter));
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
    public static readonly DependencyProperty LoopProperty = DependencyProperty.Register(nameof(Loop), typeof(bool), typeof(ImageGroup));

    /// <summary>新建：换行排列、间距 sm。</summary>
    public ImageGroup()
    {
        Wrap = FlexWrap.Wrap;
        Gap = Space.Sm;
    }

    /// <summary>到头之后从另一头继续。</summary>
    public bool Loop { get => (bool)GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>共用的预览。</summary>
    public Lightbox Lightbox { get; } = new();

    /// <summary>从 <paramref name="image"/> 那张开始预览组里能预览的图。</summary>
    internal void Open(Image image)
    {
        if (Lightbox.IsOpen) return;
        var images = Descendants(this).Where(item => item.Preview).ToList();
        Lightbox.Items = [.. images.Select(item => item.Item)];
        Lightbox.Index = Math.Max(0, images.IndexOf(image));
        Lightbox.Loop = Loop;
        _ = Lightbox.ShowAsync(image);
    }

    /// <summary>按界面里的先后顺序（可视树的先序）找组里的图片。</summary>
    static IEnumerable<Image> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is Image image) yield return image;
            else foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
