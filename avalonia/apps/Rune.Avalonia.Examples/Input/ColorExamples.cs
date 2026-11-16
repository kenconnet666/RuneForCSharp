using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Rune.Avalonia.Examples;

/// <summary>ColorPicker、ColorField 的示例（vue-tsx-pro 文档站 input/ColorPicker、input/ColorField）。</summary>
static class ColorExamples
{
    public static Example[] All =>
    [
        new("ColorPicker", "button", "按钮配色", "取色器平铺在页面上，右边实时预览按钮，并算白字在这个底色上的对比度（ColorMath.ContrastRatio，AA 要 4.5:1）。Presets 是品牌色板；最近使用同一个应用里的取色器共用。", Button),
        new("ColorField", "brand", "品牌配色", "三个 ColorField 各管一种颜色，取色器里带同一组预设（Presets）；改动马上反映到下面的会员日卡片上。", Brand),
        new("ColorField", "formats", "格式与透明度", "输入 rgb(255 99 71)、hsl(…)、颜色名（tomato）后回车或失焦，规范成 #hex 写回；带透明度时是 #rrggbbaa。只读时能看、能复制，不能改。", Formats),
        new("ColorField", "states", "外观与状态", "外观与其他输入框同一套（Variant、Radius、Size）；Field 的必填、出错、只读、不可用交给里面的 ColorField。", States),
    ];

    static readonly string[] Presets = ["#3e63dd", "#6e56cf", "#12a594", "#30a46c", "#e5484d", "#f76b15", "#ffc53d", "#1c2024"];

    static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    static Control Button()
    {
        var picker = new ColorPicker { Value = "#3e63dd", Presets = Presets };
        // 正文 14px：上下 0.5em、左右 1.25em，md 圆角（0.4em）
        var sample = new Border
        {
            Padding = new Thickness(1.25 * 14, 0.5 * 14), CornerRadius = new CornerRadius(14 * 0.4),
            Child = new TextBlock { Text = "立即购买", Foreground = Brushes.White, FontWeight = FontWeight.Medium },
        };
        var value = new Text { Mono = true };
        var ratio = new Small { Tone = TextTone.Muted };
        var badge = new Badge { Size = TextSize.Xs };
        void Update()
        {
            var contrast = ColorMath.ContrastRatio("#ffffff", picker.Value);
            sample.Background = Brush(picker.Value);
            value.Text = picker.Value;
            ratio.Text = $"白字对比度 {contrast:0.00}:1";
            badge.Tone = contrast >= ColorMath.ContrastText ? StatusTone.Success : StatusTone.Danger;
            badge.Content = contrast >= ColorMath.ContrastText ? "达到 AA" : "不够";
        }
        picker.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorPicker.ValueProperty) Update();
        };
        Update();
        return new Flex
        {
            Gap = Space.Xl, Wrap = FlexWrap.Wrap, Align = FlexAlign.Start,
            Children =
            {
                picker,
                new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Start,
                    Children = { sample, value, new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { ratio, badge } } },
                },
            },
        };
    }

    static Control Brand()
    {
        string[] palette = ["#6e56cf", "#3e63dd", "#12a594", "#e5484d", "#f76b15", "#ffc53d", "#2f265f", "#f4f0fe"];
        var brand = new ColorField { Value = "#6e56cf", Presets = palette };
        var background = new ColorField { Value = "#f4f0fe", Presets = palette };
        var text = new ColorField { Value = "#2f265f", Presets = palette };
        var badge = new Border { Width = 40, Height = 40, Child = new Icon { Data = Lucide.Sparkles, Foreground = Brushes.White, FontSize = 20 } };
        var title = new Strong { Text = "小满商城 · 会员日" };
        var subtitle = new Small { Text = "全场满 300 减 50，今晚 24:00 截止" };
        var preview = new Border
        {
            Child = new Flex
            {
                Align = FlexAlign.Center, Gap = Space.Md,
                Children = { badge, new Flex { Direction = FlexDirection.Column, Children = { title, subtitle } } },
            },
        };
        void Update()
        {
            badge.Background = Brush(brand.Value);
            preview.Background = Brush(background.Value);
            TextElement.SetForeground(preview, Brush(text.Value));
        }
        foreach (var field in new[] { brand, background, text })
            field.PropertyChanged += (_, e) =>
            {
                if (e.Property == ColorField.ValueProperty) Update();
            };
        Update();
        // 正文 14px：四周 lg（1em）、lg 圆角（0.7em）；图标块 2.5rem、md 圆角（0.4em）
        preview.Padding = new Thickness(14);
        preview.CornerRadius = new CornerRadius(14 * 0.7);
        badge.CornerRadius = new CornerRadius(14 * 0.4);
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch,
            Children =
            {
                new Grid
                {
                    MinItemWidth = "10rem", Gap = Space.Md,
                    Children = { new Field { Label = "品牌色", Content = brand }, new Field { Label = "卡片底色", Content = background }, new Field { Label = "文字色", Content = text } },
                },
                new Card { Padding = Space.None, Content = preview },
            },
        };
    }

    static Control Formats()
    {
        var color = new ColorField { Value = "#0ea5e9" };
        var note = new Small { Tone = TextTone.Muted };
        void Update() => note.Inlines = [new Run("值："), new Code { Inlines = { new Run(color.Value) } }];
        color.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorField.ValueProperty) Update();
        };
        Update();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
            Children =
            {
                new Field { Label = "任意格式", Description = "输入 rgb(255 99 71)、hsl(…)、颜色名（tomato）后回车或失焦，规范成 #hex 写回", Content = color },
                note,
                new Field { Label = "带透明度", Description = "#rrggbbaa：取色器里拖不透明度滑块", Content = new ColorField { Value = "#11182799" } },
                new Field { Label = "只读", IsReadOnly = true, Content = new ColorField { Value = "#16a34a" } },
            },
        };
    }

    static Control States()
    {
        var soft = new ColorField { Value = "#3e63dd", Variant = ControlVariant.Soft, Radius = Radius.Full };
        var small = new ColorField { Value = "#3e63dd", Size = TextSize.Sm };
        soft.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorField.ValueProperty) small.Value = soft.Value;
        };
        var background = new ColorField { Value = "" };
        var required = new Field { Label = "背景色", IsRequired = true, Error = "请填写背景色", Content = background };
        background.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorField.ValueProperty) required.Error = string.IsNullOrEmpty(background.Value) ? "请填写背景色" : null;
        };
        return new Grid
        {
            MinItemWidth = "14rem", Gap = Space.Lg,
            Children =
            {
                new Field { Label = "variant=\"soft\" radius=\"full\"", Content = soft },
                new Field { Label = "size=\"sm\"", Content = small },
                required,
                new Field { Label = "只读", IsReadOnly = true, Content = new ColorField { Value = "#30a46c" } },
                new Field { Label = "不可用", IsEnabled = false, Content = new ColorField { Value = "#e5484d" } },
            },
        };
    }
}
