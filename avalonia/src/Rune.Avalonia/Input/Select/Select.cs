using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 下拉选择（vue-tsx-pro 的 Select，WAI-ARIA 的 select-only combobox）：点触发框弹出选项列表，单选或多选。
/// <code>
/// &lt;rune:Field Label="城市"&gt;&lt;rune:Select ItemsSource="{Binding Cities}" Value="{Binding City}" Placeholder="选择城市" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:Select ItemsSource="{Binding Tags}" SelectionMode="multiple" Values="{Binding Picked}" Clearable="True" MaxTagCount="responsive" /&gt;
/// &lt;rune:Select ItemsSource="{Binding Suppliers}" ToOption="{Binding ToOption}" Searchable="True" Value="{Binding Supplier}" /&gt;
/// </code>
/// 外壳同输入框（<see cref="Variant"/>、<see cref="Size"/>、<see cref="Radius"/>、出错、只读、不可用，前置 <see cref="Icon"/>），触发框就是控件自己（焦点一直在它上面）。
/// 弹出层贴着外壳下方、至少与它同宽，里面是不可聚焦的 <see cref="Listbox"/>（状态与键盘见 <see cref="ListState"/>，<see cref="ItemTemplate"/> 自定义选项内容）。
/// 多选时已选的项是外壳里一排可移除的 <see cref="SelectedTags"/>（<see cref="MaxTagCount"/> 收起），读屏从下拉自己念出全部已选。
/// 键盘：关闭时 ↓ / Enter / 空格打开（↑ 打开并高亮最后一项），单选时按字母直接选中；打开时 ↑↓、Home / End、Page Up / Down 移动，Enter / 空格选中
/// （单选随后关闭），Esc、Tab 关闭；多选时 Backspace 移除最后一项。<see cref="Searchable"/> 时弹出层顶部有搜索框，打开后焦点在里面，输入就过滤
/// （按显示文字，忽略大小写与重音，或 <see cref="Filter"/>）；<see cref="Creatable"/> 时自带搜索框，输入的文字没有对应的选项时末尾多一项"新建"，
/// 选中后值就是这段文字，<see cref="OptionCreated"/> 报告它。单选的值是空字符串时算没有选中。部件名见 <see cref="SelectParts"/>。
/// 远程选项：给了 <see cref="Load"/> 时打开就按搜索词请求，输入停下 300 毫秒再请求，只采用最后一次，结果代替 ItemsSource（ItemsSource 是第一次结果之前的选项，
/// 编辑页放上已选的那一项）；搜索交给服务端，不在本地过滤。返回的一页带 Next 时分页：列表滚到离末尾不到 5 项取下一页，骨架接在后面。
/// 加载失败时列表里提示"加载失败，请重试"。已选项的文字记着见过的选项：搜索换掉了结果，触发框照样显示文字。
/// </summary>
public class Select : TemplatedControl, IFieldControl
{
    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<Select, IEnumerable?>(nameof(ItemsSource));

    /// <summary>数据 → 选项。</summary>
    public static readonly StyledProperty<Func<object?, ListboxOption>?> ToOptionProperty = AvaloniaProperty.Register<Select, Func<object?, ListboxOption>?>(nameof(ToOption));

    /// <summary>单选或多选，默认单选。</summary>
    public static readonly StyledProperty<SelectionMode> SelectionModeProperty = AvaloniaProperty.Register<Select, SelectionMode>(nameof(SelectionMode));

    /// <summary>单选的值（双向绑定）；null 或空字符串是没有选中。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<Select, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>多选的值（双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ValuesProperty =
        AvaloniaProperty.Register<Select, IReadOnlyList<string>?>(nameof(Values), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>开着（双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<Select, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>没有选择时显示的文字；不写时取语言包（请选择）。</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty = AvaloniaProperty.Register<Select, string?>(nameof(Placeholder));

    /// <summary>有选择时显示清空按钮。</summary>
    public static readonly StyledProperty<bool> ClearableProperty = AvaloniaProperty.Register<Select, bool>(nameof(Clearable));

    /// <summary>弹出层顶部带搜索框。</summary>
    public static readonly StyledProperty<bool> SearchableProperty = AvaloniaProperty.Register<Select, bool>(nameof(Searchable));

    /// <summary>搜索时怎样算匹配（数据、搜索词）；不写时显示文字包含搜索词（忽略大小写与重音）。</summary>
    public static readonly StyledProperty<Func<object?, string, bool>?> FilterProperty = AvaloniaProperty.Register<Select, Func<object?, string, bool>?>(nameof(Filter));

    /// <summary>可以新建选项（自带搜索框）。</summary>
    public static readonly StyledProperty<bool> CreatableProperty = AvaloniaProperty.Register<Select, bool>(nameof(Creatable));

    /// <summary>多选时最多显示几个标签；不写时全部显示。</summary>
    public static readonly StyledProperty<TagLimit?> MaxTagCountProperty = AvaloniaProperty.Register<Select, TagLimit?>(nameof(MaxTagCount));

    /// <summary>样式，默认 surface。</summary>
    public static readonly StyledProperty<ControlVariant> VariantProperty = AvaloniaProperty.Register<Select, ControlVariant>(nameof(Variant));

    /// <summary>字号档位；不写时跟着外面。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Select, TextSize>(nameof(Size));

    /// <summary>圆角，默认 md。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<Select, Radius>(nameof(Radius));

    /// <summary>前置图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<Select, IconData?>(nameof(Icon));

    /// <summary>校验未通过；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = AvaloniaProperty.Register<Select, bool?>(nameof(IsInvalid));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsRequiredProperty = AvaloniaProperty.Register<Select, bool?>(nameof(IsRequired));

    /// <summary>只读：能聚焦、能看到已选的内容，不能打开、不能清空；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsReadOnlyProperty = AvaloniaProperty.Register<Select, bool?>(nameof(IsReadOnly));

    /// <summary>正在取选项：列表换成骨架行。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Select, bool>(nameof(IsLoading));

    /// <summary>没有选项时列表里显示的内容。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<Select, object?>(nameof(Empty));

    /// <summary>选项的内容模板（拿到原始数据）；触发框里仍显示文字。</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<Select, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>远程选项的加载函数（TS 的 load）：搜索词、第几页 → 一页选项。</summary>
    public static readonly StyledProperty<OptionsLoader?> LoadProperty = AvaloniaProperty.Register<Select, OptionsLoader?>(nameof(Load));

    /// <summary>与外壳的距离（像素）。</summary>
    const double Offset = 6;

    readonly TemplateResources resources;
    readonly Popup popup = new() { IsLightDismissEnabled = true, WindowManagerAddShadowHint = false, Placement = PlacementMode.BottomEdgeAlignedLeft };
    readonly Border panel = new() { Name = SelectParts.Popover, ClipToBounds = true };
    readonly Border header = new() { Name = SelectParts.Header, IsVisible = false };
    readonly TextField search = new() { Name = SelectParts.Search, Icon = Lucide.Search, Variant = ControlVariant.Soft };
    readonly Listbox listbox = new() { Name = SelectParts.Listbox, Focusable = false };
    readonly RemoteOptions remote;
    readonly Dictionary<string, string> seen = [];
    IReadOnlyList<object?> all = [];
    Dictionary<string, string>? labels;
    SelectedTags? tags;
    TextBlock? valueText;
    ButtonBase? clear;
    OpenChangeReason? pending;
    bool syncing;

    /// <summary>新建：外壳的字号、圆角按档位解析成主题资源；弹出层（搜索框、列表）。</summary>
    public Select()
    {
        resources = new TemplateResources(this);
        remote = new RemoteOptions(item => ListboxOption.From(item, ToOption).Value, OnRemoteChanged);
        listbox.Reached += last => remote.Reached(last);
        Resolved.Bind(this, Resolved.RadiusProperty, Radius.Md.ResourceKey);
        header.Child = search;
        var body = new DockPanel { Children = { header, listbox } };
        DockPanel.SetDock(header, Dock.Top);
        panel.Child = body;
        popup.Child = new Panel { Margin = new Thickness(0, Offset), Children = { panel } };
        popup.PlacementTarget = this;
        LogicalChildren.Add(popup);
        popup.Closed += (_, _) =>
        {
            if (IsOpen) SetOpen(false, OpenChangeReason.Outside);
        };
        popup.Opened += (_, _) => OnOpened();
        listbox.PropertyChanged += (_, e) =>
        {
            if (e.Property == Listbox.ValueProperty || e.Property == Listbox.ValuesProperty) OnListChanged();
        };
        listbox.Picked += (_, _) => Picked();
        search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) OnSearchChanged();
        };
        search.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        Focusable = true;
    }

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

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

    /// <summary>搜索时怎样算匹配。</summary>
    public Func<object?, string, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>可以新建选项。</summary>
    public bool Creatable { get => GetValue(CreatableProperty); set => SetValue(CreatableProperty, value); }

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

    /// <summary>正在取选项。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>没有选项时显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>选项的内容模板。</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>远程选项的加载函数。</summary>
    public OptionsLoader? Load { get => GetValue(LoadProperty); set => SetValue(LoadProperty, value); }

    /// <summary>用户开、关了（触发框、选中、点外面、Esc、Tab）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>新建了一项（<see cref="Creatable"/> 时）：参数是输入的文字，也就是选中的值。</summary>
    public event EventHandler<SelectCreatedEventArgs>? OptionCreated;

    /// <summary>弹出层里的列表（测试用）。</summary>
    internal Listbox List => listbox;

    /// <summary>弹出层顶部的搜索框（测试用）。</summary>
    internal TextField SearchBox => search;

    /// <summary>那一排已选标签（测试用）。</summary>
    internal SelectedTags? TagRow => tags;

    bool Multiple => SelectionMode == SelectionMode.Multiple;

    bool Invalid => IsInvalid ?? Field.Of(this)?.Invalid ?? false;

    bool Required => IsRequired ?? Field.Of(this)?.IsRequired ?? false;

    bool ReadOnly => IsReadOnly ?? Field.Of(this)?.IsReadOnly ?? false;

    /// <summary>不能打开、不能改：不可用或只读。</summary>
    bool Locked => !IsEffectivelyEnabled || ReadOnly;

    bool WithSearch => Searchable || Creatable;

    /// <summary>已选的值（单选的空字符串算没有选中）。</summary>
    IReadOnlyList<string> Selected => Multiple ? Values ?? [] : string.IsNullOrEmpty(Value) ? [] : [Value];

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
        else if (change.Property == ItemsSourceProperty || change.Property == ToOptionProperty || change.Property == FilterProperty || change.Property == CreatableProperty)
        {
            all = ItemsSource?.Cast<object?>().ToList() ?? [];
            labels = null;
            header.IsVisible = WithSearch;
            UpdateItems();
            Sync();
        }
        else if (change.Property == ValueProperty || change.Property == ValuesProperty || change.Property == SelectionModeProperty
                 || change.Property == PlaceholderProperty || change.Property == ClearableProperty || change.Property == MaxTagCountProperty
                 || change.Property == IsInvalidProperty || change.Property == IsReadOnlyProperty || change.Property == IsRequiredProperty
                 || change.Property == Field.OwnerProperty || change.Property == IsEnabledProperty)
            Sync();
        else if (change.Property == SearchableProperty) header.IsVisible = WithSearch;
        else if (change.Property == IsOpenProperty) Toggle();
        else if (change.Property == IsLoadingProperty) listbox.IsLoading = IsLoading || remote.Loading;
        else if (change.Property == LoadProperty)
        {
            remote.Load = Load;
            OnRemoteChanged();
        }
        else if (change.Property == EmptyProperty) UpdateEmpty();
        else if (change.Property == ItemTemplateProperty) listbox.ItemTemplate = ItemTemplate;
        else if (change.Property == TextElement.FontSizeProperty) UpdateMetrics();
    }

    /// <summary>选项名称：数据 → 选项（"新建"项是"新建 …"加一个加号）。</summary>
    ListboxOption OptionOf(object? item) => item is SelectCreation creation
        ? new ListboxOption(creation.Text, Provider.GetLang(this).SelectCreate(creation.Text)) { Icon = Lucide.Plus }
        : ListboxOption.From(item, ToOption);

    /// <summary>当前的数据：远程请求到的，没有时是 ItemsSource。</summary>
    IReadOnlyList<object?> Source => remote.ItemsOr(all);

    /// <summary>列表里的数据：按搜索词过滤（远程选项交给服务端），creatable 时可能多一项"新建"。</summary>
    void UpdateItems()
    {
        listbox.ToOption = OptionOf;
        listbox.ItemsSource = WithSearch
            ? SelectSearch.Shown(Source, item => ListboxOption.From(item, ToOption), search.Text ?? "", localFilter: Load is null, Filter, Creatable)
            : Source;
        UpdateEmpty();
    }

    /// <summary>远程选项变了（请求到了、开始或停止加载、失败了）：列表、加载状态、已选项的文字跟着变。</summary>
    void OnRemoteChanged()
    {
        labels = null;
        listbox.IsLoading = IsLoading || remote.Loading;
        listbox.IsLoadingMore = remote.LoadingMore;
        UpdateItems();
        Sync();
    }

    /// <summary>没有选项时：加载失败是"加载失败，请重试"；有搜索词是"没有匹配的选项"；否则是使用方的 Empty（不写时列表自己的）。</summary>
    void UpdateEmpty()
    {
        var lang = Provider.GetLang(this);
        listbox.Empty = remote.Failed ? new Empty { Size = TextSize.Sm, Description = lang.ListLoadFailed }
            : string.IsNullOrEmpty(search.Text) ? Empty
            : new Empty { Size = TextSize.Sm, Description = lang.SelectNoMatch };
    }

    /// <summary>值 → 列表、触发框（文字或标签）、清空按钮、状态伪类。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        var selected = Selected;
        syncing = true;
        listbox.SelectionMode = SelectionMode;
        listbox.IsRequired = !Multiple;
        if (Multiple) listbox.Values = selected;
        else listbox.Value = selected.FirstOrDefault();
        syncing = false;

        var labels = selected.Select(LabelOf).ToList();
        var tagged = Multiple && labels.Count > 0;
        if (tags is not null)
        {
            tags.IsVisible = tagged;
            tags.MaxTagCount = MaxTagCount;
            tags.Tags = [.. selected.Select((value, index) => new SelectedTag(value, labels[index]))];
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

    /// <summary>
    /// 已选项的文字取自全部数据（被过滤掉的也在）；数据里没有时取见过的（远程搜索换掉了结果），再没有时（新建了还没加进来）就是值本身。
    /// 值 → 文字的表按数据建一次：已选项很多时逐个在数据里找是 O(n²)（上游的性能用例：三万个全选）。初始化器里 ToOption 可能还没设：认不出的数据先跳过。
    /// </summary>
    string LabelOf(string value)
    {
        if (labels is null)
        {
            labels = [];
            foreach (var item in Source)
            {
                var option = ToOption?.Invoke(item) ?? item switch { ListboxOption known => known, string text => new ListboxOption(text, text), _ => null };
                if (option is not null) labels.TryAdd(option.Value, option.Label);
            }
        }
        if (labels.TryGetValue(value, out var label)) return seen[value] = label;
        return seen.TryGetValue(value, out var remembered) ? remembered : value;
    }

    /// <summary>列表的选择 → 值；选中了"新建"项时报告新建，清空搜索词。</summary>
    void OnListChanged()
    {
        if (syncing) return;
        var next = Multiple ? listbox.Values ?? [] : listbox.Value is { } value ? [value] : [];
        var created = listbox.ItemsSource?.OfType<SelectCreation>().FirstOrDefault(creation => next.Contains(creation.Text));
        if (Multiple) SetCurrentValue(ValuesProperty, next);
        else SetCurrentValue(ValueProperty, next.FirstOrDefault());
        if (created is null) return;
        OptionCreated?.Invoke(this, new SelectCreatedEventArgs(created.Text));
        search.Text = "";
    }

    /// <summary>选中了一项：单选时关闭（焦点回到触发框）。</summary>
    void Picked()
    {
        if (Multiple) return;
        SetOpen(false, OpenChangeReason.Select);
        Focus(NavigationMethod.Tab);
    }

    void OnSearchChanged()
    {
        remote.SetQuery(search.Text ?? "");
        UpdateItems();
        // 输入搜索词：高亮第一个匹配的（打开时清空搜索词不算：那时高亮的是已选项）
        if (!string.IsNullOrEmpty(search.Text)) listbox.State.MoveToEnd(last: false);
    }

    /// <summary>打开：清空搜索词，高亮已选的第一项（<paramref name="last"/> 且没有已选时高亮最后一项）。</summary>
    void Show(bool last = false)
    {
        search.Text = "";
        UpdateItems();
        listbox.State.ActivateDefault();
        if (last && Selected.Count == 0) listbox.State.MoveToEnd(last: true);
        SetOpen(true, OpenChangeReason.Trigger);
    }

    void SetOpen(bool open, OpenChangeReason reason)
    {
        pending = reason;
        SetCurrentValue(IsOpenProperty, open);
        pending = null;
    }

    void Toggle()
    {
        var reason = pending;
        PseudoClasses.Set(":open", IsOpen);
        remote.SetOpen(IsOpen);
        if (IsOpen) panel.MinWidth = Bounds.Width;
        if (popup.IsOpen != IsOpen) popup.IsOpen = IsOpen;
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(IsOpen, known));
    }

    /// <summary>打开后：可搜索时焦点进搜索框（关上后回到触发框）。</summary>
    void OnOpened()
    {
        if (!WithSearch) return;
        Dispatcher.UIThread.Post(() => search.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }

    /// <summary>按 em 的尺寸：面板 lg 圆角；搜索框那一行 space.xs 内边距、下边 borderMuted 分隔线。</summary>
    void UpdateMetrics()
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
        header.IsVisible = WithSearch;
    }

    void OnClear(object? sender, RoutedEventArgs e)
    {
        if (Multiple) SetCurrentValue(ValuesProperty, Array.Empty<string>());
        else SetCurrentValue(ValueProperty, null);
        Focus(NavigationMethod.Tab);
    }

    void OnTagRemoved(object? sender, SelectedTagEventArgs e) => Remove(e.Value);

    void Remove(string value) => SetCurrentValue(ValuesProperty, Selected.Where(item => item != value).ToList());

    void RemoveLast()
    {
        if (Selected is [.., var last]) Remove(last);
    }

    /// <summary>事件来自弹出层（沿宿主冒泡上来的：列表的点击、搜索框的输入）。</summary>
    bool InPopup(object? source) => source is Visual visual && (visual == panel || panel.IsVisualAncestorOf(visual));

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

    /// <summary>触发框上的键盘（见类的说明）。</summary>
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
            if (e.Key is Key.Down or Key.Enter or Key.Space) Show();
            else if (e.Key == Key.Up) Show(last: true);
            else return;
            e.Handled = true;
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
        if (Listbox.ToListKey(e.Key) is { } key) e.Handled = listbox.State.Handle(key, space: true, picked: _ => Picked());
    }

    /// <summary>字母：打开时在列表里类型搜索；关闭时单选直接选中以它开头的项（同平台的下拉框）。</summary>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || Locked || InPopup(e.Source) || e.Text is not { Length: 1 } text || !listbox.State.Typeahead(text)) return;
        e.Handled = true;
        if (!IsOpen && !Multiple && listbox.State.Active is { } active && !listbox.State.IsSelected(active)) listbox.State.Select(active);
    }

    /// <summary>搜索框里：↑↓、Page Up / Down 移动，Enter 选中（Home / End、空格留给输入）；Esc 关闭、焦点回到触发框；Tab 关闭；多选时空的搜索框里 Backspace 移除最后一项。</summary>
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
            case Key.Back when Multiple && string.IsNullOrEmpty(search.Text):
                RemoveLast();
                e.Handled = true;
                return;
            case Key.Home or Key.End:
                return;
        }
        if (Listbox.ToListKey(e.Key) is { } key) e.Handled = listbox.State.Handle(key, space: false, picked: _ => Picked());
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectPeer(this);

    /// <summary>读屏：组合框（展开、收起），值是全部已选的文字；没写名称时取所在字段的标签。</summary>
    sealed class SelectPeer(Select owner) : ControlAutomationPeer(owner), IExpandCollapseProvider, IValueProvider
    {
        protected override string? GetHelpTextCore() => Field.DescribedBy(owner, base.GetHelpTextCore());

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

        protected override string GetClassNameCore() => nameof(Select);

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

        public string? Value => Provider.GetLang(owner).SelectSummary([.. owner.Selected.Select(owner.LabelOf)]);

        public void SetValue(string? value) => throw new InvalidOperationException("下拉选择的值从选项里选。");
    }
}

/// <summary>新建了一项的事件参数。</summary>
/// <param name="text">输入的文字，也就是选中的值。</param>
public sealed class SelectCreatedEventArgs(string text) : EventArgs
{
    /// <summary>输入的文字。</summary>
    public string Text { get; } = text;
}

/// <summary>Select 的部件名：模板里的用 <c>rune|Select /template/ rune|SelectedTags#PART_Tags</c>，弹出层里的用 <c>rune|Select Border#PART_Popover</c>。</summary>
public static class SelectParts
{
    /// <summary>多选时那排标签（TS 的 propsTags）。</summary>
    public const string Tags = "PART_Tags";

    /// <summary>已选内容（单选的文字、占位文字，TS 的 propsValue）。</summary>
    public const string Value = "PART_Value";

    /// <summary>清空按钮（TS 的 propsClear）。</summary>
    public const string Clear = "PART_Clear";

    /// <summary>下拉箭头（TS 的 propsChevron）。</summary>
    public const string Chevron = "PART_Chevron";

    /// <summary>前置图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>弹出层面板（TS 的 propsPopover）。</summary>
    public const string Popover = "PART_Popover";

    /// <summary>弹出层顶部放搜索框的那一行。</summary>
    public const string Header = "PART_Header";

    /// <summary>搜索框（TS 的 propsSearch）。</summary>
    public const string Search = "PART_Search";

    /// <summary>列表（TS 的 propsListbox）。</summary>
    public const string Listbox = "PART_Listbox";
}
