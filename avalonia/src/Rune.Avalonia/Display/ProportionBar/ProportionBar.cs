using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>比例条的一段（vue-tsx-pro 的 ProportionSegment）。</summary>
/// <param name="Key">标识。</param>
/// <param name="Label">名称。</param>
/// <param name="Value">数量。</param>
/// <param name="Tone">色调；不写时按顺序取 accent、info、success、warning、danger、neutral。</param>
/// <param name="Color">任意颜色（如图表色板）；写了时不看色调。</param>
public sealed record ProportionSegment(string Key, string Label, double Value, StatusTone? Tone = null, IBrush? Color = null);

/// <summary>
/// 比例条（vue-tsx-pro 的 ProportionBar）：一根横条按各部分的数量分段，下面是图例（色点、名称、数量），看一个总数由哪几块构成
/// （工单的三种状态、各状态的占比、存储空间）。部分之间互斥、加起来是总数时用它；要比较趋势用图表。
/// <code>
/// new ProportionBar { Label = "工单构成", Segments = [new("done", "已完成", 25, StatusTone.Success), new("open", "进行中", 35, StatusTone.Info)], ValueText = segment => $"{segment.Value} 项" };
/// </code>
/// 横条是圆角的胶囊（full），段与段之间空 borderWidths.sm，粗细 sm 8 像素、md 12 像素；颜色同 StatusDot 的色调实色或任意颜色，数量为 0 的段不画但留在图例里。
/// 图例在横条下面（space.sm），换行排开（列间距 lg、行间距 xs，sm 的字号）：色点（md）、名称、数量（半粗、等宽数字）与补充。
/// 横条对读屏是一张图，名称是 <see cref="Label"/> 加上各段的数量；图例是普通文字。
/// </summary>
public class ProportionBar : Control
{
    /// <summary>各段：按顺序从起始边排。</summary>
    public static readonly StyledProperty<IReadOnlyList<ProportionSegment>> SegmentsProperty = AvaloniaProperty.Register<ProportionBar, IReadOnlyList<ProportionSegment>>(nameof(Segments), []);

    /// <summary>横条的名称（读屏）：后面自动带上各段的数量。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<ProportionBar, string?>(nameof(Label));

    /// <summary>图例里数量的写法（文字或控件）；不写时是数字。</summary>
    public static readonly StyledProperty<Func<ProportionSegment, object?>?> ValueTextProperty = AvaloniaProperty.Register<ProportionBar, Func<ProportionSegment, object?>?>(nameof(ValueText));

    /// <summary>下面的图例，默认显示。</summary>
    public static readonly StyledProperty<bool> LegendProperty = AvaloniaProperty.Register<ProportionBar, bool>(nameof(Legend), true);

    /// <summary>横条的粗细，默认 md。</summary>
    public static readonly StyledProperty<ProportionBarSize> SizeProperty = AvaloniaProperty.Register<ProportionBar, ProportionBarSize>(nameof(Size));

    /// <summary>图例每一项后面的补充（"待定价"）。</summary>
    public static readonly StyledProperty<Func<ProportionSegment, object?>?> LegendExtraProperty = AvaloniaProperty.Register<ProportionBar, Func<ProportionSegment, object?>?>(nameof(LegendExtra));

    readonly Flex root = new() { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Gap = Space.Sm };
    readonly Strip bar;
    readonly Flex legend = new() { Name = ProportionBarParts.Legend, Wrap = true, ColumnGap = Space.Lg, RowGap = Space.Xs };

    /// <summary>新建：没有段。</summary>
    public ProportionBar()
    {
        bar = new Strip(this) { Name = ProportionBarParts.Bar };
        root.Children.AddRange([bar, legend]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>各段。</summary>
    public IReadOnlyList<ProportionSegment> Segments { get => GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }

    /// <summary>横条的名称。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>图例里数量的写法。</summary>
    public Func<ProportionSegment, object?>? ValueText { get => GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }

    /// <summary>下面的图例。</summary>
    public bool Legend { get => GetValue(LegendProperty); set => SetValue(LegendProperty, value); }

    /// <summary>横条的粗细。</summary>
    public ProportionBarSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>图例每一项后面的补充。</summary>
    public Func<ProportionSegment, object?>? LegendExtra { get => GetValue(LegendExtraProperty); set => SetValue(LegendExtraProperty, value); }

    /// <summary>横条（测试用）。</summary>
    internal Control Bar => bar;

    /// <summary>图例的各项（测试用）。</summary>
    internal IReadOnlyList<Control> LegendItems => legend.Children;

    /// <summary>横条给读屏的名称。</summary>
    public string BarLabel => Proportions.Label(Label, Segments.Select(segment => (segment.Label, segment.Value)));

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty || change.Property == ValueTextProperty || change.Property == LegendProperty || change.Property == LegendExtraProperty) Rebuild();
        else if (change.Property == SizeProperty) bar.InvalidateMeasure();
        else if (change.Property == LabelProperty) AutomationProperties.SetName(bar, BarLabel);
    }

    /// <summary>图例：每段一项 [色点][名称][数量][补充]，间距 space.sm。</summary>
    void Rebuild()
    {
        AutomationProperties.SetName(bar, BarLabel);
        bar.InvalidateVisual();
        legend.Children.Clear();
        legend.IsVisible = Legend;
        if (!Legend) return;
        legend.SetValue(TextElement.FontSizeProperty, this.Resource<double>(ThemeKeys.FontSize.Sm));
        for (var index = 0; index < Segments.Count; index++)
        {
            var segment = Segments[index];
            var item = new Flex { Name = ProportionBarParts.LegendItem, Align = FlexAlign.Center, Gap = Space.Sm };
            item.Children.Add(new StatusDot { Size = StatusDotSize.Md, Tone = Proportions.ToneAt(segment.Tone, index), Color = segment.Color });
            item.Children.Add(new Text { Text = segment.Label });
            item.Children.Add(Part(ValueText?.Invoke(segment) ?? segment.Value.ToString(System.Globalization.CultureInfo.CurrentCulture), value: true));
            if (LegendExtra?.Invoke(segment) is { } extra) item.Children.Add(Part(extra, value: false));
            legend.Children.Add(item);
        }
    }

    static Control Part(object content, bool value) => content switch
    {
        Control control => control,
        _ => value
            ? new Text { Name = ProportionBarParts.Value, Text = content.ToString(), Weight = TextWeight.Semibold, Numeric = true }
            : new Text { Text = content.ToString() },
    };

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>横条：按数量分段的胶囊，段间空 borderWidths.sm；读屏是一张图。</summary>
    sealed class Strip : Control
    {
        readonly ProportionBar owner;

        public Strip(ProportionBar owner)
        {
            this.owner = owner;
            this.RedrawOnResourceChanges(owner);
        }

        protected override LayoutSize MeasureOverride(LayoutSize availableSize) =>
            new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, (owner.Size.Value is null ? ProportionBarSize.Md : owner.Size).Height);

        public override void Render(DrawingContext context)
        {
            var shown = owner.Segments.Select((segment, index) => (Segment: segment, Index: index)).Where(entry => entry.Segment.Value > 0).ToList();
            var total = shown.Sum(entry => entry.Segment.Value);
            if (total <= 0) return;
            var gap = owner.Resource<double>(ThemeKeys.BorderWidth.Sm);
            var (width, height) = (Bounds.Width, Bounds.Height);
            var room = Math.Max(0, width - gap * (shown.Count - 1));
            // 整根按胶囊（radii.full）裁，两头的段是圆的
            using var clip = context.PushClip(new RoundedRect(new Rect(Bounds.Size), height / 2));
            var x = 0.0;
            foreach (var (segment, index) in shown)
            {
                var share = room * segment.Value / total;
                var brush = segment.Color ?? owner.Resource<IBrush?>(StatusDot.BrushKey(Proportions.ToneAt(segment.Tone, index)));
                if (brush is not null) context.FillRectangle(brush, new Rect(x, 0, share, height));
                x += share + gap;
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new StripPeer(this);

        sealed class StripPeer(Strip owner) : ControlAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

            protected override bool IsControlElementCore() => true;
        }
    }
}

/// <summary>ProportionBar 的部件名。</summary>
public static class ProportionBarParts
{
    /// <summary>横条。</summary>
    public const string Bar = "PART_Bar";

    /// <summary>图例。</summary>
    public const string Legend = "PART_Legend";

    /// <summary>图例的一项。</summary>
    public const string LegendItem = "PART_LegendItem";

    /// <summary>图例里的数量。</summary>
    public const string Value = "PART_Value";
}
