namespace Rune.WinForms.Tests;

/// <summary>
/// TagControl：订阅了 RemoveRequested 才有移除按钮，读屏名称带上标签文字、可以改；点了请求移除；移除了时焦点交给同一容器里下一个能聚焦的控件；
/// 移除按钮 1.5em、在标签里面，标签宽出按钮的宽度。
/// </summary>
public class TagTests
{
    [Fact]
    [Upstream("Tag", "给了 onRemove 才有移除按钮")]
    public Task The_remove_button_appears_with_a_handler_and_asks_for_removal() => WinFormsUi.Run(() =>
    {
        var requests = 0;
        var plain = new TagControl { Text = "只读" };
        var shanghai = new TagControl { Text = "上海" };
        shanghai.RemoveRequested += (_, _) => requests++;
        var beijing = new TagControl { Text = "北京", RemoveLabel = "删除筛选条件" };
        beijing.RemoveRequested += (_, _) => { };
        using var form = WinFormsUi.Show(plain, shanghai, beijing);

        Assert.Null(plain.RemoveButton);
        Assert.Equal("移除 上海", shanghai.RemoveButton!.AccessibilityObject.Name);
        Assert.Equal("删除筛选条件", beijing.RemoveButton!.AccessibilityObject.Name);
        shanghai.RemoveButton.PerformClick();
        Assert.Equal(1, requests);
    });

    [Fact]
    public Task Removing_hands_focus_to_the_next_tag() => WinFormsUi.Run(() =>
    {
        var row = new FlexControl { Dock = DockStyle.Fill };
        foreach (var city in new[] { "上海", "北京", "广州" })
        {
            var tag = new TagControl { Text = city };
            tag.RemoveRequested += (sender, _) => row.Controls.Remove((Control)sender!);
            row.Controls.Add(tag);
        }
        using var form = WinFormsUi.Show(row);
        var (first, second) = ((TagControl)row.Controls[0], (TagControl)row.Controls[1]);

        first.RemoveButton!.Focus();
        Assert.True(first.RemoveButton.Focused);
        first.RemoveButton.PerformClick();
        Assert.DoesNotContain(first, row.Controls.Cast<Control>());
        Assert.True(second.RemoveButton!.Focused);
    });

    [Fact]
    public Task The_remove_button_is_one_and_a_half_em_inside_the_tag() => WinFormsUi.Run(() =>
    {
        var plain = new TagControl { Text = "上海" };
        var removable = new TagControl { Text = "上海", Location = new Point(0, 40) };
        removable.RemoveRequested += (_, _) => { };
        using var form = WinFormsUi.Show(plain, removable);

        var em = removable.Element.EmIn(ElementContext.Of(removable));
        var button = removable.RemoveButton!;
        Assert.Equal((int)MathF.Round(1.5f * em), button.Width - button.Padding.Horizontal);
        Assert.Equal(plain.Height, removable.Height);
        Assert.InRange(removable.Width - plain.Width, (int)(1.5f * em) - 1, (int)(1.5f * em) + 1);
        Assert.True(button.Right <= removable.Width);
    });
}
