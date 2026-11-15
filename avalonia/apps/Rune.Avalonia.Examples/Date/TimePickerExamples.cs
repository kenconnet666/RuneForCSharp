using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>
/// TimePicker 的示例（vue-tsx-pro 文档站 date/TimePicker）。上游的 valueType="dateValue"（值是 Time 对象）在 C# 里是 Time 属性（TimeOnly）。
/// </summary>
static class TimePickerExamples
{
    public static Example[] All =>
    [
        new("TimePicker", "meeting", "约会议、提醒与闹钟", "工作时间内每半小时一档，午休 12:00–13:30 不可选；提醒每 15 分钟一档，12 小时制；不在列表里的时间也能直接键入。", Meeting),
        new("TimePicker", "states", "精确到哪一段与外观状态", "Granularity 精确到秒或只到时，Time 属性读写 TimeOnly；外观与只读、不可用同其他输入框。", States),
    ];

    static void OnValue(TimePicker picker, Action changed) => picker.PropertyChanged += (_, e) =>
    {
        if (e.Property == SegmentedField.ValueProperty) changed();
    };

    static Grid Columns(params Control[] children)
    {
        var grid = new Grid { MinItemWidth = new Length("14em"), Gap = Space.Lg };
        grid.Children.AddRange(children);
        return grid;
    }

    /// <summary>说明里显示当前的值。</summary>
    static Field Shown(string label, TimePicker picker)
    {
        var field = new Field { Label = label, Content = picker };
        void Sync() => field.Description = picker.Value ?? "null";
        OnValue(picker, Sync);
        Sync();
        return field;
    }

    static Control Meeting() => Columns(
        Shown("会议开始", new TimePicker
        {
            Value = "10:30",
            Min = "09:00",
            Max = "18:00",
            // 午休：列表里不可用，键入时显示出错
            IsTimeUnavailable = time => time >= new TimeOnly(12, 0) && time < new TimeOnly(13, 30),
        }),
        Shown("提醒时间", new TimePicker { Step = 15, HourCycle = "h12", Clearable = true }),
        new Field { Label = "闹钟", Description = "列表每小时一档，06:45 这样的时间直接键入", Content = new TimePicker { Value = "06:45", Step = 60 } });

    static Control States()
    {
        var alarm = new TimePicker { Clearable = true };
        var alarmField = new Field { Label = "Time 属性", Content = alarm };
        void Sync() => alarmField.Description = alarm.Time is { } time ? $"TimeOnly：{time.Hour} 时 {time.Minute} 分" : "读写的是 TimeOnly";
        OnValue(alarm, Sync);
        Sync();
        return Columns(
            Shown("Granularity=Second", new TimePicker { Value = "00:42:15", Granularity = TimeFieldGranularity.Second, Step = 5, Icon = Lucide.Timer }),
            Shown("Granularity=Hour", new TimePicker { Value = "08:00", Granularity = TimeFieldGranularity.Hour, Step = 60, Min = "06:00", Max = "22:00" }),
            alarmField,
            new Field { Label = "Variant=soft · Radius=full", Content = new TimePicker { Value = "13:30", Variant = ControlVariant.Soft, Radius = Radius.Full, Size = TextSize.Sm } },
            new Field { Label = "只读", IsReadOnly = true, Content = new TimePicker { Value = "09:00" } },
            new Field { Label = "不可用", IsEnabled = false, Content = new TimePicker { Value = "09:00" } });
    }
}
