using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>RatingControl：单选组的读屏子节点是各个分数（名称"3 星"）；点选、再点清空、半星；方向键换分数；只读时是一张图，名称里总有分数。</summary>
public class RatingTests
{
    static void Press(Control control, Keys key) =>
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [new KeyEventArgs(key)]);

    static AccessibleObject Option(RatingControl rating, int index) => rating.AccessibilityObject.GetChild(index)!;

    static IEnumerable<AccessibleObject> Options(RatingControl rating) => Enumerable.Range(0, rating.AccessibilityObject.GetChildCount()).Select(index => Option(rating, index));

    [Fact]
    [Upstream("Rating", "radiogroup 以 Field 的标签为名称；每颗星一个 radio，名称是“n 星”")]
    public Task Field_names_the_group_and_each_star_is_a_radio() => WinFormsUi.Run(() =>
    {
        var rating = new RatingControl();
        using var form = WinFormsUi.Show(new FieldControl { Label = "交付质量", Content = rating });

        Assert.Equal("交付质量", rating.AccessibilityObject.Name);
        Assert.Equal(AccessibleRole.Grouping, rating.AccessibilityObject.Role);
        Assert.Equal(["1 星", "2 星", "3 星", "4 星", "5 星"], Options(rating).Select(option => option.Name));
        Assert.All(Options(rating), option => Assert.Equal(AccessibleRole.RadioButton, option.Role));
        Assert.True(rating.TabStop);
    });

    [Fact]
    [Upstream("Rating", "点选写回分数；再点同一个清空；clearable={false} 时不清空")]
    public Task Picking_writes_the_score_and_picking_again_clears() => WinFormsUi.Run(() =>
    {
        var rating = new RatingControl();
        var kept = new RatingControl { Value = 2, Clearable = false };
        using var form = WinFormsUi.Show(rating, kept);

        Option(rating, 3).DoDefaultAction();
        Assert.Equal(4, rating.Value);
        Assert.Equal([100, 100, 100, 100, 0], Enumerable.Range(1, 5).Select(rating.Filled));
        Assert.True(Option(rating, 3).State.HasFlag(AccessibleStates.Checked));
        // 点同一颗星：清空
        var star = Rectangle.Round(rating.ScoreBounds(4));
        typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(rating, [new MouseEventArgs(MouseButtons.Left, 1, star.X + star.Width / 2, star.Y + star.Height / 2, 0)]);
        Assert.Null(rating.Value);

        kept.Pick(2);
        Assert.Equal(2, kept.Value);
    });

    [Fact]
    [Upstream("Rating", "半星：每颗星两个 radio；3.5 星时第四颗是半颗")]
    public Task Half_stars_have_two_options_each() => WinFormsUi.Run(() =>
    {
        var rating = new RatingControl { Value = 3.5, AllowHalf = true };
        using var form = WinFormsUi.Show(rating);

        Assert.Equal(10, rating.AccessibilityObject.GetChildCount());
        Assert.Equal("3.5 星", Option(rating, 6).Name);
        Assert.True(Option(rating, 6).State.HasFlag(AccessibleStates.Checked));
        Assert.Equal([100, 100, 100, 50, 0], Enumerable.Range(1, 5).Select(rating.Filled));
        Assert.True(rating.ScoreBounds(3.5).X < rating.ScoreBounds(4).X);
    });

    [Fact]
    public Task Arrow_keys_move_and_wrap() => WinFormsUi.Run(() =>
    {
        var rating = new RatingControl { Value = 5 };
        using var form = WinFormsUi.Show(rating);

        Press(rating, Keys.Right);
        Assert.Equal(1, rating.Value);
        Press(rating, Keys.Left);
        Assert.Equal(5, rating.Value);
        Press(rating, Keys.Up);
        Assert.Equal(4, rating.Value);
        Press(rating, Keys.Space);
        Assert.Null(rating.Value);
    });

    [Fact]
    [Upstream("Rating", "只读：一个 role=")]
    public Task Read_only_is_an_image_with_the_score() => WinFormsUi.Run(() =>
    {
        var rating = new RatingControl { Value = 4.5, IsReadOnly = true, AllowHalf = true };
        var empty = new RatingControl { IsReadOnly = true };
        using var form = WinFormsUi.Show(rating, empty);

        Assert.Equal(AccessibleRole.Graphic, rating.AccessibilityObject.Role);
        Assert.Equal("4.5 星，满分 5 星", rating.AccessibilityObject.Name);
        Assert.Equal(0, rating.AccessibilityObject.GetChildCount());
        Assert.False(rating.TabStop);
        Assert.Equal("未评分", empty.AccessibilityObject.Name);
    });

    [Fact]
    [Upstream("Rating", "只读时名称里总有分数：自己的 aria-label 后面接上，在 Field 里时标签后面接上")]
    public Task Read_only_names_keep_the_score() => WinFormsUi.Run(() =>
    {
        var own = new RatingControl { Value = 4, IsReadOnly = true, AccessibleName = "交付质量" };
        var inField = new RatingControl { Value = 3 };
        using var form = WinFormsUi.Show(own, new FieldControl { Label = "响应速度", IsReadOnly = true, Content = inField });

        Assert.Equal("交付质量 4 星，满分 5 星", own.AccessibilityObject.Name);
        Assert.Equal("响应速度 3 星，满分 5 星", inField.AccessibilityObject.Name);
    });
}
