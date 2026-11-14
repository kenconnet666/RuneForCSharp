using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>比例条的一段（vue-tsx-pro 的 ProportionSegment）。</summary>
/// <param name="Key">标识。</param>
/// <param name="Label">名称。</param>
/// <param name="Value">数量。</param>
/// <param name="Tone">色调；不写时按顺序取 accent、info、success、warning、danger、neutral。</param>
/// <param name="Color">任意颜色（如图表色板）；写了时不看色调。</param>
public sealed record ProportionSegment(string Key, string Label, double Value, StatusTone? Tone = null, Brush? Color = null);

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
    public static readonly DependencyProperty SegmentsProperty = Register(nameof(Segments), typeof(IReadOnlyList<ProportionSegment>), Array.Empty<ProportionSegment>());

    /// <summary>横条的名称（读屏）：后面自动带上各段的数量。</summary>
    public static readonly DependencyProperty LabelProperty = Register(nameof(Label), typeof(string), null);

    /// <summary>图例里数量的写法（文字或元素）；不写时是数字。</summary>
    public static readonly DependencyProperty ValueTextProperty = Register(nameof(ValueText), typeof(Func<ProportionSegment, object?>), null);

    /// <summary>下面的图例，默认显示。</summary>
    public static readonly DependencyProperty LegendProperty = Register(nameof(Legend), typeof(bool), true);

    /// <summary>横条的粗细，默认 md。</summary>
    public static readonly DependencyProperty SizeProperty = Register(nameof(Size), typeof(ProportionBarSize), default(ProportionBarSize));

    /// <summary>图例每一项后面的补充（"待定价"）。</summary>
    public static readonly DependencyProperty LegendExtraProperty = Register(nameof(LegendExtra), typeof(Func<ProportionSegment, object?>), null);

    readonly Flex root = new() { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Gap = Space.Sm };
    readonly Strip bar;
    readonly Flex legend = new() { Name = "PART_Legend", Wrap = true, ColumnGap = Space.Lg, RowGap = Space.Xs };

    static ProportionBar()
    {
        FocusableProperty.OverrideMetadata(typeof(ProportionBar), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(ProportionBar), new FrameworkPropertyMetadata(false));
    }

    /// <summary>新建：没有段。</summary>
    public ProportionBar()
    {
        bar = new Strip(this) { Name = "PART_Bar" };
        legend.SetResourceReference(TextElement.FontSizeProperty, "Rune.FontSize.Sm");
        root.Children.Add(bar);
        root.Children.Add(legend);
        AddVisualChild(root);
        AddLogicalChild(root);
    }

    static DependencyProperty Register(string name, Type type, object? fallback) =>
        DependencyProperty.Register(name, type, typeof(ProportionBar), new FrameworkPropertyMetadata(fallback, (d, _) => ((ProportionBar)d).Rebuild()));

    /// <summary>各段。</summary>
    public IReadOnlyList<ProportionSegment> Segments { get => (IReadOnlyList<ProportionSegment>)GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }

    /// <summary>横条的名称。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>图例里数量的写法。</summary>
    public Func<ProportionSegment, object?>? ValueText { get => (Func<ProportionSegment, object?>?)GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }

    /// <summary>下面的图例。</summary>
    public bool Legend { get => (bool)GetValue(LegendProperty); set => SetValue(LegendProperty, value); }

    /// <summary>横条的粗细。</summary>
    public ProportionBarSize Size { get => (ProportionBarSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>图例每一项后面的补充。</summary>
    public Func<ProportionSegment, object?>? LegendExtra { get => (Func<ProportionSegment, object?>?)GetValue(LegendExtraProperty); set => SetValue(LegendExtraProperty, value); }

    /// <summary>横条（测试用）。</summary>
    internal FrameworkElement Bar => bar;

    /// <summary>图例的各项（测试用）。</summary>
    internal IReadOnlyList<UIElement> LegendItems => [.. legend.Children.Cast<UIElement>()];

    /// <summary>横条给读屏的名称。</summary>
    public string BarLabel => Proportions.Label(Label, Segments.Select(segment => (segment.Label, segment.Value)));

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <summary>横条按各段重建；图例每段一项 [色点][名称][数量][补充]，间距 space.sm。</summary>
    void Rebuild()
    {
        AutomationProperties.SetName(bar, BarLabel);
        bar.Rebuild();
        legend.Children.Clear();
        legend.Visibility = Legend ? Visibility.Visible : Visibility.Collapsed;
        if (!Legend) return;
        for (var index = 0; index < Segments.Count; index++)
        {
            var segment = Segments[index];
            var item = new Flex { Name = "PART_LegendItem", Align = FlexAlign.Center, Gap = Space.Sm };
            item.Children.Add(new StatusDot { Size = StatusDotSize.Md, Tone = Proportions.ToneAt(segment.Tone, index), Color = segment.Color });
            item.Children.Add(new Text { Text = segment.Label });
            item.Children.Add(Part(ValueText?.Invoke(segment) ?? segment.Value.ToString(System.Globalization.CultureInfo.CurrentCulture), value: true));
            if (LegendExtra?.Invoke(segment) is { } extra) item.Children.Add(Part(extra, value: false));
            legend.Children.Add(item);
        }
    }

    static UIElement Part(object content, bool value) => content switch
    {
        UIElement element => element,
        _ => value
            ? new Text { Name = "PART_Value", Text = content.ToString(), Weight = TextWeight.Semibold, Numeric = true }
            : new Text { Text = content.ToString() },
    };

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        root.Measure(constraint);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        root.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    /// <summary>
    /// 横条：每段一块（色调的颜色是资源引用，换主题跟着变），按数量分宽、段间空 borderWidths.sm，整根按胶囊裁；读屏是一张图。
    /// </summary>
    sealed class Strip(ProportionBar owner) : Panel
    {
        readonly List<double> shares = [];

        public void Rebuild()
        {
            Children.Clear();
            shares.Clear();
            for (var index = 0; index < owner.Segments.Count; index++)
            {
                var segment = owner.Segments[index];
                if (segment.Value <= 0) continue;
                var block = new Border();
                if (segment.Color is { } color) block.Background = color;
                else block.SetResourceReference(Border.BackgroundProperty, StatusDot.BrushKey(Proportions.ToneAt(segment.Tone, index)));
                Children.Add(block);
                shares.Add(segment.Value);
            }
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize) =>
            new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, (owner.Size.Value is null ? ProportionBarSize.Md : owner.Size).Height);

        protected override Size ArrangeOverride(Size finalSize)
        {
            var gap = TryFindResource("Rune.BorderWidth.Sm") is double value ? value : 2;
            var room = Math.Max(0, finalSize.Width - gap * (shares.Count - 1));
            var total = shares.Sum();
            var x = 0.0;
            for (var index = 0; index < Children.Count; index++)
            {
                var width = total > 0 ? room * shares[index] / total : 0;
                Children[index].Arrange(new Rect(x, 0, width, finalSize.Height));
                x += width + gap;
            }
            // 整根按胶囊（radii.full）裁，两头的段是圆的
            Clip = new RectangleGeometry(new Rect(finalSize), finalSize.Height / 2, finalSize.Height / 2);
            return finalSize;
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new StripPeer(this);

        sealed class StripPeer(Strip owner) : FrameworkElementAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

            protected override string GetClassNameCore() => "ProportionBar";

            protected override List<AutomationPeer>? GetChildrenCore() => null;
        }
    }
}

/// <summary>ProportionBar 的部件（元素的 Name 是 PART_ 加部件名）。</summary>
public static class ProportionBarParts
{
    /// <summary>横条。</summary>
    public static readonly PartKey Bar = new(typeof(ProportionBar), nameof(Bar), typeof(Panel));

    /// <summary>图例。</summary>
    public static readonly PartKey Legend = new(typeof(ProportionBar), nameof(Legend), typeof(Flex));

    /// <summary>图例的一项。</summary>
    public static readonly PartKey LegendItem = new(typeof(ProportionBar), nameof(LegendItem), typeof(Flex));

    /// <summary>图例里的数量。</summary>
    public static readonly PartKey Value = new(typeof(ProportionBar), nameof(Value), typeof(Text));
}
