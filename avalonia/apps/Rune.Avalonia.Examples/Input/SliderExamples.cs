using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Slider 页的示例（vue-tsx-pro 文档站 input/Slider）。桌面没有表单提交：form 示例的"提交"换成显示松手后保存的值。</summary>
static class SliderExamples
{
    public static Example[] All =>
    [
        new("Slider", "player", "播放器", "ValueText 是读屏念的值（\"1:12 / 4:05\"）。← → 按 Step 走，Page Up / Page Down 按 BigStep（默认十分之一），Home / End 到两端；点轨道任意位置直接跳过去并接着拖。", Player),
        new("Slider", "range", "价格区间与刻度", "Range 不为 null 时选一个范围：两个滑块各自可聚焦、不能交叉，读屏名称后面加上\"最小值\"\"最大值\"。Marks 是刻度；拖动时实时写回，ValueCommitted 在松手时触发一次，适合去查询接口。", Price),
        new("Slider", "vertical", "竖排的均衡器", "Orientation=Vertical：↑ 增大、↓ 减小。数值是 double：需要精确小数或超大整数的输入用 NumberField。", Equalizer),
        new("Slider", "form", "保存与不可用", "拖动过程中 Value 一直在变，ValueCommitted 只在松手（或键盘改完）时触发，适合保存到服务端。Field 不可用时里面的滑块也不可用。", Form),
    ];

    static Slider Named(Slider slider, string name)
    {
        AutomationProperties.SetName(slider, name);
        return slider;
    }

    static string Clock(double seconds) => $"{(int)(seconds / 60)}:{Math.Round(seconds % 60):00}";

    static Control Player()
    {
        const double duration = 245;
        var position = Named(new Slider { Value = 72, Max = duration, ValueText = value => $"{Clock(value)} / {Clock(duration)}" }, "播放进度");
        var elapsed = new Small { Tone = TextTone.Muted };
        var left = new Small { Tone = TextTone.Muted };
        void ShowPosition()
        {
            elapsed.Text = Clock(position.Value);
            left.Text = $"-{Clock(duration - position.Value)}";
        }
        position.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) ShowPosition(); };
        ShowPosition();

        var volume = 60d;
        var muted = false;
        var volumeSlider = Named(new Slider { Value = volume, ValueText = value => $"音量 {value}%" }, "音量");
        var mute = new IconButton();
        void ShowVolume()
        {
            mute.Icon = muted || volume == 0 ? Lucide.VolumeX : volume < 50 ? Lucide.Volume1 : Lucide.Volume2;
            AutomationProperties.SetName(mute, muted ? "取消静音" : "静音");
            volumeSlider.Value = muted ? 0 : volume;
        }
        mute.Click += (_, _) =>
        {
            muted = !muted;
            ShowVolume();
        };
        volumeSlider.PropertyChanged += (_, e) =>
        {
            // 静音时显示 0；拖动音量就取消静音
            if (e.Property != Slider.ValueProperty || muted && volumeSlider.Value == 0) return;
            volume = volumeSlider.Value;
            muted = false;
            ShowVolume();
        };
        ShowVolume();

        var brightness = Named(new Slider { Value = 0.8, Min = 0, Max = 1, Step = 0.05, Tone = StatusTone.Warning, ValueText = value => $"{Math.Round(value * 100)}%" }, "亮度");
        static Flex Row(Control start, Slider slider)
        {
            var row = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { start, slider } };
            Flex.SetGrow(slider, true);
            return row;
        }
        return new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Lg,
                Children =
                {
                    new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { position, new Flex { Justify = FlexJustify.Between, Children = { elapsed, left } } } },
                    Row(mute, volumeSlider),
                    Row(new Icon { Data = Lucide.Sun }, brightness),
                },
            },
        };
    }

    static string Yuan(double value) => $"¥{value.ToString("#,0", CultureInfo.InvariantCulture)}";

    static Control Price()
    {
        var price = new Slider
        {
            Range = new SliderRange(800, 3500), Min = 0, Max = 5000, Step = 100, ValueText = Yuan,
            Marks = [new(0, "¥0"), new(1000, "¥1k"), new(2500, "¥2.5k"), new(5000, "¥5k+")],
        };
        var committed = price.Range!.Value;
        var status = new Small { Tone = TextTone.Muted };
        void Show() => status.Text = $"正在看 {Yuan(price.Range!.Value.Low)}–{Yuan(price.Range!.Value.High)}；已查询 {Yuan(committed.Low)}–{Yuan(committed.High)}";
        price.PropertyChanged += (_, e) => { if (e.Property == Slider.RangeProperty) Show(); };
        price.ValueCommitted += (_, _) =>
        {
            committed = price.Range!.Value;
            Show();
        };
        Show();
        var rating = new Slider
        {
            Value = 4, Min = 1, Max = 5, Tone = StatusTone.Success, ValueText = value => $"{value} 星及以上",
            Marks = [.. Enumerable.Range(1, 5).Select(value => new SliderMark(value, $"{value} 星"))],
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Xl,
            Children =
            {
                new Field { Label = "价格区间", Description = "拖动时实时写回；松手（或键盘改完）时 ValueCommitted 才去查询", Content = price },
                status,
                new Field { Label = "最低评分", Content = rating },
            },
        };
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
        var preset = new ToggleGroup
        {
            IsRequired = true, Size = TextSize.Sm, Value = "bass",
            Children = { new ToggleItem { Value = "flat", Content = "平直" }, new ToggleItem { Value = "bass", Content = "低音" }, new ToggleItem { Value = "vocal", Content = "人声" }, new ToggleItem { Value = "bright", Content = "明亮" } },
        };
        AutomationProperties.SetName(preset, "预设");
        var sliders = bands.Select(band => Named(new Slider
        {
            Orientation = SliderOrientation.Vertical, Min = -12, Max = 12, Size = TextSize.Sm,
            ValueText = value => $"{(value > 0 ? "+" : "")}{value} dB",
        }, $"{band} Hz")).ToArray();
        void Apply()
        {
            var gains = presets[preset.Value ?? "flat"];
            for (var i = 0; i < sliders.Length; i++) sliders[i].Value = gains[i];
        }
        preset.PropertyChanged += (_, e) => { if (e.Property == ToggleGroup.ValueProperty) Apply(); };
        Apply();
        var columns = new Flex { Justify = FlexJustify.Between, Gap = Space.Sm };
        for (var i = 0; i < bands.Length; i++)
            columns.Children.Add(new Flex { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Xs, Children = { sliders[i], new Small { Text = bands[i], Tone = TextTone.Muted } } });
        return new Card { Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Children = { preset, columns } } };
    }

    static Control Form()
    {
        var volume = new Slider { Value = 60 };
        var field = new Field { Label = "提醒音量", Content = volume };
        var committed = volume.Value;
        void Show() => field.Description = $"拖动中 {volume.Value}，松手后才保存：{committed}";
        volume.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Show(); };
        volume.ValueCommitted += (_, _) =>
        {
            committed = volume.Value;
            Show();
        };
        Show();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children = { field, new Field { Label = "系统音量", Description = "由系统设置控制", IsEnabled = false, Content = new Slider { Value = 40 } } },
        };
    }
}
