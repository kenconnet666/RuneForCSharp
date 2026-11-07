namespace Rune.WinForms.Examples;

/// <summary>
/// Popover 页的示例（vue-tsx-pro 文档站 overlay/Popover）：popover 与 Tooltip 的两节。hover-card 要等 HoverCard；
/// tooltip 里"更多格式"按钮的菜单要等 Menu；timing 要等 HoverCard。WinForms 的提示是文字（快捷键写在文字里）。
/// </summary>
static class PopoverExamples
{
    public static Example[] All =>
    [
        new("Popover", "popover", "Popover", "点触发器在旁边弹出一块内容：放筛选条件、简短表单、分享面板。点外面（浮层窗口失去激活）或 Esc 关闭，打开时焦点进到第一个能聚焦的控件，关上后回到触发器；OpenChanged 报告原因。浮层是以触发器为名称的对话框。", Share),
        new("Popover", "tooltip", "Tooltip", "给图标按钮补一句用途和快捷键：窗体里放一个 TooltipProvider，控件多出 Tooltip 属性。指针停一会儿显示，在一排按钮上移动时不用每次都等；键盘聚焦立即显示，Esc 隐藏。提示是控件的说明（AccessibleDescription），控件自己要有名称。", Toolbar),
        new("Popover", "placement", "位置与箭头", "TooltipPlacement 是边加对齐，共 12 种；空间不够时自动翻到另一边、贴着屏幕边挪回来。TooltipArrow 显示指向控件的小箭头。", Placements),
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
