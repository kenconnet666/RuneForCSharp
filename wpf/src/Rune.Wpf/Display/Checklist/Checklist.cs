using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

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
public class Checklist : CompositeControl
{
    /// <summary>各项。</summary>
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IEnumerable<ChecklistItem>), typeof(Checklist), new PropertyMetadata(null, OnRebuild));

    /// <summary>排法，默认 list。</summary>
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(nameof(Layout), typeof(ChecklistLayout), typeof(Checklist), new PropertyMetadata(ChecklistLayout.List, OnRebuild));

    /// <summary>上面显示"已完成 2 / 3"与进度条（list 排法）。</summary>
    public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(nameof(Summary), typeof(bool), typeof(Checklist), new PropertyMetadata(false, OnRebuild));

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Checklist),
        new PropertyMetadata(default(TextSize), (d, _) => ((Checklist)d).ApplySize()));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
    readonly List<ChecklistRow> rows = [];
    EventHandler<ChecklistCheckedEventArgs>? checkedChange;

    /// <summary>新建：空的清单。</summary>
    public Checklist()
    {
        Root = root;
        // 界面里的链接（证据、操作）悬停才有下划线
        Link.SetUnderlineScope(this, LinkUnderline.Hover);
        // 状态文字取所在位置的语言包
        Loaded += (_, _) => Rebuild();
    }

    /// <summary>各项。</summary>
    public IEnumerable<ChecklistItem>? Items { get => (IEnumerable<ChecklistItem>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>排法。</summary>
    public ChecklistLayout Layout { get => (ChecklistLayout)GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <summary>显示汇总。</summary>
    public bool Summary { get => (bool)GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

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

    internal bool CanCheck => checkedChange is not null;

    /// <summary>显示的各行（测试用）：list 与 inline 排法的每一项。</summary>
    internal IReadOnlyList<ChecklistRow> Rows => rows;

    /// <summary>汇总行（测试用）；没有时为 null。</summary>
    internal Flex? SummaryPart { get; private set; }

    /// <summary>图标排法的那一排（测试用）。</summary>
    internal Flex? IconsPart { get; private set; }

    static void OnRebuild(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Checklist)d).Rebuild();

    /// <summary>字号档位换成主题的字号资源（像素值直接用）；不写时继承外面的。</summary>
    void ApplySize()
    {
        if (Size.Pixels is { } pixels) SetValue(FontSizeProperty, pixels);
        else if (Size.ResourceKey is { } key) SetResourceReference(FontSizeProperty, key);
        else ClearValue(FontSizeProperty);
    }

    /// <summary>按排法重建（同 TS 每次渲染重算）；重建前放开旧行里使用方给的元素，新行才放得进去。</summary>
    void Rebuild()
    {
        foreach (var row in rows) row.Detach();
        rows.Clear();
        root.Children.Clear();
        SummaryPart = IconsPart = null;
        var items = Items?.ToList() ?? [];
        var lang = Provider.GetLang(this);
        if (Layout == ChecklistLayout.Icons)
        {
            // 只有一排图标：整组是一张图，名称与提示是每一项的名称与状态
            var label = lang.ChecklistIconsLabel([.. items.Select(item => lang.ChecklistItemState(ChecklistModel.LabelText(item), lang.ChecklistStatus[ChecklistModel.LangKey(item.Status)]))]);
            IconsPart = new IconStrip { Align = FlexAlign.Center, Gap = Space.Xxs, ToolTip = label };
            foreach (var item in items) IconsPart.Children.Add(StatusIcon(item.Status));
            AutomationProperties.SetName(IconsPart, label);
            root.Children.Add(IconsPart);
            return;
        }
        if (Layout == ChecklistLayout.Inline)
        {
            var inline = new FlexList { Wrap = FlexWrap.Wrap, ColumnGap = Space.Lg, RowGap = Space.Xs, Align = FlexAlign.Center };
            foreach (var item in items) inline.Children.Add(Add(new ChecklistRow(this, item, inline: true)));
            root.Children.Add(inline);
            return;
        }
        var (done, total) = ChecklistModel.Count(items);
        if (Summary && total > 0)
        {
            var text = lang.ChecklistSummary(done, total);
            var progress = new Progress { Size = TextSize.Xs, Tone = done == total ? StatusTone.Success : StatusTone.Accent, Value = done, Max = total, ShowValueLabel = false };
            AutomationProperties.SetName(progress, text);
            Flex.SetGrow(progress, true);
            SummaryPart = new Flex { Align = FlexAlign.Center, Gap = Space.Md, Children = { new Text { Text = text, Tone = TextTone.Muted, Size = TextSize.Sm }, progress } };
            root.Children.Add(SummaryPart);
        }
        var list = new FlexList { Direction = FlexDirection.Column, Align = FlexAlign.Stretch };
        foreach (var item in items) list.Children.Add(Add(new ChecklistRow(this, item, inline: false)));
        root.Children.Add(list);
    }

    ChecklistRow Add(ChecklistRow row)
    {
        rows.Add(row);
        return row;
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

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ChecklistPeer(this);

    /// <summary>Checklist 自己不占读屏节点：里面的列表（或图标排法的那张图）才是。</summary>
    sealed class ChecklistPeer(Checklist owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }

    /// <summary>图标排法的那一排：读屏里是一张图（名称是每一项的名称与状态），里面的图标不另念。</summary>
    sealed class IconStrip : Flex
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new StripPeer(this);

        sealed class StripPeer(IconStrip owner) : FrameworkElementAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

            protected override List<AutomationPeer>? GetChildrenCore() => null;
        }
    }
}

/// <summary>
/// 清单的一项（列表的一项）：list 排法是 [状态][名称、说明、负责人 · 日期 · 证据][操作]，状态贴着名称那一行，上下 space.xs；
/// inline 排法是一行 [状态][名称][日期][证据]，隔 space.xs。
/// </summary>
public sealed class ChecklistRow : Decorator
{
    readonly List<Panel> owned = [];
    // 小一号的部分（说明、负责人、日期、证据）：使用方给的元素（证据的链接）也要小一号
    readonly List<FrameworkElement> small = [];
    readonly StackPanel? body;

    internal ChecklistRow(Checklist owner, ChecklistItem item, bool inline)
    {
        Item = item;
        var lang = Provider.GetLang(owner);
        var state = lang.ChecklistStatus[ChecklistModel.LangKey(item.Status)];
        var label = Label(item);
        Status = StatusPart(owner, item, state);
        var date = item.Date is null ? null : Part(item.Date, item.Status == ChecklistStatus.Overdue ? TextTone.Danger : TextTone.Muted, isSmall: true);
        Loaded += (_, _) => UpdateMetrics();
        if (inline)
        {
            var line = Own(new Flex { Align = FlexAlign.Center, Gap = Space.Xs });
            line.Children.Add(Status);
            line.Children.Add(label);
            if (date is not null) line.Children.Add(date);
            if (item.Evidence is not null) line.Children.Add(Part(item.Evidence, TextTone.Muted, isSmall: true));
            Child = line;
            return;
        }
        body = Own(new StackPanel());
        body.Children.Add(label);
        if (item.Description is not null) body.Children.Add(Description = Part(item.Description, TextTone.Muted, isSmall: true));
        if (item.Owner is not null || date is not null || item.Evidence is not null)
        {
            Meta = Own(new Flex { Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, ColumnGap = Space.Sm, RowGap = Space.Xxs });
            if (item.Owner is not null) Meta.Children.Add(Part(item.Owner, TextTone.Muted, isSmall: true));
            if (date is not null) Meta.Children.Add(date);
            if (item.Evidence is not null) Meta.Children.Add(Part(item.Evidence, TextTone.Muted, isSmall: true));
            body.Children.Add(Meta);
        }
        var grid = Own(new LayoutGrid());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        LayoutGrid.SetColumn(body, 1);
        grid.Children.Add(Status);
        grid.Children.Add(body);
        if (item.Action is not null)
        {
            Action = Part(item.Action, TextTone.Neutral, isSmall: false);
            Action.VerticalAlignment = VerticalAlignment.Top;
            LayoutGrid.SetColumn(Action, 2);
            grid.Children.Add(Action);
        }
        Child = grid;
    }

    /// <summary>这一项。</summary>
    public ChecklistItem Item { get; }

    /// <summary>状态（图标，或可勾选时的复选按钮）。</summary>
    internal FrameworkElement Status { get; }

    /// <summary>名称。</summary>
    internal FrameworkElement? LabelPart { get; private set; }

    /// <summary>说明。</summary>
    internal FrameworkElement? Description { get; }

    /// <summary>负责人、日期、证据那一行。</summary>
    internal Flex? Meta { get; }

    /// <summary>操作。</summary>
    internal FrameworkElement? Action { get; }

    T Own<T>(T panel) where T : Panel
    {
        owned.Add(panel);
        return panel;
    }

    /// <summary>放开使用方给的元素：重建时它们要放进新的一行。</summary>
    internal void Detach()
    {
        foreach (var panel in owned) panel.Children.Clear();
        Child = null;
    }

    /// <summary>名称：没完成的是 textMuted，不适用的划掉。</summary>
    FrameworkElement Label(ChecklistItem item)
    {
        var label = Part(item.Label ?? "", item.Status == ChecklistStatus.Done ? TextTone.Neutral : TextTone.Muted, isSmall: false);
        if (label is TextBlock text && item.Status == ChecklistStatus.Skipped) text.TextDecorations = TextDecorations.Strikethrough;
        LabelPart = label;
        return label;
    }

    /// <summary>状态：可以勾选时是复选按钮（名称是这一项的名称、帮助文字是状态），否则是图标（读屏的名称是状态）。比文字大一档，贴着名称那一行。</summary>
    static FrameworkElement StatusPart(Checklist owner, ChecklistItem item, string state)
    {
        var icon = Checklist.StatusIcon(item.Status);
        if (!owner.CanCheck)
        {
            AutomationProperties.SetName(icon, state);
            return new StatusBox { Child = icon };
        }
        var toggle = new ChecklistToggle(item, ChecklistModel.LabelText(item), state) { Content = icon, IsEnabled = ChecklistModel.Checkable(item.Status) };
        toggle.Click += (_, _) => owner.Toggle(item);
        return new StatusBox { Child = toggle };
    }

    FrameworkElement Part(object value, TextTone tone, bool isSmall)
    {
        if (value is FrameworkElement element)
        {
            if (isSmall) small.Add(element);
            return element;
        }
        return new Text { Text = value.ToString(), Tone = tone, Size = isSmall ? TextSize.Sm : default, TextWrapping = TextWrapping.Wrap };
    }

    /// <summary>按 em：上下 space.xs、状态与名称隔 space.sm、名称与说明隔 space.xxs；小一号的元素是 fontSizes.sm（固定的字号，同 TS 的 _sm）。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        double Space(string key) => this.Resource<double>(key) * em;
        if (Child is LayoutGrid grid)
        {
            grid.Margin = new Thickness(0, Space(ThemeKeys.Space.Xs), 0, Space(ThemeKeys.Space.Xs));
            body!.Margin = new Thickness(Space(ThemeKeys.Space.Sm), 0, Action is null ? 0 : Space(ThemeKeys.Space.Sm), 0);
            foreach (FrameworkElement part in body.Children.Cast<FrameworkElement>().Skip(1)) part.Margin = new Thickness(0, Space(ThemeKeys.Space.Xxs), 0, 0);
        }
        var sm = this.Resource<double>(ThemeKeys.FontSize.Sm);
        foreach (var element in small) TextElement.SetFontSize(element, sm);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    /// <summary>读屏：列表的一项。</summary>
    sealed class RowPeer(ChecklistRow owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(ChecklistRow);
    }
}

/// <summary>状态那一格：图标比文字大一档（1.14em），高度至少一行，图标竖着居中在第一行里。</summary>
sealed class StatusBox : Decorator
{
    public StatusBox() => VerticalAlignment = VerticalAlignment.Top;

    protected override Size MeasureOverride(Size constraint)
    {
        var em = TextElement.GetFontSize(this);
        if (Child is FrameworkElement child)
        {
            TextElement.SetFontSize(child, 1.14 * em);
            child.VerticalAlignment = VerticalAlignment.Center;
        }
        var size = base.MeasureOverride(constraint);
        // 至少一行高（TS 的 min-height: 1lh）
        return new Size(size.Width, Math.Max(size.Height, em * this.Resource<double>(ThemeKeys.LineHeight.Normal)));
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(arrangeSize));
        return arrangeSize;
    }
}

/// <summary>可以勾选时的状态：只有图标的复选按钮，可点的区域不小于 24 像素，悬停时圆圈后面一层 fillHover。</summary>
sealed class ChecklistToggle : ButtonBase
{
    public ChecklistToggle(ChecklistItem item, string label, string state)
    {
        (Item, Label, State) = (item, label, state);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Loaded += (_, _) =>
        {
            var target = this.Resource<double>(ThemeKeys.Size.MinTarget);
            MinWidth = MinHeight = target;
            CornerRadius = new CornerRadius(target / 2);
        };
    }

    ChecklistItem Item { get; }

    string Label { get; }

    string State { get; }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (IsEnabled) SetResourceReference(BackgroundProperty, ThemeKeys.Brush.FillHover);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        Background = Brushes.Transparent;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TogglePeer(this);

    /// <summary>读屏：复选框，名称是这一项的名称，帮助文字是状态。</summary>
    sealed class TogglePeer(ChecklistToggle owner) : FrameworkElementAutomationPeer(owner), IToggleProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;

        protected override string GetClassNameCore() => nameof(ChecklistToggle);

        protected override string GetNameCore() => owner.Label;

        protected override string GetHelpTextCore() => owner.State;

        protected override List<AutomationPeer>? GetChildrenCore() => null;

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Toggle ? this : base.GetPattern(patternInterface);

        public ToggleState ToggleState => owner.Item.Status == ChecklistStatus.Done ? ToggleState.On : ToggleState.Off;

        public void Toggle()
        {
            if (owner.IsEnabled) owner.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        }
    }
}
