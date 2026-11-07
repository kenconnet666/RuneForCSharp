using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Styling;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 卡片（vue-tsx-pro 的 Card）：把一组相关内容放在一块表面上。可选的页头、页脚与正文之间有分隔线。
/// <code>
/// &lt;rune:Card Header="订单 #1024"&gt;…&lt;/rune:Card&gt;
/// &lt;rune:Card Header="超时最久的项目" Extra="{…全部}"&gt;…&lt;/rune:Card&gt;   &lt;!-- 页头结束边的操作，放不下时换行 --&gt;
/// &lt;rune:Card IsLoading="{Binding Loading}" Header="…"&gt;…&lt;/rune:Card&gt;     &lt;!-- 正文换成几行扫光的骨架 --&gt;
/// </code>
/// 样式（surface、outline、elevated、ghost）、内边距（space 档位，默认 lg）、圆角（默认 lg）的默认值在主题里，Provider 的样式能改。
/// 加载中正文换成骨架（页头、页脚照常显示），很快完成时不闪（<see cref="DelayedLoading"/>）；出现过骨架之后，骨架与正文互换时淡入。
/// 加载时读屏的项状态是"加载中"。部件是子元素：<c>rune|Card Border#PART_Header</c>、<c>#PART_Body</c>、<c>#PART_Footer</c>、
/// <c>rune|SkeletonText#PART_Skeleton</c>（行数用样式改 Lines）。TS 的装饰效果（tilt、spotlight、beam）还没做。
/// </summary>
public class Card : Control
{
    /// <summary>样式，主题里默认 surface。</summary>
    public static readonly StyledProperty<CardVariant> VariantProperty = AvaloniaProperty.Register<Card, CardVariant>(nameof(Variant));

    /// <summary>页头、正文、页脚的内边距档位，主题里默认 lg。</summary>
    public static readonly StyledProperty<Space> PaddingProperty = AvaloniaProperty.Register<Card, Space>(nameof(Padding));

    /// <summary>圆角，主题里默认 lg。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<Card, Radius>(nameof(Radius));

    /// <summary>页头：标题、操作。</summary>
    public static readonly StyledProperty<object?> HeaderProperty = AvaloniaProperty.Register<Card, object?>(nameof(Header));

    /// <summary>页头结束边的内容（"全部"、按钮、筛选）。</summary>
    public static readonly StyledProperty<object?> ExtraProperty = AvaloniaProperty.Register<Card, object?>(nameof(Extra));

    /// <summary>页脚：按钮等操作，靠结束边排。</summary>
    public static readonly StyledProperty<object?> FooterProperty = AvaloniaProperty.Register<Card, object?>(nameof(Footer));

    /// <summary>正文加载中：换成骨架。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Card, bool>(nameof(IsLoading));

    /// <summary>正文。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Card, object?>(nameof(Content));

    /// <summary>表面色（样式档位在主题里设）。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<Card>();

    /// <summary>边框色（样式档位在主题里设；没有边框的样式也占 1px）。</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty = Border.BorderBrushProperty.AddOwner<Card>();

    /// <summary>阴影（elevated）。</summary>
    public static readonly StyledProperty<BoxShadows> BoxShadowProperty = Border.BoxShadowProperty.AddOwner<Card>();

    /// <summary>字号（继承），内边距、圆角按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Card>();

    static readonly StyledProperty<double> PaddingFactorProperty = AvaloniaProperty.Register<Card, double>("PaddingFactor");
    static readonly StyledProperty<double> RadiusFactorProperty = AvaloniaProperty.Register<Card, double>("RadiusFactor");

    readonly Border surface = new() { BorderThickness = new Thickness(1), ClipToBounds = true };
    readonly StackPanel layout = new();
    readonly Border header = new() { Name = CardParts.Header };
    readonly Flex headerRow = new() { Wrap = true, Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm };
    readonly ContentPresenter headerContent = new();
    readonly ContentPresenter extraContent = new();
    readonly Border body = new() { Name = CardParts.Body };
    readonly ContentPresenter bodyContent = new();
    readonly SkeletonText skeleton = new() { Name = CardParts.Skeleton };
    readonly Border footer = new() { Name = CardParts.Footer };
    readonly Flex footerRow = new() { Justify = FlexJustify.End, Gap = Space.Sm };
    readonly ContentPresenter footerContent = new();
    readonly TemplateResources resources;
    DelayedLoading loading;
    bool revealing;

    static Card() => AffectsMeasure<Card>(PaddingFactorProperty, RadiusFactorProperty, FontSizeProperty);

    /// <summary>新建：页头、正文、页脚组合好，分隔线取 borderMuted。</summary>
    public Card()
    {
        resources = new TemplateResources(this);
        loading = new DelayedLoading(ShowSkeleton);
        surface.Bind(Border.BackgroundProperty, this.GetObservable(BackgroundProperty));
        surface.Bind(Border.BorderBrushProperty, this.GetObservable(BorderBrushProperty));
        surface.Bind(Border.BoxShadowProperty, this.GetObservable(BoxShadowProperty));
        headerRow.Children.AddRange([headerContent, extraContent]);
        header.Child = headerRow;
        // 页头的操作链接（"全部"）悬停才有下划线；正文里的链接照旧（上游 Card 页头的 LinkUnderlineScope）
        Link.SetUnderlineScope(header, LinkUnderline.Hover);
        body.Child = bodyContent;
        footerRow.Children.Add(footerContent);
        footer.Child = footerRow;
        layout.Children.AddRange([header, body, footer]);
        surface.Child = layout;
        LogicalChildren.Add(surface);
        VisualChildren.Add(surface);
        UpdateSlots();
    }

    /// <summary>样式。</summary>
    public CardVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>内边距档位（TS 的 padding；Card 不从 TemplatedControl 派生，没有 Thickness 的 Padding）。</summary>
    public Space Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>页头。</summary>
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>页头结束边的内容。</summary>
    public object? Extra { get => GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }

    /// <summary>页脚。</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>正文加载中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>正文。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>表面色。</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>边框色。</summary>
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>阴影。</summary>
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>出现过骨架：之后骨架与正文互换时淡入。</summary>
    internal bool Revealing => revealing;

    /// <summary>骨架在显示吗（防闪烁之后的）。</summary>
    internal bool SkeletonShown => body.Child == skeleton;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PaddingProperty) resources.Bind(PaddingFactorProperty, Padding.ResourceKey);
        else if (change.Property == RadiusProperty) resources.Bind(RadiusFactorProperty, Radius.ResourceKey);
        else if (change.Property == HeaderProperty || change.Property == ExtraProperty || change.Property == FooterProperty || change.Property == ContentProperty) UpdateSlots();
        else if (change.Property == IsLoadingProperty) loading.Update(IsLoading);
        else if (change.Property == PaddingFactorProperty || change.Property == RadiusFactorProperty || change.Property == FontSizeProperty) UpdateMetrics();
    }

    /// <summary>页头（有 Header 或 Extra 时）、正文、页脚（有 Footer 时）。</summary>
    void UpdateSlots()
    {
        headerContent.Content = Header;
        extraContent.Content = Extra;
        extraContent.IsVisible = Extra is not null;
        header.IsVisible = Header is not null || Extra is not null;
        bodyContent.Content = Content;
        footerContent.Content = Footer;
        // 页脚是布局容器（一组按钮、两端对齐的一行）时撑满一行由它自己排，单个控件靠结束边（TS 的页脚是 justify-content: flex-end 的一行）
        Flex.SetGrow(footerContent, Footer is Panel);
        footer.IsVisible = Footer is not null;
    }

    /// <summary>内边距 = space 系数 × 字号；圆角 = radii 系数 × 字号（full 按高度的一半）。</summary>
    void UpdateMetrics()
    {
        var padding = new Thickness(GetValue(PaddingFactorProperty) * FontSize);
        header.Padding = body.Padding = footer.Padding = padding;
        surface.CornerRadius = new CornerRadius(Radius.Value == "full" ? 9999 : GetValue(RadiusFactorProperty) * FontSize);
    }

    /// <summary>骨架与正文互换：出现过骨架之后都淡入（一开始就有正文的卡片不播）。</summary>
    void ShowSkeleton(bool shown)
    {
        body.Child = shown ? skeleton : bodyContent;
        if (shown) revealing = true;
        if (!revealing) return;
        var duration = Motion.Duration(this, "Rune.Duration.Lg", TimeSpan.FromMilliseconds(300));
        if (duration <= TimeSpan.Zero) return;
        var easing = this.Resource<Easing>("Rune.Easing.Enter", new CubicEaseOut());
        _ = new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1d) } },
            },
        }.RunAsync(body.Child);
    }

    /// <summary>挂上时已经在加载就立即显示骨架（没有内容可看，空白比骨架更糟）。</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!IsLoading || SkeletonShown) return;
        loading.Stop();
        loading = new DelayedLoading(ShowSkeleton, loading: true);
        ShowSkeleton(true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        loading.Stop();
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        surface.Measure(availableSize);
        return surface.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        surface.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CardAutomationPeer(this);

    /// <summary>卡片本身是一组内容（读屏念页头）；加载中时项状态是"加载中"（TS 的 aria-busy）。</summary>
    sealed class CardAutomationPeer(Card owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();
    }
}

/// <summary>Card 的部件名：样式用 <c>rune|Card Border#PART_Header</c> 这样的后代选择器。</summary>
public static class CardParts
{
    /// <summary>页头（TS 的 propsHeader）。</summary>
    public const string Header = "PART_Header";

    /// <summary>正文（TS 的 propsBody）。</summary>
    public const string Body = "PART_Body";

    /// <summary>页脚（TS 的 propsFooter）。</summary>
    public const string Footer = "PART_Footer";

    /// <summary>加载中的骨架（TS 的 propsSkeleton），是 SkeletonText。</summary>
    public const string Skeleton = "PART_Skeleton";
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
