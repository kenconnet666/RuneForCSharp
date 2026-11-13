using System.Collections;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 列表框（vue-tsx-pro 的 Listbox）：在一列选项里单选或多选。数据驱动、只画看得见的行（上万个选项也不建控件）。
/// <code>
/// var list = new ListboxControl { ItemsSource = cities, Value = "上海", AccessibleName = "城市" };
/// var columns = new ListboxControl { ItemsSource = fields, SelectionMode = SelectionMode.Multiple, Values = ["field-0"], Height = 256 };
/// </code>
/// 选项是 <see cref="ListboxOption"/>（值、文字、不可用、说明、图标、分组）或字符串；别的数据给 <see cref="ToOption"/>。
/// 焦点在列表上：↑↓、Home / End、Page Up / Down 移动高亮，Enter / 空格选中，按字母跳到以它开头的项（状态与键盘见 <see cref="ListState"/>）；
/// 指针悬停高亮、点击选中。分组的组名是一行小标题。没有选项时显示空状态（语言包的"没有选项"），<see cref="IsLoading"/> 时换成几行骨架。
/// 没写 Height 时最高 20em。放进 FieldControl 时以标签为名称，沿用必填（单选时再点已选项不取消）。
/// 出入：选项是组件库画的，没有自定义选项内容（TS 的渲染函数）与部件属性。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Picked))]
[Description("列表框：在一列选项里单选或多选，只画看得见的行。")]
public class ListboxControl : Control, IFieldControl
{
    readonly ScrollAreaControl scroll = new() { IsViewportFocusable = false };
    readonly ListboxView view;
    readonly EmptyControl empty = new() { Icon = null, TextSize = TextSize.Sm, Visible = false };
    readonly FlexControl skeleton = new() { Direction = FlexDirection.Column, Visible = false };
    readonly DelayedLoading loading;
    bool syncing;

    /// <summary>新建：列表状态、画出来的行、骨架与空状态。</summary>
    public ListboxControl()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = true;
        State = new ListState(OnSelectionChanged);
        State.ActiveChanged += OnActiveChanged;
        view = new ListboxView(this);
        scroll.Content = view;
        for (var index = 0; index < 4; index++) skeleton.Controls.Add(new SkeletonControl { Delay = index * 100, Tag = 0.85 - index * 0.15 });
        loading = new DelayedLoading(shown =>
        {
            skeleton.Visible = shown;
            UpdateBody();
        });
        Controls.AddRange([scroll, skeleton, empty]);
        foreach (Control child in Controls) child.MouseDown += (_, _) => FocusUnlessEmbedded();
    }

    /// <summary>嵌在下拉的弹出层里（SelectControl）：不能聚焦、点击不抢焦点，按键由下拉转过来。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool Embedded
    {
        get;
        set
        {
            field = value;
            SetStyle(ControlStyles.Selectable, !value);
            TabStop = !value;
        }
    }

    /// <summary>没有选项时的说明；null 时取语言包的"没有选项"（下拉搜索没有匹配时换成"没有匹配的选项"）。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string? EmptyText
    {
        get;
        set
        {
            field = value;
            UpdateBody();
        }
    }

    internal void FocusUnlessEmbedded()
    {
        if (!Embedded) Focus();
    }

    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable? ItemsSource { get; set { field = value; Rebuild(); } }

    /// <summary>数据 → 选项；数据是 ListboxOption、字符串时不用写。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, ListboxOption>? ToOption { get; set { field = value; Rebuild(); } }

    /// <summary>选择方式，默认 single。</summary>
    [Category("Behavior")]
    public SelectionMode SelectionMode { get => field.Value is null ? SelectionMode.Single : field; set { field = value; SyncSelection(); } }

    /// <summary>单选：选中项的值；没有选中时为空。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            SyncSelection();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>多选：选中项的值。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Values
    {
        get;
        set
        {
            field = value;
            SyncSelection();
            ValuesChanged?.Invoke(this, EventArgs.Empty);
        }
    } = [];

    /// <summary>必须有选中项：单选时再点已选项不取消；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsRequired { get; set { field = value; SyncSelection(); } }

    /// <summary>加载中：选项换成骨架行（很快完成时不闪）。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsLoading { get; set { field = value; loading.Update(value); } }

    /// <summary>单选的值变了。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>多选的值变了。</summary>
    public event EventHandler? ValuesChanged;

    /// <summary>点选了一项（点击、Enter、空格；再选已选的那项也会发）。</summary>
    public event EventHandler<ListboxPickedEventArgs>? Picked;

    /// <summary>列表的状态（选项、高亮、已选、键盘）。</summary>
    [Browsable(false)]
    public ListState State { get; }

    /// <summary>画出来的行（测试用）。</summary>
    internal ListboxView View => view;

    /// <summary>空状态（测试用）。</summary>
    internal EmptyControl EmptyPart => empty;

    /// <summary>骨架（测试用）。</summary>
    internal Control SkeletonPart => skeleton;

    bool ShouldSerializeSelectionMode() => SelectionMode != SelectionMode.Single;

    bool Multiple => SelectionMode == SelectionMode.Multiple;

    bool Required => IsRequired ?? FieldControl.Of(this)?.IsRequired ?? false;

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    void IFieldControl.FieldChanged() => SyncSelection();

    /// <summary>数据 → 选项（按分组排好）；数据不是选项、又还没有 ToOption 时先不建（初始化器里 ItemsSource 可能先于 ToOption 设）。</summary>
    void Rebuild()
    {
        var items = ItemsSource?.Cast<object?>().ToList() ?? [];
        if (ToOption is null && items.Any(item => item is not (ListboxOption or string))) return;
        State.SetItems(items, ToOption);
        view.Rebuild();
        SyncSelection();
        UpdateBody();
    }

    void SyncSelection()
    {
        if (syncing) return;
        State.Mode = SelectionMode;
        State.Required = Required;
        State.Selected = Multiple ? Values : Value is { } value ? [value] : [];
        view.Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.Selection, -1);
    }

    void OnSelectionChanged(IReadOnlyList<string> next)
    {
        syncing = true;
        if (Multiple) Values = next;
        else Value = next.FirstOrDefault();
        syncing = false;
        view.Invalidate();
    }

    /// <summary>高亮项变了：重画，滚进可见范围，告诉读屏焦点到了那一项。</summary>
    void OnActiveChanged()
    {
        view.Invalidate();
        if (State.Active is not { } value) return;
        var row = view.RowOf(value);
        if (row < 0) return;
        var (top, height) = view.RowBounds(row == 1 && view.IsGroup(0) ? 0 : row);
        var offset = scroll.ScrollOffset.Y;
        var visible = scroll.ClientSize.Height;
        if (top < offset) scroll.ScrollOffset = new Point(0, top);
        else if (top + height > offset + visible) scroll.ScrollOffset = new Point(0, top + height - visible);
        if (Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, State.IndexOf(value));
    }

    void UpdateBody()
    {
        var hasRows = State.Options.Count > 0;
        scroll.Visible = !skeleton.Visible && hasRows;
        empty.Visible = !skeleton.Visible && !hasRows && !IsLoading;
        empty.Description = EmptyText ?? ProviderControl.LangOf(this).SelectEmpty;
        PerformLayout();
    }

    /// <summary>内边距 space.xs；没写高度时按内容，最高 20em；骨架行高 1em、上下 0.625em、宽度依次递减。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var em = Em;
        var xs = (int)MathF.Round(Tokens.Space.Xs * em);
        var area = new Rectangle(xs, xs, Math.Max(0, ClientSize.Width - 2 * xs), Math.Max(0, ClientSize.Height - 2 * xs));
        scroll.Bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        view.Padding = new Padding(xs);
        empty.Bounds = area;
        skeleton.Bounds = area;
        var sm = (int)MathF.Round(Tokens.Space.Sm * em);
        foreach (var bar in skeleton.Controls.OfType<SkeletonControl>())
        {
            bar.Margin = new Padding(sm, (int)MathF.Round(0.625f * em), sm, (int)MathF.Round(0.625f * em));
            var width = (int)Math.Max(0, (area.Width - 4 * em) * (double)bar.Tag!);
            bar.MinimumSize = bar.MaximumSize = new Size(width, (int)MathF.Round(em));
        }
    }

    /// <summary>自己滚动时的最高高度（设备像素）；null 时 20em（命令面板换成 min(24rem, 窗体高度一半)）。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int? ScrollMaxHeight { get; set; }

    /// <summary>没写高度时按内容（行、骨架或空状态），最高 20em（或 <see cref="ScrollMaxHeight"/>）。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var em = Em;
        var xs = (int)MathF.Round(Tokens.Space.Xs * em);
        var width = proposedSize.Width > 0 ? proposedSize.Width : Width;
        var inner = skeleton.Visible ? skeleton.GetPreferredSize(new Size(width, 0)).Height
            : empty.Visible ? empty.GetPreferredSize(new Size(width, 0)).Height
            : view.TotalHeight;
        return new Size(width, Math.Min(inner + 2 * xs, ScrollMaxHeight ?? (int)MathF.Round(20 * em)));
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        view.Rebuild();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        view.Rebuild();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        SyncSelection();
        UpdateBody();
    }

    /// <summary>得到焦点：没有高亮时高亮已选的第一项（没有已选时第一个可用项）；焦点环跟着重画。</summary>
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (State.Active is null) State.ActivateDefault();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <summary>点选了一项：切换选中、发 Picked。</summary>
    internal void Pick(string value)
    {
        if (!Enabled) return;
        State.SetActive(value);
        State.Select(value);
        Picked?.Invoke(this, new ListboxPickedEventArgs(value));
    }

    /// <summary>方向键、Home / End、Page Up / Down、Enter、空格由列表处理（不让窗体拿去切焦点、按默认按钮）。</summary>
    protected override bool IsInputKey(Keys keyData) => ToListKey(keyData) is not null || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Enabled || ToListKey(e.KeyData) is not { } key) return;
        e.Handled = State.Handle(key, picked: value => Picked?.Invoke(this, new ListboxPickedEventArgs(value)));
        // 空格的字符不再交给类型搜索
        e.SuppressKeyPress = e.Handled;
    }

    /// <summary>字母：类型搜索。</summary>
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (!e.Handled && Enabled && !char.IsControl(e.KeyChar)) e.Handled = State.Typeahead(e.KeyChar.ToString());
    }

    /// <summary>平台的按键 → 列表的按键（带 Ctrl、Alt 的不算）；不是列表认的键时为 null。</summary>
    /// <param name="keyData">按键与修饰键。</param>
    public static ListKey? ToListKey(Keys keyData) => (keyData & ~Keys.Shift) switch
    {
        Keys.Down => ListKey.Down,
        Keys.Up => ListKey.Up,
        Keys.Home => ListKey.Home,
        Keys.End => ListKey.End,
        Keys.PageDown => ListKey.PageDown,
        Keys.PageUp => ListKey.PageUp,
        Keys.Enter => ListKey.Enter,
        Keys.Space => ListKey.Space,
        _ => null,
    };

    /// <summary>键盘聚焦时一圈 2 像素的焦点环（画在列表的边上）。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!Focused || !ShowFocusCues) return;
        var ring = LogicalToDeviceUnits(2);
        using var pen = new Pen(ProviderControl.ThemeOf(this).Colors.FocusRing, ring);
        e.Graphics.DrawRectangle(pen, ring / 2f, ring / 2f, Width - ring, Height - ring);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ListboxAccessibleObject(this);

    /// <summary>读屏：列表（名称没写时取字段的标签），子节点是每个选项（选中、不可用、高亮时有焦点）。</summary>
    sealed class ListboxAccessibleObject(ListboxControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Help => FieldControl.DescribedBy(owner, base.Help);

        public override string? Name => owner.AccessibleName is { Length: > 0 } name ? name : FieldControl.Of(owner) is { } scope && scope.Owns(owner) ? scope.Label : null;

        public override AccessibleRole Role => AccessibleRole.List;

        public override int GetChildCount() => owner.State.Options.Count;

        public override AccessibleObject? GetChild(int index) => index >= 0 && index < owner.State.Options.Count ? new OptionAccessibleObject(owner, this, index) : null;

        public override AccessibleObject? GetFocused() => owner.State.Active is { } value ? GetChild(owner.State.IndexOf(value)) : base.GetFocused();
    }

    sealed class OptionAccessibleObject(ListboxControl owner, AccessibleObject list, int index) : AccessibleObject
    {
        ListboxOption Option => owner.State.Options[index].Option;

        public override string? Name => Option.Label;

        public override string? Description => Option.Group;

        public override AccessibleRole Role => AccessibleRole.ListItem;

        public override AccessibleObject Parent => list;

        public override string DefaultAction => "选中";

        public override AccessibleStates State
        {
            get
            {
                var states = AccessibleStates.Selectable;
                if (owner.State.IsSelected(Option.Value)) states |= AccessibleStates.Selected;
                if (Option.IsDisabled) states |= AccessibleStates.Unavailable;
                if (owner.State.Active == Option.Value) states |= AccessibleStates.Focused;
                return states;
            }
        }

        public override Rectangle Bounds
        {
            get
            {
                var (top, height) = owner.view.RowBounds(owner.view.RowOf(Option.Value));
                return owner.view.RectangleToScreen(new Rectangle(0, top, owner.view.Width, height));
            }
        }

        public override void DoDefaultAction()
        {
            if (!Option.IsDisabled) owner.Pick(Option.Value);
        }
    }
}

/// <summary>点选了一项的事件参数。</summary>
/// <param name="value">选项的值。</param>
public sealed class ListboxPickedEventArgs(string value) : EventArgs
{
    /// <summary>选项的值。</summary>
    public string Value { get; } = value;
}

/// <summary>
/// 列表的行（<see cref="ListboxControl"/> 建的）：组名、选项一行一行往下排，只画落在可见区域里的行（滚动区域移动它，重画时按剪切区算）。
/// 选项：控件高度、上下 space.xs、左右 space.sm、sm 圆角，图标、文字（下面一行 sm 说明）、行尾的已选标记；高亮 fillHover 底，已选 accentText，不可用变淡。
/// 组名：sm、textMuted、medium，上 space.sm、下 space.xs、左右 space.sm。
/// </summary>
sealed class ListboxView : Control, IElementHost
{
    readonly ListboxControl owner;
    readonly Text label = new() { Truncate = true, Tone = TextTone.Inherit };
    readonly Text description = new() { Size = TextSize.Sm, Tone = TextTone.Inherit };
    readonly Text group = new() { Size = TextSize.Sm, Tone = TextTone.Muted, Weight = TextWeight.Medium };
    readonly Icon icon = new();
    readonly Icon check = new() { Data = Lucide.Check, Tone = TextTone.Accent };
    readonly Effect paint;
    List<Row> rows = [];
    bool pressed;
    bool painting;

    /// <summary>一行：组名（Option 为空）或选项，在内容里的位置与高度。</summary>
    readonly record struct Row(string? Group, ListStateEntry? Option, int Top, int Height);

    public ListboxView(ListboxControl owner)
    {
        this.owner = owner;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        foreach (var element in new Element[] { label, description, group, icon, check }) element.Host = this;
        paint = new Effect(Invalidate);
        Cursor = Cursors.Hand;
    }

    /// <summary>所有行的总高度（含上下内边距）。</summary>
    public int TotalHeight => (rows.Count > 0 ? rows[^1].Top + rows[^1].Height : 0) + Padding.Bottom;

    /// <summary>选项所在的行号；不在时 -1。</summary>
    public int RowOf(string value) => rows.FindIndex(row => row.Option?.Option.Value == value);

    /// <summary>这一行是组名。</summary>
    public bool IsGroup(int row) => row >= 0 && row < rows.Count && rows[row].Option is null;

    /// <summary>行的位置与高度。</summary>
    public (int Top, int Height) RowBounds(int row) => row >= 0 && row < rows.Count ? (rows[row].Top, rows[row].Height) : (0, 0);

    /// <summary>选项、字号、DPI 变了：重新量各行的高度。</summary>
    public void Rebuild()
    {
        var context = ElementContext.Of(this);
        var em = context.Em;
        var lineGap = (int)MathF.Round(Tokens.Space.Xs * em);
        var minHeight = (int)MathF.Ceiling(ControlHeight.Of(context, em, Tokens.Size.Control));
        var unbounded = new Size(int.MaxValue, int.MaxValue);
        label.Content = "国";
        var labelHeight = label.Measure(context, unbounded).Height;
        description.Content = "国";
        var descriptionHeight = description.Measure(context, unbounded).Height;
        group.Content = "国";
        var smEm = Tokens.FontSize.Sm * context.Dpi / 96f;
        var groupHeight = group.Measure(context, unbounded).Height + (int)MathF.Round((Tokens.Space.Sm + Tokens.Space.Xs) * smEm);
        var next = new List<Row>();
        var top = Padding.Top;
        string? current = null;
        foreach (var entry in owner.State.Options)
        {
            if (entry.Option.Group is { } name && name != current)
            {
                next.Add(new Row(name, null, top, groupHeight));
                top += groupHeight;
            }
            current = entry.Option.Group;
            var height = Math.Max(minHeight, 2 * lineGap + labelHeight + (entry.Option.Description is null ? 0 : descriptionHeight));
            next.Add(new Row(null, entry, top, height));
            top += height;
        }
        rows = next;
        Height = TotalHeight;
        Parent?.PerformLayout();
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize) => new(proposedSize.Width, TotalHeight);

    // 画的时候给 label、description 换内容只是借它们来量、来画：这时的失效通知不再要求重画，否则每画一遍都让自己失效，无限重画
    void IElementHost.InvalidateElement(Element element)
    {
        if (!painting) Invalidate();
    }

    void IElementHost.InvalidateLayout(Element element)
    {
        if (!painting) Invalidate();
    }

    /// <summary>只画与剪切区相交的行。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        painting = true;
        try
        {
            PaintRows(e);
        }
        finally
        {
            painting = false;
        }
    }

    void PaintRows(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        var context = ElementContext.Of(this);
        var colors = context.Theme.Colors;
        var em = context.Em;
        var sm = (int)MathF.Round(Tokens.Space.Sm * em);
        var left = Padding.Left;
        var width = Math.Max(0, Width - Padding.Horizontal);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        foreach (var row in rows)
        {
            if (row.Top + row.Height < e.ClipRectangle.Top || row.Top > e.ClipRectangle.Bottom) continue;
            if (row.Option is not { } entry)
            {
                var smEm = Tokens.FontSize.Sm * context.Dpi / 96f;
                group.Content = row.Group!;
                var size = group.Measure(context, new Size(width, int.MaxValue));
                group.Bounds = new Rectangle(left + (int)MathF.Round(Tokens.Space.Sm * smEm), row.Top + (int)MathF.Round(Tokens.Space.Sm * smEm), size.Width, size.Height);
                group.Paint(graphics, context);
                continue;
            }
            var option = entry.Option;
            var active = owner.State.Active == option.Value && !option.IsDisabled;
            var selected = owner.State.IsSelected(option.Value);
            var box = new Rectangle(left, row.Top, width, row.Height);
            if (active)
            {
                using var path = Drawing.RoundedRectangle(box, (ProviderControl.ThemeOf(this).Radii.Sm) * em);
                using var fill = new SolidBrush(colors.FillHover);
                graphics.FillPath(fill, path);
            }
            // 不可用：文字、图标混进底色（TS 的 opacity: disabled）
            Color Dim(Color color) => option.IsDisabled ? AppShellControl.Blend(Color.FromArgb((int)(255 * Tokens.Opacity.Disabled), color), colors.Background) : color;
            var foreground = Dim(selected ? colors.AccentText : colors.Text);
            var x = box.Left + sm;
            var inner = box.Width - 2 * sm;
            var checkSize = check.Measure(context, new Size(inner, box.Height));
            if (option.Icon is { } data)
            {
                icon.Data = data;
                var size = icon.Measure(context, new Size(inner, box.Height));
                icon.Bounds = new Rectangle(x, box.Top + (box.Height - size.Height) / 2, size.Width, size.Height);
                icon.Paint(graphics, context with { ForeColor = foreground });
                x += size.Width + sm;
            }
            var textWidth = Math.Max(0, box.Right - sm - checkSize.Width - sm - x);
            label.Content = option.Label;
            var labelSize = label.Measure(context, new Size(textWidth, int.MaxValue));
            var descriptionSize = Size.Empty;
            if (option.Description is { } text)
            {
                description.Content = text;
                descriptionSize = description.Measure(context, new Size(textWidth, int.MaxValue));
            }
            var y = box.Top + (box.Height - labelSize.Height - descriptionSize.Height) / 2;
            label.Bounds = new Rectangle(x, y, textWidth, labelSize.Height);
            label.Paint(graphics, context with { ForeColor = foreground });
            if (option.Description is not null)
            {
                description.Bounds = new Rectangle(x, y + labelSize.Height, textWidth, descriptionSize.Height);
                description.Paint(graphics, context with { ForeColor = Dim(colors.TextMuted) });
            }
            if (!selected) continue;
            check.Bounds = new Rectangle(box.Right - sm - checkSize.Width, box.Top + (box.Height - checkSize.Height) / 2, checkSize.Width, checkSize.Height);
            check.Paint(graphics, context);
        }
    }

    /// <summary>指针所在的选项（组名、空白处为空）。</summary>
    ListboxOption? OptionAt(Point point) => rows.FirstOrDefault(row => point.Y >= row.Top && point.Y < row.Top + row.Height).Option?.Option;

    /// <summary>
    /// 上一次指针移动时的屏幕位置。列表出现在不动的指针下面时系统也补发一次移动：那次不抢键盘的高亮（同 Radix Select），
    /// 否则一打开下拉、命令面板，高亮就跳到指针碰巧停着的那项。
    /// </summary>
    Point lastPointer;

    /// <inheritdoc />
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) lastPointer = MousePosition;
    }

    /// <summary>指针移过：高亮（不可用的不高亮）。</summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (MousePosition == lastPointer) return;
        lastPointer = MousePosition;
        if (OptionAt(e.Location) is { IsDisabled: false } option && owner.State.Active != option.Value) owner.State.SetActive(option.Value);
    }

    /// <summary>按下：焦点交给列表（选项不抢焦点）。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        pressed = e.Button == MouseButtons.Left;
        owner.FocusUnlessEmbedded();
    }

    /// <summary>在一个可用的选项上按下、松开：选中。</summary>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (pressed && e.Button == MouseButtons.Left && OptionAt(e.Location) is { IsDisabled: false } option) owner.Pick(option.Value);
        pressed = false;
    }
}
