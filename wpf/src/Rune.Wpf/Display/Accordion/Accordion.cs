using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 手风琴（vue-tsx-pro 的 Accordion）：一组可展开收起的 <see cref="AccordionItem"/>，单开或多开（WAI-ARIA 的 Accordion 模式）。
/// <code>
/// &lt;rune:Accordion Value="{Binding Open}" AutomationProperties.Name="常见问题"&gt;
///   &lt;rune:AccordionItem Value="ship" Title="多久发货？"&gt;&lt;rune:Paragraph&gt;付款后 48 小时内。&lt;/rune:Paragraph&gt;&lt;/rune:AccordionItem&gt;
/// &lt;/rune:Accordion&gt;
/// &lt;rune:Accordion SelectionMode="multiple" Values="{Binding OpenList}"&gt;…&lt;/rune:Accordion&gt;
/// </code>
/// 单开时 <see cref="Value"/> 是展开那项的值（默认双向绑定），再点可以收起（<see cref="Collapsible"/>）；多开时 <see cref="Values"/> 是展开着的值。
/// 标题都在 Tab 顺序里，另外 ↑ / ↓ 在标题之间移动（到头后循环），Home / End 到第一个、最后一个。不可用用平台的 IsEnabled。
/// </summary>
public class Accordion : StackPanel
{
    /// <summary>单开（默认）或多开。</summary>
    public static readonly DependencyProperty SelectionModeProperty = Register(nameof(SelectionMode), typeof(SelectionMode), SelectionMode.Single);

    /// <summary>单开时展开那项的值；默认双向绑定。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(Accordion), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Accordion)d).SyncItems()));

    /// <summary>多开时展开着的值；默认双向绑定。</summary>
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<string>), typeof(Accordion), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Accordion)d).SyncItems()));

    /// <summary>单开时允许把唯一展开的那项也收起，默认 true。</summary>
    public static readonly DependencyProperty CollapsibleProperty = DependencyProperty.Register(nameof(Collapsible), typeof(bool), typeof(Accordion), new PropertyMetadata(true));

    /// <summary>标题的层级（2–6），按所在页面的大纲选，默认 3。</summary>
    public static readonly DependencyProperty HeadingLevelProperty = DependencyProperty.Register(
        nameof(HeadingLevel), typeof(int), typeof(Accordion), new FrameworkPropertyMetadata(3, (d, _) => ((Accordion)d).SyncItems()), value => value is >= 2 and <= 6);

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize?), typeof(Accordion), new FrameworkPropertyMetadata(null, (d, _) => ((Accordion)d).ApplySize()));

    static DependencyProperty Register(string name, Type type, object defaultValue) =>
        DependencyProperty.Register(name, type, typeof(Accordion), new FrameworkPropertyMetadata(defaultValue, (d, _) => ((Accordion)d).SyncItems()));

    /// <summary>新建：单开，↑ ↓ Home End 在标题之间移动。</summary>
    public Accordion() => AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDown));

    /// <summary>单开或多开。</summary>
    public SelectionMode SelectionMode { get => (SelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>单开时展开那项的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>多开时展开着的值。</summary>
    public IReadOnlyList<string>? Values { get => (IReadOnlyList<string>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>单开时能否全部收起。</summary>
    public bool Collapsible { get => (bool)GetValue(CollapsibleProperty); set => SetValue(CollapsibleProperty, value); }

    /// <summary>标题的层级。</summary>
    public int HeadingLevel { get => (int)GetValue(HeadingLevelProperty); set => SetValue(HeadingLevelProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize? Size { get => (TextSize?)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>展开着的值（两种方式合在一起看）。</summary>
    internal IReadOnlyList<string> Expanded => SelectionMode == SelectionMode.Multiple ? Values ?? [] : Value is { } value ? [value] : [];

    /// <summary>展开或收起一项（单开 / 多开的规则同 ToggleGroup：<see cref="Selection.Toggle"/>）。</summary>
    internal void Toggle(string item)
    {
        var next = Selection.Toggle(Expanded, item, SelectionMode, required: !Collapsible);
        if (SelectionMode == SelectionMode.Multiple) SetCurrentValue(ValuesProperty, next);
        else SetCurrentValue(ValueProperty, next.Count > 0 ? next[0] : null);
    }

    void SyncItems()
    {
        foreach (var item in Children.OfType<AccordionItem>()) item.Sync();
    }

    void ApplySize()
    {
        if (Size is not { } size) ClearValue(TextElement.FontSizeProperty);
        else if (size.Pixels is { } pixels) SetValue(TextElement.FontSizeProperty, pixels);
        else SetResourceReference(TextElement.FontSizeProperty, size.ResourceKey);
    }

    /// <summary>↑ / ↓ 在标题之间移动（循环），Home / End 到两端；只在焦点在这一组的标题上时处理。</summary>
    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not AccordionTrigger focused) return;
        var triggers = Children.OfType<AccordionItem>().Select(item => item.Trigger).Where(trigger => trigger.IsEnabled && trigger.IsVisible).ToList();
        var index = triggers.IndexOf(focused);
        if (index < 0) return;
        AccordionTrigger? next = e.Key switch
        {
            Key.Down => triggers[(index + 1) % triggers.Count],
            Key.Up => triggers[(index - 1 + triggers.Count) % triggers.Count],
            Key.Home => triggers[0],
            Key.End => triggers[^1],
            _ => null,
        };
        if (next is null) return;
        e.Handled = true;
        next.Focus();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AccordionPeer(this);

    sealed class AccordionPeer(Accordion owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Accordion);
    }
}

/// <summary>
/// 手风琴里的一项（vue-tsx-pro 的 AccordionItem）：标题按钮 + 展开收起的内容（<see cref="Collapse"/>）。底边一条 borderMuted 的线。
/// 标题按钮占满一行：上下 space.md、左右 space.sm、sm 圆角，悬停 fill 底，medium 字重，结束边是展开箭头（展开时翻转）；焦点环画在里面。
/// 读屏：标题按钮带展开状态与标题层级，内容区以标题为名称。子元素的名字在 <see cref="AccordionItemParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class AccordionItem : FrameworkElement
{
    /// <summary>这一项的值。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(AccordionItem), new PropertyMetadata(null, (d, _) => ((AccordionItem)d).Sync()));

    /// <summary>标题：文字或控件（可以放图标、计数）。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(object), typeof(AccordionItem), new PropertyMetadata(null, (d, _) => ((AccordionItem)d).ShowTitle()));

    /// <summary>内容。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(UIElement), typeof(AccordionItem), new PropertyMetadata(null, (d, _) => ((AccordionItem)d).body.Child = ((AccordionItem)d).Content));

    readonly Border root = new() { BorderThickness = new Thickness(0, 0, 0, 1) };
    readonly ContentPresenter title = new() { Name = AccordionItemParts.Title, VerticalAlignment = VerticalAlignment.Center };
    readonly Icon chevron = new() { Name = AccordionItemParts.Chevron, Data = Lucide.ChevronDown, Tone = TextTone.Muted, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5) };
    readonly Collapse collapse = new() { Name = AccordionItemParts.Content };
    readonly Border body = new();

    static AccordionItem() => TextElement.FontSizeProperty.OverrideMetadata(typeof(AccordionItem), new FrameworkPropertyMetadata(
        SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((AccordionItem)d).UpdatePadding()));

    /// <summary>新建：标题按钮在上，内容在下。</summary>
    public AccordionItem()
    {
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(chevron, Dock.Right);
        row.Children.Add(chevron);
        row.Children.Add(title);
        Trigger.Content = row;
        Trigger.Click += (_, _) => Owner?.Toggle(Value ?? "");
        collapse.Content = body;
        root.Child = new StackPanel { Children = { Trigger, collapse } };
        // 底边一条 borderMuted 的线（borderWidths.xs）
        root.SetResourceReference(Border.BorderBrushProperty, "Rune.Brush.BorderMuted");
        AddLogicalChild(root);
        AddVisualChild(root);
        UpdatePadding();
        Loaded += (_, _) => Sync();
    }

    /// <summary>这一项的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>标题。</summary>
    public object? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>内容。</summary>
    public UIElement? Content { get => (UIElement?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>标题按钮。</summary>
    internal AccordionTrigger Trigger { get; } = new() { Name = AccordionItemParts.Trigger };

    /// <summary>展开着。</summary>
    public bool IsOpen => Owner?.Expanded.Contains(Value ?? "") == true;

    Accordion? Owner => Parent as Accordion;

    void ShowTitle()
    {
        title.Content = Title;
        AutomationProperties.SetName(collapse, Field.PlainText(Title) ?? "");
    }

    /// <summary>内容区左右 space.sm、下面 space.md（按字号算）。</summary>
    void UpdatePadding()
    {
        var em = TextElement.GetFontSize(this);
        body.Padding = new Thickness(0.5 * em, 0, 0.5 * em, 0.75 * em);
    }

    /// <summary>跟上组的展开状态与标题层级：内容展开收起、箭头翻转、读屏的展开状态。</summary>
    internal void Sync()
    {
        var open = IsOpen;
        collapse.IsOpen = open;
        chevron.RenderTransform = open ? new RotateTransform(180) : Transform.Identity;
        Trigger.SetValue(Popover.TriggerExpandedProperty, open);
        AutomationProperties.SetHeadingLevel(Trigger, AutomationHeadingLevel.Level1 + ((Owner?.HeadingLevel ?? 3) - 1));
    }

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>手风琴的标题按钮：主题在 Accordion.xaml（悬停 fill 底、内边距、圆角）；焦点环画在按钮里面（整行宽，放进不留内边距的卡片时外扩的环会被裁掉）。</summary>
public class AccordionTrigger : ButtonBase
{
    static AccordionTrigger() => DefaultStyleKeyProperty.OverrideMetadata(typeof(AccordionTrigger), new FrameworkPropertyMetadata(typeof(AccordionTrigger)));

    /// <inheritdoc />
    private protected override double FocusRingOutset => -2;
}

/// <summary>
/// 可展开收起的区域（vue-tsx-pro 的 Collapsible，WAI-ARIA 的 Disclosure 模式）：触发器 + <see cref="Collapse"/>。
/// <code>
/// &lt;rune:Collapsible&gt;
///   &lt;rune:Collapsible.Trigger&gt;&lt;rune:Button Variant="ghost" Content="高级筛选" /&gt;&lt;/rune:Collapsible.Trigger&gt;
///   &lt;rune:Grid&gt;…&lt;/rune:Grid&gt;
/// &lt;/rune:Collapsible&gt;
/// </code>
/// 点触发器切换 <see cref="IsOpen"/>（默认双向绑定）；触发器是按钮家族时读屏带上展开状态（TS 的 aria-expanded）。
/// IsDisabled 时点击不切换（IsOpen 仍可由外面控制）。触发器靠起始边，不被拉满一行。
/// </summary>
[ContentProperty(nameof(Content))]
public class Collapsible : FrameworkElement
{
    /// <summary>触发器：一个按钮。</summary>
    public static readonly DependencyProperty TriggerProperty = DependencyProperty.Register(
        nameof(Trigger), typeof(System.Windows.Controls.Primitives.ButtonBase), typeof(Collapsible), new PropertyMetadata(null, OnTriggerChanged));

    /// <summary>展开着；默认双向绑定。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(Collapsible), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Collapsible)d).Sync()));

    /// <summary>不可用：触发器不可用，点击不切换。</summary>
    public static readonly DependencyProperty IsDisabledProperty = DependencyProperty.Register(
        nameof(IsDisabled), typeof(bool), typeof(Collapsible), new PropertyMetadata(false, (d, _) => ((Collapsible)d).Sync()));

    /// <summary>内容。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(UIElement), typeof(Collapsible), new PropertyMetadata(null, (d, _) => ((Collapsible)d).collapse.Content = ((Collapsible)d).Content));

    readonly StackPanel root = new();
    readonly Collapse collapse = new();

    /// <summary>新建：收起着。</summary>
    public Collapsible()
    {
        root.Children.Add(collapse);
        AddLogicalChild(root);
        AddVisualChild(root);
    }

    /// <summary>触发器。</summary>
    public System.Windows.Controls.Primitives.ButtonBase? Trigger { get => (System.Windows.Controls.Primitives.ButtonBase?)GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }

    /// <summary>展开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>不可用。</summary>
    public bool IsDisabled { get => (bool)GetValue(IsDisabledProperty); set => SetValue(IsDisabledProperty, value); }

    /// <summary>内容。</summary>
    public UIElement? Content { get => (UIElement?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    static void OnTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var collapsible = (Collapsible)d;
        if (e.OldValue is System.Windows.Controls.Primitives.ButtonBase old)
        {
            old.Click -= collapsible.OnTriggerClick;
            collapsible.root.Children.Remove(old);
        }
        if (e.NewValue is System.Windows.Controls.Primitives.ButtonBase trigger)
        {
            trigger.Click += collapsible.OnTriggerClick;
            trigger.HorizontalAlignment = HorizontalAlignment.Left;
            collapsible.root.Children.Insert(0, trigger);
        }
        collapsible.Sync();
    }

    void OnTriggerClick(object sender, RoutedEventArgs e)
    {
        if (!IsDisabled) SetCurrentValue(IsOpenProperty, !IsOpen);
    }

    void Sync()
    {
        collapse.IsOpen = IsOpen;
        if (Trigger is not { } trigger) return;
        trigger.SetValue(Popover.TriggerExpandedProperty, IsOpen);
        trigger.IsEnabled = !IsDisabled;
    }

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>AccordionItem 里子元素的名字（代码组合的子元素）。</summary>
public static class AccordionItemParts
{
    /// <summary>标题按钮（TS 的 propsTrigger）：AccordionTrigger。</summary>
    public const string Trigger = "PART_Trigger";

    /// <summary>标题（TS 的 propsHeading 里的文字）：ContentPresenter。</summary>
    public const string Title = "PART_Title";

    /// <summary>展开箭头（TS 的 propsChevron）：Icon。</summary>
    public const string Chevron = "PART_Chevron";

    /// <summary>内容区（TS 的 propsContent）：Collapse。</summary>
    public const string Content = "PART_Content";
}
