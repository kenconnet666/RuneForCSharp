namespace Rune.WinForms.Tests;

/// <summary>EmptyControl：图标、标题、说明、操作居中排成一列；没给的不占位；内边距按字体。</summary>
public class EmptyTests
{
    [Fact]
    public Task Icon_title_description_and_action_stack_centered() => WinFormsUi.Run(() =>
    {
        var action = new ButtonControl { Text = "去逛逛" };
        var empty = new EmptyControl { Title = "还没有订单", Description = "下单后会显示在这里。", Action = action, Dock = DockStyle.Top };
        var bare = new EmptyControl { Icon = null, Title = "没有找到", Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(bare, empty);
        var em = empty.Font.SizeInPoints * empty.DeviceDpi / 72f;

        Assert.True(Math.Abs(action.Left + action.Width / 2 - empty.ClientSize.Width / 2) <= 1); // 操作居中
        Assert.True(action.Top > (int)MathF.Round(1.5f * em) + (int)MathF.Round(2.5f * em)); // 在图标、标题、说明下面
        Assert.Equal(action.Bottom + (int)MathF.Round(1.5f * em), empty.Height); // 下内边距 space.xl
        Assert.True(bare.Height < empty.Height); // 没给的不占位
        Assert.Equal(2.5f * em, empty.IconElement.Bounds.Width, 1f); // 图标 2.5em：画的大小与占的格子一样
        Assert.Equal(empty.IconElement.Bounds.Width * 96f / empty.DeviceDpi, empty.IconElement.Size.Pixels!.Value, 0.01f);
        Assert.Equal((AccessibleRole.Grouping, "还没有订单"), (empty.AccessibilityObject.Role, empty.AccessibilityObject.Name));
    });

    [Fact]
    public Task Part_props_merge_over_the_defaults() => WinFormsUi.Run(() =>
    {
        var empty = new EmptyControl { Title = "还没有订单", Description = "下单后会显示在这里。" };
        Assert.Equal((TextWeight.Medium, TextTone.Muted), (empty.TextElements.Title.Weight, empty.TextElements.Description.Tone));

        // 只改写了的字段，文字与没写的默认（居中）保留
        empty.PropsTitle = new TextProps { Tone = TextTone.Danger };
        empty.PropsDescription = new TextProps { Tone = TextTone.Accent };
        var (title, description) = empty.TextElements;
        Assert.Equal(("还没有订单", TextTone.Danger, TextWeight.Medium, TextAlign.Center), (title.Content, title.Tone, title.Weight, title.Align));
        Assert.Equal(("下单后会显示在这里。", TextTone.Accent), (description.Content, description.Tone));
    });
}
