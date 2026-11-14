using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 角标（vue-tsx-pro 的 Indicator）：叠在头像、图标按钮角上的计数或小圆点（未读消息、在线状态）。
/// <code>
/// &lt;rune:Indicator Count="{Binding Unread}"&gt;&lt;rune:IconButton Icon="{x:Static rune:Lucide.Bell}" AutomationProperties.Name="通知" /&gt;&lt;/rune:Indicator&gt;
/// &lt;rune:Indicator Dot="True" Tone="success" Placement="bottom-end" Overlap="circular" Label="在线"&gt;……&lt;/rune:Indicator&gt;
/// </code>
/// 大小就是被标记元素的大小，角标的中心对准它的角（同 TS 的绝对定位，不占位置）；圆形元素上收进 14.6%。
/// 计数超过 <see cref="Max"/> 显示"99+"；Count 为 0（且没有 ShowZero）、也没有 Dot 时不显示角标。
/// 读屏：角标是一段文字，名称是 <see cref="Label"/>（默认取语言包的"N 条新消息""有新消息"），数字本身不念。
/// 部件是子元素：<c>rune|Indicator &gt; Border#PART_Badge</c>、<c>rune|Indicator TextBlock#PART_Count</c>。
/// </summary>
public class Indicator : Control
{
    /// <summary>计数；超过 Max 显示"Max+"。</summary>
    public static readonly StyledProperty<int?> CountProperty = AvaloniaProperty.Register<Indicator, int?>(nameof(Count));

    /// <summary>计数的上限，默认 99。</summary>
    public static readonly StyledProperty<int> MaxProperty = AvaloniaProperty.Register<Indicator, int>(nameof(Max), 99);

    /// <summary>只显示小圆点（不显示数字）。</summary>
    public static readonly StyledProperty<bool> DotProperty = AvaloniaProperty.Register<Indicator, bool>(nameof(Dot));

    /// <summary>Count 为 0 时也显示。</summary>
    public static readonly StyledProperty<bool> ShowZeroProperty = AvaloniaProperty.Register<Indicator, bool>(nameof(ShowZero));

    /// <summary>色调，主题里默认 danger。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Indicator, StatusTone>(nameof(Tone));

    /// <summary>位置，主题里默认 top-end。</summary>
    public static readonly StyledProperty<IndicatorPlacement> PlacementProperty = AvaloniaProperty.Register<Indicator, IndicatorPlacement>(nameof(Placement));

    /// <summary>被标记元素的形状，主题里默认 rectangular。</summary>
    public static readonly StyledProperty<IndicatorOverlap> OverlapProperty = AvaloniaProperty.Register<Indicator, IndicatorOverlap>(nameof(Overlap));

    /// <summary>给读屏的说明；不写时取语言包。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Indicator, string?>(nameof(Label));

    /// <summary>被标记的元素。</summary>
    public static readonly StyledProperty<Control?> ContentProperty = AvaloniaProperty.Register<Indicator, Control?>(nameof(Content));

    /// <summary>字号（继承），角标的 em 尺寸按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Indicator>();

    /// <summary>色调的调色板：主题里的 setter 按 <see cref="Tone"/> 绑定（<c>Rune.Palette.*</c>），角标取实底与反色。</summary>
    public static readonly StyledProperty<TonePalette?> PaletteProperty = AvaloniaProperty.Register<Indicator, TonePalette?>(nameof(Palette));

    static readonly StyledProperty<Color> RingProperty = AvaloniaProperty.Register<Indicator, Color>("Ring");

    readonly IndicatorBadge badge = new() { Name = IndicatorParts.Badge };
    readonly TextBlock count = new() { Name = IndicatorParts.Count };
    readonly TemplateResources resources;
    Control? contentPart;

    static Indicator()
    {
        AffectsMeasure<Indicator>(FontSizeProperty, CountProperty, MaxProperty, DotProperty, ShowZeroProperty);
        AffectsArrange<Indicator>(PlacementProperty, OverlapProperty);
    }

    /// <summary>新建：角标的圈取页面底色。</summary>
    public Indicator()
    {
        resources = new TemplateResources(this);
        resources.Bind(RingProperty, ThemeKeys.Color.Background);
        AutomationProperties.SetAccessibilityView(count, AccessibilityView.Raw);
        badge.Child = count;
        VisualChildren.Add(badge);
        LogicalChildren.Add(badge);
        UpdateBadge();
    }

    /// <summary>计数。</summary>
    public int? Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <summary>计数的上限。</summary>
    public int Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>只显示小圆点。</summary>
    public bool Dot { get => GetValue(DotProperty); set => SetValue(DotProperty, value); }

    /// <summary>Count 为 0 时也显示。</summary>
    public bool ShowZero { get => GetValue(ShowZeroProperty); set => SetValue(ShowZeroProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>位置。</summary>
    public IndicatorPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>被标记元素的形状。</summary>
    public IndicatorOverlap Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }

    /// <summary>给读屏的说明。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>被标记的元素。</summary>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>色调的调色板。</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public TonePalette? Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }

    /// <summary>角标显示吗：有 Dot，或计数大于 0（ShowZero 时 0 也算）。</summary>
    bool Visible => Dot || Count is { } n && (n > 0 || ShowZero);

    /// <summary>读屏念的说明。</summary>
    internal string Description => Label ?? (Dot ? Provider.GetLang(this).IndicatorDot : Provider.GetLang(this).IndicatorCount(Count ?? 0));

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty) Resolved.Bind(this, Resolved.PaletteProperty, Tone.PaletteKey);
        else if (change.Property == ContentProperty)
        {
            if (contentPart is not null)
            {
                VisualChildren.Remove(contentPart);
                LogicalChildren.Remove(contentPart);
            }
            contentPart = Content;
            if (contentPart is not null)
            {
                // 被标记的元素在下面，角标叠在上面
                VisualChildren.Insert(0, contentPart);
                LogicalChildren.Insert(0, contentPart);
            }
        }
        else if (change.Property == CountProperty || change.Property == MaxProperty || change.Property == DotProperty
                 || change.Property == ShowZeroProperty || change.Property == PaletteProperty || change.Property == RingProperty
                 || change.Property == LabelProperty) UpdateBadge();
    }

    void UpdateBadge()
    {
        badge.IsVisible = Visible;
        count.IsVisible = !Dot;
        count.Text = Count is not { } n ? "" : n > Max ? $"{Max}+" : n.ToString(System.Globalization.CultureInfo.CurrentCulture);
        // 一圈页面底色（borderWidths.sm），和被标记的元素分开
        badge.BoxShadow = new BoxShadows(new BoxShadow { Spread = 2, Color = GetValue(RingProperty) });
        AutomationProperties.SetName(badge, Visible ? Description : null);
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var em = FontSize;
        if (Dot)
        {
            badge.Width = badge.Height = 0.625 * em;
            badge.MinWidth = 0;
            badge.Padding = default;
        }
        else
        {
            // 计数的字号是 0.75em，最小宽、高 1.6 倍、左右内边距 0.4 倍（都按计数自己的字号），行高 1
            var size = 0.75 * em;
            count.FontSize = count.LineHeight = size;
            badge.Width = double.NaN;
            badge.Height = 1.6 * size;
            badge.MinWidth = 1.6 * size;
            badge.Padding = new Thickness(0.4 * size, 0);
            count.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        }
        badge.CornerRadius = new CornerRadius(badge.Height / 2);
        badge.Measure(LayoutSize.Infinity);
        if (contentPart is null) return default;
        contentPart.Measure(availableSize);
        return contentPart.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        contentPart?.Arrange(new Rect(finalSize));
        // 角标的中心对准角（按书写方向：从右到左时 Avalonia 整体镜像）；圆形元素收进宽度的 14.6%（1 - 1/√2 的一半）
        var inset = Overlap == IndicatorOverlap.Circular ? 0.146 * finalSize.Width : 0;
        var (end, bottom) = (Placement.Value is "top-end" or "bottom-end" or null, Placement.Value is "bottom-end" or "bottom-start");
        var x = end ? finalSize.Width - inset : inset;
        var y = bottom ? finalSize.Height - inset : inset;
        var size = badge.DesiredSize;
        badge.Arrange(new Rect(x - size.Width / 2, y - size.Height / 2, size.Width, size.Height));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new IndicatorAutomationPeer(this);

    /// <summary>外层不是控件元素（同 TS 的 span）：读屏读被标记的元素，再读角标的说明。</summary>
    sealed class IndicatorAutomationPeer(Indicator owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;
    }
}

/// <summary>角标：样式上就是 Border（选择器写 Border#PART_Badge）；读屏里是一段文字，名称是说明，计数文字不念。</summary>
sealed class IndicatorBadge : Border
{
    protected override Type StyleKeyOverride => typeof(Border);

    protected override AutomationPeer OnCreateAutomationPeer() => new BadgeAutomationPeer(this);

    sealed class BadgeAutomationPeer(IndicatorBadge owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>Indicator 的部件名：样式用子选择器 <c>rune|Indicator &gt; Border#PART_Badge</c>。</summary>
public static class IndicatorParts
{
    /// <summary>角标（TS 的 propsBadge）。</summary>
    public const string Badge = "PART_Badge";

    /// <summary>角标里的计数文字（TS 的 propsCount），读屏不念。</summary>
    public const string Count = "PART_Count";
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
