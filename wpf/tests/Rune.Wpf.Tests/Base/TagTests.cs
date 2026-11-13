using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>
/// Tag：订阅了 RemoveRequested 才有移除按钮，读屏名称带上标签文字、可以改；离场结束才请求移除，没移除时复原；
/// 移除了时焦点交给同一容器里下一个能聚焦的元素；外观同 Badge，移除按钮让末端的内边距收成 0.25em。
/// </summary>
public class TagTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static CloseButton? RemoveButton(Tag tag) => WpfUi.Descendants(tag).OfType<CloseButton>().SingleOrDefault();

    static void Invoke(CloseButton button) => ((IInvokeProvider)button.Peer().GetPattern(PatternInterface.Invoke)).Invoke();

    [Fact]
    [Upstream("Tag", "给了 onRemove 才有移除按钮")]
    public Task The_remove_button_appears_with_a_handler_and_asks_after_leaving() => WpfUi.Run(() =>
    {
        var requests = 0;
        var opacityWhenAsked = double.NaN;
        var plain = new Tag { Content = "只读" };
        var shanghai = new Tag { Content = "上海" };
        shanghai.RemoveRequested += (_, _) => (requests, opacityWhenAsked) = (requests + 1, shanghai.Opacity);
        var beijing = new Tag { Content = "北京", RemoveLabel = "删除筛选条件" };
        beijing.RemoveRequested += (_, _) => { };
        WpfUi.Show(Host(plain, shanghai, beijing));

        Assert.Null(RemoveButton(plain));
        Assert.Equal("移除 上海", AutomationProperties.GetName(RemoveButton(shanghai)!));
        Assert.Equal("删除筛选条件", AutomationProperties.GetName(RemoveButton(beijing)!));

        Invoke(RemoveButton(shanghai)!);
        WpfUi.Flush();
        WpfUi.Until(() => requests == 1);
        Assert.Equal(0, opacityWhenAsked); // 离场结束才问（关了动画的机器上离场是同步的，不能看 IsDismissing 的中间态）
        // 使用方没移除：复原
        WpfUi.Until(() => shanghai.Opacity == 1 && !shanghai.IsDismissing);
    });

    [Fact]
    public Task Removing_hands_focus_to_the_next_tag() => WpfUi.Run(() =>
    {
        var row = Host();
        foreach (var city in new[] { "上海", "北京", "广州" })
        {
            var tag = new Tag { Content = city };
            tag.RemoveRequested += (sender, _) => row.Children.Remove((Tag)sender!);
            row.Children.Add(tag);
        }
        var window = WpfUi.Show(row);
        window.Activate();
        var (first, second) = ((Tag)row.Children[0], (Tag)row.Children[1]);

        RemoveButton(first)!.Focus();
        WpfUi.Until(() => RemoveButton(first)!.IsKeyboardFocused);
        Invoke(RemoveButton(first)!);
        WpfUi.Until(() => !row.Children.Contains(first));
        WpfUi.Until(() => RemoveButton(second)!.IsKeyboardFocused);
    });

    [Fact]
    public Task The_remove_button_is_one_and_a_half_em_and_the_end_padding_shrinks() => WpfUi.Run(() =>
    {
        var plain = new Tag { Content = "上海" };
        var removable = new Tag { Content = "上海" };
        removable.RemoveRequested += (_, _) => { };
        WpfUi.Show(Host(plain, removable));

        var button = RemoveButton(removable)!;
        Assert.Equal(18, button.ActualWidth); // 12px × 1.5
        Assert.Equal(12, button.FontSize);
        Assert.Equal(plain.ActualHeight, removable.ActualHeight);
        // 末端：0.25em（3）代替 0.5em（6），中间隔 0.25em
        Assert.Equal(plain.DesiredSize.Width - 6 + 3 + 3 + 18, removable.DesiredSize.Width, 0.5);
    });

    [Fact]
    [Upstream("Tag", "_selected：整个标签是按钮")]
    public Task Selectable_tag_is_a_toggle_button_with_a_check_when_selected() => WpfUi.Run(() =>
    {
        var urgent = new Tag { Content = "加急", IsSelected = false };
        urgent.RemoveRequested += (_, _) => { };
        WpfUi.Show(Host(urgent));
        WpfUi.Flush();

        var peer = urgent.Peer();
        var toggle = (IToggleProvider)peer.GetPattern(PatternInterface.Toggle);
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
        Assert.Equal(ToggleState.Off, toggle.ToggleState);
        Assert.Null(urgent.Icon);
        Assert.Null(RemoveButton(urgent)); // 可选中的标签不带移除按钮
        Assert.Equal((StatusTone.Accent, TextSize.Sm, StatusVariant.Surface), (urgent.Tone, urgent.Size, urgent.Variant)); // 没写的色调、字号取 accent、sm
        Assert.True(urgent.Focusable);

        toggle.Toggle();
        WpfUi.Flush();
        Assert.True(urgent.IsSelected);
        Assert.Equal(ToggleState.On, toggle.ToggleState);
        Assert.Same(Lucide.Check, urgent.Icon); // 选中时前面一个对勾
        Assert.Equal(StatusVariant.Solid, urgent.Variant);
    });
}
