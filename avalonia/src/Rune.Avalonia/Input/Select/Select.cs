using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Rune.Avalonia;

/// <summary>
/// 下拉选择（vue-tsx-pro 的 Select，WAI-ARIA 的 select-only combobox）：点触发框弹出选项列表，单选或多选。
/// <code>
/// &lt;rune:Field Label="城市"&gt;&lt;rune:Select ItemsSource="{Binding Cities}" Value="{Binding City}" Placeholder="选择城市" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:Select ItemsSource="{Binding Tags}" SelectionMode="multiple" Values="{Binding Picked}" Clearable="True" MaxTagCount="responsive" /&gt;
/// &lt;rune:Select ItemsSource="{Binding Suppliers}" ToOption="{Binding ToOption}" Searchable="True" Value="{Binding Supplier}" /&gt;
/// </code>
/// 外壳、标签、清空、弹出层与开关见 <see cref="SelectBase"/>；弹出层里是不可聚焦的 <see cref="Listbox"/>（状态与键盘见 <see cref="ListState"/>，
/// <see cref="ItemTemplate"/> 自定义选项内容），焦点一直在触发框上。读屏从下拉自己念出全部已选。
/// 键盘：关闭时 ↓ / Enter / 空格打开（↑ 打开并高亮最后一项），单选时按字母直接选中；打开时 ↑↓、Home / End、Page Up / Down 移动，Enter / 空格选中
/// （单选随后关闭），Esc、Tab 关闭；多选时 Backspace 移除最后一项。<see cref="SelectBase.Searchable"/> 时弹出层顶部有搜索框，打开后焦点在里面，输入就过滤
/// （按显示文字，忽略大小写与重音，或 <see cref="Filter"/>）；<see cref="Creatable"/> 时自带搜索框，输入的文字没有对应的选项时末尾多一项"新建"，
/// 选中后值就是这段文字，<see cref="OptionCreated"/> 报告它。单选的值是空字符串时算没有选中。部件名见 <see cref="SelectParts"/>。
/// 远程选项：给了 <see cref="Load"/> 时打开就按搜索词请求，输入停下 300 毫秒再请求，只采用最后一次，结果代替 ItemsSource（ItemsSource 是第一次结果之前的选项，
/// 编辑页放上已选的那一项）；搜索交给服务端，不在本地过滤。返回的一页带 Next 时分页：列表滚到离末尾不到 5 项取下一页，骨架接在后面。
/// 加载失败时列表里提示"加载失败，请重试"。已选项的文字记着见过的选项：搜索换掉了结果，触发框照样显示文字。
/// </summary>
public class Select : SelectBase
{
    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<Select, IEnumerable?>(nameof(ItemsSource));

    /// <summary>数据 → 选项。</summary>
    public static readonly StyledProperty<Func<object?, ListboxOption>?> ToOptionProperty = AvaloniaProperty.Register<Select, Func<object?, ListboxOption>?>(nameof(ToOption));

    /// <summary>搜索时怎样算匹配（数据、搜索词）；不写时显示文字包含搜索词（忽略大小写与重音）。</summary>
    public static readonly StyledProperty<Func<object?, string, bool>?> FilterProperty = AvaloniaProperty.Register<Select, Func<object?, string, bool>?>(nameof(Filter));

    /// <summary>可以新建选项（自带搜索框）。</summary>
    public static readonly StyledProperty<bool> CreatableProperty = AvaloniaProperty.Register<Select, bool>(nameof(Creatable));

    /// <summary>选项的内容模板（拿到原始数据）；触发框里仍显示文字。</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<Select, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>远程选项的加载函数（TS 的 load）：搜索词、第几页 → 一页选项。</summary>
    public static readonly StyledProperty<OptionsLoader?> LoadProperty = AvaloniaProperty.Register<Select, OptionsLoader?>(nameof(Load));

    /// <summary><see cref="OptionCreated"/> 的路由事件（直接：不往上传，嵌套的浮层发的同名事件不会串到外层）。</summary>
    public static readonly RoutedEvent<SelectCreatedEventArgs> OptionCreatedEvent = RoutedEvent.Register<Select, SelectCreatedEventArgs>(nameof(OptionCreated), RoutingStrategies.Direct);

    readonly Listbox listbox = new() { Name = SelectParts.Listbox, Focusable = false };
    readonly RemoteOptions remote;
    readonly Dictionary<string, string> seen = [];
    IReadOnlyList<object?> all = [];
    Dictionary<string, string>? labels;
    bool syncing;

    /// <summary>新建：弹出层里是列表。</summary>
    public Select()
    {
        remote = new RemoteOptions(item => ListboxOption.From(item, ToOption).Value, OnRemoteChanged);
        listbox.Reached += last => remote.Reached(last);
        SetBody(listbox);
        listbox.PropertyChanged += (_, e) =>
        {
            if (e.Property == Listbox.ValueProperty || e.Property == Listbox.ValuesProperty) OnListChanged();
        };
        listbox.Picked += (_, _) => Picked();
        Search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) OnSearchChanged();
        };
    }

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>搜索时怎样算匹配。</summary>
    public Func<object?, string, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>可以新建选项。</summary>
    public bool Creatable { get => GetValue(CreatableProperty); set => SetValue(CreatableProperty, value); }

    /// <summary>选项的内容模板。</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>远程选项的加载函数。</summary>
    public OptionsLoader? Load { get => GetValue(LoadProperty); set => SetValue(LoadProperty, value); }

    /// <summary>新建了一项（<see cref="Creatable"/> 时）：参数是输入的文字，也就是选中的值。</summary>
    public event EventHandler<SelectCreatedEventArgs>? OptionCreated
    {
        add => AddHandler(OptionCreatedEvent, value);
        remove => RemoveHandler(OptionCreatedEvent, value);
    }

    /// <summary>弹出层里的列表（测试用）。</summary>
    internal Listbox List => listbox;

    /// <inheritdoc />
    protected override bool WithSearch => Searchable || Creatable;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == ToOptionProperty || change.Property == FilterProperty || change.Property == CreatableProperty)
        {
            all = ItemsSource?.Cast<object?>().ToList() ?? [];
            labels = null;
            if (change.Property == CreatableProperty) UpdateHeader();
            UpdateItems();
            Sync();
        }
        else if (change.Property == IsLoadingProperty) listbox.IsLoading = IsLoading || remote.Loading;
        else if (change.Property == LoadProperty)
        {
            remote.Load = Load;
            OnRemoteChanged();
        }
        else if (change.Property == EmptyProperty) UpdateEmpty();
        else if (change.Property == ItemTemplateProperty) listbox.ItemTemplate = ItemTemplate;
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
            ? SelectSearch.Shown(Source, item => ListboxOption.From(item, ToOption), Search.Text ?? "", localFilter: Load is null, Filter, Creatable)
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
            : string.IsNullOrEmpty(Search.Text) ? Empty
            : new Empty { Size = TextSize.Sm, Description = lang.SelectNoMatch };
    }

    /// <summary>值 → 列表，再同步外壳。</summary>
    protected override void Sync()
    {
        var selected = Selected;
        syncing = true;
        listbox.SelectionMode = SelectionMode;
        listbox.IsRequired = !Multiple;
        if (Multiple) listbox.Values = selected;
        else listbox.Value = selected.FirstOrDefault();
        syncing = false;
        base.Sync();
    }

    /// <summary>
    /// 已选项的文字取自全部数据（被过滤掉的也在）；数据里没有时取见过的（远程搜索换掉了结果），再没有时（新建了还没加进来）就是值本身。
    /// 值 → 文字的表按数据建一次：已选项很多时逐个在数据里找是 O(n²)（上游的性能用例：三万个全选）。初始化器里 ToOption 可能还没设：认不出的数据先跳过。
    /// </summary>
    protected override string LabelOf(string value)
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
        RaiseEvent(new SelectCreatedEventArgs(OptionCreatedEvent, created.Text));
        Search.Text = "";
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
        remote.SetQuery(Search.Text ?? "");
        UpdateItems();
        // 输入搜索词：高亮第一个匹配的（打开时清空搜索词不算：那时高亮的是已选项）
        if (!string.IsNullOrEmpty(Search.Text)) listbox.State.MoveToEnd(last: false);
    }

    /// <summary>打开：清空搜索词，高亮已选的第一项（<paramref name="last"/> 且没有已选时高亮最后一项）。</summary>
    protected override void Show(bool last = false)
    {
        Search.Text = "";
        UpdateItems();
        listbox.State.ActivateDefault();
        if (last && Selected.Count == 0) listbox.State.MoveToEnd(last: true);
        SetOpen(true, OpenChangeReason.Trigger);
    }

    /// <inheritdoc />
    protected override void Toggle()
    {
        remote.SetOpen(IsOpen);
        base.Toggle();
    }

    /// <summary>关着时 ↑ 也打开（高亮最后一项）。</summary>
    protected override bool OnClosedKeyDown(Key key)
    {
        if (key != Key.Up) return base.OnClosedKeyDown(key);
        Show(last: true);
        return true;
    }

    /// <summary>开着时：列表的按键（焦点留在触发框上）。</summary>
    protected override void OnOpenKeyDown(KeyEventArgs e)
    {
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

    /// <summary>搜索框里：↑↓、Page Up / Down 移动，Enter 选中（Home / End、空格留给输入）。</summary>
    protected override void OnSearchKey(KeyEventArgs e)
    {
        if (e.Key is Key.Home or Key.End) return;
        if (Listbox.ToListKey(e.Key) is { } key) e.Handled = listbox.State.Handle(key, space: false, picked: _ => Picked());
    }
}

/// <summary>新建了一项的事件参数。</summary>
/// <param name="routedEvent">事件。</param>
/// <param name="text">输入的文字，也就是选中的值。</param>
public sealed class SelectCreatedEventArgs(RoutedEvent routedEvent, string text) : RoutedEventArgs(routedEvent)
{
    /// <summary>输入的文字。</summary>
    public string Text { get; } = text;
}

/// <summary>Select、TreeSelect 的部件名：模板里的用 <c>rune|Select /template/ rune|SelectedTags#PART_Tags</c>，弹出层里的用 <c>rune|Select Border#PART_Popover</c>。</summary>
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

    /// <summary>TreeSelect 弹出层里的树（TS 的 propsTree）。</summary>
    public const string Tree = "PART_Tree";
}
