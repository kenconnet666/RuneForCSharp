using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

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
/// 标签多了换行；只读、不可用时标签没有移除按钮。部件样式键见 <see cref="TagsInputParts"/>。
/// </summary>
public class TagsInput : TextInputBase
{
    /// <summary>已选的值（默认双向绑定）。</summary>
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(nameof(Values), typeof(IReadOnlyList<string>), typeof(TagsInput),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((TagsInput)d).Sync()));

    /// <summary>建议：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>；不给时是纯自由输入。</summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(TagsInput),
        new FrameworkPropertyMetadata(null, (d, _) => ((TagsInput)d).OnItemsChanged()));

    /// <summary>数据 → 选项。</summary>
    public static readonly DependencyProperty ToOptionProperty = DependencyProperty.Register(nameof(ToOption), typeof(Func<object?, ListboxOption>), typeof(TagsInput),
        new FrameworkPropertyMetadata(null, (d, _) => ((TagsInput)d).OnItemsChanged()));

    /// <summary>允许加入不在建议里的文字，默认 true。</summary>
    public static readonly DependencyProperty AllowCustomValueProperty = DependencyProperty.Register(nameof(AllowCustomValue), typeof(bool), typeof(TagsInput), new FrameworkPropertyMetadata(true));

    /// <summary>失焦时把还没确认的文字加成标签，默认 true；false 时文字留在框里。</summary>
    public static readonly DependencyProperty AcceptOnBlurProperty = DependencyProperty.Register(nameof(AcceptOnBlur), typeof(bool), typeof(TagsInput), new FrameworkPropertyMetadata(true));

    /// <summary>把输入拆成标签的分隔符；换行与制表符总是分隔。</summary>
    public static readonly DependencyProperty DelimitersProperty = DependencyProperty.Register(nameof(Delimiters), typeof(IReadOnlyList<string>), typeof(TagsInput),
        new FrameworkPropertyMetadata(TagsText.DefaultDelimiters));

    /// <summary>最多几个标签；到了上限不再加入。</summary>
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(nameof(Max), typeof(int?), typeof(TagsInput), new FrameworkPropertyMetadata(null));

    /// <summary>建议列表开着（双向绑定）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(TagsInput),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((TagsInput)d).Toggle()));

    /// <summary>怎样算匹配（数据、输入的文字）；不写时显示文字包含输入（忽略大小写与重音）。</summary>
    public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(nameof(Filter), typeof(Func<object?, string, bool>), typeof(TagsInput),
        new FrameworkPropertyMetadata(null, (d, _) => ((TagsInput)d).UpdateItems()));

    /// <summary>按输入在本地过滤建议，默认 true；异步取建议（TS 的 filter={false}）时关掉，这时没有建议也是组合框。</summary>
    public static readonly DependencyProperty LocalFilterProperty = DependencyProperty.Register(nameof(LocalFilter), typeof(bool), typeof(TagsInput),
        new FrameworkPropertyMetadata(true, (d, _) => ((TagsInput)d).OnItemsChanged()));

    /// <summary>正在取建议：列表显示加载中。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(TagsInput),
        new FrameworkPropertyMetadata(false, (d, e) => ((TagsInput)d).listbox.IsLoading = (bool)e.NewValue));

    /// <summary>前置图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(TagsInput), new FrameworkPropertyMetadata(null));

    /// <summary>建议的内容模板（拿到原始数据）。</summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(TagsInput),
        new FrameworkPropertyMetadata(null, (d, e) => ((TagsInput)d).listbox.ItemTemplate = (DataTemplate?)e.NewValue));

    /// <summary><see cref="OpenChanged"/> 的路由事件（直接：不往上传）。</summary>
    public static readonly RoutedEvent OpenChangedEvent = EventManager.RegisterRoutedEvent(nameof(OpenChanged), RoutingStrategy.Direct, typeof(EventHandler<OpenChangedEventArgs>), typeof(TagsInput));

    // 列表的已选（绑定到 Listbox.Values）：用户在列表里选了一项时收到
    static readonly DependencyProperty ListValuesProperty = DependencyProperty.Register("ListValues", typeof(IReadOnlyList<string>), typeof(TagsInput),
        new FrameworkPropertyMetadata(null, (d, _) => ((TagsInput)d).OnListChanged()));

    /// <summary>与外壳的距离（像素）。</summary>
    const double Offset = 6;

    /// <summary>弹出层四周给阴影留的透明边（像素）。</summary>
    const double Overhang = 16;

    readonly Popup popup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Custom };
    readonly Border shadow = new();
    readonly Border panel = new() { ClipToBounds = true };
    readonly Listbox listbox = new() { Focusable = false, SelectionMode = SelectionMode.Multiple };
    readonly Dictionary<string, Tag> tags = [];
    readonly Dictionary<Tag, EventHandler> removers = [];
    readonly Dictionary<string, string> seen = [];
    IReadOnlyList<object?> all = [];
    Flex? flow;
    FrameworkElement? input;
    ButtonBase? clear;
    OpenChangeReason? pending;
    bool syncing;

    static TagsInput() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TagsInput), new FrameworkPropertyMetadata(typeof(TagsInput)));

    /// <summary>新建：没有标签、没有建议。</summary>
    public TagsInput()
    {
        shadow.SetResourceReference(StyleProperty, TagsInputParts.PopoverShadow);
        panel.SetResourceReference(StyleProperty, TagsInputParts.Popover);
        panel.Child = listbox;
        var layer = new LayoutGrid { Margin = new Thickness(Overhang) };
        layer.Children.Add(shadow);
        layer.Children.Add(panel);
        popup.Child = layer;
        popup.PlacementTarget = this;
        popup.CustomPopupPlacementCallback = Place;
        AddLogicalChild(popup);
        popup.Closed += (_, _) =>
        {
            if (IsOpen) SetOpen(false, OpenChangeReason.Outside);
        };
        SetBinding(ListValuesProperty, new System.Windows.Data.Binding(nameof(Listbox.Values)) { Source = listbox });
        listbox.Picked += (_, _) => SetCurrentValue(TextProperty, "");
        TextChanged += (_, _) => OnTextChanged();
        DataObject.AddPastingHandler(this, OnPasting);
        Loaded += (_, _) => UpdateMetrics();
    }

    /// <summary>已选的值。</summary>
    public IReadOnlyList<string>? Values { get => (IReadOnlyList<string>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>建议。</summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => (Func<object?, ListboxOption>?)GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>允许加入不在建议里的文字。</summary>
    public bool AllowCustomValue { get => (bool)GetValue(AllowCustomValueProperty); set => SetValue(AllowCustomValueProperty, value); }

    /// <summary>失焦时把没确认的文字加成标签。</summary>
    public bool AcceptOnBlur { get => (bool)GetValue(AcceptOnBlurProperty); set => SetValue(AcceptOnBlurProperty, value); }

    /// <summary>分隔符。</summary>
    public IReadOnlyList<string> Delimiters { get => (IReadOnlyList<string>)GetValue(DelimitersProperty); set => SetValue(DelimitersProperty, value); }

    /// <summary>最多几个标签。</summary>
    public int? Max { get => (int?)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>建议列表开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>怎样算匹配。</summary>
    public Func<object?, string, bool>? Filter { get => (Func<object?, string, bool>?)GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>按输入在本地过滤建议。</summary>
    public bool LocalFilter { get => (bool)GetValue(LocalFilterProperty); set => SetValue(LocalFilterProperty, value); }

    /// <summary>正在取建议。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>建议的内容模板。</summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>用户开、关了建议列表（输入、↓、选中、点外面、Esc、失焦）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs> OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary>建议列表（测试用）。</summary>
    internal Listbox List => listbox;

    /// <summary>显示出来的标签（测试用，按值的顺序）。</summary>
    internal IEnumerable<Tag> Tags => Current.Where(tags.ContainsKey).Select(value => tags[value]);

    /// <summary>有建议列表：给了建议，或异步取建议（还没回来也算）。</summary>
    internal bool Suggests => all.Count > 0 || !LocalFilter;

    IReadOnlyList<string> Current => Values ?? [];

    bool ReadOnlyNow => ShowsReadOnly;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { popup }.GetEnumerator();

    /// <inheritdoc />
    protected override void OnTemplateParts()
    {
        if (flow is not null) foreach (var tag in tags.Values) flow.Children.Remove(tag);
        tags.Clear();
        removers.Clear();
        flow = GetTemplateChild(TagsInputParts.Flow) as Flex;
        input = GetTemplateChild(TagsInputParts.Input) as FrameworkElement;
        clear = GetTemplateChild(TextInputParts.Clear) as ButtonBase;
        UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) UpdateMetrics();
    }

    void OnItemsChanged()
    {
        all = ItemsSource?.Cast<object?>().ToList() ?? [];
        UpdateItems();
        Sync();
    }

    /// <summary>值 → 标签（按值复用，移除按钮上的焦点不丢）、列表的已选、清空按钮。</summary>
    protected override void Sync()
    {
        base.Sync();
        var values = Current;
        syncing = true;
        listbox.Values = values;
        syncing = false;
        if (clear is not null) clear.Visibility = Clearable && Editable && values.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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
                tag = new Tag { Variant = StatusVariant.Surface };
                tag.SetResourceReference(StyleProperty, TagsInputParts.Tag);
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
        if (!Suggests || text.Length == 0 || ReadOnlyNow) return;
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

    /// <summary>键盘（见类的说明）；在 TextBox 自己处理之前（输入法组合中的键是 ImeProcessed，不处理）。</summary>
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
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!e.Handled && Press(e.Key)) e.Handled = true;
    }

    /// <summary>粘贴多段（有分隔符、换行）时拆开加入；只有一段时照常粘贴。</summary>
    void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !PasteText(text)) return;
        e.CancelCommand();
    }

    /// <summary>粘贴一段文字（测试直接调用）：多段时拆开加入并返回 true（拦下平台的粘贴），否则交给平台。</summary>
    internal bool PasteText(string text)
    {
        var parts = TagsText.Split(text, Delimiters);
        if (parts.Length < 2) return false;
        Add(parts);
        return true;
    }

    /// <summary>失焦（焦点离开输入框，包括移到标签的移除按钮上）：关上建议，按需把没确认的文字加成标签。</summary>
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        if (!ReferenceEquals(e.OriginalSource, this)) return;
        if (IsOpen) SetOpen(false, OpenChangeReason.Focus);
        if (AcceptOnBlur && (Text ?? "").Trim().Length > 0 && !ReadOnlyNow && Add([Text!])) SetCurrentValue(TextProperty, "");
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
        var shown = IsOpen && Suggests;
        if (shown) panel.MinWidth = ActualWidth;
        if (popup.IsOpen != shown) popup.IsOpen = shown;
        if (reason is { } known) RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, IsOpen, known));
    }

    /// <summary>平台的位置：外壳下方、起始边对齐，放不下时翻到上方（同 Select）。尺寸是设备像素。</summary>
    CustomPopupPlacement[] Place(System.Windows.Size popupSize, System.Windows.Size targetSize, Point offset)
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

    /// <summary>按 em 的尺寸：输入框至少 6em；面板 lg 圆角，弹出层不在外壳里，字号同外壳。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        if (input is not null) input.MinWidth = 6 * em;
        panel.CornerRadius = shadow.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Lg) * em);
        TextElement.SetFontSize(panel, em);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TagsInputPeer(this);

    /// <summary>读屏：有建议时是组合框（展开、收起），否则是编辑框；名称、说明同输入框。</summary>
    sealed class TagsInputPeer(TagsInput owner) : TextInputPeer(owner), IExpandCollapseProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.Suggests ? AutomationControlType.ComboBox : base.GetAutomationControlTypeCore();

        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.ExpandCollapse && owner.Suggests ? this : base.GetPattern(patternInterface);

        public ExpandCollapseState ExpandCollapseState => owner.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

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

/// <summary>TagsInput 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:TagsInputParts.Tag}" TargetType="rune:Tag"&gt;</c>。</summary>
public static class TagsInputParts
{
    /// <summary>外壳（模板里的名字，TS 的 propsRoot）。</summary>
    public const string Root = TextInputParts.Root;

    /// <summary>清空按钮（模板里的名字，TS 的 propsClear）。</summary>
    public const string Clear = TextInputParts.Clear;

    /// <summary>外壳里排标签与输入框、会换行的那一行（模板里的名字）。</summary>
    public const string Flow = "PART_Flow";

    /// <summary>输入框的文字区（模板里的名字；至少 6em，放不下时换到下一行）。</summary>
    public const string Input = "PART_Input";

    /// <summary>每个标签（TS 的 propsTag）。</summary>
    public static readonly PartKey Tag = new(typeof(TagsInput), nameof(Tag), typeof(Tag));

    /// <summary>弹出层面板（TS 的 propsPopover）。</summary>
    public static readonly PartKey Popover = new(typeof(TagsInput), nameof(Popover), typeof(Border));

    /// <summary>弹出层的阴影层。</summary>
    public static readonly PartKey PopoverShadow = new(typeof(TagsInput), nameof(PopoverShadow), typeof(Border));
}
