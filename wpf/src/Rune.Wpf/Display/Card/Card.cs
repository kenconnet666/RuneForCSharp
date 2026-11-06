using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Rune.Wpf;

/// <summary>
/// 卡片（vue-tsx-pro 的 Card）：把一组相关内容放在一块表面上。可选的页头、页脚与正文之间有分隔线。
/// <code>
/// &lt;rune:Card Header="订单 #1024"&gt;…&lt;/rune:Card&gt;
/// &lt;rune:Card Header="超时最久的项目" Extra="{…全部}"&gt;…&lt;/rune:Card&gt;   &lt;!-- 页头结束边的操作，放不下时换行 --&gt;
/// &lt;rune:Card IsLoading="{Binding Loading}" Header="…"&gt;…&lt;/rune:Card&gt;     &lt;!-- 正文换成几行扫光的骨架 --&gt;
/// </code>
/// 样式（surface、outline、elevated、ghost）、内边距（space 档位，默认 lg）、圆角（默认 lg）的默认值与样式档位的颜色在主题样式里，
/// Provider 的样式能改。从 FrameworkElement 派生（Control 的 Padding 是 Thickness，和 TS 的 padding 档位撞名）。
/// 阴影画在正文后面单独的一层上（DropShadowEffect 套在正文上会连文字一起投影）；正文按圆角裁剪。
/// 加载中正文换成骨架（页头、页脚照常显示），很快完成时不闪；出现过骨架之后，骨架与正文互换时淡入。加载时读屏的项状态是"加载中"。
/// 部件样式键：<see cref="CardParts"/>。TS 的装饰效果（tilt、spotlight、beam）还没做。
/// </summary>
[ContentProperty(nameof(Content))]
public class Card : FrameworkElement
{
    /// <summary>样式，主题里默认 surface。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(CardVariant), typeof(Card), new FrameworkPropertyMetadata(default(CardVariant)));

    /// <summary>页头、正文、页脚的内边距档位，主题里默认 lg。</summary>
    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding), typeof(Space), typeof(Card), new FrameworkPropertyMetadata(default(Space), (d, _) => ((Card)d).BindPadding()));

    /// <summary>圆角，主题里默认 lg。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(Radius), typeof(Card), new FrameworkPropertyMetadata(default(Radius), (d, _) => ((Card)d).BindRadius()));

    /// <summary>页头：标题、操作。</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(object), typeof(Card), new FrameworkPropertyMetadata(null, (d, _) => ((Card)d).UpdateSlots()));

    /// <summary>页头结束边的内容（"全部"、按钮、筛选）。</summary>
    public static readonly DependencyProperty ExtraProperty = DependencyProperty.Register(
        nameof(Extra), typeof(object), typeof(Card), new FrameworkPropertyMetadata(null, (d, _) => ((Card)d).UpdateSlots()));

    /// <summary>页脚：按钮等操作，靠结束边排。</summary>
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(object), typeof(Card), new FrameworkPropertyMetadata(null, (d, _) => ((Card)d).UpdateSlots()));

    /// <summary>正文加载中：换成骨架。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(Card), new FrameworkPropertyMetadata(false, (d, _) => ((Card)d).loading.Update(((Card)d).IsLoading)));

    /// <summary>正文。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(Card), new FrameworkPropertyMetadata(null, (d, _) => ((Card)d).UpdateSlots()));

    /// <summary>表面色（样式档位在主题样式里设）。</summary>
    public static readonly DependencyProperty BackgroundProperty = Border.BackgroundProperty.AddOwner(typeof(Card));

    /// <summary>边框色（没有边框的样式也占 1px）。</summary>
    public static readonly DependencyProperty BorderBrushProperty = Border.BorderBrushProperty.AddOwner(typeof(Card));

    /// <summary>阴影（elevated，主题里是 Rune.Shadow.Md）。</summary>
    public static readonly DependencyProperty ShadowProperty = DependencyProperty.Register(
        nameof(Shadow), typeof(Effect), typeof(Card), new FrameworkPropertyMetadata(null));

    static readonly DependencyProperty PaddingFactorProperty = DependencyProperty.Register(
        "PaddingFactor", typeof(double), typeof(Card), new FrameworkPropertyMetadata(0d, (d, _) => ((Card)d).UpdateMetrics()));

    static readonly DependencyProperty RadiusFactorProperty = DependencyProperty.Register(
        "RadiusFactor", typeof(double), typeof(Card), new FrameworkPropertyMetadata(0d, (d, _) => ((Card)d).UpdateMetrics()));

    readonly Grid root = new();
    readonly Border shadow = new();
    readonly Border surface = new() { BorderThickness = new Thickness(1) };
    readonly StackPanel layout = new();
    readonly Border header = new() { BorderThickness = new Thickness(0, 0, 0, 1) };
    readonly Flex headerRow = new() { Wrap = true, Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm };
    readonly ContentPresenter headerContent = new();
    readonly ContentPresenter extraContent = new();
    readonly Border body = new();
    readonly ContentPresenter bodyContent = new();
    readonly SkeletonText skeleton = new();
    readonly Border footer = new() { BorderThickness = new Thickness(0, 1, 0, 0) };
    readonly Flex footerRow = new() { Justify = FlexJustify.End, Gap = Space.Sm };
    readonly ContentPresenter footerContent = new();
    DelayedLoading loading;
    bool revealing;

    static Card()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(typeof(Card)));
        TextElement.FontSizeProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((Card)d).UpdateMetrics()));
    }

    /// <summary>新建：页头、正文、页脚组合好，分隔线取 borderMuted。</summary>
    public Card()
    {
        loading = new DelayedLoading(ShowSkeleton);
        Bind(shadow, Border.BackgroundProperty, nameof(Background));
        Bind(shadow, EffectProperty, nameof(Shadow));
        Bind(surface, Border.BackgroundProperty, nameof(Background));
        Bind(surface, Border.BorderBrushProperty, nameof(BorderBrush));
        header.SetResourceReference(Border.BorderBrushProperty, "Rune.Brush.BorderMuted");
        footer.SetResourceReference(Border.BorderBrushProperty, "Rune.Brush.BorderMuted");
        header.SetResourceReference(StyleProperty, CardParts.Header);
        body.SetResourceReference(StyleProperty, CardParts.Body);
        footer.SetResourceReference(StyleProperty, CardParts.Footer);
        skeleton.SetResourceReference(StyleProperty, CardParts.Skeleton);
        headerRow.Children.Add(headerContent);
        headerRow.Children.Add(extraContent);
        header.Child = headerRow;
        body.Child = bodyContent;
        footerRow.Children.Add(footerContent);
        footer.Child = footerRow;
        layout.Children.Add(header);
        layout.Children.Add(body);
        layout.Children.Add(footer);
        surface.Child = layout;
        root.Children.Add(shadow);
        root.Children.Add(surface);
        AddVisualChild(root);
        AddLogicalChild(root);
        UpdateSlots();
        Loaded += (_, _) => ShowIfLoading();
        Unloaded += (_, _) => loading.Stop();

        void Bind(DependencyObject target, DependencyProperty property, string path) =>
            System.Windows.Data.BindingOperations.SetBinding(target, property, new System.Windows.Data.Binding(path) { Source = this });
    }

    /// <summary>样式。</summary>
    public CardVariant Variant { get => (CardVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>内边距档位。</summary>
    public Space Padding { get => (Space)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>页头。</summary>
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>页头结束边的内容。</summary>
    public object? Extra { get => GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }

    /// <summary>页脚。</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>正文加载中。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>正文。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>表面色。</summary>
    public Brush? Background { get => (Brush?)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>边框色。</summary>
    public Brush? BorderBrush { get => (Brush?)GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>阴影。</summary>
    public Effect? Shadow { get => (Effect?)GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    /// <summary>出现过骨架：之后骨架与正文互换时淡入。</summary>
    internal bool Revealing => revealing;

    /// <summary>骨架在显示吗（防闪烁之后的）。</summary>
    internal bool SkeletonShown => body.Child == skeleton;

    double Em => TextElement.GetFontSize(this);

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    void BindPadding()
    {
        if (Padding.ResourceKey is { } key) SetResourceReference(PaddingFactorProperty, key);
        else ClearValue(PaddingFactorProperty);
    }

    void BindRadius()
    {
        if (Radius.ResourceKey is { } key) SetResourceReference(RadiusFactorProperty, key);
        else ClearValue(RadiusFactorProperty);
    }

    /// <summary>页头（有 Header 或 Extra 时）、正文、页脚（有 Footer 时）。</summary>
    void UpdateSlots()
    {
        headerContent.Content = Header;
        extraContent.Content = Extra;
        extraContent.Visibility = Extra is null ? Visibility.Collapsed : Visibility.Visible;
        header.Visibility = Header is null && Extra is null ? Visibility.Collapsed : Visibility.Visible;
        bodyContent.Content = Content;
        footerContent.Content = Footer;
        footer.Visibility = Footer is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>内边距 = space 系数 × 字号；圆角 = radii 系数 × 字号（full 按高度的一半）。</summary>
    void UpdateMetrics()
    {
        var padding = new Thickness((double)GetValue(PaddingFactorProperty) * Em);
        header.Padding = body.Padding = footer.Padding = padding;
        var radius = new CornerRadius(Radius.Value == "full" ? 9999 : (double)GetValue(RadiusFactorProperty) * Em);
        surface.CornerRadius = shadow.CornerRadius = radius;
        InvalidateArrange();
    }

    /// <summary>挂上时已经在加载就立即显示骨架（没有内容可看，空白比骨架更糟）。</summary>
    void ShowIfLoading()
    {
        if (!IsLoading || SkeletonShown) return;
        loading.Stop();
        loading = new DelayedLoading(ShowSkeleton, loading: true);
        ShowSkeleton(true);
    }

    /// <summary>骨架与正文互换：出现过骨架之后都淡入（一开始就有正文的卡片不播）。</summary>
    void ShowSkeleton(bool shown)
    {
        UIElement next = shown ? skeleton : bodyContent;
        body.Child = next;
        if (shown) revealing = true;
        if (!revealing) return;
        var duration = Motion.Duration(this, "Rune.Duration.Lg", TimeSpan.FromMilliseconds(300));
        if (duration <= TimeSpan.Zero) return;
        next.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = TryFindResource("Rune.Easing.Enter") as IEasingFunction });
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        // 正文按圆角裁剪（同 CSS 的 overflow: hidden）
        var radius = Math.Min(surface.CornerRadius.TopLeft, Math.Min(finalSize.Width, finalSize.Height) / 2);
        layout.Clip = new RectangleGeometry(new Rect(new Size(Math.Max(0, finalSize.Width - 2), Math.Max(0, finalSize.Height - 2))), Math.Max(0, radius - 1), Math.Max(0, radius - 1));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CardAutomationPeer(this);

    /// <summary>卡片本身是一组内容；加载中时项状态是"加载中"（TS 的 aria-busy）。</summary>
    sealed class CardAutomationPeer(Card owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Card);

        protected override string GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();
    }
}

/// <summary>Card 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:CardParts.Header}" TargetType="Border"&gt;</c>。</summary>
public static class CardParts
{
    /// <summary>页头（TS 的 propsHeader）。</summary>
    public static readonly PartKey Header = new(typeof(Card), nameof(Header), typeof(Border));

    /// <summary>正文（TS 的 propsBody）。</summary>
    public static readonly PartKey Body = new(typeof(Card), nameof(Body), typeof(Border));

    /// <summary>页脚（TS 的 propsFooter）。</summary>
    public static readonly PartKey Footer = new(typeof(Card), nameof(Footer), typeof(Border));

    /// <summary>加载中的骨架（TS 的 propsSkeleton），是 SkeletonText：样式里改 Lines。</summary>
    public static readonly PartKey Skeleton = new(typeof(Card), nameof(Skeleton), typeof(SkeletonText));
}

/// <summary>卡片样式（vue-tsx-pro 的 CardVariants）。</summary>
/// <param name="Value">取值（surface、outline、elevated、ghost）。</param>
[TypeConverter(typeof(VariantConverter<CardVariant>))]
public readonly record struct CardVariant(string Value) : IVariant<CardVariant>
{
    /// <summary>表面色加细边框（默认）。</summary>
    public static readonly CardVariant Surface = new("surface");

    /// <summary>只有边框，底色透明。</summary>
    public static readonly CardVariant Outline = new("outline");

    /// <summary>表面色加阴影，浮在页面上。</summary>
    public static readonly CardVariant Elevated = new("elevated");

    /// <summary>没有底色与边框，只保留内边距与分区。</summary>
    public static readonly CardVariant Ghost = new("ghost");

    /// <inheritdoc />
    public static IReadOnlyList<CardVariant> Known { get; } = [Surface, Outline, Elevated, Ghost];

    /// <inheritdoc />
    public static CardVariant Parse(string value)
    {
        var variant = new CardVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
