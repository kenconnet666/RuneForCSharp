using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 分段输入框（vue-tsx-pro 的 SegmentedField）：输入框外壳（<see cref="ControlShell"/>，同 TextFieldControl、SelectControl）里放一组日期分段（<see cref="DateSegmentGroupControl"/>）、
/// 可选的前置图标、清空按钮与尾部内容。<see cref="DateFieldControl"/>、<see cref="TimeFieldControl"/> 是它的两种用法（只差有哪些段与怎样校验）。
/// 值是 ISO 字符串（<see cref="DateSegments.ToIso"/>），变了发 <see cref="ValueChanged"/>；所有段都填了才写回，删掉一段写回 null。点外壳的空白处时焦点进第一个没填的段。
/// 段的顺序、小时制、占位文字按所在的语言包；所在表单重置时丢掉打了一半的段。外壳的外观与状态见 <see cref="SegmentedFieldControlBase"/>。
/// </summary>
[ToolboxItem(false)]
[DefaultEvent(nameof(ValueChanged))]
public class SegmentedFieldControl : SegmentedFieldControlBase
{
    string? value;

    /// <summary>新建：一组分段。</summary>
    public SegmentedFieldControl() : base(1)
    {
    }

    /// <summary>值（ISO 字符串）；没填全时 null。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value { get => value; set => SetValue(value); }

    /// <summary>值变了。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>分段状态（测试与派生类用）。</summary>
    internal DateSegments Segments => Lanes[0];

    /// <summary>这一组分段（测试与派生类用）。</summary>
    internal DateSegmentGroupControl Group => Groups[0];

    /// <summary>值超出范围、是不可选的日期或时刻：外壳显示出错，值照样写回（由表单校验决定能不能提交）。</summary>
    /// <param name="iso">填好的 ISO 字符串。</param>
    protected virtual bool IsOutOfRange(string iso) => false;

    private protected override DateFieldValues? LaneValue(int lane) => DateSegments.FromIso(Value, Fields);

    private protected override void LaneChanged(int lane, string? iso) => SetValue(iso);

    private protected override bool HasValue => Value is not null;

    private protected override bool ValueInvalid => Value is { } iso && IsOutOfRange(iso);

    private protected override void Cleared() => SetValue(null);

    void SetValue(string? next)
    {
        if (value == next) return;
        value = next;
        SyncLanes();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }
}
