using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 下拉选择的共同部分（vue-tsx-pro 的 Select、TreeSelect 共用的 controlParts、selectStyles、SelectedTags 与锚定的弹出层）：
/// 外壳同输入框（<see cref="Variant"/>、<see cref="Size"/>、<see cref="Radius"/>、出错、只读、不可用，前置 <see cref="Icon"/>），触发框就是控件自己；
/// 单选的值 <see cref="Value"/>、多选的值 <see cref="Values"/>，多选时已选的是一排可移除的 <see cref="SelectedTags"/>（<see cref="MaxTagCount"/> 收起）；
/// 清空按钮、下拉箭头；弹出层贴着外壳下方、至少与它同宽，<see cref="Searchable"/> 时顶部是搜索框，下面是子类给的内容（列表或树）。
/// 开关：点外壳、↓ / Enter / 空格打开，Esc、Tab、点外面关闭；关上时焦点还在弹出层里就回到触发框。多选时 Backspace 移除最后一个标签。
/// </summary>
public abstract class SelectBase : TemplatedControl, IFieldControl
{
    /// <summary>单选或多选，默认单选。</summary>
    public static readonly StyledProperty<SelectionMode> SelectionModeProperty = AvaloniaProperty.Register<SelectBase, SelectionMode>(nameof(SelectionMode));

    /// <summary>单选的值（双向绑定）；null 或空字符串是没有选中。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<SelectBase, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>多选的值（双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ValuesProperty =
        AvaloniaProperty.Register<SelectBase, IReadOnlyList<string>?>(nameof(Values), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>开着（双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<SelectBase, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>没有选择时显示的文字；不写时取语言包（请选择）。</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty = AvaloniaProperty.Register<SelectBase, string?>(nameof(Placeholder));

    /// <summary>有选择时显示清空按钮。</summary>
    public static readonly StyledProperty<bool> ClearableProperty = AvaloniaProperty.Register<SelectBase, bool>(nameof(Clearable));

    /// <summary>弹出层顶部带搜索框。</summary>
    public static readonly StyledProperty<bool> SearchableProperty = AvaloniaProperty.Register<SelectBase, bool>(nameof(Searchable));

    /// <summary>多选时最多显示几个标签；不写时全部显示。</summary>
    public static readonly StyledProperty<TagLimit?> MaxTagCountProperty = AvaloniaProperty.Register<SelectBase, TagLimit?>(nameof(MaxTagCount));

    /// <summary>样式，默认 surface。</summary>
    public static readonly StyledProperty<ControlVariant> VariantProperty = AvaloniaProperty.Register<SelectBase, ControlVariant>(nameof(Variant));

    /// <summary>字号档位；不写时跟着外面。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<SelectBase, TextSize>(nameof(Size));

    /// <summary>圆角，默认 md。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<SelectBase, Radius>(nameof(Radius));

    /// <summary>前置图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<SelectBase, IconData?>(nameof(Icon));

    /// <summary>校验未通过；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = AvaloniaProperty.Register<SelectBase, bool?>(nameof(IsInvalid));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsRequiredProperty = AvaloniaProperty.Register<SelectBase, bool?>(nameof(IsRequired));

    /// <summary>只读：能聚焦、能看到已选的内容，不能打开、不能清空；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsReadOnlyProperty = AvaloniaProperty.Register<SelectBase, bool?>(nameof(IsReadOnly));

    /// <summary>正在取数据：弹出层的内容换成骨架行。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<SelectBase, bool>(nameof(IsLoading));

    /// <summary>没有数据时弹出层里显示的内容。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<SelectBase, object?>(nameof(Empty));

    /// <summary><see cref="OpenChanged"/> 的路由事件（直接：不往上传，嵌套的浮层发的同名事件不会串到外层）。</summary>
    public static readonly RoutedEvent<OpenChangedEventArgs> OpenChangedEvent = RoutedEvent.Register<SelectBase, OpenChangedEventArgs>(nameof(OpenChanged), RoutingStrategies.Direct);

    /// <summary>与外壳的距离（像素）。</summary>
    const double Offset = 6;

    readonly TemplateResources resources;
    readonly Popup popup = new() { IsLightDismissEnabled = true, WindowManagerAddShadowHint = false, Placement = PlacementMode.BottomEdgeAlignedLeft };
    readonly Border panel = new() { Name = SelectParts.Popover, ClipToBounds = true };
    readonly Border header = new() { Name = SelectParts.Header, IsVisible = false };
    readonly DockPanel body = new();
    SelectedTags? tags;
    TextBlock? valueText;
    ButtonBase? clear;
    OpenChangeReason? pending;

    // 放进 ControlGroup 时相邻一侧不要圆角
    static SelectBase() => Attached.SquareCornersOf<SelectBase>();

    /// <summary>新建：外壳的字号、圆角按档位解析成主题资源；弹出层（搜索框，内容由子类用 <see cref="SetBody"/> 放进来）。</summary>
    protected SelectBase()
    {
        resources = new TemplateResources(this);
        Resolved.Bind(this, Resolved.RadiusProperty, Radius.Md.ResourceKey);
        header.Child = Search;
        DockPanel.SetDock(header, Dock.Top);
        body.Children.Add(header);
        panel.Child = body;
        popup.Child = new Panel { Margin = new Thickness(0, Offset), Children = { panel } };
        popup.PlacementTarget = this;
        LogicalChildren.Add(popup);
        popup.Closed += (_, _) =>
        {
            if (IsOpen) SetOpen(false, OpenChangeReason.Outside);
        };
        popup.Opened += (_, _) => Dispatcher.UIThread.Post(OnOpened, DispatcherPriority.Loaded);
        Search.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        // 焦点在弹出层的内容里（树）时：Esc、Tab 关上，焦点回到触发框
        panel.KeyDown += (_, e) =>
        {
            if (e.Handled || e.Key is not (Key.Escape or Key.Tab)) return;
            SetOpen(false, e.Key == Key.Escape ? OpenChangeReason.Escape : OpenChangeReason.Focus);
            Focus(NavigationMethod.Tab);
            e.Handled = true;
        };
        Focusable = true;
    }

    /// <summary>单选或多选。</summary>
    public SelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>单选的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>多选的值。</summary>
    public IReadOnlyList<string>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>占位文字。</summary>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>显示清空按钮。</summary>
    public bool Clearable { get => GetValue(ClearableProperty); set => SetValue(ClearableProperty, value); }

    /// <summary>带搜索框。</summary>
    public bool Searchable { get => GetValue(SearchableProperty); set => SetValue(SearchableProperty, value); }

    /// <summary>多选时最多显示几个标签。</summary>
    public TagLimit? MaxTagCount { get => GetValue(MaxTagCountProperty); set => SetValue(MaxTagCountProperty, value); }

    /// <summary>样式。</summary>
    public ControlVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>正在取数据。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>没有数据时显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>用户开、关了（触发框、选中、点外面、Esc、Tab）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary>弹出层顶部的搜索框（测试用）。</summary>
    internal SearchField SearchBox => Search;

    /// <summary>那一排已选标签（测试用）。</summary>
    internal SelectedTags? TagRow => tags;

    /// <summary>触发框里的文字（模板的 PART_Value）。</summary>
    private protected TextBlock? ValueText => valueText;

    /// <summary>弹出层顶部的搜索框。</summary>
    private protected SearchField Search { get; } = new() { Name = SelectParts.Search, Variant = ControlVariant.Soft };

    /// <summary>多选。</summary>
    protected bool Multiple => SelectionMode == SelectionMode.Multiple;

    /// <summary>校验未通过（自己的或所在 Field 的）。</summary>
    protected bool Invalid => IsInvalid ?? Field.Of(this)?.Invalid ?? false;

    /// <summary>必填（自己的或所在 Field 的）。</summary>
    protected bool Required => IsRequired ?? Field.Of(this)?.Required ?? false;

    /// <summary>只读（自己的或所在 Field 的）。</summary>
    protected bool ReadOnly => IsReadOnly ?? Field.ReadOnlyOf(this);

    /// <summary>不能打开、不能改：不可用或只读。</summary>
    protected bool Locked => !IsEffectivelyEnabled || ReadOnly;

    /// <summary>弹出层带搜索框。</summary>
    protected virtual bool WithSearch => Searchable;

    /// <summary>已选的值（单选的空字符串算没有选中）。</summary>
    protected IReadOnlyList<string> Selected => Multiple ? Values ?? [] : string.IsNullOrEmpty(Value) ? [] : [Value];

    /// <summary>触发框里列出的值（标签、文字）：默认就是已选的值。</summary>
    protected virtual IReadOnlyList<string> Shown => Selected;

    /// <summary>值的显示文字。</summary>
    protected abstract string LabelOf(string value);

    /// <summary>打开（<paramref name="last"/>：从末尾开始，↑ 打开时）。</summary>
    protected abstract void Show(bool last = false);

    /// <summary>移除一个标签（标签的移除按钮、Backspace）：默认从多选的值里去掉它。</summary>
    protected virtual void Remove(string value) => SetCurrentValue(ValuesProperty, Selected.Where(item => item != value).ToList());

    /// <summary>弹出层打开后：可搜索时焦点进搜索框。</summary>
    protected virtual void OnOpened()
    {
        if (WithSearch) Search.Focus(NavigationMethod.Tab);
    }

    /// <summary>弹出层最窄多宽（与外壳同宽之外的下限）。</summary>
    protected virtual double MinPanelWidth => 0;

    /// <summary>放进弹出层：在搜索框下面，占满剩下的高度。</summary>
    protected void SetBody(Control content) => body.Children.Add(content);

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (clear is not null) clear.Click -= OnClear;
        if (tags is not null) tags.RemoveRequested -= OnTagRemoved;
        tags = e.NameScope.Find<SelectedTags>(SelectParts.Tags);
        valueText = e.NameScope.Find<TextBlock>(SelectParts.Value);
        clear = e.NameScope.Find<ButtonBase>(SelectParts.Clear);
        if (clear is not null) clear.Click += OnClear;
        Sync();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            ControlHeight.Apply(this, Size);
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == RadiusProperty) Resolved.Bind(this, Resolved.RadiusProperty, (Radius.Value is null ? Radius.Md : Radius).ResourceKey);
        else if (change.Property == VariantProperty) PseudoClasses.Set(":soft", Variant == ControlVariant.Soft);
        else if (change.Property == ValueProperty || change.Property == ValuesProperty || change.Property == SelectionModeProperty
                 || change.Property == PlaceholderProperty || change.Property == ClearableProperty || change.Property == MaxTagCountProperty
                 || change.Property == IsInvalidProperty || change.Property == IsReadOnlyProperty || change.Property == IsRequiredProperty
                 || change.Property == Field.OwnerProperty || change.Property == IsEnabledProperty)
            Sync();
        else if (change.Property == SearchableProperty) UpdateHeader();
        else if (change.Property == IsOpenProperty) Toggle();
        else if (change.Property == TextElement.FontSizeProperty) UpdateMetrics();
    }

    /// <summary>值 → 触发框（文字或标签）、清空按钮、状态伪类；子类先同步自己的内容再调它。</summary>
    protected virtual void Sync()
    {
        var lang = Provider.GetLang(this);
        var shown = Shown;
        var labels = shown.Select(LabelOf).ToList();
        var tagged = Multiple && labels.Count > 0;
        if (tags is not null)
        {
            tags.IsVisible = tagged;
            tags.MaxTagCount = MaxTagCount;
            tags.Tags = [.. shown.Select((value, index) => new SelectedTag(value, labels[index]))];
            tags.RemoveRequested -= OnTagRemoved;
            if (!Locked) tags.RemoveRequested += OnTagRemoved;
        }
        if (valueText is not null)
        {
            valueText.IsVisible = !tagged;
            valueText.Text = labels.Count == 0 ? Placeholder ?? lang.SelectPlaceholder : labels[0];
        }
        PseudoClasses.Set(":placeholder", labels.Count == 0);
        PseudoClasses.Set(":invalid", Invalid);
        PseudoClasses.Set(":readonly", ReadOnly);
        if (clear is not null) clear.IsVisible = Clearable && labels.Count > 0 && !Locked;
    }

    /// <summary>用户开、关：记下原因，<see cref="OpenChanged"/> 带上它。</summary>
    protected void SetOpen(bool open, OpenChangeReason reason)
    {
        pending = reason;
        SetCurrentValue(IsOpenProperty, open);
        pending = null;
    }

    /// <summary>开着的变了：弹出层跟着开关；关上时焦点还在弹出层里就回到触发框。</summary>
    protected virtual void Toggle()
    {
        var reason = pending;
        PseudoClasses.Set(":open", IsOpen);
        if (IsOpen) panel.MinWidth = Math.Max(Bounds.Width, MinPanelWidth);
        else if (panel.IsKeyboardFocusWithin) Focus(NavigationMethod.Tab);
        if (popup.IsOpen != IsOpen) popup.IsOpen = IsOpen;
        if (reason is { } known) RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, IsOpen, known));
    }

    /// <summary>按 em 的尺寸：面板 lg 圆角；搜索框那一行 space.xs 内边距、下边 borderMuted 分隔线。</summary>
    protected virtual void UpdateMetrics()
    {
        var em = FontSize;
        panel.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Lg) * em);
        header.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * em);
        // 弹出层不在外壳里：字号同外壳
        panel.SetValue(TextElement.FontSizeProperty, em);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateMetrics();
        UpdateHeader();
    }

    /// <summary>搜索框那一行跟着 <see cref="WithSearch"/> 显示。</summary>
    protected void UpdateHeader() => header.IsVisible = WithSearch;

    void OnClear(object? sender, RoutedEventArgs e)
    {
        ClearSelection();
        Focus(NavigationMethod.Tab);
    }

    /// <summary>清空按钮：单选写回 null，多选写回空列表。</summary>
    protected virtual void ClearSelection()
    {
        if (Multiple) SetCurrentValue(ValuesProperty, Array.Empty<string>());
        else SetCurrentValue(ValueProperty, null);
    }

    void OnTagRemoved(object? sender, SelectedTagEventArgs e) => Remove(e.Value);

    void RemoveLast()
    {
        if (Shown is [.., var last]) Remove(last);
    }

    /// <summary>事件来自弹出层（沿宿主冒泡上来的：列表的点击、搜索框的输入）。</summary>
    protected bool InPopup(object? source) => source is Visual visual && (visual == panel || panel.IsVisualAncestorOf(visual));

    /// <summary>点外壳（文字、标签、图标、箭头）：聚焦并切换打开；框里的按钮（清空、标签的移除）自己处理。只读时只聚焦。</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !IsEffectivelyEnabled || InPopup(e.Source) || e.Source is Visual source && source.FindAncestorOfType<ButtonBase>(true) is not null) return;
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
        if (ReadOnly) return;
        if (IsOpen) SetOpen(false, OpenChangeReason.Trigger);
        else Show();
    }

    /// <summary>触发框上的键盘：多选时 Backspace 移除最后一个标签；关着时 ↓ / Enter / 空格打开；开着时 Tab、Esc 关闭，其余交给 <see cref="OnOpenKeyDown"/>。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Locked || InPopup(e.Source)) return;
        if (e.Key == Key.Back && Multiple)
        {
            RemoveLast();
            e.Handled = true;
            return;
        }
        if (!IsOpen)
        {
            e.Handled = OnClosedKeyDown(e.Key);
            return;
        }
        if (e.Key == Key.Tab)
        {
            SetOpen(false, OpenChangeReason.Focus);
            return;
        }
        if (e.Key == Key.Escape)
        {
            SetOpen(false, OpenChangeReason.Escape);
            e.Handled = true;
            return;
        }
        OnOpenKeyDown(e);
    }

    /// <summary>关着时的按键：↓ / Enter / 空格打开；返回是否处理了。</summary>
    protected virtual bool OnClosedKeyDown(Key key)
    {
        if (key is not (Key.Down or Key.Enter or Key.Space)) return false;
        Show();
        return true;
    }

    /// <summary>开着时触发框上的其余按键（焦点留在触发框上的列表用）。</summary>
    protected virtual void OnOpenKeyDown(KeyEventArgs e)
    {
    }

    /// <summary>搜索框里：Esc 关闭、焦点回到触发框；Tab 关闭；多选时空的搜索框里 Backspace 移除最后一个标签；其余交给 <see cref="OnSearchKey"/>。</summary>
    void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Tab:
                SetOpen(false, OpenChangeReason.Focus);
                Focus(NavigationMethod.Tab);
                e.Handled = true;
                return;
            case Key.Escape:
                SetOpen(false, OpenChangeReason.Escape);
                Focus(NavigationMethod.Tab);
                e.Handled = true;
                return;
            case Key.Back when Multiple && string.IsNullOrEmpty(Search.Text):
                RemoveLast();
                e.Handled = true;
                return;
        }
        OnSearchKey(e);
    }

    /// <summary>搜索框里的其余按键（移动到列表或树里）。</summary>
    protected virtual void OnSearchKey(KeyEventArgs e)
    {
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectPeer(this);

    /// <summary>读屏：组合框（展开、收起），值是全部已选的文字；没写名称时取所在字段的标签。</summary>
    sealed class SelectPeer(SelectBase owner) : ControlAutomationPeer(owner), IExpandCollapseProvider, IValueProvider
    {
        protected override string? GetHelpTextCore() => Field.DescribedBy(owner, base.GetHelpTextCore());

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

        protected override string GetClassNameCore() => owner.GetType().Name;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null;

        public ExpandCollapseState ExpandCollapseState => owner.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!owner.IsOpen && !owner.Locked) owner.Show();
        }

        public void Collapse()
        {
            if (owner.IsOpen) owner.SetOpen(false, OpenChangeReason.Trigger);
        }

        public bool IsReadOnly => true;

        public string? Value => Provider.GetLang(owner).SelectSummary([.. owner.Shown.Select(owner.LabelOf)]);

        public void SetValue(string? value) => throw new InvalidOperationException("下拉选择的值从选项里选。");
    }
}
