using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Rune.Wpf;

/// <summary>
/// 头像（vue-tsx-pro 的 Avatar）：显示图片，图片还没下载好或失败时显示后备内容（默认是名字的首字母，没有名字时是人像图标）。
/// <code>
/// &lt;rune:Avatar Source="{Binding Photo}" DisplayName="{Binding Name}" /&gt;
/// &lt;rune:Avatar DisplayName="张三" Size="lg" Radius="md" /&gt;
/// &lt;rune:Avatar DisplayName="李四" Variant="solid" Tone="success" /&gt;
/// </code>
/// 边长是字号的 2.5 倍，默认圆形。后备内容的底色按状态色调（生成的 AvatarRecipe.g.xaml，与 Badge 同一张调色板）：soft（默认）是浅底 +
/// 色调文字色，solid 是实底 + 反色；色调默认 accent。读屏：有名字时是一张图片、名称是名字；没有名字时是装饰。
/// TS 的 name、src 在这里叫 DisplayName、Source（Name 是元素名）。部件样式键：<see cref="AvatarParts"/>。
/// </summary>
public class Avatar : Control
{
    /// <summary>图片；不写、还在下载或失败时显示后备内容。</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ImageSource), typeof(Avatar), new FrameworkPropertyMetadata(null, (d, e) => ((Avatar)d).Watch((ImageSource?)e.OldValue)));

    /// <summary>名字：读屏的名称，也用来生成首字母；不写时头像是装饰。</summary>
    public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.Register(
        nameof(DisplayName), typeof(string), typeof(Avatar), new FrameworkPropertyMetadata(null, (d, _) => ((Avatar)d).UpdateFallback()));

    /// <summary>后备内容，默认是名字的首字母，没有名字时是人像图标。</summary>
    public static readonly DependencyProperty FallbackProperty = DependencyProperty.Register(
        nameof(Fallback), typeof(object), typeof(Avatar), new FrameworkPropertyMetadata(null, (d, _) => ((Avatar)d).UpdateFallback()));

    /// <summary>字号档位，头像边长是它的 2.5 倍；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(Avatar), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    /// <summary>圆角，主题里默认 full（圆形）；团队、组织、应用的图标常用 md。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(Radius), typeof(Avatar), new FrameworkPropertyMetadata(default(Radius), (d, _) => ((Avatar)d).BindRadius()));

    /// <summary>后备内容的底色样式，主题里默认 soft。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(AvatarVariant), typeof(Avatar), new FrameworkPropertyMetadata(default(AvatarVariant)));

    /// <summary>后备内容的色调，主题里默认 accent。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(Avatar), new FrameworkPropertyMetadata(default(StatusTone)));

    static readonly DependencyProperty RadiusFactorProperty = DependencyProperty.Register(
        "RadiusFactor", typeof(double), typeof(Avatar), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsArrange));

    readonly Border root = new();
    readonly Grid layers = new();
    readonly Image image = new() { Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed };
    readonly ContentPresenter fallback = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    bool failed;

    static Avatar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Avatar), new FrameworkPropertyMetadata(typeof(Avatar)));
        FocusableProperty.OverrideMetadata(typeof(Avatar), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Avatar), new FrameworkPropertyMetadata(false));
        FontSizeProperty.OverrideMetadata(typeof(Avatar), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));
    }

    /// <summary>新建：后备内容垫在图片下面。</summary>
    public Avatar()
    {
        image.SetResourceReference(StyleProperty, AvatarParts.Image);
        fallback.SetResourceReference(StyleProperty, AvatarParts.Fallback);
        image.ImageFailed += (_, _) => Fail();
        root.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { Source = this });
        layers.Children.Add(fallback);
        layers.Children.Add(image);
        root.Child = layers;
        AddVisualChild(root);
        AddLogicalChild(root);
        UpdateFallback();
    }

    /// <summary>图片。</summary>
    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>名字。</summary>
    public string? DisplayName { get => (string?)GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }

    /// <summary>后备内容。</summary>
    public object? Fallback { get => GetValue(FallbackProperty); set => SetValue(FallbackProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>后备内容的底色样式。</summary>
    public AvatarVariant Variant { get => (AvatarVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>后备内容的色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>后备内容在显示吗。</summary>
    internal bool FallbackShown => fallback.Visibility == Visibility.Visible;

    double Side => 2.5 * FontSize;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <summary>名字的首字母（vue-tsx-pro 的 initials）：中文取最后一个字（通常是名），其他语言取第一个与最后一个词的首字母，大写。</summary>
    /// <param name="name">名字。</param>
    public static string Initials(string name) => AvatarInitials.Of(name);

    /// <summary>档位的字号在主题样式的触发器里；写成像素数时是本地值。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TextSize)e.NewValue).Pixels is { } pixels) d.SetValue(FontSizeProperty, pixels);
        else if (((TextSize)e.OldValue).Pixels is not null) d.ClearValue(FontSizeProperty);
    }

    void BindRadius()
    {
        if (Radius.ResourceKey is { } key) SetResourceReference(RadiusFactorProperty, key);
        else ClearValue(RadiusFactorProperty);
    }

    /// <summary>换了图片：重新等它下载好（下载中、失败时显示后备内容）。</summary>
    void Watch(ImageSource? old)
    {
        if (old is BitmapSource previous && !previous.IsFrozen)
        {
            previous.DownloadCompleted -= OnDownloaded;
            previous.DownloadFailed -= OnFailed;
            previous.DecodeFailed -= OnFailed;
        }
        failed = false;
        if (Source is BitmapSource next && !next.IsFrozen)
        {
            next.DownloadCompleted += OnDownloaded;
            next.DownloadFailed += OnFailed;
            next.DecodeFailed += OnFailed;
        }
        UpdateFallback();
    }

    void OnDownloaded(object? sender, EventArgs e) => UpdateFallback();

    void OnFailed(object? sender, ExceptionEventArgs e) => Fail();

    void Fail()
    {
        failed = true;
        UpdateFallback();
    }

    /// <summary>图片好了就显示图片；没有、在下载或失败时显示后备内容：给的、名字的首字母，或人像图标。</summary>
    void UpdateFallback()
    {
        var ready = Source is not null && !failed && Source is not BitmapSource { IsDownloading: true };
        image.Source = failed ? null : Source;
        image.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        fallback.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        fallback.Content = Fallback ?? (string.IsNullOrWhiteSpace(DisplayName)
            ? new Icon { Data = Lucide.User }
            : new Text { Text = Initials(DisplayName), Weight = TextWeight.Medium, Wrap = TextWrap.NoWrap });
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        root.Measure(new Size(Side, Side));
        return new Size(Side, Side);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var radius = Radius.Value == "full" ? Side / 2 : Math.Min((double)GetValue(RadiusFactorProperty) * FontSize, Side / 2);
        root.CornerRadius = new CornerRadius(radius);
        root.Arrange(new Rect(0, 0, Side, Side));
        // 图片按圆角裁剪（同 CSS 的 overflow: hidden）
        layers.Clip = new RectangleGeometry(new Rect(0, 0, Side, Side), radius, radius);
        return new Size(Side, Side);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AvatarAutomationPeer(this);

    /// <summary>有名字时是一张图片、名称是名字（里面的首字母、图片不另念）；没有名字时是装饰（TS 的 aria-hidden）。</summary>
    sealed class AvatarAutomationPeer(Avatar owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetClassNameCore() => nameof(Avatar);

        protected override string GetNameCore() => owner.DisplayName ?? "";

        protected override bool IsControlElementCore() => !string.IsNullOrWhiteSpace(owner.DisplayName);

        protected override bool IsContentElementCore() => IsControlElementCore();

        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>Avatar 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:AvatarParts.Image}" TargetType="Image"&gt;</c>。</summary>
public static class AvatarParts
{
    /// <summary>图片（TS 的 propsImage）。</summary>
    public static readonly PartKey Image = new(typeof(Avatar), nameof(Image), typeof(Image));

    /// <summary>后备内容（TS 的 propsFallback）。</summary>
    public static readonly PartKey Fallback = new(typeof(Avatar), nameof(Fallback), typeof(ContentPresenter));
}

/// <summary>后备内容的底色样式（vue-tsx-pro 的 AvatarVariants）。</summary>
/// <param name="Value">取值（soft、solid）。</param>
[TypeConverter(typeof(VariantConverter<AvatarVariant>))]
public readonly record struct AvatarVariant(string Value) : IVariant<AvatarVariant>
{
    /// <summary>浅色底（默认）。</summary>
    public static readonly AvatarVariant Soft = new("soft");

    /// <summary>实底。</summary>
    public static readonly AvatarVariant Solid = new("solid");

    /// <inheritdoc />
    public static IReadOnlyList<AvatarVariant> Known { get; } = [Soft, Solid];

    /// <inheritdoc />
    public static AvatarVariant Parse(string value)
    {
        var variant = new AvatarVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
