using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using Avalonia.Metadata;

namespace Rune.Avalonia;

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
    public static readonly StyledProperty<SelectionMode> SelectionModeProperty = AvaloniaProperty.Register<Accordion, SelectionMode>(nameof(SelectionMode), SelectionMode.Single);

    /// <summary>单开时展开那项的值；默认双向绑定。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<Accordion, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>多开时展开着的值；默认双向绑定。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ValuesProperty =
        AvaloniaProperty.Register<Accordion, IReadOnlyList<string>?>(nameof(Values), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>单开时允许把唯一展开的那项也收起，默认 true。</summary>
    public static readonly StyledProperty<bool> CollapsibleProperty = AvaloniaProperty.Register<Accordion, bool>(nameof(Collapsible), true);

    /// <summary>标题的层级（2–6），按所在页面的大纲选，默认 3。</summary>
    public static readonly StyledProperty<int> HeadingLevelProperty =
        AvaloniaProperty.Register<Accordion, int>(nameof(HeadingLevel), 3, validate: level => level is >= 2 and <= 6);

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly StyledProperty<TextSize?> SizeProperty = AvaloniaProperty.Register<Accordion, TextSize?>(nameof(Size));

    readonly TemplateResources resources;

    /// <summary>新建：单开，↑ ↓ Home End 在标题之间移动。</summary>
    public Accordion()
    {
        resources = new TemplateResources(this);
        AddHandler(KeyDownEvent, OnKeyDown);
    }

    /// <summary>单开或多开。</summary>
    public SelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>单开时展开那项的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>多开时展开着的值。</summary>
    public IReadOnlyList<string>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>单开时能否全部收起。</summary>
    public bool Collapsible { get => GetValue(CollapsibleProperty); set => SetValue(CollapsibleProperty, value); }

    /// <summary>标题的层级。</summary>
    public int HeadingLevel { get => GetValue(HeadingLevelProperty); set => SetValue(HeadingLevelProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize? Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>展开着的值（两种方式合在一起看）。</summary>
    internal IReadOnlyList<string> Expanded => SelectionMode == SelectionMode.Multiple ? Values ?? [] : Value is { } value ? [value] : [];

    /// <summary>展开或收起一项（单开 / 多开的规则同 ToggleGroup：<see cref="Selection.Toggle"/>）。</summary>
    internal void Toggle(string item)
    {
        var next = Selection.Toggle(Expanded, item, SelectionMode, required: !Collapsible);
        if (SelectionMode == SelectionMode.Multiple) SetCurrentValue(ValuesProperty, next);
        else SetCurrentValue(ValueProperty, next.Count > 0 ? next[0] : null);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == ValuesProperty || change.Property == SelectionModeProperty || change.Property == HeadingLevelProperty)
            foreach (var item in Children.OfType<AccordionItem>()) item.Sync();
        else if (change.Property == SizeProperty)
        {
            if (Size is not { } size) resources.Bind(TextElement.FontSizeProperty, null);
            else if (size.Pixels is { } pixels) resources.Set(TextElement.FontSizeProperty, pixels);
            else resources.Bind(TextElement.FontSizeProperty, size.ResourceKey);
        }
    }

    /// <summary>↑ / ↓ 在标题之间移动（循环），Home / End 到两端；只在焦点在这一组的标题上时处理。</summary>
    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is not AccordionTrigger focused || focused.FindLogicalAncestorOfType<Accordion>() != this) return;
        var triggers = Children.OfType<AccordionItem>().Select(item => item.Trigger).Where(trigger => trigger.IsEffectivelyEnabled && trigger.IsEffectivelyVisible).ToList();
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
        next.Focus(NavigationMethod.Directional);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AccordionPeer(this);

    sealed class AccordionPeer(Accordion owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// 手风琴里的一项（vue-tsx-pro 的 AccordionItem）：标题按钮 + 展开收起的内容（<see cref="Collapse"/>）。底边一条 borderMuted 的线。
/// 标题按钮占满一行：上下 space.md、左右 space.sm、sm 圆角，悬停 fill 底，medium 字重，结束边是展开箭头（展开时翻转）；焦点环画在里面。
/// 读屏：标题按钮带展开状态与标题层级，内容区以标题为名称。部件名在 <see cref="AccordionItemParts"/>。
/// </summary>
public class AccordionItem : Control
{
    /// <summary>这一项的值。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<AccordionItem, string?>(nameof(Value));

    /// <summary>标题：文字或控件（可以放图标、计数）。</summary>
    public static readonly StyledProperty<object?> TitleProperty = AvaloniaProperty.Register<AccordionItem, object?>(nameof(Title));

    /// <summary>内容。</summary>
    public static readonly StyledProperty<Control?> ContentProperty = AvaloniaProperty.Register<AccordionItem, Control?>(nameof(Content));

    readonly Border root = new() { BorderThickness = new Thickness(0, 0, 0, 1) };
    readonly ContentPresenter title = new() { Name = AccordionItemParts.Title, VerticalAlignment = VerticalAlignment.Center };
    readonly Icon chevron = new() { Name = AccordionItemParts.Chevron, Data = Lucide.ChevronDown, Tone = TextTone.Muted, VerticalAlignment = VerticalAlignment.Center };
    readonly Collapse collapse = new() { Name = AccordionItemParts.Content };
    readonly Border body = new();

    /// <summary>新建：标题按钮在上，内容在下。</summary>
    public AccordionItem()
    {
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(chevron, Dock.Right);
        row.Children.AddRange([chevron, title]);
        Trigger.Content = row;
        Trigger.Click += (_, _) => Owner?.Toggle(Value ?? "");
        collapse.Content = body;
        root.Child = new StackPanel { Children = { Trigger, collapse } };
        // 底边一条 borderMuted 的线（borderWidths.xs）
        root[!Border.BorderBrushProperty] = root.GetResourceObservable("Rune.Brush.BorderMuted").ToBinding();
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>这一项的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>标题。</summary>
    public object? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>内容。</summary>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>标题按钮。</summary>
    internal AccordionTrigger Trigger { get; } = new() { Name = AccordionItemParts.Trigger };

    /// <summary>展开着。</summary>
    public bool IsOpen => Owner?.Expanded.Contains(Value ?? "") == true;

    Accordion? Owner => Parent as Accordion;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            title.Content = Title;
            AutomationProperties.SetName(collapse, Field.PlainText(Title));
        }
        else if (change.Property == ContentProperty) body.Child = Content;
        else if (change.Property == ValueProperty) Sync();
        else if (change.Property == TextElement.FontSizeProperty) UpdatePadding();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Sync();
        UpdatePadding();
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
        PseudoClasses.Set(":open", open);
        collapse.IsOpen = open;
        chevron.RenderTransform = open ? TransformOperations.Parse("rotate(180deg)") : TransformOperations.Identity;
        Trigger.SetValue(Popover.TriggerExpandedProperty, open);
        AutomationProperties.SetHeadingLevel(Trigger, Owner?.HeadingLevel ?? 3);
    }
}

/// <summary>手风琴的标题按钮：主题在 Accordion.axaml（悬停 fill 底、内边距、圆角）；焦点环画在按钮里面（整行宽，放进不留内边距的卡片时外扩的环会被裁掉）。</summary>
public class AccordionTrigger : ButtonBase
{
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
public class Collapsible : Control
{
    /// <summary>触发器：一个按钮。</summary>
    public static readonly StyledProperty<ButtonBase?> TriggerProperty = AvaloniaProperty.Register<Collapsible, ButtonBase?>(nameof(Trigger));

    /// <summary>展开着；默认双向绑定。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<Collapsible, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>不可用：触发器不可用，点击不切换。</summary>
    public static readonly StyledProperty<bool> IsDisabledProperty = AvaloniaProperty.Register<Collapsible, bool>(nameof(IsDisabled));

    /// <summary>内容。</summary>
    public static readonly StyledProperty<Control?> ContentProperty = AvaloniaProperty.Register<Collapsible, Control?>(nameof(Content));

    readonly StackPanel root = new();
    readonly Collapse collapse = new();

    /// <summary>新建：收起着。</summary>
    public Collapsible()
    {
        root.Children.Add(collapse);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>触发器。</summary>
    public ButtonBase? Trigger { get => GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }

    /// <summary>展开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>不可用。</summary>
    public bool IsDisabled { get => GetValue(IsDisabledProperty); set => SetValue(IsDisabledProperty, value); }

    /// <summary>内容。</summary>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TriggerProperty)
        {
            if (change.GetOldValue<ButtonBase?>() is { } old)
            {
                old.Click -= OnTriggerClick;
                root.Children.Remove(old);
            }
            if (Trigger is { } trigger)
            {
                trigger.Click += OnTriggerClick;
                trigger.HorizontalAlignment = HorizontalAlignment.Left;
                root.Children.Insert(0, trigger);
            }
            Sync();
        }
        else if (change.Property == IsOpenProperty || change.Property == IsDisabledProperty) Sync();
        else if (change.Property == ContentProperty) collapse.Content = Content;
    }

    void OnTriggerClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!IsDisabled) SetCurrentValue(IsOpenProperty, !IsOpen);
    }

    void Sync()
    {
        collapse.IsOpen = IsOpen;
        PseudoClasses.Set(":open", IsOpen);
        if (Trigger is not { } trigger) return;
        trigger.SetValue(Popover.TriggerExpandedProperty, IsOpen);
        trigger.IsEnabled = !IsDisabled;
    }
}

/// <summary>AccordionItem 里的部件名（代码组合的子元素）：<c>rune|AccordionItem rune|AccordionTrigger#PART_Trigger</c>。</summary>
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
