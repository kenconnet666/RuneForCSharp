using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
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
    public Task The_remove_button_hovers_with_the_tag_text_color() => HeadlessUi.Run(() =>
    {
        var tag = new Tag { Content = "品牌：小米", Tone = StatusTone.Danger };
        tag.RemoveRequested += (_, _) => { };
        var window = HeadlessUi.Show(new StackPanel { Children = { tag } });
        var remove = RemoveButton(tag)!;
        var fore = Assert.IsAssignableFrom<ISolidColorBrush>(tag.Foreground).Color;

        var center = remove.TranslatePoint(new global::Avalonia.Point(remove.Bounds.Width / 2, remove.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        HeadlessUi.Flush(window);
        var hover = Assert.IsAssignableFrom<ISolidColorBrush>(remove.GetBaseValue(global::Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty).Value); // 基础值（不含颜色过渡的中间值）
        Assert.Equal((fore, 0.14), (hover.Color, hover.Opacity)); // 标签文字色的 14%，不是幽灵按钮的灰色
        window.MouseMove(tag.TranslatePoint(new global::Avalonia.Point(2, tag.Bounds.Height / 2), window)!.Value); // 移到标签的文字那边
        HeadlessUi.Flush(window);
        Assert.NotEqual(fore, (remove.GetBaseValue(global::Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty).Value as ISolidColorBrush)?.Color); // 离开后没有底色
    });

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

    [Fact]
    [Upstream("Tag", "_selected：整个标签是按钮")]
    public Task Selectable_tag_is_a_toggle_button_with_a_check_when_selected() => HeadlessUi.Run(() =>
    {
        var urgent = new Tag { Content = "加急", IsSelected = false, Variant = StatusVariant.Soft };
        urgent.RemoveRequested += (_, _) => { };
        var window = HeadlessUi.Show(urgent);

        var peer = urgent.Peer();
        Assert.Equal(global::Avalonia.Automation.Peers.AutomationControlType.Button, peer.GetAutomationControlType());
        Assert.Equal(ToggleState.Off, ((IToggleProvider)peer).ToggleState);
        Assert.Null(urgent.Icon);
        Assert.Null(RemoveButton(urgent)); // 可选中的标签不带移除按钮
        Assert.Equal((StatusTone.Accent, TextSize.Sm, StatusVariant.Soft), (urgent.Tone, urgent.Size, urgent.Variant)); // 没写的色调、字号取 accent、sm
        Assert.True(urgent.Focusable);

        ((IToggleProvider)peer).Toggle();
        Assert.True(urgent.IsSelected);
        Assert.Equal(ToggleState.On, ((IToggleProvider)peer).ToggleState);
        Assert.Same(Lucide.Check, urgent.Icon); // 选中时前面一个对勾
        Assert.Equal(StatusVariant.Solid, urgent.Variant); // 选中一律实底

        urgent.Focus();
        urgent.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space, Source = urgent });
        Assert.False(urgent.IsSelected);
        Assert.Equal(StatusVariant.Soft, urgent.Variant);
    });
}
