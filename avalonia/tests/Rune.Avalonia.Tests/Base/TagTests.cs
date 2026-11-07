using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Tag：订阅了 RemoveRequested 才有移除按钮，读屏名称带上标签文字、可以改；离场结束才请求移除，没移除时复原；
/// 移除了时焦点交给同一容器里下一个能聚焦的元素；外观同 Badge，移除按钮让末端的内边距收成 0.25em。
/// </summary>
public class TagTests
{
    static CloseButton? RemoveButton(Tag tag) => tag.GetVisualChildren().OfType<CloseButton>().SingleOrDefault();

    [Fact]
    [Upstream("Tag", "给了 onRemove 才有移除按钮")]
    public Task The_remove_button_appears_with_a_handler_and_asks_after_leaving() => HeadlessUi.Run(() =>
    {
        var requests = 0;
        var plain = new Tag { Content = "只读" };
        var shanghai = new Tag { Content = "上海" };
        shanghai.RemoveRequested += (_, _) => requests++;
        var beijing = new Tag { Content = "北京", RemoveLabel = "删除筛选条件" };
        beijing.RemoveRequested += (_, _) => { };
        var window = HeadlessUi.Show(new StackPanel { Children = { plain, shanghai, beijing } });

        Assert.Null(RemoveButton(plain));
        Assert.Equal("移除 上海", AutomationProperties.GetName(RemoveButton(shanghai)!));
        Assert.Equal("删除筛选条件", AutomationProperties.GetName(RemoveButton(beijing)!));

        ((IInvokeProvider)RemoveButton(shanghai)!.Peer()).Invoke();
        HeadlessUi.Flush(window);
        Assert.True(shanghai.IsDismissing);
        HeadlessUi.Until(() => requests == 1);
        // 使用方没移除：复原
        HeadlessUi.Until(() => shanghai.Opacity == 1 && !shanghai.IsDismissing);
    });

    [Fact]
    public Task Removing_hands_focus_to_the_next_tag() => HeadlessUi.Run(() =>
    {
        var row = new StackPanel();
        foreach (var city in new[] { "上海", "北京", "广州" })
        {
            var tag = new Tag { Content = city };
            tag.RemoveRequested += (sender, _) => row.Children.Remove((Tag)sender!);
            row.Children.Add(tag);
        }
        var window = HeadlessUi.Show(row);
        var (first, second) = ((Tag)row.Children[0], (Tag)row.Children[1]);

        RemoveButton(first)!.Focus(NavigationMethod.Tab);
        ((IInvokeProvider)RemoveButton(first)!.Peer()).Invoke();
        HeadlessUi.Until(() => !row.Children.Contains(first));
        HeadlessUi.Until(() => RemoveButton(second)!.IsFocused);
    });

    [Fact]
    public Task The_remove_button_is_one_and_a_half_em_and_the_end_padding_shrinks() => HeadlessUi.Run(() =>
    {
        var plain = new Tag { Content = "上海" };
        var removable = new Tag { Content = "上海" };
        removable.RemoveRequested += (_, _) => { };
        HeadlessUi.Show(new StackPanel { Children = { plain, removable } });

        var button = RemoveButton(removable)!;
        Assert.Equal(18, button.Bounds.Width); // 12px × 1.5
        Assert.Equal(12, button.FontSize);
        Assert.Equal(plain.Bounds.Height, removable.Bounds.Height);
        // 末端：0.25em（3）代替 0.5em（6），中间隔 0.25em
        Assert.Equal(plain.Bounds.Width - 6 + 3 + 3 + 18, removable.Bounds.Width, 0.5);
    });
}
