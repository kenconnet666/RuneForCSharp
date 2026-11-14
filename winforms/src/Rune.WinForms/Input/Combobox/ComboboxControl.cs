using System.Collections;
using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 组合框（vue-tsx-pro 的 Combobox，WAI-ARIA 的 combobox）：边输入边从列表里选，或者只当带建议的输入框用。
/// <code>
/// var city = new ComboboxControl { ItemsSource = cities, Clearable = true, AccessibleName = "城市" };
/// city.ValueChanged += (_, _) =&gt; Save(city.Value);
/// </code>
/// 外壳同 TextFieldControl，里面是平台的 TextBox：<see cref="Control.Text"/> 是输入框里的文字（上游的 inputValue），<see cref="Value"/> 是选中项的值。
/// 选项在输入框下面的浮层里（与外壳同宽），焦点一直在输入框里。过滤：默认按显示文字包含输入（忽略大小写与重音），<see cref="Filter"/> 自定义；
/// <see cref="LocalFilter"/> 关掉时不过滤（调用方按 TextChanged 换 ItemsSource，配合 <see cref="IsLoading"/>）；给 <see cref="Load"/> 时组件自己请求
/// （打开时与输入停下 300 毫秒后按输入请求，只采用最后一次，可以分页，同 SelectControl）。
/// 选中后输入框显示选项文字；失焦时文字不是选项（又没开 <see cref="AllowCustomValue"/>）就恢复成已选项的文字，清空则取消选择；
/// 开着 AllowCustomValue 时文字保留、Value 为 null。键盘：↓ / ↑ 打开，↑↓、Page Up / Down 移动，Enter 选中，Esc 关闭（关着时再按 Esc 清空）。
/// <see cref="ShowToggle"/> 控制右侧的展开按钮（不进 Tab 顺序），<see cref="HideEmpty"/> 时没有可选的项不弹出列表。WinForms 的选项是画出来的，没有自定义内容。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[Description("组合框：边输入边从列表里选。")]
public class ComboboxControl : TextInputControl
{
    readonly IconControl icon = new() { Tone = TextTone.Muted, Visible = false };
    readonly IconButtonControl toggle = new() { Icon = Lucide.ChevronDown, Radius = Radius.Full, TabStop = false };
    readonly ListPopover popover;
    readonly ListboxControl listbox;
    readonly RemoteOptions remote;
    readonly Dictionary<string, string> seen = [];
    IReadOnlyList<object?> all = [];
    // 用户输入的过滤词：选中、用箭头打开时清空（显示全部选项）
    string query = "";
    // 组件自己写 Text（选中后写入选项文字）时，不当作用户输入
    bool writing;
    bool open;

    /// <summary>新建：没有选项、没有选中。</summary>
    public ComboboxControl() : base(new ComboTextBox())
    {
        ((ComboTextBox)Input).Host = this;
        Controls.AddRange([icon, Input, ClearButton, toggle]);
        SetGrow(Input, true);
        remote = new RemoteOptions(item => ListboxOption.From(item, ToOption).Value, OnRemoteChanged);
        popover = new ListPopover(this, Input, () => SetOpen(false, OpenChangeReason.Outside), () => SetOpen(false, OpenChangeReason.Escape));
        listbox = popover.Listbox;
        listbox.IsRequired = true;
        listbox.Reached += last => remote.Reached(last);
        listbox.Picked += (_, e) => Pick(e.Value);
        toggle.Click += (_, _) =>
        {
            if (open) SetOpen(false, OpenChangeReason.Trigger);
            else ShowAll();
            Input.Focus();
        };
        Input.TextChanged += (_, _) => OnInputChanged();
        Input.LostFocus += (_, _) => OnInputLostFocus();
    }

    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>；有 Load 时是第一次加载之前的选项。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable? ItemsSource { get; set { field = value; OnItemsChanged(); } }

    /// <summary>数据 → 选项。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, ListboxOption>? ToOption { get; set { field = value; OnItemsChanged(); } }

    /// <summary>选中项的值；没有选中时是 null。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            listbox.Value = value;
            ShowSelectedLabel();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>怎样算匹配（数据、输入的文字）；不写时显示文字包含输入（忽略大小写与重音）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, string, bool>? Filter { get; set { field = value; UpdateItems(); } }

    /// <summary>按输入在本地过滤选项，默认 true；调用方自己按 TextChanged 取数据时（TS 的 filter={false}）关掉。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool LocalFilter { get; set { field = value; UpdateItems(); } } = true;

    /// <summary>远程选项的加载函数（TS 的 load）：输入、第几页 → 一页选项；结果代替 ItemsSource，不在本地过滤。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public OptionsLoader? Load
    {
        get => remote.Load;
        set
        {
            remote.Load = value;
            OnRemoteChanged();
        }
    }

    /// <summary>允许输入不在选项里的文字：失焦时保留，Value 为 null。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool AllowCustomValue { get; set; }

    /// <summary>显示右侧的展开按钮，默认 true；只当带建议的输入框用时（AutocompleteControl）关掉。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool ShowToggle { get; set { field = value; Sync(); } } = true;

    /// <summary>没有可选的项（也不在加载）时不弹出列表、不显示"没有匹配的选项"（AutocompleteControl）。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool HideEmpty { get; set { field = value; Present(); } }

    /// <summary>正在取选项：列表换成骨架行。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsLoading
    {
        get;
        set
        {
            field = value;
            listbox.IsLoading = value || remote.Loading;
            Present();
        }
    }

    /// <summary>前置图标。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public IconData? Icon
    {
        get => icon.Data;
        set
        {
            icon.Data = value;
            icon.Visible = value is not null;
        }
    }

    /// <summary>没有匹配的选项时列表里的说明（上游的 empty；WinForms 的空状态是固定的一种，只换说明文字）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? EmptyText { get; set { field = value; UpdateEmpty(); } }

    /// <summary>列表开着。</summary>
    [Browsable(false)]
    public bool IsOpen => open;

    /// <summary>选中项变了。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>用户开、关了列表（输入、↓、展开按钮、选中、点外面、Esc、失焦）。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>列表（测试用）。</summary>
    internal ListboxControl List => listbox;

    /// <summary>展开按钮（测试用）。</summary>
    internal IconButtonControl Toggle => toggle;

    /// <summary>浮层（测试用）。</summary>
    internal FloatingSurface Surface => popover.Surface;

    /// <summary>列表真的弹出来了：开着，且有可选的项、在加载或不隐藏空列表。</summary>
    internal bool Shown => open && (!HideEmpty || listbox.State.Options.Count > 0 || IsLoading || remote.Loading);

    bool Editable => Enabled && !ReadOnly;

    /// <summary>当前的数据：远程请求到的，没有时是 ItemsSource。</summary>
    IReadOnlyList<object?> Source => remote.ItemsOr(all);

    /// <summary>展开按钮看 ShowToggle，只读、不可用时不能按；尺寸同清空按钮。</summary>
    protected override void Sync()
    {
        base.Sync();
        // 构造时基类先同步一次：这时字段还没初始化
        if (toggle is null) return;
        ControlShell.FitInnerButton(toggle, Em);
        toggle.Visible = ShowToggle;
        toggle.Enabled = Editable;
        toggle.AccessibleName = ProviderControl.LangOf(this).ComboboxToggle;
        toggle.Icon = Shown ? Lucide.ChevronUp : Lucide.ChevronDown;
    }

    void OnItemsChanged()
    {
        all = ItemsSource?.Cast<object?>().ToList() ?? [];
        UpdateItems();
        ShowSelectedLabel();
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

    /// <summary>父级改了 Value，或选项后到：输入框显示它的文字；用户正在输入时不打断。</summary>
    void ShowSelectedLabel()
    {
        if (query.Length > 0) return;
        if (!AllowCustomValue || Value is not null) SetText(SelectedLabel);
    }

    void SetText(string text)
    {
        if (Text == text) return;
        writing = true;
        Text = text;
        writing = false;
    }

    /// <summary>列表里的选项：本地按输入过滤；远程选项、关掉本地过滤时原样。</summary>
    void UpdateItems()
    {
        // 构造时属性初始化器先跑：这时列表还没建
        if (listbox is null) return;
        listbox.ToOption = ToOption;
        listbox.ItemsSource = Load is not null || !LocalFilter || query.Length == 0
            ? Source
            : [.. Source.Where(item => Filter?.Invoke(item, query) ?? SelectSearch.Matches(ListboxOption.From(item, ToOption).Label, query))];
        listbox.Value = Value;
        UpdateEmpty();
        Present();
    }

    /// <summary>没有选项时：加载失败是"加载失败，请重试"，否则是 EmptyText（不写时列表自己的）。</summary>
    void UpdateEmpty()
    {
        if (listbox is null) return;
        listbox.EmptyText = remote.Failed ? ProviderControl.LangOf(this).ListLoadFailed : EmptyText;
    }

    void OnRemoteChanged()
    {
        listbox.IsLoading = IsLoading || remote.Loading;
        listbox.IsLoadingMore = remote.LoadingMore;
        UpdateItems();
        ShowSelectedLabel();
    }

    /// <summary>用户输入：按输入过滤并高亮第一项；输入框聚焦时打开（父级把文字改掉时不弹出）。</summary>
    void OnInputChanged()
    {
        if (writing) return;
        query = Text;
        remote.SetQuery(query);
        UpdateItems();
        if (!ReadOnly && Input.Focused) SetOpen(true, OpenChangeReason.Trigger);
        listbox.State.MoveToEnd(last: false);
    }

    /// <summary>选中一项：值是它，输入框显示它的文字，关上。</summary>
    internal void Pick(string choice)
    {
        query = "";
        remote.SetQuery("");
        Value = choice;
        SetText(SelectedLabel);
        SetOpen(false, OpenChangeReason.Select);
    }

    /// <summary>打开：显示全部选项，高亮已选的（没有时第一项）。</summary>
    void ShowAll()
    {
        query = "";
        remote.SetQuery("");
        UpdateItems();
        listbox.State.ActivateDefault();
        SetOpen(true, OpenChangeReason.Trigger);
    }

    /// <summary>失焦：关上；文字不是选项时恢复成已选项的文字（清空则取消选择）；允许自定义时文字保留、Value 为 null。</summary>
    void OnInputLostFocus()
    {
        if (popover.Presenting) return;
        if (open) SetOpen(false, OpenChangeReason.Focus);
        if (AllowCustomValue)
        {
            if (Text != SelectedLabel) Value = null;
            query = "";
            return;
        }
        if (Text.Length == 0) Value = null;
        else SetText(SelectedLabel);
        query = "";
    }

    /// <summary>输入框要自己处理的键（不当作对话框键：开着时回车不触发窗体的默认按钮，Esc 不触发取消按钮）。</summary>
    internal bool WantsKey(Keys key) => key switch
    {
        Keys.Enter or Keys.PageUp or Keys.PageDown => open,
        Keys.Escape => open || Text.Length > 0,
        _ => false,
    };

    /// <summary>键盘（见类的说明）；输入法组合中的键是 ProcessKey，不处理。</summary>
    internal bool Press(Keys key)
    {
        if (!Editable) return false;
        if (key == Keys.Escape)
        {
            if (open)
            {
                SetOpen(false, OpenChangeReason.Escape);
                return true;
            }
            if (Text.Length == 0) return false;
            SetText("");
            query = "";
            Value = null;
            return true;
        }
        if (!open)
        {
            if (key is not (Keys.Down or Keys.Up)) return false;
            ShowAll();
            return true;
        }
        return ListboxControl.ToListKey(key) is { } listKey and not (ListKey.Home or ListKey.End or ListKey.Space)
               && listbox.State.Handle(listKey, space: false, picked: Pick);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Press(e.KeyData)) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    /// <inheritdoc />
    protected override void ClearText()
    {
        SetText("");
        query = "";
        Value = null;
        Input.Focus();
    }

    void SetOpen(bool next, OpenChangeReason reason)
    {
        if (next == open) return;
        open = next;
        remote.SetOpen(next);
        Present();
        OpenChanged?.Invoke(this, new OpenChangedEventArgs(next, reason));
    }

    /// <summary>弹出或藏起浮层（Shown 变了）；展开按钮的箭头跟着。</summary>
    void Present()
    {
        if (popover is null) return;
        if (Shown) popover.Show();
        else popover.Hide();
        toggle.Icon = Shown ? Lucide.ChevronUp : Lucide.ChevronDown;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) popover.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ComboboxAccessibleObject(this);

    /// <summary>读屏：组合框（展开、收起），值是输入框里的文字；名称、说明同输入框。</summary>
    sealed class ComboboxAccessibleObject(ComboboxControl owner) : FieldAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.ComboBox;

        public override AccessibleStates State => base.State | (owner.Shown ? AccessibleStates.Expanded : AccessibleStates.Collapsed);
    }

    /// <summary>真正的输入框：开着时的回车、翻页与要清空时的 Esc 当作输入键。</summary>
    sealed class ComboTextBox : TextBox
    {
        // 字段不是属性：设计器不序列化它
        public ComboboxControl? Host;

        protected override bool IsInputKey(Keys keyData) => Host?.WantsKey(keyData) == true || base.IsInputKey(keyData);
    }
}

/// <summary>
/// 自动完成（vue-tsx-pro 的 Autocomplete）：值就是输入框里的文字（Text），列表只是建议（邮箱后缀、常用地址、搜索历史、供应商名称）。
/// 选一项等于把它的文字填进来；不选也行，输入什么就是什么。要从固定的选项里选一个（值是选项的 value）时用 <see cref="ComboboxControl"/>。
/// 建在 ComboboxControl 上：允许自定义值、没有展开按钮、没有可选的建议时不弹出列表。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(TextChanged))]
[Description("自动完成：带建议的输入框，值就是文字。")]
public class AutocompleteControl : ComboboxControl
{
    /// <summary>新建：允许自定义值、没有展开按钮、空列表不弹出。</summary>
    public AutocompleteControl()
    {
        AllowCustomValue = true;
        ShowToggle = false;
        HideEmpty = true;
    }
}
