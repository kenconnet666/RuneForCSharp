namespace Rune.WinForms.Examples;

/// <summary>
/// Popover 页的示例（vue-tsx-pro 文档站 overlay/Popover）。tooltip 里"更多格式"按钮的菜单要等 Menu；compute 是网页的定位函数，WinForms 的定位在 FloatingLayout 里。
/// WinForms 的提示是文字（快捷键写在文字里）；段落里不能嵌控件，带悬停卡片的一段文字是按基线对齐、可以换行的一排控件。
/// </summary>
static class PopoverExamples
{
    public static Example[] All =>
    [
        new("Popover", "popover", "Popover", "点触发器在旁边弹出一块内容：放筛选条件、简短表单、分享面板。点外面（浮层窗口失去激活）或 Esc 关闭，打开时焦点进到第一个能聚焦的控件，关上后回到触发器；OpenChanged 报告原因。浮层是以触发器为名称的对话框。", Share),
        new("Popover", "tooltip", "Tooltip", "给图标按钮补一句用途和快捷键：窗体里放一个 TooltipProvider，控件多出 Tooltip 属性。指针停一会儿显示，在一排按钮上移动时不用每次都等；键盘聚焦立即显示，Esc 隐藏。提示是控件的说明（AccessibleDescription），控件自己要有名称。", Toolbar),
        new("Popover", "hover-card", "HoverCard", "悬停或键盘聚焦链接时弹出一张富内容卡片：可以移到卡片上，Esc、按下链接、焦点离开时关闭。卡片窗口不激活，焦点留在链接上（键盘不能 Tab 进卡片）。卡片只是预览，里面的信息在别处也要能拿到——触屏没有悬停。", Profile),
        new("Popover", "placement", "位置与箭头", "TooltipPlacement 是边加对齐，共 12 种；空间不够时自动翻到另一边、贴着屏幕边挪回来。TooltipArrow 显示指向控件的小箭头。", Placements),
        new("Popover", "timing", "显示的时机", "提示停多久才显示是 TooltipProvider 的 Delay（默认 500 毫秒），工具栏这类连续的按钮可以设成 0；Active=False 时不显示。HoverCardControl 的 Delay / CloseDelay 分别控制打开与离开后关闭的等待，Offset 是与触发器的距离（像素）。", Timing),
    ];

    static Control Share()
    {
        const string link = "https://example.com/s/q3-report";
        var linkField = new TextFieldControl { Text = link, IsReadOnly = true, AccessibleName = "分享链接" };
        var linkRow = new FlexControl { Gap = Space.Xs };
        linkRow.Controls.AddRange([linkField, new CopyButtonControl { Value = link }]);
        linkRow.SetGrow(linkField, true);
        var shareContent = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, AutoSize = true };
        shareContent.Controls.AddRange([new StrongControl { Text = "分享“第三季度报告”" }, linkRow, new SwitchControl { Text = "允许查看者评论", IsChecked = true }]);
        shareContent.MinimumSize = new Size(shareContent.LogicalToDeviceUnits(20 * 16), 0);
        var sharing = new PopoverControl
        {
            Placement = FloatingPlacement.BottomStart,
            Arrow = true,
            Trigger = new ButtonControl { Icon = Lucide.Share2, Text = "分享" },
            Content = shareContent,
        };

        string[] statuses = ["待付款", "备货中", "已发货", "已完成", "已退款"];
        IReadOnlyList<string> applied = ["待付款", "备货中"];
        var picked = new CheckboxGroupControl { Value = applied, AccessibleName = "订单状态" };
        picked.Controls.AddRange([.. statuses.Select(status => new CheckboxControl { Value = status, Text = status })]);
        var trigger = new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Funnel };
        var summary = new SmallControl { Tone = TextTone.Muted };
        void Show()
        {
            trigger.Text = applied.Count > 0 ? $"订单状态（{applied.Count}）" : "订单状态";
            summary.Text = $"筛选：{(applied.Count > 0 ? string.Join("、", applied) : "全部状态")}";
        }
        var clear = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Text = "清空" };
        var apply = new ButtonControl { TextSize = TextSize.Sm, Text = "应用" };
        var actions = new FlexControl { Gap = Space.Sm, Justify = FlexJustify.End };
        actions.Controls.AddRange([clear, apply]);
        var filterContent = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, AutoSize = true };
        filterContent.Controls.AddRange([picked, actions]);
        var filtering = new PopoverControl { Placement = FloatingPlacement.BottomStart, Trigger = trigger, Content = filterContent };
        clear.Click += (_, _) => picked.Value = [];
        apply.Click += (_, _) =>
        {
            applied = picked.Value;
            filtering.IsOpen = false;
            Show();
        };
        Show();
        var triggers = new FlexControl { Gap = Space.Sm, Wrap = FlexWrap.Wrap };
        triggers.Controls.AddRange([sharing, filtering]);
        var root = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start };
        root.Controls.AddRange([triggers, summary]);
        return root;
    }

    static readonly (IconData Icon, string Label, string? Shortcut)[][] Groups =
    [
        [(Lucide.LetterB, "加粗", "Ctrl+B"), (Lucide.Italic, "斜体", "Ctrl+I"), (Lucide.Underline, "下划线", "Ctrl+U"), (Lucide.Strikethrough, "删除线", null)],
        [(Lucide.List, "无序列表", null), (Lucide.ListOrdered, "有序列表", null), (Lucide.Quote, "引用", null)],
        [(Lucide.Link2, "插入链接", "Ctrl+K")],
    ];

    /// <summary>示例自己的提示组件，随示例一起释放。</summary>
    static TooltipProvider TipsFor(Control example)
    {
        var tips = new TooltipProvider();
        example.Disposed += (_, _) => tips.Dispose();
        return tips;
    }

    static readonly (string Key, string Name, string Handle, string Bio, string City, string Joined, string Followers)[] People =
    [
        ("yihang", "周一航", "@yihang", "交互设计师，做了八年工具类产品。喜欢把复杂的事讲简单。", "上海", "2019 年加入", "1,284"),
        ("xiaoman", "林小满", "@xiaoman", "前端工程师，vue-tsx-pro 的维护者之一。", "杭州", "2021 年加入", "3,096"),
    ];

    static Control IconText(IconData icon, string text) =>
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs }, new IconControl { Data = icon, Tone = TextTone.Muted }, new SmallControl { Tone = TextTone.Muted, Text = text });

    /// <summary>一个人的悬停卡片：触发器是 @名字 的链接。</summary>
    static HoverCardControl Mention(int index)
    {
        var person = People[index];
        var top = Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md },
            new AvatarControl { DisplayName = person.Name }, new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Text = "关注" });
        var name = Build.Flex(new FlexControl { Direction = FlexDirection.Column }, new StrongControl { Text = person.Name }, new SmallControl { Tone = TextTone.Muted, Text = person.Handle });
        var meta = Build.Flex(new FlexControl { Gap = Space.Md, Wrap = true },
            IconText(Lucide.MapPin, person.City), IconText(Lucide.CalendarDays, person.Joined), new SmallControl { Tone = TextTone.Muted, Text = $"{person.Followers} 关注者" });
        var content = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch },
            top, name, new ParagraphControl { TextSize = TextSize.Sm, Text = person.Bio }, meta);
        return new HoverCardControl { Trigger = new LinkControl { Href = new Uri($"https://example.com/u/{person.Key}"), Text = person.Handle }, Content = content };
    }

    static Control Profile() => Build.Flex(new FlexControl { Align = FlexAlign.Baseline, Wrap = true },
        new TextControl { Text = "这一版的设计由 " }, Mention(0), new TextControl { Text = " 负责，组件实现与文档是 " }, Mention(1),
        new TextControl { Text = " 做的。把指针停在名字上，或用 Tab 聚焦它，就能看到资料卡。" });

    static Control Timing()
    {
        (IconData Icon, string Label, string Keys)[] marks = [(Lucide.LetterB, "加粗", "Ctrl+B"), (Lucide.Italic, "斜体", "Ctrl+I"), (Lucide.Underline, "下划线", "Ctrl+U")];
        var buttons = new FlexControl { Gap = Space.Xs };
        var quick = TipsFor(buttons);
        quick.Delay = 0;
        foreach (var (icon, label, keys) in marks)
        {
            var button = new IconButtonControl { Icon = icon, Variant = ButtonVariant.Ghost, AccessibleName = label };
            quick.SetTooltip(button, $"{label}  {keys}");
            buttons.Controls.Add(button);
        }
        var hints = new SwitchControl { Text = "显示按钮提示", IsChecked = true };
        hints.IsCheckedChanged += (_, _) => quick.Active = hints.IsChecked;
        var slow = new ButtonControl { Variant = ButtonVariant.Outline, Text = "停一秒才提示" };
        var patient = TipsFor(slow);
        patient.Delay = 1000;
        patient.SetTooltip(slow, "不常用的说明可以等久一点，免得指针划过时到处弹出");
        var author = new HoverCardControl
        {
            Delay = TimeSpan.FromMilliseconds(200), CloseDelay = TimeSpan.FromMilliseconds(600), Offset = 12,
            Trigger = new LinkControl { Href = new Uri("https://example.com/components/avatar"), Text = "@周一航" },
            Content = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm },
                new AvatarControl { DisplayName = "周一航" },
                Build.Flex(new FlexControl { Direction = FlexDirection.Column }, new StrongControl { Text = "周一航" }, new SmallControl { Tone = TextTone.Muted, Text = "设计中心 · 高级交互设计师" })),
        };
        var byline = Build.Flex(new FlexControl { Align = FlexAlign.Baseline }, new SmallControl { Tone = TextTone.Muted, Text = "作者 " }, author, new SmallControl { Tone = TextTone.Muted, Text = " 发布于 3 天前" });
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
            Build.Flex(new FlexControl { Gap = Space.Md, Align = FlexAlign.Center, Wrap = true }, buttons, hints),
            Build.Flex(new FlexControl { Gap = Space.Lg, Align = FlexAlign.Center, Wrap = true }, slow, byline));
    }

    static Control Toolbar()
    {
        var row = new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs, Wrap = true };
        var tips = TipsFor(row);
        foreach (var (group, index) in Groups.Select((group, index) => (group, index)))
        {
            if (index > 0) row.Controls.Add(new SeparatorControl { Orientation = SeparatorOrientation.Vertical });
            foreach (var (icon, label, shortcut) in group)
            {
                var button = new IconButtonControl { Icon = icon, AccessibleName = label };
                tips.SetTooltip(button, shortcut is null ? label : $"{label}  {shortcut}");
                row.Controls.Add(button);
            }
        }
        row.Controls.Add(new SeparatorControl { Orientation = SeparatorOrientation.Vertical });
        var more = new IconButtonControl { Icon = Lucide.Ellipsis, AccessibleName = "更多格式" };
        tips.SetTooltip(more, "更多格式");
        row.Controls.Add(more);
        return Build.Card(new CardControl { CardPadding = Space.Xs }, row);
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
        // CSS 的 grid：五列按内容宽、间距 space.sm（0.5em，画布的字号 md 是 14px，7 像素）。按钮四周本来留着 4 像素画焦点环，相邻两个之间是 8，差 1 像素
        var table = new TableLayoutPanel { ColumnCount = 5, RowCount = 5, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Top };
        for (var i = 0; i < 5; i++)
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        var tips = TipsFor(table);
        for (var row = 0; row < Rows.Length; row++)
        for (var column = 0; column < Rows[row].Length; column++)
        {
            if (Rows[row][column] is not { } placement) continue;
            var button = new ButtonControl { Text = placement.Value, TextSize = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, AutoSize = true, Margin = new Padding(0) };
            tips.SetTooltip(button, $"placement=\"{placement.Value}\"");
            tips.SetTooltipPlacement(button, placement);
            tips.SetTooltipArrow(button, true);
            table.Controls.Add(button, column, row);
        }
        return table;
    }
}
