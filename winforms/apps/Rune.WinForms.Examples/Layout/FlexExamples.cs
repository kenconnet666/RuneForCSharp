namespace Rune.WinForms.Examples;

/// <summary>Flex 页的示例（vue-tsx-pro 文档站 layout/Flex）：FlexControl、GridControl、GridListControl、FlexListControl、ContainerPanel、SeparatorControl。零件图片用图标代替（Image 还没做）。</summary>
static class FlexExamples
{
    public static Example[] All =>
    [
        new("Flex", "flex", "Flex", "不拆 Stack / Group：竖排写 Direction=Column。Gap 取主题的间距档位（none、xs–xl），整个应用的疏密跟着主题一起变。FlexControl 量、拉伸的是 AutoSize 的子控件。", Playground),
        new("Flex", "auto-grid", "自动换列", "MinItemWidth 给每项的最小宽度，容器多宽就放几列，不需要断点；项目少于放得下的列数时各项拉宽占满（auto-fit）。这里用 GridListControl：读屏念\"列表\"。固定几项的（一排指标卡、一组入口）加 Balanced 按项数取列数，最后一行最多空一格。", AutoGrid),
        new("Flex", "labelled-separator", "带文字的分隔线", "SeparatorControl 的 Text：默认居中，两边各一段线；LabelAlign=Start 文字在前、线在后，像一节的小标题。Decorative=False 时读屏里是分隔符，以文字为名称。", LabelledSeparators),
        new("Flex", "dashboard", "固定列数与跨格", "Columns 固定等宽列；GridControl.SetSpan、SetRowSpan 跨几列、几行，GridSpan.Full 占满一整行。Flow=RowDense 回填前面留下的空位。", Dashboard),
        new("Flex", "separator", "分隔线与列表", "SeparatorControl 默认是装饰性的、读屏里没有；分隔两组不同内容、需要读屏感知时写 Decorative=False。竖向的在 FlexControl 里自动撑满行高。", Separators),
        new("Flex", "container", "Container", "ContainerPanel 限制最大宽度并水平居中：xs 30rem（登录）、sm 40rem（表单、设置）、md 48rem（文章）、lg 64rem（默认）、xl 80rem（仪表盘）。", Containers),
        new("Flex", "grow", "伸展与行列间距", "SetGrow 让子控件占满剩余空间，并且可以收缩到比内容窄——头像旁边的长标题才能截断。GridListControl 的 RowGap、ColumnGap 分别覆盖 Gap：附件列表行与行挨得近、列与列隔得开。WinForms 没有行内排版，TS 的 inline 用基线对齐的一行代替。", Grow),
    ];

    static FlexControl Column(Space gap, params Control[] children) =>
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = gap, Align = FlexAlign.Stretch }, children);

    static ToggleGroupControl Choices(string name, string value, params string[] values)
    {
        var group = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, AccessibleName = name };
        group.Controls.AddRange([.. values.Select(item => new ToggleItemControl { Value = item, Text = item })]);
        group.Value = value;
        return group;
    }

    static Control Playground()
    {
        double[] heights = [2, 3.5, 2.5, 4, 3];
        var direction = Choices("direction", "row", "row", "column");
        var gap = Choices("gap", "sm", "none", "xs", "sm", "md", "lg", "xl");
        var align = Choices("align", "center", "start", "center", "end", "stretch", "baseline");
        var justify = Choices("justify", "start", "start", "center", "end", "between", "evenly");
        var wrap = new SwitchControl { AccessibleName = "wrap" };

        var stage = new FlexControl { Padding = new Padding(16), MinimumSize = new Size(0, 176) };
        for (var i = 0; i < heights.Length; i++)
            stage.Controls.Add(new Box(heights[i]) { Text = $"{i + 1}" });
        var code = new CodeTextControl();

        void Apply()
        {
            stage.Direction = FlexDirection.Parse(direction.Value!);
            stage.Gap = Space.Parse(gap.Value!);
            stage.Align = FlexAlign.Parse(align.Value!);
            stage.Justify = FlexJustify.Parse(justify.Value!);
            stage.Wrap = wrap.IsChecked;
            code.Source = $"`new FlexControl {{ Direction = {direction.Value}, Align = {align.Value}, Justify = {justify.Value}, Gap = {gap.Value}{(wrap.IsChecked ? ", Wrap = true" : "")} }}`";
        }
        foreach (var group in new[] { direction, gap, align, justify }) group.ValueChanged += (_, _) => Apply();
        wrap.IsCheckedChanged += (_, _) => Apply();
        Apply();

        static Control Control(string label, Control group) =>
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm }, new TextControl { Text = label, TextSize = TextSize.Sm, Tone = TextTone.Muted, MinimumSize = new Size(56, 0) }, group);
        return Column(Space.Md,
            Column(Space.Sm, Control("direction", direction), Control("gap", gap), Control("align", align), Control("justify", justify), Control("wrap", wrap)),
            Build.Card(new CardControl { CardPadding = Space.None }, stage), code);
    }

    /// <summary>演示用的方块：accentSoft 底、accentText 字，高矮不一（才看得出 align 的差别）。</summary>
    sealed class Box : Control
    {
        readonly double rem;

        public Box(double rem)
        {
            this.rem = rem;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AutoSize = true;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var px = 16 * DeviceDpi / 96.0;
            return new Size((int)Math.Max(3 * px, 2 * rem / 4 * px + TextRenderer.MeasureText(Text, Font).Width), (int)(rem * px));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ProviderControl.ThemeOf(this).Colors;
            using (var fill = new SolidBrush(colors.AccentSoft)) e.Graphics.FillRectangle(fill, ClientRectangle);
            using var bold = new Font(Font, FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics, Text, bold, ClientRectangle, colors.AccentText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    static Control AutoGrid()
    {
        (string Title, IconData Icon, int Price)[] parts =
        [
            ("法兰轴承座", Lucide.Cog, 86), ("直线导轨滑块", Lucide.Ruler, 128), ("同步带轮", Lucide.Disc, 245),
            ("内六角螺栓", Lucide.Wrench, 12), ("弹性联轴器", Lucide.Link, 39), ("气缸接头", Lucide.Plug, 64),
        ];
        var list = new GridListControl { MinItemWidth = "9rem", Gap = Space.Md, AccessibleName = "常用零件" };
        foreach (var (title, icon, price) in parts)
        {
            var cover = Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Center, MinimumSize = new Size(0, 88), BackColor = ProviderControl.ThemeOf(list).Colors.Fill },
                Build.Styled(new IconControl { Data = icon, Tone = TextTone.Muted }, TextSize.Xl));
            var priceRow = Build.Flex(new FlexControl { Align = FlexAlign.Baseline, Gap = Space.Xxs }, new StrongControl { Text = $"¥{price}" }, new SmallControl { Text = "/ 件", Tone = TextTone.Muted });
            list.Controls.Add(Build.Card(new CardControl { CardPadding = Space.Sm }, Column(Space.Xs, cover, new TextControl { Text = title, TextSize = TextSize.Sm }, priceRow)));
        }
        return list;
    }

    static Control LabelledSeparators()
    {
        static DescriptionsControl Spec(string code, string material)
        {
            var descriptions = new DescriptionsControl { Columns = 1, TextSize = TextSize.Sm, Dock = DockStyle.Top };
            descriptions.Items.Add(new DescriptionItem { Label = "件号", Value = code });
            descriptions.Items.Add(new DescriptionItem { Label = "材料", Value = material });
            return descriptions;
        }
        var methods = Build.Grid(new GridControl { Columns = 2, Gap = Space.Sm },
            new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.MessageSquareText, Text = "企业微信" },
            new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Smartphone, Text = "短信验证码" });
        var login = Build.Card(new CardControl(), Column(Space.Md,
            new TextFieldControl { PlaceholderText = "工号或邮箱", AccessibleName = "账号" },
            new TextFieldControl { PlaceholderText = "密码", AccessibleName = "密码", Type = TextFieldType.Password },
            new ButtonControl { Text = "登录" }, new SeparatorControl { Text = "或者" }, methods));
        var change = Build.Card(new CardControl(), Column(Space.Md,
            new SeparatorControl { LabelAlign = SeparatorLabelAlign.Start, Text = "更改前" }, Spec("G26", "铝合金 6061"),
            new SeparatorControl { LabelAlign = SeparatorLabelAlign.Start, Decorative = false, Text = "更改后" }, Spec("G29", "铝合金 7075")));
        return Build.Grid(new GridControl { MinItemWidth = "16rem", Gap = Space.Md }, login, change);
    }

    static Control Dashboard()
    {
        // 四列三行正好 12 格：跨两列的 3 个、跨两行的 1 个、单格的 4 个
        (string Title, string Note, int Span, int RowSpan)[] cells =
        [
            ("销售趋势", "跨两列", 2, 1), ("待办", "跨两行", 1, 2), ("新客户", "1 格", 1, 1), ("转化率", "1 格", 1, 1),
            ("退款", "1 格", 1, 1), ("客单价", "1 格", 1, 1), ("库存预警", "跨两列", 2, 1), ("物流异常", "跨两列", 2, 1),
        ];
        var grid = new GridControl { Columns = 4, Gap = Space.Sm, Flow = GridFlow.RowDense };
        foreach (var (title, note, span, rowSpan) in cells)
        {
            var card = Build.Card(new CardControl { CardPadding = Space.Sm, MinimumSize = new Size(0, 72) },
                Column(Space.Xs, new StrongControl { Text = title }, new SmallControl { Text = note, Tone = TextTone.Muted }));
            grid.Controls.Add(card);
            grid.SetSpan(card, span);
            grid.SetRowSpan(card, rowSpan);
        }
        return grid;
    }

    static Control Separators()
    {
        (IconData Icon, string Label, string Value)[] stats = [(Lucide.Eye, "阅读", "3.2 万"), (Lucide.ThumbsUp, "赞", "1,284"), (Lucide.MessageSquare, "评论", "96")];
        var interactions = new FlexListControl { Gap = Space.Lg, AccessibleName = "互动数据" };
        foreach (var (icon, label, value) in stats)
            interactions.Controls.Add(Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs }, new IconControl { Data = icon }, new SmallControl { Text = $"{label} {value}", Tone = TextTone.Muted }));
        var meta = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true },
            new AvatarControl { TextSize = TextSize.Xs, DisplayName = "周一航" }, new SmallControl { Text = "周一航" }, new SeparatorControl { Orientation = SeparatorOrientation.Vertical },
            new SmallControl { Text = "10 月 18 日", Tone = TextTone.Muted }, new SeparatorControl { Orientation = SeparatorOrientation.Vertical }, new SmallControl { Text = "阅读约 8 分钟", Tone = TextTone.Muted });
        return Build.Card(new CardControl(), Column(Space.Md,
            new H3Control { Text = "用 CSS 变量写一套主题系统", TextSize = TextSize.Lg }, meta, new SeparatorControl(),
            new ParagraphControl { Text = "defineVars 声明变量，createTheme 覆盖一组取值，套在任意子树上就能局部换肤；组件只认 token，不写死颜色。", TextSize = TextSize.Sm },
            new SeparatorControl { Decorative = false }, interactions));
    }

    static Control Containers()
    {
        var form = Build.Card(new CardControl { CardPadding = Space.Xl }, Column(Space.Md,
            new H3Control { Text = "登录", TextSize = TextSize.Lg },
            new FieldControl { Label = "邮箱", Content = new TextFieldControl { PlaceholderText = "you@example.com" } },
            new FieldControl { Label = "密码", Content = new TextFieldControl { Type = TextFieldType.Password } },
            new ButtonControl { Text = "登录" },
            new SmallControl { Text = "ContainerSize = Xs：最宽 30rem，居中，左右留 padding", Tone = TextTone.Muted }));
        // 页面里通常 Dock=Fill；放在示例里时跟着表单的高度
        var container = new ContainerPanel { ContainerSize = ContainerSize.Xs, Dock = DockStyle.Top, AutoSize = true };
        form.Dock = DockStyle.Top;
        container.Controls.Add(form);
        var page = new Panel { Padding = new Padding(0, 32, 0, 32), BackColor = ProviderControl.ThemeOf(container).Colors.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        page.Controls.Add(container);
        return page;
    }

    static Control Grow()
    {
        var titles = Column(Space.None,
            new StrongControl { Text = "林小满上传了《第三季度经营分析报告（终版，含附录与数据来源说明）》", Truncate = true },
            new SmallControl { Text = "设计中心 · 10 分钟前 · 来自网页端", Tone = TextTone.Muted, Truncate = true });
        var upload = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, Dock = DockStyle.Top },
            new AvatarControl { DisplayName = "林小满", TextSize = TextSize.Sm }, titles, new BadgeControl { Tone = StatusTone.Success, Text = "已审核" });
        upload.SetGrow(titles, true);

        var status = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs },
            new TextControl { Text = "状态：" }, new IconControl { Data = Lucide.CircleCheck, Tone = TextTone.Success }, new TextControl { Text = "已通过，下一步由财务确认。" });

        (string Name, string Size)[] attachments =
        [
            ("采购合同（盖章版）.pdf", "2.4 MB"), ("报价单.xlsx", "86 KB"), ("发票.pdf", "312 KB"), ("验收单.pdf", "1.1 MB"), ("对账单.xlsx", "64 KB"), ("回款记录.csv", "12 KB"),
        ];
        var files = new GridListControl { MinItemWidth = "12rem", RowGap = Space.Xs, ColumnGap = Space.Lg, AccessibleName = "附件" };
        foreach (var (name, size) in attachments)
        {
            var label = new SmallControl { Text = name, Truncate = true };
            var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs }, new IconControl { Data = Lucide.FileText, Tone = TextTone.Muted }, label, new SmallControl { Text = size, Tone = TextTone.Muted });
            row.SetGrow(label, true);
            files.Controls.Add(row);
        }
        return Column(Space.Lg, Build.Card(new CardControl { CardPadding = Space.Sm }, upload), status, files);
    }
}
