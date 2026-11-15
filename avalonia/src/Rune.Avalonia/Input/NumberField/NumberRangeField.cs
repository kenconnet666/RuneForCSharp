using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;

namespace Rune.Avalonia;

/// <summary>数值区间的值（vue-tsx-pro 的 NumberRange）：起与止，空着的一端是 null（"10 万元以上"）。</summary>
/// <param name="Start">起；没填时 null。</param>
/// <param name="End">止；没填时 null。</param>
public readonly record struct NumberRange(decimal? Start, decimal? End)
{
    /// <summary>两端都空着。</summary>
    public static readonly NumberRange Empty = new(null, null);

    /// <summary>改一端：两端都有值且起比止大时对调（不用报错）。</summary>
    /// <param name="end">改的是止（false 时是起）。</param>
    /// <param name="value">新的值。</param>
    public NumberRange With(bool end, decimal? value)
    {
        var (start, stop) = end ? (Start, value) : (value, End);
        return start is { } a && stop is { } b && a > b ? new NumberRange(b, a) : new NumberRange(start, stop);
    }
}

/// <summary>
/// 数值区间（vue-tsx-pro 的 NumberRangeField）：两个 <see cref="NumberField"/>，最小到最大。预期收益量级、金额范围、数量区间的录入与筛选。
/// <code>
/// &lt;rune:Field Label="预期收益量级"&gt;&lt;rune:NumberRangeField Value="{Binding Saving}" Suffix="万元" Min="0" /&gt;&lt;/rune:Field&gt;
/// </code>
/// 值是 <see cref="NumberRange"/>（默认双向绑定），任一端可以为空。两端都填了、起比止大时自动对调。
/// Min、Max、Step、Format、Prefix、Suffix 与外观（Variant、Size、Radius）两端共用，同 NumberField；两个框不带加减按钮。
/// 在 Field 里时：点标签聚焦第一个框，这一组以标签为名称、带说明与错误，只有第一个框跟着必填；两个框各自念"最小值""最大值"。
/// 部件：<see cref="NumberRangeFieldParts"/>。
/// </summary>
public class NumberRangeField : Control, IFieldControl
{
    /// <summary>[起, 止]（默认双向绑定）。</summary>
    public static readonly StyledProperty<NumberRange> ValueProperty = AvaloniaProperty.Register<NumberRangeField, NumberRange>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>两端共同的最小值。</summary>
    public static readonly StyledProperty<decimal?> MinProperty = NumberField.MinProperty.AddOwner<NumberRangeField>();

    /// <summary>两端共同的最大值。</summary>
    public static readonly StyledProperty<decimal?> MaxProperty = NumberField.MaxProperty.AddOwner<NumberRangeField>();

    /// <summary>步长，默认 1。</summary>
    public static readonly StyledProperty<decimal> StepProperty = NumberField.StepProperty.AddOwner<NumberRangeField>();

    /// <summary>显示格式，同 NumberField（Scale 时按缩放后的单位输入）。</summary>
    public static readonly StyledProperty<NumericFormat?> FormatProperty = NumberField.FormatProperty.AddOwner<NumberRangeField>();

    /// <summary>框里文字前的内容。</summary>
    public static readonly StyledProperty<string?> PrefixProperty = NumberField.PrefixProperty.AddOwner<NumberRangeField>();

    /// <summary>框里文字后的单位。</summary>
    public static readonly StyledProperty<string?> SuffixProperty = NumberField.SuffixProperty.AddOwner<NumberRangeField>();

    /// <summary>起那个框的占位文字；不写时取语言包（最小值）。</summary>
    public static readonly StyledProperty<string?> StartPlaceholderProperty = AvaloniaProperty.Register<NumberRangeField, string?>(nameof(StartPlaceholder));

    /// <summary>止那个框的占位文字；不写时取语言包（最大值）。</summary>
    public static readonly StyledProperty<string?> EndPlaceholderProperty = AvaloniaProperty.Register<NumberRangeField, string?>(nameof(EndPlaceholder));

    /// <summary>两端中间的文字，默认"–"。</summary>
    public static readonly StyledProperty<string> SeparatorProperty = AvaloniaProperty.Register<NumberRangeField, string>(nameof(Separator), "–");

    /// <summary>样式，同输入框。</summary>
    public static readonly StyledProperty<ControlVariant> VariantProperty = AvaloniaProperty.Register<NumberRangeField, ControlVariant>(nameof(Variant));

    /// <summary>字号档位；不写时跟着外面。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<NumberRangeField, TextSize>(nameof(Size));

    /// <summary>圆角，同输入框。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<NumberRangeField, Radius>(nameof(Radius));

    /// <summary>校验未通过；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = AvaloniaProperty.Register<NumberRangeField, bool?>(nameof(IsInvalid));

    /// <summary>必填（只落在第一个框上）；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsRequiredProperty = AvaloniaProperty.Register<NumberRangeField, bool?>(nameof(IsRequired));

    /// <summary>只读；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsReadOnlyProperty = AvaloniaProperty.Register<NumberRangeField, bool?>(nameof(IsReadOnly));

    readonly Flex root = new() { Align = FlexAlign.Center, Gap = Space.Sm };
    readonly NumberField start = new() { Name = NumberRangeFieldParts.Start, Stepper = false };
    readonly Text separator = new() { Name = NumberRangeFieldParts.Separator, Tone = TextTone.Muted };
    readonly NumberField end = new() { Name = NumberRangeFieldParts.End, Stepper = false };
    bool syncing;

    /// <summary>新建：两个框中间一个"–"，两个框平分宽度。</summary>
    public NumberRangeField()
    {
        Flex.SetGrow(start, true);
        Flex.SetGrow(end, true);
        AutomationProperties.SetAccessibilityView(separator, AccessibilityView.Raw);
        root.Children.AddRange([start, separator, end]);
        start.PropertyChanged += (_, e) => OnEndChanged(e, false);
        end.PropertyChanged += (_, e) => OnEndChanged(e, true);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Sync();
    }

    /// <summary>[起, 止]。</summary>
    public NumberRange Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>最小值。</summary>
    public decimal? Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最大值。</summary>
    public decimal? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>步长。</summary>
    public decimal Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>显示格式。</summary>
    public NumericFormat? Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>前缀。</summary>
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>单位。</summary>
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>起那个框的占位文字。</summary>
    public string? StartPlaceholder { get => GetValue(StartPlaceholderProperty); set => SetValue(StartPlaceholderProperty, value); }

    /// <summary>止那个框的占位文字。</summary>
    public string? EndPlaceholder { get => GetValue(EndPlaceholderProperty); set => SetValue(EndPlaceholderProperty, value); }

    /// <summary>中间的文字。</summary>
    public string Separator { get => GetValue(SeparatorProperty); set => SetValue(SeparatorProperty, value); }

    /// <summary>样式。</summary>
    public ControlVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>两个框（测试用）。</summary>
    internal (NumberField Start, NumberField End) Fields => (start, end);

    /// <summary>一个框写回了：两端都有值且起比止大时对调。</summary>
    void OnEndChanged(AvaloniaPropertyChangedEventArgs e, bool isEnd)
    {
        if (syncing || e.Property != NumberField.ValueProperty) return;
        SetCurrentValue(ValueProperty, Value.With(isEnd, ((NumberField)e.Sender).Value));
    }

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MinProperty || change.Property == MaxProperty || change.Property == StepProperty
            || change.Property == FormatProperty || change.Property == PrefixProperty || change.Property == SuffixProperty
            || change.Property == StartPlaceholderProperty || change.Property == EndPlaceholderProperty || change.Property == SeparatorProperty
            || change.Property == VariantProperty || change.Property == SizeProperty || change.Property == RadiusProperty
            || change.Property == IsInvalidProperty || change.Property == IsRequiredProperty || change.Property == IsReadOnlyProperty
            || change.Property == Provider.LangProperty || change.Property == Field.OwnerProperty)
            Sync();
    }

    /// <summary>两端共用的设置、各自的值与名称；这一组的读屏名称与说明来自所在的 Field。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        var field = Field.Of(this) is { } owner && owner.Owns(this) ? owner : null;
        syncing = true;
        try
        {
            foreach (var box in (NumberField[])[start, end])
            {
                box.Min = Min;
                box.Max = Max;
                box.Step = Step;
                box.Format = Format;
                box.Prefix = Prefix;
                box.Suffix = Suffix;
                box.Variant = Variant;
                box.Size = Size;
                box.Radius = Radius;
                box.IsInvalid = IsInvalid;
                // 没写时不动框自己的只读：所在 Field、表单的只读由框自己跟着
                if (IsReadOnly is { } readOnly) box.IsReadOnly = readOnly;
                else box.ClearValue(TextBox.IsReadOnlyProperty);
            }
            start.Value = Value.Start;
            end.Value = Value.End;
        }
        finally
        {
            syncing = false;
        }
        // 控件里再嵌的控件不接 Field：只有第一个框跟着必填，说明与错误在这一组上
        start.IsRequired = IsRequired ?? field?.Required ?? false;
        end.IsRequired = false;
        start.PlaceholderText = StartPlaceholder ?? lang.SliderMin;
        end.PlaceholderText = EndPlaceholder ?? lang.SliderMax;
        AutomationProperties.SetName(start, lang.SliderMin);
        AutomationProperties.SetName(end, lang.SliderMax);
        separator.Text = Separator;
        separator.Size = Size;
        AutomationProperties.SetHelpText(this, Field.DescribedBy(this));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    /// <summary>读屏：一组（名称是自己写的或所在字段的标签）。</summary>
    sealed class GroupPeer(NumberRangeField owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null;
    }
}

/// <summary>NumberRangeField 的部件名（TS 的部件）。外层（TS 的 root）是区间自己。</summary>
public static class NumberRangeFieldParts
{
    /// <summary>起的那个框（TS 的 propsStart）：NumberField，不带加减按钮。</summary>
    public const string Start = "PART_Start";

    /// <summary>止的那个框（TS 的 propsEnd）：NumberField。</summary>
    public const string End = "PART_End";

    /// <summary>中间的分隔（TS 的 propsSeparator）：Text，textMuted，读屏跳过。</summary>
    public const string Separator = "PART_Separator";
}
