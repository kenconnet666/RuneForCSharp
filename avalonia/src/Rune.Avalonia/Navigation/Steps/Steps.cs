using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 步骤条（vue-tsx-pro 的 Steps）：多步流程里的位置（下单、注册、配置向导、任务的流转）。每一步是序号圆圈加标题与说明：
/// 已完成的显示对勾、品牌色底；当前步骤品牌色的圈；未开始的灰圈；出错的显示警示图标、危险色。读屏里当前步骤的项状态是"当前步骤"，
/// 其余的状态另有文字（已完成、未开始、出错），不只靠颜色。
/// <code>
/// &lt;rune:Steps Items="{Binding Items}" Current="{Binding Step}" Navigable="True" /&gt;
/// </code>
/// 每一步的状态默认按 <see cref="Current"/> 推出，<see cref="StepItem.Status"/> 可以改（出错的一步、非线性的流程）。
/// <see cref="Navigable"/> 时已完成、出错的步骤可以点回去（写回 Current，可双向绑定）；有 <see cref="StepItem.Href"/> 的是链接。
/// 横排时各步等宽，连接线在标题右侧延伸到下一步；竖排时连接线从圆圈下面连到下一步的圆圈。<see cref="Size"/> 让圆圈、文字一起缩放。
/// </summary>
public class Steps : Control
{
    /// <summary>步骤。</summary>
    public static readonly StyledProperty<IReadOnlyList<StepItem>?> ItemsProperty = AvaloniaProperty.Register<Steps, IReadOnlyList<StepItem>?>(nameof(Items));

    /// <summary>当前步骤的序号，从 0 开始；默认双向绑定。</summary>
    public static readonly StyledProperty<int> CurrentProperty =
        AvaloniaProperty.Register<Steps, int>(nameof(Current), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>方向，默认横排。</summary>
    public static readonly StyledProperty<StepsOrientation> OrientationProperty = AvaloniaProperty.Register<Steps, StepsOrientation>(nameof(Orientation));

    /// <summary>字号档位：圆圈、文字随之缩放；不写时继承上下文的字号。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Steps, TextSize>(nameof(Size));

    /// <summary>已完成、出错的步骤可以点回去（写回 Current）。</summary>
    public static readonly StyledProperty<bool> NavigableProperty = AvaloniaProperty.Register<Steps, bool>(nameof(Navigable));

    readonly Grid host = new() { Name = StepsParts.List };
    readonly List<View> views = [];
    readonly TemplateResources resources;

    /// <summary>一步的部件：这一项、圆圈、文字区、圆圈与文字（可点回时是外面的按钮）、状态；按字号定尺寸、画连接线时用。</summary>
    sealed record View(Control Item, Border Indicator, StackPanel Text, Control Content, StepState State);

    static Steps()
    {
        AffectsRender<Steps>(OrientationProperty);
    }

    /// <summary>新建：横排。读屏里是一组。</summary>
    public Steps()
    {
        resources = new TemplateResources(this);
        LogicalChildren.Add(host);
        VisualChildren.Add(host);
        this.RedrawOnResourceChanges(this);
    }

    /// <summary>步骤。</summary>
    public IReadOnlyList<StepItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>当前步骤的序号。</summary>
    public int Current { get => GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }

    /// <summary>方向。</summary>
    public StepsOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>已完成、出错的步骤可以点回去。</summary>
    public bool Navigable { get => GetValue(NavigableProperty); set => SetValue(NavigableProperty, value); }

    bool Vertical => Orientation == StepsOrientation.Vertical;

    double Em => GetValue(TextElement.FontSizeProperty);

    /// <summary>各步（测试用）：每项是一个 rune:Flex，第一个子元素是圆圈与文字，可点回时是外面的按钮。</summary>
    internal Grid List => host;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty || change.Property == CurrentProperty || change.Property == OrientationProperty || change.Property == NavigableProperty)
            Rebuild();
        else if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(TextElement.FontSizeProperty, pixels);
            else resources.Bind(TextElement.FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == TextElement.FontSizeProperty) UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Rebuild();
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
            var indicator = new Border { Name = StepsParts.Indicator, Child = Mark(item, index, state), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            indicator.Classes.Add(state.Value);
            var text = new StackPanel { Name = StepsParts.Text };
            text.Children.Add(new TextBlock { Name = StepsParts.Title, Text = item.Title, TextWrapping = TextWrapping.Wrap });
            if (item.Description is { } description) text.Children.Add(new TextBlock { Name = StepsParts.Description, Text = description, TextWrapping = TextWrapping.Wrap });
            var body = new Flex { Align = FlexAlign.Start, Gap = Space.Sm, Children = { indicator, text } };
            Control content = body;
            if (StepRules.Reachable(item, state) && (item.Href is not null || Navigable))
            {
                var trigger = new ButtonBase { Name = StepsParts.Trigger, Content = body, HorizontalAlignment = HorizontalAlignment.Left };
                AutomationProperties.SetName(trigger, item.Title);
                var (href, at) = (item.Href, index);
                trigger.Click += (_, _) =>
                {
                    if (href is not null) LinkLauncher.Open(this, new Uri(href, UriKind.RelativeOrAbsolute));
                    else SetCurrentValue(CurrentProperty, at);
                };
                content = trigger;
            }
            var row = new Flex { Name = StepsParts.Item, Align = FlexAlign.Start, Children = { content } };
            AutomationProperties.SetItemStatus(row, state == StepState.Current ? "当前步骤" : lang.StepState(state.Value).Trim('（', '）', '(', ')', ' '));
            host.Children.Add(row);
            views.Add(new View(row, indicator, text, content, state));
        }
        UpdateMetrics();
        InvalidateVisual();
    }

    /// <summary>圆圈里：已完成是对勾，出错是警示图标，有图标时是图标，否则是序号（从 1 起）。</summary>
    static Control Mark(StepItem item, int index, StepState state)
    {
        var icon = state == StepState.Complete ? Lucide.Check : state == StepState.Error ? Alert.IconFor(StatusTone.Danger) : item.Icon;
        return icon is not null
            ? new Icon { Data = icon, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            : new TextBlock { Text = (index + 1).ToString(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    }

    /// <summary>
    /// 按字号算的尺寸（TS 用 em）：圆圈的字号 0.93em、直径是它的 2 倍、圆角是一半、xs 边框；文字区上面留 0.3em（对齐圆圈里的字）、标题与说明之间 space.xs；
    /// 说明 0.93em；可点回的按钮 sm 圆角。
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
            view.Indicator.SetValue(TextElement.FontSizeProperty, mark);
            if (view.Indicator.Child is Icon icon) icon.Size = (TextSize)mark;
            var text = view.Text;
            text.Margin = new Thickness(0, 0.3 * em, 0, 0);
            text.Spacing = 0.25 * em;
            if (text.Children.Count > 1) text.Children[1].SetValue(TextElement.FontSizeProperty, mark);
            if (view.Content is ButtonBase trigger) trigger.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * em);
        }
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        InvalidateVisual();
        return arranged;
    }

    /// <summary>
    /// 步骤之间的连接线（xs 宽；这一步已完成时品牌色，否则 border）：横排在圆圈与文字的右边、隔 space.sm，延伸到这一项的右边，对齐圆圈的中线；
    /// 竖排从圆圈下面隔 space.xs 连到下一步的圆圈上面 space.xs。
    /// </summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var em = Em;
        var line = this.Resource<double>(ThemeKeys.BorderWidth.Xs);
        for (var index = 0; index < views.Count - 1; index++)
        {
            var view = views[index];
            if (view.Indicator.TranslatePoint(default, this) is not { } circle || view.Item.TranslatePoint(default, this) is not { } item) continue;
            var brush = this.Resource<IBrush>(view.State == StepState.Complete ? ThemeKeys.Brush.Accent : ThemeKeys.Brush.Border);
            var diameter = view.Indicator.Bounds.Width;
            if (Vertical)
            {
                if (views[index + 1].Item.TranslatePoint(default, this) is not { } next) continue;
                var x = circle.X + diameter / 2 - line / 2;
                var top = circle.Y + diameter + 0.25 * em;
                context.FillRectangle(brush, new Rect(x, top, line, Math.Max(0, next.Y - 0.25 * em - top)));
                continue;
            }
            if (view.Content.TranslatePoint(new Point(view.Content.Bounds.Width, 0), this) is not { } end) continue;
            var left = end.X + 0.5 * em;
            var right = item.X + view.Item.Bounds.Width;
            context.FillRectangle(brush, new Rect(left, circle.Y + diameter / 2 - line / 2, Math.Max(0, right - left), line));
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new StepsPeer(this);

    sealed class StepsPeer(Steps owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}

/// <summary>Steps 的部件名（样式选择器用）。</summary>
public static class StepsParts
{
    /// <summary>列表（rune:Grid：横排时每步一列）。</summary>
    public const string List = "PART_List";

    /// <summary>每一步（rune:Flex）；读屏的项状态是这一步的状态。</summary>
    public const string Item = "PART_Item";

    /// <summary>序号圆圈（Border，类名是状态：complete、current、upcoming、error）。</summary>
    public const string Indicator = "PART_Indicator";

    /// <summary>可点回的步骤外层（rune:ButtonBase：点了写回 Current，有地址时打开链接）。</summary>
    public const string Trigger = "PART_Trigger";

    /// <summary>文字区（StackPanel：标题与说明）。</summary>
    public const string Text = "PART_Text";

    /// <summary>标题（TextBlock）。</summary>
    public const string Title = "PART_Title";

    /// <summary>说明（TextBlock）。</summary>
    public const string Description = "PART_Description";
}
