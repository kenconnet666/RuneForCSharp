namespace Rune.WinForms.Examples;

/// <summary>Skeleton 页的示例（vue-tsx-pro 文档站 feedback/Skeleton）。表格的骨架行在 DataTable 里，DataTable 还没做，table 示例先不做。</summary>
static class SkeletonExamples
{
    public static Example[] All =>
    [
        new("Skeleton", "shapes", "形状", "Shape 给出常用形状：文字行、标题、头像、按钮、图片；多行段落用 SkeletonTextControl，最后一行短一些，各行的光带依次错开。其他大小关掉 AutoSize 给 Size。", Shapes),
        new("Skeleton", "wrapping", "包着内容", "被占位的控件加成子控件：内容照常决定大小但隐藏，加载完换成内容时布局不跳。Delay 让几块骨架依次扫过，连成一道波；间隔用 SkeletonTextControl.Stagger 的倍数，与列表、表格的骨架行节奏一致。", Wrapping),
        new("Skeleton", "layouts", "常见布局", "按真实布局拼骨架：文章（配图、标题、段落）、消息列表（头像加两行文字）、看板、表单。CardControl、StatControl 自带 IsLoading；自己拼的骨架在加载时换掉整块内容。", Layouts),
    ];

    const int Stagger = SkeletonTextControl.Stagger;

    static FlexControl Column(Space gap, params Control[] children) =>
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = gap, Align = FlexAlign.Stretch }, children);

    static Control Shapes()
    {
        (SkeletonShape Shape, string Label)[] shapes =
        [
            (SkeletonShape.Text, "文字行"), (SkeletonShape.Heading, "标题"), (SkeletonShape.Circle, "头像"), (SkeletonShape.Button, "按钮"), (SkeletonShape.Image, "图片"),
        ];
        var grid = new GridControl { MinItemWidth = "10em", Gap = Space.Lg };
        foreach (var (shape, label) in shapes) grid.Controls.Add(Cell(new SkeletonControl { Shape = shape }, $"{label} Shape=\"{shape}\""));
        grid.Controls.Add(Cell(new SkeletonTextControl { Lines = 3 }, "段落 SkeletonTextControl"));
        return grid;

        // 头像、按钮、标题是自己的大小，靠起始边；文字行、图片、段落占满格子
        static Control Cell(Control shape, string label)
        {
            var fixedSize = shape is SkeletonControl { Shape.Value: "circle" or "button" or "heading" };
            var cell = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = fixedSize ? FlexAlign.Start : FlexAlign.Stretch, Justify = FlexJustify.End },
                shape, new SmallControl { Text = label, Tone = TextTone.Muted });
            return cell;
        }
    }

    static Control Wrapping()
    {
        var toggle = new SwitchControl { Text = "加载中", IsChecked = true };
        var skeletons = new List<SkeletonControl>();
        SkeletonControl Wrap(Control content, SkeletonShape shape = default, int delay = 0)
        {
            var skeleton = new SkeletonControl { Shape = shape, Delay = delay };
            skeleton.Controls.Add(content);
            skeletons.Add(skeleton);
            return skeleton;
        }
        toggle.IsCheckedChanged += (_, _) =>
        {
            foreach (var skeleton in skeletons) skeleton.IsLoading = toggle.IsChecked;
        };
        var person = Build.Flex(new FlexControl { Gap = Space.Md, Align = FlexAlign.Center },
            Wrap(new AvatarControl { DisplayName = "Ada Lovelace" }, SkeletonShape.Circle),
            Column(Space.Xs,
                Wrap(new TextControl { Text = "Ada Lovelace", Weight = TextWeight.Semibold }),
                Wrap(new TextControl { Text = "分析机程序员 · 伦敦", TextSize = TextSize.Sm, Tone = TextTone.Muted }, delay: Stagger)));
        var card = Build.Card(new CardControl { MaximumSize = new Size(384, 0) },
            Column(Space.Md, person, Wrap(new ParagraphControl { Text = "为巴贝奇的分析机写下了计算伯努利数的算法，被认为是世界上第一段程序。" }, delay: 2 * Stagger)));
        return Column(Space.Md, toggle, card);
    }

    static readonly (string Name, string Text)[] Messages =
    [
        ("林小满", "周五的评审材料我放到共享盘了，你看下第三页的数据。"),
        ("陈一舟", "收到，下午三点前给你反馈。"),
        ("王若曦", "接口文档更新了分页参数，前端记得同步。"),
    ];

    static Control Layouts()
    {
        var toggle = new SwitchControl { Text = "加载中", IsChecked = true };
        var swaps = new List<Action<bool>>();
        toggle.IsCheckedChanged += (_, _) =>
        {
            foreach (var swap in swaps) swap(toggle.IsChecked);
        };

        // 自己拼的骨架：加载时显示骨架、内容出现时换成内容
        CardControl Swap(Control skeleton, Control content)
        {
            var card = Build.Card(new CardControl(), skeleton, content);
            skeleton.Dock = content.Dock = DockStyle.Top;
            void Apply(bool on)
            {
                skeleton.Visible = on;
                content.Visible = !on;
            }
            Apply(true);
            swaps.Add(Apply);
            return card;
        }

        var article = Swap(
            Column(Space.Md, new SkeletonControl { Shape = SkeletonShape.Image }, new SkeletonControl { Shape = SkeletonShape.Heading, Delay = Stagger }, new SkeletonTextControl { Lines = 3, Delay = 2 * Stagger }),
            Column(Space.Md,
                new Panel { Height = 140, BackColor = Color.FromArgb(0x63, 0x66, 0xf1) },
                new H4Control { Text = "设计系统的第一年" },
                new ParagraphControl { Text = "从十几个按钮样式收敛到一套 tokens，我们踩过的坑和留下的约定：命名、档位、主题与文档。", Tone = TextTone.Muted }));

        var messageSkeletons = Column(Space.Lg);
        var messageList = Column(Space.Lg);
        for (var i = 0; i < Messages.Length; i++)
        {
            var lines = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch },
                new SkeletonControl { Shape = SkeletonShape.Text, Delay = i * Stagger, AutoSize = false, Size = new Size(64, 14) },
                new SkeletonControl { Shape = SkeletonShape.Text, Delay = i * Stagger + Stagger / 2 });
            var row = Build.Flex(new FlexControl { Gap = Space.Md, Align = FlexAlign.Center }, new SkeletonControl { Shape = SkeletonShape.Circle, Delay = i * Stagger }, lines);
            row.SetGrow(lines, true);
            messageSkeletons.Controls.Add(row);
            var text = Column(Space.None, new TextControl { Text = Messages[i].Name, Weight = TextWeight.Semibold }, new SmallControl { Text = Messages[i].Text, Tone = TextTone.Muted, Truncate = true });
            var message = Build.Flex(new FlexControl { Gap = Space.Md, Align = FlexAlign.Center }, new AvatarControl { DisplayName = Messages[i].Name }, text);
            message.SetGrow(text, true);
            messageList.Controls.Add(message);
        }
        var messages = Swap(messageSkeletons, messageList);

        // 看板：StatControl、CardControl 自带 IsLoading（骨架、读屏的忙状态都由组件管）
        var stat = new StatControl { Label = "本月营收", Value = 128.6m, Unit = "万元", Change = 0.084m, ChangeLabel = "较上月", IsLoading = true };
        var summary = Build.Card(new CardControl { Header = "本周摘要", IsLoading = true }, new ParagraphControl { Text = "新签客户 12 家，续约率 94%，两笔大单进入法务审核。", Tone = TextTone.Muted, Dock = DockStyle.Top });
        swaps.Add(on =>
        {
            stat.IsLoading = on;
            summary.IsLoading = on;
        });
        var board = Column(Space.Md, Build.Card(new CardControl(), stat), summary);

        var fieldSkeletons = Column(Space.Lg);
        var fields = Column(Space.Lg);
        string[] labels = ["姓名", "邮箱", "部门"];
        for (var i = 0; i < labels.Length; i++)
        {
            fieldSkeletons.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Start },
                new SkeletonControl { AutoSize = false, Size = new Size(56, 12), Delay = i * Stagger },
                new SkeletonControl { AutoSize = false, Size = new Size(240, 32), Delay = i * Stagger + Stagger / 2 }));
            fields.Controls.Add(Column(Space.Sm, new SmallControl { Text = labels[i] }, new TextFieldControl { AccessibleName = labels[i] }));
        }
        fieldSkeletons.Controls.Add(new SkeletonControl { Shape = SkeletonShape.Button, Delay = 3 * Stagger });
        fields.Controls.Add(new ButtonControl { Text = "保存" });
        fieldSkeletons.Align = fields.Align = FlexAlign.Start;
        var form = Swap(fieldSkeletons, fields);

        var grid = Build.Grid(new GridControl { MinItemWidth = "16em", Gap = Space.Md, Align = GridAlign.Start }, article, messages, board, form);
        return Column(Space.Md, toggle, grid);
    }
}
