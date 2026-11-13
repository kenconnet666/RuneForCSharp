namespace Rune.WinForms.Tests;

/// <summary>
/// TagControl：订阅了 RemoveRequested 才有移除按钮，读屏名称带上标签文字、可以改；点了请求移除；移除了时焦点交给同一容器里下一个能聚焦的控件；
/// 移除按钮 1.5em、在标签里面，标签宽出按钮的宽度。
/// </summary>
public class TagTests
{
    static void Raise(Control control, string method) =>
        typeof(Control).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(control, [EventArgs.Empty]);

    [Fact]
    public Task Hover_tints_with_the_tag_text_color() => WinFormsUi.Run(() =>
    {
        var removable = new TagControl { Text = "品牌：小米", Tone = StatusTone.Danger };
        removable.RemoveRequested += (_, _) => { };
        var selectable = new TagControl { Text = "加急", IsSelected = false };
        using var form = WinFormsUi.Show(removable, selectable);
        var fore = removable.Element.ForegroundIn(ElementContext.Of(removable));

        // 移除按钮悬停：底色是标签文字色的 14%，不是幽灵按钮的灰色；离开后没有
        Raise(removable.RemoveButton!, "OnMouseEnter");
        Assert.Equal(Color.FromArgb((int)MathF.Round(fore.A * 0.14f), fore), removable.RemoveButton!.Sx?.Background);
        Raise(removable.RemoveButton!, "OnMouseLeave");
        Assert.Null(removable.RemoveButton!.Sx?.Background);
        Assert.Equal(fore, removable.RemoveButton!.Sx?.Foreground);

        // 可选中的标签悬停叠一层 8% 的文字色：画出来比没悬停时深
        Color Edge()
        {
            using var bitmap = new Bitmap(selectable.Width, selectable.Height);
            selectable.DrawToBitmap(bitmap, new Rectangle(Point.Empty, selectable.Size));
            return bitmap.GetPixel(selectable.Element.Bounds.Left + 3, selectable.Height / 2);
        }
        var plain = Edge();
        Raise(selectable, "OnMouseEnter");
        Assert.True(Edge().GetBrightness() < plain.GetBrightness());
        Raise(selectable, "OnMouseLeave");
        Assert.Equal(plain, Edge());
    });

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

    [Fact]
    [Upstream("Tag", "_selected：整个标签是按钮")]
    public Task Selectable_tag_is_a_toggle_button_with_a_check_when_selected() => WinFormsUi.Run(() =>
    {
        var urgent = new TagControl { Text = "加急", IsSelected = false, Variant = StatusVariant.Soft };
        urgent.RemoveRequested += (_, _) => { };
        using var form = WinFormsUi.Show(urgent);

        Assert.Equal(AccessibleRole.PushButton, urgent.AccessibilityObject.Role);
        Assert.False(urgent.AccessibilityObject.State.HasFlag(AccessibleStates.Pressed));
        Assert.Null(urgent.RemoveButton); // 可选中的标签不带移除按钮
        Assert.Null(urgent.Element.IconPart);
        Assert.Equal((StatusTone.Accent, TextSize.Sm, StatusVariant.Soft), (urgent.Tone, urgent.TextSize, urgent.Variant)); // 没写的色调、字号取 accent、sm
        Assert.True(urgent.CanSelect);

        urgent.AccessibilityObject.DoDefaultAction();
        Assert.True(urgent.IsSelected);
        Assert.True(urgent.AccessibilityObject.State.HasFlag(AccessibleStates.Pressed));
        Assert.Same(Lucide.Check, urgent.Element.IconPart?.Data); // 选中时前面一个对勾
        Assert.Equal(StatusVariant.Solid, urgent.Variant); // 选中一律实底
    });
}
