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

namespace Rune.Avalonia;

/// <summary>
/// 标签输入（vue-tsx-pro 的 TagsInput，多选的组合框）：输入框里排着已选的 <see cref="Tag"/>，输入文字后回车（或输入逗号）加一个，可以从建议列表里选。
/// 关键词、收件人、标签、多个城市。
/// <code>
/// &lt;rune:TagsInput Values="{Binding Keywords}" AutomationProperties.Name="关键词" /&gt;                              &lt;!-- 自由输入 --&gt;
/// &lt;rune:TagsInput Values="{Binding Cities}" ItemsSource="{Binding CityOptions}" AllowCustomValue="False" /&gt;   &lt;!-- 只能从建议里选 --&gt;
/// </code>
/// 输入框就是控件自己（TextBox），<see cref="TextBox.Text"/> 是还没确认的文字（上游的 inputValue，异步取建议时据此请求）。
/// 回车、输入分隔符（<see cref="Delimiters"/>，默认 , ， ; ；）把文字加成标签；粘贴时按分隔符与换行拆成多个；重复的、超出 <see cref="Max"/> 的不加。
/// 输入框为空时按退格删掉最后一个标签；每个标签有移除按钮（在 Tab 顺序里，可以逐个删除）；失焦时没确认的文字默认加成标签（<see cref="AcceptOnBlur"/>）。
/// 有 <see cref="ItemsSource"/> 时边输入边过滤建议（同 Select 的搜索），↓ 打开，↑↓ 移动，回车加入或移除高亮项（已选的打勾），Esc 关闭；
/// 允许自定义值时不自动高亮（回车加的是打的字），只能从建议里选时高亮第一条。异步取建议时关掉 <see cref="LocalFilter"/>、绑定 Text 去请求、
/// 写 <see cref="IsLoading"/>：这时没有建议也是组合框，列表显示加载中。外壳同输入框（Variant、Size、Radius、Icon、出错、只读、不可用），
/// 标签多了换行；只读、不可用时标签没有移除按钮。部件名见 <see cref="TagsInputParts"/>。
/// </summary>
public class TagsInput : TextInputBase
{
    /// <summary>已选的值（默认双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ValuesProperty =
        AvaloniaProperty.Register<TagsInput, IReadOnlyList<string>?>(nameof(Values), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>建议：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>；不给时是纯自由输入。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<TagsInput, IEnumerable?>(nameof(ItemsSource));

    /// <summary>数据 → 选项。</summary>
    public static readonly StyledProperty<Func<object?, ListboxOption>?> ToOptionProperty = AvaloniaProperty.Register<TagsInput, Func<object?, ListboxOption>?>(nameof(ToOption));

    /// <summary>允许加入不在建议里的文字，默认 true。</summary>
    public static readonly StyledProperty<bool> AllowCustomValueProperty = AvaloniaProperty.Register<TagsInput, bool>(nameof(AllowCustomValue), true);

    /// <summary>失焦时把还没确认的文字加成标签，默认 true；false 时文字留在框里。</summary>
    public static readonly StyledProperty<bool> AcceptOnBlurProperty = AvaloniaProperty.Register<TagsInput, bool>(nameof(AcceptOnBlur), true);

    /// <summary>把输入拆成标签的分隔符；换行与制表符总是分隔。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> DelimitersProperty =
        AvaloniaProperty.Register<TagsInput, IReadOnlyList<string>>(nameof(Delimiters), TagsText.DefaultDelimiters);

    /// <summary>最多几个标签；到了上限不再加入。</summary>
    public static readonly StyledProperty<int?> MaxProperty = AvaloniaProperty.Register<TagsInput, int?>(nameof(Max));

    /// <summary>建议列表开着（双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<TagsInput, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>怎样算匹配（数据、输入的文字）；不写时显示文字包含输入（忽略大小写与重音）。</summary>
    public static readonly StyledProperty<Func<object?, string, bool>?> FilterProperty = AvaloniaProperty.Register<TagsInput, Func<object?, string, bool>?>(nameof(Filter));

    /// <summary>按输入在本地过滤建议，默认 true；异步取建议（TS 的 filter={false}）时关掉，这时没有建议也是组合框。</summary>
    public static readonly StyledProperty<bool> LocalFilterProperty = AvaloniaProperty.Register<TagsInput, bool>(nameof(LocalFilter), true);

    /// <summary>正在取建议：列表显示加载中。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<TagsInput, bool>(nameof(IsLoading));

    /// <summary>前置图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<TagsInput, IconData?>(nameof(Icon));

    /// <summary>建议的内容模板（拿到原始数据）。</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<TagsInput, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary><see cref="OpenChanged"/> 的路由事件（直接：不往上传）。</summary>
    public static readonly RoutedEvent<OpenChangedEventArgs> OpenChangedEvent = RoutedEvent.Register<TagsInput, OpenChangedEventArgs>(nameof(OpenChanged), RoutingStrategies.Direct);

    /// <summary>与外壳的距离（像素）。</summary>
    const double Offset = 6;

    readonly Popup popup = new() { IsLightDismissEnabled = true, WindowManagerAddShadowHint = false, Placement = PlacementMode.BottomEdgeAlignedLeft };
    readonly Border panel = new() { Name = TagsInputParts.Popover, ClipToBounds = true };
    readonly Listbox listbox = new() { Name = TagsInputParts.Listbox, Focusable = false, SelectionMode = SelectionMode.Multiple };
    readonly Dictionary<string, Tag> tags = [];
    readonly Dictionary<Tag, EventHandler> removers = [];
    readonly Dictionary<string, string> seen = [];
    IReadOnlyList<object?> all = [];
    Flex? flow;
    Control? input;
    ButtonBase? clear;
    OpenChangeReason? pending;
    bool syncing;

    /// <summary>新建：没有标签、没有建议。</summary>
    public TagsInput()
    {
        panel.Child = listbox;
        popup.Child = new Panel { Margin = new Thickness(0, Offset), Children = { panel } };
        popup.PlacementTarget = this;
        LogicalChildren.Add(popup);
        popup.Closed += (_, _) =>
        {
            if (IsOpen) SetOpen(false, OpenChangeReason.Outside);
        };
        listbox.PropertyChanged += (_, e) =>
        {
            if (e.Property == Listbox.ValuesProperty) OnListChanged();
        };
        listbox.Picked += (_, _) => SetCurrentValue(TextProperty, "");
        AddHandler(PastingFromClipboardEvent, OnPasting);
    }

    /// <summary>已选的值。</summary>
    public IReadOnlyList<string>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>建议。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>允许加入不在建议里的文字。</summary>
    public bool AllowCustomValue { get => GetValue(AllowCustomValueProperty); set => SetValue(AllowCustomValueProperty, value); }

    /// <summary>失焦时把没确认的文字加成标签。</summary>
    public bool AcceptOnBlur { get => GetValue(AcceptOnBlurProperty); set => SetValue(AcceptOnBlurProperty, value); }

    /// <summary>分隔符。</summary>
    public IReadOnlyList<string> Delimiters { get => GetValue(DelimitersProperty); set => SetValue(DelimitersProperty, value); }

    /// <summary>最多几个标签。</summary>
    public int? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>建议列表开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>怎样算匹配。</summary>
    public Func<object?, string, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>按输入在本地过滤建议。</summary>
    public bool LocalFilter { get => GetValue(LocalFilterProperty); set => SetValue(LocalFilterProperty, value); }

    /// <summary>正在取建议。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>建议的内容模板。</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>用户开、关了建议列表（输入、↓、选中、点外面、Esc、失焦）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary>建议列表（测试用）。</summary>
    internal Listbox List => listbox;

    /// <summary>显示出来的标签（测试用，按值的顺序）。</summary>
    internal IEnumerable<Tag> Tags => (Values ?? []).Select(value => tags[value]);

    /// <summary>有建议列表：给了建议，或异步取建议（还没回来也算）。</summary>
    internal bool Suggests => all.Count > 0 || !LocalFilter;

    IReadOnlyList<string> Current => Values ?? [];

    /// <inheritdoc />
    protected override void OnTemplateParts(INameScope scope)
    {
        if (flow is not null) foreach (var tag in tags.Values) flow.Children.Remove(tag);
        flow = scope.Find<Flex>(TagsInputParts.Flow);
        input = scope.Find<Control>(TagsInputParts.Input);
        clear = scope.Find<ButtonBase>(TextInputParts.Clear);
        tags.Clear();
        removers.Clear();
    }

    /// <inheritdoc />
    protected override bool SyncsOn(AvaloniaProperty property) => property == ValuesProperty;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) OnTextChanged();
        else if (change.Property == ItemsSourceProperty || change.Property == ToOptionProperty || change.Property == FilterProperty || change.Property == LocalFilterProperty)
        {
            all = ItemsSource?.Cast<object?>().ToList() ?? [];
            UpdateItems();
            Sync();
        }
        else if (change.Property == IsOpenProperty) Toggle();
        else if (change.Property == IsLoadingProperty) listbox.IsLoading = IsLoading;
        else if (change.Property == ItemTemplateProperty) listbox.ItemTemplate = ItemTemplate;
        else if (change.Property == FontSizeProperty) UpdateMetrics();
    }

    /// <summary>值 → 标签（按值复用，移除按钮上的焦点不丢）、列表的已选、清空按钮。</summary>
    protected override void Sync()
    {
        base.Sync();
        var values = Current;
        syncing = true;
        listbox.Values = values;
        syncing = false;
        if (clear is not null) clear.IsVisible = Clearable && Editable && values.Count > 0;
        if (flow is null || input is null) return;
        foreach (var gone in tags.Keys.Except(values).ToList())
        {
            removers.Remove(tags[gone]);
            flow.Children.Remove(tags[gone]);
            tags.Remove(gone);
        }
        var at = flow.Children.IndexOf(input);
        foreach (var value in values)
        {
            if (!tags.TryGetValue(value, out var tag))
            {
                tag = new Tag { Name = TagsInputParts.Tag, Variant = StatusVariant.Surface };
                removers[tag] = (_, _) => Remove(value);
                tags[value] = tag;
            }
            tag.Content = LabelOf(value);
            // 只读、不可用时没有移除按钮（Tag 订阅了 RemoveRequested 才有）
            tag.RemoveRequested -= removers[tag];
            if (Editable) tag.RemoveRequested += removers[tag];
            var index = flow.Children.IndexOf(tag);
            if (index < 0) flow.Children.Insert(at, tag);
            else if (index != at)
            {
                flow.Children.RemoveAt(index);
                flow.Children.Insert(index < at ? at - 1 : at, tag);
            }
            at = flow.Children.IndexOf(tag) + 1;
        }
    }

    /// <summary>标签的文字：建议里的文字；建议换掉了时取见过的；自定义的值就是文字。</summary>
    string LabelOf(string value)
    {
        foreach (var item in all)
        {
            var option = ListboxOption.From(item, ToOption);
            if (option.Value == value) return seen[value] = option.Label;
        }
        return seen.TryGetValue(value, out var label) ? label : value;
    }

    /// <summary>列表里的建议：有输入时按文字过滤（异步取建议时不过滤）。</summary>
    void UpdateItems()
    {
        listbox.ToOption = ToOption;
        var query = (Text ?? "").Trim();
        listbox.ItemsSource = !LocalFilter || query.Length == 0
            ? all
            : [.. all.Where(item => Filter?.Invoke(item, query) ?? SelectSearch.Matches(ListboxOption.From(item, ToOption).Label, query))];
    }

    /// <summary>输入了分隔符：前面的部分加成标签，剩下的留在框里；有建议时打开并按需高亮第一条。</summary>
    void OnTextChanged()
    {
        var text = Text ?? "";
        var parts = TagsText.Split(text, Delimiters);
        if (parts.Length > 1)
        {
            Add(parts[..^1]);
            SetCurrentValue(TextProperty, parts[^1]);
            CaretIndex = parts[^1].Length;
            return;
        }
        UpdateItems();
        if (!Suggests || text.Length == 0 || ReadOnly) return;
        SetOpen(true, OpenChangeReason.Trigger);
        // 允许自己输入时不高亮：回车加的是打的字（"java"不该变成建议里的"javascript"），要选建议按 ↓；只能从建议里选时高亮第一条
        if (AllowCustomValue) listbox.State.SetActive(null);
        else listbox.State.MoveToEnd(last: false);
    }

    /// <summary>加入几段文字（去重、限个数；只读、不可用时不加）；返回是否加入了。</summary>
    internal bool Add(IEnumerable<string> texts)
    {
        if (!Editable) return false;
        var options = all.Select(item => ListboxOption.From(item, ToOption)).ToList();
        var next = TagsText.Add(Current, texts.Select(text => TagsText.Resolve(text, options, AllowCustomValue)), Max);
        if (next is null) return false;
        SetCurrentValue(ValuesProperty, next);
        return true;
    }

    void Remove(string value) => SetCurrentValue(ValuesProperty, Current.Where(item => item != value).ToList());

    /// <summary>列表的选择 → 值；从建议里选的也受上限限制（取消选中照常）。</summary>
    void OnListChanged()
    {
        if (syncing) return;
        var next = listbox.Values ?? [];
        if (TagsText.Accepts(Current, next, Max))
        {
            SetCurrentValue(ValuesProperty, next);
            return;
        }
        // 拒绝了：列表退回原来的已选（新的列表实例，列表才会重新同步）
        syncing = true;
        listbox.Values = [.. Current];
        syncing = false;
    }

    /// <summary>键盘（见类的说明）；在 TextBox 自己处理之前。</summary>
    internal bool Press(Key key)
    {
        if (!Editable) return false;
        var text = Text ?? "";
        if (key == Key.Back && text.Length == 0 && Current is [.., var last])
        {
            Remove(last);
            return true;
        }
        if (IsOpen && Suggests)
        {
            if (key == Key.Escape)
            {
                SetOpen(false, OpenChangeReason.Escape);
                return true;
            }
            if (Listbox.ToListKey(key) is { } listKey and not (ListKey.Home or ListKey.End or ListKey.Space) && listbox.State.Handle(listKey, space: false))
            {
                if (key == Key.Enter) SetCurrentValue(TextProperty, "");
                return true;
            }
        }
        else if (key == Key.Down && Suggests)
        {
            listbox.State.ActivateDefault();
            SetOpen(true, OpenChangeReason.Trigger);
            return true;
        }
        if (key != Key.Enter || text.Trim().Length == 0) return false;
        if (Add([text])) SetCurrentValue(TextProperty, "");
        return true;
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

    /// <summary>粘贴多段（有分隔符、换行）时拆开加入；只有一段时照常粘贴。</summary>
    async void OnPasting(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        e.Handled = true;
        PasteText(await global::Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard) ?? "");
    }

    /// <summary>粘贴一段文字（测试直接调用）：多段时拆开加入，否则替换选中的文字。</summary>
    internal void PasteText(string text)
    {
        var parts = TagsText.Split(text, Delimiters);
        if (parts.Length > 1)
        {
            Add(parts);
            return;
        }
        if (!Editable) return;
        var current = Text ?? "";
        var start = Math.Min(SelectionStart, SelectionEnd);
        var length = Math.Abs(SelectionEnd - SelectionStart);
        SetCurrentValue(TextProperty, current.Remove(start, length).Insert(start, text));
        CaretIndex = start + text.Length;
    }

    /// <summary>失焦（焦点离开输入框，包括移到标签的移除按钮上）：关上建议，按需把没确认的文字加成标签。</summary>
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (e.Source != this) return;
        if (IsOpen) SetOpen(false, OpenChangeReason.Focus);
        if (AcceptOnBlur && (Text ?? "").Trim().Length > 0 && !ReadOnly && Add([Text!])) SetCurrentValue(TextProperty, "");
    }

    /// <inheritdoc />
    protected override void ClearText()
    {
        SetCurrentValue(ValuesProperty, Array.Empty<string>());
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
        PseudoClasses.Set(":open", IsOpen);
        var shown = IsOpen && Suggests;
        if (shown) panel.MinWidth = Bounds.Width;
        if (popup.IsOpen != shown) popup.IsOpen = shown;
        if (reason is { } known) RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, IsOpen, known));
    }

    /// <summary>弹出层的尺寸按 em：lg 圆角；字号同外壳。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        panel.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Lg) * em);
        panel.SetValue(TextElement.FontSizeProperty, em);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateMetrics();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TagsInputPeer(this);

    /// <summary>读屏：有建议时是组合框（展开、收起），否则是编辑框；名称、说明同输入框。</summary>
    sealed class TagsInputPeer(TagsInput owner) : TextInputPeer(owner), IExpandCollapseProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.Suggests ? AutomationControlType.ComboBox : base.GetAutomationControlTypeCore();

        public ExpandCollapseState ExpandCollapseState => owner.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (owner.Suggests && owner.Editable) owner.SetOpen(true, OpenChangeReason.Trigger);
        }

        public void Collapse()
        {
            if (owner.IsOpen) owner.SetOpen(false, OpenChangeReason.Trigger);
        }
    }
}

/// <summary>TagsInput 的部件名：模板里的用 <c>rune|TagsInput /template/ rune|Tag#PART_Tag</c>，弹出层里的用 <c>rune|TagsInput Border#PART_Popover</c>。</summary>
public static class TagsInputParts
{
    /// <summary>外壳（TS 的 propsRoot）。</summary>
    public const string Root = TextInputParts.Root;

    /// <summary>清空按钮（TS 的 propsClear）。</summary>
    public const string Clear = TextInputParts.Clear;

    /// <summary>外壳里排标签与输入框、会换行的那一行（TS 的 propsRoot 的内容）。</summary>
    public const string Flow = "PART_Flow";

    /// <summary>每个标签（TS 的 propsTag）。</summary>
    public const string Tag = "PART_Tag";

    /// <summary>输入框的文字区（至少 6em，放不下时换到下一行）。</summary>
    public const string Input = "PART_Input";

    /// <summary>弹出层面板（TS 的 propsPopover）。</summary>
    public const string Popover = "PART_Popover";

    /// <summary>建议列表（TS 的 propsListbox）。</summary>
    public const string Listbox = "PART_Listbox";
}
