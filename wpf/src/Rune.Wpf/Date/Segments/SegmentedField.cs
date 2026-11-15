using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Rune.Wpf;

/// <summary>
/// 分段输入框（vue-tsx-pro 的 SegmentedField）：输入框外壳（同 TextField、Select）里放一组日期分段（<see cref="DateSegmentGroup"/>）、
/// 可选的前置图标、清空按钮与尾部内容。<see cref="DateField"/>、<see cref="TimeField"/> 是它的两种用法（只差有哪些段与怎样校验）；日期选择器把打开日历的按钮放在 Suffix 里。
/// 值是 ISO 字符串（<see cref="DateSegments.ToIso"/>，默认双向绑定）；所有段都填了才写回，删掉一段写回 null。点外壳的空白处时焦点进第一个没填的段。
/// 段的顺序、小时制、占位文字按所在的语言包；所在表单重置时丢掉打了一半的段。
/// 外观同输入框：控件高度、左右 space.md、xs 宽的 border 描边（悬停 borderStrong）、md 圆角、surface 底；soft、只读、出错、不可用同 TextField；焦点在里面时焦点环压住边框。
/// 部件名见 <see cref="SegmentedFieldParts"/>，默认外观在 SegmentedField.xaml。
/// </summary>
[TemplatePart(Name = SegmentedFieldParts.Input, Type = typeof(DateSegmentGroup))]
[TemplatePart(Name = SegmentedFieldParts.Clear, Type = typeof(ButtonBase))]
public class SegmentedField : Control, IFieldControl
{
    /// <summary>值（ISO 字符串，默认双向绑定）；没填全时 null。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(SegmentedField),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((SegmentedField)d).OnValueChanged()));

    /// <summary>有哪些段（按年 → 秒的顺序，显示顺序按语言）。</summary>
    public static readonly DependencyProperty FieldsProperty = DependencyProperty.Register(nameof(Fields), typeof(IReadOnlyList<DateSegmentType>), typeof(SegmentedField),
        new PropertyMetadata(new[] { DateSegmentType.Year, DateSegmentType.Month, DateSegmentType.Day }, (d, _) => ((SegmentedField)d).Configure()));

    /// <summary>12 / 24 小时制（"h12" / "h23"，有时的段时）；不写时按语言。</summary>
    public static readonly DependencyProperty HourCycleProperty = DependencyProperty.Register(nameof(HourCycle), typeof(string), typeof(SegmentedField),
        new PropertyMetadata(null, (d, _) => ((SegmentedField)d).Configure()));

    /// <summary>样式，默认 surface。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(ControlVariant), typeof(SegmentedField),
        new FrameworkPropertyMetadata(ControlVariant.Surface));

    /// <summary>字号档位；不写时跟着外面。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(SegmentedField));

    /// <summary>圆角，默认 md。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(Radius), typeof(SegmentedField),
        new FrameworkPropertyMetadata(Radius.Md));

    /// <summary>外壳的圆角（按 <see cref="Radius"/> 与字号算出，主题样式写系数）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(SegmentedField),
        new FrameworkPropertyMetadata(default(CornerRadius), null, EmSize.CoerceCornerRadius));

    /// <summary>前置图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(SegmentedField));

    /// <summary>有值时显示清空按钮。</summary>
    public static readonly DependencyProperty ClearableProperty = Register(nameof(Clearable), typeof(bool), false);

    /// <summary>框里最后的内容（日期选择器打开日历的按钮）。</summary>
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(nameof(Suffix), typeof(object), typeof(SegmentedField));

    /// <summary>校验未通过；不写时跟着所在的 Field，值超出范围、不可选时也算。</summary>
    public static readonly DependencyProperty IsInvalidProperty = Register(nameof(IsInvalid), typeof(bool?), null);

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsRequiredProperty = Register(nameof(IsRequired), typeof(bool?), null);

    /// <summary>只读：能聚焦、能换段，不能改；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = Register(nameof(IsReadOnly), typeof(bool?), null);

    static readonly DependencyPropertyKey ShowsInvalidKey = DependencyProperty.RegisterReadOnly(nameof(ShowsInvalid), typeof(bool), typeof(SegmentedField), new PropertyMetadata(false));

    /// <summary>显示成出错（主题样式的触发器用）。</summary>
    public static readonly DependencyProperty ShowsInvalidProperty = ShowsInvalidKey.DependencyProperty;

    static readonly DependencyPropertyKey ShowsReadOnlyKey = DependencyProperty.RegisterReadOnly(nameof(ShowsReadOnly), typeof(bool), typeof(SegmentedField), new PropertyMetadata(false));

    /// <summary>显示成只读（主题样式的触发器用）。</summary>
    public static readonly DependencyProperty ShowsReadOnlyProperty = ShowsReadOnlyKey.DependencyProperty;

    DateSegmentGroup? group;
    ButtonBase? clear;
    IFormController? form;
    int resets;

    static DependencyProperty Register(string name, Type type, object? fallback) => DependencyProperty.Register(
        name, type, typeof(SegmentedField), new PropertyMetadata(fallback, (d, _) => ((SegmentedField)d).Sync()));

    static SegmentedField()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SegmentedField), new FrameworkPropertyMetadata(typeof(SegmentedField)));
        EmSize.Register(typeof(SegmentedField), CornerRadiusProperty);
        IsEnabledProperty.OverrideMetadata(typeof(SegmentedField), new FrameworkPropertyMetadata((d, _) => ((SegmentedField)d).Sync()));
        Form.ControllerProperty.OverrideMetadata(typeof(SegmentedField), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((SegmentedField)d).Subscribe()));
    }

    /// <summary>新建：分段状态按所在的语言包。</summary>
    public SegmentedField()
    {
        Segments = new DateSegments(Fields)
        {
            Value = () => DateSegments.FromIso(Value, Fields),
            Changed = next => SetCurrentValue(ValueProperty, next is null ? null : DateSegments.ToIso(next, Fields)),
            PlaceholderValue = PlaceholderNow,
            ReadOnly = () => !IsEnabled || ReadOnly,
        };
        AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnShellPressed));
        Loaded += (_, _) =>
        {
            Subscribe();
            Configure();
        };
        Unloaded += (_, _) => Subscribe();
        Configure();
    }

    /// <summary>值（ISO 字符串）。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>有哪些段。</summary>
    public IReadOnlyList<DateSegmentType> Fields { get => (IReadOnlyList<DateSegmentType>)GetValue(FieldsProperty); set => SetValue(FieldsProperty, value); }

    /// <summary>12 / 24 小时制。</summary>
    public string? HourCycle { get => (string?)GetValue(HourCycleProperty); set => SetValue(HourCycleProperty, value); }

    /// <summary>样式。</summary>
    public ControlVariant Variant { get => (ControlVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>外壳的圆角。</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>有值时显示清空按钮。</summary>
    public bool Clearable { get => (bool)GetValue(ClearableProperty); set => SetValue(ClearableProperty, value); }

    /// <summary>框里最后的内容。</summary>
    public object? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => (bool?)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => (bool?)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => (bool?)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>显示成出错。</summary>
    public bool ShowsInvalid => (bool)GetValue(ShowsInvalidProperty);

    /// <summary>显示成只读。</summary>
    public bool ShowsReadOnly => (bool)GetValue(ShowsReadOnlyProperty);

    /// <summary>分段状态（测试与派生类用）。</summary>
    internal DateSegments Segments { get; }

    /// <summary>这一组分段（测试用）。</summary>
    internal DateSegmentGroup? Group => group;

    /// <summary>清空按钮（测试用）。</summary>
    internal ButtonBase? ClearButton => clear;

    /// <summary>出错：自己写了的优先，其次所在字段的；有值且超出范围、不可选时也算。</summary>
    internal bool Invalid => (IsInvalid ?? Field.Of(this)?.Invalid ?? false) || Value is { } iso && IsOutOfRange(iso);

    bool ReadOnly => IsReadOnly ?? Field.ReadOnlyOf(this);

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
        if (Segments is null) return;
        var lang = Provider.GetLang(this);
        Segments.Fields = Fields;
        Segments.Culture = CultureInfo.GetCultureInfo(lang.Code);
        Segments.HourCycle = HourCycle ?? lang.HourCycle;
        Segments.Placeholders = lang.DateFieldPlaceholders;
        Segments.PeriodPatterns = lang.DateFieldPeriodPatterns;
        Segments.Sync();
        Sync();
    }

    void OnValueChanged()
    {
        Segments.Sync();
        Sync();
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (clear is not null) clear.Click -= OnClear;
        group = GetTemplateChild(SegmentedFieldParts.Input) as DateSegmentGroup;
        clear = GetTemplateChild(SegmentedFieldParts.Clear) as ButtonBase;
        if (clear is not null) clear.Click += OnClear;
        if (group is not null) group.Segments = Segments;
        Sync();
    }

    /// <summary>状态（出错、只读）、清空按钮，再让分段跟上（读屏名称、只读、出错）。</summary>
    void Sync()
    {
        if (Segments is null) return;
        var invalid = Invalid;
        SetValue(ShowsInvalidKey, invalid);
        SetValue(ShowsReadOnlyKey, ReadOnly);
        if (clear is not null) clear.Visibility = Clearable && (Value is not null || Segments.Partial) && IsEnabled && !ReadOnly ? Visibility.Visible : Visibility.Collapsed;
        if (group is null) return;
        var field = Field.Of(this) is { } owner && owner.Owns(this) ? owner : null;
        var name = AutomationProperties.GetName(this) is { Length: > 0 } own ? own : field?.LabelText;
        AutomationProperties.SetName(group, name ?? "");
        AutomationProperties.SetHelpText(group, Field.DescribedBy(this, AutomationProperties.GetHelpText(this)));
        group.IsReadOnly = ReadOnly;
        group.IsRequired = Required;
        group.IsInvalid = invalid;
        group.Refresh();
    }

    void OnClear(object sender, RoutedEventArgs e)
    {
        Segments.Clear();
        SetCurrentValue(ValueProperty, null);
        Segments.SetFocused(FirstSegment());
        Sync();
        group?.Boxes.FirstOrDefault(box => box.Type == Segments.FocusedSegment)?.Focus();
        e.Handled = true;
    }

    /// <summary>点外壳的空白处（分隔符、内边距）：焦点进第一个没填的段（都填了时第一段）。</summary>
    void OnShellPressed(object sender, MouseButtonEventArgs e)
    {
        if (!IsEnabled || group is null || e.OriginalSource is DependencyObject source && (Within<DateSegmentBox>(source) || Within<ButtonBase>(source))) return;
        var target = FirstSegment();
        group.Boxes.FirstOrDefault(box => box.Type == target)?.Focus();
        e.Handled = true;

        static bool Within<T>(DependencyObject node)
        {
            for (DependencyObject? current = node; current is not null; current = current is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
                if (current is T) return true;
            return false;
        }
    }

    DateSegmentType? FirstSegment()
    {
        var editable = Segments.Editable;
        return (editable.FirstOrDefault(segment => segment.IsPlaceholder) ?? editable.FirstOrDefault())?.Type;
    }

    /// <summary>所在表单重置时（ResetCount 变了）丢掉打了一半的段（上游 useFormReset）。</summary>
    void Subscribe()
    {
        var next = IsLoaded ? Form.GetController(this) : null;
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

    /// <summary>读屏不经过外壳：这一组分段（group）是主元素。</summary>
    protected override AutomationPeer? OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}

/// <summary>SegmentedField 的部件名（模板里的元素）。外层（TS 的 root）是 Border PART_Root。</summary>
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
