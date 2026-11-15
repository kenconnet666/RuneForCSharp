using System.ComponentModel;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 分段输入框（vue-tsx-pro 的 SegmentedField）：输入框外壳（<see cref="ControlShell"/>，同 TextFieldControl、SelectControl）里放一组日期分段（<see cref="DateSegmentGroupControl"/>）、
/// 可选的前置图标、清空按钮与尾部内容。<see cref="DateFieldControl"/>、<see cref="TimeFieldControl"/> 是它的两种用法（只差有哪些段与怎样校验）。
/// 值是 ISO 字符串（<see cref="DateSegments.ToIso"/>），变了发 <see cref="ValueChanged"/>；所有段都填了才写回，删掉一段写回 null。点外壳的空白处时焦点进第一个没填的段。
/// 段的顺序、小时制、占位文字按所在的语言包；所在表单重置时丢掉打了一半的段。
/// </summary>
[ToolboxItem(false)]
[DefaultEvent(nameof(ValueChanged))]
public class SegmentedFieldControl : FlexControl, IFieldControl, IFormPart
{
    readonly IconControl icon = new() { Tone = TextTone.Muted, Visible = false };
    readonly DateSegmentGroupControl group = new();
    readonly ClearButtonControl clear = new() { Radius = Radius.Full, Visible = false, TabStop = false };
    readonly ContainerPanel suffix = new() { AutoSize = true, Visible = false };
    IFormController? form;
    int resets;
    bool hovered;
    string? value;

    /// <summary>新建：分段状态按所在的语言包。</summary>
    public SegmentedFieldControl()
    {
        Align = FlexAlign.Center;
        Gap = Space.Sm;
        AutoSize = true;
        Cursor = Cursors.IBeam;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Segments = new DateSegments([DateSegmentType.Year, DateSegmentType.Month, DateSegmentType.Day])
        {
            Value = () => DateSegments.FromIso(Value, Fields),
            Changed = next => SetValue(next is null ? null : DateSegments.ToIso(next, Fields)),
            PlaceholderValue = PlaceholderNow,
            ReadOnly = () => !Enabled || ReadOnly,
        };
        group.Segments = Segments;
        Controls.AddRange([icon, group, clear, suffix]);
        SetGrow(group, true);
        clear.Click += (_, _) => Clear();
        group.GotFocus += (_, _) => Invalidate();
        group.LostFocus += (_, _) => Invalidate();
        foreach (Control child in Controls)
        {
            child.MouseEnter += (_, _) => SetHovered(true);
            child.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        }
        Configure();
    }

    /// <summary>值（ISO 字符串）；没填全时 null。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value { get => value; set => SetValue(value); }

    /// <summary>有哪些段（按年 → 秒的顺序，显示顺序按语言）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<DateSegmentType> Fields { get; set { field = value; Configure(); } } = [DateSegmentType.Year, DateSegmentType.Month, DateSegmentType.Day];

    /// <summary>12 / 24 小时制（"h12" / "h23"，有时的段时）；不写时按语言。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? HourCycle { get; set { field = value; Configure(); } }

    /// <summary>样式，默认 surface。</summary>
    [Category("Appearance")]
    public ControlVariant Variant { get => field.Value is null ? ControlVariant.Surface : field; set { field = value; Invalidate(); } }

    /// <summary>字号档位；不写时跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get;
        set
        {
            field = value;
            if (value.Pixels is { } pixels) Font = Fonts.Get(Font.FamilyName(), pixels * 72 / 96, Font.Style);
            else ResetFont();
        }
    }

    /// <summary>圆角，默认 md。</summary>
    [Category("Appearance")]
    public Radius Radius { get => field.Value is null ? Radius.Md : field; set { field = value; Invalidate(); } }

    /// <summary>前置图标。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconData? Icon { get => icon.Data; set { icon.Data = value; icon.Visible = value is not null; } }

    /// <summary>有值时显示清空按钮。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Clearable { get; set { field = value; Sync(); } }

    /// <summary>框里最后的内容（日期选择器打开日历的按钮）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Suffix
    {
        get;
        set
        {
            suffix.Controls.Clear();
            field = value;
            if (value is not null) suffix.Controls.Add(value);
            suffix.Visible = value is not null;
        }
    }

    /// <summary>校验未通过；不写时跟着所在的 Field，值超出范围、不可选时也算。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsInvalid { get; set { field = value; Sync(); } }

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsRequired { get; set { field = value; Sync(); } }

    /// <summary>只读：能聚焦、能换段，不能改；不写时跟着所在的 Field。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsReadOnly { get; set { field = value; Sync(); } }

    /// <summary>值变了。</summary>
    public event EventHandler? ValueChanged;

    bool ShouldSerializeVariant() => Variant != ControlVariant.Surface;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    bool ShouldSerializeRadius() => Radius != Radius.Md;

    /// <summary>分段状态（测试与派生类用）。</summary>
    internal DateSegments Segments { get; }

    /// <summary>这一组分段（测试用）。</summary>
    internal DateSegmentGroupControl Group => group;

    /// <summary>清空按钮（测试用）。</summary>
    internal ClearButtonControl ClearButton => clear;

    /// <summary>出错：自己写了的优先，其次所在字段的；有值且超出范围、不可选时也算。</summary>
    internal bool Invalid => (IsInvalid ?? FieldControl.Of(this)?.Invalid ?? false) || Value is { } iso && IsOutOfRange(iso);

    bool ReadOnly => IsReadOnly ?? FieldControl.ReadOnlyOf(this);

    bool Required => IsRequired ?? FieldControl.Of(this)?.Required ?? false;

    float Em => this.EmPixels();

    /// <summary>值超出范围、是不可选的日期或时刻：外壳显示出错，值照样写回（由表单校验决定能不能提交）。</summary>
    /// <param name="iso">填好的 ISO 字符串。</param>
    protected virtual bool IsOutOfRange(string iso) => false;

    void IFieldControl.FieldChanged() => Sync();

    void IFormPart.Connect() => Subscribe();

    /// <summary>范围、不可选的条件变了：重新判断出错（派生类用）。</summary>
    private protected void Revalidate() => Sync();

    void SetValue(string? next)
    {
        if (value == next) return;
        value = next;
        Segments.Sync();
        Sync();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>↑↓ 从空段开始时的起点：此刻（选周时从这一周所属的年起步：12 月底的那周可能已经算明年的第 1 周）。</summary>
    DateFieldValues PlaceholderNow()
    {
        var now = DateTime.Now;
        return DateSegments.FromDateTime(now) with
        {
            Year = Fields.Contains(DateSegmentType.Week) ? ISOWeek.GetYear(now) : now.Year,
            Quarter = (now.Month + 2) / 3,
            Week = ISOWeek.GetWeekOfYear(now),
            Second = 0,
        };
    }

    /// <summary>段与语言：段、语言、小时制、占位、季度与周的排法。</summary>
    void Configure()
    {
        if (Segments is null) return;
        var lang = ProviderControl.LangOf(this);
        Segments.Fields = Fields;
        Segments.Culture = CultureInfo.GetCultureInfo(lang.Code);
        Segments.HourCycle = HourCycle ?? lang.HourCycle;
        Segments.Placeholders = lang.DateFieldPlaceholders;
        Segments.PeriodPatterns = lang.DateFieldPeriodPatterns;
        Segments.Sync();
        Sync();
    }

    /// <summary>出错、只读、清空按钮、读屏名称，再让分段跟上。</summary>
    void Sync()
    {
        if (Segments is null) return;
        var field = FieldControl.Of(this) is { } owner && owner.Owns(this) ? owner : null;
        group.AccessibleName = AccessibleName is { Length: > 0 } own ? own : field?.Label;
        group.AccessibleDescription = FieldControl.DescribedBy(this, AccessibleDescription);
        group.IsReadOnly = ReadOnly;
        group.IsRequired = Required;
        group.IsInvalid = Invalid;
        group.Enabled = Enabled;
        clear.Visible = Clearable && (Value is not null || Segments.Partial) && Enabled && !ReadOnly;
        group.Refresh();
        ControlShell.Layout(this, Em);
        ControlShell.FitInnerButton(clear, Em);
        Invalidate();
    }

    void Clear()
    {
        Segments.Clear();
        SetValue(null);
        var editable = Segments.Editable;
        Segments.SetFocused((editable.FirstOrDefault(segment => segment.IsPlaceholder) ?? editable.FirstOrDefault())?.Type);
        group.Focus();
        Sync();
    }

    /// <summary>点外壳的空白处（图标、内边距）：焦点进第一个没填的段（都填了时第一段）。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left) return;
        var editable = Segments.Editable;
        Segments.SetFocused((editable.FirstOrDefault(segment => segment.IsPlaceholder) ?? editable.FirstOrDefault())?.Type);
        group.Focus();
        group.Refresh();
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

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Subscribe();
        Configure();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Subscribe();
        Configure();
    }

    /// <summary>所在表单重置时（ResetCount 变了）丢掉打了一半的段（上游 useFormReset）。</summary>
    void Subscribe()
    {
        var next = FormControl.Of(this)?.Controller;
        if (next == form) return;
        if (form is not null) form.Changed -= OnFormChanged;
        form = next;
        resets = form?.ResetCount ?? 0;
        if (form is not null) form.Changed += OnFormChanged;
    }

    void OnFormChanged(object? sender, EventArgs e)
    {
        if (form is null || form.ResetCount == resets) return;
        resets = form.ResetCount;
        Segments.Reset();
        Sync();
    }

    /// <summary>不小于控件高度：放在 FieldControl 这类不是 FlexControl 的容器里时，它按想要的大小排，不看最小尺寸。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize);
        return new Size(Math.Max(size.Width, MinimumSize.Width), Math.Max(size.Height, MinimumSize.Height));
    }

    /// <summary>外壳（<see cref="ControlShell"/>）：聚焦在分段上时画焦点环。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ControlShell.Paint(e.Graphics, this, Em, Radius, Variant, (ReadOnly, Invalid, hovered, group.Focused));
    }
}
