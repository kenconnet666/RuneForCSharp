using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>DateField、TimeField 的示例（vue-tsx-pro 文档站 date/DateField、date/TimeField）。DateField 的 rules（值是 DateValue 对象）没有对应：C# 里用 Date、DateTime 属性。</summary>
static class DateFieldExamples
{
    public static Example[] All =>
    [
        new("DateField", "basic", "基础用法", "生日（不能晚于今天）、证件有效期（不能早于今天）：段的顺序按语言，填全了才有值，值是 ISO 字符串（2026-10-20）。", Basic),
        new("DateField", "granularity", "精确到哪一段", "Granularity：年、年月、年月日、日期加时分、加时分秒（同原生的 month、date、datetime-local），值的格式跟着变。", Granularity),
        new("DateField", "locales", "段的顺序跟随语言", "段的顺序与分隔符取自语言的区域格式：中文 年/月/日，美国 月/日/年，德国 日.月.年，英国 日/月/年；时间部分 12 / 24 小时制也跟着语言。", Locales),
        new("DateField", "appearance", "外观与状态", "和 TextField 同一个输入框外壳（样式、尺寸、圆角、图标、清空），只读、不可用、出错。", Appearance),
        new("TimeField", "basic", "基础用法", "门店营业时间：开门、打烊，打烊不能早于开门（Min）；值是 24 小时制的 HH:mm，可以直接比较大小。", TimeBasic),
        new("TimeField", "hour-cycle", "12 / 24 小时制", "默认跟随语言（中文 24 小时、美国 12 小时加 AM / PM），HourCycle 可以指定；值总是 24 小时制。", HourCycle),
        new("TimeField", "states", "外观与状态", "图标、清空按钮、只读、不可用、出错：与其他输入框同一套。", TimeStates),
    ];

    static Grid Columns(string minWidth, params Control[] children)
    {
        var grid = new Grid { MinItemWidth = new Length(minWidth), Gap = Space.Lg };
        grid.Children.AddRange(children);
        return grid;
    }

    static string Today => DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    static Control Basic()
    {
        var birthday = new DateField { Value = "1992-08-16", Max = Today, Clearable = true };
        var expires = new DateField { Min = Today };
        var birthdayField = new Field { Label = "生日", Content = birthday };
        var expiresField = new Field { Label = "证件有效期至", IsRequired = true, Content = expires };
        var result = new Text { Tone = TextTone.Muted, Size = TextSize.Sm };
        void Sync()
        {
            birthdayField.Description = birthday.Date is { } day ? day.ToString("yyyy年M月d日 dddd", System.Globalization.CultureInfo.GetCultureInfo("zh-CN")) : "年 / 月 / 日";
            expiresField.Error = expires.Value is { } value && string.CompareOrdinal(value, Today) < 0 ? "证件已过期" : null;
            result.Text = $"提交的值：birthday={birthday.Value ?? "null"}，expires={expires.Value ?? "null"}";
        }
        birthday.PropertyChanged += (_, e) => { if (e.Property == SegmentedField.ValueProperty) Sync(); };
        expires.PropertyChanged += (_, e) => { if (e.Property == SegmentedField.ValueProperty) Sync(); };
        Sync();
        return Columns("16em", birthdayField, expiresField, result);
    }

    static Field Shown(string label, SegmentedField control)
    {
        var field = new Field { Label = label, Content = control, Description = control.Value ?? "null" };
        control.PropertyChanged += (_, e) => { if (e.Property == SegmentedField.ValueProperty) field.Description = control.Value ?? "null"; };
        return field;
    }

    static Control Granularity() => Columns("15em",
        Shown("年度", new DateField { Granularity = DateFieldGranularity.Year, Value = "2026" }),
        Shown("账期", new DateField { Granularity = DateFieldGranularity.Month, Value = "2026-10" }),
        Shown("发货日期", new DateField { Value = "2026-10-20" }),
        Shown("会议开始", new DateField { Granularity = DateFieldGranularity.Minute, Value = "2026-10-20T14:30" }),
        Shown("日志时间", new DateField { Granularity = DateFieldGranularity.Second, Value = "2026-10-20T14:30:15" }));

    static Control Locales()
    {
        (string Label, Lang Lang)[] cases =
        [
            ("简体中文", Lang.ZhCn),
            ("English (US)", Lang.EnUs),
            ("English (UK)", Lang.EnUs with { Code = "en-GB" }),
            ("Deutsch", Lang.EnUs with { Code = "de-DE" }),
            ("한국어", Lang.EnUs with { Code = "ko-KR" }),
        ];
        return Columns("16em", [.. cases.Select(entry =>
        {
            var field = new Field { Label = entry.Label, Content = new DateField { Granularity = DateFieldGranularity.Minute, Value = "2026-10-20T14:30" } };
            Provider.SetLang(field, entry.Lang);
            return (Control)field;
        })]);
    }

    static Control Appearance() => Columns("15em",
        new Field { Label = "surface（默认）", Content = new DateField { Value = "2026-10-20", Icon = Lucide.CalendarDays, Clearable = true } },
        new Field { Label = "soft", Content = new DateField { Value = "2026-10-20", Variant = ControlVariant.Soft, Icon = Lucide.CalendarDays } },
        new Field { Label = "Size=sm · Radius=full", Content = new DateField { Value = "2026-10-20", Size = TextSize.Sm, Radius = Radius.Full } },
        new Field { Label = "Size=lg", Content = new DateField { Value = "2026-10-20", Size = TextSize.Lg } },
        new Field { Label = "只读", Content = new DateField { Value = "2026-10-20", IsReadOnly = true } },
        new Field { Label = "不可用", Content = new DateField { Value = "2026-10-20", IsEnabled = false } },
        new Field { Label = "出错", Error = "早于开学日期", Content = new DateField { Value = "2026-08-01", Min = "2026-09-01" } },
        new Field { Label = "空着", Content = new DateField() });

    static Control TimeBasic()
    {
        var grid = new Grid { Columns = 4, ColumnGap = Space.Md, RowGap = Space.Sm, Align = GridAlign.Center };
        grid.Children.AddRange([new Small { Tone = TextTone.Muted, Text = "日期" }, new Small { Tone = TextTone.Muted, Text = "营业" }, new Small { Tone = TextTone.Muted, Text = "开门" }, new Small { Tone = TextTone.Muted, Text = "打烊" }]);
        var badges = new Flex { Gap = Space.Sm, Wrap = true };
        foreach (var (name, open, opens, closes) in new[] { ("工作日", true, "09:00", "21:30"), ("周六", true, "10:00", "22:00"), ("周日", false, "10:00", "18:00") })
        {
            var toggle = new Switch { IsChecked = open };
            global::Avalonia.Automation.AutomationProperties.SetName(toggle, $"{name}营业");
            var start = new TimeField { Value = opens, IsEnabled = open };
            var end = new TimeField { Value = closes, Min = opens, IsEnabled = open };
            global::Avalonia.Automation.AutomationProperties.SetName(start, $"{name}开门时间");
            global::Avalonia.Automation.AutomationProperties.SetName(end, $"{name}打烊时间");
            var badge = new Badge { Radius = Radius.Full };
            void Sync()
            {
                start.IsEnabled = end.IsEnabled = toggle.IsChecked;
                end.Min = start.Value;
                badge.Tone = toggle.IsChecked ? StatusTone.Success : StatusTone.Neutral;
                badge.Content = $"{name}：{(toggle.IsChecked && start.Value is { } a && end.Value is { } b ? $"{a} – {b}" : "休息")}";
            }
            toggle.PropertyChanged += (_, e) => { if (e.Property == ChoiceBase.IsCheckedProperty) Sync(); };
            start.PropertyChanged += (_, e) => { if (e.Property == SegmentedField.ValueProperty) Sync(); };
            end.PropertyChanged += (_, e) => { if (e.Property == SegmentedField.ValueProperty) Sync(); };
            Sync();
            grid.Children.AddRange([new Text { Weight = TextWeight.Medium, Text = name }, toggle, start, end]);
            badges.Children.Add(badge);
        }
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { grid, badges } };
    }

    static Control HourCycle()
    {
        var english = new Field { Label = "English (US)", Description = "Type a / p in the last segment", Content = new TimeField { Value = "19:45" } };
        Provider.SetLang(english, Lang.EnUs);
        return Columns("13em",
            Shown("简体中文（24 小时制）", new TimeField { Value = "19:45" }),
            new Field { Label = "HourCycle=h12", Description = "中文的上午 / 下午", Content = new TimeField { Value = "19:45", HourCycle = "h12" } },
            english,
            new Field { Label = "Granularity=Second", Content = new TimeField { Value = "08:30:15", Granularity = TimeFieldGranularity.Second } },
            new Field { Label = "Granularity=Hour", Content = new TimeField { Value = "08:00", Granularity = TimeFieldGranularity.Hour } });
    }

    static Control TimeStates() => Columns("13em",
        new Field { Label = "图标与清空按钮", Content = new TimeField { Value = "09:30", Icon = Lucide.Clock, Clearable = true } },
        new Field { Label = "soft", Content = new TimeField { Value = "09:30", Variant = ControlVariant.Soft } },
        new Field { Label = "只读", Content = new TimeField { Value = "09:30", IsReadOnly = true } },
        new Field { Label = "不可用", Content = new TimeField { Value = "09:30", IsEnabled = false } },
        new Field { Label = "出错", Error = "不在营业时间", Content = new TimeField { Value = "23:30", Max = "22:00" } });
}
