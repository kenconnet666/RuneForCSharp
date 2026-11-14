using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf.Examples;

/// <summary>Flex 页的示例（vue-tsx-pro 文档站 layout/Flex）：Flex、Grid、GridItem、GridList、FlexList、Container、Separator。零件图片用图标代替（Image 还没做）。</summary>
static class FlexExamples
{
    public static Example[] All =>
    [
        new("Flex", "flex", "Flex", "不拆 Stack / Group：竖排写 Direction=Column。Gap 取主题的间距档位（none、xs–xl），整个应用的疏密跟着主题一起变。", Playground),
        new("Flex", "auto-grid", "自动换列", "MinItemWidth 给每项的最小宽度，容器多宽就放几列，不需要断点；项目少于放得下的列数时各项拉宽占满（auto-fit）。这里用 GridList：读屏念\"列表\"。商品、零件这类会越来越多的列表，最后一行不满是正常的；一排指标卡、一组入口这类固定几项的，加 Balanced 按项数取列数，最后一行最多空一格。", AutoGrid),
        new("Flex", "labelled-separator", "带文字的分隔线", "Separator 里写文字：默认居中，两边各一段线；LabelAlign=Start 文字在前、线在后，像一节的小标题。文字照常给读屏，两段线对读屏隐藏；Decorative=False 时整条是分隔符，以文字为名称。", LabelledSeparators),
        new("Flex", "dashboard", "固定列数与跨格", "Columns 固定等宽列；GridItem 的 Span、RowSpan 跨几列、几行，Span=Full 占满一整行（GridList 里用 GridListItem）。Flow=RowDense 回填前面留下的空位。", Dashboard),
        new("Flex", "separator", "分隔线与列表", "Separator 默认是装饰性的、对读屏隐藏；分隔两组不同内容、需要读屏感知时写 Decorative=False。竖向的在 Flex 里自动撑满行高。", Separators),
        new("Flex", "container", "Container", "限制最大宽度并水平居中：xs 30rem（登录）、sm 40rem（表单、设置）、md 48rem（文章）、lg 64rem（默认）、xl 80rem（仪表盘）。要别的语义时从 Container 派生。", Containers),
        new("Flex", "grow", "伸展、行内与行列间距", "Flex.Grow 让 Flex 在父级的 Flex 里占满剩余空间，并且可以收缩到比内容窄——头像旁边的长标题才能截断；Inline 是行内的 Flex，放在一段文字中间（InlineUIContainer）。Grid、GridList 的 RowGap、ColumnGap 分别覆盖 Gap：附件列表行与行挨得近、列与列隔得开。", Grow),
    ];

    static ToggleGroup Choices(string name, string value, params string[] values)
    {
        var group = new ToggleGroup { IsRequired = true, Size = TextSize.Sm, Value = value };
        foreach (var item in values) group.Children.Add(new ToggleItem { Value = item, Content = item });
        AutomationProperties.SetName(group, name);
        return group;
    }

    static FrameworkElement Playground()
    {
        double[] heights = [2, 3.5, 2.5, 4, 3];
        var direction = Choices("direction", "row", "row", "column");
        var gap = Choices("gap", "sm", "none", "xs", "sm", "md", "lg", "xl");
        var align = Choices("align", "center", "start", "center", "end", "stretch", "baseline");
        var justify = Choices("justify", "start", "start", "center", "end", "between", "evenly");
        var wrap = new Switch();
        AutomationProperties.SetName(wrap, "wrap");

        var stage = new Flex();
        for (var i = 0; i < heights.Length; i++)
        {
            // 高矮不一，才看得出 align 的差别
            var box = new Border
            {
                MinWidth = 48, Height = heights[i] * 16, Padding = new Thickness(heights[i] * 4, 0, heights[i] * 4, 0), CornerRadius = new CornerRadius(4),
                Child = new Text { Text = $"{i + 1}", Tone = TextTone.Accent, Weight = TextWeight.Semibold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center },
            };
            box.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.AccentSoft);
            stage.Children.Add(box);
        }
        var frame = new Border { MinHeight = 176, Padding = new Thickness(16), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = stage };
        frame.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.Brush.Border);
        var code = new CodeText();

        void Apply()
        {
            stage.Direction = FlexDirection.Parse(direction.Value!);
            stage.Gap = Space.Parse(gap.Value!);
            stage.Align = FlexAlign.Parse(align.Value!);
            stage.Justify = FlexJustify.Parse(justify.Value!);
            stage.Wrap = wrap.IsChecked == true;
            code.Source = $"`<Flex Direction=\"{direction.Value}\" Align=\"{align.Value}\" Justify=\"{justify.Value}\" Gap=\"{gap.Value}\"{(wrap.IsChecked == true ? " Wrap=\"True\"" : "")}>`";
        }
        foreach (var group in new[] { direction, gap, align, justify }) DependencyPropertyDescriptor.FromProperty(ToggleGroup.ValueProperty, typeof(ToggleGroup)).AddValueChanged(group, (_, _) => Apply());
        DependencyPropertyDescriptor.FromProperty(ChoiceBase.IsCheckedProperty, typeof(Switch)).AddValueChanged(wrap, (_, _) => Apply());
        Apply();

        static FrameworkElement Control(string label, FrameworkElement group) =>
            new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Text { Text = label, Size = TextSize.Sm, Tone = TextTone.Muted, MinWidth = 50 }, group } };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children =
            {
                new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { Control("direction", direction), Control("gap", gap), Control("align", align), Control("justify", justify), Control("wrap", wrap) } },
                frame, code,
            },
        };
    }

    static FrameworkElement AutoGrid()
    {
        (string Title, IconData Icon, int Price)[] parts =
        [
            ("法兰轴承座", Lucide.Cog, 86), ("直线导轨滑块", Lucide.Ruler, 128), ("同步带轮", Lucide.Disc, 245),
            ("内六角螺栓", Lucide.Wrench, 12), ("弹性联轴器", Lucide.Link, 39), ("气缸接头", Lucide.Plug, 64),
        ];
        var list = new GridList { MinItemWidth = "9rem", Gap = Space.Md };
        AutomationProperties.SetName(list, "常用零件");
        foreach (var (title, icon, price) in parts)
        {
            // 零件图：fill 底上一个图标（文档站是图纸插画）
            var cover = new Border { Height = 88, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 0, 8), Child = new Icon { Data = icon, FontSize = 32, Tone = TextTone.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            cover.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.Fill);
            var priceText = new Strong();
            priceText.Inlines.Add(new Run($"¥{price} "));
            priceText.Inlines.Add(new InlineUIContainer(new Small { Text = "/ 件", Tone = TextTone.Muted }));
            list.Children.Add(new GridListItem
            {
                Child = new Card
                {
                    Padding = Space.Sm,
                    Content = new StackPanel { Children = { cover, new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new Text { Text = title, Size = TextSize.Sm }, priceText } } } },
                },
            });
        }
        return list;
    }

    static FrameworkElement LabelledSeparators()
    {
        TextField Field(string placeholder, string name, bool password = false)
        {
            var field = new TextField { PlaceholderText = placeholder, Type = password ? TextFieldType.Password : TextFieldType.Text };
            AutomationProperties.SetName(field, name);
            return field;
        }
        static Descriptions Spec(string code, string material)
        {
            var descriptions = new Descriptions { Columns = 1, Size = TextSize.Sm };
            descriptions.Items.Add(new DescriptionItem { Label = "件号", Value = code });
            descriptions.Items.Add(new DescriptionItem { Label = "材料", Value = material });
            return descriptions;
        }
        var login = new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children =
                {
                    Field("工号或邮箱", "账号"), Field("密码", "密码", password: true), new Button { Content = "登录" },
                    new Separator { Content = "或者" },
                    new Grid
                    {
                        Columns = 2, Gap = Space.Sm,
                        Children =
                        {
                            new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.MessageSquareText, Content = "企业微信" },
                            new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Smartphone, Content = "短信验证码" },
                        },
                    },
                },
            },
        };
        var change = new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children =
                {
                    new Separator { LabelAlign = SeparatorLabelAlign.Start, Content = "更改前" }, Spec("G26", "铝合金 6061"),
                    new Separator { LabelAlign = SeparatorLabelAlign.Start, Decorative = false, Content = "更改后" }, Spec("G29", "铝合金 7075"),
                },
            },
        };
        return new Grid { MinItemWidth = "16rem", Gap = Space.Md, Children = { login, change } };
    }

    static FrameworkElement Dashboard()
    {
        // 四列三行正好 12 格：跨两列的 3 个、跨两行的 1 个、单格的 4 个
        (string Title, string Note, int Span, int RowSpan)[] cells =
        [
            ("销售趋势", "跨两列", 2, 1), ("待办", "跨两行", 1, 2), ("新客户", "1 格", 1, 1), ("转化率", "1 格", 1, 1),
            ("退款", "1 格", 1, 1), ("客单价", "1 格", 1, 1), ("库存预警", "跨两列", 2, 1), ("物流异常", "跨两列", 2, 1),
        ];
        var grid = new Grid { Columns = 4, Gap = Space.Sm, Flow = GridFlow.RowDense };
        foreach (var (title, note, span, rowSpan) in cells)
            grid.Children.Add(new GridItem
            {
                Span = span, RowSpan = rowSpan,
                // 卡片撑满格子：跨两行的格子里卡片也一样高
                Child = new Card { Padding = Space.Sm, MinHeight = 72, Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new Strong { Text = title }, new Small { Text = note, Tone = TextTone.Muted } } } },
            });
        return grid;
    }

    static FrameworkElement Separators()
    {
        (IconData Icon, string Label, string Value)[] stats = [(Lucide.Eye, "阅读", "3.2 万"), (Lucide.ThumbsUp, "赞", "1,284"), (Lucide.MessageSquare, "评论", "96")];
        var interactions = new FlexList { Gap = Space.Lg };
        AutomationProperties.SetName(interactions, "互动数据");
        foreach (var (icon, label, value) in stats)
            interactions.Children.Add(new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Icon { Data = icon }, new Small { Text = $"{label} {value}", Tone = TextTone.Muted } } });
        return new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children =
                {
                    new H3 { Text = "用 CSS 变量写一套主题系统", Size = TextSize.Lg },
                    new Flex
                    {
                        Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true,
                        Children =
                        {
                            new Avatar { Size = TextSize.Xs, DisplayName = "周一航" }, new Small { Text = "周一航" }, new Separator { Orientation = SeparatorOrientation.Vertical },
                            new Small { Text = "10 月 18 日", Tone = TextTone.Muted }, new Separator { Orientation = SeparatorOrientation.Vertical }, new Small { Text = "阅读约 8 分钟", Tone = TextTone.Muted },
                        },
                    },
                    new Separator(),
                    new Paragraph { Text = "defineVars 声明变量，createTheme 覆盖一组取值，套在任意子树上就能局部换肤；组件只认 token，不写死颜色。", Size = TextSize.Sm },
                    new Separator { Decorative = false },
                    interactions,
                },
            },
        };
    }

    static FrameworkElement Containers()
    {
        TextField Input(string placeholder = "", bool password = false) => new() { PlaceholderText = placeholder, Type = password ? TextFieldType.Password : TextFieldType.Text };
        var form = new Card
        {
            Padding = Space.Xl,
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children =
                {
                    new H3 { Text = "登录", Size = TextSize.Lg },
                    new Field { Label = "邮箱", Content = Input("you@example.com") },
                    new Field { Label = "密码", Content = Input(password: true) },
                    new Button { Content = "登录" },
                    new Small { Text = "Container Size=\"xs\"：最宽 30rem，居中，左右留 padding", Tone = TextTone.Muted },
                },
            },
        };
        var page = new Border { Padding = new Thickness(0, 32, 0, 32), CornerRadius = new CornerRadius(8), Child = new Container { Size = ContainerSize.Xs, Child = form } };
        page.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.Fill);
        return page;
    }

    static FrameworkElement Grow()
    {
        var titles = new Flex
        {
            Direction = FlexDirection.Column,
            Children =
            {
                new Strong { Text = "林小满上传了《第三季度经营分析报告（终版，含附录与数据来源说明）》", Truncate = true },
                new Small { Text = "设计中心 · 10 分钟前 · 来自网页端", Tone = TextTone.Muted, Truncate = true },
            },
        };
        Flex.SetGrow(titles, true);
        var upload = new Card
        {
            Padding = Space.Sm,
            Content = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Avatar { DisplayName = "林小满", Size = TextSize.Sm }, titles, new Badge { Tone = StatusTone.Success, Content = "已审核" } } },
        };

        var status = new Text();
        status.Inlines.Add(new Run("状态："));
        status.Inlines.Add(new InlineUIContainer(new Flex { Inline = true, Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Icon { Data = Lucide.CircleCheck, Tone = TextTone.Success }, new Text { Text = "已通过" } } }) { BaselineAlignment = BaselineAlignment.Center });
        status.Inlines.Add(new Run("，下一步由财务确认。"));

        (string Name, string Size)[] attachments =
        [
            ("采购合同（盖章版）.pdf", "2.4 MB"), ("报价单.xlsx", "86 KB"), ("发票.pdf", "312 KB"), ("验收单.pdf", "1.1 MB"), ("对账单.xlsx", "64 KB"), ("回款记录.csv", "12 KB"),
        ];
        var files = new GridList { MinItemWidth = "12rem", RowGap = Space.Xs, ColumnGap = Space.Lg };
        AutomationProperties.SetName(files, "附件");
        foreach (var (name, size) in attachments)
        {
            var label = new Small { Text = name, Truncate = true };
            Flex.SetGrow(label, true);
            files.Children.Add(new GridListItem { Child = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Icon { Data = Lucide.FileText, Tone = TextTone.Muted }, label, new Small { Text = size, Tone = TextTone.Muted } } } });
        }
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Children = { upload, status, files } };
    }
}
