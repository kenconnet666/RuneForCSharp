using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 分段输入框的外壳（vue-tsx-pro 的 controlParts 加 useFieldSegments）：输入框外壳（同 TextField、Select）里放一组或两组日期分段（<see cref="DateSegmentGroup"/>）、
/// 可选的前置图标、清空按钮与尾部内容。<see cref="SegmentedField"/> 是一组（DateField、TimeField、DatePicker），DateRangePicker 是两组（开始 – 结束）。
/// 这里管与组数无关的部分：外观档位、所在 Field 的标签、说明、出错、只读、必填，表单重置时丢掉打了一半的段，清空，点外壳空白处聚焦；
/// 值怎样读写由派生类给（<see cref="LaneValue"/>、<see cref="LaneChanged"/>）。
/// 外观同输入框：控件高度、左右 space.md、xs 宽的 border 描边（悬停 borderStrong）、md 圆角、surface 底；soft、只读、出错、不可用同 TextField；聚焦在里面时焦点环压住边框。
/// 部件：<see cref="SegmentedFieldParts"/>，默认外观在 SegmentedField.axaml（样式键是 SegmentedField）。
/// </summary>
[TemplatePart(SegmentedFieldParts.Input, typeof(DateSegmentGroup))]
[TemplatePart(SegmentedFieldParts.End, typeof(DateSegmentGroup))]
[TemplatePart(SegmentedFieldParts.Separator, typeof(Control))]
[TemplatePart(SegmentedFieldParts.Clear, typeof(ButtonBase))]
[PseudoClasses(":invalid", ":readonly", ":soft", ":focus-within")]
public abstract class SegmentedFieldBase : TemplatedControl, IFieldControl
{
    /// <summary>有哪些段（按年 → 秒的顺序，显示顺序按语言）；两组用同一套。</summary>
    public static readonly StyledProperty<IReadOnlyList<DateSegmentType>> FieldsProperty =
        AvaloniaProperty.Register<SegmentedFieldBase, IReadOnlyList<DateSegmentType>>(nameof(Fields), [DateSegmentType.Year, DateSegmentType.Month, DateSegmentType.Day]);

    /// <summary>12 / 24 小时制（"h12" / "h23"，有时的段时）；不写时按语言。</summary>
    public static readonly StyledProperty<string?> HourCycleProperty = AvaloniaProperty.Register<SegmentedFieldBase, string?>(nameof(HourCycle));

    /// <summary>样式，默认 surface。</summary>
    public static readonly StyledProperty<ControlVariant> VariantProperty = AvaloniaProperty.Register<SegmentedFieldBase, ControlVariant>(nameof(Variant));

    /// <summary>字号档位；不写时跟着外面。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<SegmentedFieldBase, TextSize>(nameof(Size));

    /// <summary>圆角，默认 md。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<SegmentedFieldBase, Radius>(nameof(Radius));

    /// <summary>前置图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<SegmentedFieldBase, IconData?>(nameof(Icon));

    /// <summary>有值时显示清空按钮。</summary>
    public static readonly StyledProperty<bool> ClearableProperty = AvaloniaProperty.Register<SegmentedFieldBase, bool>(nameof(Clearable));

    /// <summary>框里最后的内容（日期选择器打开日历的按钮）。</summary>
    public static readonly StyledProperty<object?> SuffixProperty = AvaloniaProperty.Register<SegmentedFieldBase, object?>(nameof(Suffix));

    /// <summary>校验未通过；不写时跟着所在的 Field，值超出范围、不可选时也算。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = AvaloniaProperty.Register<SegmentedFieldBase, bool?>(nameof(IsInvalid));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsRequiredProperty = AvaloniaProperty.Register<SegmentedFieldBase, bool?>(nameof(IsRequired));

    /// <summary>只读：能聚焦、能换段，不能改；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsReadOnlyProperty = AvaloniaProperty.Register<SegmentedFieldBase, bool?>(nameof(IsReadOnly));

    readonly TemplateResources resources;
    readonly DateSegmentGroup?[] groups;
    Control? separator;
    ButtonBase? clear;
    IFormController? form;
    int resets;

    // 放进 ControlGroup 时相邻一侧不要圆角
    static SegmentedFieldBase() => Attached.SquareCornersOf<SegmentedFieldBase>();

    /// <summary>新建：外壳的字号、圆角按档位解析成主题资源；每组分段的状态按所在的语言包。</summary>
    /// <param name="lanes">几组分段：1 或 2（开始 – 结束）。</param>
    private protected SegmentedFieldBase(int lanes)
    {
        resources = new TemplateResources(this);
        Resolved.Bind(this, Resolved.RadiusProperty, Radius.Md.ResourceKey);
        groups = new DateSegmentGroup?[lanes];
        Lanes = [.. Enumerable.Range(0, lanes).Select(lane => new DateSegments(Fields)
        {
            Value = () => LaneValue(lane),
            Changed = next => LaneChanged(lane, next is null ? null : DateSegments.ToIso(next, Fields)),
            PlaceholderValue = PlaceholderNow,
            ReadOnly = () => !IsEffectivelyEnabled || ReadOnly,
        })];
        AddHandler(PointerPressedEvent, OnShellPressed, RoutingStrategies.Bubble, handledEventsToo: false);
        Configure();
    }

    /// <summary>有哪些段。</summary>
    public IReadOnlyList<DateSegmentType> Fields { get => GetValue(FieldsProperty); set => SetValue(FieldsProperty, value); }

    /// <summary>12 / 24 小时制。</summary>
    public string? HourCycle { get => GetValue(HourCycleProperty); set => SetValue(HourCycleProperty, value); }

    /// <summary>样式。</summary>
    public ControlVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>有值时显示清空按钮。</summary>
    public bool Clearable { get => GetValue(ClearableProperty); set => SetValue(ClearableProperty, value); }

    /// <summary>框里最后的内容。</summary>
    public object? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>每组分段的状态（开始、结束）。</summary>
    internal IReadOnlyList<DateSegments> Lanes { get; }

    /// <summary>每组分段的控件（模板建好之前是 null）。</summary>
    internal IReadOnlyList<DateSegmentGroup?> Groups => groups;

    /// <summary>出错：自己写了的优先，其次所在字段的；值超出范围、不可选、两端颠倒时也算。</summary>
    internal bool Invalid => (IsInvalid ?? Field.Of(this)?.Invalid ?? false) || ValueInvalid;

    /// <summary>只读：自己写了的优先，其次所在字段的。</summary>
    private protected bool ReadOnly => IsReadOnly ?? Field.ReadOnlyOf(this);

    bool Required => IsRequired ?? Field.Of(this)?.Required ?? false;

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
        var lang = Provider.GetLang(this);
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

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (clear is not null) clear.Click -= OnClear;
        groups[0] = e.NameScope.Find<DateSegmentGroup>(SegmentedFieldParts.Input);
        var end = e.NameScope.Find<DateSegmentGroup>(SegmentedFieldParts.End);
        separator = e.NameScope.Find<Control>(SegmentedFieldParts.Separator);
        if (groups.Length > 1) groups[1] = end;
        // 两组时：开始那组按内容宽，结束那组占满剩下的宽度（读起来是"开始 – 结束"，空白在最后）
        if (end is not null) end.IsVisible = groups.Length > 1;
        if (separator is not null) separator.IsVisible = groups.Length > 1;
        if (groups[0] is { } first) Flex.SetGrow(first, groups.Length == 1);
        clear = e.NameScope.Find<ButtonBase>(SegmentedFieldParts.Clear);
        if (clear is not null) clear.Click += OnClear;
        for (var lane = 0; lane < groups.Length; lane++)
            if (groups[lane] is { } group) group.Segments = Lanes[lane];
        Sync();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            ControlHeight.Apply(this, Size);
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == RadiusProperty) Resolved.Bind(this, Resolved.RadiusProperty, (Radius.Value is null ? Radius.Md : Radius).ResourceKey);
        else if (change.Property == VariantProperty) PseudoClasses.Set(":soft", Variant == ControlVariant.Soft);
        else if (change.Property == FieldsProperty || change.Property == HourCycleProperty || change.Property == Provider.LangProperty) Configure();
        else if (change.Property == IsInvalidProperty || change.Property == IsReadOnlyProperty || change.Property == IsRequiredProperty
                 || change.Property == ClearableProperty || change.Property == IsEnabledProperty || change.Property == Field.OwnerProperty)
            Sync();
        else if (change.Property == IsKeyboardFocusWithinProperty) PseudoClasses.Set(":focus-within", IsKeyboardFocusWithin);
        else if (change.Property == Form.ControllerProperty) Subscribe();
    }

    /// <summary>状态伪类、清空按钮，再让各组分段跟上（读屏名称、只读、出错）。</summary>
    private protected void Sync()
    {
        var invalid = Invalid;
        PseudoClasses.Set(":invalid", invalid);
        PseudoClasses.Set(":readonly", ReadOnly);
        if (clear is not null) clear.IsVisible = Clearable && (HasValue || Lanes.Any(lane => lane.Partial)) && IsEffectivelyEnabled && !ReadOnly;
        Synced();
        var field = Field.Of(this) is { } owner && owner.Owns(this) ? owner : null;
        var name = AutomationProperties.GetName(this) is { Length: > 0 } own ? own : field?.LabelText;
        for (var lane = 0; lane < groups.Length; lane++)
        {
            if (groups[lane] is not { } group) continue;
            AutomationProperties.SetName(group, LaneName(lane, name));
            AutomationProperties.SetHelpText(group, Field.DescribedBy(this, AutomationProperties.GetHelpText(this)));
            group.IsReadOnly = ReadOnly;
            group.IsRequired = Required;
            group.IsInvalid = invalid;
            group.Refresh();
        }
    }

    /// <summary>状态（只读、可用、出错、值）同步过了：派生类跟着改自己的部件（选择器的打开按钮）。</summary>
    private protected virtual void Synced()
    {
    }

    void OnClear(object? sender, RoutedEventArgs e)
    {
        foreach (var lane in Lanes) lane.Clear();
        Cleared();
        Lanes[0].SetFocused(FirstSegment(Lanes[0]));
        Sync();
        FocusLane(0, Lanes[0].FocusedSegment);
        e.Handled = true;
    }

    /// <summary>焦点进某一组的某一段（null 时第一个没填的段）。</summary>
    private protected void FocusLane(int lane, DateSegmentType? type = null)
    {
        var target = type ?? FirstSegment(Lanes[lane]);
        groups[lane]?.Boxes.FirstOrDefault(box => box.Type == target)?.Focus(NavigationMethod.Tab);
    }

    /// <summary>点外壳的空白处（分隔符、内边距）：焦点进第一个还没填全的那组里第一个没填的段（都填了时第一组的第一段）。</summary>
    void OnShellPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || e.Source is DateSegmentBox || groups[0] is null) return;
        if ((e.Source as Visual)?.FindAncestorOfType<ButtonBase>(includeSelf: true) is not null) return;
        var lane = Enumerable.Range(0, Lanes.Count).FirstOrDefault(index => LaneValue(index) is null);
        groups[lane]?.Boxes.FirstOrDefault(box => box.Type == FirstSegment(Lanes[lane]))?.Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    static DateSegmentType? FirstSegment(DateSegments lane)
    {
        var editable = lane.Editable;
        return (editable.FirstOrDefault(segment => segment.IsPlaceholder) ?? editable.FirstOrDefault())?.Type;
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Subscribe();
        Configure();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        Subscribe();
    }

    /// <summary>接上所在表单（重置时丢掉打了一半的段）。</summary>
    void Subscribe()
    {
        var next = ((ILogical)this).IsAttachedToLogicalTree ? Form.GetController(this) : null;
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

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(SegmentedField);

    /// <summary>外壳本身不进读屏树：读屏的主元素是里面的分段组（同上游 role="group" 在分段上）。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);
}
