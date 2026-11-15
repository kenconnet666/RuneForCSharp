using System.Globalization;

namespace Rune.WinForms.Examples;

/// <summary>DateField、TimeField 的示例（vue-tsx-pro 文档站 date/DateField、date/TimeField）。WinForms 里是 DateFieldControl、TimeFieldControl。DateField 的 rules（值是 DateValue 对象）没有对应：用 Date、DateTime 属性。</summary>
static class DateFieldExamples
{
    public static Example[] All =>
    [
        new("DateField", "basic", "基础用法", "生日（不能晚于今天）、证件有效期（不能早于今天）：段的顺序按语言，填全了才有值，值是 ISO 字符串（2026-10-20）。", Basic),
        new("DateField", "granularity", "精确到哪一段", "Granularity：年、年月、年月日、日期加时分、加时分秒（同原生的 month、date、datetime-local），值的格式跟着变。", Granularity),
        new("DateField", "locales", "段的顺序跟随语言", "段的顺序与分隔符取自语言的区域格式：中文 年/月/日，美国 月/日/年，德国 日.月.年，英国 日/月/年；时间部分 12 / 24 小时制也跟着语言。", Locales),
        new("DateField", "appearance", "外观与状态", "和 TextFieldControl 同一个输入框外壳（样式、尺寸、圆角、图标、清空），只读、不可用、出错。", Appearance),
        new("TimeField", "basic", "基础用法", "门店营业时间：开门、打烊，打烊不能早于开门（Min）；值是 24 小时制的 HH:mm，可以直接比较大小。", TimeBasic),
        new("TimeField", "hour-cycle", "12 / 24 小时制", "默认跟随语言（中文 24 小时、美国 12 小时加 AM / PM），HourCycle 可以指定；值总是 24 小时制。", HourCycle),
        new("TimeField", "states", "外观与状态", "图标、清空按钮、只读、不可用、出错：与其他输入框同一套。", TimeStates),
    ];

    static string Today => DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static Control Columns(string minWidth, params Control[] children) => Build.Grid(new GridControl { MinItemWidth = minWidth, Gap = Space.Lg }, children);

    static FieldControl Shown(string label, SegmentedFieldControl control)
    {
        var field = new FieldControl { Label = label, Content = control, Description = control.Value ?? "null" };
        control.ValueChanged += (_, _) => field.Description = control.Value ?? "null";
        return field;
    }

    static Control Basic()
    {
        var birthday = new DateFieldControl { Value = "1992-08-16", Max = Today, Clearable = true };
        var expires = new DateFieldControl { Min = Today };
        var birthdayField = new FieldControl { Label = "生日", Content = birthday };
        var expiresField = new FieldControl { Label = "证件有效期至", IsRequired = true, Content = expires };
        var result = new TextControl { Tone = TextTone.Muted, TextSize = TextSize.Sm, Wrap = TextWrap.Wrap };
        void Sync()
        {
            birthdayField.Description = birthday.Date is { } day ? day.ToString("yyyy年M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN")) : "年 / 月 / 日";
            expiresField.Error = expires.Value is { } value && string.CompareOrdinal(value, Today) < 0 ? "证件已过期" : null;
            result.Text = $"提交的值：birthday={birthday.Value ?? "null"}，expires={expires.Value ?? "null"}";
        }
        birthday.ValueChanged += (_, _) => Sync();
        expires.ValueChanged += (_, _) => Sync();
        Sync();
        return Columns("16rem", birthdayField, expiresField, result);
    }

    static Control Granularity() => Columns("15rem",
        Shown("年度", new DateFieldControl { Granularity = DateFieldGranularity.Year, Value = "2026" }),
        Shown("账期", new DateFieldControl { Granularity = DateFieldGranularity.Month, Value = "2026-10" }),
        Shown("发货日期", new DateFieldControl { Value = "2026-10-20" }),
        Shown("会议开始", new DateFieldControl { Granularity = DateFieldGranularity.Minute, Value = "2026-10-20T14:30" }),
        Shown("日志时间", new DateFieldControl { Granularity = DateFieldGranularity.Second, Value = "2026-10-20T14:30:15" }));

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
        return Columns("16rem", [.. cases.Select(entry =>
        {
            var provider = new ProviderControl { Lang = entry.Lang, AutoSize = true };
            provider.Controls.Add(new FieldControl { Label = entry.Label, Content = new DateFieldControl { Granularity = DateFieldGranularity.Minute, Value = "2026-10-20T14:30" }, Dock = DockStyle.Top });
            return (Control)provider;
        })]);
    }

    static Control Appearance() => Columns("15rem",
        new FieldControl { Label = "surface（默认）", Content = new DateFieldControl { Value = "2026-10-20", Icon = Lucide.CalendarDays, Clearable = true } },
        new FieldControl { Label = "soft", Content = new DateFieldControl { Value = "2026-10-20", Variant = ControlVariant.Soft, Icon = Lucide.CalendarDays } },
        new FieldControl { Label = "TextSize=Sm · Radius=Full", Content = new DateFieldControl { Value = "2026-10-20", TextSize = TextSize.Sm, Radius = Radius.Full } },
        new FieldControl { Label = "TextSize=Lg", Content = new DateFieldControl { Value = "2026-10-20", TextSize = TextSize.Lg } },
        new FieldControl { Label = "只读", Content = new DateFieldControl { Value = "2026-10-20", IsReadOnly = true } },
        new FieldControl { Label = "不可用", Content = new DateFieldControl { Value = "2026-10-20", Enabled = false } },
        new FieldControl { Label = "出错", Error = "早于开学日期", Content = new DateFieldControl { Value = "2026-08-01", Min = "2026-09-01" } },
        new FieldControl { Label = "空着", Content = new DateFieldControl() });

    static Control TimeBasic()
    {
        var grid = new GridControl { Columns = 4, ColumnGap = Space.Md, RowGap = Space.Sm, Align = GridAlign.Center };
        foreach (var title in new[] { "日期", "营业", "开门", "打烊" }) grid.Controls.Add(new SmallControl { Tone = TextTone.Muted, Text = title });
        var badges = new FlexControl { Gap = Space.Sm, Wrap = true };
        foreach (var (name, open, opens, closes) in new[] { ("工作日", true, "09:00", "21:30"), ("周六", true, "10:00", "22:00"), ("周日", false, "10:00", "18:00") })
        {
            var toggle = new SwitchControl { IsChecked = open, AccessibleName = $"{name}营业" };
            var start = new TimeFieldControl { Value = opens, AccessibleName = $"{name}开门时间" };
            var end = new TimeFieldControl { Value = closes, Min = opens, AccessibleName = $"{name}打烊时间" };
            var badge = new BadgeControl { Radius = Radius.Full };
            void Sync()
            {
                start.Enabled = end.Enabled = toggle.IsChecked;
                end.Min = start.Value;
                badge.Tone = toggle.IsChecked ? StatusTone.Success : StatusTone.Neutral;
                badge.Text = $"{name}：{(toggle.IsChecked && start.Value is { } a && end.Value is { } b ? $"{a} – {b}" : "休息")}";
            }
            toggle.IsCheckedChanged += (_, _) => Sync();
            start.ValueChanged += (_, _) => Sync();
            end.ValueChanged += (_, _) => Sync();
            Sync();
            grid.Controls.AddRange([new TextControl { Weight = TextWeight.Medium, Text = name }, toggle, start, end]);
            badges.Controls.Add(badge);
        }
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch }, grid, badges);
    }

    static Control HourCycle()
    {
        var english = new ProviderControl { Lang = Lang.EnUs, AutoSize = true };
        english.Controls.Add(new FieldControl { Label = "English (US)", Description = "Type a / p in the last segment", Content = new TimeFieldControl { Value = "19:45" }, Dock = DockStyle.Top });
        return Columns("13rem",
            Shown("简体中文（24 小时制）", new TimeFieldControl { Value = "19:45" }),
            new FieldControl { Label = "HourCycle=h12", Description = "中文的上午 / 下午", Content = new TimeFieldControl { Value = "19:45", HourCycle = "h12" } },
            english,
            new FieldControl { Label = "Granularity=Second", Content = new TimeFieldControl { Value = "08:30:15", Granularity = TimeFieldGranularity.Second } },
            new FieldControl { Label = "Granularity=Hour", Content = new TimeFieldControl { Value = "08:00", Granularity = TimeFieldGranularity.Hour } });
    }

    static Control TimeStates() => Columns("13rem",
        new FieldControl { Label = "图标与清空按钮", Content = new TimeFieldControl { Value = "09:30", Icon = Lucide.Clock, Clearable = true } },
        new FieldControl { Label = "soft", Content = new TimeFieldControl { Value = "09:30", Variant = ControlVariant.Soft } },
        new FieldControl { Label = "只读", Content = new TimeFieldControl { Value = "09:30", IsReadOnly = true } },
        new FieldControl { Label = "不可用", Content = new TimeFieldControl { Value = "09:30", Enabled = false } },
        new FieldControl { Label = "出错", Error = "不在营业时间", Content = new TimeFieldControl { Value = "23:30", Max = "22:00" } });
}
