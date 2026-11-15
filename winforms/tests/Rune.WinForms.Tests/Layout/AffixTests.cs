namespace Rune.WinForms.Tests;

/// <summary>AffixControl：滚过 OffsetTop 时内容挪到滚动区域上面、停在顶部（原位留空位），滚回来回到原位；不超出父控件；OffsetBottom 时贴在底部。</summary>
public class AffixTests
{
    static Panel Block(int height) => new() { Height = height, Width = 100 };

    static (Form Form, ScrollAreaControl Scroller) Open(params Control[] items)
    {
        var column = new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch };
        column.Controls.AddRange(items);
        var scroller = new ScrollAreaControl { Dock = DockStyle.Fill, Content = column };
        var form = new Form { Size = new Size(400, 340), FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(100, 100) };
        form.ClientSize = new Size(400, 300);
        form.Controls.Add(scroller);
        form.Show();
        WinFormsUi.Pump();
        return (form, scroller);
    }

    static int TopIn(Control control, Control ancestor) => ancestor.PointToClient(control.PointToScreen(Point.Empty)).Y;

    [Fact]
    [Upstream("Affix", "哨兵滚到 offsetTop 之上就是贴住：data-stuck、阴影、onChange；滚回来取消")]
    [Upstream("Affix", "贴顶：滚下去后贴在面板顶部")]
    public Task Scrolling_past_the_offset_pins_it_to_the_top() => WinFormsUi.Run(() =>
    {
        var bar = Block(40);
        var affix = new AffixControl { OffsetTop = 8, Content = bar };
        var changes = new List<bool>();
        affix.StuckChanged += (_, _) => changes.Add(affix.IsStuck);
        var (form, scroller) = Open(Block(100), affix, Block(2000));
        using var _ = form;

        scroller.ScrollOffset = new Point(0, 50);
        WinFormsUi.Pump();
        Assert.False(affix.IsStuck);
        Assert.Same(affix, bar.Parent);

        scroller.ScrollOffset = new Point(0, 200);
        WinFormsUi.Pump();
        Assert.True(affix.IsStuck);
        Assert.Same(scroller, bar.Parent);
        Assert.Equal(affix.LogicalToDeviceUnits(8), TopIn(bar, scroller));
        Assert.Equal(bar.Height, affix.Height);

        scroller.ScrollOffset = new Point(0, 0);
        WinFormsUi.Pump();
        Assert.False(affix.IsStuck);
        Assert.Same(affix, bar.Parent);
        Assert.Equal([true, false], changes);
    });

    [Fact]
    public Task It_stays_inside_its_parent() => WinFormsUi.Run(() =>
    {
        var bar = Block(40);
        var affix = new AffixControl { Content = bar };
        var section = new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch };
        section.Controls.AddRange([affix, Block(200)]);
        var (form, scroller) = Open(section, Block(2000));
        using var _ = form;

        // 父控件（240 高）滚出去 220：最多停在父控件的底边（离视口顶 240 - 220 - 40 = -20）
        scroller.ScrollOffset = new Point(0, 220);
        WinFormsUi.Pump();
        Assert.True(affix.IsStuck);
        Assert.Equal(-20, TopIn(bar, scroller));
    });

    [Fact]
    [Upstream("Affix", "贴底：压着内容时停在面板底边并标记贴住；滚到末尾回到原位")]
    public Task Offset_bottom_pins_it_to_the_bottom_until_its_place_scrolls_in() => WinFormsUi.Run(() =>
    {
        var bar = Block(40);
        var affix = new AffixControl { OffsetBottom = 0, Content = bar };
        var (form, scroller) = Open(Block(2000), affix);
        using var _ = form;
        scroller.ScrollOffset = new Point(0, 1);
        scroller.ScrollOffset = new Point(0, 0);
        WinFormsUi.Pump();

        Assert.True(affix.IsStuck);
        Assert.Equal(scroller.ClientSize.Height - 40, TopIn(bar, scroller));
        scroller.ScrollOffset = new Point(0, 100000);
        WinFormsUi.Pump();
        Assert.False(affix.IsStuck);
    });
}
