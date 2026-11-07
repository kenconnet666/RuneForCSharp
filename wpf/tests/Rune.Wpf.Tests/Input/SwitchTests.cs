using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Switch：点击、空格切换；读屏的 Toggle；只读与切换中不切换；轨道里的文字只显示当前那份、对读屏隐藏；尺寸按 em；色调实底。</summary>
public class SwitchTests
{
    /// <summary>放进带主题的容器并完成布局。</summary>
    static StackPanel Show(params Switch[] toggles)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var toggle in toggles) host.Children.Add(toggle);
        WpfUi.Show(host);
        return host;
    }

    /// <summary>用这个部件样式键的元素（样式是同一个引用）。</summary>
    static List<T> Parts<T>(Switch owner, PartKey key) where T : FrameworkElement
    {
        var style = (Style)owner.FindResource(key);
        return WpfUi.Descendants(owner).OfType<T>().Where(part => ReferenceEquals(part.Style, style)).ToList();
    }

    static T Part<T>(Switch owner, PartKey key) where T : FrameworkElement => Assert.Single(Parts<T>(owner, key));

    static Color ColorOf(object? brush) => brush is SolidColorBrush solid ? solid.Color : default;

    static Color Resource(Switch owner, string key) => ColorOf(owner.FindResource(key));

    static AutomationPeer Peer(Switch owner) => UIElementAutomationPeer.CreatePeerForElement(owner);

    static IToggleProvider Toggle(Switch owner) => (IToggleProvider)Peer(owner).GetPattern(PatternInterface.Toggle);

    static void Click(Switch owner)
    {
        owner.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
        owner.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
    }

    static void PressSpace(Switch owner)
    {
        foreach (var routed in new[] { Keyboard.KeyDownEvent, Keyboard.KeyUpEvent })
            owner.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(owner), 0, Key.Space) { RoutedEvent = routed });
    }

    /// <summary>滑块在轨道里的横向位置。</summary>
    static double ThumbX(Switch owner) => Part<Border>(owner, SwitchParts.Thumb).TranslatePoint(default, Part<Border>(owner, SwitchParts.Track)).X;

    [Fact]
    [Upstream("Switch", "role=")]
    public Task Click_space_and_screen_readers_toggle_it() => WpfUi.Run(() =>
    {
        var toggle = new Switch { Content = "接收通知", IsInvalid = true };
        var host = Show(toggle);
        var track = Part<Border>(toggle, SwitchParts.Track);

        Assert.Equal(AutomationControlType.Button, Peer(toggle).GetAutomationControlType());
        Assert.Equal("接收通知", Peer(toggle).GetName());
        Assert.Equal(ToggleState.Off, Toggle(toggle).ToggleState);
        Assert.Equal(Resource(toggle, "Rune.Brush.Danger"), ColorOf(track.BorderBrush)); // 出错：轨道描边是危险色

        Click(toggle);
        host.UpdateLayout();
        Assert.True(toggle.IsChecked);
        Assert.Same(toggle.ToneFill, track.Background); // 开：色调实底

        PressSpace(toggle);
        Assert.False(toggle.IsChecked);

        Toggle(toggle).Toggle();
        Assert.Equal(ToggleState.On, Toggle(toggle).ToggleState);
    });

    [Fact]
    [Upstream("Switch", "loading：aria-busy")]
    public Task Loading_and_readonly_do_not_toggle_and_track_content_is_hidden_from_screen_readers() => WpfUi.Run(() =>
    {
        var toggle = new Switch { IsLoading = true, CheckedContent = "开", UncheckedContent = "关", Content = "自动保存" };
        Show(toggle);

        Toggle(toggle).Toggle();
        Click(toggle);
        Assert.False(toggle.IsChecked);
        Assert.Equal(Provider.GetLang(toggle).Loading, Peer(toggle).GetItemStatus());
        var contents = Parts<Border>(toggle, SwitchParts.Content);
        Assert.Equal(["开", "关"], contents.Select(c => ((ContentPresenter)c.Child).Content));
        Assert.Null(Peer(toggle).GetChildren()); // 轨道里的文字不进读屏的子节点
        Assert.Equal([Visibility.Hidden, Visibility.Visible], contents.Select(c => c.Visibility)); // 只显示当前状态那份，另一份占位

        toggle.IsLoading = false;
        toggle.IsReadOnly = true;
        Toggle(toggle).Toggle();
        PressSpace(toggle);
        Assert.False(toggle.IsChecked);
    });

    [Fact]
    [Upstream("Switch", "部件属性：轨道文字的外层与字号层分开")]
    public Task Track_content_has_an_outer_layer_and_a_text_layer() => WpfUi.Run(() =>
    {
        var toggle = new Switch { CheckedContent = "开", UncheckedContent = "关" };
        Show(toggle);

        foreach (var content in Parts<Border>(toggle, SwitchParts.Content))
        {
            var text = Assert.IsType<ContentPresenter>(content.Child);
            Assert.Same(toggle.FindResource(SwitchParts.ContentText), text.Style);
            Assert.Equal(toggle.FindResource("Rune.FontSize.Xs"), text.GetValue(TextElement.FontSizeProperty)); // 字号缩在字号层：外层的内边距仍按开关的字号算
        }
        // 外层的内边距给滑块留位置：文字在滑块的另一侧
        var contents = Parts<Border>(toggle, SwitchParts.Content);
        Assert.Equal((0.5 * toggle.FontSize, 1.375 * toggle.FontSize), (contents[0].Padding.Left, contents[0].Padding.Right));
        Assert.Equal((1.375 * toggle.FontSize, 0.5 * toggle.FontSize), (contents[1].Padding.Left, contents[1].Padding.Right));
    });

    [Fact]
    public Task Part_styles_from_the_scope_override_the_theme_defaults() => WpfUi.Run(() =>
    {
        var toggle = new Switch();
        var host = new StackPanel { Children = { toggle } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Resources.Add(SwitchParts.Thumb, new Style(typeof(Border)) { Setters = { new Setter(Border.BackgroundProperty, Brushes.Red) } });
        WpfUi.Show(host);

        Assert.Equal(Brushes.Red, Part<Border>(toggle, SwitchParts.Thumb).Background);
    });

    [Fact]
    public Task Sizes_are_in_em_and_the_track_widens_for_its_content() => WpfUi.Run(() =>
    {
        // 时长 token 设成 0：只看终态（滑块的过渡另测）
        var plain = new Switch { Resources = { ["Rune.Duration.Sm"] = TimeSpan.Zero } };
        var wide = new Switch { CheckedContent = "已开启", UncheckedContent = "关" };
        var labeled = new Switch { Content = "接收通知" };
        var host = Show(plain, wide, labeled);
        var em = plain.FontSize;

        var track = Part<Border>(plain, SwitchParts.Track);
        Assert.Equal(2.25 * em, track.ActualWidth, tolerance: 0.6);
        Assert.Equal(1.25 * em, track.ActualHeight, tolerance: 0.6);
        Assert.Equal(0.875 * em, Part<Border>(plain, SwitchParts.Thumb).ActualWidth, tolerance: 0.6);
        Assert.Equal(0.1875 * em, ThumbX(plain), tolerance: 0.6);
        Assert.True(plain.ActualHeight >= 24); // 可点击区域不小于 24px
        Assert.True(Part<Border>(wide, SwitchParts.Track).ActualWidth > 2.25 * em);

        // 文字：一行 1.5em 高，轨道居中在这一行里
        Assert.Equal(1.5 * em, Part<ContentPresenter>(labeled, ChoiceBaseParts.Label).ActualHeight, tolerance: 0.6);
        Assert.Equal(0.125 * em, Part<Border>(labeled, SwitchParts.Track).TranslatePoint(default, labeled).Y, tolerance: 0.6);

        plain.IsChecked = true;
        host.UpdateLayout();
        Assert.Equal(2.25 * em - 1.0625 * em, ThumbX(plain), tolerance: 0.6); // 滑到结束边
    });

    [Fact]
    public Task The_thumb_slides_in_the_duration_token() => WpfUi.Run(() =>
    {
        var toggle = new Switch();
        var host = Show(toggle);
        var end = 2.25 * toggle.FontSize - 1.0625 * toggle.FontSize;

        toggle.IsChecked = true;
        Assert.Equal(Motion.Enabled, toggle.HasAnimatedProperties); // 系统关闭动画时直接到位
        WpfUi.Wait(TimeSpan.FromMilliseconds(400));
        host.UpdateLayout();
        Assert.Equal(end, ThumbX(toggle), tolerance: 0.6);
    });

    [Fact]
    public Task Toggling_can_be_cancelled_for_controlled_use() => WpfUi.Run(() =>
    {
        var toggle = new Switch { Tone = StatusTone.Success };
        toggle.Toggling += (_, e) => e.Cancel = e.IsChecked;
        Show(toggle);

        Toggle(toggle).Toggle();
        Assert.False(toggle.IsChecked); // 拦下了：接口回来再写 IsChecked
        Assert.Equal(Resource(toggle, "Rune.Brush.SuccessText"), ColorOf(toggle.ToneFill));
    });

    [Fact]
    public Task Loading_shows_the_spinner_after_a_delay_and_disabled_switches_fade() => WpfUi.Run(() =>
    {
        var toggle = new Switch();
        var disabled = new Switch { IsEnabled = false };
        Show(toggle, disabled);
        var spinner = Part<Spinner>(toggle, SwitchParts.Spinner);

        // 显示以后再开始加载：等待从这里算起（显示本身慢的机器上，建好时就开始的话可能已经过了 200 毫秒）
        toggle.IsLoading = true;
        Assert.Equal(Visibility.Collapsed, spinner.Visibility); // 很快完成的切换不闪加载指示
        WpfUi.Wait(TimeSpan.FromMilliseconds(350));
        Assert.Equal(Visibility.Visible, spinner.Visibility);
        Assert.Equal(Resource(toggle, "Rune.Brush.Accent"), ColorOf(spinner.Foreground));

        Assert.Equal(toggle.FindResource("Rune.Opacity.Disabled"), disabled.Opacity);
        Assert.Throws<ElementNotEnabledException>(() => Toggle(disabled).Toggle());
    });
}
