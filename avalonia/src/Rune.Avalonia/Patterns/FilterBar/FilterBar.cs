using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;

namespace Rune.Avalonia;

/// <summary>
/// 查询栏（vue-tsx-pro 的 FilterBar）：列表页上方的一排筛选条件，最后是"查询""重置"；条件多时其余的收起来，点"展开"再显示。
/// <code>
/// &lt;rune:FilterBar SearchRequested="OnSearch" ResetRequested="OnReset" MoreActive="2"&gt;
///   &lt;rune:Field Label="项目名称"&gt;&lt;rune:TextField /&gt;&lt;/rune:Field&gt;
///   &lt;rune:FilterBar.More&gt;&lt;rune:Field Label="完成年度"&gt;…&lt;/rune:Field&gt;&lt;/rune:FilterBar.More&gt;
/// &lt;/rune:FilterBar&gt;
/// </code>
/// <list type="bullet">
/// <item>默认只显示一排：<see cref="Start"/>（标题）、条件、按钮区。按钮全在这一排的结束边；按剩下的宽度算出放得下几个条件（每个不窄于 <see cref="MinFieldWidth"/>），
/// 常用条件（<see cref="Fields"/>）在前、<see cref="More"/> 在后，放得下的铺满这一排；其余的都收进"展开"，展开后从第二排起，列宽与第一排的每格相同、上下对齐。
/// 全部放得下时不出"展开"；窄到一个条件也放不下时条件独占一排、按钮区换到下面一行。比一格宽的条件包一层 <see cref="GridItem"/>（Span="2"）跨两格。</item>
/// <item>按钮区依次是展开、高级筛选、<see cref="Actions"/>、重置、查询，再是 <see cref="End"/>。<see cref="MoreActive"/> 是收起的条件里已经填了几项，显示在"展开"旁边。</item>
/// <item>标签默认在起始边、宽度随文字（Field 的 LabelPlacement、LabelWidth 继承下去）；在单行输入框里按回车就是查询。</item>
/// <item>高级筛选：给了 <see cref="ConditionFields"/> 时按钮区多一个"高级筛选"，展开后在下面用 <see cref="ConditionBuilder"/> 编辑 <see cref="Condition"/>；
/// 收起时有条件就写成一句话，带"清空"；"重置"同时把条件清成 null。</item>
/// </list>
/// 部件：<see cref="FilterBarParts"/>。
/// </summary>
public class FilterBar : Control
{
    /// <summary>查询中："查询"按钮显示加载中。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<FilterBar, bool>(nameof(IsLoading));

    /// <summary>是否展开了第一排放不下的条件（双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<FilterBar, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>收起的条件里已经填了几项：大于 0 时显示在"展开"旁边。</summary>
    public static readonly StyledProperty<int> MoreActiveProperty = AvaloniaProperty.Register<FilterBar, int>(nameof(MoreActive));

    /// <summary>每个条件的最小宽度（em），默认 14。</summary>
    public static readonly StyledProperty<double> MinFieldWidthProperty = AvaloniaProperty.Register<FilterBar, double>(nameof(MinFieldWidth), 14);

    /// <summary>按钮区额外的按钮：放在"重置"前面（如"导出筛选结果"）。</summary>
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<FilterBar, object?>(nameof(Actions));

    /// <summary>第一排起始边的内容：在条件前面（QueryTable 的标题）。</summary>
    public static readonly StyledProperty<object?> StartProperty = AvaloniaProperty.Register<FilterBar, object?>(nameof(Start));

    /// <summary>按钮区最后的内容：在"查询"后面（QueryTable 的工具栏按钮）。</summary>
    public static readonly StyledProperty<object?> EndProperty = AvaloniaProperty.Register<FilterBar, object?>(nameof(End));

    /// <summary>高级筛选的字段：给了时按钮区多一个"高级筛选"。</summary>
    public static readonly StyledProperty<IReadOnlyList<ConditionField>?> ConditionFieldsProperty = AvaloniaProperty.Register<FilterBar, IReadOnlyList<ConditionField>?>(nameof(ConditionFields));

    /// <summary>高级筛选的条件（双向绑定）：没有时是 null，"重置"时清成 null。</summary>
    public static readonly StyledProperty<Condition?> ConditionProperty = AvaloniaProperty.Register<FilterBar, Condition?>(nameof(Condition), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>条件的标签位置，默认起始边（同 Field.LabelPlacement，继承给里面的 Field）。</summary>
    public static readonly StyledProperty<FieldLabelPlacement> LabelPlacementProperty = Field.LabelPlacementProperty.AddOwner<FilterBar>();

    /// <summary>条件的标签宽度，默认随文字（<see cref="Field.AutoLabelWidth"/>，同 Field.LabelWidth）；要几行对齐时写固定宽度。</summary>
    public static readonly StyledProperty<double> LabelWidthProperty = Field.LabelWidthProperty.AddOwner<FilterBar>();

    readonly ContentPresenter start = new() { Name = FilterBarParts.Start, VerticalAlignment = VerticalAlignment.Center };
    readonly Flex actions = new() { Name = FilterBarParts.Actions, Wrap = FlexWrap.Wrap, Justify = FlexJustify.End, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Button toggle = new() { Name = FilterBarParts.Toggle, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, IsVisible = false };
    readonly Button advancedToggle = new() { Name = FilterBarParts.AdvancedToggle, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Icon = Lucide.ListFilter, IsVisible = false };
    // 没有内容时不显示：按钮区的 Flex 给每个显示着的子元素都留间距
    readonly ContentPresenter actionsHost = new() { IsVisible = false };
    readonly Button reset = new() { Name = FilterBarParts.Reset, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral };
    readonly Button search = new() { Name = FilterBarParts.Search };
    readonly ContentPresenter endHost = new() { IsVisible = false };
    readonly ConditionBuilder builder = new();
    readonly Border advanced = new() { Name = FilterBarParts.Advanced, IsVisible = false };
    readonly Small summaryText = new() { Tone = TextTone.Muted, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
    readonly Button clearAdvanced = new() { Name = FilterBarParts.ClearAdvanced, Variant = ButtonVariant.Link, Size = TextSize.Sm };
    readonly Flex summary = new() { Name = FilterBarParts.Summary, Align = FlexAlign.Center, Gap = Space.Sm, IsVisible = false };
    // 每个条件包一格：收起的格子不显示（不改使用方控件自己的 IsVisible）
    readonly List<Decorator> cells = [];
    bool advancedOpen;
    // 上次量到的"展开"按钮宽度（含间距）：它没显示时按钮区仍按带着它算，两种情况比的是同一个基准，不会来回切换
    double lastToggle;
    // 这次排版的结果（Arrange 用）
    readonly Dictionary<Control, Rect> slots = [];

    /// <summary>新建：按钮区、高级筛选。</summary>
    public FilterBar()
    {
        Fields.CollectionChanged += (_, _) => SyncCells();
        More.CollectionChanged += (_, _) => SyncCells();
        SetValue(LabelPlacementProperty, FieldLabelPlacement.Start, BindingPriority.Template);
        SetValue(LabelWidthProperty, Field.AutoLabelWidth, BindingPriority.Template);
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Search);
        toggle.Click += (_, _) => SetCurrentValue(IsExpandedProperty, !IsExpanded);
        advancedToggle.Click += (_, _) =>
        {
            advancedOpen = !advancedOpen;
            Sync();
        };
        reset.Click += (_, _) =>
        {
            if (ConditionFields is not null) SetCurrentValue(ConditionProperty, null);
            ResetRequested?.Invoke(this, EventArgs.Empty);
        };
        search.Click += (_, _) => SearchRequested?.Invoke(this, EventArgs.Empty);
        clearAdvanced.Click += (_, _) => SetCurrentValue(ConditionProperty, null);
        builder.PropertyChanged += (_, e) =>
        {
            if (e.Property == ConditionBuilder.ValueProperty) SetCurrentValue(ConditionProperty, builder.Value);
        };
        advanced.Child = builder;
        advanced[!Border.BackgroundProperty] = this.GetResourceObservable(ThemeKeys.Brush.SurfaceMuted).ToBinding();
        advanced[!Border.BorderBrushProperty] = this.GetResourceObservable(ThemeKeys.Brush.BorderMuted).ToBinding();
        summary.Children.AddRange([summaryText, clearAdvanced]);
        actions.Children.AddRange([toggle, advancedToggle, actionsHost, reset, search, endHost]);
        // 在单行输入框里按回车就是查询（上游是原生表单提交）
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        Adopt(start);
        Adopt(actions);
        Adopt(advanced);
        Adopt(summary);
    }

    /// <summary>点"查询"或在输入框里按回车。</summary>
    public event EventHandler? SearchRequested;

    /// <summary>点"重置"：清空条件由使用方做（通常回到初始值，再查询一次）。</summary>
    public event EventHandler? ResetRequested;

    /// <summary>常用的条件：Field。</summary>
    [Content]
    public AvaloniaList<Control> Fields { get; } = [];

    /// <summary>不常用的条件：排在常用条件后面，第一排放不下的点"展开"才显示。</summary>
    public AvaloniaList<Control> More { get; } = [];

    /// <summary>查询中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>是否展开了第一排放不下的条件。</summary>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>收起的条件里已经填了几项。</summary>
    public int MoreActive { get => GetValue(MoreActiveProperty); set => SetValue(MoreActiveProperty, value); }

    /// <summary>每个条件的最小宽度（em）。</summary>
    public double MinFieldWidth { get => GetValue(MinFieldWidthProperty); set => SetValue(MinFieldWidthProperty, value); }

    /// <summary>按钮区额外的按钮。</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <summary>第一排起始边的内容。</summary>
    public object? Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <summary>按钮区最后的内容。</summary>
    public object? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>高级筛选的字段。</summary>
    public IReadOnlyList<ConditionField>? ConditionFields { get => GetValue(ConditionFieldsProperty); set => SetValue(ConditionFieldsProperty, value); }

    /// <summary>高级筛选的条件。</summary>
    public Condition? Condition { get => GetValue(ConditionProperty); set => SetValue(ConditionProperty, value); }

    /// <summary>条件的标签位置。</summary>
    public FieldLabelPlacement LabelPlacement { get => GetValue(LabelPlacementProperty); set => SetValue(LabelPlacementProperty, value); }

    /// <summary>条件的标签宽度。</summary>
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>高级筛选的条件构造器（测试用）。</summary>
    internal ConditionBuilder Builder => builder;

    void Adopt(Control child)
    {
        LogicalChildren.Add(child);
        VisualChildren.Add(child);
    }

    void Drop(Control child)
    {
        LogicalChildren.Remove(child);
        VisualChildren.Remove(child);
    }

    /// <summary>条件变了：重新包格子（常用在前、More 在后）。</summary>
    void SyncCells()
    {
        foreach (var cell in cells)
        {
            cell.Child = null;
            Drop(cell);
        }
        cells.Clear();
        foreach (var field in Fields.Concat(More))
        {
            var cell = new Decorator { Child = field };
            cells.Add(cell);
            Adopt(cell);
        }
        InvalidateMeasure();
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter || e.Source is not TextBox { AcceptsReturn: false }) return;
        e.Handled = true;
        SearchRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StartProperty) start.Content = Start;
        else if (change.Property == ActionsProperty) (actionsHost.Content, actionsHost.IsVisible) = (Actions, Actions is not null);
        else if (change.Property == EndProperty) (endHost.Content, endHost.IsVisible) = (End, End is not null);
        else if (change.Property == IsExpandedProperty || change.Property == MinFieldWidthProperty) InvalidateMeasure();
        if (change.Property == IsExpandedProperty || change.Property == MoreActiveProperty || change.Property == IsLoadingProperty || change.Property == ConditionFieldsProperty
            || change.Property == ConditionProperty || change.Property == Provider.LangProperty) Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Sync();
    }

    /// <summary>按状态：按钮的文字与计数、高级筛选与摘要。</summary>
    void Sync()
    {
        var messages = PatternsMessages.For(Provider.GetLang(this));
        var lang = Provider.GetLang(this);
        toggle.Content = Label(IsExpanded ? messages.FilterCollapse : messages.FilterExpand, !IsExpanded && MoreActive > 0 ? messages.FilterActive(MoreActive) : null);
        toggle.IconEnd = IsExpanded ? Lucide.ChevronUp : Lucide.ChevronDown;
        toggle.SetValue(Popover.TriggerExpandedProperty, IsExpanded);
        var fields = ConditionFields;
        advancedToggle.IsVisible = fields is not null;
        advancedToggle.Content = Label(messages.FilterAdvanced, !advancedOpen && Condition is { } counted ? messages.FilterActive(CountRules(counted)) : null);
        advancedToggle.SetValue(Popover.TriggerExpandedProperty, fields is null ? null : advancedOpen);
        reset.Content = messages.FilterReset;
        search.Content = messages.FilterSearch;
        search.IsLoading = IsLoading;
        advanced.IsVisible = fields is not null && advancedOpen;
        if (fields is not null)
        {
            builder.Fields = fields;
            builder.Value = Condition;
            AutomationProperties.SetName(builder, messages.FilterAdvanced);
        }
        summary.IsVisible = fields is not null && !advancedOpen && Condition is not null;
        summaryText.Text = fields is not null && Condition is { } condition ? $"{messages.FilterAdvanced}：{condition.Describe(fields, lang)}" : "";
        clearAdvanced.Content = lang.Clear;
        advanced.Padding = new Thickness(Token(ThemeKeys.Space.Md));
        advanced.BorderThickness = new Thickness(this.Resource<double>(ThemeKeys.BorderWidth.Xs));
        advanced.CornerRadius = new CornerRadius(Token(ThemeKeys.Radius.Md));
        InvalidateMeasure();
    }

    /// <summary>按钮的文字，带计数时后面是个 accent 的圆角徽标。</summary>
    static object Label(string text, string? count) => count is null
        ? text
        : new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new TextBlock { Text = text }, new Badge { Name = FilterBarParts.Count, Tone = StatusTone.Accent, Radius = Radius.Full, Content = count } } };

    /// <summary>条件里有几条比较（按钮旁的计数）。</summary>
    static int CountRules(Condition condition) => condition switch
    {
        ConditionRule => 1,
        ConditionNot not => CountRules(not.Inner),
        ConditionAll all => all.Items.Sum(CountRules),
        ConditionAny any => any.Items.Sum(CountRules),
        _ => 0,
    };

    double Em => TextElement.GetFontSize(this);

    double Token(string key) => this.Resource<double>(key) * Em;

    double ControlHeight => this.Resource<double>(ThemeKeys.Size.Control) * Em;

    /// <summary>
    /// 第一排放得下几个条件：条件区 = 整排 − 按钮区 − 间距，n 格占 n × min + (n − 1) × gap；一个也放不下时（First 为 null）条件独占一排、按钮区换到下面一行。
    /// 全部放得下时不出"展开"。Cell 是第一排每格的宽度，展开的几排列宽跟它一样（同上游的 layoutFields）。
    /// </summary>
    internal static (bool Toggle, int Shown, int? First, double Cell) Layout(double row, double gap, double min, double actionsWithToggle, double toggleWidth, int total)
    {
        int? Fit(double width)
        {
            var n = (int)Math.Floor((row - width) / (min + gap));
            return n > 0 ? n : null;
        }
        var alone = Math.Max(1, (int)Math.Floor((row + gap) / (min + gap)));
        var bare = Fit(actionsWithToggle - toggleWidth);
        var hasToggle = bare is null ? alone < total : bare < total;
        var first = hasToggle ? Fit(actionsWithToggle) : bare;
        var shown = Math.Min(total, first ?? alone);
        var area = first is null ? row : row - (hasToggle ? actionsWithToggle : actionsWithToggle - toggleWidth) - gap;
        var cell = shown > 0 ? (area - (shown - 1) * gap) / shown : 0;
        return (hasToggle, shown, first, cell);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        slots.Clear();
        var gap = Token(ThemeKeys.Space.Xl);
        var rowGap = Token(ThemeKeys.Space.Md);
        var min = MinFieldWidth * Em;
        var width = double.IsInfinity(availableSize.Width) ? 64 * Em : availableSize.Width;
        // start（标题）单独一列，其余都在它右边；窄到放不下时标题自己一行
        var y = 0.0;
        var bodyX = 0.0;
        if (Start is not null)
        {
            start.Measure(new Size(width, double.PositiveInfinity));
            var startSize = new Size(start.DesiredSize.Width, Math.Max(start.DesiredSize.Height, ControlHeight));
            if (startSize.Width + gap + min <= width)
            {
                bodyX = startSize.Width + gap;
                slots[start] = new Rect(new Point(0, 0), startSize);
            }
            else
            {
                slots[start] = new Rect(new Point(0, 0), startSize);
                y = startSize.Height + rowGap;
            }
        }
        var body = Math.Max(0, width - bodyX);

        // 按钮区：带着"展开"量；它没显示时用上次量到的宽度。头一回要显示"展开"时还没量过它：显示出来、量过再算一遍
        actions.MinHeight = ControlHeight;
        (bool Toggle, int Shown, int? First, double Cell) layout;
        while (true)
        {
            actions.Measure(new Size(body, double.PositiveInfinity));
            var toggleWidth = toggle.IsVisible ? toggle.DesiredSize.Width + Token(ThemeKeys.Space.Sm) : lastToggle;
            var measured = toggle.IsVisible;
            if (measured) lastToggle = toggleWidth;
            layout = Layout(body, gap, min, actions.DesiredSize.Width + (measured ? 0 : toggleWidth), toggleWidth, cells.Count);
            if (toggle.IsVisible == layout.Toggle) break;
            toggle.IsVisible = layout.Toggle;
            if (measured || lastToggle > 0)
            {
                actions.Measure(new Size(body, double.PositiveInfinity));
                break;
            }
        }
        var expanded = layout.Toggle && IsExpanded;
        for (var i = 0; i < cells.Count; i++) cells[i].IsVisible = i < layout.Shown || expanded;

        // 第一排：条件（每格 Cell 宽）与按钮区；一个也放不下时按钮区换到下面一行
        var rowTop = y;
        var first = cells.Take(layout.Shown).ToList();
        var fieldsHeight = Place(first, bodyX, rowTop, layout.Cell, gap, rowGap, layout.Shown);
        var actionsSize = actions.DesiredSize;
        if (layout.First is null)
        {
            var below = rowTop + fieldsHeight + (fieldsHeight > 0 ? rowGap : 0);
            slots[actions] = new Rect(bodyX + body - actionsSize.Width, below, actionsSize.Width, actionsSize.Height);
            y = below + actionsSize.Height;
        }
        else
        {
            slots[actions] = new Rect(bodyX + body - actionsSize.Width, rowTop, actionsSize.Width, actionsSize.Height);
            y = rowTop + Math.Max(fieldsHeight, actionsSize.Height);
        }

        // 第二排起：列宽与第一排的每格相同、上下对齐
        if (expanded && cells.Count > layout.Shown)
        {
            var columns = Math.Max(1, (int)Math.Floor((body + gap) / (layout.Cell + gap)));
            y += rowGap;
            y += Place(cells.Skip(layout.Shown).ToList(), bodyX, y, Math.Min(layout.Cell, body), gap, rowGap, columns);
        }
        foreach (var part in new Control[] { advanced, summary })
        {
            if (!part.IsVisible) continue;
            part.Measure(new Size(body, double.PositiveInfinity));
            y += rowGap;
            slots[part] = new Rect(bodyX, y, body, part.DesiredSize.Height);
            y += part.DesiredSize.Height;
        }
        return new Size(width, Math.Max(y, Start is not null ? slots[start].Bottom : 0));
    }

    /// <summary>格子按列排（GridItem 的 Span 跨几格），每格 cell 宽；返回占的高度。</summary>
    double Place(List<Decorator> items, double x0, double y0, double cell, double gap, double rowGap, int columns)
    {
        if (items.Count == 0) return 0;
        var column = 0;
        var y = y0;
        var lineHeight = 0.0;
        var line = new List<(Decorator Cell, Rect Slot)>();
        void Flush()
        {
            foreach (var (item, slot) in line) slots[item] = slot.WithHeight(lineHeight);
            line.Clear();
        }
        foreach (var item in items)
        {
            var span = Math.Clamp(item.Child is GridItem { Span: var s } && int.TryParse(s.Value, out var n) ? n : 1, 1, Math.Max(1, columns));
            if (column + span > columns && column > 0)
            {
                Flush();
                y += lineHeight + rowGap;
                lineHeight = 0;
                column = 0;
            }
            var w = span * cell + (span - 1) * gap;
            item.Measure(new Size(w, double.PositiveInfinity));
            lineHeight = Math.Max(lineHeight, item.DesiredSize.Height);
            line.Add((item, new Rect(x0 + column * (cell + gap), y, w, item.DesiredSize.Height)));
            column += span;
        }
        Flush();
        return y + lineHeight - y0;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (child, slot) in slots) child.Arrange(slot);
        foreach (var cell in cells.Where(cell => !slots.ContainsKey(cell))) cell.Arrange(default);
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new FilterBarAutomationPeer(this);

    sealed class FilterBarAutomationPeer(FilterBar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => "search";
    }
}

/// <summary>FilterBar 的部件名。</summary>
public static class FilterBarParts
{
    /// <summary>起始边的内容（标题）。</summary>
    public const string Start = "PART_Start";

    /// <summary>按钮区。</summary>
    public const string Actions = "PART_Actions";

    /// <summary>展开收起的按钮。</summary>
    public const string Toggle = "PART_Toggle";

    /// <summary>高级筛选的按钮。</summary>
    public const string AdvancedToggle = "PART_AdvancedToggle";

    /// <summary>重置按钮。</summary>
    public const string Reset = "PART_Reset";

    /// <summary>查询按钮。</summary>
    public const string Search = "PART_Search";

    /// <summary>高级筛选那一行（条件构造器的外框）。</summary>
    public const string Advanced = "PART_Advanced";

    /// <summary>收起高级筛选后的摘要那一行。</summary>
    public const string Summary = "PART_Summary";

    /// <summary>摘要后面"清空"条件的按钮。</summary>
    public const string ClearAdvanced = "PART_ClearAdvanced";

    /// <summary>按钮旁的计数徽标。</summary>
    public const string Count = "PART_Count";
}
