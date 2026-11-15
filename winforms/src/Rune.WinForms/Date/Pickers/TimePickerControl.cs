using System.ComponentModel;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 时间选择器（vue-tsx-pro 的 TimePicker）：TimeFieldControl 加一个打开时间列表的按钮，按 Step 列出可选的时间（同 Google 日历的时间下拉）；可以直接键入，也可以点选。
/// 参考 Mantine TimePicker、MUI 的 digital clock；列表是 <see cref="ListboxControl"/>（↑↓、Home / End、按数字跳转）。
/// <code>
/// var start = new TimePickerControl { Min = "09:00", Max = "18:00" };
/// var remind = new TimePickerControl { Step = 15 };
/// </code>
/// 值同 TimeFieldControl（HH:mm，精确到秒时 HH:mm:ss）。列表从 Min（默认 00:00）起每 Step 分钟一项，到 Max 为止，按语言的 12 / 24 小时制显示；
/// 不在列表里的时间（14:47）照样能键入；IsTimeUnavailable 的时间在列表里不可用、键入时显示出错。
/// 列表下方"此刻"选当前时间（此刻不可选时不可用）。弹层与输入框同宽；选好后关闭，焦点回到按钮；Esc、点外部关闭，分段里 Alt + ↓ 打开。
/// </summary>
public class TimePickerControl : TimeFieldControl
{
    readonly PickerPopover picker;
    bool open;

    /// <summary>新建：时分。</summary>
    public TimePickerControl()
    {
        picker = new PickerPopover(this, Lucide.Clock, SetOpen) { MatchWidth = true, Inset = Space.Xs };
        Suffix = picker.Button;
        SyncPicker();
    }

    /// <summary>弹层开着。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => open; set => SetOpen(value, null); }

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FloatingPlacement Placement { get; set; } = FloatingPlacement.BottomStart;

    /// <summary>列表里相邻两项相隔几分钟，默认 30。</summary>
    [Category("Behavior")]
    [DefaultValue(30)]
    public int Step
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    } = 30;

    /// <summary>用户开、关了（按钮、Alt + ↓、选好了、Esc、点外部）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>打开按钮（测试用）。</summary>
    internal IconButtonControl Trigger => picker.Button;

    /// <summary>弹层的内容窗口（测试用）。</summary>
    internal Form Surface => picker.Surface;

    /// <summary>此刻（测试里换掉）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<TimeOnly> Now { get; set; } = () => TimeOnly.FromDateTime(DateTime.Now);

    bool Editable => Enabled && !ReadOnly;

    /// <summary>改开关并带上原因（代码里改 IsOpen 时为 null，不报告）；只读、不可用时不开。</summary>
    void SetOpen(bool next, OpenChangeReason? reason)
    {
        if (next == open || next && !Editable) return;
        open = next;
        if (next) picker.Show(BuildPanel(out var list), Placement, ProviderControl.LangOf(this).TimePickerOpen, () => list);
        else picker.Hide();
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(next, known));
    }

    void SetOpen(bool next, OpenChangeReason reason) => SetOpen(next, (OpenChangeReason?)reason);

    /// <inheritdoc />
    private protected override void Synced() => SyncPicker();

    void SyncPicker()
    {
        // 基类的构造里就会同步一次（设段的时候），那时打开按钮还没建
        if (picker is null) return;
        picker.Sync(ProviderControl.LangOf(this).TimePickerOpen, Editable);
        if (open && !Editable) SetOpen(false, OpenChangeReason.Close);
    }

    /// <summary>能不能选：在 Min–Max 之内、不是不可选的时间。</summary>
    bool Selectable(TimeOnly time) =>
        !(TimeSlots.Parse(Min) is { } low && time < low || TimeSlots.Parse(Max) is { } high && time > high || IsTimeUnavailable?.Invoke(time) == true);

    /// <summary>写回（精确到秒时补 :00），关上，焦点回到按钮。</summary>
    void Write(TimeOnly time)
    {
        Value = TimeSlots.Iso(time, Granularity == TimeFieldGranularity.Second);
        SetOpen(false, OpenChangeReason.Select);
    }

    /// <summary>弹层的内容：时间列表（最高 16em，其余滚动）与"此刻"。</summary>
    /// <param name="list">打开后聚焦的时间列表。</param>
    Control BuildPanel(out ListboxControl list)
    {
        var lang = ProviderControl.LangOf(this);
        var culture = CultureInfo.GetCultureInfo(lang.Code);
        var cycle = HourCycle ?? lang.HourCycle;
        var options = TimeSlots.Between(TimeSlots.Parse(Min), TimeSlots.Parse(Max), Step)
            .Select(time => new ListboxOption(TimeSlots.Iso(time, false), TimeSlots.Label(time, culture, cycle)) { IsDisabled = !Selectable(time) })
            .ToList();
        // 当前值正好是列表里的一项时高亮它（只比时分）
        list = new ListboxControl
        {
            ItemsSource = options,
            Value = Time is { } current ? TimeSlots.Iso(new TimeOnly(current.Hour, current.Minute), false) : null,
            IsRequired = true,
            // 按内容高（最高 16em），宽度由弹层拉满
            AutoSize = true,
            AccessibleName = lang.TimePickerOpen,
            ScrollMaxHeight = (int)MathF.Round(16 * this.EmPixels()),
        };
        list.Picked += (_, e) =>
        {
            if (TimeSlots.Parse(e.Value) is { } time) Write(time);
        };
        var now = Now();
        now = new TimeOnly(now.Hour, now.Minute);
        var button = new ButtonControl { Text = lang.TimePickerNow, Variant = ButtonVariant.Ghost, TextSize = TextSize.Sm, FullWidth = true, Enabled = Selectable(now) };
        button.Click += (_, _) => Write(now);
        var body = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch, AutoSize = true };
        body.Controls.AddRange([list, button]);
        return body;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) picker.Dispose();
        base.Dispose(disposing);
    }
}
