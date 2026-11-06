using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 角标（vue-tsx-pro 的 Indicator）：叠在头像、图标按钮角上的计数或小圆点（未读消息、在线状态）。
/// <code>
/// &lt;rune:Indicator Count="{Binding Unread}"&gt;&lt;rune:IconButton Icon="{x:Static rune:Lucide.Bell}" AutomationProperties.Name="通知" /&gt;&lt;/rune:Indicator&gt;
/// &lt;rune:Indicator Dot="True" Tone="success" Placement="bottom-end" Overlap="circular" Label="在线"&gt;……&lt;/rune:Indicator&gt;
/// </code>
/// 大小就是被标记元素的大小，角标的中心对准它的角（同 TS 的绝对定位，不占位置）；圆形元素上收进 14.6%。
/// 计数超过 <see cref="Max"/> 显示"99+"；Count 为 0（且没有 ShowZero）、也没有 Dot 时不显示角标。
/// 色调的颜色是生成的 IndicatorRecipe.g.xaml（与 Badge 的 solid 同一张调色板表），设在 <see cref="BadgeBackground"/>、<see cref="BadgeForeground"/> 上。
/// 读屏：角标是一段文字，名称是 <see cref="Label"/>（默认取语言包的"N 条新消息""有新消息"），数字本身不念。
/// 部件样式键 <see cref="IndicatorParts.Badge"/>（Border）、<see cref="IndicatorParts.Count"/>（TextBlock）。
/// </summary>
[ContentProperty(nameof(Content))]
public class Indicator : Control
{
    /// <summary>计数；超过 Max 显示"Max+"。</summary>
    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(
        nameof(Count), typeof(int?), typeof(Indicator), new FrameworkPropertyMetadata(null, OnBadgeChanged));

    /// <summary>计数的上限，默认 99。</summary>
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(
        nameof(Max), typeof(int), typeof(Indicator), new FrameworkPropertyMetadata(99, OnBadgeChanged));

    /// <summary>只显示小圆点（不显示数字）。</summary>
    public static readonly DependencyProperty DotProperty = DependencyProperty.Register(
        nameof(Dot), typeof(bool), typeof(Indicator), new FrameworkPropertyMetadata(false, OnBadgeChanged));

    /// <summary>Count 为 0 时也显示。</summary>
    public static readonly DependencyProperty ShowZeroProperty = DependencyProperty.Register(
        nameof(ShowZero), typeof(bool), typeof(Indicator), new FrameworkPropertyMetadata(false, OnBadgeChanged));

    /// <summary>色调，主题里默认 danger。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(Indicator), new FrameworkPropertyMetadata(default(StatusTone)));

    /// <summary>位置，主题里默认 top-end。</summary>
    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
        nameof(Placement), typeof(IndicatorPlacement), typeof(Indicator), new FrameworkPropertyMetadata(default(IndicatorPlacement), FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>被标记元素的形状，主题里默认 rectangular。</summary>
    public static readonly DependencyProperty OverlapProperty = DependencyProperty.Register(
        nameof(Overlap), typeof(IndicatorOverlap), typeof(Indicator), new FrameworkPropertyMetadata(default(IndicatorOverlap), FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>给读屏的说明；不写时取语言包。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(Indicator), new FrameworkPropertyMetadata(null, OnBadgeChanged));

    /// <summary>被标记的元素。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(UIElement), typeof(Indicator), new FrameworkPropertyMetadata(null, (d, e) => ((Indicator)d).ReplaceContent((UIElement?)e.OldValue, (UIElement?)e.NewValue)));

    /// <summary>角标的底色（色调的实底，主题样式的触发器按 Tone 设）。</summary>
    public static readonly DependencyProperty BadgeBackgroundProperty = DependencyProperty.Register(
        nameof(BadgeBackground), typeof(Brush), typeof(Indicator), new FrameworkPropertyMetadata(null));

    /// <summary>角标里计数的颜色（色调的反色）。</summary>
    public static readonly DependencyProperty BadgeForegroundProperty = DependencyProperty.Register(
        nameof(BadgeForeground), typeof(Brush), typeof(Indicator), new FrameworkPropertyMetadata(null));

    readonly IndicatorBadge badge = new() { Name = "PART_Badge" };
    readonly TextBlock count = new() { Name = "PART_Count", TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly List<UIElement> parts = [];

    static Indicator()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Indicator), new FrameworkPropertyMetadata(typeof(Indicator)));
        FocusableProperty.OverrideMetadata(typeof(Indicator), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Indicator), new FrameworkPropertyMetadata(false));
    }

    /// <summary>新建：角标叠在最上面，圈取页面底色。</summary>
    public Indicator()
    {
        badge.SetResourceReference(StyleProperty, IndicatorParts.Badge);
        count.SetResourceReference(StyleProperty, IndicatorParts.Count);
        badge.SetBinding(Border.BackgroundProperty, new Binding(nameof(BadgeBackground)) { Source = this });
        count.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(BadgeForeground)) { Source = this });
        // 一圈页面底色（borderWidths.sm）：角标外面多 2px 的边，和被标记的元素分开
        badge.SetResourceReference(Border.BorderBrushProperty, "Rune.Brush.Background");
        badge.BorderThickness = new Thickness(Ring);
        count.FontWeight = FontWeights.SemiBold;
        System.Windows.Documents.Typography.SetNumeralAlignment(count, FontNumeralAlignment.Tabular);
        badge.Child = count;
        parts.Add(badge);
        AddVisualChild(badge);
        AddLogicalChild(badge);
        UpdateBadge();
    }

    const double Ring = 2;

    /// <summary>计数。</summary>
    public int? Count { get => (int?)GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <summary>计数的上限。</summary>
    public int Max { get => (int)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>只显示小圆点。</summary>
    public bool Dot { get => (bool)GetValue(DotProperty); set => SetValue(DotProperty, value); }

    /// <summary>Count 为 0 时也显示。</summary>
    public bool ShowZero { get => (bool)GetValue(ShowZeroProperty); set => SetValue(ShowZeroProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>位置。</summary>
    public IndicatorPlacement Placement { get => (IndicatorPlacement)GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>被标记元素的形状。</summary>
    public IndicatorOverlap Overlap { get => (IndicatorOverlap)GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }

    /// <summary>给读屏的说明。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>被标记的元素。</summary>
    public UIElement? Content { get => (UIElement?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>角标的底色。</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Brush? BadgeBackground { get => (Brush?)GetValue(BadgeBackgroundProperty); set => SetValue(BadgeBackgroundProperty, value); }

    /// <summary>角标里计数的颜色。</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Brush? BadgeForeground { get => (Brush?)GetValue(BadgeForegroundProperty); set => SetValue(BadgeForegroundProperty, value); }

    /// <summary>角标显示吗：有 Dot，或计数大于 0（ShowZero 时 0 也算）。</summary>
    bool Visible => Dot || Count is { } n && (n > 0 || ShowZero);

    /// <summary>读屏念的说明。</summary>
    internal string Description => Label ?? (Dot ? Provider.GetLang(this).IndicatorDot : Provider.GetLang(this).IndicatorCount(Count ?? 0));

    /// <inheritdoc />
    protected override int VisualChildrenCount => parts.Count;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => parts[index];

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => parts.GetEnumerator();

    static void OnBadgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Indicator)d).UpdateBadge();

    void ReplaceContent(UIElement? old, UIElement? content)
    {
        if (old is not null)
        {
            parts.Remove(old);
            RemoveLogicalChild(old);
            RemoveVisualChild(old);
        }
        if (content is not null)
        {
            // 被标记的元素在下面，角标叠在上面
            parts.Insert(0, content);
            AddVisualChild(content);
            AddLogicalChild(content);
        }
        InvalidateMeasure();
    }

    void UpdateBadge()
    {
        badge.Visibility = Visible ? Visibility.Visible : Visibility.Collapsed;
        count.Visibility = Dot ? Visibility.Collapsed : Visibility.Visible;
        count.Text = Count is not { } n ? "" : n > Max ? $"{Max}+" : n.ToString(CultureInfo.CurrentCulture);
        if (Visible) AutomationProperties.SetName(badge, Description);
        else badge.ClearValue(AutomationProperties.NameProperty);
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        var em = FontSize;
        if (Dot)
        {
            badge.Width = badge.Height = 0.625 * em + 2 * Ring;
            badge.MinWidth = 0;
            badge.Padding = default;
        }
        else
        {
            // 计数的字号是 0.75em，最小宽、高 1.6 倍、左右内边距 0.4 倍（都按计数自己的字号），行高 1
            var size = 0.75 * em;
            count.FontSize = count.LineHeight = size;
            count.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            badge.Width = double.NaN;
            badge.Height = 1.6 * size + 2 * Ring;
            badge.MinWidth = 1.6 * size + 2 * Ring;
            badge.Padding = new Thickness(0.4 * size, 0, 0.4 * size, 0);
        }
        badge.CornerRadius = new CornerRadius(badge.Height / 2);
        badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        if (Content is null) return default;
        Content.Measure(constraint);
        return Content.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        Content?.Arrange(new Rect(arrangeBounds));
        // 角标的中心对准角（按书写方向：从右到左时 WPF 整体镜像）；圆形元素收进宽度的 14.6%（1 - 1/√2 的一半）
        var inset = Overlap == IndicatorOverlap.Circular ? 0.146 * arrangeBounds.Width : 0;
        var (end, bottom) = (Placement.Value is "top-end" or "bottom-end" or null, Placement.Value is "bottom-end" or "bottom-start");
        var x = end ? arrangeBounds.Width - inset : inset;
        var y = bottom ? arrangeBounds.Height - inset : inset;
        var size = badge.DesiredSize;
        badge.Arrange(new Rect(x - size.Width / 2, y - size.Height / 2, size.Width, size.Height));
        return arrangeBounds;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new IndicatorAutomationPeer(this);

    /// <summary>外层不是控件元素（同 TS 的 span）：读屏读被标记的元素，再读角标的说明。</summary>
    sealed class IndicatorAutomationPeer(Indicator owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Indicator);

        protected override bool IsControlElementCore() => false;
    }
}

/// <summary>角标：读屏里是一段文字，名称是说明，计数文字不念。外面那圈页面底色是它的边框（CSS 的 box-shadow 不占位置，这里多出来的 2px 在定位时算进去）。</summary>
sealed class IndicatorBadge : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new BadgeAutomationPeer(this);

    sealed class BadgeAutomationPeer(IndicatorBadge owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetClassNameCore() => "IndicatorBadge";

        protected override bool IsControlElementCore() => true;

        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>Indicator 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:IndicatorParts.Badge}" TargetType="Border"&gt;</c>。</summary>
public static class IndicatorParts
{
    /// <summary>角标（TS 的 propsBadge）。</summary>
    public static readonly PartKey Badge = new(typeof(Indicator), nameof(Badge), typeof(Border));

    /// <summary>角标里的计数文字（TS 的 propsCount），读屏不念。</summary>
    public static readonly PartKey Count = new(typeof(Indicator), nameof(Count), typeof(TextBlock));
}

/// <summary>角标在被标记元素的哪个角（vue-tsx-pro 的 IndicatorPlacements），按书写方向。</summary>
/// <param name="Value">取值（top-end、bottom-end、top-start、bottom-start）。</param>
[TypeConverter(typeof(VariantConverter<IndicatorPlacement>))]
public readonly record struct IndicatorPlacement(string Value) : IVariant<IndicatorPlacement>
{
    /// <summary>右上（默认；从右到左排版时左上）。</summary>
    public static readonly IndicatorPlacement TopEnd = new("top-end");

    /// <summary>右下：头像的在线状态。</summary>
    public static readonly IndicatorPlacement BottomEnd = new("bottom-end");

    /// <summary>左上。</summary>
    public static readonly IndicatorPlacement TopStart = new("top-start");

    /// <summary>左下。</summary>
    public static readonly IndicatorPlacement BottomStart = new("bottom-start");

    /// <inheritdoc />
    public static IReadOnlyList<IndicatorPlacement> Known { get; } = [TopEnd, BottomEnd, TopStart, BottomStart];

    /// <inheritdoc />
    public static IndicatorPlacement Parse(string value)
    {
        var placement = new IndicatorPlacement(value);
        Variants.WarnIfUnknown(placement);
        return placement;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>被标记元素的形状（vue-tsx-pro 的 IndicatorOverlaps）：决定角标往里收多少。</summary>
/// <param name="Value">取值（rectangular、circular）。</param>
[TypeConverter(typeof(VariantConverter<IndicatorOverlap>))]
public readonly record struct IndicatorOverlap(string Value) : IVariant<IndicatorOverlap>
{
    /// <summary>方形（默认）：角标中心在角上。</summary>
    public static readonly IndicatorOverlap Rectangular = new("rectangular");

    /// <summary>圆形（圆形头像）：角标收到圆的边上。</summary>
    public static readonly IndicatorOverlap Circular = new("circular");

    /// <inheritdoc />
    public static IReadOnlyList<IndicatorOverlap> Known { get; } = [Rectangular, Circular];

    /// <inheritdoc />
    public static IndicatorOverlap Parse(string value)
    {
        var overlap = new IndicatorOverlap(value);
        Variants.WarnIfUnknown(overlap);
        return overlap;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
