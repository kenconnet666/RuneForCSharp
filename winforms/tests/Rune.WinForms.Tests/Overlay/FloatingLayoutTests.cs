namespace Rune.WinForms.Tests;

/// <summary>
/// FloatingLayout：同上游 computePlacement 的单元测试（utils/dom/.test/floating.test.ts）——十二种位置、从右到左镜像、
/// 放不下时翻到对侧、两侧都放不下时选空间大的、沿对齐方向挪回屏幕。
/// </summary>
public class FloatingLayoutTests
{
    static readonly Rectangle Screen = new(0, 0, 1000, 800);
    static readonly Rectangle Center = new(450, 380, 100, 40);

    static (string Side, Rectangle Panel) Place(string placement = "bottom-start", Rectangle? anchor = null, int width = 200, int height = 120, bool rtl = false, Rectangle? area = null) =>
        FloatingLayout.Position(new FloatingPlacement(placement), rtl, anchor ?? Center, new Size(width, height), 6, area ?? Screen);

    [Theory]
    [InlineData("bottom-start", 450, 426)]
    [InlineData("bottom", 400, 426)]
    [InlineData("bottom-end", 350, 426)]
    [InlineData("top-start", 450, 254)]
    [InlineData("top", 400, 254)]
    [InlineData("top-end", 350, 254)]
    [InlineData("right-start", 556, 380)]
    [InlineData("right", 556, 340)]
    [InlineData("right-end", 556, 300)]
    [InlineData("left-start", 244, 380)]
    [InlineData("left", 244, 340)]
    [InlineData("left-end", 244, 300)]
    public void Twelve_placements_keep_the_gap_and_align(string placement, int x, int y) =>
        Assert.Equal(new Point(x, y), Place(placement).Panel.Location);

    [Fact]
    public void Right_to_left_mirrors_start_and_end_above_and_below()
    {
        Assert.Equal(350, Place("bottom-start", rtl: true).Panel.X);
        Assert.Equal(450, Place("bottom-end", rtl: true).Panel.X);
        Assert.Equal(400, Place("bottom", rtl: true).Panel.X);
        Assert.Equal(380, Place("right-start", rtl: true).Panel.Y);
    }

    [Fact]
    public void Flips_to_the_opposite_side_when_it_does_not_fit()
    {
        var (side, panel) = Place("bottom-end", Center with { Y = 740 });
        Assert.Equal("top", side);
        Assert.Equal(740 - 6 - 120, panel.Y);
        Assert.Equal("left", Place("right", Center with { X = 880 }).Side);
        Assert.Equal("top", Place("top").Side);
    }

    [Fact]
    public void Picks_the_roomier_side_when_neither_fits()
    {
        Assert.Equal("bottom", Place("top-start", Center with { Y = 200 }, height: 600).Side);
        Assert.Equal("top", Place("bottom-start", Center with { Y = 560 }, height: 600).Side);
    }

    [Fact]
    public void Slides_back_along_the_alignment_axis()
    {
        Assert.Equal(1000 - 8 - 200, Place(anchor: Center with { X = 900 }).Panel.X);
        Assert.Equal(8, Place("bottom-end", Center with { X = 0 }).Panel.X);
        Assert.Equal(8, Place(width: 1200).Panel.X);
        Assert.Equal(8, Place("right", Center with { Y = 0 }).Panel.Y);
    }

    [Fact]
    public void Uses_the_visible_area_when_it_does_not_start_at_zero()
    {
        // 可见区域 300–600：下方只剩 180，上方 80，都放不下高 200 的，下方空间大
        Assert.Equal("bottom", Place("top", height: 200, area: new Rectangle(0, 300, 1000, 300)).Side);
    }
}
