using System.Globalization;

namespace Rune.WinForms.Examples;

/// <summary>Slider 页的示例（vue-tsx-pro 文档站 input/Slider）。桌面没有表单提交：form 示例的"提交"换成显示松手后保存的值。</summary>
static class SliderExamples
{
    public static Example[] All =>
    [
        new("Slider", "player", "播放器", "ValueText 是读屏念的值（\"1:12 / 4:05\"）。← → 按 Step 走，Page Up / Page Down 按 BigStep（默认十分之一），Home / End 到两端；点轨道任意位置直接跳过去并接着拖。", Player),
        new("Slider", "range", "价格区间与刻度", "Range 不为 null 时选一个范围：两个滑块（Tab 在两个之间走）、不能交叉，读屏名称后面加上\"最小值\"\"最大值\"。Marks 是刻度；拖动时实时写回（RangeChanged），ValueCommitted 在松手时触发一次，适合去查询接口。", Price),
        new("Slider", "vertical", "竖排的均衡器", "Orientation=Vertical：↑ 增大、↓ 减小。数值是 double：需要精确小数或超大整数的输入用 NumberFieldControl。", Equalizer),
        new("Slider", "form", "保存与不可用", "拖动过程中 Value 一直在变（ValueChanged），ValueCommitted 只在松手（或键盘改完）时触发，适合保存到服务端。字段不可用时里面的滑块也不可用。", Form),
    ];

    static string Clock(double seconds) => $"{(int)(seconds / 60)}:{Math.Round(seconds % 60):00}";

    static FlexControl Row(Control start, SliderControl slider)
    {
        var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm }, start, slider);
        row.SetGrow(slider, true);
        return row;
    }

    static Control Player()
    {
        const double duration = 245;
        var position = new SliderControl { Value = 72, Max = duration, ValueText = value => $"{Clock(value)} / {Clock(duration)}", AccessibleName = "播放进度" };
        var elapsed = new SmallControl { Tone = TextTone.Muted };
        var left = new SmallControl { Tone = TextTone.Muted };
        void ShowPosition()
        {
            elapsed.Text = Clock(position.Value);
            left.Text = $"-{Clock(duration - position.Value)}";
        }
        position.ValueChanged += (_, _) => ShowPosition();
        ShowPosition();

        var volume = 60d;
        var muted = false;
        var volumeSlider = new SliderControl { Value = volume, ValueText = value => $"音量 {value}%", AccessibleName = "音量" };
        var mute = new IconButtonControl();
        void ShowVolume()
        {
            mute.Icon = muted || volume == 0 ? Lucide.VolumeX : volume < 50 ? Lucide.Volume1 : Lucide.Volume2;
            mute.AccessibleName = muted ? "取消静音" : "静音";
            volumeSlider.Value = muted ? 0 : volume;
        }
        mute.Click += (_, _) =>
        {
            muted = !muted;
            ShowVolume();
        };
        volumeSlider.ValueChanged += (_, _) =>
        {
            // 静音时显示 0；拖动音量就取消静音
            if (muted && volumeSlider.Value == 0) return;
            volume = volumeSlider.Value;
            muted = false;
            ShowVolume();
        };
        ShowVolume();

        var brightness = new SliderControl { Value = 0.8, Min = 0, Max = 1, Step = 0.05, Tone = StatusTone.Warning, ValueText = value => $"{Math.Round(value * 100)}%", AccessibleName = "亮度" };
        var progress = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch },
            position, Build.Flex(new FlexControl { Justify = FlexJustify.Between }, elapsed, left));
        return Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch, Dock = DockStyle.Top },
            progress, Row(mute, volumeSlider), Row(new IconControl { Data = Lucide.Sun }, brightness)));
    }

    static string Yuan(double value) => $"¥{value.ToString("#,0", CultureInfo.InvariantCulture)}";

    static Control Price()
    {
        var price = new SliderControl
        {
            Range = new SliderRange(800, 3500), Min = 0, Max = 5000, Step = 100, ValueText = Yuan,
            Marks = [new(0, "¥0"), new(1000, "¥1k"), new(2500, "¥2.5k"), new(5000, "¥5k+")],
        };
        var committed = price.Range!.Value;
        var status = new SmallControl { Tone = TextTone.Muted };
        void Show() => status.Text = $"正在看 {Yuan(price.Range!.Value.Low)}–{Yuan(price.Range!.Value.High)}；已查询 {Yuan(committed.Low)}–{Yuan(committed.High)}";
        price.RangeChanged += (_, _) => Show();
        price.ValueCommitted += (_, _) =>
        {
            committed = price.Range!.Value;
            Show();
        };
        Show();
        var rating = new SliderControl
        {
            Value = 4, Min = 1, Max = 5, Tone = StatusTone.Success, ValueText = value => $"{value} 星及以上",
            Marks = [.. Enumerable.Range(1, 5).Select(value => new SliderMark(value, $"{value} 星"))],
        };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xl, Align = FlexAlign.Stretch },
            new FieldControl { Label = "价格区间", Description = "拖动时实时写回；松手（或键盘改完）时 ValueCommitted 才去查询", Content = price },
            status,
            new FieldControl { Label = "最低评分", Content = rating });
    }

    static Control Equalizer()
    {
        string[] bands = ["60", "150", "400", "1k", "2.4k", "6k", "15k"];
        var presets = new Dictionary<string, double[]>
        {
            ["flat"] = [0, 0, 0, 0, 0, 0, 0],
            ["bass"] = [8, 6, 3, 0, -1, -2, -2],
            ["vocal"] = [-2, -1, 2, 5, 4, 1, 0],
            ["bright"] = [-1, -1, 0, 1, 3, 6, 8],
        };
        var preset = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, AccessibleName = "预设" };
        preset.Controls.AddRange([
            new ToggleItemControl { Value = "flat", Text = "平直" }, new ToggleItemControl { Value = "bass", Text = "低音" },
            new ToggleItemControl { Value = "vocal", Text = "人声" }, new ToggleItemControl { Value = "bright", Text = "明亮" },
        ]);
        preset.Value = "bass";
        var sliders = bands.Select(band => new SliderControl
        {
            Orientation = SliderOrientation.Vertical, Min = -12, Max = 12, TextSize = TextSize.Sm, AccessibleName = $"{band} Hz",
            ValueText = value => $"{(value > 0 ? "+" : "")}{value} dB",
        }).ToArray();
        void Apply()
        {
            var gains = presets[preset.Value ?? "flat"];
            for (var i = 0; i < sliders.Length; i++) sliders[i].Value = gains[i];
        }
        preset.ValueChanged += (_, _) => Apply();
        Apply();
        var columns = new FlexControl { Justify = FlexJustify.Between, Gap = Space.Sm };
        for (var i = 0; i < bands.Length; i++)
            columns.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Xs }, sliders[i], new SmallControl { Text = bands[i], Tone = TextTone.Muted }));
        return Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch, Dock = DockStyle.Top }, preset, columns));
    }

    static Control Form()
    {
        var volume = new SliderControl { Value = 60 };
        var field = new FieldControl { Label = "提醒音量", Content = volume };
        var committed = volume.Value;
        void Show() => field.Description = $"拖动中 {volume.Value}，松手后才保存：{committed}";
        volume.ValueChanged += (_, _) => Show();
        volume.ValueCommitted += (_, _) =>
        {
            committed = volume.Value;
            Show();
        };
        Show();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch },
            field, new FieldControl { Label = "系统音量", Description = "由系统设置控制", Enabled = false, Content = new SliderControl { Value = 40 } });
    }
}
