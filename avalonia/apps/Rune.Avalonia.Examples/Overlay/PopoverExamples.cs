using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using PlatformGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia.Examples;

/// <summary>
/// Popover 页的示例（vue-tsx-pro 文档站 overlay/Popover）：popover 与 Tooltip 的两节。hover-card 要等 HoverCard；
/// tooltip 里"更多格式"按钮的菜单要等 Menu；timing 要等 HoverCard。
/// </summary>
static class PopoverExamples
{
    public static Example[] All =>
    [
        new("Popover", "popover", "Popover", "点触发器在旁边弹出一块内容：放筛选条件、简短表单、分享面板。点外面或 Esc 关闭，打开时焦点进到第一个能聚焦的元素，关上后回到触发器；IsOpen 可以双向绑定。浮层以触发器为名称。", Share),
        new("Popover", "tooltip", "Tooltip", "给图标按钮补一句用途和快捷键：写 ToolTip.Tip。指针停一会儿显示，在一排按钮上移动时不用每次都等；键盘聚焦立即显示，Esc 隐藏。提示是触发器的帮助文字，触发器自己要有名称，提示里不放可交互的内容。", Toolbar),
        new("Popover", "placement", "位置与箭头", "rune:Tooltip.Placement 是边加对齐，共 12 种；空间不够时自动翻到另一边。rune:Tooltip.Arrow 显示指向触发器的小箭头。", Placements),
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
