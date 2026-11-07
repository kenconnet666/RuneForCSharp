using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace Rune.Wpf.Examples;

/// <summary>Avatar 的示例（vue-tsx-pro 文档站 display/Avatar）。group（头像组）要等 AvatarGroup；user-button 的账号菜单要等 Menu。</summary>
static class AvatarExamples
{
    public static Example[] All =>
    [
        new("Avatar", "basic", "基础用法", "DisplayName 是读屏念的名称，也用来生成首字母；图片还在下载、下载失败时显示后备内容。Radius=\"md\" 用于团队、组织；后备内容的 Variant 与 Tone 同 Badge。", Basic),
        new("Avatar", "user-button", "用户按钮", "UserButton 是头像 + 名字 + 箭头，做账号菜单的触发器（菜单还没有：点它时打开自己的菜单）。写了 Description 时多一行说明、占满一行，适合侧栏底部；Compact 只剩头像，名字成为按钮的名称。", Account),
        new("Avatar", "indicator", "角标", "Indicator 包住要标记的元素：计数超过 Max 显示\"99+\"，0 时不显示（ShowZero 时显示 0）；Dot 是小圆点。数字对读屏隐藏，另有一句完整的 Label。", Indicators),
    ];

    static FrameworkElement Account()
    {
        var me = Photos.Members[0];
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start,
            Children =
            {
                new Flex
                {
                    Align = FlexAlign.Center, Gap = Space.Md, Wrap = true,
                    Children = { new UserButton { DisplayName = me.Name, Source = me.Photo }, new UserButton { DisplayName = me.Name, Source = me.Photo, Compact = true } },
                },
                new Card { Padding = Space.Sm, Width = 16 * 16, Content = new UserButton { DisplayName = me.Name, Source = me.Photo, Description = "采购部 · 管理员" } },
                new Small { Tone = TextTone.Muted, Text = "顶栏、只剩头像、侧栏底部三种写法" },
            },
        };
    }

    static readonly TextSize[] Sizes = [TextSize.Xs, TextSize.Sm, TextSize.Md, TextSize.Lg, TextSize.Xl];

    static readonly StatusTone[] Tones = [StatusTone.Accent, StatusTone.Info, StatusTone.Success, StatusTone.Warning, StatusTone.Danger, StatusTone.Neutral];

    static Flex Row(string label, params UIElement[] items)
    {
        var row = new Flex { Align = FlexAlign.Center, Gap = Space.Md, Wrap = true, Children = { new Text { Text = label, Size = TextSize.Xs, Tone = TextTone.Muted } } };
        foreach (var item in items) row.Children.Add(item);
        return row;
    }

    static T Named<T>(T element, string name) where T : DependencyObject
    {
        AutomationProperties.SetName(element, name);
        return element;
    }

    static FrameworkElement Basic() => new Flex
    {
        Direction = FlexDirection.Column,
        Gap = Space.Lg,
        Children =
        {
            Row("尺寸", [.. Sizes.Select(size => new Avatar { Size = size, Source = Photos.Members[0].Photo, DisplayName = $"林小满（{size}）" })]),
            Row("后备内容",
                new Avatar { DisplayName = "吴悠" },
                new Avatar { DisplayName = "Zhao Qing" },
                new Avatar { DisplayName = "陈默" },
                new Avatar(),
                new Avatar { DisplayName = "设计中心", Radius = Radius.Md, Fallback = new Icon { Data = Lucide.BuildingComplex } }),
            Row("色调", [
                .. Tones.Select(tone => new Avatar { DisplayName = tone.Value, Tone = tone }),
                .. Tones.Select(tone => new Avatar { DisplayName = tone.Value, Tone = tone, Variant = AvatarVariant.Solid }),
            ]),
        },
    };

    static FrameworkElement Indicators()
    {
        var unread = new Indicator { Count = 8, Content = Named(new IconButton { Icon = Lucide.Bell, Variant = ButtonVariant.Surface }, "通知") };
        var add = new Button { Content = "来一条消息", Size = TextSize.Sm, Variant = ButtonVariant.Soft };
        var clear = new Button { Content = "全部已读", Size = TextSize.Sm, Variant = ButtonVariant.Ghost };
        add.Click += (_, _) =>
        {
            unread.Count = (unread.Count ?? 0) + 1;
            clear.IsEnabled = true;
        };
        clear.Click += (_, _) =>
        {
            unread.Count = 0;
            clear.IsEnabled = false;
        };
        string[] states = ["在线", "在线", "忙碌", "离线"];
        StatusTone[] tones = [StatusTone.Success, StatusTone.Success, StatusTone.Warning, StatusTone.Neutral];
        return new Flex
        {
            Direction = FlexDirection.Column,
            Gap = Space.Lg,
            Children =
            {
                new Flex
                {
                    Align = FlexAlign.Center, Gap = Space.Lg, Wrap = true,
                    Children =
                    {
                        unread,
                        new Indicator { Count = 120, Max = 99, Tone = StatusTone.Accent, Content = Named(new IconButton { Icon = Lucide.Inbox, Variant = ButtonVariant.Surface }, "收件箱") },
                        new Indicator { Count = 0, ShowZero = true, Tone = StatusTone.Neutral, Content = Named(new IconButton { Icon = Lucide.ListTodo, Variant = ButtonVariant.Surface }, "待办") },
                        new Indicator { Dot = true, Tone = StatusTone.Warning, Label = "有新回复", Content = Named(new IconButton { Icon = Lucide.MessageCircle, Variant = ButtonVariant.Surface }, "评论") },
                        add,
                        clear,
                    },
                },
                Row("在线状态", [.. Photos.Members.Take(4).Select((member, i) => new Indicator
                {
                    Dot = true, Tone = tones[i], Placement = IndicatorPlacement.BottomEnd, Overlap = IndicatorOverlap.Circular, Label = states[i],
                    Content = new Avatar { Size = TextSize.Lg, DisplayName = member.Name, Source = member.Photo },
                })]),
            },
        };
    }
}

/// <summary>示例用的"照片"：渐变底加一个人像剪影的矢量图（同 vue-tsx-pro 文档站的 photos.ts），不依赖图片文件。</summary>
static class Photos
{
    /// <summary>示例里的团队成员（没有照片的显示首字母）。</summary>
    public static readonly (string Name, ImageSource? Photo)[] Members =
    [
        ("林小满", Create("#f97316", "#db2777")),
        ("周一航", Create("#0ea5e9", "#6366f1")),
        ("Chen Mo", Create("#10b981", "#0d9488")),
        ("吴悠", null),
        ("Zhao Qing", Create("#a855f7", "#ec4899")),
        ("孙浩然", null),
        ("Li Wei", Create("#eab308", "#f97316")),
    ];

    /// <summary>64×64 的渐变底，上面一个半透明白色的头与肩。</summary>
    public static ImageSource Create(string from, string to)
    {
        var white = new SolidColorBrush(Colors.White) { Opacity = 0.85 };
        var image = new DrawingImage(new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing(new LinearGradientBrush((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to), new Point(0, 0), new Point(1, 1)), null, new RectangleGeometry(new Rect(0, 0, 64, 64))),
                new GeometryDrawing(white, null, new EllipseGeometry(new Rect(21, 15, 22, 22))),
                new GeometryDrawing(white, null, Geometry.Parse("M12 60C14 47 23 41 32 41S50 47 52 60Z")),
            },
        });
        image.Freeze();
        return image;
    }
}
