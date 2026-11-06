using global::Avalonia;
using global::Avalonia.Automation.Peers;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Metadata;
using LayoutSize = global::Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 标记（vue-tsx-pro 的 Badge）：状态、计数、分类等短文字，只展示、不可交互（可关闭、可选择的用 Tag）。
/// <code>
/// &lt;rune:Badge Tone="success"&gt;已发货&lt;/rune:Badge&gt;
/// &lt;rune:Badge Variant="solid" Tone="danger" Radius="full"&gt;12&lt;/rune:Badge&gt;
/// &lt;rune:Badge Tone="warning" Dot="True"&gt;待审核&lt;/rune:Badge&gt;   &lt;!-- 文字前一个同色的圆点 --&gt;
/// </code>
/// 数量大的轻量部件（DESIGN §9）：不用模板，底色、描边、圆角在 Render 里画，图标、圆点、文字是代码里组合的子元素
/// （ROADMAP 决定 20）。部件是真的元素：<c>rune|Badge &gt; rune|Icon</c>、
/// <c>rune|Badge &gt; Border#PART_Dot</c> 的样式能改它们。
/// 样式 × 色调决定的颜色（<see cref="StatusColors"/>）在代码里绑定主题资源，是受管的；默认的样式、色调、字号、圆角在主题里，
/// Provider 的样式能改。尺寸都是 em：内边距 0.5em、间距 0.25em、最小高度 1.75em、圆点 0.5em，圆角取主题的 radii。
/// </summary>
public class Badge : Control
{
    /// <summary>样式，主题里默认 soft。</summary>
    public static readonly StyledProperty<StatusVariant> VariantProperty = AvaloniaProperty.Register<Badge, StatusVariant>(nameof(Variant));

    /// <summary>色调，主题里默认 neutral。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Badge, StatusTone>(nameof(Tone));

    /// <summary>字号档位，主题里默认 xs。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Badge, TextSize>(nameof(Size));

    /// <summary>圆角，主题里默认 sm；表格、密集列表里的状态常用 none。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<Badge, Radius>(nameof(Radius));

    /// <summary>文字前的图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<Badge, IconData?>(nameof(Icon));

    /// <summary>文字前一个同色的圆点（没有图标时）：表格、列表里的状态，比整块底色更轻。</summary>
    public static readonly StyledProperty<bool> DotProperty = AvaloniaProperty.Register<Badge, bool>(nameof(Dot));

    /// <summary>内容：文字（放进一个 Text）或控件。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Badge, object?>(nameof(Content));

    /// <summary>文字与图标的颜色（继承给子元素）。</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = global::Avalonia.Controls.Documents.TextElement.ForegroundProperty.AddOwner<Badge>();

    /// <summary>字号（继承给子元素），em 尺寸按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = global::Avalonia.Controls.Documents.TextElement.FontSizeProperty.AddOwner<Badge>();

    /// <summary>圆角档位从主题取到的 em 系数（主题与作用域换了跟着变）。</summary>
    static readonly StyledProperty<double> RadiusCoefficientProperty = AvaloniaProperty.Register<Badge, double>("RadiusCoefficient");

    static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<Badge, IBrush?>("Background");

    static readonly StyledProperty<IBrush?> BorderBrushProperty = AvaloniaProperty.Register<Badge, IBrush?>("BorderBrush");

    readonly Dictionary<AvaloniaProperty, IDisposable> resourceBindings = [];
    Icon? iconPart;
    Border? dotPart;
    Control? contentPart;

    static Badge()
    {
        AffectsRender<Badge>(BackgroundProperty, BorderBrushProperty, RadiusCoefficientProperty);
        AffectsMeasure<Badge>(FontSizeProperty);
    }

    /// <summary>样式。</summary>
    public StatusVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>文字前的圆点。</summary>
    public bool Dot { get => GetValue(DotProperty); set => SetValue(DotProperty, value); }

    /// <summary>内容。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>文字与图标的颜色。</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    double Em => FontSize;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty || change.Property == ToneProperty)
        {
            var colors = StatusColors.Of(Variant, Tone);
            BindResource(ForegroundProperty, colors.Foreground);
            BindResource(BackgroundProperty, colors.Background);
            BindResource(BorderBrushProperty, colors.Border);
        }
        else if (change.Property == SizeProperty)
        {
            if (change.GetOldValue<TextSize>().Pixels is not null) ClearValue(FontSizeProperty);
            BindResource(FontSizeProperty, Size.ResourceKey);
            if (Size.Pixels is { } pixels) SetValue(FontSizeProperty, pixels);
        }
        else if (change.Property == RadiusProperty) BindResource(RadiusCoefficientProperty, Radius.ResourceKey);
        else if (change.Property == IconProperty || change.Property == DotProperty) UpdateLeading();
        else if (change.Property == ContentProperty) UpdateContent();
    }

    /// <summary>文字前面：有图标时是图标部件，否则有 Dot 时是圆点部件。</summary>
    void UpdateLeading()
    {
        if (Icon is { } data)
        {
            iconPart ??= Adopt(new Icon { Name = BadgeParts.Icon, VerticalAlignment = VerticalAlignment.Center });
            iconPart.Data = data;
        }
        else iconPart = Release(iconPart);

        if (Icon is null && Dot)
        {
            dotPart ??= Adopt(new Border { Name = BadgeParts.Dot, VerticalAlignment = VerticalAlignment.Center });
            dotPart.Bind(Border.BackgroundProperty, this.GetObservable(ForegroundProperty));
        }
        else dotPart = Release(dotPart);
    }

    void UpdateContent()
    {
        Release(contentPart);
        contentPart = Content switch
        {
            null => null,
            Control control => Adopt(control),
            var value => Adopt(new Text { Text = value.ToString(), Weight = TextWeight.Medium, Wrap = TextWrap.NoWrap, VerticalAlignment = VerticalAlignment.Center }),
        };
    }

    T Adopt<T>(T child) where T : Control
    {
        LogicalChildren.Add(child);
        VisualChildren.Add(child);
        InvalidateMeasure();
        return child;
    }

    T? Release<T>(T? child) where T : Control
    {
        if (child is null) return null;
        LogicalChildren.Remove(child);
        VisualChildren.Remove(child);
        InvalidateMeasure();
        return null;
    }

    IEnumerable<Control> Parts()
    {
        if (iconPart is not null) yield return iconPart;
        if (dotPart is not null) yield return dotPart;
        if (contentPart is not null) yield return contentPart;
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        if (dotPart is not null)
        {
            dotPart.Width = dotPart.Height = 0.5 * Em;
            dotPart.CornerRadius = new CornerRadius(0.25 * Em);
        }
        double width = 0, height = 0;
        foreach (var part in Parts())
        {
            part.Measure(LayoutSize.Infinity);
            width += part.DesiredSize.Width;
            height = Math.Max(height, part.DesiredSize.Height);
        }
        var gaps = Math.Max(0, Parts().Count() - 1) * 0.25 * Em;
        return new LayoutSize(width + gaps + 2 * (0.5 * Em + BorderWidth), Math.Max(1.75 * Em, height + 2 * BorderWidth));
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        var x = 0.5 * Em + BorderWidth;
        foreach (var part in Parts())
        {
            var size = part.DesiredSize;
            part.Arrange(new Rect(x, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
            x += size.Width + 0.25 * Em;
        }
        return finalSize;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size).Deflate(BorderWidth / 2);
        var radius = Math.Min(GetValue(RadiusCoefficientProperty) * Em, bounds.Height / 2);
        var pen = GetValue(BorderBrushProperty) is { } border ? new Pen(border, BorderWidth) : null;
        context.DrawRectangle(GetValue(BackgroundProperty), pen, bounds, radius, radius);
    }

    /// <summary>描边宽度：borderWidths.xs（设备无关像素，不随字号）。没有描边色时边框透明，同样占位，各样式等高。</summary>
    const double BorderWidth = 1;

    void BindResource(AvaloniaProperty property, string? key)
    {
        if (resourceBindings.Remove(property, out var old))
        {
            old.Dispose();
            ClearValue(property);
        }
        if (key is not null) resourceBindings[property] = Bind(property, this.GetResourceObservable(key));
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BadgeAutomationPeer(this);

    /// <summary>标记本身不是控件元素：读屏直接读里面的文字（同 TS 的 span）。</summary>
    sealed class BadgeAutomationPeer(Badge owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;
    }
}

/// <summary>Badge 的部件名：自绘组件的部件是子元素，样式用子选择器 <c>rune|Badge &gt; rune|Icon#PART_Icon</c>。</summary>
public static class BadgeParts
{
    /// <summary>文字前的图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>文字前的圆点（TS 的 propsDot）。</summary>
    public const string Dot = "PART_Dot";
}
