using System.Drawing.Drawing2D;

namespace Rune.WinForms.Examples;

/// <summary>Toolbar 页的示例（vue-tsx-pro 文档站 navigation/Toolbar）。</summary>
static class ToolbarExamples
{
    public static Example[] All =>
    [
        new("Toolbar", "editor", "编辑器工具栏", "Tab 进入工具栏，← → 在按钮之间移动，Home / End 到两端，再按 Tab 离开。里面可以放 ToggleGroupControl、SelectControl 和分隔线；名称写在 AccessibleName。", Editor),
        new("Toolbar", "vertical", "竖排的绘图工具", "Orientation=Vertical 时用 ↑ ↓ 移动。选中的工具换成 soft 外观、accent 色调；图标按钮配提示说明用途。", Tools),
    ];

    static ToggleItemControl Item(string value, IconData icon, string name) => new() { Value = value, Icon = icon, AccessibleName = name };

    static SeparatorControl Divider() => new() { Orientation = SeparatorOrientation.Vertical };

    static Control Editor()
    {
        var font = new SelectControl { ItemsSource = new[] { "正文", "标题 1", "标题 2", "引用" }, Value = "正文", TextSize = TextSize.Sm, Width = 112, AccessibleName = "段落样式" };
        var marks = new ToggleGroupControl { SelectionMode = SelectionMode.Multiple, TextSize = TextSize.Sm, AccessibleName = "字形" };
        marks.Controls.AddRange([Item("bold", Lucide.LetterB, "加粗"), Item("italic", Lucide.Italic, "斜体"), Item("underline", Lucide.Underline, "下划线")]);
        marks.Values = ["bold"];
        var align = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, AccessibleName = "对齐" };
        align.Controls.AddRange([Item("left", Lucide.TextAlignStart, "左对齐"), Item("center", Lucide.TextAlignCenter, "居中"), Item("right", Lucide.TextAlignEnd, "右对齐")]);
        align.Value = "left";
        var toolbar = Build.Flex(new ToolbarControl { Gap = Space.Xs, AccessibleName = "文字格式" },
            new IconButtonControl { Icon = Lucide.Undo2, AccessibleName = "撤销" }, new IconButtonControl { Icon = Lucide.Redo2, AccessibleName = "重做" }, Divider(),
            font, Divider(), marks, align, Divider(), new IconButtonControl { Icon = Lucide.Link2, AccessibleName = "插入链接" });

        var state = new SmallControl { Tone = TextTone.Muted };
        void Show() => state.Text = $"{font.Value} · {(marks.Values is { Count: > 0 } values ? string.Join("、", values) : "无字形")} · {align.Value}";
        Show();
        font.ValueChanged += (_, _) => Show();
        marks.ValuesChanged += (_, _) => Show();
        align.ValueChanged += (_, _) => Show();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch },
            Build.Card(new CardControl { CardPadding = Space.Xs }, toolbar), state);
    }

    static Control Tools()
    {
        (string Value, string Label, IconData Icon)[] tools =
        [
            ("select", "选择", Lucide.MousePointer2), ("hand", "平移", Lucide.Hand), ("pencil", "画笔", Lucide.Pencil), ("rect", "矩形", Lucide.Square),
            ("ellipse", "椭圆", Lucide.Circle), ("text", "文字", Lucide.Type), ("eraser", "橡皮", Lucide.Eraser),
        ];
        var current = "pencil";
        var status = new SmallControl { Tone = TextTone.Muted, Location = new Point(8, 8), BackColor = Color.Transparent };
        var toolbar = new ToolbarControl { Orientation = ToolbarOrientation.Vertical, AccessibleName = "绘图工具" };
        var tips = new TooltipProvider();
        var buttons = new List<(string Value, IconButtonControl Button)>();
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
            var button = new IconButtonControl { Icon = icon, AccessibleName = label };
            tips.SetTooltip(button, label);
            tips.SetTooltipPlacement(button, FloatingPlacement.Right);
            button.Click += (_, _) =>
            {
                current = value;
                Show();
            };
            buttons.Add((value, button));
            toolbar.Controls.Add(button);
        }
        Show();
        var canvas = new GridPaper();
        canvas.Controls.Add(status);
        var row = Build.Flex(new FlexControl { Gap = Space.Md, Align = FlexAlign.Stretch }, Build.Card(new CardControl { CardPadding = Space.Xs }, toolbar), canvas);
        row.SetGrow(canvas, true);
        row.Disposed += (_, _) => tips.Dispose();
        return row;
    }

    /// <summary>画布：fill 底、borderMuted 的 24px 网格，md 圆角（跟着主题取色）。</summary>
    sealed class GridPaper : Panel
    {
        public GridPaper()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            // FlexControl 只拉伸 AutoSize 的子控件：高度跟着工具栏，至少 16em
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            MinimumSize = new Size(0, 256 * DeviceDpi / 96);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            MinimumSize = new Size(0, 256 * DeviceDpi / 96);
        }

        // 画在背景里：透明的子控件（状态文字）透出来的是网格
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var colors = ProviderControl.ThemeOf(this).Colors;
            var step = 24 * DeviceDpi / 96f;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var radius = 2 * 8 * DeviceDpi / 96f;
            using var clip = new GraphicsPath();
            clip.AddArc(0, 0, radius, radius, 180, 90);
            clip.AddArc(Width - radius, 0, radius, radius, 270, 90);
            clip.AddArc(Width - radius, Height - radius, radius, radius, 0, 90);
            clip.AddArc(0, Height - radius, radius, radius, 90, 90);
            clip.CloseFigure();
            e.Graphics.SetClip(clip);
            using (var fill = new SolidBrush(colors.Fill)) e.Graphics.FillRectangle(fill, ClientRectangle);
            e.Graphics.SmoothingMode = SmoothingMode.None;
            using var line = new Pen(colors.BorderMuted);
            for (var x = 0f; x < Width; x += step) e.Graphics.DrawLine(line, x, 0, x, Height);
            for (var y = 0f; y < Height; y += step) e.Graphics.DrawLine(line, 0, y, Width, y);
        }
    }
}
