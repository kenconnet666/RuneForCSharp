using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 一排标签（role="tablist"）：line 时底边一道 borderMuted 的线、当前标签下方 2 像素的品牌色指示条（切换时滑过去，durations.md、standard）；
/// soft 时 fill 底、xs 内边距、md 圆角。方向键在标签之间移动（漫游焦点），到两端后从另一端继续（<see cref="Loop"/>）。
/// 名称写 <c>AutomationProperties.Name</c>（TS 的 aria-label）。
/// </summary>
[PseudoClasses(":line", ":soft", ":vertical")]
public class TabList : Flex
{
    /// <summary>到两端后是否从另一端继续，默认 true。</summary>
    public static readonly StyledProperty<bool> LoopProperty = AvaloniaProperty.Register<TabList, bool>(nameof(Loop), true);

    /// <summary>指示条的起点（主轴上，相对列表）。</summary>
    static readonly StyledProperty<double> IndicatorStartProperty = AvaloniaProperty.Register<TabList, double>("IndicatorStart");

    /// <summary>指示条的长度。</summary>
    static readonly StyledProperty<double> IndicatorLengthProperty = AvaloniaProperty.Register<TabList, double>("IndicatorLength");

    readonly RovingFocus focus;
    Tabs? owner;
    bool placed;

    readonly Decoration decoration;

    /// <summary>新建：横排、居中对齐；分隔线、指示条、soft 底色画在最底下一层（不参加 Flex 的排版）。</summary>
    public TabList()
    {
        Align = FlexAlign.Stretch;
        // ZIndex -1：画在所有标签下面（Panel 按 Children 的下标插入可视子元素，下标会错开一位）
        decoration = new Decoration(this) { ZIndex = -1, IsHitTestVisible = false };
        VisualChildren.Insert(0, decoration);
        focus = new RovingFocus(this, control => control is Tab) { Loop = true };
        LayoutUpdated += (_, _) => PlaceIndicator();
    }

    /// <summary>到两端后是否从另一端继续。</summary>
    public bool Loop { get => GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LoopProperty) focus.Loop = Loop;
        else if (change.Property == IndicatorStartProperty || change.Property == IndicatorLengthProperty) decoration.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        this.FindLogicalAncestorOfType<Tabs>()?.Sync();
    }

    /// <summary>跟着 Tabs 的样式与方向：间距、内边距、底色在主题里按伪类；方向键的方向。</summary>
    internal void Apply(Tabs tabs)
    {
        owner = tabs;
        var vertical = tabs.Orientation == TabsOrientation.Vertical;
        var line = tabs.Variant != TabsVariant.Soft;
        PseudoClasses.Set(":line", line);
        PseudoClasses.Set(":soft", !line);
        PseudoClasses.Set(":vertical", vertical);
        Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
        Gap = line && !vertical ? Space.Md : Space.Xs;
        // line 的分隔线横贯整行；soft 的底色只在标签后面（列表按内容宽）
        HorizontalAlignment = line || vertical ? global::Avalonia.Layout.HorizontalAlignment.Stretch : global::Avalonia.Layout.HorizontalAlignment.Left;
        focus.Orientation = vertical ? "vertical" : "horizontal";
        focus.Sync();
        InvalidateMeasure();
        decoration.InvalidateVisual();
    }

    /// <summary>指示条对准当前标签：第一次直接到位，之后滑过去（系统关闭动画时直接到位）。</summary>
    void PlaceIndicator()
    {
        if (owner is null || owner.Variant == TabsVariant.Soft) return;
        if (Children.OfType<Tab>().FirstOrDefault(tab => tab.IsSelected) is not { } selected || !selected.IsArrangeValid) return;
        var vertical = owner.Orientation == TabsOrientation.Vertical;
        var bounds = selected.Bounds;
        var (start, length) = vertical ? (bounds.Y, bounds.Height) : (bounds.X, bounds.Width);
        if (start == IndicatorStart && length == IndicatorLength) return;
        if (!placed)
        {
            placed = true;
            Transitions = null;
        }
        else if (Transitions is null)
        {
            var duration = Motion.Duration(this, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200));
            var easing = this.Resource<Easing>("Rune.Easing.Standard", new CubicEaseInOut());
            if (duration > TimeSpan.Zero)
                Transitions =
                [
                    new DoubleTransition { Property = IndicatorStartProperty, Duration = duration, Easing = easing },
                    new DoubleTransition { Property = IndicatorLengthProperty, Duration = duration, Easing = easing },
                ];
        }
        IndicatorStart = start;
        IndicatorLength = length;
    }

    double IndicatorStart { get => GetValue(IndicatorStartProperty); set => SetValue(IndicatorStartProperty, value); }

    double IndicatorLength { get => GetValue(IndicatorLengthProperty); set => SetValue(IndicatorLengthProperty, value); }

    /// <summary>soft 时四周 space.xs 的内边距（Flex 没有 Padding：量之前收进去、排完再挪过去）。</summary>
    Thickness Inset => owner?.Variant == TabsVariant.Soft ? new Thickness(0.25 * TextElement.GetFontSize(this)) : default;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = Inset;
        decoration.Measure(availableSize);
        return base.MeasureOverride(availableSize.Deflate(inset)).Inflate(inset);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var inset = Inset;
        decoration.Arrange(new Rect(finalSize));
        base.ArrangeOverride(finalSize.Deflate(inset));
        if (inset != default)
            foreach (var child in Children) child.Arrange(child.Bounds.Translate(new Vector(inset.Left, inset.Top)));
        return finalSize;
    }

    /// <summary>
    /// soft：fill 底、md 圆角。line：底边（竖排时结束边）一道 borderMuted 的分隔线，当前标签那一段压上 2 像素的品牌色指示条（sm 圆角）。
    /// </summary>
    void Paint(DrawingContext context)
    {
        if (owner is null) return;
        if (owner.Variant == TabsVariant.Soft)
        {
            // soft：fill 底、md 圆角
            if (this.Resource<IBrush?>("Rune.Brush.Fill", null) is { } fill)
                context.DrawRectangle(fill, null, new RoundedRect(new Rect(Bounds.Size), this.Resource("Rune.Radius.Md", 0.375) * TextElement.GetFontSize(this)));
            return;
        }
        var vertical = owner.Orientation == TabsOrientation.Vertical;
        var size = Bounds.Size;
        var divider = this.Resource<IBrush?>("Rune.Brush.BorderMuted", null);
        if (divider is not null)
            context.FillRectangle(divider, vertical ? new Rect(size.Width - 1, 0, 1, size.Height) : new Rect(0, size.Height - 1, size.Width, 1));
        if (IndicatorLength <= 0 || this.Resource<IBrush?>("Rune.Brush.Accent", null) is not { } accent) return;
        var bar = vertical ? new Rect(size.Width - 2, IndicatorStart, 2, IndicatorLength) : new Rect(IndicatorStart, size.Height - 2, IndicatorLength, 2);
        var radius = this.Resource("Rune.Radius.Sm", 0.25) * TextElement.GetFontSize(this);
        context.DrawRectangle(accent, null, new RoundedRect(bar, Math.Min(radius, 1)));
    }

    /// <summary>最底下一层：只画 TabList 的分隔线、指示条与 soft 底色（Panel 的 Render 是密封的）。</summary>
    sealed class Decoration(TabList owner) : Control
    {
        public override void Render(DrawingContext context) => owner.Paint(context);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabListPeer(this);

    sealed class TabListPeer(TabList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    }
}
