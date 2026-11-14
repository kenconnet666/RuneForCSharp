using System.Text.RegularExpressions;

namespace Rune.WinForms.Examples;

/// <summary>Splitter 页的示例（vue-tsx-pro 文档站 layout/Splitter）。</summary>
static partial class SplitterExamples
{
    public static Example[] All =>
    [
        new("Splitter", "master-detail", "主从视图", "Start、End 是两栏的内容控件，Ratio 是起始一栏的百分比（上游的 size：Control 自己有 Size），变了触发 RatioChanged（这里记在静态字段里，换个示例再回来比例还在）。Collapsible 让一侧可以收起：分隔条中间有小按钮，收起的一栏隐藏、另一栏占满；拖动或用键盘调比例时自动展开。两栏各自滚动，外层要给高度。", MasterDetail),
        new("Splitter", "vertical", "上下排列", "Orientation=Vertical 上下排，分隔条横着、用 ↑ / ↓；MinRatio、MaxRatio 限定比例的范围。", Vertical),
        new("Splitter", "resize-handle", "只拖宽一个元素：ResizeHandleControl", "ResizeHandleControl 放进要调整的那个控件里，自己贴着它的结束边、叠在内容上面，改的是这个控件的 Width，已经夹在 MinLength、MaxLength 之间；聚焦后按 ← / → 每次调整 Step（默认 16 像素，拖动的键盘替代），写了 DefaultLength 时双击回到它。手柄读屏是分隔符，名称写在 AccessibleName 上。", ResizeHandleDemo),
    ];

    sealed record Mail(int Id, string From, string Title, string Body);

    static readonly Mail[] Mails =
    [
        new(1, "财务部", "费用核算待复核", "测算参数与报价不一致，请复核材料单价。"),
        new(2, "PMO", "派单：机柜导轨第二供应商", "请在 5 个工作日内完成方案设计。"),
        new(3, "财务", "结算超 30 天提醒", "显示器支架的结算已经 32 天。"),
    ];

    // 真实应用里存进用户设置（TS 用 usePersistentState 记在浏览器里）
    static double savedRatio = 40;
    static bool savedHidden;

    static CardControl Frame(Control content) =>
        Build.Card(new CardControl { Variant = CardVariant.Outline, CardPadding = Space.None, Radius = Radius.Md, Dock = DockStyle.Top }, content);

    static Control MasterDetail()
    {
        var list = new ItemListControl { Inset = Space.Md, AccessibleName = "消息" };
        var title = new TextControl { Weight = TextWeight.Semibold };
        var from = new SmallControl { Tone = TextTone.Muted };
        var body = new TextControl { TextSize = TextSize.Sm };
        var items = new List<ItemListItemControl>();
        void Show(Mail mail)
        {
            title.Text = mail.Title;
            from.Text = $"来自 {mail.From}";
            body.Text = mail.Body;
            foreach (var item in items) item.IsSelected = Equals(item.Tag, mail);
        }
        foreach (var mail in Mails)
        {
            var item = new ItemListItemControl { Text = mail.Title, Description = mail.From, Tag = mail };
            item.Invoked += (_, _) => Show(mail);
            items.Add(item);
            list.Controls.Add(item);
        }
        Show(Mails[0]);
        var detail = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm }, title, from, body);
        detail.Padding = new Padding(detail.LogicalToDeviceUnits(12));
        var splitter = new SplitterControl { Dock = DockStyle.Top, Ratio = savedRatio, Collapsible = PaneSide.End, IsCollapsed = savedHidden, Start = list, End = detail };
        splitter.Height = splitter.LogicalToDeviceUnits(16 * 16);
        splitter.RatioChanged += (_, _) => savedRatio = splitter.Ratio;
        splitter.CollapsedChanged += (_, _) => savedHidden = splitter.IsCollapsed;
        return Frame(splitter);
    }

    [GeneratedRegex(@"^\d+[.、]\s*")]
    private static partial Regex Numbered();

    /// <summary>文字按行渲染：连续的带编号的行是一个有序列表，其余每行一段。</summary>
    static IEnumerable<Control> Preview(string text)
    {
        OrderedListControl? list = null;
        foreach (var line in text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Trim().Length > 0))
        {
            if (Numbered().IsMatch(line))
            {
                if (list is null)
                {
                    list = new OrderedListControl();
                    yield return list;
                }
                var item = new ListItemControl();
                item.Controls.Add(new ParagraphControl { Text = Numbered().Replace(line, ""), Dock = DockStyle.Top });
                list.Controls.Add(item);
                continue;
            }
            list = null;
            yield return new ParagraphControl { Text = line };
        }
    }

    static Control Vertical()
    {
        var editor = new TextareaControl { Text = "改进方案：\r\n1. 卡箍改用铝合金\r\n2. 合并两件支架，减少紧固件\r\n3. 线束改走机柜背面", Fixed = true, AccessibleName = "方案" };
        var top = new Panel();
        top.Padding = new Padding(top.LogicalToDeviceUnits(8));
        editor.Dock = DockStyle.Fill;
        top.Controls.Add(editor);
        var caption = new SmallControl { Tone = TextTone.Muted };
        var prose = new ProseControl();
        var preview = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm }, caption, prose);
        preview.Padding = new Padding(preview.LogicalToDeviceUnits(8));
        var splitter = new SplitterControl
        {
            Dock = DockStyle.Top, Orientation = SplitterOrientation.Vertical, MinRatio = 30, MaxRatio = 70, Collapsible = PaneSide.Start,
            Start = top, End = preview,
        };
        splitter.Height = splitter.LogicalToDeviceUnits(18 * 16);
        void Render()
        {
            caption.Text = $"预览（上栏 {splitter.Ratio}%）";
            prose.SuspendLayout();
            foreach (var old in prose.Controls.Cast<Control>().ToList()) old.Dispose();
            prose.Controls.AddRange([.. Preview(editor.Text)]);
            prose.ResumeLayout();
        }
        editor.TextChanged += (_, _) => Render();
        splitter.RatioChanged += (_, _) => Render();
        Render();
        return Frame(splitter);
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
        var nav = new NavListControl { AccessibleName = "零件", CurrentHref = Parts[0].No, Dock = DockStyle.Fill };
        nav.Controls.AddRange([.. Parts.Select(part => new NavItemControl { Href = part.No, Text = part.Name })]);
        // 长度是设备像素：按 DPI 换算（列表宽度显示的是 96 DPI 下的像素）
        var panel = new Panel { Dock = DockStyle.Left };
        int Px(int logical) => panel.LogicalToDeviceUnits(logical);
        // 结束边留出分隔线的位置
        panel.Padding = new Padding(0, 0, Px(1), 0);
        panel.Width = Px(220);
        var handle = new ResizeHandleControl { AccessibleName = "调整零件列表宽度", MinLength = Px(160), MaxLength = Px(360), DefaultLength = Px(220) };
        var list = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Px(8)) };
        list.Controls.Add(nav);
        panel.Controls.AddRange([list, handle]);
        // 面板的结束边一条分隔线，画在面板上：手柄叠在上面（透明的子控件透出的是父控件画的东西，盖住兄弟控件）
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(ProviderControl.ThemeOf(panel).Colors.Border, Px(1));
            e.Graphics.DrawLine(pen, panel.Width - Px(1) / 2f - 0.5f, 0, panel.Width - Px(1) / 2f - 0.5f, panel.Height);
        };
        panel.Resize += (_, _) => panel.Invalidate();
        var name = new TextControl { Weight = TextWeight.Semibold };
        var descriptions = new DescriptionsControl { Columns = 2, TextSize = TextSize.Sm };
        var detail = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Padding = new Padding(Px(12)), Dock = DockStyle.Fill },
            name, descriptions, new SmallControl { Tone = TextTone.Muted, Text = "拖动列表的右边、聚焦后按 ← / →，或双击回到 220 px" });
        void Render()
        {
            var part = Parts.First(item => item.No == nav.CurrentHref);
            name.Text = part.Name;
            descriptions.Items.Clear();
            foreach (var item in new DescriptionItem[]
            {
                new() { Label = "件号", Value = part.No },
                new() { Label = "材料牌号", Value = part.Material },
                new() { Label = "数量", Value = part.Quantity.ToString() },
                new() { Label = "列表宽度", Value = $"{panel.Width * 96 / panel.DeviceDpi} px" },
            }) descriptions.Items.Add(item);
        }
        nav.CurrentHrefChanged += (_, _) => Render();
        handle.LengthChanged += (_, _) => Render();
        Render();
        var body = new Panel { Height = Px(12 * 16), Dock = DockStyle.Top };
        body.Controls.AddRange([detail, panel]);
        return Frame(body);
    }
}
