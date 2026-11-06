namespace Rune.WinForms.Tests;

/// <summary>正文列表：圆点、编号、图标代替圆点（部件属性逐层合并）；项之间的间距与字号档位。</summary>
public class ListTests
{
    [Fact]
    public Task Bullets_numbers_and_icons() => WinFormsUi.Run(() =>
    {
        var bullets = new UnorderedListControl();
        bullets.Controls.AddRange([new ListItemControl { Text = "一" }, new ListItemControl { Text = "二" }]);
        var numbers = new OrderedListControl { Start = 3 };
        numbers.Controls.AddRange([new ListItemControl { Text = "三" }, new ListItemControl { Text = "四" }]);
        var nested = new OrderedListControl();
        var step = new ListItemControl { Text = "第一步" };
        nested.Controls.Add(step);
        var included = new ListItemControl { Text = "包含" };
        var excluded = new ListItemControl { Text = "不包含", Icon = Lucide.X, PropsIcon = new() { StrokeWidth = 3 } };
        excluded.Controls.Add(nested);
        var icons = new UnorderedListControl { Icon = Lucide.Check, PropsIcon = new() { Tone = TextTone.Success } };
        icons.Controls.AddRange([included, excluded]);
        using var form = WinFormsUi.Show(bullets, numbers, icons);

        Assert.Equal(["•", "•"], bullets.Controls.Cast<ListItemControl>().Select(item => item.Marker));
        Assert.Equal(["3.", "4."], numbers.Controls.Cast<ListItemControl>().Select(item => item.Marker));
        Assert.Equal("", included.Marker);
        Assert.Equal((Lucide.Check, TextTone.Success), (included.IconElement!.Data, included.IconElement.Tone));
        // 逐项覆盖，部件属性与列表的合并；有序列表里不沿用外层的图标
        Assert.Equal((Lucide.X, TextTone.Success, 3f), (excluded.IconElement!.Data, excluded.IconElement.Tone, excluded.IconElement.StrokeWidth));
        Assert.Equal("1.", step.Marker);
        Assert.Null(step.IconElement);
        // 嵌套的列表排在项的文字下面
        Assert.Equal(excluded.TextBounds.Bottom, nested.Top);
    });

    [Fact]
    public Task Text_is_indented_past_the_marker_and_items_keep_the_gap() => WinFormsUi.Run(() =>
    {
        var first = new ListItemControl { Text = "一" };
        var second = new ListItemControl { Text = "二" };
        var bullets = new UnorderedListControl();
        bullets.Controls.AddRange([first, second]);
        var roomy = new UnorderedListControl { Gap = Space.Md, TextSize = TextSize.Lg };
        var roomyItem = new ListItemControl { Text = "一" };
        roomy.Controls.Add(roomyItem);
        using var form = WinFormsUi.Show(bullets, roomy);
        var em = first.Font.SizeInPoints * first.DeviceDpi / 72f;

        Assert.Equal(first.Bottom + (int)MathF.Round(0.25f * em), second.Top);
        Assert.Equal(16f * first.DeviceDpi / 96f, roomyItem.Font.SizeInPoints * roomyItem.DeviceDpi / 72f, 1);
        // 记号占 1.5em，文字再隔 space.xs
        Assert.Equal((int)MathF.Round(1.75f * em), first.TextBounds.X);
        Assert.Equal(AccessibleRole.List, bullets.AccessibilityObject.Role);
        Assert.Equal((AccessibleRole.ListItem, "一"), (first.AccessibilityObject.Role, first.AccessibilityObject.Name));
    });
}
