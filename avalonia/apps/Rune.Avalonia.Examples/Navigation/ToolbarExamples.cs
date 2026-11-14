using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace Rune.Avalonia.Examples;

/// <summary>Toolbar 页的示例（vue-tsx-pro 文档站 navigation/Toolbar）。</summary>
static class ToolbarExamples
{
    public static Example[] All =>
    [
        new("Toolbar", "editor", "编辑器工具栏", "Tab 进入工具栏，← → 在按钮之间移动，Home / End 到两端，再按 Tab 离开。里面可以放 ToggleGroup、Select 和分隔线；名称写在 AutomationProperties.Name。", Editor),
        new("Toolbar", "vertical", "竖排的绘图工具", "Orientation=Vertical 时用 ↑ ↓ 移动。选中的工具换成 soft 外观、accent 色调；图标按钮配提示说明用途。", Tools),
    ];

    static IconButton Icon(IconData icon, string name)
    {
        var button = new IconButton { Icon = icon };
        AutomationProperties.SetName(button, name);
        return button;
    }

    static ToggleItem Item(string value, IconData icon, string name)
    {
        var item = new ToggleItem { Value = value, Icon = icon };
        AutomationProperties.SetName(item, name);
        return item;
    }

    static Separator Divider() => new() { Orientation = SeparatorOrientation.Vertical };

    static Control Editor()
    {
        var font = new Select { ItemsSource = new[] { "正文", "标题 1", "标题 2", "引用" }, Value = "正文", Size = TextSize.Sm, Width = 112 };
        AutomationProperties.SetName(font, "段落样式");
        var marks = new ToggleGroup
        {
            SelectionMode = SelectionMode.Multiple, Values = ["bold"], Size = TextSize.Sm,
            Children = { Item("bold", Lucide.LetterB, "加粗"), Item("italic", Lucide.Italic, "斜体"), Item("underline", Lucide.Underline, "下划线") },
        };
        AutomationProperties.SetName(marks, "字形");
        var align = new ToggleGroup
        {
            IsRequired = true, Value = "left", Size = TextSize.Sm,
            Children = { Item("left", Lucide.TextAlignStart, "左对齐"), Item("center", Lucide.TextAlignCenter, "居中"), Item("right", Lucide.TextAlignEnd, "右对齐") },
        };
        AutomationProperties.SetName(align, "对齐");
        var toolbar = new Toolbar
        {
            Gap = Space.Xs,
            Children = { Icon(Lucide.Undo2, "撤销"), Icon(Lucide.Redo2, "重做"), Divider(), font, Divider(), marks, align, Divider(), Icon(Lucide.Link2, "插入链接") },
        };
        AutomationProperties.SetName(toolbar, "文字格式");

        var state = new Small { Tone = TextTone.Muted };
        void Show() => state.Text = $"{font.Value} · {(marks.Values is { Count: > 0 } values ? string.Join("、", values) : "无字形")} · {align.Value}";
        Show();
        font.PropertyChanged += (_, e) => { if (e.Property == Select.ValueProperty) Show(); };
        marks.PropertyChanged += (_, e) => { if (e.Property == ToggleGroup.ValuesProperty) Show(); };
        align.PropertyChanged += (_, e) => { if (e.Property == ToggleGroup.ValueProperty) Show(); };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { new Card { Padding = Space.Xs, Content = toolbar }, state } };
    }

    static Control Tools()
    {
        (string Value, string Label, IconData Icon)[] tools =
        [
            ("select", "选择", Lucide.MousePointer2), ("hand", "平移", Lucide.Hand), ("pencil", "画笔", Lucide.Pencil), ("rect", "矩形", Lucide.Square),
            ("ellipse", "椭圆", Lucide.Circle), ("text", "文字", Lucide.Type), ("eraser", "橡皮", Lucide.Eraser),
        ];
        var current = "pencil";
        var status = new Small { Tone = TextTone.Muted };
        var toolbar = new Toolbar { Orientation = ToolbarOrientation.Vertical };
        AutomationProperties.SetName(toolbar, "绘图工具");
        var buttons = new List<(string Value, IconButton Button)>();
        void Show()
        {
            foreach (var (value, button) in buttons)
            {
                button.Variant = value == current ? ButtonVariant.Soft : ButtonVariant.Ghost;
                button.Tone = value == current ? StatusTone.Accent : StatusTone.Neutral;
            }
            status.Text = $"当前工具：{tools.First(tool => tool.Value == current).Label}";
        }
        foreach (var (value, label, icon) in tools)
        {
            var button = Icon(icon, label);
            ToolTip.SetTip(button, label);
            ToolTip.SetPlacement(button, PlacementMode.Right);
            button.Click += (_, _) =>
            {
                current = value;
                Show();
            };
            buttons.Add((value, button));
            toolbar.Children.Add(button);
        }
        Show();
        var canvas = new GridPaper { MinHeight = 256, Padding = new Thickness(8), Child = status };
        var row = new Flex { Gap = Space.Md, Align = FlexAlign.Stretch, Children = { new Card { Padding = Space.Xs, Content = toolbar }, canvas } };
        Flex.SetGrow(canvas, true);
        return row;
    }

    /// <summary>画布：fill 底、borderMuted 的 24px 网格，md 圆角（跟着主题取色）。</summary>
    sealed class GridPaper : Decorator
    {
        public override void Render(DrawingContext context)
        {
            var theme = ActualThemeVariant;
            var fill = this.TryFindResource(ThemeKeys.Brush.Fill, theme, out var f) ? f as IBrush : null;
            var line = new Pen(this.TryFindResource(ThemeKeys.Brush.BorderMuted, theme, out var b) ? b as IBrush : null, 1);
            using (context.PushClip(new RoundedRect(new Rect(Bounds.Size), 8)))
            {
                context.FillRectangle(fill ?? Brushes.Transparent, new Rect(Bounds.Size));
                for (var x = 0.5; x < Bounds.Width; x += 24) context.DrawLine(line, new Point(x, 0), new Point(x, Bounds.Height));
                for (var y = 0.5; y < Bounds.Height; y += 24) context.DrawLine(line, new Point(0, y), new Point(Bounds.Width, y));
            }
            base.Render(context);
        }
    }
}
