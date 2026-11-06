using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 标记（vue-tsx-pro 的 Badge）：状态、计数、分类等短文字，只展示、不可交互（可关闭、可选择的用 Tag）。
/// <code>
/// &lt;rune:Badge Tone="success"&gt;已发货&lt;/rune:Badge&gt;
/// &lt;rune:Badge Variant="solid" Tone="danger" Radius="full"&gt;12&lt;/rune:Badge&gt;
/// &lt;rune:Badge Tone="warning" Dot="True"&gt;待审核&lt;/rune:Badge&gt;   &lt;!-- 文字前一个同色的圆点 --&gt;
/// </code>
/// 数量大的轻量部件（DESIGN §9）：不用模板，底色、描边、圆角在 OnRender 里画，图标、圆点、文字是代码里组合的子元素
/// （ROADMAP 决定 20）。图标部件引用部件样式键 <see cref="BadgeParts.Icon"/>，
/// Provider.Styles 里同键的样式照常叠上去。样式 × 色调的颜色（<see cref="StatusColors"/>）是主题资源的引用（本地值，受管）；
/// 默认的样式、色调、字号、圆角在主题样式里。尺寸都是 em：内边距 0.5em、间距 0.25em、最小高度 1.75em、圆点 0.5em。
/// </summary>
[ContentProperty(nameof(Content))]
public class Badge : Control
{
    /// <summary>样式，主题里默认 soft。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(StatusVariant), typeof(Badge), new FrameworkPropertyMetadata(default(StatusVariant), OnColorsChanged));

    /// <summary>色调，主题里默认 neutral。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(Badge), new FrameworkPropertyMetadata(default(StatusTone), OnColorsChanged));

    /// <summary>字号档位，主题里默认 xs。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(Badge), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    /// <summary>圆角，主题里默认 sm；表格、密集列表里的状态常用 none。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(Radius), typeof(Badge), new FrameworkPropertyMetadata(default(Radius), OnRadiusChanged));

    /// <summary>文字前的图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(IconData), typeof(Badge), new FrameworkPropertyMetadata(null, (d, _) => ((Badge)d).UpdateLeading()));

    /// <summary>文字前一个同色的圆点（没有图标时）：表格、列表里的状态，比整块底色更轻。</summary>
    public static readonly DependencyProperty DotProperty = DependencyProperty.Register(
        nameof(Dot), typeof(bool), typeof(Badge), new FrameworkPropertyMetadata(false, (d, _) => ((Badge)d).UpdateLeading()));

    /// <summary>内容：文字（放进一个 Text）或元素。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(Badge), new FrameworkPropertyMetadata(null, (d, _) => ((Badge)d).UpdateContent()));

    /// <summary>圆角档位从主题取到的 em 系数（资源引用，主题换了跟着变）。</summary>
    static readonly DependencyProperty RadiusCoefficientProperty = DependencyProperty.Register(
        "RadiusCoefficient", typeof(double), typeof(Badge), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>描边宽度：borderWidths.xs（设备无关像素，不随字号）。没有描边色时边框透明，同样占位，各样式等高。</summary>
    const double BorderWidth = 1;

    readonly List<UIElement> parts = [];
    Icon? iconPart;
    Border? dotPart;
    UIElement? contentPart;

    static Badge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(typeof(Badge)));
        FocusableProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(false));
        // 底色、描边是自己画的：Control 的这两个属性元数据里不带 AffectsRender
        BackgroundProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        BorderBrushProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    }

    /// <summary>样式。</summary>
    public StatusVariant Variant { get => (StatusVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>文字前的圆点。</summary>
    public bool Dot { get => (bool)GetValue(DotProperty); set => SetValue(DotProperty, value); }

    /// <summary>内容。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <inheritdoc />
    protected override int VisualChildrenCount => parts.Count;

    double Em => FontSize;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => parts[index];

    static void OnColorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var badge = (Badge)d;
        var colors = StatusColors.Of(badge.Variant, badge.Tone);
        badge.ReferenceResource(ForegroundProperty, colors.Foreground);
        badge.ReferenceResource(BackgroundProperty, colors.Background);
        badge.ReferenceResource(BorderBrushProperty, colors.Border);
    }

    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var badge = (Badge)d;
        badge.ReferenceResource(FontSizeProperty, badge.Size.ResourceKey);
        if (badge.Size.Pixels is { } pixels) badge.SetValue(FontSizeProperty, pixels);
    }

    static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((Badge)d).ReferenceResource(RadiusCoefficientProperty, ((Badge)d).Radius.ResourceKey);

    void ReferenceResource(DependencyProperty property, string? key)
    {
        if (key is null) ClearValue(property);
        else SetResourceReference(property, key);
    }

    /// <summary>文字前面：有图标时是图标部件，否则有 Dot 时是圆点。</summary>
    void UpdateLeading()
    {
        if (Icon is { } data)
        {
            if (iconPart is null)
            {
                iconPart = Adopt(new Icon { VerticalAlignment = VerticalAlignment.Center }, 0);
                iconPart.SetResourceReference(StyleProperty, BadgeParts.Icon);
            }
            iconPart.Data = data;
        }
        else iconPart = Release(iconPart);

        if (Icon is null && Dot)
        {
            if (dotPart is null)
            {
                dotPart = Adopt(new Border { Name = "PART_Dot", VerticalAlignment = VerticalAlignment.Center }, 0);
                dotPart.SetResourceReference(StyleProperty, BadgeParts.Dot);
            }
            dotPart.SetBinding(Border.BackgroundProperty, new Binding(nameof(Foreground)) { Source = this });
        }
        else dotPart = Release(dotPart);
    }

    void UpdateContent()
    {
        Release(contentPart);
        contentPart = Content switch
        {
            null => null,
            UIElement element => Adopt(element, parts.Count),
            var value => Adopt(new Text { Text = value.ToString(), Weight = TextWeight.Medium, Wrap = TextWrap.NoWrap, VerticalAlignment = VerticalAlignment.Center }, parts.Count),
        };
    }

    T Adopt<T>(T child, int index) where T : UIElement
    {
        parts.Insert(index, child);
        AddVisualChild(child);
        AddLogicalChild(child);
        InvalidateMeasure();
        return child;
    }

    T? Release<T>(T? child) where T : UIElement
    {
        if (child is null) return null;
        parts.Remove(child);
        RemoveLogicalChild(child);
        RemoveVisualChild(child);
        InvalidateMeasure();
        return null;
    }

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => parts.GetEnumerator();

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        if (dotPart is not null)
        {
            dotPart.Width = dotPart.Height = 0.5 * Em;
            dotPart.CornerRadius = new CornerRadius(0.25 * Em);
        }
        double width = 0, height = 0;
        foreach (var part in parts)
        {
            part.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            width += part.DesiredSize.Width;
            height = Math.Max(height, part.DesiredSize.Height);
        }
        var gaps = Math.Max(0, parts.Count - 1) * 0.25 * Em;
        return new Size(width + gaps + 2 * (0.5 * Em + BorderWidth), Math.Max(1.75 * Em, height + 2 * BorderWidth));
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var x = 0.5 * Em + BorderWidth;
        foreach (var part in parts)
        {
            var size = part.DesiredSize;
            part.Arrange(new Rect(x, (arrangeBounds.Height - size.Height) / 2, size.Width, size.Height));
            x += size.Width + 0.25 * Em;
        }
        return arrangeBounds;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        var bounds = new Rect(RenderSize);
        bounds.Inflate(-BorderWidth / 2, -BorderWidth / 2);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var radius = Math.Min((double)GetValue(RadiusCoefficientProperty) * Em, bounds.Height / 2);
        var pen = BorderBrush is { } border ? new Pen(border, BorderWidth) : null;
        drawingContext.DrawRoundedRectangle(Background, pen, bounds, radius, radius);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BadgeAutomationPeer(this);

    /// <summary>标记本身不是控件元素：读屏直接读里面的文字（同 TS 的 span）。</summary>
    sealed class BadgeAutomationPeer(Badge owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Badge);

        protected override bool IsControlElementCore() => false;
    }
}

/// <summary>Badge 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:BadgeParts.Icon}" TargetType="rune:Icon"&gt;</c>。</summary>
public static class BadgeParts
{
    /// <summary>文字前的图标（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(Badge), nameof(Icon), typeof(Icon));

    /// <summary>文字前的圆点（TS 的 propsDot），名字是 PART_Dot 的 Border。</summary>
    public static readonly PartKey Dot = new(typeof(Badge), nameof(Dot), typeof(Border));
}
