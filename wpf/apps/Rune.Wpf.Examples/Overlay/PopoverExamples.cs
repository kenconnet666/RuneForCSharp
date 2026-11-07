using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PlatformGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf.Examples;

/// <summary>
/// Popover 页的示例（vue-tsx-pro 文档站 overlay/Popover）：先有 Tooltip 的两节。popover、hover-card 要等 Popover、HoverCard；
/// tooltip 里"更多格式"按钮的菜单要等 Menu；timing 要等 HoverCard。
/// </summary>
static class PopoverExamples
{
    public static Example[] All =>
    [
        new("Popover", "tooltip", "Tooltip", "给图标按钮补一句用途和快捷键：写平台的 ToolTip。指针停一会儿显示，在一排按钮上移动时不用每次都等；键盘聚焦时显示，Esc 隐藏。提示是触发器的帮助文字，触发器自己要有名称，提示里不放可交互的内容。", Toolbar),
        new("Popover", "placement", "位置与箭头", "rune:Tooltip.Placement 是边加对齐，共 12 种；空间不够时自动翻到另一边。rune:Tooltip.Arrow 显示指向触发器的小箭头。", Placements),
    ];

    static readonly (IconData Icon, string Label, string? Shortcut)[][] Groups =
    [
        [(Lucide.LetterB, "加粗", "Ctrl B"), (Lucide.Italic, "斜体", "Ctrl I"), (Lucide.Underline, "下划线", "Ctrl U"), (Lucide.Strikethrough, "删除线", null)],
        [(Lucide.List, "无序列表", null), (Lucide.ListOrdered, "有序列表", null), (Lucide.Quote, "引用", null)],
        [(Lucide.Link2, "插入链接", "Ctrl K")],
    ];

    static FrameworkElement Toolbar()
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

    static FrameworkElement WithTip(FrameworkElement trigger, string name, object tip)
    {
        AutomationProperties.SetName(trigger, name);
        trigger.ToolTip = tip;
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

    static FrameworkElement Placements()
    {
        // CSS 的 grid：五列按内容宽、间距 space.sm（0.5em，画布的字号 md 是 14px），整体居中。WPF 的 Grid 没有行列间距：每个按钮四周各留一半，整体再收回去
        var grid = new PlatformGrid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(-3.5) };
        for (var i = 0; i < 5; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        for (var row = 0; row < Rows.Length; row++)
        for (var column = 0; column < Rows[row].Length; column++)
        {
            if (Rows[row][column] is not { } placement) continue;
            var button = new Button { Content = placement.Value, Size = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Margin = new Thickness(3.5) };
            button.ToolTip = $"placement=\"{placement.Value}\"";
            Tooltip.SetPlacement(button, placement);
            Tooltip.SetArrow(button, true);
            PlatformGrid.SetRow(button, row);
            PlatformGrid.SetColumn(button, column);
            grid.Children.Add(button);
        }
        return grid;
    }
}
