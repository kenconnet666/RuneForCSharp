using Avalonia;
using Avalonia.Data;

namespace Rune.Avalonia;

/// <summary>
/// 分段输入框（vue-tsx-pro 的 SegmentedField）：输入框外壳（同 TextField、Select）里放一组日期分段（<see cref="DateSegmentGroup"/>）、
/// 可选的前置图标、清空按钮与尾部内容。<see cref="DateField"/>、<see cref="TimeField"/> 是它的两种用法（只差有哪些段与怎样校验）；日期选择器把打开日历的按钮放在 Suffix 里。
/// <code>
/// &lt;rune:SegmentedField Fields="Hour,Minute" Value="{Binding Time}" /&gt;
/// </code>
/// 值是 ISO 字符串（<see cref="DateSegments.ToIso"/>）；所有段都填了才写回，删掉一段写回 null。点外壳的空白处时焦点进第一个没填的段。
/// 段的顺序、小时制、占位文字按所在的语言包（Lang.Code、HourCycle、DateFieldPlaceholders）；所在表单重置时丢掉打了一半的段。
/// 外壳的外观与状态见 <see cref="SegmentedFieldBase"/>；部件：<see cref="SegmentedFieldParts"/>，默认外观在 SegmentedField.axaml。
/// </summary>
public class SegmentedField : SegmentedFieldBase
{
    /// <summary>值（ISO 字符串，默认双向绑定）；没填全时 null。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<SegmentedField, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>新建：一组分段。</summary>
    public SegmentedField() : base(1)
    {
    }

    /// <summary>值（ISO 字符串）。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>分段状态（测试与派生类用）。</summary>
    internal DateSegments Segments => Lanes[0];

    /// <summary>这一组分段（测试用）。</summary>
    internal DateSegmentGroup? Group => Groups[0];

    /// <summary>值超出范围、是不可选的日期或时刻：外壳显示出错，值照样写回（由表单校验决定能不能提交）。</summary>
    /// <param name="iso">填好的 ISO 字符串。</param>
    protected virtual bool IsOutOfRange(string iso) => false;

    private protected override DateFieldValues? LaneValue(int lane) => DateSegments.FromIso(Value, Fields);

    private protected override void LaneChanged(int lane, string? iso) => SetCurrentValue(ValueProperty, iso);

    private protected override bool HasValue => Value is not null;

    private protected override bool ValueInvalid => Value is { } iso && IsOutOfRange(iso);

    private protected override void Cleared() => SetCurrentValue(ValueProperty, null);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) SyncLanes();
    }
}

/// <summary>分段输入框的部件名（模板里的元素）。外层（TS 的 root）是 Border#PART_Root。</summary>
public static class SegmentedFieldParts
{
    /// <summary>外壳（TS 的 propsRoot）：Border。</summary>
    public const string Root = "PART_Root";

    /// <summary>这一组分段（TS 的主元素；区间时是开始那组，TS 的 propsStart）：DateSegmentGroup。</summary>
    public const string Input = "PART_Input";

    /// <summary>区间时两组之间的"–"（TS 的 propsSeparator）：Text，textMuted、左右 xs。</summary>
    public const string Separator = "PART_Separator";

    /// <summary>区间时结束那组（TS 的 propsEnd）：DateSegmentGroup。</summary>
    public const string End = "PART_End";

    /// <summary>前置图标（TS 的 propsIcon）：Icon，textMuted。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>清空按钮（TS 的 propsClear）：ClearButton。</summary>
    public const string Clear = "PART_Clear";

    /// <summary>尾部内容：ContentPresenter。</summary>
    public const string Suffix = "PART_Suffix";
}
