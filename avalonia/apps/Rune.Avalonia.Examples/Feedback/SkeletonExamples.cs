using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>Skeleton 页的示例（vue-tsx-pro 文档站 feedback/Skeleton）。表格的骨架行在 DataTable 里，DataTable 还没做，table 示例先不做。</summary>
static class SkeletonExamples
{
    public static Example[] All =>
    [
        new("Skeleton", "shapes", "形状", "Shape 给出常用形状：文字行、标题、头像、按钮、图片；多行段落用 SkeletonText，最后一行短一些，各行的光带依次错开。其他大小用 Width、Height 给。", Shapes),
        new("Skeleton", "wrapping", "包着内容", "包着真实内容时，内容照常排版但看不见，占位正好是内容的大小，加载完换成内容时布局不跳。Delay 让几块骨架依次扫过，连成一道波；间隔用 SkeletonText.Stagger 的倍数，与列表、表格的骨架行节奏一致。", Wrapping),
        new("Skeleton", "layouts", "常见布局", "按真实布局拼骨架：文章（配图、标题、段落）、消息列表（头像加两行文字）、看板、表单。Card、Stat 自带 IsLoading；自己拼的骨架在整块区域上设读屏的忙状态（AutomationProperties.ItemStatus）。", Layouts),
    ];

    const int Stagger = SkeletonText.Stagger;

    static Control Shapes()
    {
        var grid = new Grid { MinItemWidth = "10em", Gap = Space.Lg };
        (SkeletonShape Shape, string Label)[] shapes =
        [
            (SkeletonShape.Text, "文字行"), (SkeletonShape.Heading, "标题"), (SkeletonShape.Circle, "头像"), (SkeletonShape.Button, "按钮"), (SkeletonShape.Image, "图片"),
        ];
        foreach (var (shape, label) in shapes) grid.Children.Add(Cell(new Skeleton { Shape = shape }, $"{label} Shape=\"{shape}\""));
        grid.Children.Add(Cell(new SkeletonText { Lines = 3 }, "段落 SkeletonText"));
        return grid;

        // 同一行的格子一样高：形状在剩下的空间里居中，说明文字都落在格子底部
        static Control Cell(Control shape, string label)
        {
            var cell = new DockPanel();
            var caption = new Small { Text = label, Tone = TextTone.Muted, Margin = new(0, 8, 0, 0) };
            DockPanel.SetDock(caption, Dock.Bottom);
            shape.VerticalAlignment = VerticalAlignment.Center;
            // 头像、按钮是固定大小，不拉满格子
            shape.HorizontalAlignment = shape is Skeleton { Shape.Value: "circle" or "button" or "heading" } ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
            cell.Children.Add(caption);
            cell.Children.Add(shape);
            return cell;
        }
    }

    static Control Wrapping()
    {
        var toggle = new Switch { Content = "加载中", IsChecked = true };
        Skeleton Wrap(Control content, SkeletonShape shape = default, int delay = 0)
        {
            var skeleton = new Skeleton { Content = content, Shape = shape, Delay = delay };
            skeleton.Bind(Skeleton.IsLoadingProperty, toggle.GetObservable(Switch.IsCheckedProperty));
            return skeleton;
        }
        var card = new Card
        {
            MaxWidth = 384, HorizontalAlignment = HorizontalAlignment.Left,
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children =
                {
                    new Flex
                    {
                        Gap = Space.Md, Align = FlexAlign.Center,
                        Children =
                        {
                            Wrap(new Avatar { DisplayName = "Ada Lovelace" }, SkeletonShape.Circle),
                            new Flex
                            {
                                Direction = FlexDirection.Column, Gap = Space.Xs,
                                Children =
                                {
                                    Wrap(new Text { Text = "Ada Lovelace", Weight = TextWeight.Semibold }),
                                    Wrap(new Text { Text = "分析机程序员 · 伦敦", Size = TextSize.Sm, Tone = TextTone.Muted }, delay: Stagger),
                                },
                            },
                        },
                    },
                    Wrap(new Paragraph { Text = "为巴贝奇的分析机写下了计算伯努利数的算法，被认为是世界上第一段程序。" }, delay: 2 * Stagger),
                },
            },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { toggle, card } };
    }

    static readonly (string Name, string Text)[] Messages =
    [
        ("林小满", "周五的评审材料我放到共享盘了，你看下第三页的数据。"),
        ("陈一舟", "收到，下午三点前给你反馈。"),
        ("王若曦", "接口文档更新了分页参数，前端记得同步。"),
    ];

    static Control Layouts()
    {
        var toggle = new Switch { Content = "加载中", IsChecked = true };
        var loading = toggle.GetObservable(Switch.IsCheckedProperty);
        var swaps = new List<Action<bool>>();
        toggle.PropertyChanged += (_, e) =>
        {
            if (e.Property != Switch.IsCheckedProperty) return;
            foreach (var swap in swaps) swap(toggle.IsChecked == true);
        };

        // 自己拼的骨架：加载时显示骨架、内容出现时换成内容
        Control Swap(Control skeleton, Control content)
        {
            var panel = new Panel { Children = { skeleton, content } };
            void Apply(bool on)
            {
                skeleton.IsVisible = on;
                content.IsVisible = !on;
                AutomationProperties.SetItemStatus(panel, on ? "busy" : null);
            }
            Apply(true);
            swaps.Add(Apply);
            return panel;
        }

        var article = new Card
        {
            Content = Swap(
                new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Md,
                    Children = { new Skeleton { Shape = SkeletonShape.Image }, new Skeleton { Shape = SkeletonShape.Heading, Delay = Stagger }, new SkeletonText { Lines = 3, Delay = 2 * Stagger } },
                },
                new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Md,
                    Children =
                    {
                        new Border { Height = 140, CornerRadius = new(8), Background = Brushes.Cover },
                        new H4 { Text = "设计系统的第一年" },
                        new Paragraph { Text = "从十几个按钮样式收敛到一套 tokens，我们踩过的坑和留下的约定：命名、档位、主题与文档。", Tone = TextTone.Muted },
                    },
                }),
        };

        var messageSkeletons = new Flex { Direction = FlexDirection.Column, Gap = Space.Lg };
        var messageList = new Flex { Direction = FlexDirection.Column, Gap = Space.Lg };
        for (var i = 0; i < Messages.Length; i++)
        {
            var lines = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Sm,
                Children = { new Skeleton { Shape = SkeletonShape.Text, Delay = i * Stagger, Width = 64, HorizontalAlignment = HorizontalAlignment.Left }, new Skeleton { Shape = SkeletonShape.Text, Delay = i * Stagger + Stagger / 2 } },
            };
            var row = new Flex { Gap = Space.Md, Align = FlexAlign.Center, Children = { new Skeleton { Shape = SkeletonShape.Circle, Delay = i * Stagger }, lines } };
            Flex.SetGrow(lines, true);
            messageSkeletons.Children.Add(row);
            var text = new Flex
            {
                Direction = FlexDirection.Column,
                Children = { new Text { Text = Messages[i].Name, Weight = TextWeight.Semibold }, new Small { Text = Messages[i].Text, Tone = TextTone.Muted, Truncate = true } },
            };
            var message = new Flex { Gap = Space.Md, Align = FlexAlign.Center, Children = { new Avatar { DisplayName = Messages[i].Name }, text } };
            Flex.SetGrow(text, true);
            messageList.Children.Add(message);
        }
        var messages = new Card { Content = Swap(messageSkeletons, messageList) };

        // 看板：Stat、Card 自带 IsLoading（骨架、读屏的忙状态都由组件管）
        var stat = new Stat { Label = "本月营收", Value = 128.6m, Unit = "万元", Change = 0.084m, ChangeLabel = "较上月" };
        stat.Bind(Stat.IsLoadingProperty, loading);
        var summary = new Card { Header = new Text { Text = "本周摘要", Weight = TextWeight.Semibold }, Content = new Paragraph { Text = "新签客户 12 家，续约率 94%，两笔大单进入法务审核。", Tone = TextTone.Muted } };
        summary.Bind(Card.IsLoadingProperty, loading);
        var board = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { new Card { Content = stat }, summary } };

        var fieldSkeletons = new Flex { Direction = FlexDirection.Column, Gap = Space.Lg };
        var fields = new Flex { Direction = FlexDirection.Column, Gap = Space.Lg };
        string[] labels = ["姓名", "邮箱", "部门"];
        for (var i = 0; i < labels.Length; i++)
        {
            fieldSkeletons.Children.Add(new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Sm,
                Children = { new Skeleton { Width = 56, Height = 12, Delay = i * Stagger, HorizontalAlignment = HorizontalAlignment.Left }, new Skeleton { Height = 32, Delay = i * Stagger + Stagger / 2 } },
            });
            var input = new TextField();
            AutomationProperties.SetName(input, labels[i]);
            fields.Children.Add(new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { new Small { Text = labels[i] }, input } });
        }
        fieldSkeletons.Children.Add(new Skeleton { Shape = SkeletonShape.Button, Delay = 3 * Stagger, HorizontalAlignment = HorizontalAlignment.Left });
        fields.Children.Add(new Button { Content = "保存", HorizontalAlignment = HorizontalAlignment.Left });
        var form = new Card { Content = Swap(fieldSkeletons, fields) };

        var grid = new Grid { MinItemWidth = "16em", Gap = Space.Md, Align = GridAlign.Start, Children = { article, messages, board, form } };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { toggle, grid } };
    }

    /// <summary>示例的配图：一张渐变（文档站用的是插画）。</summary>
    static class Brushes
    {
        public static readonly global::Avalonia.Media.IBrush Cover = new global::Avalonia.Media.LinearGradientBrush
        {
            StartPoint = new(0, 0, global::Avalonia.RelativeUnit.Relative),
            EndPoint = new(1, 1, global::Avalonia.RelativeUnit.Relative),
            GradientStops = { new(global::Avalonia.Media.Color.Parse("#0ea5e9"), 0), new(global::Avalonia.Media.Color.Parse("#6366f1"), 1) },
        };
    }
}
