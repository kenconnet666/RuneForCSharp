using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 分段输入框（vue-tsx-pro 的 SegmentedField）：输入框外壳（同 TextField、Select）里放一组日期分段（<see cref="DateSegmentGroup"/>）、
/// 可选的前置图标、清空按钮与尾部内容。<see cref="DateField"/>、<see cref="TimeField"/> 是它的两种用法（只差有哪些段与怎样校验）；日期选择器把打开日历的按钮放在 Suffix 里。
/// <code>
/// &lt;rune:SegmentedField Fields="Hour,Minute" Value="{Binding Time}" /&gt;
/// </code>
/// 值是 ISO 字符串（<see cref="DateSegments.ToIso"/>）；所有段都填了才写回，删掉一段写回 null。点外壳的空白处时焦点进第一个没填的段。
/// 段的顺序、小时制、占位文字按所在的语言包（Lang.Code、HourCycle、DateFieldPlaceholders）；所在表单重置时丢掉打了一半的段。
/// 外观同输入框：控件高度、左右 space.md、xs 宽的 border 描边（悬停 borderStrong）、md 圆角、surface 底；soft、只读、出错、不可用同 TextField；聚焦在里面时焦点环压住边框。
/// 部件：<see cref="SegmentedFieldParts"/>，默认外观在 SegmentedField.axaml。
/// </summary>
[TemplatePart(SegmentedFieldParts.Input, typeof(DateSegmentGroup))]
[TemplatePart(SegmentedFieldParts.Clear, typeof(ButtonBase))]
[PseudoClasses(":invalid", ":readonly", ":soft", ":focus-within")]
public class SegmentedField : TemplatedControl, IFieldControl
{
    /// <summary>值（ISO 字符串，默认双向绑定）；没填全时 null。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<SegmentedField, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>有哪些段（按年 → 秒的顺序，显示顺序按语言）。</summary>
    public static readonly StyledProperty<IReadOnlyList<DateSegmentType>> FieldsProperty =
        AvaloniaProperty.Register<SegmentedField, IReadOnlyList<DateSegmentType>>(nameof(Fields), [DateSegmentType.Year, DateSegmentType.Month, DateSegmentType.Day]);

    /// <summary>12 / 24 小时制（"h12" / "h23"，有时的段时）；不写时按语言。</summary>
    public static readonly StyledProperty<string?> HourCycleProperty = AvaloniaProperty.Register<SegmentedField, string?>(nameof(HourCycle));

    /// <summary>样式，默认 surface。</summary>
    public static readonly StyledProperty<ControlVariant> VariantProperty = AvaloniaProperty.Register<SegmentedField, ControlVariant>(nameof(Variant));

    /// <summary>字号档位；不写时跟着外面。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<SegmentedField, TextSize>(nameof(Size));

    /// <summary>圆角，默认 md。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<SegmentedField, Radius>(nameof(Radius));

    /// <summary>前置图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<SegmentedField, IconData?>(nameof(Icon));

    /// <summary>有值时显示清空按钮。</summary>
    public static readonly StyledProperty<bool> ClearableProperty = AvaloniaProperty.Register<SegmentedField, bool>(nameof(Clearable));

    /// <summary>框里最后的内容（日期选择器打开日历的按钮）。</summary>
    public static readonly StyledProperty<object?> SuffixProperty = AvaloniaProperty.Register<SegmentedField, object?>(nameof(Suffix));

    /// <summary>校验未通过；不写时跟着所在的 Field，值超出范围、不可选时也算。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = AvaloniaProperty.Register<SegmentedField, bool?>(nameof(IsInvalid));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsRequiredProperty = AvaloniaProperty.Register<SegmentedField, bool?>(nameof(IsRequired));

    /// <summary>只读：能聚焦、能换段，不能改；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsReadOnlyProperty = AvaloniaProperty.Register<SegmentedField, bool?>(nameof(IsReadOnly));

    readonly TemplateResources resources;
    DateSegmentGroup? group;
    ButtonBase? clear;
    IFormController? form;
    int resets;

    // 放进 ControlGroup 时相邻一侧不要圆角
    static SegmentedField() => Attached.SquareCornersOf<SegmentedField>();

    /// <summary>新建：外壳的字号、圆角按档位解析成主题资源；分段状态按所在的语言包。</summary>
    public SegmentedField()
    {
        resources = new TemplateResources(this);
        Resolved.Bind(this, Resolved.RadiusProperty, Radius.Md.ResourceKey);
        Segments = new DateSegments(Fields)
        {
            Value = () => DateSegments.FromIso(Value, Fields),
            Changed = next => SetCurrentValue(ValueProperty, next is null ? null : DateSegments.ToIso(next, Fields)),
            PlaceholderValue = PlaceholderNow,
            ReadOnly = () => !IsEffectivelyEnabled || ReadOnly,
        };
        AddHandler(PointerPressedEvent, OnShellPressed, RoutingStrategies.Bubble, handledEventsToo: false);
        Configure();
    }

    /// <summary>值（ISO 字符串）。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

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

    /// <summary>分段状态（测试与派生类用）。</summary>
    internal DateSegments Segments { get; }

    /// <summary>这一组分段（测试用）。</summary>
    internal DateSegmentGroup? Group => group;

    /// <summary>出错：自己写了的优先，其次所在字段的；有值且超出范围、不可选时也算。</summary>
    internal bool Invalid => (IsInvalid ?? Field.Of(this)?.Invalid ?? false) || Value is { } iso && IsOutOfRange(iso);

    /// <summary>只读：自己写了的优先，其次所在字段的。</summary>
    private protected bool ReadOnly => IsReadOnly ?? Field.ReadOnlyOf(this);

    bool Required => IsRequired ?? Field.Of(this)?.Required ?? false;

    /// <summary>值超出范围、是不可选的日期或时刻：外壳显示出错，值照样写回（由表单校验决定能不能提交）。</summary>
    /// <param name="iso">填好的 ISO 字符串。</param>
    protected virtual bool IsOutOfRange(string iso) => false;

    void IFieldControl.FieldChanged() => Sync();

    /// <summary>范围、不可选的条件变了：重新判断出错（派生类用）。</summary>
    private protected void Revalidate() => Sync();

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
        Segments.Fields = Fields;
        Segments.Culture = CultureInfo.GetCultureInfo(lang.Code);
        Segments.HourCycle = HourCycle ?? lang.HourCycle;
        Segments.Placeholders = lang.DateFieldPlaceholders;
        Segments.PeriodPatterns = lang.DateFieldPeriodPatterns;
        Segments.Sync();
        Sync();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (clear is not null) clear.Click -= OnClear;
        group = e.NameScope.Find<DateSegmentGroup>(SegmentedFieldParts.Input);
        clear = e.NameScope.Find<ButtonBase>(SegmentedFieldParts.Clear);
        if (clear is not null) clear.Click += OnClear;
        if (group is not null) group.Segments = Segments;
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
        else if (change.Property == ValueProperty)
        {
            Segments.Sync();
            Sync();
        }
        else if (change.Property == IsInvalidProperty || change.Property == IsReadOnlyProperty || change.Property == IsRequiredProperty
                 || change.Property == ClearableProperty || change.Property == IsEnabledProperty || change.Property == Field.OwnerProperty)
            Sync();
        else if (change.Property == IsKeyboardFocusWithinProperty) PseudoClasses.Set(":focus-within", IsKeyboardFocusWithin);
        else if (change.Property == Form.ControllerProperty) Subscribe();
    }

    /// <summary>状态伪类、清空按钮，再让分段跟上（读屏名称、只读、出错）。</summary>
    void Sync()
    {
        var invalid = Invalid;
        PseudoClasses.Set(":invalid", invalid);
        PseudoClasses.Set(":readonly", ReadOnly);
        if (clear is not null) clear.IsVisible = Clearable && (Value is not null || Segments.Partial) && IsEffectivelyEnabled && !ReadOnly;
        Synced();
        if (group is null) return;
        var field = Field.Of(this) is { } owner && owner.Owns(this) ? owner : null;
        var name = AutomationProperties.GetName(this) is { Length: > 0 } own ? own : field?.LabelText;
        AutomationProperties.SetName(group, name);
        AutomationProperties.SetHelpText(group, Field.DescribedBy(this, AutomationProperties.GetHelpText(this)));
        group.IsReadOnly = ReadOnly;
        group.IsRequired = Required;
        group.IsInvalid = invalid;
        group.Refresh();
    }

    /// <summary>状态（只读、可用、出错、值）同步过了：派生类跟着改自己的部件（选择器的打开按钮）。</summary>
    private protected virtual void Synced()
    {
    }

    void OnClear(object? sender, RoutedEventArgs e)
    {
        Segments.Clear();
        SetCurrentValue(ValueProperty, null);
        Segments.SetFocused(FirstSegment());
        Sync();
        group?.Boxes.FirstOrDefault(box => box.Type == Segments.FocusedSegment)?.Focus(NavigationMethod.Tab);
        e.Handled = true;
    }

    /// <summary>点外壳的空白处（分隔符、内边距）：焦点进第一个没填的段（都填了时第一段）。</summary>
    void OnShellPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || e.Source is DateSegmentBox || group is null) return;
        if ((e.Source as Visual)?.FindAncestorOfType<ButtonBase>(includeSelf: true) is not null) return;
        var target = FirstSegment();
        group.Boxes.FirstOrDefault(box => box.Type == target)?.Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    DateSegmentType? FirstSegment()
    {
        var editable = Segments.Editable;
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

    /// <summary>所在表单重置时（ResetCount 变了）丢掉打了一半的段（上游 useFormReset）。</summary>
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
        Segments.Reset();
        Sync();
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(SegmentedField);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);
}

/// <summary>SegmentedField 的部件名（模板里的元素）。外层（TS 的 root）是 Border#PART_Root。</summary>
public static class SegmentedFieldParts
{
    /// <summary>外壳（TS 的 propsRoot）：Border。</summary>
    public const string Root = "PART_Root";

    /// <summary>这一组分段（TS 的主元素）：DateSegmentGroup。</summary>
    public const string Input = "PART_Input";

    /// <summary>前置图标（TS 的 propsIcon）：Icon，textMuted。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>清空按钮（TS 的 propsClear）：ClearButton。</summary>
    public const string Clear = "PART_Clear";

    /// <summary>尾部内容：ContentPresenter。</summary>
    public const string Suffix = "PART_Suffix";
}
