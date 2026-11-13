using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 一排标签（role="tablist"）：line 时底边（竖排时结束边）一道 borderMuted 的线、当前标签那一段 2 像素的品牌色指示条（切换时滑过去，durations.md、standard）；
/// soft 时 fill 底、xs 内边距、md 圆角，宽度按标签。方向键在标签之间移动（漫游焦点），到两端后从另一端继续（<see cref="Loop"/>）。
/// 名称写 <c>AutomationProperties.Name</c>。
/// </summary>
public class TabList : Flex
{
    /// <summary>到两端后是否从另一端继续，默认 true。</summary>
    public static readonly DependencyProperty LoopProperty = DependencyProperty.Register(nameof(Loop), typeof(bool), typeof(TabList),
        new FrameworkPropertyMetadata(true, (d, _) => ((TabList)d).focus.Loop = (bool)d.GetValue(LoopProperty)));

    static readonly DependencyProperty IndicatorStartProperty = DependencyProperty.Register("IndicatorStart", typeof(double), typeof(TabList),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly DependencyProperty IndicatorLengthProperty = DependencyProperty.Register("IndicatorLength", typeof(double), typeof(TabList),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    readonly RovingFocus focus;
    Tabs? owner;
    bool placed;

    /// <summary>新建：横排、拉伸对齐。</summary>
    public TabList()
    {
        Align = FlexAlign.Stretch;
        focus = new RovingFocus(this, element => element is Tab);
        // 方向键移动了焦点之后（漫游焦点先处理）：自动切换时切到新聚焦的标签
        AddHandler(KeyDownEvent, new KeyEventHandler((_, _) =>
        {
            if (owner?.Activation == TabsActivation.Automatic && Keyboard.FocusedElement is Tab { IsDisabled: false, Value: { } value } tab && Tabs.Owner(tab) == owner)
                owner.Select(value);
        }), handledEventsToo: true);
        LayoutUpdated += (_, _) => PlaceIndicator();
        Loaded += (_, _) => Tabs.Owner(this)?.Sync();
    }

    /// <summary>到两端后是否从另一端继续。</summary>
    public bool Loop { get => (bool)GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>跟着 Tabs 的样式与方向：间距、soft 的宽度、方向键的方向。</summary>
    internal void Apply(Tabs tabs)
    {
        owner = tabs;
        var vertical = tabs.Orientation == TabsOrientation.Vertical;
        var line = tabs.Variant != TabsVariant.Soft;
        Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
        Gap = line && !vertical ? Space.Md : Space.Xs;
        // line 的分隔线横贯整行；soft 的底色只在标签后面（列表按内容宽）
        HorizontalAlignment = line || vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        focus.Orientation = vertical ? "vertical" : "horizontal";
        focus.Sync();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>soft 时四周 space.xs 的内边距（Flex 没有 Padding：量之前收进去、排完再挪过去）。</summary>
    Thickness Inset => owner?.Variant == TabsVariant.Soft ? new Thickness(0.25 * TextElement.GetFontSize(this)) : default;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = Inset;
        var inner = base.MeasureOverride(new Size(Math.Max(0, availableSize.Width - inset.Left - inset.Right), Math.Max(0, availableSize.Height - inset.Top - inset.Bottom)));
        return new Size(inner.Width + inset.Left + inset.Right, inner.Height + inset.Top + inset.Bottom);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var inset = Inset;
        base.ArrangeOverride(new Size(Math.Max(0, finalSize.Width - inset.Left - inset.Right), Math.Max(0, finalSize.Height - inset.Top - inset.Bottom)));
        if (inset != default)
            foreach (UIElement child in InternalChildren)
            {
                var slot = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot((FrameworkElement)child);
                child.Arrange(new Rect(slot.X + inset.Left, slot.Y + inset.Top, slot.Width, slot.Height));
            }
        return finalSize;
    }

    /// <summary>指示条对准当前标签：第一次直接到位，之后滑过去（系统关闭动画时直接到位）。</summary>
    void PlaceIndicator()
    {
        if (owner is null || owner.Variant == TabsVariant.Soft) return;
        if (InternalChildren.OfType<Tab>().FirstOrDefault(tab => tab.IsSelected) is not { IsArrangeValid: true } selected) return;
        var vertical = owner.Orientation == TabsOrientation.Vertical;
        var offset = selected.TranslatePoint(new Point(), this);
        var (start, length) = vertical ? (offset.Y, selected.ActualHeight) : (offset.X, selected.ActualWidth);
        if (Math.Abs(start - (double)GetValue(IndicatorStartProperty)) < 0.01 && Math.Abs(length - (double)GetValue(IndicatorLengthProperty)) < 0.01) return;
        var duration = placed ? Motion.Duration(this, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200)) : TimeSpan.Zero;
        placed = true;
        Animate(IndicatorStartProperty, start, duration);
        Animate(IndicatorLengthProperty, length, duration);
    }

    void Animate(DependencyProperty property, double to, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            BeginAnimation(property, null);
            SetValue(property, to);
            return;
        }
        var from = (double)GetValue(property);
        SetValue(property, to);
        BeginAnimation(property, new DoubleAnimation(from, to, duration) { EasingFunction = TryFindResource("Rune.Easing.Standard") as IEasingFunction, FillBehavior = FillBehavior.Stop });
    }

    /// <summary>soft：fill 底、md 圆角。line：分隔线与当前标签那一段 2 像素的品牌色指示条（sm 圆角）。</summary>
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (owner is null) return;
        var em = TextElement.GetFontSize(this);
        if (owner.Variant == TabsVariant.Soft)
        {
            if (TryFindResource("Rune.Brush.Fill") is Brush fill)
            {
                var radius = (TryFindResource("Rune.Radius.Md") as double? ?? 0.375) * em;
                context.DrawRoundedRectangle(fill, null, new Rect(RenderSize), radius, radius);
            }
            return;
        }
        var vertical = owner.Orientation == TabsOrientation.Vertical;
        var size = RenderSize;
        if (TryFindResource("Rune.Brush.BorderMuted") is Brush divider)
            context.DrawRectangle(divider, null, vertical ? new Rect(size.Width - 1, 0, 1, size.Height) : new Rect(0, size.Height - 1, size.Width, 1));
        var start = (double)GetValue(IndicatorStartProperty);
        var length = (double)GetValue(IndicatorLengthProperty);
        if (length <= 0 || TryFindResource("Rune.Brush.Accent") is not Brush accent) return;
        var bar = vertical ? new Rect(size.Width - 2, start, 2, length) : new Rect(start, size.Height - 2, length, 2);
        context.DrawRoundedRectangle(accent, null, bar, 1, 1);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabListPeer(this);

    sealed class TabListPeer(TabList owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

        protected override string GetClassNameCore() => nameof(TabList);
    }
}
