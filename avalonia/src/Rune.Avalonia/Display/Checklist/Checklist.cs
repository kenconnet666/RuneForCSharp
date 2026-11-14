using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using PlatformGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia;

/// <summary>可以勾选的清单里，一项的完成状态改了。</summary>
/// <param name="item">这一项。</param>
/// <param name="done">新的完成状态。</param>
public sealed class ChecklistCheckedEventArgs(ChecklistItem item, bool done) : EventArgs
{
    /// <summary>这一项。</summary>
    public ChecklistItem Item { get; } = item;

    /// <summary>新的完成状态（状态由使用方保存，再换掉 Items）。</summary>
    public bool Done { get; } = done;
}

/// <summary>
/// 完成清单（vue-tsx-pro 的 Checklist）：一组要做完的事，每项的状态（完成、待办、超时、受阻、不适用）与它的上下文——怎么做、谁负责、哪天、证据、去处理。
/// 里程碑与交付证据、结项材料、上线检查、新用户的引导任务。
/// <code>
/// &lt;rune:Checklist Items="{Binding Materials}" Summary="True" /&gt;
/// &lt;rune:Checklist Items="{Binding Nodes}" Layout="Icons" AutomationProperties.Name="里程碑" /&gt;
/// checklist.CheckedChange += (_, e) => Save(e.Item.Key, e.Done);   // 订阅了就能勾选
/// </code>
/// <see cref="Summary"/> 在上面显示"已完成 2 / 3"与 xs 的进度条（不适用的不算，全部完成时 success 色）。没完成的名称是 textMuted，不适用的划掉；超时的日期 dangerText。
/// 状态图标比文字大一档（1.14em），done 是 successText、overdue 是 dangerText、blocked 是 warningText，其余 textMuted；状态写在读屏的名称里。
/// 订阅了 <see cref="CheckedChange"/> 时状态图标是复选按钮（读屏的名称是这一项的名称，帮助文字是状态），点了报告新的完成状态；不适用、受阻的不能勾。
/// 排法见 <see cref="ChecklistLayout"/>。容器里的链接（证据）悬停才有下划线。
/// </summary>
public class Checklist : Control
{
    /// <summary>各项。</summary>
    public static readonly StyledProperty<IEnumerable<ChecklistItem>?> ItemsProperty = AvaloniaProperty.Register<Checklist, IEnumerable<ChecklistItem>?>(nameof(Items));

    /// <summary>排法，默认 list。</summary>
    public static readonly StyledProperty<ChecklistLayout> LayoutProperty = AvaloniaProperty.Register<Checklist, ChecklistLayout>(nameof(Layout));

    /// <summary>上面显示"已完成 2 / 3"与进度条（list 排法）。</summary>
    public static readonly StyledProperty<bool> SummaryProperty = AvaloniaProperty.Register<Checklist, bool>(nameof(Summary));

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Checklist, TextSize>(nameof(Size));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
    readonly TemplateResources resources;
    EventHandler<ChecklistCheckedEventArgs>? checkedChange;

    /// <summary>新建：空的清单。</summary>
    public Checklist()
    {
        resources = new TemplateResources(this);
        // 界面里的链接（证据、操作）悬停才有下划线
        Link.SetUnderlineScope(this, LinkUnderline.Hover);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>各项。</summary>
    public IEnumerable<ChecklistItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>排法。</summary>
    public ChecklistLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <summary>显示汇总。</summary>
    public bool Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>可以勾选：订阅了它，状态图标就是复选按钮；点了报告新的完成状态（不适用、受阻的不能勾）。</summary>
    public event EventHandler<ChecklistCheckedEventArgs>? CheckedChange
    {
        add
        {
            checkedChange += value;
            Rebuild();
        }
        remove
        {
            checkedChange -= value;
            Rebuild();
        }
    }

    bool Checkable => checkedChange is not null;

    /// <summary>显示的各行（测试用）：list 与 inline 排法的每一项。</summary>
    internal IReadOnlyList<ChecklistRow> Rows => [.. root.GetLogicalDescendants().OfType<ChecklistRow>()];

    /// <summary>汇总行（测试用）；没有时为 null。</summary>
    internal Flex? SummaryPart { get; private set; }

    /// <summary>图标排法的那一排（测试用）。</summary>
    internal Flex? IconsPart { get; private set; }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty || change.Property == LayoutProperty || change.Property == SummaryProperty) Rebuild();
        else if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(TextElement.FontSizeProperty, pixels);
            else resources.Bind(TextElement.FontSizeProperty, Size.ResourceKey);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // 状态文字取所在位置的语言包
        Rebuild();
    }

    /// <summary>按排法重建（同 TS 每次渲染重算）；重建前放开旧行里使用方给的控件，新行才放得进去。</summary>
    void Rebuild()
    {
        foreach (var row in Rows) row.Detach();
        root.Children.Clear();
        SummaryPart = IconsPart = null;
        var items = Items?.ToList() ?? [];
        var lang = Provider.GetLang(this);
        if (Layout == ChecklistLayout.Icons)
        {
            // 只有一排图标：整组是一张图，名称与提示是每一项的名称与状态
            var label = lang.ChecklistIconsLabel([.. items.Select(item => lang.ChecklistItemState(ChecklistModel.LabelText(item), lang.ChecklistStatus[ChecklistModel.LangKey(item.Status)]))]);
            IconsPart = new Flex { Align = FlexAlign.Center, Gap = Space.Xxs };
            foreach (var item in items) IconsPart.Children.Add(StatusIcon(item.Status));
            AutomationProperties.SetName(IconsPart, label);
            AutomationProperties.SetControlTypeOverride(IconsPart, AutomationControlType.Image);
            ToolTip.SetTip(IconsPart, label);
            root.Children.Add(IconsPart);
            return;
        }
        if (Layout == ChecklistLayout.Inline)
        {
            var inline = new FlexList { Wrap = FlexWrap.Wrap, ColumnGap = Space.Lg, RowGap = Space.Xs, Align = FlexAlign.Center };
            foreach (var item in items) inline.Children.Add(new ChecklistRow(this, item, inline: true));
            root.Children.Add(inline);
            return;
        }
        var (done, total) = ChecklistModel.Count(items);
        if (Summary && total > 0)
        {
            var text = lang.ChecklistSummary(done, total);
            var progress = new Progress { Name = ChecklistParts.Progress, Size = TextSize.Xs, Tone = done == total ? StatusTone.Success : StatusTone.Accent, Value = done, Max = total, ShowValueLabel = false };
            AutomationProperties.SetName(progress, text);
            Flex.SetGrow(progress, true);
            SummaryPart = new Flex { Name = ChecklistParts.Summary, Align = FlexAlign.Center, Gap = Space.Md, Children = { new Text { Text = text, Tone = TextTone.Muted, Size = TextSize.Sm }, progress } };
            root.Children.Add(SummaryPart);
        }
        var list = new FlexList { Name = ChecklistParts.List, Direction = FlexDirection.Column, Align = FlexAlign.Stretch };
        foreach (var item in items) list.Children.Add(new ChecklistRow(this, item, inline: false));
        root.Children.Add(list);
    }

    /// <summary>状态图标：done successText、overdue dangerText、blocked warningText，其余 textMuted。</summary>
    internal static Icon StatusIcon(ChecklistStatus status) => new()
    {
        Data = ChecklistModel.Icon(status),
        Tone = status switch
        {
            ChecklistStatus.Done => TextTone.Success,
            ChecklistStatus.Overdue => TextTone.Danger,
            ChecklistStatus.Blocked => TextTone.Warning,
            _ => TextTone.Muted,
        },
    };

    /// <summary>勾了、取消勾了一项。</summary>
    internal void Toggle(ChecklistItem item) => checkedChange?.Invoke(this, new ChecklistCheckedEventArgs(item, item.Status != ChecklistStatus.Done));

    internal bool CanCheck => Checkable;

    /// <inheritdoc />
    protected override global::Avalonia.Size MeasureOverride(global::Avalonia.Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override global::Avalonia.Size ArrangeOverride(global::Avalonia.Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ChecklistPeer(this);

    /// <summary>Checklist 自己不占读屏节点：里面的列表（或图标排法的那张图）才是。</summary>
    sealed class ChecklistPeer(Checklist owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>
/// 清单的一项（列表的一项）：list 排法是 [状态][名称、说明、负责人 · 日期 · 证据][操作]，状态贴着名称那一行，上下 space.xs；
/// inline 排法是一行 [状态][名称][日期][证据]，隔 space.xs。
/// </summary>
public sealed class ChecklistRow : Panel
{
    readonly List<Panel> owned = [];
    // 小一号的部分（说明、负责人、日期、证据）：使用方给的控件（证据的链接）也要小一号
    readonly List<Control> small = [];

    internal ChecklistRow(Checklist owner, ChecklistItem item, bool inline)
    {
        Item = item;
        Name = ChecklistParts.Item;
        var lang = Provider.GetLang(owner);
        var state = lang.ChecklistStatus[ChecklistModel.LangKey(item.Status)];
        var label = Label(item);
        Status = StatusPart(owner, item, state);
        var date = item.Date is null ? null : Part(item.Date, item.Status == ChecklistStatus.Overdue ? TextTone.Danger : TextTone.Muted, small: true);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.ListItem);
        if (inline)
        {
            var line = Own(new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { Status, label } });
            if (date is not null) line.Children.Add(date);
            if (item.Evidence is not null) line.Children.Add(Part(item.Evidence, TextTone.Muted, small: true));
            Children.Add(line);
            return;
        }
        var body = Own(new StackPanel { Name = ChecklistParts.Body });
        body.Children.Add(label);
        if (item.Description is not null) body.Children.Add(Description = Named(Part(item.Description, TextTone.Muted, small: true), ChecklistParts.Description));
        if (item.Owner is not null || date is not null || item.Evidence is not null)
        {
            Meta = Own(new Flex { Name = ChecklistParts.Meta, Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, ColumnGap = Space.Sm, RowGap = Space.Xxs });
            if (item.Owner is not null) Meta.Children.Add(Part(item.Owner, TextTone.Muted, small: true));
            if (date is not null) Meta.Children.Add(date);
            if (item.Evidence is not null) Meta.Children.Add(Part(item.Evidence, TextTone.Muted, small: true));
            body.Children.Add(Meta);
        }
        var grid = Own(new PlatformGrid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") });
        PlatformGrid.SetColumn(body, 1);
        grid.Children.AddRange([Status, body]);
        if (item.Action is not null)
        {
            // 操作包一层（TS 的 action 那一格）：样式按部件名选中，不动使用方给的控件
            Action = Part(item.Action, TextTone.Neutral, small: false);
            var cell = new Border { Name = ChecklistParts.Action, Child = Action, VerticalAlignment = VerticalAlignment.Top };
            PlatformGrid.SetColumn(cell, 2);
            grid.Children.Add(cell);
        }
        Children.Add(grid);
    }

    /// <summary>这一项。</summary>
    public ChecklistItem Item { get; }

    /// <summary>状态（图标，或可勾选时的复选按钮）。</summary>
    internal Control Status { get; }

    /// <summary>名称。</summary>
    internal Control? LabelPart { get; private set; }

    /// <summary>说明。</summary>
    internal Control? Description { get; }

    /// <summary>负责人、日期、证据那一行。</summary>
    internal Flex? Meta { get; }

    /// <summary>操作。</summary>
    internal Control? Action { get; }

    // 自己建的文字带部件名；使用方给的控件不改它的名字
    static Control Named(Control control, string name)
    {
        if (control is Text && control.Name is null) control.Name = name;
        return control;
    }

    T Own<T>(T panel) where T : Panel
    {
        owned.Add(panel);
        return panel;
    }

    /// <summary>放开使用方给的控件：重建时它们要放进新的一行。</summary>
    internal void Detach()
    {
        foreach (var panel in owned) panel.Children.Clear();
        if (Action?.Parent is Border cell) cell.Child = null;
        Children.Clear();
    }

    /// <summary>名称：没完成的是 textMuted，不适用的划掉。</summary>
    Control Label(ChecklistItem item)
    {
        var tone = item.Status == ChecklistStatus.Done ? TextTone.Neutral : TextTone.Muted;
        var label = Part(item.Label ?? "", tone, small: false);
        if (label is TextBlock text && item.Status == ChecklistStatus.Skipped) text.TextDecorations = TextDecorations.Strikethrough;
        LabelPart = Named(label, ChecklistParts.Label);
        return label;
    }

    /// <summary>状态：可以勾选时是复选按钮（名称是这一项的名称、帮助文字是状态），否则是图标（读屏的名称是状态）。比文字大一档，贴着名称那一行。</summary>
    static Control StatusPart(Checklist owner, ChecklistItem item, string state)
    {
        var icon = Checklist.StatusIcon(item.Status);
        if (!owner.CanCheck)
        {
            AutomationProperties.SetName(icon, state);
            return new StatusBox { Name = ChecklistParts.Status, Child = icon };
        }
        var toggle = new ChecklistToggle(item, ChecklistModel.LabelText(item), state) { Content = icon, IsEnabled = ChecklistModel.Checkable(item.Status) };
        toggle.Click += (_, _) => owner.Toggle(item);
        return new StatusBox { Name = ChecklistParts.Status, Child = toggle };
    }

    Control Part(object value, TextTone tone, bool small)
    {
        if (value is Control control)
        {
            if (small) this.small.Add(control);
            return control;
        }
        var text = new Text { Text = value.ToString(), Tone = tone, Size = small ? TextSize.Sm : default };
        text.TextWrapping = TextWrapping.Wrap;
        return text;
    }

    /// <inheritdoc />
    protected override global::Avalonia.Size MeasureOverride(global::Avalonia.Size availableSize)
    {
        UpdateMetrics();
        return base.MeasureOverride(availableSize);
    }

    /// <summary>按 em：上下 space.xs、状态与名称隔 space.sm、名称与说明隔 space.xxs、操作之间 space.xs。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        double Space(string key) => this.Resource<double>(key) * em;
        if (Children.FirstOrDefault() is PlatformGrid grid)
        {
            grid.ColumnSpacing = Space(ThemeKeys.Space.Sm);
            grid.Margin = new Thickness(0, Space(ThemeKeys.Space.Xs));
        }
        if (owned.OfType<StackPanel>().FirstOrDefault() is { } body) body.Spacing = Space(ThemeKeys.Space.Xxs);
        // fontSizes.sm 是固定的字号（同 TS 的 _sm），不跟着清单的档位
        var sm = this.Resource<double>(ThemeKeys.FontSize.Sm);
        foreach (var control in small) TextElement.SetFontSize(control, sm);
    }
}

/// <summary>状态那一格：图标比文字大一档（1.14em），高度至少一行，图标竖着居中在第一行里。</summary>
sealed class StatusBox : Decorator
{
    public StatusBox() => VerticalAlignment = VerticalAlignment.Top;

    protected override global::Avalonia.Size MeasureOverride(global::Avalonia.Size availableSize)
    {
        var em = TextElement.GetFontSize(this);
        if (Child is { } child)
        {
            TextElement.SetFontSize(child, 1.14 * em);
            child.VerticalAlignment = VerticalAlignment.Center;
        }
        var size = base.MeasureOverride(availableSize);
        // 至少一行高（TS 的 min-height: 1lh）
        return new global::Avalonia.Size(size.Width, Math.Max(size.Height, em * this.Resource<double>(ThemeKeys.LineHeight.Normal)));
    }
}

/// <summary>可以勾选时的状态：只有图标的复选按钮，可点的区域不小于 24 像素，悬停时圆圈后面一层 fillHover。</summary>
sealed class ChecklistToggle(ChecklistItem item, string label, string state) : ButtonBase
{
    ChecklistItem Item { get; } = item;

    string Label { get; } = label;

    string State { get; } = state;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var target = this.Resource<double>(ThemeKeys.Size.MinTarget);
        MinWidth = MinHeight = target;
        CornerRadius = new CornerRadius(target / 2);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (IsEffectivelyEnabled) Background = this.Resource<IBrush>(ThemeKeys.Brush.FillHover);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Background = Brushes.Transparent;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TogglePeer(this);

    /// <summary>读屏：复选框，名称是这一项的名称，帮助文字是状态。</summary>
    sealed class TogglePeer(ChecklistToggle owner) : ControlAutomationPeer(owner), IToggleProvider, IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;

        protected override string GetClassNameCore() => nameof(ChecklistToggle);

        protected override string? GetNameCore() => owner.Label;

        protected override string? GetHelpTextCore() => owner.State;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        public ToggleState ToggleState => owner.Item.Status == ChecklistStatus.Done ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        public void Invoke()
        {
            if (owner.IsEffectivelyEnabled) owner.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(ClickEvent));
        }
    }
}

/// <summary>Checklist 的部件名：样式里用 <c>rune|Checklist rune|Text#PART_Label</c> 这样选中。</summary>
public static class ChecklistParts
{
    /// <summary>汇总行（TS 的 propsSummary）。</summary>
    public const string Summary = "PART_Summary";

    /// <summary>汇总行里的进度条（TS 的 propsProgress）。</summary>
    public const string Progress = "PART_Progress";

    /// <summary>列表（TS 的 propsList）。</summary>
    public const string List = "PART_List";

    /// <summary>每一项（TS 的 propsItem）。</summary>
    public const string Item = "PART_Item";

    /// <summary>状态（TS 的 propsStatus）。</summary>
    public const string Status = "PART_Status";

    /// <summary>名称、说明与负责人那一列（TS 的 body）。</summary>
    public const string Body = "PART_Body";

    /// <summary>名称（TS 的 propsLabel）。</summary>
    public const string Label = "PART_Label";

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public const string Description = "PART_Description";

    /// <summary>负责人、日期、证据那一行（TS 的 propsMeta）。</summary>
    public const string Meta = "PART_Meta";

    /// <summary>操作那一格（TS 的 propsAction）。</summary>
    public const string Action = "PART_Action";
}
