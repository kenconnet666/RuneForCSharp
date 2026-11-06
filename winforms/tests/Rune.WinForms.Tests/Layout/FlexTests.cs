namespace Rune.WinForms.Tests;

/// <summary>FlexControl：CSS flexbox 的排法（间距、伸缩、分布、对齐、换行、反向）。各项是自然大小固定的自动大小控件，结果按像素对。</summary>
public class FlexTests
{
    /// <summary>自然大小固定的自动大小控件（同 CSS 里写了内容尺寸的项）；给了宽度时高度按"面积不变"换行。</summary>
    sealed class Box : Control
    {
        readonly Size natural;

        public Box(int width, int height, bool wraps = false)
        {
            natural = new Size(width, height);
            Wraps = wraps;
            AutoSize = true;
            Size = natural;
        }

        bool Wraps { get; }

        public override Size GetPreferredSize(Size proposedSize) =>
            Wraps && proposedSize.Width is > 1 and < int.MaxValue && proposedSize.Width < natural.Width
                ? new Size(proposedSize.Width, natural.Height * (int)Math.Ceiling(natural.Width / (double)proposedSize.Width))
                : natural;
    }

    static FlexControl Flex(FlexControl flex, params Control[] children)
    {
        flex.Controls.AddRange(children);
        return flex;
    }

    /// <summary>放进窗体，宽度由父容器给（同 CSS 的块级 flex 撑满）。</summary>
    static Form Show(FlexControl flex, int width)
    {
        flex.AutoSize = false;
        flex.Width = width;
        flex.Height = flex.GetPreferredSize(new Size(width, 0)).Height;
        var form = WinFormsUi.Show(flex);
        flex.PerformLayout();
        return form;
    }

    static float Em(Control control) => control.Font.SizeInPoints * control.DeviceDpi / 72f;

    [Upstream("Flex", "div，未声明的属性落到元素上")]
    [Fact]
    public Task Row_with_gap_in_em_stretches_auto_sized_items() => WinFormsUi.Run(() =>
    {
        var (a, b) = (new Box(40, 10), new Box(60, 30));
        var fixedSize = new Panel { Size = new Size(20, 10) };
        var flex = Flex(new FlexControl { Gap = Space.Sm }, a, b, fixedSize);
        using var form = Show(flex, 300);
        var gap = (int)Math.Round(0.5 * Em(flex));

        Assert.Equal(new Rectangle(0, 0, 40, 30), a.Bounds); // stretch：自动大小的项拉到行高
        Assert.Equal(new Rectangle(40 + gap, 0, 60, 30), b.Bounds);
        Assert.Equal(new Size(20, 10), fixedSize.Size); // 关掉 AutoSize 的不拉伸
    });

    [Upstream("Flex", "grow：在父级 flex 里占满剩余空间并允许收缩")]
    [Fact]
    public Task Grow_takes_the_rest_and_can_shrink_below_its_content() => WinFormsUi.Run(() =>
    {
        var (icon, text, end) = (new Box(20, 20), new Box(400, 20), new Box(30, 20));
        var flex = Flex(new FlexControl(), icon, text, end);
        flex.SetGrow(text, true);
        using var form = Show(flex, 200);

        Assert.Equal(150, text.Width); // 200 - 20 - 30，比内容窄
        Assert.Equal(170, end.Left);

        // FlexControl 自己的 Grow：在外层 FlexControl 里分剩下的空间
        var inner = Flex(new FlexControl { Grow = true }, new Box(10, 10));
        var last = new Box(30, 20);
        var outer = Flex(new FlexControl(), new Box(20, 20), inner, last);
        using var second = Show(outer, 200);
        Assert.Equal(170, last.Left);
        Assert.True(outer.GetGrow(inner));
    });

    [Fact]
    public Task Items_shrink_by_their_content_width() => WinFormsUi.Run(() =>
    {
        var (a, b) = (new Box(200, 20, wraps: true), new Box(100, 20, wraps: true));
        using var form = Show(Flex(new FlexControl { Align = FlexAlign.Start }, a, b), 240);

        Assert.Equal((160, 80), (a.Width, b.Width)); // 按内容宽度加权：各缩 20%
        Assert.Equal((40, 40), (a.Height, b.Height)); // 缩窄后换了行
    });

    [Fact]
    public Task Justify_and_align_place_items_on_both_axes() => WinFormsUi.Run(() =>
    {
        var (a, b, c) = (new Box(20, 10), new Box(20, 30), new Box(20, 20));
        using var form = Show(Flex(new FlexControl { Justify = FlexJustify.Between, Align = FlexAlign.Center }, a, b, c), 200);
        Assert.Equal(new Rectangle(0, 10, 20, 10), a.Bounds);
        Assert.Equal(new Rectangle(90, 0, 20, 30), b.Bounds);
        Assert.Equal(new Rectangle(180, 5, 20, 20), c.Bounds);

        var (d, e) = (new Box(20, 10), new Box(20, 30));
        using var second = Show(Flex(new FlexControl { Justify = FlexJustify.Evenly, Align = FlexAlign.End }, d, e), 100);
        Assert.Equal(new Rectangle(20, 20, 20, 10), d.Bounds);
        Assert.Equal(new Rectangle(60, 0, 20, 30), e.Bounds);
    });

    [Fact]
    public Task Wrap_breaks_lines_and_reverse_flips_them() => WinFormsUi.Run(() =>
    {
        var boxes = Enumerable.Range(0, 5).Select(_ => new Box(40, 20)).ToArray();
        var flex = Flex(new FlexControl { Wrap = true, Gap = Space.None, RowGap = Space.Sm }, boxes);
        using var form = Show(flex, 130);

        Assert.Equal([0, 40, 80, 0, 40], boxes.Select(box => box.Left));
        Assert.Equal(20 + (int)Math.Round(0.5 * Em(flex)), boxes[3].Top);

        var reversed = Enumerable.Range(0, 3).Select(_ => new Box(40, 20)).ToArray();
        using var second = Show(Flex(new FlexControl { Wrap = FlexWrap.Reverse }, reversed), 90);
        Assert.Equal((20, 0), (reversed[0].Top, reversed[2].Top)); // 新的一行在上面
    });

    [Fact]
    public Task Column_reverse_and_right_to_left() => WinFormsUi.Run(() =>
    {
        var (a, b) = (new Box(20, 10), new Box(30, 20));
        using var form = Show(Flex(new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Start }, a, b), 300);
        Assert.Equal(new Rectangle(0, 0, 20, 10), a.Bounds);
        Assert.Equal(new Rectangle(0, 10, 30, 20), b.Bounds);

        var (c, d) = (new Box(20, 10), new Box(30, 10));
        using var second = Show(Flex(new FlexControl { Direction = FlexDirection.RowReverse }, c, d), 100);
        Assert.Equal((80, 50), (c.Left, d.Left)); // 从结束边排起

        var (e, f) = (new Box(20, 10), new Box(30, 10));
        using var third = Show(Flex(new FlexControl { RightToLeft = RightToLeft.Yes }, e, f), 100);
        Assert.Equal((80, 50), (e.Left, f.Left)); // 从右到左：起始边在右
    });

    [Fact]
    public Task Baseline_aligns_the_first_line_of_text() => WinFormsUi.Run(() =>
    {
        var big = new TextControl { Text = "128", TextSize = 30 };
        var small = new TextControl { Text = "件", TextSize = TextSize.Xs };
        using var form = Show(Flex(new FlexControl { Align = FlexAlign.Baseline }, big, small), 300);

        Assert.Equal(big.Top + ((IBaseline)big).Baseline, small.Top + ((IBaseline)small).Baseline);
        Assert.NotEqual(big.Top, small.Top);
    });

    [Upstream("Flex", "FlexList 是 ul")]
    [Fact]
    public Task Flex_lists_are_lists() => WinFormsUi.Run(() =>
    {
        using var list = new FlexListControl();
        using var ordered = new FlexOrderedListControl();

        Assert.Equal((AccessibleRole.List, AccessibleRole.List), (list.AccessibilityObject.Role, ordered.AccessibilityObject.Role));
    });
}
