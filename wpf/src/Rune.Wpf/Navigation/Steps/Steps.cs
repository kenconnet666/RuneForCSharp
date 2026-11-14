using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 步骤条（vue-tsx-pro 的 Steps）：多步流程里的位置（下单、注册、配置向导、任务的流转）。每一步是序号圆圈加标题与说明：
/// 已完成的显示对勾、品牌色底；当前步骤品牌色的圈；未开始的灰圈；出错的显示警示图标、危险色。读屏里当前步骤的项状态是"当前步骤"，
/// 其余的状态另有文字（已完成、未开始、出错），不只靠颜色。
/// <code>
/// &lt;rune:Steps Items="{Binding Items}" Current="{Binding Step}" Navigable="True" /&gt;
/// </code>
/// 每一步的状态默认按 <see cref="Current"/> 推出，<see cref="StepItem.Status"/> 可以改（出错的一步、非线性的流程）。
/// <see cref="Navigable"/> 时已完成、出错的步骤可以点回去（写回 Current，默认双向绑定）；有 <see cref="StepItem.Href"/> 的是链接。
/// 横排时各步等宽，连接线在标题右侧延伸到下一步；竖排时连接线从圆圈下面连到下一步的圆圈。<see cref="Size"/> 让圆圈、文字一起缩放。
/// 部件样式键：<see cref="StepsParts"/>；圆圈的状态在附加属性 <see cref="StateProperty"/> 上（样式按它换颜色）。
/// </summary>
public class Steps : FrameworkElement
{
    /// <summary>步骤。</summary>
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IReadOnlyList<StepItem>), typeof(Steps),
        new FrameworkPropertyMetadata(null, (d, _) => ((Steps)d).Rebuild()));

    /// <summary>当前步骤的序号，从 0 开始；默认双向绑定。</summary>
    public static readonly DependencyProperty CurrentProperty = DependencyProperty.Register(nameof(Current), typeof(int), typeof(Steps),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Steps)d).Rebuild()));

    /// <summary>方向，默认横排。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(StepsOrientation), typeof(Steps),
        new FrameworkPropertyMetadata(default(StepsOrientation), (d, _) => ((Steps)d).Rebuild()));

    /// <summary>字号档位：圆圈、文字随之缩放；不写时继承上下文的字号。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Steps),
        new FrameworkPropertyMetadata(default(TextSize), (d, _) => ((Steps)d).ApplySize()));

    /// <summary>已完成、出错的步骤可以点回去（写回 Current）。</summary>
    public static readonly DependencyProperty NavigableProperty = DependencyProperty.Register(nameof(Navigable), typeof(bool), typeof(Steps),
        new FrameworkPropertyMetadata(false, (d, _) => ((Steps)d).Rebuild()));

    /// <summary>圆圈的状态（部件样式按它换颜色）。</summary>
    public static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(StepState), typeof(Steps));

    readonly Grid host = new();
    readonly List<View> views = [];

    /// <summary>一步的部件：这一项、圆圈、文字区、圆圈与文字（可点回时是外面的按钮）、状态；按字号定尺寸、画连接线时用。</summary>
    sealed record View(FrameworkElement Item, Border Indicator, StackPanel Text, FrameworkElement Content, StepState State);

    /// <summary>新建：横排。</summary>
    public Steps()
    {
        host.SetResourceReference(StyleProperty, StepsParts.List);
        AddLogicalChild(host);
        AddVisualChild(host);
        Loaded += (_, _) => Rebuild();
    }

    /// <summary>步骤。</summary>
    public IReadOnlyList<StepItem>? Items { get => (IReadOnlyList<StepItem>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>当前步骤的序号。</summary>
    public int Current { get => (int)GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }

    /// <summary>方向。</summary>
    public StepsOrientation Orientation { get => (StepsOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>已完成、出错的步骤可以点回去。</summary>
    public bool Navigable { get => (bool)GetValue(NavigableProperty); set => SetValue(NavigableProperty, value); }

    /// <summary>圆圈的状态。</summary>
    public static StepState GetState(DependencyObject element) => (StepState)element.GetValue(StateProperty);

    /// <inheritdoc cref="GetState" />
    public static void SetState(DependencyObject element, StepState value) => element.SetValue(StateProperty, value);

    bool Vertical => Orientation == StepsOrientation.Vertical;

    double Em => TextElement.GetFontSize(this);

    /// <summary>各步（测试用）：每项是一个 rune:Flex，第一个子元素是圆圈与文字，可点回时是外面的按钮。</summary>
    internal Grid List => host;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { host }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => host;

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TextElement.FontSizeProperty) UpdateMetrics();
    }

    /// <summary>档位是主题的字号（资源引用，换主题跟着换），写成像素数时是本地值。</summary>
    void ApplySize()
    {
        if (Size.Pixels is { } pixels) SetValue(TextElement.FontSizeProperty, pixels);
        else if (Size.ResourceKey is { } key) SetResourceReference(TextElement.FontSizeProperty, key);
        else ClearValue(TextElement.FontSizeProperty);
    }

    /// <summary>按步骤重建：横排时每步一列（等宽，TS 的 flex: 1 1 0），竖排时一列往下排。</summary>
    void Rebuild()
    {
        host.Children.Clear();
        views.Clear();
        var items = Items ?? [];
        host.Columns = Vertical ? 1 : Math.Max(1, items.Count);
        host.Gap = Vertical ? Space.Lg : Space.Sm;
        var lang = Provider.GetLang(this);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var state = StepRules.StateAt(item, index, Current);
            var indicator = new Border { Child = Mark(item, index, state), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            SetState(indicator, state);
            indicator.SetResourceReference(StyleProperty, StepsParts.Indicator);
            var text = new StackPanel();
            text.SetResourceReference(StyleProperty, StepsParts.Text);
            text.Children.Add(Part(new TextBlock { Text = item.Title, TextWrapping = TextWrapping.Wrap }, StepsParts.Title));
            if (item.Description is { } description) text.Children.Add(Part(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap }, StepsParts.Description));
            var body = new Flex { Align = FlexAlign.Start, Gap = Space.Sm, Children = { indicator, text } };
            FrameworkElement content = body;
            if (StepRules.Reachable(item, state) && (item.Href is not null || Navigable))
            {
                var trigger = new ButtonBase { Content = body, HorizontalAlignment = HorizontalAlignment.Left };
                trigger.SetResourceReference(StyleProperty, StepsParts.Trigger);
                AutomationProperties.SetName(trigger, item.Title);
                var (href, at) = (item.Href, index);
                trigger.Click += (_, _) =>
                {
                    if (href is not null) LinkLauncher.Open(new Uri(href, UriKind.RelativeOrAbsolute));
                    else SetCurrentValue(CurrentProperty, at);
                };
                content = trigger;
            }
            var row = new Flex { Align = FlexAlign.Start, Children = { content } };
            row.SetResourceReference(StyleProperty, StepsParts.Item);
            AutomationProperties.SetItemStatus(row, state == StepState.Current ? "当前步骤" : lang.StepState(state.Value).Trim('（', '）', '(', ')', ' '));
            host.Children.Add(row);
            views.Add(new View(row, indicator, text, content, state));
        }
        UpdateMetrics();
    }

    static TextBlock Part(TextBlock block, PartKey key)
    {
        block.SetResourceReference(StyleProperty, key);
        return block;
    }

    /// <summary>圆圈里：已完成是对勾，出错是警示图标，有图标时是图标，否则是序号（从 1 起）。</summary>
    static UIElement Mark(StepItem item, int index, StepState state)
    {
        var icon = state == StepState.Complete ? Lucide.Check : state == StepState.Error ? Alert.IconFor(StatusTone.Danger) : item.Icon;
        return icon is not null
            ? new Icon { Data = icon, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            : new TextBlock { Text = (index + 1).ToString(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    }

    /// <summary>
    /// 按字号算的尺寸（TS 用 em）：圆圈的字号 0.93em、直径是它的 2 倍、圆角是一半、xs 边框；文字区上面留 0.3em（对齐圆圈里的字）、
    /// 标题与说明之间 space.xs；说明 0.93em；可点回的按钮 sm 圆角。
    /// </summary>
    void UpdateMetrics()
    {
        var em = Em;
        var mark = 0.93 * em;
        foreach (var view in views)
        {
            view.Indicator.Width = view.Indicator.Height = 2 * mark;
            view.Indicator.CornerRadius = new CornerRadius(mark);
            view.Indicator.BorderThickness = new Thickness(this.Resource<double>(ThemeKeys.BorderWidth.Xs));
            TextElement.SetFontSize(view.Indicator, mark);
            if (view.Indicator.Child is Icon icon) icon.Size = (TextSize)mark;
            view.Text.Margin = new Thickness(0, 0.3 * em, 0, 0);
            for (var i = 1; i < view.Text.Children.Count; i++)
            {
                var block = (TextBlock)view.Text.Children[i];
                block.Margin = new Thickness(0, 0.25 * em, 0, 0);
                block.FontSize = mark;
            }
            if (view.Content is ButtonBase trigger) EmSize.SetCornerRadius(trigger, this.Resource<double>(ThemeKeys.Radius.Sm));
        }
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        host.Measure(availableSize);
        return host.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        host.Arrange(new Rect(finalSize));
        InvalidateVisual();
        return finalSize;
    }

    /// <summary>
    /// 步骤之间的连接线（xs 宽；这一步已完成时品牌色，否则 border）：横排在圆圈与文字的右边、隔 space.sm，延伸到这一项的右边，对齐圆圈的中线；
    /// 竖排从圆圈下面隔 space.xs 连到下一步的圆圈上面 space.xs。
    /// </summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var em = Em;
        var line = this.Resource<double>(ThemeKeys.BorderWidth.Xs);
        for (var index = 0; index < views.Count - 1; index++)
        {
            var view = views[index];
            if (!IsAncestorOf(view.Indicator)) continue;
            var circle = view.Indicator.TransformToAncestor(this).Transform(default);
            var item = view.Item.TransformToAncestor(this).Transform(default);
            var brush = this.Resource<Brush>(view.State == StepState.Complete ? ThemeKeys.Brush.Accent : ThemeKeys.Brush.Border);
            var diameter = view.Indicator.ActualWidth;
            if (Vertical)
            {
                var next = views[index + 1].Item.TransformToAncestor(this).Transform(default);
                var top = circle.Y + diameter + 0.25 * em;
                drawingContext.DrawRectangle(brush, null, new Rect(circle.X + diameter / 2 - line / 2, top, line, Math.Max(0, next.Y - 0.25 * em - top)));
                continue;
            }
            var end = view.Content.TransformToAncestor(this).Transform(new Point(view.Content.ActualWidth, 0));
            var left = end.X + 0.5 * em;
            drawingContext.DrawRectangle(brush, null, new Rect(left, circle.Y + diameter / 2 - line / 2, Math.Max(0, item.X + view.Item.ActualWidth - left), line));
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new StepsPeer(this);

    sealed class StepsPeer(Steps owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => nameof(Steps);
    }
}

/// <summary>Steps 的部件样式键。</summary>
public static class StepsParts
{
    /// <summary>列表（rune:Grid：横排时每步一列）。</summary>
    public static readonly PartKey List = new(typeof(Steps), nameof(List), typeof(Grid));

    /// <summary>每一步；读屏的项状态是这一步的状态。</summary>
    public static readonly PartKey Item = new(typeof(Steps), nameof(Item), typeof(Flex));

    /// <summary>序号圆圈（状态在附加属性 Steps.State 上）。</summary>
    public static readonly PartKey Indicator = new(typeof(Steps), nameof(Indicator), typeof(Border));

    /// <summary>可点回的步骤外层（点了写回 Current，有地址时打开链接）。</summary>
    public static readonly PartKey Trigger = new(typeof(Steps), nameof(Trigger), typeof(ButtonBase));

    /// <summary>文字区（标题与说明）。</summary>
    public static readonly PartKey Text = new(typeof(Steps), nameof(Text), typeof(StackPanel));

    /// <summary>标题。</summary>
    public static readonly PartKey Title = new(typeof(Steps), nameof(Title), typeof(TextBlock));

    /// <summary>说明。</summary>
    public static readonly PartKey Description = new(typeof(Steps), nameof(Description), typeof(TextBlock));
}
