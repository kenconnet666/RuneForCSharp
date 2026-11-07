using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 列表框（vue-tsx-pro 的 Listbox）：在一列选项里单选或多选。数据驱动、虚拟滚动（上万个选项也只建看得见的几十行）。
/// <code>
/// &lt;rune:Listbox ItemsSource="{Binding Cities}" Value="{Binding City}" AutomationProperties.Name="城市" /&gt;
/// &lt;rune:Listbox ItemsSource="{Binding Columns}" SelectionMode="multiple" Values="{Binding Visible}" Height="256" /&gt;
/// </code>
/// 选项是 <see cref="ListboxOption"/>（值、文字、不可用、说明、图标、分组）或字符串；别的数据给 <see cref="ToOption"/>，<see cref="ItemTemplate"/> 自定义选项内容时拿到的仍是原始数据。
/// 焦点在列表上：↑↓、Home / End、Page Up / Down 移动高亮，Enter / 空格选中，按字母跳到以它开头的项（状态与键盘见 <see cref="ListState"/>）；
/// 指针悬停高亮、点击选中。分组的组名是一行小标题。没有选项时显示 <see cref="Empty"/>（默认语言包的"没有选项"），<see cref="IsLoading"/> 时换成几行骨架。
/// 自己滚动时最高 20em（写了 Height 时按它）。放进 Field 时以标签为名称，沿用必填（单选时再点已选项不取消）、不可用。
/// Select 用同一个列表当弹出层里的视图（不可聚焦，按键从触发按钮转过来）。部件名在 <see cref="ListboxParts"/>。
/// </summary>
public class Listbox : Control, IFieldControl
{
    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<Listbox, IEnumerable?>(nameof(ItemsSource));

    /// <summary>数据 → 选项；数据是 ListboxOption、字符串时不用写。</summary>
    public static readonly StyledProperty<Func<object?, ListboxOption>?> ToOptionProperty = AvaloniaProperty.Register<Listbox, Func<object?, ListboxOption>?>(nameof(ToOption));

    /// <summary>选择方式，默认 single。</summary>
    public static readonly StyledProperty<SelectionMode> SelectionModeProperty = AvaloniaProperty.Register<Listbox, SelectionMode>(nameof(SelectionMode));

    /// <summary>单选：选中项的值（默认双向绑定）；没有选中时为空。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<Listbox, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>多选：选中项的值（默认双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ValuesProperty =
        AvaloniaProperty.Register<Listbox, IReadOnlyList<string>?>(nameof(Values), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>必须有选中项：单选时再点已选项不取消；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsRequiredProperty = AvaloniaProperty.Register<Listbox, bool?>(nameof(IsRequired));

    /// <summary>加载中：选项换成骨架行（很快完成时不闪）。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Listbox, bool>(nameof(IsLoading));

    /// <summary>自定义选项内容：数据模板拿到 ItemsSource 里的原始数据。</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<Listbox, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>没有选项时显示的内容；不写时是 Empty（文案取语言包）。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<Listbox, object?>(nameof(Empty));

    /// <summary>点选了一项（点击、Enter、空格；再选已选的那项也会发）。</summary>
    public static readonly RoutedEvent<ListboxPickedEventArgs> PickedEvent = RoutedEvent.Register<Listbox, ListboxPickedEventArgs>(nameof(Picked), RoutingStrategies.Bubble);

    readonly ScrollArea scroll = new() { Name = ListboxParts.Scroll, IsViewportFocusable = false };
    readonly ItemsControl rows = new() { Name = ListboxParts.Rows };
    readonly StackPanel skeleton = new() { Name = ListboxParts.Skeleton, IsVisible = false };
    readonly ContentPresenter empty = new() { Name = ListboxParts.Empty, IsVisible = false };
    readonly DelayedLoading loading;
    bool syncing;

    /// <summary>新建：列表状态、虚拟滚动的行、骨架与空状态。</summary>
    public Listbox()
    {
        State = new ListState(OnSelectionChanged);
        State.ActiveChanged += OnActiveChanged;
        loading = new DelayedLoading(shown =>
        {
            skeleton.IsVisible = shown;
            UpdateBody();
        });
        rows.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
        rows.DataTemplates.Add(new FuncDataTemplate<ListboxGroupRow>((_, _) => new ListboxGroupLabel(), supportsRecycling: true));
        rows.DataTemplates.Add(new FuncDataTemplate<ListboxOptionRow>((_, _) => new ListboxItem(this), supportsRecycling: true));
        for (var index = 0; index < 4; index++)
            skeleton.Children.Add(new Skeleton { Delay = index * 100, Width = double.NaN, HorizontalAlignment = HorizontalAlignment.Left, Tag = 0.85 - index * 0.15 });
        var body = new Panel { Children = { rows, skeleton, empty } };
        scroll.Content = body;
        LogicalChildren.Add(scroll);
        VisualChildren.Add(scroll);
        Focusable = true;
        GotFocus += (_, _) =>
        {
            if (State.Active is null) State.ActivateDefault();
        };
    }

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>选择方式。</summary>
    public SelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>单选的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>多选的值。</summary>
    public IReadOnlyList<string>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>必选。</summary>
    public bool? IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>加载中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>选项的数据模板。</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>空状态。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>点选了一项。</summary>
    public event EventHandler<ListboxPickedEventArgs>? Picked
    {
        add => AddHandler(PickedEvent, value);
        remove => RemoveHandler(PickedEvent, value);
    }

    /// <summary>列表的状态（选项、高亮、已选、键盘）：Select 把按键转给它。</summary>
    public ListState State { get; }

    /// <summary>已经建出来的选项行（测试用）。</summary>
    internal IEnumerable<ListboxItem> RealizedItems => rows.GetRealizedContainers().Select(container => (container as ContentPresenter)?.Child).OfType<ListboxItem>();

    bool Multiple => SelectionMode == SelectionMode.Multiple;

    bool Required => IsRequired ?? Field.Of(this)?.IsRequired ?? false;

    void IFieldControl.FieldChanged()
    {
        State.Required = Required;
        IsEnabled = Field.Of(this)?.IsEnabled ?? IsEnabled;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // 进了逻辑树以后才建选项：初始化器、XAML 里 ItemsSource 可能先于 ToOption 设
        if (change.Property == ItemsSourceProperty || change.Property == ToOptionProperty)
        {
            if (((ILogical)this).IsAttachedToLogicalTree) Rebuild();
        }
        else if (change.Property == ValueProperty || change.Property == ValuesProperty || change.Property == SelectionModeProperty) SyncSelection();
        else if (change.Property == IsRequiredProperty || change.Property == Field.OwnerProperty) State.Required = Required;
        else if (change.Property == IsLoadingProperty) loading.Update(IsLoading);
        else if (change.Property == EmptyProperty) UpdateBody();
        else if (change.Property == ItemTemplateProperty) Refresh();
        else if (change.Property == TextElement.FontSizeProperty || change.Property == HeightProperty) UpdateMetrics();
    }

    /// <summary>数据 → 选项（按分组排好）→ 行：每组前插一行组名。</summary>
    void Rebuild()
    {
        State.SetItems(ItemsSource?.Cast<object?>() ?? [], ToOption);
        var list = new List<object>();
        string? group = null;
        for (var index = 0; index < State.Options.Count; index++)
        {
            var entry = State.Options[index];
            if (entry.Option.Group is { } name && name != group) list.Add(new ListboxGroupRow(name));
            group = entry.Option.Group;
            list.Add(new ListboxOptionRow(entry, index));
        }
        rows.ItemsSource = list;
        SyncSelection();
        UpdateBody();
    }

    /// <summary>值 → 列表状态的已选项。</summary>
    void SyncSelection()
    {
        if (syncing) return;
        State.Mode = SelectionMode;
        State.Required = Required;
        State.Selected = Multiple ? Values ?? [] : Value is { } value ? [value] : [];
        Refresh();
    }

    /// <summary>列表状态的选择 → 值（写回绑定）。</summary>
    void OnSelectionChanged(IReadOnlyList<string> next)
    {
        syncing = true;
        if (Multiple) SetCurrentValue(ValuesProperty, next);
        else SetCurrentValue(ValueProperty, next.FirstOrDefault());
        syncing = false;
        Refresh();
    }

    /// <summary>高亮项变了：重画，滚进可见范围（高亮第一项、上面是组名时滚到最顶上，组名不被挡住）。</summary>
    void OnActiveChanged()
    {
        Refresh();
        if (State.Active is not { } value || rows.ItemsSource is not IList list) return;
        var index = list.IndexOf(list.OfType<ListboxOptionRow>().FirstOrDefault(row => row.Entry.Option.Value == value));
        if (index < 0) return;
        rows.ScrollIntoView(index == 1 && list[0] is ListboxGroupRow ? 0 : index);
    }

    /// <summary>已经建出来的行按状态重画。</summary>
    void Refresh()
    {
        foreach (var item in RealizedItems) item.Sync();
    }

    /// <summary>行、骨架、空状态三选一。</summary>
    void UpdateBody()
    {
        var hasRows = State.Options.Count > 0;
        rows.IsVisible = !skeleton.IsVisible && hasRows;
        empty.IsVisible = !skeleton.IsVisible && !hasRows && !IsLoading;
        empty.Content = Empty ?? new Empty { Size = TextSize.Sm, Description = Provider.GetLang(this).SelectEmpty };
    }

    /// <summary>按 em 的尺寸：内边距 space.xs，自己滚动时最高 20em，骨架行高 1em、上下 0.625em、宽度依次递减。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        var xs = this.Resource("Rune.Space.Xs", 0.25) * em;
        var sm = this.Resource("Rune.Space.Sm", 0.5) * em;
        ((Panel)scroll.Content!).Margin = new Thickness(xs);
        scroll.MaxHeight = double.IsNaN(Height) ? 20 * em : double.PositiveInfinity;
        foreach (var bar in skeleton.Children.OfType<Skeleton>())
        {
            bar.Height = em;
            bar.Margin = new Thickness(sm, 0.625 * em);
        }
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        // 骨架行的宽度按列表宽度的比例
        foreach (var bar in skeleton.Children.OfType<Skeleton>()) bar.Width = Math.Max(0, (finalSize.Width - 4 * TextElement.GetFontSize(this)) * (double)bar.Tag!);
        return size;
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateMetrics();
        UpdateBody();
    }

    /// <summary>点选了一项：切换选中、发 Picked。</summary>
    internal void Pick(string value)
    {
        if (!IsEffectivelyEnabled) return;
        State.SetActive(value);
        State.Select(value);
        RaiseEvent(new ListboxPickedEventArgs(PickedEvent, value));
    }

    /// <summary>键盘：↑↓、Home / End、Page Up / Down、Enter / 空格（同 TS 的 handleListKeydown）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEffectivelyEnabled || e.KeyModifiers is not KeyModifiers.None and not KeyModifiers.Shift) return;
        if (ToListKey(e.Key) is { } key) e.Handled = State.Handle(key, picked: value => RaiseEvent(new ListboxPickedEventArgs(PickedEvent, value)));
    }

    /// <summary>字母：类型搜索。</summary>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!e.Handled && IsEffectivelyEnabled && e.Text is { Length: 1 } text) e.Handled = State.Typeahead(text);
    }

    /// <summary>平台的按键 → 列表的按键；不是列表认的键时为 null。</summary>
    /// <param name="key">按键。</param>
    public static ListKey? ToListKey(Key key) => key switch
    {
        Key.Down => ListKey.Down,
        Key.Up => ListKey.Up,
        Key.Home => ListKey.Home,
        Key.End => ListKey.End,
        Key.PageDown => ListKey.PageDown,
        Key.PageUp => ListKey.PageUp,
        Key.Enter => ListKey.Enter,
        Key.Space => ListKey.Space,
        _ => null,
    };

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListboxPeer(this);

    /// <summary>读屏：列表（能多选、必选）；没写名称时取所在字段的标签。</summary>
    sealed class ListboxPeer(Listbox owner) : ControlAutomationPeer(owner), ISelectionProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => nameof(Listbox);

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null;

        public bool CanSelectMultiple => owner.Multiple;

        public bool IsSelectionRequired => owner.Required;

        public IReadOnlyList<AutomationPeer> GetSelection() =>
            [.. owner.RealizedItems.Where(item => item.IsSelected).Select(item => (AutomationPeer)GetOrCreate(item))];
    }
}

/// <summary>点选了一项的事件参数。</summary>
/// <param name="routedEvent">事件。</param>
/// <param name="value">选项的值。</param>
public sealed class ListboxPickedEventArgs(RoutedEvent routedEvent, string value) : RoutedEventArgs(routedEvent)
{
    /// <summary>选项的值。</summary>
    public string Value { get; } = value;
}

/// <summary>渲染用的一行：分组标题。</summary>
/// <param name="Label">组名。</param>
sealed record ListboxGroupRow(string Label);

/// <summary>分组标题：只是视觉上的一行（读屏从选项的说明里听到组名）。样式同 TextBlock（<c>TextBlock#PART_GroupLabel</c>）。</summary>
sealed class ListboxGroupLabel : TextBlock
{
    public ListboxGroupLabel()
    {
        Name = ListboxParts.GroupLabel;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Text = (DataContext as ListboxGroupRow)?.Label;
    }
}

/// <summary>渲染用的一行：选项。</summary>
/// <param name="Entry">选项与原始数据。</param>
/// <param name="Index">在全部选项里的位置。</param>
sealed record ListboxOptionRow(ListStateEntry Entry, int Index);

/// <summary>
/// 列表里的一个选项（<see cref="Listbox"/> 建的，不单独用）：图标、文字（下面一行说明）、已选标记。不可聚焦，焦点留在列表、触发按钮或搜索框上。
/// 伪类：<c>:active</c>（高亮）、<c>:selected</c>；不可用的选项 IsEnabled 为 false（<c>:disabled</c>）。
/// </summary>
[PseudoClasses(":active", ":selected")]
public class ListboxItem : Control
{
    /// <summary>行的底色（主题里高亮时 fillHover）。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<ListboxItem>();

    /// <summary>文字颜色（继承；主题里已选时 accentText）。</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ListboxItem>();

    readonly Listbox owner;
    readonly Border root = new() { Name = ListboxParts.Option };
    readonly Flex layout = new() { Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Icon icon = new();
    readonly StackPanel content = new();
    readonly TextBlock label = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    readonly Text description = new() { Size = TextSize.Sm, Tone = TextTone.Muted, TextWrapping = TextWrapping.Wrap };
    readonly ContentPresenter custom = new();
    readonly Icon check = new() { Name = ListboxParts.Check, Data = Lucide.Check };

    static ListboxItem() => AffectsRender<ListboxItem>(BackgroundProperty);

    /// <summary>新建一行。</summary>
    /// <param name="owner">所在的列表。</param>
    internal ListboxItem(Listbox owner)
    {
        this.owner = owner;
        content.Children.AddRange([label, description, custom]);
        Flex.SetGrow(content, true);
        layout.Children.AddRange([icon, content, check]);
        root.Child = layout;
        root.Bind(Border.BackgroundProperty, this.GetObservable(BackgroundProperty));
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>底色。</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>文字颜色。</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>这一行的选项。</summary>
    public ListboxOption? Option => Row?.Entry.Option;

    /// <summary>已选。</summary>
    public bool IsSelected => Option is { } option && owner.State.IsSelected(option.Value);

    /// <summary>高亮。</summary>
    public bool IsActive => Option is { } option && owner.State.Active == option.Value;

    ListboxOptionRow? Row => DataContext as ListboxOptionRow;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Sync();
    }

    /// <summary>按选项与状态更新：内容、伪类、读屏名称；尺寸按 em（控件高度、上下 space.xs、左右 space.sm、sm 圆角）。</summary>
    internal void Sync()
    {
        if (Row is not { } row) return;
        var option = row.Entry.Option;
        var template = owner.ItemTemplate;
        icon.Data = option.Icon;
        icon.IsVisible = template is null && option.Icon is not null;
        label.Text = option.Label;
        label.IsVisible = template is null;
        description.Text = option.Description;
        description.IsVisible = template is null && option.Description is not null;
        custom.IsVisible = template is not null;
        custom.ContentTemplate = template;
        custom.Content = template is null ? null : row.Entry.Item;
        check.Opacity = IsSelected ? 1 : 0;
        PseudoClasses.Set(":active", IsActive);
        PseudoClasses.Set(":selected", IsSelected);
        // 不可用：平台的 :disabled 伪类、不跟指针、读屏的不可用都随 IsEnabled
        IsEnabled = !option.IsDisabled;
        AutomationProperties.SetName(this, option.Label);
        AutomationProperties.SetHelpText(this, option.Group);
        var em = TextElement.GetFontSize(this);
        // 行高 lineHeights.normal：TextBlock.LineHeight 不往下继承，直接设在文字上
        label.LineHeight = this.Resource("Rune.LineHeight.Normal", 1.5) * em;
        root.Padding = new Thickness(this.Resource("Rune.Space.Sm", 0.5) * em, this.Resource("Rune.Space.Xs", 0.25) * em);
        root.CornerRadius = new CornerRadius(this.Resource("Rune.Radius.Sm", 0.25) * em);
        root.MinHeight = ControlHeight.Of(em, this.Resource("Rune.Size.Control", 2.25));
    }

    /// <summary>指针移过：高亮（不可用的不高亮）。</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Option is { IsDisabled: false } option && owner.State.Active != option.Value) owner.State.SetActive(option.Value);
    }

    /// <summary>按下不抢焦点（焦点留在列表、触发按钮或搜索框上）。</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
    }

    /// <summary>松开在这一行上：选中。</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (Option is { IsDisabled: false } option && e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this))) owner.Pick(option.Value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListboxItemPeer(this);

    /// <summary>读屏：列表项，选中状态与"选中"操作。</summary>
    sealed class ListboxItemPeer(ListboxItem owner) : ControlAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsSelected => owner.IsSelected;

        public ISelectionProvider? SelectionContainer => GetOrCreate(owner.owner) as ISelectionProvider;

        public void Select()
        {
            if (owner.Option is { } option && !IsSelected) owner.owner.Pick(option.Value);
        }

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
            if (owner.Option is { } option && IsSelected) owner.owner.Pick(option.Value);
        }
    }
}

/// <summary>列表框的部件名：样式里用 <c>rune|Listbox rune|ListboxItem</c>、<c>rune|Listbox TextBlock#PART_GroupLabel</c> 选中。</summary>
public static class ListboxParts
{
    /// <summary>滚动区域（自己滚动时）。</summary>
    public const string Scroll = "PART_Scroll";

    /// <summary>虚拟滚动的行。</summary>
    public const string Rows = "PART_Rows";

    /// <summary>分组标题（TS 的 propsGroupLabel）。</summary>
    public const string GroupLabel = "PART_GroupLabel";

    /// <summary>选项的外框（TS 的 propsOption：底色、圆角、内边距）。</summary>
    public const string Option = "PART_Option";

    /// <summary>已选标记（TS 的 propsCheck）。</summary>
    public const string Check = "PART_Check";

    /// <summary>空状态（TS 的 propsEmpty）。</summary>
    public const string Empty = "PART_Empty";

    /// <summary>加载中的骨架行（TS 的 propsSkeleton）。</summary>
    public const string Skeleton = "PART_Skeleton";
}
