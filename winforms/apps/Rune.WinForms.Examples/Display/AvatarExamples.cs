using System.Drawing.Drawing2D;

namespace Rune.WinForms.Examples;

/// <summary>Avatar 的示例（vue-tsx-pro 文档站 display/Avatar）。group（头像组）要等 AvatarGroup；user-button 的账号菜单要等 Menu。</summary>
static class AvatarExamples
{
    public static Example[] All =>
    [
        new("Avatar", "basic", "基础用法", "DisplayName 是读屏念的名称，也用来生成首字母；没有图片时显示后备内容。Radius=md 用于团队、组织；后备内容的 Variant 与 Tone 同 Badge。", Basic),
        new("Avatar", "user-button", "用户按钮", "UserButtonControl 是头像 + 名字 + 箭头，做账号菜单的触发器（菜单还没有：在 Click 里打开自己的菜单）。写了 Description 时多一行说明、占满一行，适合侧栏底部；Compact 只剩头像，名字成为按钮的名称。", Account),
        new("Avatar", "indicator", "角标", "IndicatorControl 包住要标记的控件：计数超过 Max 显示\"99+\"，0 时不显示（ShowZero 时显示 0）；Dot 是小圆点。数字对读屏隐藏，另有一句完整的 Label。WinForms 的角标画不到控件外面，伸出去的一半占位置。", Indicators),
    ];

    static readonly StatusTone[] Tones = [StatusTone.Accent, StatusTone.Info, StatusTone.Success, StatusTone.Warning, StatusTone.Danger, StatusTone.Neutral];

    static FlexControl Row(string label, params Control[] items) =>
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md, Wrap = true }, [new TextControl { Text = label, TextSize = TextSize.Xs, Tone = TextTone.Muted }, .. items]);

    static Control Basic() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
        Row("尺寸", [.. Build.Steps.Select(size => new AvatarControl { TextSize = size, Source = Photos.Members[0].Photo, DisplayName = $"林小满（{size}）" })]),
        Row("后备内容",
            new AvatarControl { DisplayName = "吴悠" },
            new AvatarControl { DisplayName = "Zhao Qing" },
            new AvatarControl { DisplayName = "陈默" },
            new AvatarControl(),
            new AvatarControl { DisplayName = "设计中心", Radius = Radius.Md, Fallback = "设" }),
        Row("色调", [
            .. Tones.Select(tone => new AvatarControl { DisplayName = tone.Value, Tone = tone }),
            .. Tones.Select(tone => new AvatarControl { DisplayName = tone.Value, Tone = tone, Variant = AvatarVariant.Solid }),
        ]));

    static Control Account()
    {
        var me = Photos.Members[0];
        var sidebar = Build.Card(new CardControl { CardPadding = Space.Sm }, new UserButtonControl { DisplayName = me.Name, Source = me.Photo, Description = "采购部 · 管理员" });
        sidebar.MinimumSize = sidebar.MaximumSize = new Size(sidebar.LogicalToDeviceUnits(16 * 16), 0);
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start },
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md, Wrap = true },
                new UserButtonControl { DisplayName = me.Name, Source = me.Photo },
                new UserButtonControl { DisplayName = me.Name, Source = me.Photo, Compact = true }),
            sidebar,
            new SmallControl { Text = "顶栏、只剩头像、侧栏底部三种写法", Tone = TextTone.Muted });
    }

    static IndicatorControl Mark(Control content, Action<IndicatorControl> setup)
    {
        var indicator = new IndicatorControl();
        setup(indicator);
        indicator.Controls.Add(content);
        return indicator;
    }

    static IconButtonControl Tool(IconData icon, string name) => new() { Icon = icon, Variant = ButtonVariant.Surface, AccessibleName = name };

    static Control Indicators()
    {
        var unread = Mark(Tool(Lucide.Bell, "通知"), i => i.Count = 8);
        var add = new ButtonControl { Text = "来一条消息", TextSize = TextSize.Sm, Variant = ButtonVariant.Soft };
        var clear = new ButtonControl { Text = "全部已读", TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost };
        add.Click += (_, _) =>
        {
            unread.Count = (unread.Count ?? 0) + 1;
            clear.Enabled = true;
        };
        clear.Click += (_, _) =>
        {
            unread.Count = 0;
            clear.Enabled = false;
        };
        string[] states = ["在线", "在线", "忙碌", "离线"];
        StatusTone[] tones = [StatusTone.Success, StatusTone.Success, StatusTone.Warning, StatusTone.Neutral];
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Lg, Wrap = true },
                unread,
                Mark(Tool(Lucide.Inbox, "收件箱"), i => (i.Count, i.Max, i.Tone) = (120, 99, StatusTone.Accent)),
                Mark(Tool(Lucide.ListTodo, "待办"), i => (i.Count, i.ShowZero, i.Tone) = (0, true, StatusTone.Neutral)),
                Mark(Tool(Lucide.MessageCircle, "评论"), i => (i.Dot, i.Tone, i.Label) = (true, StatusTone.Warning, "有新回复")),
                add,
                clear),
            Row("在线状态", [.. Photos.Members.Take(4).Select((member, i) => Mark(
                new AvatarControl { TextSize = TextSize.Lg, DisplayName = member.Name, Source = member.Photo },
                indicator => (indicator.Dot, indicator.Tone, indicator.Placement, indicator.Overlap, indicator.Label) = (true, tones[i], IndicatorPlacement.BottomEnd, IndicatorOverlap.Circular, states[i])))]));
    }
}

/// <summary>示例用的"照片"：渐变底加一个人像剪影（同 vue-tsx-pro 文档站的 photos.ts），不依赖图片文件。</summary>
static class Photos
{
    /// <summary>示例里的团队成员（没有照片的显示首字母）。</summary>
    public static readonly (string Name, Image? Photo)[] Members =
    [
        ("林小满", Create("#f97316", "#db2777")),
        ("周一航", Create("#0ea5e9", "#6366f1")),
        ("Chen Mo", Create("#10b981", "#0d9488")),
        ("吴悠", null),
        ("Zhao Qing", Create("#a855f7", "#ec4899")),
        ("孙浩然", null),
        ("Li Wei", Create("#eab308", "#f97316")),
    ];

    /// <summary>128×128 的位图（64 的设计稿放大两倍，高 DPI 下不糊）：渐变底，上面一个半透明白色的头与肩。</summary>
    public static Image Create(string from, string to)
    {
        var bitmap = new Bitmap(128, 128);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(2, 2);
        using (var gradient = new LinearGradientBrush(new Rectangle(0, 0, 64, 64), ColorTranslator.FromHtml(from), ColorTranslator.FromHtml(to), LinearGradientMode.ForwardDiagonal))
            graphics.FillRectangle(gradient, 0, 0, 64, 64);
        using var white = new SolidBrush(Color.FromArgb(217, Color.White));
        graphics.FillEllipse(white, 21, 15, 22, 22);
        using var shoulders = new GraphicsPath();
        shoulders.AddBezier(12, 60, 14, 47, 23, 41, 32, 41);
        shoulders.AddBezier(32, 41, 41, 41, 50, 47, 52, 60);
        shoulders.CloseFigure();
        graphics.FillPath(white, shoulders);
        return bitmap;
    }
}
