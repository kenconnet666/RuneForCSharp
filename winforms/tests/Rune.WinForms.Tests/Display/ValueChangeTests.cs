namespace Rune.WinForms.Tests;

/// <summary>ValueChangeControl：原值划掉、箭头、新值，读屏念"原值 改为 新值"；没有原值时只有新值。</summary>
public class ValueChangeTests
{
    [Fact]
    [Upstream("ValueChange", "有原值：del、改为、ins")]
    public Task Before_is_struck_out_and_read_as_changed_to() => WinFormsUi.Run(() =>
    {
        var change = new ValueChangeControl { Before = "1280", After = "1281" };
        using var form = WinFormsUi.Show(change);

        var (before, arrow, after) = change.Elements;
        Assert.True(before.Strikethrough);
        Assert.True(before.Bounds.Right < arrow.Bounds.Left && arrow.Bounds.Right < after.Bounds.Left); // 原值、箭头、新值依次排开
        Assert.Equal((AccessibleRole.StaticText, "1280 改为 1281"), (change.AccessibilityObject.Role, change.AccessibilityObject.Name));
    });

    [Fact]
    [Upstream("ValueChange", "没有原值：只有新值")]
    public Task Without_before_only_the_new_value_shows() => WinFormsUi.Run(() =>
    {
        var change = new ValueChangeControl { After = "新值" };
        using var form = WinFormsUi.Show(change);

        Assert.Equal(0, change.Elements.After.Bounds.Left);
        Assert.Equal(change.Elements.After.Bounds.Width, change.Width); // 只占新值的宽度
        Assert.Equal("新值", change.AccessibilityObject.Name);
    });
}
