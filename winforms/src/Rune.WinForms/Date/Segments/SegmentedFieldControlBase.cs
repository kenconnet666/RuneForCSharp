using System.ComponentModel;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 分段输入框的外壳（vue-tsx-pro 的 controlParts 加 useFieldSegments）：输入框外壳（<see cref="ControlShell"/>，同 TextFieldControl、SelectControl）里放一组或两组日期分段（<see cref="DateSegmentGroupControl"/>）、
/// 可选的前置图标、清空按钮与尾部内容。<see cref="SegmentedFieldControl"/> 是一组（DateFieldControl、TimeFieldControl、DatePickerControl），<c>DateRangePickerControl</c> 是两组（开始 – 结束）。
/// 这里管与组数无关的部分：外观档位、所在 FieldControl 的标签、说明、出错、只读、必填，表单重置时丢掉打了一半的段，清空，点外壳空白处聚焦；
/// 值怎样读写由派生类给（<see cref="LaneValue"/>、<see cref="LaneChanged"/>）。
/// </summary>
[ToolboxItem(false)]
public abstract class SegmentedFieldControlBase : FlexControl, IFieldControl, IFormPart
{
    readonly IconControl icon = new() { Tone = TextTone.Muted, Visible = false };
    readonly DateSegmentGroupControl[] groups;
    // 两组之间的"–"：FlexControl 不看外边距，左右的 xs 是外面这层的内边距
    readonly ContainerPanel separator = new() { AutoSize = true, Visible = false };
    readonly ClearButtonControl clear = new() { Radius = Radius.Full, Visible = false, TabStop = false };
    readonly ContainerPanel suffix = new() { AutoSize = true, Visible = false };
    IFormController? form;
    int resets;
    bool hovered;

    /// <summary>新建：每组分段的状态按所在的语言包。</summary>
    /// <param name="lanes">几组分段：1 或 2（开始 – 结束）。</param>
    private protected SegmentedFieldControlBase(int lanes)
    {
        Align = FlexAlign.Center;
        Gap = Space.Sm;
        AutoSize = true;
        Cursor = Cursors.IBeam;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Lanes = [.. Enumerable.Range(0, lanes).Select(lane => new DateSegments(Fields)
        {
            Value = () => LaneValue(lane),
            Changed = next => LaneChanged(lane, next is null ? null : DateSegments.ToIso(next, Fields)),
            PlaceholderValue = PlaceholderNow,
            ReadOnly = () => !Enabled || ReadOnly,
        })];
        groups = [.. Lanes.Select(lane => new DateSegmentGroupControl { Segments = lane })];
        separator.Controls.Add(new TextControl { Text = "–", Tone = TextTone.Muted, AutoSize = true });
        Controls.Add(icon);
        Controls.Add(groups[0]);
        if (lanes > 1)
        {
            separator.Visible = true;
            Controls.Add(separator);
            Controls.Add(groups[1]);
        }
        Controls.AddRange([clear, suffix]);
        // 一组时占满剩下的宽度；两组时开始那组按内容宽，结束那组占满（读起来是"开始 – 结束"，空白在最后）
        SetGrow(groups[^1], true);
        clear.Click += (_, _) => Clear();
        foreach (var group in groups)
        {
            group.GotFocus += (_, _) => Invalidate();
            group.LostFocus += (_, _) => Invalidate();
        }
        foreach (Control child in Controls)
        {
            child.MouseEnter += (_, _) => SetHovered(true);
            child.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        }
        Configure();
    }

    /// <summary>有哪些段（按年 → 秒的顺序，显示顺序按语言）；两组用同一套。</summary>
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

    bool ShouldSerializeVariant() => Variant != ControlVariant.Surface;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    bool ShouldSerializeRadius() => Radius != Radius.Md;

    /// <summary>每组分段的状态（开始、结束）。</summary>
    internal IReadOnlyList<DateSegments> Lanes { get; }

    /// <summary>每组分段的控件。</summary>
    internal IReadOnlyList<DateSegmentGroupControl> Groups => groups;

    /// <summary>清空按钮（测试用）。</summary>
    internal ClearButtonControl ClearButton => clear;

    /// <summary>出错：自己写了的优先，其次所在字段的；值超出范围、不可选、两端颠倒时也算。</summary>
    internal bool Invalid => (IsInvalid ?? FieldControl.Of(this)?.Invalid ?? false) || ValueInvalid;

    /// <summary>只读：自己写了的优先，其次所在字段的。</summary>
    private protected bool ReadOnly => IsReadOnly ?? FieldControl.ReadOnlyOf(this);

    bool Required => IsRequired ?? FieldControl.Of(this)?.Required ?? false;

    float Em => this.EmPixels();

    /// <summary>这一组现在的值（派生类从自己的值里取）。</summary>
    private protected abstract DateFieldValues? LaneValue(int lane);

    /// <summary>这一组填全了（ISO 字符串）或删空了（null）：派生类写回自己的值。</summary>
    private protected abstract void LaneChanged(int lane, string? iso);

    /// <summary>有值（清空按钮出现）。</summary>
    private protected abstract bool HasValue { get; }

    /// <summary>值本身不对（超出范围、不可选、两端颠倒）：外壳显示出错。</summary>
    private protected virtual bool ValueInvalid => false;

    /// <summary>这一组的读屏名称：默认是控件或所在字段的名称；两组时派生类加上"开始日期""结束日期"。</summary>
    private protected virtual string? LaneName(int lane, string? name) => name;

    /// <summary>清空之后（派生类写回空值）。</summary>
    private protected abstract void Cleared();

    void IFieldControl.FieldChanged() => Sync();

    void IFormPart.Connect() => Subscribe();

    /// <summary>范围、不可选的条件变了：重新判断出错（派生类用）。</summary>
    private protected void Revalidate() => Sync();

    /// <summary>值从外面变了：各组跟上，再同步状态。</summary>
    private protected void SyncLanes()
    {
        foreach (var lane in Lanes) lane.Sync();
        Sync();
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
        // 属性初始值在构造之前就会写一次
        if (Lanes is null) return;
        var lang = ProviderControl.LangOf(this);
        foreach (var lane in Lanes)
        {
            lane.Fields = Fields;
            lane.Culture = CultureInfo.GetCultureInfo(lang.Code);
            lane.HourCycle = HourCycle ?? lang.HourCycle;
            lane.Placeholders = lang.DateFieldPlaceholders;
            lane.PeriodPatterns = lang.DateFieldPeriodPatterns;
            lane.Sync();
        }
        Sync();
    }

    /// <summary>出错、只读、清空按钮、读屏名称，再让各组分段跟上。</summary>
    private protected void Sync()
    {
        if (Lanes is null) return;
        var field = FieldControl.Of(this) is { } owner && owner.Owns(this) ? owner : null;
        var name = AccessibleName is { Length: > 0 } own ? own : field?.Label;
        var invalid = Invalid;
        for (var lane = 0; lane < groups.Length; lane++)
        {
            var group = groups[lane];
            group.AccessibleName = LaneName(lane, name);
            group.AccessibleDescription = FieldControl.DescribedBy(this, AccessibleDescription);
            group.IsReadOnly = ReadOnly;
            group.IsRequired = Required;
            group.IsInvalid = invalid;
            group.Enabled = Enabled;
        }
        clear.Visible = Clearable && (HasValue || Lanes.Any(lane => lane.Partial)) && Enabled && !ReadOnly;
        Synced();
        foreach (var group in groups) group.Refresh();
        var xs = (int)MathF.Round(Tokens.Space.Xs * Em);
        separator.Padding = new Padding(xs, 0, xs, 0);
        ControlShell.Layout(this, Em);
        ControlShell.FitInnerButton(clear, Em);
        Invalidate();
    }

    /// <summary>状态（只读、可用、出错、值）同步过了：派生类跟着改自己的部件（选择器的打开按钮）。</summary>
    private protected virtual void Synced()
    {
    }

    void Clear()
    {
        foreach (var lane in Lanes) lane.Clear();
        Cleared();
        Lanes[0].SetFocused(FirstSegment(Lanes[0]));
        groups[0].Focus();
        Sync();
    }

    /// <summary>焦点进某一组的某一段（null 时第一个没填的段）。</summary>
    private protected void FocusLane(int lane, DateSegmentType? type = null)
    {
        Lanes[lane].SetFocused(type ?? FirstSegment(Lanes[lane]));
        groups[lane].Focus();
        groups[lane].Refresh();
    }

    /// <summary>点外壳的空白处（图标、内边距、"–"）：焦点进第一个还没填全的那组里第一个没填的段（都填了时第一组的第一段）。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left) return;
        FocusLane(Enumerable.Range(0, Lanes.Count).FirstOrDefault(index => LaneValue(index) is null));
    }

    static DateSegmentType? FirstSegment(DateSegments lane)
    {
        var editable = lane.Editable;
        return (editable.FirstOrDefault(segment => segment.IsPlaceholder) ?? editable.FirstOrDefault())?.Type;
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
        FormReset();
        foreach (var lane in Lanes) lane.Reset();
        Sync();
    }

    /// <summary>所在表单重置了（各组丢掉打了一半的段之前）：两组的选择器把记着的两端放回值。</summary>
    private protected virtual void FormReset()
    {
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
        ControlShell.Paint(e.Graphics, this, Em, Radius, Variant, (ReadOnly, Invalid, hovered, groups.Any(group => group.Focused)));
    }
}
