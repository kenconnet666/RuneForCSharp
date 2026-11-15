using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Rune.Wpf;

/// <summary>
/// 分段输入框的外壳（vue-tsx-pro 的 controlParts 加 useFieldSegments）：输入框外壳（同 TextField、Select）里放一组或两组日期分段（<see cref="DateSegmentGroup"/>）、
/// 可选的前置图标、清空按钮与尾部内容。<see cref="SegmentedField"/> 是一组（DateField、TimeField、DatePicker），<see cref="DateRangePicker"/> 是两组（开始 – 结束）。
/// 这里管与组数无关的部分：外观档位、所在 Field 的标签、说明、出错、只读、必填，表单重置时丢掉打了一半的段，清空，点外壳空白处聚焦；
/// 值怎样读写由派生类给（<see cref="LaneValue"/>、<see cref="LaneChanged"/>）。
/// 外观同输入框：控件高度、左右 space.md、xs 宽的 border 描边（悬停 borderStrong）、md 圆角、surface 底；soft、只读、出错、不可用同 TextField；焦点在里面时焦点环压住边框。
/// 部件名见 <see cref="SegmentedFieldParts"/>，默认外观在 SegmentedField.xaml（样式键是 SegmentedField）。
/// </summary>
[TemplatePart(Name = SegmentedFieldParts.Input, Type = typeof(DateSegmentGroup))]
[TemplatePart(Name = SegmentedFieldParts.End, Type = typeof(DateSegmentGroup))]
[TemplatePart(Name = SegmentedFieldParts.Separator, Type = typeof(FrameworkElement))]
[TemplatePart(Name = SegmentedFieldParts.Clear, Type = typeof(ButtonBase))]
public abstract class SegmentedFieldBase : Control, IFieldControl
{
    /// <summary>有哪些段（按年 → 秒的顺序，显示顺序按语言）；两组用同一套。</summary>
    public static readonly DependencyProperty FieldsProperty = DependencyProperty.Register(nameof(Fields), typeof(IReadOnlyList<DateSegmentType>), typeof(SegmentedFieldBase),
        new PropertyMetadata(new[] { DateSegmentType.Year, DateSegmentType.Month, DateSegmentType.Day }, (d, _) => ((SegmentedFieldBase)d).Configure()));

    /// <summary>12 / 24 小时制（"h12" / "h23"，有时的段时）；不写时按语言。</summary>
    public static readonly DependencyProperty HourCycleProperty = DependencyProperty.Register(nameof(HourCycle), typeof(string), typeof(SegmentedFieldBase),
        new PropertyMetadata(null, (d, _) => ((SegmentedFieldBase)d).Configure()));

    /// <summary>样式，默认 surface。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(ControlVariant), typeof(SegmentedFieldBase),
        new FrameworkPropertyMetadata(ControlVariant.Surface));

    /// <summary>字号档位；不写时跟着外面。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(SegmentedFieldBase));

    /// <summary>圆角，默认 md。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(Radius), typeof(SegmentedFieldBase),
        new FrameworkPropertyMetadata(Radius.Md));

    /// <summary>外壳的圆角（按 <see cref="Radius"/> 与字号算出，主题样式写系数）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(SegmentedFieldBase),
        new FrameworkPropertyMetadata(default(CornerRadius), null, EmSize.CoerceCornerRadius));

    /// <summary>前置图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(SegmentedFieldBase));

    /// <summary>有值时显示清空按钮。</summary>
    public static readonly DependencyProperty ClearableProperty = Register(nameof(Clearable), typeof(bool), false);

    /// <summary>框里最后的内容（日期选择器打开日历的按钮）。</summary>
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(nameof(Suffix), typeof(object), typeof(SegmentedFieldBase));

    /// <summary>校验未通过；不写时跟着所在的 Field，值超出范围、不可选时也算。</summary>
    public static readonly DependencyProperty IsInvalidProperty = Register(nameof(IsInvalid), typeof(bool?), null);

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsRequiredProperty = Register(nameof(IsRequired), typeof(bool?), null);

    /// <summary>只读：能聚焦、能换段，不能改；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = Register(nameof(IsReadOnly), typeof(bool?), null);

    static readonly DependencyPropertyKey ShowsInvalidKey = DependencyProperty.RegisterReadOnly(nameof(ShowsInvalid), typeof(bool), typeof(SegmentedFieldBase), new PropertyMetadata(false));

    /// <summary>显示成出错（主题样式的触发器用）。</summary>
    public static readonly DependencyProperty ShowsInvalidProperty = ShowsInvalidKey.DependencyProperty;

    static readonly DependencyPropertyKey ShowsReadOnlyKey = DependencyProperty.RegisterReadOnly(nameof(ShowsReadOnly), typeof(bool), typeof(SegmentedFieldBase), new PropertyMetadata(false));

    /// <summary>显示成只读（主题样式的触发器用）。</summary>
    public static readonly DependencyProperty ShowsReadOnlyProperty = ShowsReadOnlyKey.DependencyProperty;

    readonly DateSegmentGroup?[] groups;
    FrameworkElement? separator;
    ButtonBase? clear;
    IFormController? form;
    int resets;

    static DependencyProperty Register(string name, Type type, object? fallback) => DependencyProperty.Register(
        name, type, typeof(SegmentedFieldBase), new PropertyMetadata(fallback, (d, _) => ((SegmentedFieldBase)d).Sync()));

    static SegmentedFieldBase()
    {
        // 一组、两组共用 SegmentedField 的主题样式
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SegmentedFieldBase), new FrameworkPropertyMetadata(typeof(SegmentedField)));
        EmSize.Register(typeof(SegmentedFieldBase), CornerRadiusProperty);
        IsEnabledProperty.OverrideMetadata(typeof(SegmentedFieldBase), new FrameworkPropertyMetadata((d, _) => ((SegmentedFieldBase)d).Sync()));
        Form.ControllerProperty.OverrideMetadata(typeof(SegmentedFieldBase), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((SegmentedFieldBase)d).Subscribe()));
    }

    /// <summary>新建：每组分段的状态按所在的语言包。</summary>
    /// <param name="lanes">几组分段：1 或 2（开始 – 结束）。</param>
    private protected SegmentedFieldBase(int lanes)
    {
        groups = new DateSegmentGroup?[lanes];
        Lanes = [.. Enumerable.Range(0, lanes).Select(lane => new DateSegments(Fields)
        {
            Value = () => LaneValue(lane),
            Changed = next => LaneChanged(lane, next is null ? null : DateSegments.ToIso(next, Fields)),
            PlaceholderValue = PlaceholderNow,
            ReadOnly = () => !IsEnabled || ReadOnly,
        })];
        AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnShellPressed));
        Loaded += (_, _) =>
        {
            Subscribe();
            Configure();
        };
        Unloaded += (_, _) => Subscribe();
        Configure();
    }

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

    /// <summary>每组分段的状态（开始、结束）。</summary>
    internal IReadOnlyList<DateSegments> Lanes { get; }

    /// <summary>每组分段的控件（模板建好之前是 null）。</summary>
    internal IReadOnlyList<DateSegmentGroup?> Groups => groups;

    /// <summary>清空按钮（测试用）。</summary>
    internal ButtonBase? ClearButton => clear;

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
        // 依赖属性的默认值在构造之前就可能触发回调
        if (Lanes is null) return;
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
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (clear is not null) clear.Click -= OnClear;
        groups[0] = GetTemplateChild(SegmentedFieldParts.Input) as DateSegmentGroup;
        var end = GetTemplateChild(SegmentedFieldParts.End) as DateSegmentGroup;
        separator = GetTemplateChild(SegmentedFieldParts.Separator) as FrameworkElement;
        if (groups.Length > 1) groups[1] = end;
        // 两组时：开始那组按内容宽，结束那组占满剩下的宽度（读起来是"开始 – 结束"，空白在最后）
        var visibility = groups.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (end is not null) end.Visibility = visibility;
        if (separator is not null) separator.Visibility = visibility;
        if (groups[0] is { } first) Flex.SetGrow(first, groups.Length == 1);
        SpaceSeparator();
        clear = GetTemplateChild(SegmentedFieldParts.Clear) as ButtonBase;
        if (clear is not null) clear.Click += OnClear;
        for (var lane = 0; lane < groups.Length; lane++)
            if (groups[lane] is { } group) group.Segments = Lanes[lane];
        Sync();
    }

    /// <summary>"–"左右 xs（按字号，模板里写不了 em 的外边距）。</summary>
    void SpaceSeparator()
    {
        if (separator is not null) separator.Margin = new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * FontSize, 0, this.Resource<double>(ThemeKeys.Space.Xs) * FontSize, 0);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) SpaceSeparator();
    }

    /// <summary>状态（出错、只读）、清空按钮，再让各组分段跟上（读屏名称、只读、出错）。</summary>
    private protected void Sync()
    {
        if (Lanes is null) return;
        var invalid = Invalid;
        SetValue(ShowsInvalidKey, invalid);
        SetValue(ShowsReadOnlyKey, ReadOnly);
        if (clear is not null) clear.Visibility = Clearable && (HasValue || Lanes.Any(lane => lane.Partial)) && IsEnabled && !ReadOnly ? Visibility.Visible : Visibility.Collapsed;
        Synced();
        var field = Field.Of(this) is { } owner && owner.Owns(this) ? owner : null;
        var name = AutomationProperties.GetName(this) is { Length: > 0 } own ? own : field?.LabelText;
        for (var lane = 0; lane < groups.Length; lane++)
        {
            if (groups[lane] is not { } group) continue;
            AutomationProperties.SetName(group, LaneName(lane, name) ?? "");
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

    void OnClear(object sender, RoutedEventArgs e)
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
        groups[lane]?.Boxes.FirstOrDefault(box => box.Type == target)?.Focus();
    }

    /// <summary>点外壳的空白处（分隔符、内边距）：焦点进第一个还没填全的那组里第一个没填的段（都填了时第一组的第一段）。</summary>
    void OnShellPressed(object sender, MouseButtonEventArgs e)
    {
        if (!IsEnabled || groups[0] is null || e.OriginalSource is DependencyObject source && (Within<DateSegmentBox>(source) || Within<ButtonBase>(source))) return;
        var lane = Enumerable.Range(0, Lanes.Count).FirstOrDefault(index => LaneValue(index) is null);
        FocusLane(lane);
        e.Handled = true;

        static bool Within<T>(DependencyObject node)
        {
            for (DependencyObject? current = node; current is not null; current = current is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
                if (current is T) return true;
            return false;
        }
    }

    static DateSegmentType? FirstSegment(DateSegments lane)
    {
        var editable = lane.Editable;
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
        FormReset();
        foreach (var lane in Lanes) lane.Reset();
        Sync();
    }

    /// <summary>所在表单重置了（各组丢掉打了一半的段之前）：两组的选择器把记着的两端放回值。</summary>
    private protected virtual void FormReset()
    {
    }

    /// <summary>读屏不经过外壳：分段组是主元素。</summary>
    protected override AutomationPeer? OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}
