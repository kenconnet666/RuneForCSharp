using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>
/// 下拉选择（vue-tsx-pro 的 Select，WAI-ARIA 的 select-only combobox）：点触发框弹出选项列表，单选或多选。
/// <code>
/// &lt;rune:Field Label="城市"&gt;&lt;rune:Select ItemsSource="{Binding Cities}" Value="{Binding City}" Placeholder="选择城市" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:Select ItemsSource="{Binding Tags}" SelectionMode="multiple" Values="{Binding Picked}" Clearable="True" MaxTagCount="responsive" /&gt;
/// &lt;rune:Select ItemsSource="{Binding Suppliers}" ToOption="{Binding ToOption}" Searchable="True" Value="{Binding Supplier}" /&gt;
/// </code>
/// 外壳同输入框（<see cref="Variant"/>、<see cref="Size"/>、<see cref="Radius"/>、出错、只读、不可用，前置 <see cref="Icon"/>），触发框就是控件自己（焦点一直在它上面）。
/// 弹出层贴着外壳下方（放不下时翻到上方）、至少与它同宽，里面是不可聚焦的 <see cref="Listbox"/>（<see cref="ItemTemplate"/> 自定义选项内容）。
/// 多选时已选的项是外壳里一排可移除的 <see cref="SelectedTags"/>（<see cref="MaxTagCount"/> 收起），读屏从下拉自己念出全部已选。
/// 键盘：关闭时 ↓ / Enter / 空格打开（↑ 打开并高亮最后一项），单选时按字母直接选中；打开时 ↑↓、Home / End、Page Up / Down 移动，Enter / 空格选中
/// （单选随后关闭），Esc、Tab 关闭；多选时 Backspace 移除最后一项。<see cref="Searchable"/> 时弹出层顶部有搜索框，打开后焦点在里面，输入就过滤
/// （按显示文字，忽略大小写与重音，或 <see cref="Filter"/>）；<see cref="Creatable"/> 时自带搜索框，输入的文字没有对应的选项时末尾多一项"新建"，
/// 选中后值就是这段文字，<see cref="OptionCreated"/> 报告它。单选的值是空字符串时算没有选中。部件名、部件样式键见 <see cref="SelectParts"/>。
/// </summary>
[TemplatePart(Name = SelectParts.Tags, Type = typeof(SelectedTags))]
[TemplatePart(Name = SelectParts.Value, Type = typeof(TextBlock))]
[TemplatePart(Name = SelectParts.Clear, Type = typeof(ButtonBase))]
public class Select : Control, IFieldControl
{
    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(Select),
        new FrameworkPropertyMetadata(null, (d, _) => ((Select)d).OnItemsChanged()));

    /// <summary>数据 → 选项。</summary>
    public static readonly DependencyProperty ToOptionProperty = DependencyProperty.Register(nameof(ToOption), typeof(Func<object?, ListboxOption>), typeof(Select),
        new FrameworkPropertyMetadata(null, (d, _) => ((Select)d).OnItemsChanged()));

    /// <summary>单选或多选，默认单选。</summary>
    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(nameof(SelectionMode), typeof(SelectionMode), typeof(Select),
        new FrameworkPropertyMetadata(SelectionMode.Single, OnSync));

    /// <summary>单选的值（双向绑定）；null 或空字符串是没有选中。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(Select),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSync));

    /// <summary>多选的值（双向绑定）。</summary>
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(nameof(Values), typeof(IReadOnlyList<string>), typeof(Select),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSync));

    /// <summary>开着（双向绑定）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Select),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Select)d).Toggle()));

    /// <summary>没有选择时显示的文字；不写时取语言包（请选择）。</summary>
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(Select),
        new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>有选择时显示清空按钮。</summary>
    public static readonly DependencyProperty ClearableProperty = DependencyProperty.Register(nameof(Clearable), typeof(bool), typeof(Select),
        new FrameworkPropertyMetadata(false, OnSync));

    /// <summary>弹出层顶部带搜索框。</summary>
    public static readonly DependencyProperty SearchableProperty = DependencyProperty.Register(nameof(Searchable), typeof(bool), typeof(Select),
        new FrameworkPropertyMetadata(false, (d, _) => ((Select)d).OnItemsChanged()));

    /// <summary>搜索时怎样算匹配（数据、搜索词）；不写时显示文字包含搜索词（忽略大小写与重音）。</summary>
    public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(nameof(Filter), typeof(Func<object?, string, bool>), typeof(Select),
        new FrameworkPropertyMetadata(null, (d, _) => ((Select)d).OnItemsChanged()));

    /// <summary>可以新建选项（自带搜索框）。</summary>
    public static readonly DependencyProperty CreatableProperty = DependencyProperty.Register(nameof(Creatable), typeof(bool), typeof(Select),
        new FrameworkPropertyMetadata(false, (d, _) => ((Select)d).OnItemsChanged()));

    /// <summary>多选时最多显示几个标签；不写时全部显示。</summary>
    public static readonly DependencyProperty MaxTagCountProperty = DependencyProperty.Register(nameof(MaxTagCount), typeof(TagLimit?), typeof(Select),
        new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>样式，默认 surface。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(ControlVariant), typeof(Select),
        new FrameworkPropertyMetadata(ControlVariant.Surface));

    /// <summary>字号档位；不写时跟着外面。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Select));

    /// <summary>圆角，默认 md。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(Radius), typeof(Select),
        new FrameworkPropertyMetadata(Radius.Md));

    /// <summary>外壳的圆角（按 <see cref="Radius"/> 与字号算出，主题样式写系数）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(Select),
        new FrameworkPropertyMetadata(default(CornerRadius), null, EmSize.CoerceCornerRadius));

    /// <summary>前置图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(Select));

    /// <summary>校验未通过；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(nameof(IsInvalid), typeof(bool?), typeof(Select),
        new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.Register(nameof(IsRequired), typeof(bool?), typeof(Select));

    /// <summary>只读：能聚焦、能看到已选的内容，不能打开、不能清空；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool?), typeof(Select),
        new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>正在取选项：列表换成骨架行。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(Select),
        new FrameworkPropertyMetadata(false, (d, e) => ((Select)d).listbox.IsLoading = (bool)e.NewValue));

    /// <summary>没有选项时列表里显示的内容。</summary>
    public static readonly DependencyProperty EmptyProperty = DependencyProperty.Register(nameof(Empty), typeof(object), typeof(Select),
        new FrameworkPropertyMetadata(null, (d, _) => ((Select)d).UpdateEmpty()));

    /// <summary>选项的内容模板（拿到原始数据）；触发框里仍显示文字。</summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(Select),
        new FrameworkPropertyMetadata(null, (d, e) => ((Select)d).listbox.ItemTemplate = (DataTemplate?)e.NewValue));

    static readonly DependencyPropertyKey ShowsInvalidKey = DependencyProperty.RegisterReadOnly(nameof(ShowsInvalid), typeof(bool), typeof(Select), new PropertyMetadata(false));

    /// <summary>显示成出错（自己的或字段的，给主题样式用）。</summary>
    public static readonly DependencyProperty ShowsInvalidProperty = ShowsInvalidKey.DependencyProperty;

    static readonly DependencyPropertyKey ShowsReadOnlyKey = DependencyProperty.RegisterReadOnly(nameof(ShowsReadOnly), typeof(bool), typeof(Select), new PropertyMetadata(false));

    /// <summary>显示成只读（自己的或字段的，给主题样式用）。</summary>
    public static readonly DependencyProperty ShowsReadOnlyProperty = ShowsReadOnlyKey.DependencyProperty;

    static readonly DependencyPropertyKey ShowsPlaceholderKey = DependencyProperty.RegisterReadOnly(nameof(ShowsPlaceholder), typeof(bool), typeof(Select), new PropertyMetadata(false));

    /// <summary>没有选择、显示着占位文字（给主题样式用）。</summary>
    public static readonly DependencyProperty ShowsPlaceholderProperty = ShowsPlaceholderKey.DependencyProperty;

    /// <summary>列表的单选值（绑定过来，变了即列表里选了）。</summary>
    static readonly DependencyProperty ListValueProperty = DependencyProperty.Register("ListValue", typeof(string), typeof(Select),
        new FrameworkPropertyMetadata(null, (d, _) => ((Select)d).OnListChanged()));

    /// <summary>列表的多选值。</summary>
    static readonly DependencyProperty ListValuesProperty = DependencyProperty.Register("ListValues", typeof(IReadOnlyList<string>), typeof(Select),
        new FrameworkPropertyMetadata(null, (d, _) => ((Select)d).OnListChanged()));

    /// <summary>与外壳的距离（像素）。</summary>
    const double Offset = 6;

    /// <summary>弹出层四周给阴影留的透明边（像素）。</summary>
    const double Overhang = 16;

    readonly Popup popup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Custom };
    readonly Border shadow = new();
    readonly Border panel = new() { ClipToBounds = true };
    readonly Border header = new() { Visibility = Visibility.Collapsed };
    readonly TextField search = new() { Icon = Lucide.Search, Variant = ControlVariant.Soft };
    readonly Listbox listbox = new() { Focusable = false };
    IReadOnlyList<object?> all = [];
    Dictionary<string, string>? labels;
    SelectedTags? tags;
    TextBlock? valueText;
    ButtonBase? clear;
    OpenChangeReason? pending;
    long closedAt = long.MinValue / 2;
    bool syncing;

    static Select()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Select), new FrameworkPropertyMetadata(typeof(Select)));
        EmSize.Register(typeof(Select), CornerRadiusProperty);
    }

    /// <summary>新建：弹出层（阴影、面板里的搜索框与列表）。</summary>
    public Select()
    {
        shadow.SetResourceReference(StyleProperty, SelectParts.PopoverShadow);
        panel.SetResourceReference(StyleProperty, SelectParts.Popover);
        header.SetResourceReference(StyleProperty, SelectParts.Header);
        header.Child = search;
        var body = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        body.Children.Add(header);
        body.Children.Add(listbox);
        panel.Child = body;
        var layer = new LayoutGrid { Margin = new Thickness(Overhang) };
        layer.Children.Add(shadow);
        layer.Children.Add(panel);
        popup.Child = layer;
        popup.PlacementTarget = this;
        popup.CustomPopupPlacementCallback = Place;
        AddLogicalChild(popup);
        popup.Opened += (_, _) => OnOpened();
        popup.Closed += (_, _) =>
        {
            closedAt = Environment.TickCount64;
            if (IsOpen) SetOpen(false, OpenChangeReason.Outside);
        };
        SetBinding(ListValueProperty, new System.Windows.Data.Binding(nameof(Listbox.Value)) { Source = listbox });
        SetBinding(ListValuesProperty, new System.Windows.Data.Binding(nameof(Listbox.Values)) { Source = listbox });
        IsEnabledChanged += (_, _) => Sync();
        listbox.Picked += (_, _) => Picked();
        search.TextChanged += (_, _) => OnSearchChanged();
        search.PreviewKeyDown += OnSearchKeyDown;
        Focusable = true;
        Loaded += (_, _) =>
        {
            UpdateMetrics();
            OnItemsChanged();
        };
    }

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => (Func<object?, ListboxOption>?)GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>单选或多选。</summary>
    public SelectionMode SelectionMode { get => (SelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>单选的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>多选的值。</summary>
    public IReadOnlyList<string>? Values { get => (IReadOnlyList<string>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>占位文字。</summary>
    public string? Placeholder { get => (string?)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>显示清空按钮。</summary>
    public bool Clearable { get => (bool)GetValue(ClearableProperty); set => SetValue(ClearableProperty, value); }

    /// <summary>带搜索框。</summary>
    public bool Searchable { get => (bool)GetValue(SearchableProperty); set => SetValue(SearchableProperty, value); }

    /// <summary>搜索时怎样算匹配。</summary>
    public Func<object?, string, bool>? Filter { get => (Func<object?, string, bool>?)GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>可以新建选项。</summary>
    public bool Creatable { get => (bool)GetValue(CreatableProperty); set => SetValue(CreatableProperty, value); }

    /// <summary>多选时最多显示几个标签。</summary>
    public TagLimit? MaxTagCount { get => (TagLimit?)GetValue(MaxTagCountProperty); set => SetValue(MaxTagCountProperty, value); }

    /// <summary>样式。</summary>
    public ControlVariant Variant { get => (ControlVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>外壳的圆角。</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => (bool?)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => (bool?)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => (bool?)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>正在取选项。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>没有选项时显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>选项的内容模板。</summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>显示成出错。</summary>
    public bool ShowsInvalid => (bool)GetValue(ShowsInvalidProperty);

    /// <summary>显示成只读。</summary>
    public bool ShowsReadOnly => (bool)GetValue(ShowsReadOnlyProperty);

    /// <summary>显示着占位文字。</summary>
    public bool ShowsPlaceholder => (bool)GetValue(ShowsPlaceholderProperty);

    /// <summary>用户开、关了（触发框、选中、点外面、Esc、Tab）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>新建了一项（<see cref="Creatable"/> 时）：参数是输入的文字，也就是选中的值。</summary>
    public event EventHandler<SelectCreatedEventArgs>? OptionCreated;

    /// <summary>弹出层里的列表（测试用）。</summary>
    internal Listbox List => listbox;

    /// <summary>弹出层顶部的搜索框（测试用）。</summary>
    internal TextField SearchBox => search;

    /// <summary>搜索框那一行（测试用）。</summary>
    internal Border Header => header;

    /// <summary>那一排已选标签（测试用）。</summary>
    internal SelectedTags? TagRow => tags;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { popup }.GetEnumerator();

    bool Multiple => SelectionMode == SelectionMode.Multiple;

    bool ReadOnly => IsReadOnly ?? Field.Of(this)?.IsReadOnly ?? false;

    /// <summary>不能打开、不能改：不可用或只读。</summary>
    bool Locked => !IsEnabled || ReadOnly;

    bool WithSearch => Searchable || Creatable;

    /// <summary>已选的值（单选的空字符串算没有选中）。</summary>
    IReadOnlyList<string> Selected => Multiple ? Values ?? [] : string.IsNullOrEmpty(Value) ? [] : [Value];

    void IFieldControl.FieldChanged() => Sync();

    static void OnSync(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Select)d).Sync();

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) UpdateMetrics();
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (clear is not null) clear.Click -= OnClear;
        if (tags is not null) tags.RemoveRequested -= OnTagRemoved;
        tags = GetTemplateChild(SelectParts.Tags) as SelectedTags;
        valueText = GetTemplateChild(SelectParts.Value) as TextBlock;
        clear = GetTemplateChild(SelectParts.Clear) as ButtonBase;
        if (clear is not null) clear.Click += OnClear;
        TextInputBase.FitInnerButton(clear, FontSize);
        Sync();
    }

    /// <summary>数据换了：加载以后才交给列表（初始化器里 ItemsSource 可能先于 ToOption 设，列表一拿到就建选项）。</summary>
    void OnItemsChanged()
    {
        all = ItemsSource?.Cast<object?>().ToList() ?? [];
        labels = null;
        if (!IsLoaded) return;
        header.Visibility = WithSearch ? Visibility.Visible : Visibility.Collapsed;
        UpdateItems();
        Sync();
    }

    /// <summary>选项名称：数据 → 选项（"新建"项是"新建 …"加一个加号）。</summary>
    ListboxOption OptionOf(object? item) => item is SelectCreation creation
        ? new ListboxOption(creation.Text, Provider.GetLang(this).SelectCreate(creation.Text)) { Icon = Lucide.Plus }
        : ListboxOption.From(item, ToOption);

    /// <summary>列表里的数据：按搜索词过滤，creatable 时可能多一项"新建"。</summary>
    void UpdateItems()
    {
        listbox.ToOption = OptionOf;
        listbox.ItemsSource = WithSearch
            ? SelectSearch.Shown(all, item => ListboxOption.From(item, ToOption), search.Text ?? "", localFilter: true, Filter, Creatable)
            : all;
        UpdateEmpty();
    }

    /// <summary>没有选项时：有搜索词是"没有匹配的选项"，否则是使用方的 Empty（不写时列表自己的）。</summary>
    void UpdateEmpty() => listbox.Empty = string.IsNullOrEmpty(search.Text) ? Empty : new Empty { Size = TextSize.Sm, Description = Provider.GetLang(this).SelectNoMatch };

    /// <summary>值 → 列表、触发框（文字或标签）、清空按钮、状态。</summary>
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
            tags.Visibility = tagged ? Visibility.Visible : Visibility.Collapsed;
            tags.MaxTagCount = MaxTagCount;
            tags.Tags = [.. selected.Select((value, index) => new SelectedTag(value, labels[index]))];
            tags.RemoveRequested -= OnTagRemoved;
            if (!Locked) tags.RemoveRequested += OnTagRemoved;
        }
        if (valueText is not null)
        {
            valueText.Visibility = tagged ? Visibility.Collapsed : Visibility.Visible;
            valueText.Text = labels.Count == 0 ? Placeholder ?? lang.SelectPlaceholder : labels[0];
        }
        SetValue(ShowsPlaceholderKey, labels.Count == 0);
        SetValue(ShowsInvalidKey, IsInvalid ?? Field.Of(this)?.Invalid ?? false);
        SetValue(ShowsReadOnlyKey, ReadOnly);
        if (clear is not null) clear.Visibility = Clearable && labels.Count > 0 && !Locked ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 已选项的文字取自全部数据（被过滤掉的也在）；数据里没有时（新建了还没加进来）就是值本身。值 → 文字的表按数据建一次：
    /// 已选项很多时逐个在数据里找是 O(n²)（上游的性能用例：三万个全选）。初始化器里 ToOption 可能还没设：认不出的数据先跳过。
    /// </summary>
    string LabelOf(string value)
    {
        if (labels is null)
        {
            labels = [];
            foreach (var item in all)
            {
                var option = ToOption?.Invoke(item) ?? item switch { ListboxOption known => known, string text => new ListboxOption(text, text), _ => null };
                if (option is not null) labels.TryAdd(option.Value, option.Label);
            }
        }
        return labels.TryGetValue(value, out var label) ? label : value;
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
        Focus();
    }

    void OnSearchChanged()
    {
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
        if (IsOpen) panel.MinWidth = ActualWidth;
        if (popup.IsOpen != IsOpen) popup.IsOpen = IsOpen;
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(IsOpen, known));
    }

    /// <summary>平台的位置：外壳下方、起始边对齐，放不下时翻到上方；弹出层四周有透明边（阴影），与外壳隔 6。尺寸是设备像素。</summary>
    CustomPopupPlacement[] Place(Size popupSize, Size targetSize, Point offset)
    {
        var scale = ActualWidth > 0 ? targetSize.Width / ActualWidth : 1;
        var margin = Overhang * scale;
        var gap = Offset * scale;
        var x = FlowDirection == FlowDirection.LeftToRight ? -margin : targetSize.Width - popupSize.Width + margin;
        return
        [
            new(new Point(x, targetSize.Height + gap - margin), PopupPrimaryAxis.Horizontal),
            new(new Point(x, -gap - popupSize.Height + margin), PopupPrimaryAxis.Horizontal),
        ];
    }

    /// <summary>打开后：可搜索时焦点进搜索框（关上后回到触发框）。</summary>
    void OnOpened()
    {
        if (!WithSearch) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => search.Focus());
    }

    /// <summary>按 em 的尺寸：面板 lg 圆角；搜索框那一行 space.xs 内边距；弹出层不在外壳里，字号同外壳。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        panel.CornerRadius = shadow.CornerRadius = new CornerRadius((TryFindResource("Rune.Radius.Lg") as double? ?? 0.5) * em);
        header.Padding = new Thickness((TryFindResource("Rune.Space.Xs") as double? ?? 0.25) * em);
        TextElement.SetFontSize(panel, em);
        TextInputBase.FitInnerButton(clear, em);
    }

    void OnClear(object sender, RoutedEventArgs e)
    {
        if (Multiple) SetCurrentValue(ValuesProperty, Array.Empty<string>());
        else SetCurrentValue(ValueProperty, null);
        Focus();
    }

    void OnTagRemoved(object? sender, SelectedTagEventArgs e) => Remove(e.Value);

    void Remove(string value) => SetCurrentValue(ValuesProperty, Selected.Where(item => item != value).ToList());

    void RemoveLast()
    {
        if (Selected is [.., var last]) Remove(last);
    }

    /// <summary>事件来自弹出层（沿逻辑树冒泡上来的：列表的点击、搜索框的输入）。</summary>
    bool InPopup(object? source) => source is DependencyObject element && (element == panel || panel.IsAncestorOf(element));

    /// <summary>
    /// 点外壳（文字、标签、图标、箭头）：聚焦并切换打开；框里的按钮（清空、标签的移除）自己处理。只读时只聚焦。
    /// 开着时点外壳：平台先按"点了外面"关掉，紧接着这次按下又会打开——刚关掉的那一下不再打开。
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.Handled || !IsEnabled || InPopup(e.OriginalSource) || e.OriginalSource is DependencyObject source && FindButton(source) is not null) return;
        Focus();
        e.Handled = true;
        if (ReadOnly) return;
        if (IsOpen) SetOpen(false, OpenChangeReason.Trigger);
        else if (Environment.TickCount64 - closedAt >= 300) Show();
    }

    ButtonBase? FindButton(DependencyObject source)
    {
        for (var node = source; node is not null && node != this; node = VisualTreeHelper.GetParent(node))
            if (node is ButtonBase button) return button;
        return null;
    }

    /// <summary>触发框上的键盘（见类的说明）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Locked || InPopup(e.OriginalSource)) return;
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
    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || Locked || InPopup(e.OriginalSource) || e.Text is not { Length: 1 } text || !listbox.State.Typeahead(text)) return;
        e.Handled = true;
        if (!IsOpen && !Multiple && listbox.State.Active is { } active && !listbox.State.IsSelected(active)) listbox.State.Select(active);
    }

    /// <summary>搜索框里：↑↓、Page Up / Down 移动，Enter 选中（Home / End、空格留给输入）；Esc 关闭、焦点回到触发框；Tab 关闭；多选时空的搜索框里 Backspace 移除最后一项。</summary>
    void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Tab:
                SetOpen(false, OpenChangeReason.Focus);
                Focus();
                e.Handled = true;
                return;
            case Key.Escape:
                SetOpen(false, OpenChangeReason.Escape);
                Focus();
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
    sealed class SelectPeer(Select owner) : FrameworkElementAutomationPeer(owner), IExpandCollapseProvider, IValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

        protected override string GetClassNameCore() => nameof(Select);

        protected override string GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText ?? "" : "";

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface is PatternInterface.ExpandCollapse or PatternInterface.Value ? this : base.GetPattern(patternInterface);

        public ExpandCollapseState ExpandCollapseState => owner.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public void Expand()
        {
            if (!owner.IsOpen && !owner.Locked) owner.Show();
        }

        public void Collapse()
        {
            if (owner.IsOpen) owner.SetOpen(false, OpenChangeReason.Trigger);
        }

        public bool IsReadOnly => true;

        public string Value => Provider.GetLang(owner).SelectSummary([.. owner.Selected.Select(owner.LabelOf)]);

        public void SetValue(string value) => throw new InvalidOperationException("下拉选择的值从选项里选。");
    }
}

/// <summary>新建了一项的事件参数。</summary>
/// <param name="text">输入的文字，也就是选中的值。</param>
public sealed class SelectCreatedEventArgs(string text) : EventArgs
{
    /// <summary>输入的文字。</summary>
    public string Text { get; } = text;
}

/// <summary>Select 的部件：模板里的按名字找（<see cref="Tags"/>、<see cref="Value"/>、<see cref="Clear"/>），弹出层的是部件样式键（DESIGN §7）。</summary>
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
    public static readonly PartKey Popover = new(typeof(Select), nameof(Popover), typeof(Border));

    /// <summary>面板后面画阴影的一层（DropShadowEffect 不放在面板上：面板里的文字不走软件渲染）。</summary>
    public static readonly PartKey PopoverShadow = new(typeof(Select), nameof(PopoverShadow), typeof(Border));

    /// <summary>弹出层顶部放搜索框的那一行（下边一条分隔线）。</summary>
    public static readonly PartKey Header = new(typeof(Select), nameof(Header), typeof(Border));
}
