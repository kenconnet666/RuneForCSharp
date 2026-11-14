using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Rune.Avalonia;

/// <summary>
/// 组合框（vue-tsx-pro 的 Combobox，WAI-ARIA 的 combobox，aria-autocomplete="list"）：边输入边从列表里选，或者只当带建议的输入框用。
/// <code>
/// &lt;rune:Field Label="城市"&gt;&lt;rune:Combobox ItemsSource="{Binding Cities}" Value="{Binding City}" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:Combobox ItemsSource="{Binding Results}" LocalFilter="False" IsLoading="{Binding Searching}" Text="{Binding Keyword}" /&gt;
/// </code>
/// 输入框就是控件自己（TextBox，<see cref="TextBox.Text"/> 是上游的 inputValue），外壳同 TextField；选项在输入框下面的弹出层里（与外壳同宽），
/// 焦点一直在输入框里。过滤：默认按显示文字包含输入（忽略大小写与重音，同 Select 的搜索），<see cref="Filter"/> 自定义；
/// <see cref="LocalFilter"/> 关掉时不过滤（调用方按 Text 换 ItemsSource，配合 <see cref="IsLoading"/>）；给 <see cref="Load"/> 时组件自己请求
/// （打开时与输入停下 300 毫秒后按输入请求，只采用最后一次，可以分页，同 Select）。输入法组合中的文字不在 Text 里，不拿拼音去过滤。
/// 选中后输入框显示选项文字；失焦时文字不是选项（又没开 <see cref="AllowCustomValue"/>）就恢复成已选项的文字，清空则取消选择；
/// 开着 AllowCustomValue 时文字保留、Value 为 null。键盘：↓ / ↑ 打开，↑↓、Page Up / Down 移动，Enter 选中，Esc 关闭（关着时再按 Esc 清空）；
/// Home / End 照常移动光标。<see cref="ShowToggle"/> 控制右侧的展开按钮（不进 Tab 顺序），<see cref="HideEmpty"/> 时没有可选的项不弹出列表。
/// 部件名见 <see cref="ComboboxParts"/>。
/// </summary>
public class Combobox : TextInputBase
{
    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>；有 Load 时是第一次加载之前的选项（编辑页放上已选的那一项）。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<Combobox, IEnumerable?>(nameof(ItemsSource));

    /// <summary>数据 → 选项。</summary>
    public static readonly StyledProperty<Func<object?, ListboxOption>?> ToOptionProperty = AvaloniaProperty.Register<Combobox, Func<object?, ListboxOption>?>(nameof(ToOption));

    /// <summary>选中项的值（默认双向绑定）；没有选中时是 null。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<Combobox, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>列表开着（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<Combobox, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>怎样算匹配（数据、输入的文字）；不写时显示文字包含输入（忽略大小写与重音）。</summary>
    public static readonly StyledProperty<Func<object?, string, bool>?> FilterProperty = AvaloniaProperty.Register<Combobox, Func<object?, string, bool>?>(nameof(Filter));

    /// <summary>按输入在本地过滤选项，默认 true；调用方自己按 Text 取数据时（TS 的 filter={false}）关掉。</summary>
    public static readonly StyledProperty<bool> LocalFilterProperty = AvaloniaProperty.Register<Combobox, bool>(nameof(LocalFilter), true);

    /// <summary>远程选项的加载函数（TS 的 load）：输入、第几页 → 一页选项；结果代替 ItemsSource，不在本地过滤。</summary>
    public static readonly StyledProperty<OptionsLoader?> LoadProperty = AvaloniaProperty.Register<Combobox, OptionsLoader?>(nameof(Load));

    /// <summary>允许输入不在选项里的文字：失焦时保留，Value 为 null。</summary>
    public static readonly StyledProperty<bool> AllowCustomValueProperty = AvaloniaProperty.Register<Combobox, bool>(nameof(AllowCustomValue));

    /// <summary>显示右侧的展开按钮，默认 true；只当带建议的输入框用时（Autocomplete）关掉。</summary>
    public static readonly StyledProperty<bool> ShowToggleProperty = AvaloniaProperty.Register<Combobox, bool>(nameof(ShowToggle), true);

    /// <summary>没有可选的项（也不在加载）时不弹出列表、不显示"没有匹配的选项"（Autocomplete）。</summary>
    public static readonly StyledProperty<bool> HideEmptyProperty = AvaloniaProperty.Register<Combobox, bool>(nameof(HideEmpty));

    /// <summary>正在取选项：列表换成骨架行。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Combobox, bool>(nameof(IsLoading));

    /// <summary>前置图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<Combobox, IconData?>(nameof(Icon));

    /// <summary>没有匹配的选项时列表里显示的内容。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<Combobox, object?>(nameof(Empty));

    /// <summary>选项的内容模板（拿到原始数据）。</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<Combobox, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary><see cref="OpenChanged"/> 的路由事件（直接：不往上传）。</summary>
    public static readonly RoutedEvent<OpenChangedEventArgs> OpenChangedEvent = RoutedEvent.Register<Combobox, OpenChangedEventArgs>(nameof(OpenChanged), RoutingStrategies.Direct);

    readonly ListPopover popover;
    readonly Listbox listbox;
    readonly RemoteOptions remote;
    readonly Dictionary<string, string> seen = [];
    IReadOnlyList<object?> all = [];
    ButtonBase? clear;
    ButtonBase? toggle;
    OpenChangeReason? pending;
    // 用户输入的过滤词：选中、用箭头打开时清空（显示全部选项）
    string query = "";
    // 组件自己写 Text（选中后写入选项文字）时，不当作用户输入
    bool writing;
    bool syncing;

    /// <summary>新建：没有选项、没有选中。</summary>
    public Combobox()
    {
        remote = new RemoteOptions(item => ListboxOption.From(item, ToOption).Value, OnRemoteChanged);
        popover = new ListPopover(this, LogicalChildren, () =>
        {
            if (IsOpen) SetOpen(false, OpenChangeReason.Outside);
        });
        listbox = popover.Listbox;
        listbox.IsRequired = true;
        listbox.Reached += last => remote.Reached(last);
        listbox.Picked += (_, e) => Pick(e.Value);
    }

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>选中项的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>列表开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>怎样算匹配。</summary>
    public Func<object?, string, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>按输入在本地过滤选项。</summary>
    public bool LocalFilter { get => GetValue(LocalFilterProperty); set => SetValue(LocalFilterProperty, value); }

    /// <summary>远程选项的加载函数。</summary>
    public OptionsLoader? Load { get => GetValue(LoadProperty); set => SetValue(LoadProperty, value); }

    /// <summary>允许输入不在选项里的文字。</summary>
    public bool AllowCustomValue { get => GetValue(AllowCustomValueProperty); set => SetValue(AllowCustomValueProperty, value); }

    /// <summary>显示右侧的展开按钮。</summary>
    public bool ShowToggle { get => GetValue(ShowToggleProperty); set => SetValue(ShowToggleProperty, value); }

    /// <summary>没有可选的项时不弹出列表。</summary>
    public bool HideEmpty { get => GetValue(HideEmptyProperty); set => SetValue(HideEmptyProperty, value); }

    /// <summary>正在取选项。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>没有匹配的选项时显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>选项的内容模板。</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>用户开、关了列表（输入、↓、展开按钮、选中、点外面、Esc、失焦）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary>列表（测试用）。</summary>
    internal Listbox List => listbox;

    /// <summary>列表真的弹出来了：开着，且有可选的项、在加载或不隐藏空列表。</summary>
    internal bool Shown => IsOpen && (!HideEmpty || listbox.State.Options.Count > 0 || IsLoading || remote.Loading);

    /// <summary>当前的数据：远程请求到的，没有时是 ItemsSource。</summary>
    IReadOnlyList<object?> Source => remote.ItemsOr(all);

    /// <inheritdoc />
    protected override void OnTemplateParts(INameScope scope)
    {
        if (toggle is not null) toggle.Click -= OnToggleClick;
        clear = scope.Find<ButtonBase>(TextInputParts.Clear);
        toggle = scope.Find<ButtonBase>(ComboboxParts.Chevron);
        if (toggle is null) return;
        toggle.Click += OnToggleClick;
        AutomationProperties.SetName(toggle, Provider.GetLang(this).ComboboxToggle);
    }

    /// <inheritdoc />
    protected override bool SyncsOn(AvaloniaProperty property) => property == ShowToggleProperty || property == IsOpenProperty;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) OnTextChanged();
        else if (change.Property == ItemsSourceProperty || change.Property == ToOptionProperty || change.Property == FilterProperty || change.Property == LocalFilterProperty)
        {
            all = ItemsSource?.Cast<object?>().ToList() ?? [];
            UpdateItems();
            ShowSelectedLabel();
        }
        else if (change.Property == ValueProperty)
        {
            syncing = true;
            listbox.Value = Value;
            syncing = false;
            ShowSelectedLabel();
        }
        else if (change.Property == LoadProperty)
        {
            remote.Load = Load;
            OnRemoteChanged();
        }
        else if (change.Property == IsOpenProperty) Toggle();
        else if (change.Property == IsLoadingProperty || change.Property == HideEmptyProperty)
        {
            listbox.IsLoading = IsLoading || remote.Loading;
            popover.Show(Shown);
        }
        else if (change.Property == EmptyProperty) UpdateEmpty();
        else if (change.Property == ItemTemplateProperty) listbox.ItemTemplate = ItemTemplate;
        else if (change.Property == FontSizeProperty) popover.UpdateMetrics(FontSize);
    }

    /// <summary>清空按钮看 Text；展开按钮看 ShowToggle，只读、不可用时不能按。</summary>
    protected override void Sync()
    {
        base.Sync();
        if (toggle is null) return;
        toggle.IsVisible = ShowToggle;
        toggle.IsEnabled = Editable;
        PseudoClasses.Set(":open", Shown);
    }

    /// <summary>已选项的文字：数据里的文字；远程搜索换掉了结果时取见过的；都没有时是值本身。</summary>
    string LabelOf(string value)
    {
        foreach (var item in Source)
        {
            var option = ListboxOption.From(item, ToOption);
            if (option.Value == value) return seen[value] = option.Label;
        }
        return seen.TryGetValue(value, out var label) ? label : value;
    }

    string SelectedLabel => Value is { } value ? LabelOf(value) : "";

    /// <summary>父级改了 Value，或选项后到（编辑页先拿到值、再拿到选项）：输入框显示它的文字；用户正在输入时不打断。</summary>
    void ShowSelectedLabel()
    {
        if (query.Length > 0) return;
        if (!AllowCustomValue || Value is not null) SetText(SelectedLabel);
    }

    void SetText(string text)
    {
        if ((Text ?? "") == text) return;
        writing = true;
        SetCurrentValue(TextProperty, text);
        writing = false;
    }

    /// <summary>列表里的选项：本地按输入过滤；远程选项、关掉本地过滤时原样。</summary>
    void UpdateItems()
    {
        listbox.ToOption = ToOption;
        listbox.ItemsSource = Load is not null || !LocalFilter || query.Length == 0
            ? Source
            : [.. Source.Where(item => Filter?.Invoke(item, query) ?? SelectSearch.Matches(ListboxOption.From(item, ToOption).Label, query))];
        syncing = true;
        listbox.Value = Value;
        syncing = false;
        UpdateEmpty();
        popover.Show(Shown);
    }

    /// <summary>没有选项时：加载失败是"加载失败，请重试"，否则是 Empty（不写时列表自己的）。</summary>
    void UpdateEmpty()
    {
        listbox.Empty = remote.Failed ? new Empty { Size = TextSize.Sm, Description = Provider.GetLang(this).ListLoadFailed } : Empty;
    }

    void OnRemoteChanged()
    {
        listbox.IsLoading = IsLoading || remote.Loading;
        listbox.IsLoadingMore = remote.LoadingMore;
        UpdateItems();
        ShowSelectedLabel();
    }

    /// <summary>用户输入（含输入法组合结束）：按输入过滤并高亮第一项；输入框聚焦时打开（父级把文字改掉时不弹出）。</summary>
    void OnTextChanged()
    {
        if (writing) return;
        query = Text ?? "";
        remote.SetQuery(query);
        UpdateItems();
        if (!ReadOnly && IsFocused) SetOpen(true, OpenChangeReason.Trigger);
        listbox.State.MoveToEnd(last: false);
    }

    /// <summary>选中一项：值是它，输入框显示它的文字，关上。</summary>
    internal void Pick(string choice)
    {
        if (syncing) return;
        SetCurrentValue(ValueProperty, choice);
        query = "";
        remote.SetQuery("");
        SetText(SelectedLabel);
        SetOpen(false, OpenChangeReason.Select);
    }

    /// <summary>打开：显示全部选项，高亮已选的（没有时第一项）。</summary>
    void Show()
    {
        query = "";
        remote.SetQuery("");
        UpdateItems();
        listbox.State.ActivateDefault();
        SetOpen(true, OpenChangeReason.Trigger);
    }

    void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        if (IsOpen) SetOpen(false, OpenChangeReason.Trigger);
        else Show();
        Focus();
    }

    /// <summary>失焦：关上；文字不是选项时恢复成已选项的文字（清空则取消选择）；允许自定义时文字保留、Value 为 null。</summary>
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (e.Source != this) return;
        if (IsOpen) SetOpen(false, OpenChangeReason.Focus);
        var text = Text ?? "";
        if (AllowCustomValue)
        {
            if (text != SelectedLabel) SetCurrentValue(ValueProperty, null);
            query = "";
            return;
        }
        if (text.Length == 0) SetCurrentValue(ValueProperty, null);
        else SetText(SelectedLabel);
        query = "";
    }

    /// <summary>键盘（见类的说明）；在 TextBox 自己处理之前（输入法组合中的键是 ImeProcessed，不处理）。</summary>
    internal bool Press(Key key)
    {
        if (!Editable) return false;
        if (key == Key.Escape)
        {
            if (IsOpen)
            {
                SetOpen(false, OpenChangeReason.Escape);
                return true;
            }
            if ((Text ?? "").Length == 0) return false;
            SetText("");
            query = "";
            SetCurrentValue(ValueProperty, null);
            return true;
        }
        if (!IsOpen)
        {
            if (key is not (Key.Down or Key.Up)) return false;
            Show();
            return true;
        }
        return Listbox.ToListKey(key) is { } listKey and not (ListKey.Home or ListKey.End or ListKey.Space)
               && listbox.State.Handle(listKey, space: false, picked: Pick);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Press(e.Key))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void ClearText()
    {
        SetText("");
        query = "";
        SetCurrentValue(ValueProperty, null);
        Focus();
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
        remote.SetOpen(IsOpen);
        popover.Show(Shown);
        PseudoClasses.Set(":open", Shown);
        if (reason is { } known) RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, IsOpen, known));
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        popover.UpdateMetrics(FontSize);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ComboboxPeer(this);

    /// <summary>读屏：组合框（展开、收起），值是输入框里的文字；名称、说明同输入框。</summary>
    sealed class ComboboxPeer(Combobox owner) : TextInputPeer(owner), IExpandCollapseProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

        public ExpandCollapseState ExpandCollapseState => owner.Shown ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!owner.IsOpen && owner.Editable) owner.Show();
        }

        public void Collapse()
        {
            if (owner.IsOpen) owner.SetOpen(false, OpenChangeReason.Trigger);
        }
    }
}

/// <summary>
/// 自动完成（vue-tsx-pro 的 Autocomplete）：值就是输入框里的文字（<see cref="TextBox.Text"/>），列表只是建议（邮箱后缀、常用地址、搜索历史、供应商名称）。
/// 选一项等于把它的文字填进来；不选也行，输入什么就是什么。要从固定的选项里选一个（值是选项的 value）时用 <see cref="Combobox"/>。
/// <code>
/// &lt;rune:Autocomplete Text="{Binding Email}" ItemsSource="{Binding Suggestions}" LocalFilter="False" /&gt;
/// </code>
/// 建在 Combobox 上：允许自定义值、没有展开按钮、没有可选的建议时不弹出列表；↓ 打开建议，↑↓ 移动，Enter 填入，Esc 关闭（关着时再按 Esc 清空）。
/// </summary>
public class Autocomplete : Combobox
{
    /// <summary>新建：允许自定义值、没有展开按钮、空列表不弹出。</summary>
    public Autocomplete()
    {
        AllowCustomValue = true;
        ShowToggle = false;
        HideEmpty = true;
    }
}

/// <summary>Combobox 的部件名：模板里的用 <c>:is(rune|Combobox) /template/ rune|IconButton#PART_Chevron</c>，弹出层里的用 <c>:is(rune|Combobox) Border#PART_Popover</c>。</summary>
public static class ComboboxParts
{
    /// <summary>外壳（TS 的 propsRoot）。</summary>
    public const string Root = TextInputParts.Root;

    /// <summary>清空按钮（TS 的 propsClear）。</summary>
    public const string Clear = TextInputParts.Clear;

    /// <summary>展开按钮（TS 的 propsChevron）。</summary>
    public const string Chevron = "PART_Chevron";

    /// <summary>弹出层面板（TS 的 propsPopover）。</summary>
    public const string Popover = "PART_Popover";

    /// <summary>列表（TS 的 propsListbox）。</summary>
    public const string Listbox = "PART_Listbox";
}
