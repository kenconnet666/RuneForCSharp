using System.Collections;
using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 标签输入（vue-tsx-pro 的 TagsInput，多选的组合框）：输入框里排着已选的 <see cref="TagControl"/>，输入文字后回车（或输入逗号）加一个，可以从建议列表里选。
/// 关键词、收件人、标签、多个城市。
/// <code>
/// var keywords = new TagsInputControl { Values = ["组件库", "Vue"], Max = 6, AccessibleName = "关键词" };
/// var cities = new TagsInputControl { ItemsSource = cityOptions, AllowCustomValue = false };
/// keywords.ValuesChanged += (_, _) =&gt; Save(keywords.Values);
/// </code>
/// 外壳同 TextFieldControl（Variant、TextSize、Radius、Icon、出错、只读、不可用），里面是平台的 TextBox；<see cref="Control.Text"/> 是还没确认的文字（上游的 inputValue）。
/// 回车、输入分隔符（<see cref="Delimiters"/>，默认 , ， ; ；）把文字加成标签；粘贴时按分隔符与换行拆成多个；重复的、超出 <see cref="Max"/> 的不加。
/// 输入框为空时按退格删掉最后一个标签；每个标签有移除按钮（在 Tab 顺序里）；失焦时没确认的文字默认加成标签（<see cref="AcceptOnBlur"/>）。
/// 有 <see cref="ItemsSource"/> 时边输入边过滤建议，↓ 打开，↑↓ 移动，回车加入或移除高亮项，Esc 关闭；允许自定义值时不自动高亮，只能从建议里选时高亮第一条。
/// 异步取建议时关掉 <see cref="LocalFilter"/>、按 TextChanged 去请求、写 <see cref="IsLoading"/>。标签多了换行；只读、不可用时标签没有移除按钮。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValuesChanged))]
[Description("标签输入：回车或逗号加一个标签，可以带建议列表。")]
public class TagsInputControl : TextInputControl
{
    /// <summary>与外壳的距离（设备无关像素）。</summary>
    const float Offset = 6;

    readonly IconControl icon = new() { Tone = TextTone.Muted, Visible = false };
    readonly FloatingSurface surface;
    readonly ListboxControl listbox = new() { Embedded = true, Dock = DockStyle.Fill, SelectionMode = SelectionMode.Multiple };
    readonly Dictionary<string, TagControl> tags = [];
    readonly Dictionary<TagControl, EventHandler> removers = [];
    readonly Dictionary<string, string> seen = [];
    IReadOnlyList<object?> all = [];
    bool syncing;
    bool open;
    bool presenting;

    /// <summary>新建：没有标签、没有建议。</summary>
    public TagsInputControl() : base(new TagsTextBox())
    {
        var box = (TagsTextBox)Input;
        box.Host = this;
        Wrap = FlexWrap.Wrap;
        ColumnGap = Space.Sm;
        RowGap = Space.Xs;
        Controls.AddRange([icon, Input, ClearButton]);
        SetGrow(Input, true);
        surface = new FloatingSurface(this);
        surface.Form.Controls.Add(listbox);
        surface.Outside += (_, _) =>
        {
            if (!presenting) SetOpen(false, OpenChangeReason.Outside);
        };
        surface.Form.Escape += (_, _) => SetOpen(false, OpenChangeReason.Escape);
        listbox.ValuesChanged += (_, _) => OnListChanged();
        listbox.Picked += (_, _) => Text = "";
        Input.TextChanged += (_, _) => OnInputChanged();
        Input.LostFocus += (_, _) => OnInputLostFocus();
    }

    /// <summary>已选的值。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Values
    {
        get;
        set
        {
            if (field.SequenceEqual(value)) return;
            field = value;
            Sync();
            ValuesChanged?.Invoke(this, EventArgs.Empty);
        }
    } = [];

    /// <summary>建议：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>；不给时是纯自由输入。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable? ItemsSource { get; set { field = value; OnItemsChanged(); } }

    /// <summary>数据 → 选项。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, ListboxOption>? ToOption { get; set { field = value; OnItemsChanged(); } }

    /// <summary>允许加入不在建议里的文字，默认 true。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool AllowCustomValue { get; set; } = true;

    /// <summary>失焦时把还没确认的文字加成标签，默认 true；false 时文字留在框里。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool AcceptOnBlur { get; set; } = true;

    /// <summary>把输入拆成标签的分隔符；换行与制表符总是分隔。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Delimiters { get; set; } = TagsText.DefaultDelimiters;

    /// <summary>最多几个标签；到了上限不再加入。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public int? Max { get; set; }

    /// <summary>怎样算匹配（数据、输入的文字）；不写时显示文字包含输入（忽略大小写与重音）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, string, bool>? Filter { get; set { field = value; UpdateItems(); } }

    /// <summary>按输入在本地过滤建议，默认 true；异步取建议（TS 的 filter={false}）时关掉，这时没有建议也是组合框。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool LocalFilter { get; set { field = value; OnItemsChanged(); } } = true;

    /// <summary>正在取建议：列表显示加载中。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsLoading { get; set { field = value; listbox.IsLoading = value; if (open) Present(); } }

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

    /// <summary>建议列表开着。</summary>
    [Browsable(false)]
    public bool IsOpen => open;

    /// <summary>已选的值变了。</summary>
    public event EventHandler? ValuesChanged;

    /// <summary>用户开、关了建议列表（输入、↓、选中、点外面、Esc、失焦）。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>建议列表（测试用）。</summary>
    internal ListboxControl List => listbox;

    /// <summary>显示出来的标签（测试用，按值的顺序）。</summary>
    internal IEnumerable<TagControl> Tags => Values.Where(tags.ContainsKey).Select(value => tags[value]);

    /// <summary>有建议列表：给了建议，或异步取建议（还没回来也算）。</summary>
    internal bool Suggests => all.Count > 0 || !LocalFilter;

    bool Editable => Enabled && !ReadOnly;

    void OnItemsChanged()
    {
        all = ItemsSource?.Cast<object?>().ToList() ?? [];
        UpdateItems();
        Sync();
        if (open) Present();
    }

    /// <summary>值 → 标签（按值复用，移除按钮上的焦点不丢）、列表的已选、清空按钮。</summary>
    protected override void Sync()
    {
        base.Sync();
        ClearButton.Visible = Clearable && Editable && Values.Count > 0;
        // 构造时基类先同步一次：这时字段还没初始化
        if (tags is null) return;
        syncing = true;
        listbox.Values = Values;
        syncing = false;
        SuspendLayout();
        foreach (var gone in tags.Keys.Except(Values).ToList())
        {
            var tag = tags[gone];
            tags.Remove(gone);
            removers.Remove(tag);
            Controls.Remove(tag);
            tag.Dispose();
        }
        foreach (var value in Values)
        {
            if (!tags.TryGetValue(value, out var tag))
            {
                tag = new TagControl { Variant = StatusVariant.Surface };
                removers[tag] = (_, _) => Remove(value);
                tags[value] = tag;
                Controls.Add(tag);
            }
            tag.Text = LabelOf(value);
            // 只读、不可用时没有移除按钮（TagControl 订阅了 RemoveRequested 才有）
            tag.RemoveRequested -= removers[tag];
            if (Editable) tag.RemoveRequested += removers[tag];
        }
        // 排列顺序：图标、标签（按值的顺序）、输入框、清空按钮
        Control[] order = [icon, .. Values.Select(value => tags[value]), Input, ClearButton];
        for (var index = 0; index < order.Length; index++) Controls.SetChildIndex(order[index], index);
        // 输入框至少留出 6em，放不下时换到下一行
        Input.MinimumSize = new Size((int)MathF.Round(6 * Em), 0);
        ResumeLayout();
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
        var query = Text.Trim();
        listbox.ItemsSource = !LocalFilter || query.Length == 0
            ? all
            : [.. all.Where(item => Filter?.Invoke(item, query) ?? SelectSearch.Matches(ListboxOption.From(item, ToOption).Label, query))];
        listbox.Values = Values;
    }

    /// <summary>输入了分隔符：前面的部分加成标签，剩下的留在框里；有建议时打开并按需高亮第一条。</summary>
    void OnInputChanged()
    {
        var text = Text;
        var parts = TagsText.Split(text, Delimiters);
        if (parts.Length > 1)
        {
            Add(parts[..^1]);
            Text = parts[^1];
            Input.SelectionStart = parts[^1].Length;
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
        var next = TagsText.Add(Values, texts.Select(text => TagsText.Resolve(text, options, AllowCustomValue)), Max);
        if (next is null) return false;
        Values = next;
        return true;
    }

    void Remove(string value) => Values = [.. Values.Where(item => item != value)];

    /// <summary>列表的选择 → 值；从建议里选的也受上限限制（取消选中照常）。</summary>
    void OnListChanged()
    {
        if (syncing) return;
        var next = listbox.Values;
        if (TagsText.Accepts(Values, next, Max))
        {
            Values = next;
            return;
        }
        // 拒绝了：列表退回原来的已选
        syncing = true;
        listbox.Values = [.. Values];
        syncing = false;
    }

    /// <summary>输入框要自己处理的键（不当作对话框键：回车不触发窗体的默认按钮，开着时 Esc 不触发取消按钮）。</summary>
    internal bool WantsKey(Keys key) => (key == Keys.Enter && Text.Trim().Length > 0) || (key == Keys.Escape && open);

    /// <summary>键盘（见类的说明）；输入法组合中的键是 ProcessKey，不处理。</summary>
    internal bool Press(Keys key)
    {
        if (!Editable) return false;
        var text = Text;
        if (key == Keys.Back && text.Length == 0 && Values is [.., var last])
        {
            Remove(last);
            return true;
        }
        if (open && Suggests)
        {
            if (key == Keys.Escape)
            {
                SetOpen(false, OpenChangeReason.Escape);
                return true;
            }
            if (ListboxControl.ToListKey(key) is { } listKey and not (ListKey.Home or ListKey.End or ListKey.Space) && listbox.State.Handle(listKey, space: false))
            {
                if (key == Keys.Enter) Text = "";
                return true;
            }
        }
        else if (key == Keys.Down && Suggests)
        {
            listbox.State.ActivateDefault();
            SetOpen(true, OpenChangeReason.Trigger);
            return true;
        }
        if (key != Keys.Enter || text.Trim().Length == 0) return false;
        if (Add([text])) Text = "";
        return true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Press(e.KeyData)) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    /// <summary>粘贴一段文字：多段（有分隔符、换行）时拆开加入并返回 true（拦下平台的粘贴），否则交给平台。</summary>
    internal bool PasteText(string text)
    {
        var parts = TagsText.Split(text, Delimiters);
        if (parts.Length < 2) return false;
        Add(parts);
        return true;
    }

    /// <summary>失焦（焦点离开输入框，包括移到标签的移除按钮上）：关上建议，按需把没确认的文字加成标签。</summary>
    void OnInputLostFocus()
    {
        if (presenting) return;
        if (open) SetOpen(false, OpenChangeReason.Focus);
        if (AcceptOnBlur && Text.Trim().Length > 0 && !ReadOnly && Add([Text])) Text = "";
    }

    /// <inheritdoc />
    protected override void ClearText()
    {
        Values = [];
        Input.Focus();
    }

    void SetOpen(bool next, OpenChangeReason reason)
    {
        if (next == open) return;
        open = next;
        if (next) Present();
        else surface.Hide();
        OpenChanged?.Invoke(this, new OpenChangedEventArgs(next, reason));
    }

    /// <summary>面板 = 列表 + 描边，与外壳同宽；放在外壳下方（放不下时上方），隔 6。不抢焦点：焦点留在输入框里。</summary>
    void Present()
    {
        if (!IsHandleCreated || !Suggests) return;
        var context = ElementContext.Of(this);
        var border = FloatingSurface.Border(context);
        var listHeight = listbox.GetPreferredSize(new Size(Width - 2 * border, 0)).Height;
        var panelSize = new Size(Width, listHeight + 2 * border);
        var target = RectangleToScreen(ClientRectangle);
        var (_, panel) = FloatingLayout.Position(FloatingPlacement.BottomStart, context.RightToLeft, target, panelSize, context.Scale(Offset), Screen.FromControl(this).WorkingArea);
        surface.Form.AccessibleName = AccessibleName;
        // 显示浮层窗口时平台可能把焦点从输入框拿走一下、让宿主窗体失活：都不算失焦或点了外面，显示完把焦点还给输入框
        var focused = Input.Focused;
        presenting = true;
        try
        {
            surface.Show(panel, border, context, null);
            if (focused && !Input.Focused) Input.Focus();
        }
        finally
        {
            presenting = false;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) surface.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new TagsInputAccessibleObject(this);

    /// <summary>读屏：有建议时是组合框（展开、收起），否则是编辑框；名称、说明同输入框。</summary>
    sealed class TagsInputAccessibleObject(TagsInputControl owner) : FieldAccessibleObject(owner)
    {
        public override AccessibleRole Role => owner.Suggests ? AccessibleRole.ComboBox : base.Role;

        public override AccessibleStates State =>
            base.State | (!owner.Suggests ? AccessibleStates.None : owner.open ? AccessibleStates.Expanded : AccessibleStates.Collapsed);
    }

    /// <summary>真正的输入框：拦下粘贴（多段时拆成标签），回车与开着时的 Esc 当作输入键。</summary>
    sealed class TagsTextBox : TextBox
    {
        const int WmPaste = 0x0302;

        // 字段不是属性：设计器不序列化它
        public TagsInputControl? Host;

        protected override bool IsInputKey(Keys keyData) => Host?.WantsKey(keyData) == true || base.IsInputKey(keyData);

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmPaste && Host is { } owner && Clipboard.ContainsText() && owner.PasteText(Clipboard.GetText())) return;
            base.WndProc(ref m);
        }
    }
}
