using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace Rune.Wpf.Examples;

/// <summary>ToggleGroup 的示例（vue-tsx-pro 文档站 input/ToggleGroup）。</summary>
static class ToggleGroupExamples
{
    public static Example[] All =>
    [
        new("ToggleGroup", "segmented", "视图切换", "单选时 Value 是字符串；IsRequired 时至少保留一项（分段控件）。Icon 放在文字前；选中项的底色是一个滑块，切换时滑过去，减少动态效果时直接到位。", Views),
        new("ToggleGroup", "multiple", "多选的字形", "SelectionMode=\"multiple\" 时 Values 是列表，每一项各自开关。只有图标的项要写读屏名称。放进 Toolbar 时焦点交给工具栏统一管理。", Format),
        new("ToggleGroup", "vertical", "竖排与不可用", "Orientation=\"vertical\" 竖排，键盘改用 ↑↓。放进 Field 时以标签为名称，沿用 Field 的不可用；单项可以单独不可用。", Vertical),
    ];

    static ToggleGroup Group(string? name, params ToggleItem[] items)
    {
        var group = new ToggleGroup { IsRequired = true };
        foreach (var item in items) group.Children.Add(item);
        if (name is not null) AutomationProperties.SetName(group, name);
        return group;
    }

    static ToggleItem Item(string value, string? label, IconData? icon = null, string? name = null)
    {
        var item = new ToggleItem { Value = value, Content = label, Icon = icon };
        if (name is not null) AutomationProperties.SetName(item, name);
        return item;
    }

    static FrameworkElement Views()
    {
        var view = Group("视图", Item("list", "列表", Lucide.List), Item("board", "看板", Lucide.Columns3), Item("grid", "卡片", Lucide.LayoutGrid), Item("calendar", "日历", Lucide.CalendarDays));
        view.Value = "board";
        var period = Group("时间范围", Item("day", "日"), Item("week", "周"), Item("month", "月"), Item("year", "年"));
        period.Value = "week";
        period.Size = TextSize.Sm;
        var note = new Small { Tone = TextTone.Muted };
        void Update() => note.Text = $"{view.Value} · {period.Value}：required 时至少保留一项，再点已选项不会取消（分段控件）";
        var value = DependencyPropertyDescriptor.FromProperty(ToggleGroup.ValueProperty, typeof(ToggleGroup));
        value.AddValueChanged(view, (_, _) => Update());
        value.AddValueChanged(period, (_, _) => Update());
        Update();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children = { new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Wrap = FlexWrap.Wrap, Children = { view, period } }, note },
        };
    }

    static FrameworkElement Format()
    {
        var marks = new ToggleGroup
        {
            SelectionMode = Rune.Wpf.SelectionMode.Multiple, Values = ["bold"],
            Children = { Item("bold", null, Lucide.LetterB, "加粗"), Item("italic", null, Lucide.Italic, "斜体"), Item("underline", null, Lucide.Underline, "下划线"), Item("strike", null, Lucide.Strikethrough, "删除线") },
        };
        AutomationProperties.SetName(marks, "字形");
        var align = Group("对齐", Item("left", null, Lucide.TextAlignStart, "左对齐"), Item("center", null, Lucide.TextAlignCenter, "居中"), Item("right", null, Lucide.TextAlignEnd, "右对齐"), Item("justify", null, Lucide.TextAlignJustify, "两端对齐"));
        align.Value = "left";
        var text = new Paragraph { Text = "分段控件是一组互斥或可多选的按钮：方向键在组内移动，整组只占一个 Tab 位置。只有图标的项要写 aria-label。" };
        void Update()
        {
            var has = (string mark) => marks.Values?.Contains(mark) == true;
            text.FontWeight = has("bold") ? FontWeights.Bold : FontWeights.Normal;
            text.FontStyle = has("italic") ? FontStyles.Italic : FontStyles.Normal;
            var decorations = new TextDecorationCollection();
            if (has("underline")) decorations.Add(TextDecorations.Underline);
            if (has("strike")) decorations.Add(TextDecorations.Strikethrough);
            text.TextDecorations = decorations;
            text.TextAlignment = align.Value switch { "center" => TextAlignment.Center, "right" => TextAlignment.Right, "justify" => TextAlignment.Justify, _ => TextAlignment.Left };
        }
        DependencyPropertyDescriptor.FromProperty(ToggleGroup.ValuesProperty, typeof(ToggleGroup)).AddValueChanged(marks, (_, _) => Update());
        DependencyPropertyDescriptor.FromProperty(ToggleGroup.ValueProperty, typeof(ToggleGroup)).AddValueChanged(align, (_, _) => Update());
        Update();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children = { new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { marks, align } }, new Card { Content = text } },
        };
    }

    static FrameworkElement Vertical()
    {
        var appearance = Group(null, Item("light", "亮色", Lucide.Sun), Item("dark", "暗色", Lucide.Moon), Item("system", "跟随系统", Lucide.Monitor));
        appearance.Value = "system";
        appearance.Orientation = ToggleGroupOrientation.Vertical;
        var disabled = Group(null, Item("compact", "紧凑"), Item("comfortable", "适中"));
        disabled.Value = "comfortable";
        disabled.IsEnabled = false;
        var single = Group(null, Item("compact", "紧凑"), Item("comfortable", "适中"), Item("spacious", "宽松"));
        single.Value = "comfortable";
        single.Size = TextSize.Lg;
        single.Children[2].IsEnabled = false;
        return new Grid
        {
            MinItemWidth = "12rem", Gap = Space.Lg,
            Children =
            {
                new Field { Label = "外观", Content = appearance },
                new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Md,
                    Children = { new Field { Label = "整组不可用", Content = disabled }, new Field { Label = "单项不可用", Content = single } },
                },
            },
        };
    }
}
