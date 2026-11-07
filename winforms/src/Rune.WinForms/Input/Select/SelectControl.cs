using System.Collections;
using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 下拉选择（vue-tsx-pro 的 Select，WAI-ARIA 的 select-only combobox）：点触发框弹出选项列表，单选或多选。
/// <code>
/// var city = new SelectControl { ItemsSource = cities, Value = "北京", PlaceholderText = "选择城市" };
/// var tags = new SelectControl { ItemsSource = labels, SelectionMode = SelectionMode.Multiple, Values = ["前端"], Clearable = true, MaxTagCount = TagLimit.Responsive };
/// </code>
/// 外壳同输入框（<see cref="ControlShell"/>：<see cref="Variant"/>、<see cref="TextSize"/>、<see cref="Radius"/>、出错、只读、不可用，前置 <see cref="Icon"/>），
/// 触发框就是控件自己（焦点一直在它上面）。弹出层是两层窗口（<see cref="FloatingSurface"/>）：贴着外壳下方（放不下时翻到上方）、与它同宽，
/// 里面是不可聚焦的 <see cref="ListboxControl"/>。多选时已选的项是外壳里一排可移除的 <see cref="SelectedTagsControl"/>（<see cref="MaxTagCount"/> 收起），
/// 读屏从下拉自己念出全部已选。键盘：关闭时 ↓ / Enter / 空格打开（↑ 打开并高亮最后一项），单选时按字母直接选中；打开时 ↑↓、Home / End、
/// Page Up / Down 移动，Enter / 空格选中（单选随后关闭），Esc、Tab 关闭；多选时 Backspace 移除最后一项。<see cref="Searchable"/> 时弹出层顶部有搜索框，
/// 打开后焦点在里面（弹出层激活，宿主窗体的标题栏变成非激活），输入就过滤（按显示文字，忽略大小写与重音，或 <see cref="Filter"/>）；
/// <see cref="Creatable"/> 时自带搜索框，输入的文字没有对应的选项时末尾多一项"新建"，选中后值就是这段文字，<see cref="OptionCreated"/> 报告它。
/// 单选的值是空字符串时算没有选中。出入：没有自定义选项内容（选项是画出来的）；宿主窗口移动时关闭。
/// </summary>
[DefaultEvent(nameof(ValueChanged))]
public class SelectControl : FlexControl, IFieldControl
{
    /// <summary>与外壳的距离（设备无关像素）。</summary>
    const float Offset = 6;

    readonly IconControl icon = new() { Tone = TextTone.Muted, Visible = false };
    readonly SelectedTagsControl tags = new() { Visible = false };
    readonly TextControl valueText = new() { Truncate = true };
    readonly ClearButtonControl clear = new() { Radius = Radius.Full, Visible = false, TabStop = false };
    readonly IconControl chevron = new() { Tone = TextTone.Muted, Data = Lucide.ChevronDown };
    readonly FloatingSurface surface;
    readonly HeaderPanel header = new() { Dock = DockStyle.Top, Visible = false };
    readonly TextFieldControl search = new() { Icon = Lucide.Search, Variant = ControlVariant.Soft, Dock = DockStyle.Fill };
    readonly ListboxControl listbox = new() { Embedded = true, Dock = DockStyle.Fill };
    IReadOnlyList<object?> all = [];
    Dictionary<string, string>? labels;
    bool hovered;
    bool syncing;
    bool open;
    long closedAt = long.MinValue / 2;

    /// <summary>新建：外壳、部件与弹出层（搜索框、列表）。</summary>
    public SelectControl()
    {
        Align = FlexAlign.Center;
        Gap = Space.Sm;
        AutoSize = true;
        Cursor = Cursors.Hand;
        TabStop = true;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Controls.AddRange([icon, tags, valueText, clear, chevron]);
        SetGrow(tags, true);
        SetGrow(valueText, true);
        surface = new FloatingSurface(this);
        header.Controls.Add(search);
        surface.Form.Controls.AddRange([listbox, header]);
        surface.Outside += (_, _) => SetOpen(false, OpenChangeReason.Outside);
        surface.Form.Escape += (_, _) => CloseAndFocus(OpenChangeReason.Escape);
        listbox.ValueChanged += (_, _) => OnListChanged();
        listbox.ValuesChanged += (_, _) => OnListChanged();
        listbox.Picked += (_, _) => Picked();
        search.TextChanged += (_, _) => OnSearchChanged();
        search.Input.KeyDown += OnSearchKeyDown;
        search.Input.PreviewKeyDown += (_, e) => e.IsInputKey |= e.KeyCode is Keys.Up or Keys.Down or Keys.Tab;
        clear.Click += (_, _) =>
        {
            SetSelected([]);
            Focus();
        };
        tags.RemoveRequested += OnTagRemoved;
        foreach (Control child in Controls)
        {
            child.MouseEnter += (_, _) => SetHovered(true);
            child.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
            if (child != clear && child != tags) child.MouseDown += (_, e) => OnMouseDown(e);
        }
        tags.MouseDown += (_, e) => OnMouseDown(e);
        Sync();
    }

    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable? ItemsSource { get; set { field = value; OnItemsChanged(); } }

    /// <summary>数据 → 选项。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, ListboxOption>? ToOption { get; set { field = value; OnItemsChanged(); } }

    /// <summary>单选或多选，默认单选。</summary>
    [Category("Behavior")]
    public SelectionMode SelectionMode { get => field.Value is null ? SelectionMode.Single : field; set { field = value; Sync(); } }

    /// <summary>单选的值；null 或空字符串是没有选中。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>多选的值。</summary>
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

    /// <summary>开着。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => open; set => SetOpen(value, null); }

    /// <summary>没有选择时显示的文字；空时取语言包（请选择）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? PlaceholderText { get; set { field = value; Sync(); } }

    /// <summary>有选择时显示清空按钮。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Clearable { get; set { field = value; Sync(); } }

    /// <summary>弹出层顶部带搜索框。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Searchable { get; set { field = value; OnItemsChanged(); } }

    /// <summary>搜索时怎样算匹配（数据、搜索词）；不写时显示文字包含搜索词（忽略大小写与重音）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, string, bool>? Filter { get; set { field = value; OnItemsChanged(); } }

    /// <summary>可以新建选项（自带搜索框）。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Creatable { get; set { field = value; OnItemsChanged(); } }

    /// <summary>多选时最多显示几个标签；不写时全部显示。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TagLimit? MaxTagCount { get; set { field = value; Sync(); } }

    /// <summary>样式，默认 surface。</summary>
    [Category("Appearance")]
    public ControlVariant Variant { get => field.Value is null ? ControlVariant.Surface : field; set { field = value; Sync(); } }

    /// <summary>字号档位；不写时跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get;
        set
        {
            field = value;
            if (value.Pixels is { } pixels) Font = Fonts.Get(Font.FontFamily.Name, pixels * 72 / 96, Font.Style);
            else ResetFont();
        }
    }

    /// <summary>圆角，默认 md。</summary>
    [Category("Appearance")]
    public Radius Radius { get => field.Value is null ? Radius.Md : field; set { field = value; Invalidate(); } }

    /// <summary>前置图标。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public IconData? Icon { get => icon.Data; set { icon.Data = value; icon.Visible = value is not null; } }

    /// <summary>校验未通过；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsInvalid { get; set { field = value; Sync(); } }

    /// <summary>必填；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsRequired { get; set; }

    /// <summary>只读：能聚焦、能看到已选的内容，不能打开、不能清空；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsReadOnly { get; set { field = value; Sync(); } }

    /// <summary>正在取选项：列表换成骨架行。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsLoading { get => listbox.IsLoading; set => listbox.IsLoading = value; }

    bool ShouldSerializeSelectionMode() => SelectionMode != SelectionMode.Single;

    bool ShouldSerializeVariant() => Variant != ControlVariant.Surface;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    bool ShouldSerializeRadius() => Radius != Radius.Md;

    /// <summary>单选的值变了。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>多选的值变了。</summary>
    public event EventHandler? ValuesChanged;

    /// <summary>用户开、关了（触发框、选中、点外面、Esc、Tab）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>新建了一项（<see cref="Creatable"/> 时）：参数是输入的文字，也就是选中的值。</summary>
    public event EventHandler<SelectCreatedEventArgs>? OptionCreated;

    /// <summary>弹出层里的列表（测试用）。</summary>
    internal ListboxControl List => listbox;

    /// <summary>弹出层顶部的搜索框（测试用）。</summary>
    internal TextFieldControl SearchBox => search;

    /// <summary>搜索框那一行（测试用）。</summary>
    internal Control Header => header;

    /// <summary>那一排已选标签（测试用）。</summary>
    internal SelectedTagsControl TagRow => tags;

    /// <summary>已选的文字或占位文字（测试用）。</summary>
    internal TextControl ValuePart => valueText;

    /// <summary>清空按钮（测试用）。</summary>
    internal ClearButtonControl ClearPart => clear;

    /// <summary>前置图标（测试用）。</summary>
    internal IconControl IconPart => icon;

    /// <summary>弹出层（测试用）。</summary>
    internal FloatingSurface Surface => surface;

    bool Multiple => SelectionMode == SelectionMode.Multiple;

    bool Invalid => IsInvalid ?? FieldControl.Of(this)?.Invalid ?? false;

    bool ReadOnly => IsReadOnly ?? FieldControl.Of(this)?.IsReadOnly ?? false;

    /// <summary>不能打开、不能改：不可用或只读。</summary>
    bool Locked => !Enabled || ReadOnly;

    bool WithSearch => Searchable || Creatable;

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    /// <summary>已选的值（单选的空字符串算没有选中）。</summary>
    IReadOnlyList<string> Selected => Multiple ? Values : string.IsNullOrEmpty(Value) ? [] : [Value];

    /// <summary>已选的文字（读屏念的全部已选）。</summary>
    internal string Summary => ProviderControl.LangOf(this).SelectSummary([.. Selected.Select(LabelOf)]);

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        OnItemsChanged();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Sync();
    }

    /// <summary>数据换了：句柄建好以后才交给列表（初始化器里 ItemsSource 可能先于 ToOption 设，列表一拿到就建选项）。</summary>
    void OnItemsChanged()
    {
        all = ItemsSource?.Cast<object?>().ToList() ?? [];
        labels = null;
        if (!IsHandleCreated) return;
        header.Visible = WithSearch;
        surface.Form.Activates = WithSearch;
        UpdateItems();
        Sync();
    }

    /// <summary>选项名称：数据 → 选项（"新建"项是"新建 …"加一个加号）。</summary>
    ListboxOption OptionOf(object? item) => item is SelectCreation creation
        ? new ListboxOption(creation.Text, ProviderControl.LangOf(this).SelectCreate(creation.Text)) { Icon = Lucide.Plus }
        : ListboxOption.From(item, ToOption);

    /// <summary>列表里的数据：按搜索词过滤，creatable 时可能多一项"新建"。</summary>
    void UpdateItems()
    {
        listbox.ToOption = OptionOf;
        listbox.ItemsSource = WithSearch ? SelectSearch.Shown(all, item => ListboxOption.From(item, ToOption), search.Text, localFilter: true, Filter, Creatable) : all;
        listbox.EmptyText = search.Text.Length > 0 ? ProviderControl.LangOf(this).SelectNoMatch : null;
    }

    /// <summary>值 → 列表、触发框（文字或标签）、清空按钮、外壳尺寸。</summary>
    void Sync()
    {
        var selected = Selected;
        syncing = true;
        listbox.SelectionMode = SelectionMode;
        listbox.IsRequired = !Multiple;
        if (Multiple) listbox.Values = selected;
        else listbox.Value = selected.FirstOrDefault();
        syncing = false;

        var labels = selected.Select(LabelOf).ToList();
        var tagged = Multiple && labels.Count > 0;
        tags.Visible = tagged;
        // 句柄建好以后才建标签：初始化器里 Values 可能先于 MaxTagCount 设，先按没有上限建会一下子建出成千上万个控件
        if (IsHandleCreated)
        {
            tags.MaxTagCount = MaxTagCount;
            tags.Tags = [.. selected.Select((value, index) => new SelectedTag(value, labels[index]))];
            tags.RemoveRequested -= OnTagRemoved;
            if (!Locked) tags.RemoveRequested += OnTagRemoved;
        }
        valueText.Visible = !tagged;
        valueText.Text = labels.Count == 0 ? PlaceholderText ?? ProviderControl.LangOf(this).SelectPlaceholder : labels[0];
        valueText.Tone = labels.Count == 0 ? TextTone.Muted : default;
        clear.Visible = Clearable && labels.Count > 0 && !Locked;
        chevron.Data = open ? Lucide.ChevronUp : Lucide.ChevronDown;
        Cursor = ReadOnly ? Cursors.Default : Cursors.Hand;
        ControlShell.Layout(this, Em);
        ControlShell.FitInnerButton(clear, Em);
        Invalidate();
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

    void SetSelected(IReadOnlyList<string> next)
    {
        if (Multiple) Values = next;
        else Value = next.FirstOrDefault();
    }

    /// <summary>列表的选择 → 值；选中了"新建"项时报告新建，清空搜索词。</summary>
    void OnListChanged()
    {
        if (syncing) return;
        var next = Multiple ? listbox.Values : listbox.Value is { } value ? [value] : [];
        var created = listbox.ItemsSource?.OfType<SelectCreation>().FirstOrDefault(creation => next.Contains(creation.Text));
        SetSelected(next);
        if (created is null) return;
        OptionCreated?.Invoke(this, new SelectCreatedEventArgs(created.Text));
        search.Text = "";
    }

    /// <summary>选中了一项：单选时关闭（焦点回到触发框）。</summary>
    void Picked()
    {
        if (!Multiple) CloseAndFocus(OpenChangeReason.Select);
    }

    void CloseAndFocus(OpenChangeReason reason)
    {
        SetOpen(false, reason);
        FindForm()?.Activate();
        Focus();
    }

    void OnSearchChanged()
    {
        UpdateItems();
        // 输入搜索词：高亮第一个匹配的（打开时清空搜索词不算：那时高亮的是已选项）；弹出层跟着列表变高、变矮
        if (search.Text.Length > 0) listbox.State.MoveToEnd(last: false);
        if (open) Present();
    }

    void OnTagRemoved(object? sender, SelectedTagEventArgs e) => Remove(e.Value);

    void Remove(string value) => Values = [.. Selected.Where(item => item != value)];

    void RemoveLast()
    {
        if (Selected is [.., var last]) Remove(last);
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

    void SetOpen(bool next, OpenChangeReason? reason)
    {
        if (next == open) return;
        open = next;
        if (next) Present();
        else
        {
            closedAt = Environment.TickCount64;
            surface.Hide();
        }
        Sync();
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(next, known));
    }

    /// <summary>面板 = 搜索框那一行 + 列表 + 描边，与外壳同宽；放在外壳下方（放不下时上方），隔 6。</summary>
    void Present()
    {
        if (!IsHandleCreated) return;
        var context = ElementContext.Of(this);
        var border = FloatingSurface.Border(context);
        var xs = (int)MathF.Round(Tokens.Space.Xs * Em);
        header.Padding = new Padding(xs, xs, xs, xs + border);
        var width = Width - 2 * border;
        var headerHeight = header.Visible ? search.GetPreferredSize(new Size(width - 2 * xs, 0)).Height + 2 * xs + border : 0;
        var listHeight = listbox.GetPreferredSize(new Size(width, 0)).Height;
        var panelSize = new Size(Width, headerHeight + listHeight + 2 * border);
        var target = RectangleToScreen(ClientRectangle);
        var (_, panel) = FloatingLayout.Position(FloatingPlacement.BottomStart, context.RightToLeft, target, panelSize, context.Scale(Offset), Screen.FromControl(this).WorkingArea);
        surface.Form.AccessibleName = AccessibleName;
        surface.Show(panel, border, context, null);
    }

    void SetHovered(bool value)
    {
        if (hovered == value) return;
        hovered = value;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        SetHovered(true);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
    }

    /// <summary>
    /// 点外壳（文字、标签、图标、箭头）：聚焦并切换打开；只读时只聚焦。可搜索时弹出层是激活的：开着时点外壳，它先因为失去激活关了，
    /// 紧接着这次按下又会打开——刚这样关掉的那一下不再打开。
    /// </summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left) return;
        Focus();
        if (ReadOnly) return;
        if (open) SetOpen(false, OpenChangeReason.Trigger);
        else if (Environment.TickCount64 - closedAt >= 300) Show();
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Enter or Keys.Space or Keys.Escape or Keys.Back
        || base.IsInputKey(keyData);

    /// <summary>触发框上的键盘（见类的说明）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Locked) return;
        if (e.KeyCode == Keys.Back && Multiple)
        {
            RemoveLast();
            e.Handled = true;
            return;
        }
        if (!open)
        {
            if (e.KeyCode is Keys.Down or Keys.Enter or Keys.Space) Show();
            else if (e.KeyCode == Keys.Up) Show(last: true);
            else return;
            e.Handled = e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode == Keys.Escape)
        {
            SetOpen(false, OpenChangeReason.Escape);
            e.Handled = e.SuppressKeyPress = true;
            return;
        }
        if (ListboxControl.ToListKey(e.KeyData) is { } key && listbox.State.Handle(key, space: true, picked: _ => Picked()))
            e.Handled = e.SuppressKeyPress = true;
    }

    /// <summary>Tab 离开：关闭（焦点照常移走）。</summary>
    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        if (open && !surface.Form.ContainsFocus) SetOpen(false, OpenChangeReason.Focus);
    }

    /// <summary>字母：打开时在列表里类型搜索；关闭时单选直接选中以它开头的项（同平台的下拉框）。</summary>
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (e.Handled || Locked || char.IsControl(e.KeyChar) || !listbox.State.Typeahead(e.KeyChar.ToString())) return;
        e.Handled = true;
        if (!open && !Multiple && listbox.State.Active is { } active && !listbox.State.IsSelected(active)) listbox.State.Select(active);
    }

    /// <summary>搜索框里：↑↓、Page Up / Down 移动，Enter 选中（Home / End、空格留给输入）；Tab 关闭、焦点回到触发框；多选时空的搜索框里 Backspace 移除最后一项。</summary>
    void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Tab:
                CloseAndFocus(OpenChangeReason.Focus);
                e.Handled = e.SuppressKeyPress = true;
                return;
            case Keys.Back when Multiple && search.Text.Length == 0:
                RemoveLast();
                e.Handled = e.SuppressKeyPress = true;
                return;
            case Keys.Home or Keys.End:
                return;
        }
        if (ListboxControl.ToListKey(e.KeyData) is { } key && listbox.State.Handle(key, space: false, picked: _ => Picked()))
            e.Handled = e.SuppressKeyPress = true;
    }

    /// <summary>外壳（<see cref="ControlShell"/>）。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ControlShell.Paint(e.Graphics, this, Em, Radius, Variant, (ReadOnly, Invalid, hovered, Focused));
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <summary>不小于控件高度：放在 FieldControl 这类不是 FlexControl 的容器里时，它按想要的大小排，不看最小尺寸。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize);
        return new Size(Math.Max(size.Width, MinimumSize.Width), Math.Max(size.Height, MinimumSize.Height));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) surface.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new SelectAccessibleObject(this);

    /// <summary>读屏：组合框（展开、收起），值是全部已选的文字；没写名称时取所在字段的标签。</summary>
    sealed class SelectAccessibleObject(SelectControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => owner.AccessibleName is { Length: > 0 } name ? name : FieldControl.Of(owner) is { } scope && scope.Owns(owner) ? scope.Label : null;

        public override AccessibleRole Role => AccessibleRole.ComboBox;

        public override string? Value { get => owner.Summary; set { } }

        public override AccessibleStates State => base.State | (owner.open ? AccessibleStates.Expanded : AccessibleStates.Collapsed) | (owner.ReadOnly ? AccessibleStates.ReadOnly : 0);

        public override string DefaultAction => owner.open ? "收起" : "展开";

        public override void DoDefaultAction()
        {
            if (owner.open) owner.SetOpen(false, OpenChangeReason.Trigger);
            else if (!owner.Locked) owner.Show();
        }
    }

    /// <summary>搜索框那一行：下边一条 borderMuted 分隔线。</summary>
    sealed class HeaderPanel : Panel
    {
        public HeaderPanel() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var line = Math.Max(1, LogicalToDeviceUnits(1));
            using var brush = new SolidBrush(ProviderControl.ThemeOf(this).Colors.BorderMuted);
            e.Graphics.FillRectangle(brush, 0, Height - line, Width, line);
        }
    }
}

/// <summary>新建了一项的事件参数。</summary>
/// <param name="text">输入的文字，也就是选中的值。</param>
public sealed class SelectCreatedEventArgs(string text) : EventArgs
{
    /// <summary>输入的文字。</summary>
    public string Text { get; } = text;
}
