namespace Rune.WinForms.Examples;

/// <summary>
/// Tabs 的示例（vue-tsx-pro 文档站 navigation/Tabs）。workspace（关闭、新建、拖动、全部标签页）要等 Menu；vertical 的面板要等 TextField。
/// WinForms 里是 TabsControl、TabListControl、TabItemControl、TabPanelControl（平台自带 TabControl）。
/// </summary>
static class TabsExamples
{
    public static Example[] All =>
    [
        new("Tabs", "line", "订单状态", "TabsControl 管当前值（Value），TabListControl 是一排 TabItemControl，每个对应一个 TabPanelControl，只显示当前的。方向键在标签之间移动，焦点到哪个就切到哪个；当前标签下方的线切换时滑过去。", Orders),
        new("Tabs", "soft", "浅色底与手动切换", "Variant = soft 像分段控件；Activation = manual 时方向键只移动焦点，按 Enter / 空格才切换。不可用的标签跳过。", Soft),
    ];

    static readonly (string Id, string Customer, string Amount, string Status)[] OrderList =
    [
        ("#10234", "林小满", "¥1,280.00", "shipped"), ("#10233", "周一航", "¥329.00", "unpaid"), ("#10232", "陈默", "¥4,299.00", "shipped"),
        ("#10231", "许悠", "¥659.00", "unpaid"), ("#10230", "王晴", "¥88.00", "done"),
    ];

    static readonly (string Value, string Label)[] Statuses = [("all", "全部"), ("unpaid", "待付款"), ("shipped", "已发货"), ("done", "已完成"), ("refund", "退款")];

    /// <summary>一行订单：上下 space.sm，底边一道 borderMuted。</summary>
    sealed class OrderRow : FlexControl
    {
        public OrderRow()
        {
            Gap = Space.Md;
            Align = FlexAlign.Center;
            AutoSize = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var line = new SolidBrush(ProviderControl.ThemeOf(this).Colors.BorderMuted);
            e.Graphics.FillRectangle(line, 0, Height - LogicalToDeviceUnits(1), Width, LogicalToDeviceUnits(1));
        }
    }

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
            foreach (var order in orders)
            {
                var row = new OrderRow();
                // 订单号一列宽 6rem：固定大小
                var id = new StrongControl { Text = order.Id };
                id.Size = new Size(id.LogicalToDeviceUnits(96), id.GetPreferredSize(Size.Empty).Height);
                id.AutoSize = false;
                var customer = new TextControl { Text = order.Customer, Tone = TextTone.Muted };
                row.Controls.AddRange([id, customer, new TextControl { Text = order.Amount }]);
                row.SetGrow(customer, true);
                row.Padding = new Padding(0, row.LogicalToDeviceUnits(7), 0, row.LogicalToDeviceUnits(7));
                panel.Controls.Add(row);
            }
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
