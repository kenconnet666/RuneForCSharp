namespace Rune.WinForms.Examples;

/// <summary>
/// Tabs 的示例（vue-tsx-pro 文档站 navigation/Tabs）。
/// WinForms 里是 TabsControl、TabListControl、TabItemControl、TabPanelControl（平台自带 TabControl）。
/// </summary>
static class TabsExamples
{
    public static Example[] All =>
    [
        new("Tabs", "workspace", "多页签：关闭、新建、拖动与全部标签页", "TabItemControl 订阅了 CloseRequested 就有关闭按钮（不进 Tab 顺序，焦点在标签上时按 Delete，鼠标中键也关）；关掉的是当前标签时选它后面的一个，焦点跟过去。TabListControl 订阅了 AddRequested 就在标签栏末尾放“新建标签页”，订阅了 Reordered 标签就能拖动排序（键盘：Alt + ← →），参数是新的顺序；ShowAllMenu 在右边放“全部标签页”的菜单。放不下时标签横向滚动，两端出现滚动按钮，切到的标签自己滚进来。", Workspace),
        new("Tabs", "line", "订单状态", "TabsControl 管当前值（Value），TabListControl 是一排 TabItemControl，每个对应一个 TabPanelControl，只显示当前的。方向键在标签之间移动，焦点到哪个就切到哪个；当前标签下方的线切换时滑过去。", Orders),
        new("Tabs", "soft", "浅色底与手动切换", "Variant = soft 像分段控件；Activation = manual 时方向键只移动焦点，按 Enter / 空格才切换。不可用的标签跳过。", Soft),
        new("Tabs", "vertical", "竖排", "Orientation = vertical：标签在左、内容在右，键盘改用 ↑↓。设置页这类分组多、标签文字长的场景。", Vertical),
    ];

    static readonly (string Id, string Customer, string Amount, string Status)[] OrderList =
    [
        ("#10234", "林小满", "¥1,280.00", "shipped"), ("#10233", "周一航", "¥329.00", "unpaid"), ("#10232", "陈默", "¥4,299.00", "shipped"),
        ("#10231", "许悠", "¥659.00", "unpaid"), ("#10230", "王晴", "¥88.00", "done"),
    ];

    static readonly (string Value, string Label)[] Statuses = [("all", "全部"), ("unpaid", "待付款"), ("shipped", "已发货"), ("done", "已完成"), ("refund", "退款")];

    static Control Orders()
    {
        var tabs = new TabsControl { Value = "all" };
        var list = new TabListControl { AccessibleName = "订单状态" };
        var badges = new List<(string Value, BadgeControl Badge)>();
        tabs.Controls.Add(list);
        foreach (var (value, label) in Statuses)
        {
            var orders = OrderList.Where(order => value == "all" || order.Status == value).ToArray();
            var badge = new BadgeControl { Radius = Radius.Full, Text = orders.Length.ToString() };
            badges.Add((value, badge));
            var tab = new TabItemControl { Value = value };
            tab.Controls.AddRange([new TextControl { Text = label }, badge]);
            list.Controls.Add(tab);
            var panel = new TabPanelControl { Value = value };
            if (orders.Length == 0) panel.Controls.Add(new EmptyControl { Title = $"没有{label}的订单" });
            var rows = new ItemListControl { Divided = true, Dock = DockStyle.Top };
            foreach (var order in orders)
                rows.Controls.Add(new ItemListItemControl { Text = order.Customer, Description = order.Id, End = new StrongControl { Text = order.Amount }, Href = new Uri($"https://example.com/orders/{order.Id[1..]}") });
            if (orders.Length > 0) panel.Controls.Add(rows);
            tabs.Controls.Add(panel);
        }
        tabs.Controls.Add(new SmallControl { Text = "方向键在标签之间移动，焦点到哪个就切到哪个", Tone = TextTone.Muted });
        // 当前标签的计数是品牌色，其余中性色
        void Tone()
        {
            foreach (var (value, badge) in badges) badge.Tone = tabs.SelectedValue == value ? StatusTone.Accent : StatusTone.Neutral;
        }
        tabs.ValueChanged += (_, _) => Tone();
        Tone();
        return tabs;
    }

    const string Source = """
        <Button tone="success" icon={Check}>
          保存
        </Button>
        """;

    static Control Soft()
    {
        var tabs = new TabsControl { Variant = TabsVariant.Soft, TextSize = TextSize.Sm, Activation = TabsActivation.Manual };
        var list = new TabListControl { AccessibleName = "查看方式" };
        TabItemControl Item(string value, IconData icon, string label, bool enabled = true)
        {
            var tab = new TabItemControl { Value = value, Enabled = enabled };
            tab.Controls.AddRange([new IconControl { Data = icon }, new TextControl { Text = label }]);
            return tab;
        }
        list.Controls.AddRange([Item("preview", Lucide.Eye, "预览"), Item("code", Lucide.CodeXml, "代码"), Item("split", Lucide.SquareSplitHorizontal, "并排（宽屏）", enabled: false)]);
        var preview = new TabPanelControl { Value = "preview" };
        preview.Controls.Add(new PreviewBox { Text = "这里是效果。Activation = manual：方向键只移动焦点，按 Enter / 空格才切换。" });
        var code = new TabPanelControl { Value = "code" };
        code.Controls.Add(new CodeBlockControl { Code = Source, Language = "tsx" });
        tabs.Controls.AddRange([list, preview, code]);
        return tabs;
    }

    static readonly (string Id, string Title, bool Pinned)[] Pages =
    [
        ("home", "工作台", true), ("po-2401", "采购单 PO-2401", false), ("po-2402", "采购单 PO-2402", false), ("supplier-17", "供应商：远山精密", false),
        ("report-q3", "第三季度销售报表", false), ("inspect-88", "来料检验 IQC-88", false), ("contract-5", "合同审批 CT-5", false),
    ];

    /// <summary>工作区的页签（同后台系统的多页签）：打开的单据各占一个标签，可以关闭、新建、拖动排序；首页固定，不能关。</summary>
    static Control Workspace()
    {
        var tabs = new TabsControl { Value = "po-2401" };
        var list = new TabListControl { AccessibleName = "打开的页面", ShowAllMenu = true };
        tabs.Controls.Add(list);
        var notes = new List<SmallControl>();
        var created = 0;
        // 面板里写着共几个页签
        void Count()
        {
            foreach (var note in notes) note.Text = $"拖动标签换顺序（或在标签上按 Alt + ← →），Delete 或中键关闭；共 {list.Controls.OfType<TabItemControl>().Count()} 个页签";
        }
        void Open(string id, string title, bool pinned)
        {
            var tab = new TabItemControl { Value = id, Text = title };
            var panel = new TabPanelControl { Value = id, Padding = new Padding(0, 14, 0, 14) };
            var note = new SmallControl { Tone = TextTone.Muted };
            notes.Add(note);
            panel.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs }, new TextControl { Text = title, Weight = TextWeight.Semibold }, note));
            if (!pinned)
                tab.CloseRequested += (_, _) =>
                {
                    list.Controls.Remove(tab);
                    tabs.Controls.Remove(panel);
                    notes.Remove(note);
                    tab.Dispose();
                    panel.Dispose();
                    Count();
                };
            list.Controls.Add(tab);
            tabs.Controls.Add(panel);
            Count();
        }
        foreach (var (id, title, pinned) in Pages) Open(id, title, pinned);
        list.AddRequested += (_, _) =>
        {
            created++;
            Open($"draft-{created}", $"新建草稿 {created}", false);
            tabs.Value = $"draft-{created}";
        };
        // TabListControl 已经挪好了标签；使用方据新顺序保存（这里不用另做什么）
        list.Reordered += (_, _) => { };
        return Build.Card(new CardControl { CardPadding = Space.Sm }, tabs);
    }

    static Control Vertical()
    {
        var tabs = new TabsControl { Orientation = TabsOrientation.Vertical };
        var list = new TabListControl { AccessibleName = "设置", MinimumSize = new Size(144, 0) };
        foreach (var (value, label, icon) in new[] { ("profile", "个人资料", Lucide.User), ("security", "账号安全", Lucide.KeyRound), ("notifications", "通知", Lucide.Bell) })
        {
            var tab = new TabItemControl { Value = value, Gap = Space.Sm };
            tab.Controls.AddRange([new IconControl { Data = icon }, new TextControl { Text = label }]);
            list.Controls.Add(tab);
        }
        static TabPanelControl Panel(string value, Space gap, params Control[] children)
        {
            var panel = new TabPanelControl { Value = value };
            panel.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = gap }, children));
            return panel;
        }
        tabs.Controls.AddRange([
            list,
            Panel("profile", Space.Md,
                new FieldControl { Label = "昵称", Content = new TextFieldControl { Text = "周一航" } },
                new FieldControl { Label = "个人主页", Content = new TextFieldControl { Type = TextFieldType.Url, PlaceholderText = "https://" } }),
            Panel("security", Space.Md, new SwitchControl { Text = "两步验证", IsChecked = true }, new SwitchControl { Text = "新设备登录时提醒" }),
            Panel("notifications", Space.Sm, new CheckboxControl { Text = "订单状态变化", IsChecked = true }, new CheckboxControl { Text = "有人提到我", IsChecked = true }, new CheckboxControl { Text = "每周报告" }),
        ]);
        return tabs;
    }

    /// <summary>效果区：fill 底、md 圆角、内边距 space.lg 的一段文字。</summary>
    sealed class PreviewBox : Control
    {
        public PreviewBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            // 大小按文字（Flex 只量自动大小的子控件）
            AutoSize = true;
        }

        int Inset => (int)MathF.Round(Font.SizeInPoints * DeviceDpi / 72f);

        public override Size GetPreferredSize(Size proposedSize)
        {
            // 不限宽时按一行量（Flex 之后拉满交叉轴）
            var width = proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : int.MaxValue;
            var text = TextRenderer.MeasureText(Text, Font, new Size(width == int.MaxValue ? int.MaxValue : Math.Max(1, width - 2 * Inset), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            return new Size(width == int.MaxValue ? text.Width + 2 * Inset : width, text.Height + 2 * Inset);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = ProviderControl.ThemeOf(this);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            var radius = theme.Radii.Md * Font.SizeInPoints * DeviceDpi / 72f;
            var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
            path.AddArc(bounds.X, bounds.Y, 2 * radius, 2 * radius, 180, 90);
            path.AddArc(bounds.Right - 2 * radius, bounds.Y, 2 * radius, 2 * radius, 270, 90);
            path.AddArc(bounds.Right - 2 * radius, bounds.Bottom - 2 * radius, 2 * radius, 2 * radius, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - 2 * radius, 2 * radius, 2 * radius, 90, 90);
            path.CloseFigure();
            using (var fill = new SolidBrush(theme.Colors.Fill)) e.Graphics.FillPath(fill, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -Inset, -Inset), theme.Colors.Text, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }
    }
}
