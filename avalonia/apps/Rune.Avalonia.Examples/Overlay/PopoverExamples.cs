using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using PlatformGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia.Examples;

/// <summary>
/// Popover 页的示例（vue-tsx-pro 文档站 overlay/Popover）。tooltip 里"更多格式"按钮的菜单要等 Menu；compute 是网页的定位函数，桌面上交给平台的弹出层。
/// </summary>
static class PopoverExamples
{
    public static Example[] All =>
    [
        new("Popover", "popover", "Popover", "点触发器在旁边弹出一块内容：放筛选条件、简短表单、分享面板。点外面或 Esc 关闭，打开时焦点进到第一个能聚焦的元素，关上后回到触发器；IsOpen 可以双向绑定。浮层以触发器为名称。", Share),
        new("Popover", "tooltip", "Tooltip", "给图标按钮补一句用途和快捷键：写 ToolTip.Tip。指针停一会儿显示，在一排按钮上移动时不用每次都等；键盘聚焦立即显示，Esc 隐藏。提示是触发器的帮助文字，触发器自己要有名称，提示里不放可交互的内容。", Toolbar),
        new("Popover", "hover-card", "HoverCard", "悬停或键盘聚焦链接时弹出一张富内容卡片：可以移到卡片上，卡片里能放按钮（键盘 Tab 进入）。打开时焦点不动，点外面不关，Esc 关闭。卡片只是预览，里面的信息在别处也要能拿到——触屏没有悬停。", Profile),
        new("Popover", "placement", "位置与箭头", "rune:Tooltip.Placement 是边加对齐，共 12 种；空间不够时自动翻到另一边。rune:Tooltip.Arrow 显示指向触发器的小箭头。", Placements),
        new("Popover", "timing", "显示的时机", "提示停多久才显示是 ToolTip.ShowDelay（有提示的元素默认 500 毫秒），工具栏这类连续的按钮可以设成 0；ToolTip.ServiceEnabled=False 时不显示。HoverCard 的 Delay / CloseDelay 分别控制打开与离开后关闭的等待，Offset 是与触发器的距离（像素）。都可以用 IsOpen 受控。", Timing),
    ];

    static Control Share()
    {
        const string link = "https://example.com/s/q3-report";
        var linkField = new TextField { Text = link, IsReadOnly = true };
        AutomationProperties.SetName(linkField, "分享链接");
        Flex.SetGrow(linkField, true);
        var sharing = new Popover
        {
            Placement = FloatingPlacement.BottomStart,
            Arrow = true,
            Trigger = new Button { Icon = Lucide.Share2, Content = "分享" },
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md, Width = 20 * 16,
                Children =
                {
                    new Strong { Text = "分享“第三季度报告”" },
                    new Flex { Gap = Space.Xs, Children = { linkField, new CopyButton { Value = link } } },
                    new Switch { Content = "允许查看者评论", IsChecked = true },
                },
            },
        };

        string[] statuses = ["待付款", "备货中", "已发货", "已完成", "已退款"];
        IReadOnlyList<string> applied = ["待付款", "备货中"];
        var picked = new CheckboxGroup { Value = applied };
        foreach (var status in statuses) picked.Items.Add(new Checkbox { Value = status, Content = status });
        AutomationProperties.SetName(picked, "订单状态");
        var trigger = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Funnel };
        var summary = new Small { Tone = TextTone.Muted };
        void Show()
        {
            trigger.Content = applied.Count > 0 ? $"订单状态（{applied.Count}）" : "订单状态";
            summary.Text = $"筛选：{(applied.Count > 0 ? string.Join("、", applied) : "全部状态")}";
        }
        var clear = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "清空" };
        var apply = new Button { Size = TextSize.Sm, Content = "应用" };
        var filtering = new Popover
        {
            Placement = FloatingPlacement.BottomStart,
            Trigger = trigger,
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children = { picked, new Flex { Gap = Space.Sm, Justify = FlexJustify.End, Children = { clear, apply } } },
            },
        };
        clear.Click += (_, _) => picked.Value = [];
        apply.Click += (_, _) =>
        {
            applied = picked.Value;
            filtering.IsOpen = false;
            Show();
        };
        Show();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start,
            Children = { new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { sharing, filtering } }, summary },
        };
    }

    static readonly (IconData Icon, string Label, string? Shortcut)[][] Groups =
    [
        [(Lucide.LetterB, "加粗", "Ctrl B"), (Lucide.Italic, "斜体", "Ctrl I"), (Lucide.Underline, "下划线", "Ctrl U"), (Lucide.Strikethrough, "删除线", null)],
        [(Lucide.List, "无序列表", null), (Lucide.ListOrdered, "有序列表", null), (Lucide.Quote, "引用", null)],
        [(Lucide.Link2, "插入链接", "Ctrl K")],
    ];

    static readonly (string Key, string Name, string Handle, string Bio, string City, string Joined, string Followers)[] People =
    [
        ("yihang", "周一航", "@yihang", "交互设计师，做了八年工具类产品。喜欢把复杂的事讲简单。", "上海", "2019 年加入", "1,284"),
        ("xiaoman", "林小满", "@xiaoman", "前端工程师，vue-tsx-pro 的维护者之一。", "杭州", "2021 年加入", "3,096"),
    ];

    /// <summary>一个人的悬停卡片：触发器是 @名字 的链接。</summary>
    static HoverCard Mention(int index)
    {
        var person = People[index];
        var meta = new Flex
        {
            Gap = Space.Md, Wrap = true,
            Children =
            {
                new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Icon { Data = Lucide.MapPin, Tone = TextTone.Muted }, new Small { Tone = TextTone.Muted, Text = person.City } } },
                new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Icon { Data = Lucide.CalendarDays, Tone = TextTone.Muted }, new Small { Tone = TextTone.Muted, Text = person.Joined } } },
                new Small { Tone = TextTone.Muted, Text = $"{person.Followers} 关注者" },
            },
        };
        return new HoverCard
        {
            Trigger = new Link { Href = new Uri($"https://example.com/u/{person.Key}"), Content = person.Handle },
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Sm,
                Children =
                {
                    new Flex
                    {
                        Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md,
                        Children = { new Avatar { DisplayName = person.Name }, new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Content = "关注" } },
                    },
                    new Flex { Direction = FlexDirection.Column, Children = { new Strong { Text = person.Name }, new Small { Tone = TextTone.Muted, Text = person.Handle } } },
                    new Paragraph { Size = TextSize.Sm, Text = person.Bio },
                    meta,
                },
            },
        };
    }

    static Control Profile()
    {
        var text = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        text.Inlines!.Add(new global::Avalonia.Controls.Documents.Run("这一版的设计由 "));
        text.Inlines.Add(new global::Avalonia.Controls.Documents.InlineUIContainer(Mention(0)));
        text.Inlines.Add(new global::Avalonia.Controls.Documents.Run(" 负责，组件实现与文档是 "));
        text.Inlines.Add(new global::Avalonia.Controls.Documents.InlineUIContainer(Mention(1)));
        text.Inlines.Add(new global::Avalonia.Controls.Documents.Run(" 做的。把指针停在名字上，或用 Tab 聚焦它，就能看到资料卡。"));
        return text;
    }

    static Control Timing()
    {
        (IconData Icon, string Label, string Keys)[] marks = [(Lucide.LetterB, "加粗", "Ctrl B"), (Lucide.Italic, "斜体", "Ctrl I"), (Lucide.Underline, "下划线", "Ctrl U")];
        var buttons = new Flex { Gap = Space.Xs };
        foreach (var (icon, label, keys) in marks)
        {
            var button = WithTip(new IconButton { Icon = icon, Variant = ButtonVariant.Ghost }, label, Tip(label, keys));
            ToolTip.SetShowDelay(button, 0);
            buttons.Children.Add(button);
        }
        var hints = new Switch { Content = "显示按钮提示", IsChecked = true };
        hints.PropertyChanged += (_, e) =>
        {
            if (e.Property != ChoiceBase.IsCheckedProperty) return;
            foreach (var button in buttons.Children) ToolTip.SetServiceEnabled(button, hints.IsChecked == true);
        };
        var slow = WithTip(new Button { Variant = ButtonVariant.Outline, Content = "停一秒才提示" }, "停一秒才提示", "不常用的说明可以等久一点，免得指针划过时到处弹出");
        ToolTip.SetShowDelay(slow, 1000);
        var author = new HoverCard
        {
            Delay = TimeSpan.FromMilliseconds(200), CloseDelay = TimeSpan.FromMilliseconds(600), Offset = 12,
            Trigger = new Link { Href = new Uri("https://example.com/components/avatar"), Content = "@周一航" },
            Content = new Flex
            {
                Align = FlexAlign.Center, Gap = Space.Sm,
                Children =
                {
                    new Avatar { DisplayName = "周一航" },
                    new Flex { Direction = FlexDirection.Column, Children = { new Strong { Text = "周一航" }, new Small { Tone = TextTone.Muted, Text = "设计中心 · 高级交互设计师" } } },
                },
            },
        };
        var byline = new Small { Tone = TextTone.Muted };
        byline.Inlines!.Add(new global::Avalonia.Controls.Documents.Run("作者 "));
        byline.Inlines.Add(new global::Avalonia.Controls.Documents.InlineUIContainer(author));
        byline.Inlines.Add(new global::Avalonia.Controls.Documents.Run(" 发布于 3 天前"));
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Start,
            Children =
            {
                new Flex { Gap = Space.Md, Align = FlexAlign.Center, Wrap = true, Children = { buttons, hints } },
                new Flex { Gap = Space.Lg, Align = FlexAlign.Center, Wrap = true, Children = { slow, byline } },
            },
        };
    }

    static Control Toolbar()
    {
        var row = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Wrap = true };
        foreach (var (group, index) in Groups.Select((group, index) => (group, index)))
        {
            if (index > 0) row.Children.Add(new Separator { Orientation = SeparatorOrientation.Vertical });
            foreach (var (icon, label, shortcut) in group) row.Children.Add(WithTip(new IconButton { Icon = icon }, label, Tip(label, shortcut)));
        }
        row.Children.Add(new Separator { Orientation = SeparatorOrientation.Vertical });
        row.Children.Add(WithTip(new IconButton { Icon = Lucide.Ellipsis }, "更多格式", "更多格式"));
        return new Card { Padding = Space.Xs, Content = row };
    }

    /// <summary>提示的内容：用途，有快捷键时跟着按键（Ctrl + B）。</summary>
    static object Tip(string label, string? shortcut)
    {
        if (shortcut is null) return label;
        var content = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Text { Text = label } } };
        foreach (var (key, index) in shortcut.Split(' ').Select((key, index) => (key, index)))
        {
            if (index > 0) content.Children.Add(new Text { Text = "+" });
            content.Children.Add(new Kbd { Content = key });
        }
        return content;
    }

    static Control WithTip(Control trigger, string name, object tip)
    {
        AutomationProperties.SetName(trigger, name);
        ToolTip.SetTip(trigger, tip);
        return trigger;
    }

    /// <summary>三行三列围着中间：每条边三个对齐方式。</summary>
    static readonly FloatingPlacement?[][] Rows =
    [
        [null, FloatingPlacement.TopStart, FloatingPlacement.Top, FloatingPlacement.TopEnd, null],
        [FloatingPlacement.LeftStart, null, null, null, FloatingPlacement.RightStart],
        [FloatingPlacement.Left, null, null, null, FloatingPlacement.Right],
        [FloatingPlacement.LeftEnd, null, null, null, FloatingPlacement.RightEnd],
        [null, FloatingPlacement.BottomStart, FloatingPlacement.Bottom, FloatingPlacement.BottomEnd, null],
    ];

    static Control Placements()
    {
        // CSS 的 grid：五列按内容宽、间距 space.sm（0.5em，画布的字号 md 是 14px），整体居中
        var grid = new PlatformGrid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            ColumnSpacing = 7, RowSpacing = 7,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
        };
        for (var row = 0; row < Rows.Length; row++)
        for (var column = 0; column < Rows[row].Length; column++)
        {
            if (Rows[row][column] is not { } placement) continue;
            var button = new Button { Content = placement.Value, Size = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral };
            ToolTip.SetTip(button, $"placement=\"{placement.Value}\"");
            Tooltip.SetPlacement(button, placement);
            Tooltip.SetArrow(button, true);
            PlatformGrid.SetRow(button, row);
            PlatformGrid.SetColumn(button, column);
            grid.Children.Add(button);
        }
        return grid;
    }
}
