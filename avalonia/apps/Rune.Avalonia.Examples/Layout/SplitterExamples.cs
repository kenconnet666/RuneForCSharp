using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>Splitter 页的示例（vue-tsx-pro 文档站 layout/Splitter）。</summary>
static partial class SplitterExamples
{
    public static Example[] All =>
    [
        new("Splitter", "master-detail", "主从视图", "Start、End 是两栏的内容，Size 是起始一栏的百分比（双向绑定，这里记在静态字段里，换个示例再回来比例还在）。Collapsible 让一侧可以收起：分隔条中间有小按钮，收起的一栏隐藏、另一栏占满；拖动或用键盘调比例时自动展开。两栏各自滚动，外层要给高度。", MasterDetail),
        new("Splitter", "vertical", "上下排列", "Orientation=Vertical 上下排，分隔条横着、用 ↑ / ↓；MinSize、MaxSize 限定比例的范围。", Vertical),
        new("Splitter", "resize-handle", "只拖宽一个元素：ResizeHandle", "ResizeHandle 是贴着元素结束边的一条细线：放在要调整的那个 Panel 里（叠在内容上），改的是这个 Panel 的 Width，已经夹在 MinLength、MaxLength 之间；聚焦后按 ← / → 每次调整 Step（默认 16 像素，拖动的键盘替代），写了 DefaultLength 时双击回到它。手柄读屏是分隔符，名称写在 AutomationProperties.Name 上。", ResizeHandleDemo),
    ];

    sealed record Mail(int Id, string From, string Title, string Body);

    static readonly Mail[] Mails =
    [
        new(1, "财务部", "费用核算待复核", "测算参数与报价不一致，请复核材料单价。"),
        new(2, "PMO", "派单：机柜导轨第二供应商", "请在 5 个工作日内完成方案设计。"),
        new(3, "财务", "结算超 30 天提醒", "显示器支架的结算已经 32 天。"),
    ];

    // 真实应用里存进用户设置（TS 用 usePersistentState 记在浏览器里）
    static double savedSize = 40;
    static bool savedHidden;

    static Control MasterDetail()
    {
        var list = new ItemList { Inset = Space.Md };
        AutomationProperties.SetName(list, "消息");
        var title = new Text { Weight = TextWeight.Semibold };
        var from = new Small { Tone = TextTone.Muted };
        var body = new Text { Size = TextSize.Sm };
        var items = new List<ItemListItem>();
        void Show(Mail mail)
        {
            title.Text = mail.Title;
            from.Text = $"来自 {mail.From}";
            body.Text = mail.Body;
            foreach (var item in items) item.IsSelected = Equals(item.Tag, mail);
        }
        foreach (var mail in Mails)
        {
            var item = new ItemListItem { Title = mail.Title, Description = mail.From, Tag = mail };
            item.Invoked += (_, _) => Show(mail);
            items.Add(item);
            list.Children.Add(item);
        }
        Show(Mails[0]);
        var splitter = new Splitter
        {
            Height = 16 * 16, Size = savedSize, Collapsible = PaneSide.End, IsCollapsed = savedHidden,
            Start = list,
            End = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Margin = new Thickness(12), Children = { title, from, body } },
        };
        splitter.PropertyChanged += (_, e) =>
        {
            if (e.Property == Splitter.SizeProperty) savedSize = splitter.Size;
            else if (e.Property == Splitter.IsCollapsedProperty) savedHidden = splitter.IsCollapsed;
        };
        return new Card { Variant = CardVariant.Outline, Padding = Space.None, Radius = Radius.Md, Content = splitter };
    }

    [GeneratedRegex(@"^\d+[.、]\s*")]
    private static partial Regex Numbered();

    /// <summary>文字按行渲染：连续的带编号的行是一个有序列表，其余每行一段。</summary>
    static IEnumerable<Control> Preview(string text)
    {
        OrderedList? list = null;
        foreach (var line in text.Split('\n').Where(line => line.Trim().Length > 0))
        {
            if (Numbered().IsMatch(line))
            {
                if (list is null)
                {
                    list = new OrderedList();
                    yield return list;
                }
                list.Children.Add(new ListItem { Content = Numbered().Replace(line, "") });
                continue;
            }
            list = null;
            yield return new Paragraph { Text = line };
        }
    }

    static Control Vertical()
    {
        var editor = new Textarea
        {
            Text = "改进方案：\n1. 卡箍改用铝合金\n2. 合并两件支架，减少紧固件\n3. 线束改走机柜背面",
            VerticalAlignment = VerticalAlignment.Stretch, Margin = new Thickness(8),
        };
        AutomationProperties.SetName(editor, "方案");
        var caption = new Small { Tone = TextTone.Muted };
        var prose = new Prose();
        var preview = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Margin = new Thickness(8), Children = { caption, prose } };
        var splitter = new Splitter
        {
            Height = 18 * 16, Orientation = SplitterOrientation.Vertical, MinSize = 30, MaxSize = 70, Collapsible = PaneSide.Start,
            Start = editor, End = preview,
        };
        void Render()
        {
            caption.Text = $"预览（上栏 {splitter.Size}%）";
            prose.Children.Clear();
            prose.Children.AddRange(Preview(editor.Text ?? ""));
        }
        editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) Render();
        };
        splitter.PropertyChanged += (_, e) =>
        {
            if (e.Property == Splitter.SizeProperty) Render();
        };
        Render();
        return new Card { Variant = CardVariant.Outline, Padding = Space.None, Radius = Radius.Md, Content = splitter };
    }

    sealed record Part(string No, string Name, string Material, int Quantity);

    static readonly Part[] Parts =
    [
        new("SKU-0213", "显示器支架", "铝合金", 2),
        new("SKU-0475", "线缆收纳夹", "ABS", 8),
        new("SKU-0630", "包装纸箱", "瓦楞纸", 4),
        new("SKU-0847", "桌面挡板", "亚克力", 6),
    ];

    static Control ResizeHandleDemo()
    {
        var nav = new NavList { CurrentHref = Parts[0].No };
        AutomationProperties.SetName(nav, "零件");
        foreach (var part in Parts) nav.Children.Add(new NavItem { Href = part.No, Content = part.Name });
        var handle = new ResizeHandle { MinLength = 160, MaxLength = 360, DefaultLength = 220 };
        AutomationProperties.SetName(handle, "调整零件列表宽度");
        // 手柄叠在面板的内容上、贴着它的结束边（压在分隔线上）；改的是这个面板的 Width
        var line = new Border { BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(8), Child = nav };
        line.Bind(Border.BorderBrushProperty, line.GetResourceObservable("Rune.Brush.Border"));
        var panel = new Panel { Width = 220, ClipToBounds = true, Children = { line, handle } };
        var name = new Text { Weight = TextWeight.Semibold };
        var descriptions = new Descriptions { Columns = 2, Size = TextSize.Sm };
        var detail = new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Margin = new Thickness(12),
            Children = { name, descriptions, new Small { Tone = TextTone.Muted, Text = "拖动列表的右边、聚焦后按 ← / →，或双击回到 220 px" } },
        };
        void Render()
        {
            var part = Parts.First(item => item.No == nav.CurrentHref);
            name.Text = part.Name;
            descriptions.Items.Clear();
            descriptions.Items.AddRange(
            [
                new DescriptionItem { Label = "件号", Value = part.No },
                new DescriptionItem { Label = "材料牌号", Value = part.Material },
                new DescriptionItem { Label = "数量", Value = part.Quantity.ToString() },
                new DescriptionItem { Label = "列表宽度", Value = $"{panel.Width} px" },
            ]);
        }
        nav.PropertyChanged += (_, e) =>
        {
            if (e.Property == NavList.CurrentHrefProperty) Render();
        };
        handle.LengthChanged += (_, _) => Render();
        Render();
        var body = new DockPanel { Height = 12 * 16, Children = { panel, detail } };
        DockPanel.SetDock(panel, Dock.Left);
        return new Card { Variant = CardVariant.Outline, Padding = Space.None, Radius = Radius.Md, Content = body };
    }
}
