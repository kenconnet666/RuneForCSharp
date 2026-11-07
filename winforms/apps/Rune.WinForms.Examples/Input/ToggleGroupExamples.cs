namespace Rune.WinForms.Examples;

/// <summary>ToggleGroup 的示例（vue-tsx-pro 文档站 input/ToggleGroup）。WinForms 里是 ToggleGroupControl、ToggleItemControl。</summary>
static class ToggleGroupExamples
{
    public static Example[] All =>
    [
        new("ToggleGroup", "segmented", "视图切换", "单选时 Value 是字符串；IsRequired 时至少保留一项（分段控件）。Icon 放在文字前；选中项的底色是一个滑块，切换时滑过去。", Views),
        new("ToggleGroup", "multiple", "多选的字形", "SelectionMode = multiple 时 Values 是列表，每一项各自开关。只有图标的项要写 AccessibleName。放进 ToolbarControl 时焦点交给工具栏统一管理。", Format),
        new("ToggleGroup", "vertical", "竖排与不可用", "Orientation = vertical 竖排，键盘改用 ↑↓。放进 FieldControl 时以标签为名称；单项可以单独不可用。", Vertical),
    ];

    static ToggleGroupControl Group(string? name, params ToggleItemControl[] items)
    {
        var group = new ToggleGroupControl { IsRequired = true, AccessibleName = name };
        group.Controls.AddRange(items);
        return group;
    }

    static ToggleItemControl Item(string value, string? label, IconData? icon = null, string? name = null) =>
        new() { Value = value, Text = label ?? "", Icon = icon, AccessibleName = name };

    static Control Views()
    {
        var view = Group("视图", Item("list", "列表", Lucide.List), Item("board", "看板", Lucide.Columns3), Item("grid", "卡片", Lucide.LayoutGrid), Item("calendar", "日历", Lucide.CalendarDays));
        view.Value = "board";
        var period = Group("时间范围", Item("day", "日"), Item("week", "周"), Item("month", "月"), Item("year", "年"));
        period.Value = "week";
        period.TextSize = TextSize.Sm;
        var note = new SmallControl { Tone = TextTone.Muted };
        void Update() => note.Text = $"{view.Value} · {period.Value}：required 时至少保留一项，再点已选项不会取消（分段控件）";
        view.ValueChanged += (_, _) => Update();
        period.ValueChanged += (_, _) => Update();
        Update();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Wrap = FlexWrap.Wrap }, view, period), note);
    }

    static Control Format()
    {
        var marks = Group("字形", Item("bold", null, Lucide.LetterB, "加粗"), Item("italic", null, Lucide.Italic, "斜体"), Item("underline", null, Lucide.Underline, "下划线"), Item("strike", null, Lucide.Strikethrough, "删除线"));
        marks.IsRequired = null;
        marks.SelectionMode = SelectionMode.Multiple;
        marks.Values = ["bold"];
        var align = Group("对齐", Item("left", null, Lucide.TextAlignStart, "左对齐"), Item("center", null, Lucide.TextAlignCenter, "居中"), Item("right", null, Lucide.TextAlignEnd, "右对齐"), Item("justify", null, Lucide.TextAlignJustify, "两端对齐"));
        align.Value = "left";
        var text = new ParagraphControl { Text = "分段控件是一组互斥或可多选的按钮：方向键在组内移动，整组只占一个 Tab 位置。只有图标的项要写 aria-label。" };
        void Update()
        {
            bool Has(string mark) => marks.Values?.Contains(mark) == true;
            text.Weight = Has("bold") ? TextWeight.Bold : default;
            var style = (Has("italic") ? FontStyle.Italic : 0) | (Has("underline") ? FontStyle.Underline : 0) | (Has("strike") ? FontStyle.Strikeout : 0);
            text.Font = new Font(text.Parent?.Font ?? text.Font, style);
            text.Align = align.Value switch { "center" => TextAlign.Center, "right" => TextAlign.End, _ => TextAlign.Start };
        }
        marks.ValuesChanged += (_, _) => Update();
        align.ValueChanged += (_, _) => Update();
        Update();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
            Build.Flex(new FlexControl { Gap = Space.Sm, Wrap = FlexWrap.Wrap }, marks, align), Build.Card(new CardControl(), text));
    }

    static Control Vertical()
    {
        var appearance = Group(null, Item("light", "亮色", Lucide.Sun), Item("dark", "暗色", Lucide.Moon), Item("system", "跟随系统", Lucide.Monitor));
        appearance.Value = "system";
        appearance.Orientation = ToggleGroupOrientation.Vertical;
        var disabled = Group(null, Item("compact", "紧凑"), Item("comfortable", "适中"));
        disabled.Value = "comfortable";
        disabled.Enabled = false;
        var single = Group(null, Item("compact", "紧凑"), Item("comfortable", "适中"), Item("spacious", "宽松"));
        single.Value = "comfortable";
        single.TextSize = TextSize.Lg;
        single.Controls[2].Enabled = false;
        return Build.Grid(new GridControl { MinItemWidth = "12rem", Gap = Space.Lg },
            new FieldControl { Label = "外观", Content = appearance },
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
                new FieldControl { Label = "整组不可用", Content = disabled }, new FieldControl { Label = "单项不可用", Content = single }));
    }
}
