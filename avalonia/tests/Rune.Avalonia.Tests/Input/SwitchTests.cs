using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Rune.Avalonia.Tests;

/// <summary>Switch：点击、空格切换；读屏的 Toggle；只读与切换中不切换；轨道里的文字只显示当前那份、对读屏隐藏；尺寸按 em；色调实底。</summary>
public class SwitchTests
{
    static T Part<T>(Switch owner, string name) where T : Control => owner.GetLogicalDescendants().OfType<T>().First(part => part.Name == name);

    /// <summary>主题资源的颜色（按控件的主题变体查）。</summary>
    static Color Resource(Switch owner, string key) => owner.TryFindResource(key, owner.ActualThemeVariant, out var value) && value is ISolidColorBrush brush ? brush.Color : default;

    static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : default;

    static IToggleProvider Toggle(Switch owner) => (IToggleProvider)ControlAutomationPeer.CreatePeerForElement(owner);

    [Fact]
    [Upstream("Switch", "role=")]
    public Task Click_space_and_screen_readers_toggle_it() => HeadlessUi.Run(() =>
    {
        var toggle = new Switch { Content = "接收通知", IsInvalid = true };
        var window = HeadlessUi.Show(new StackPanel { Children = { toggle } });

        Assert.Equal(AutomationControlType.Button, ControlAutomationPeer.CreatePeerForElement(toggle).GetAutomationControlType());
        Assert.Equal("接收通知", ControlAutomationPeer.CreatePeerForElement(toggle).GetName());
        Assert.Equal(ToggleState.Off, Toggle(toggle).ToggleState);
        Assert.Equal(Resource(toggle, "Rune.Brush.Danger"), ColorOf(Part<Border>(toggle, "PART_Track").BorderBrush)); // 出错：轨道描边是危险色

        window.MouseDown(new Point(10, 10), MouseButton.Left);
        window.MouseUp(new Point(10, 10), MouseButton.Left);
        Assert.True(toggle.IsChecked);
        Assert.Contains(":checked", toggle.Classes);
        Assert.Same(toggle.ToneFill, Part<Border>(toggle, "PART_Track").Background); // 开：色调实底

        toggle.Focus();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Assert.False(toggle.IsChecked);

        Toggle(toggle).Toggle();
        Assert.Equal(ToggleState.On, Toggle(toggle).ToggleState);
    });

    [Fact]
    [Upstream("Switch", "loading：aria-busy")]
    public Task Loading_and_readonly_do_not_toggle_and_track_content_is_hidden_from_screen_readers() => HeadlessUi.Run(() =>
    {
        var toggle = new Switch { IsLoading = true, CheckedContent = "开", UncheckedContent = "关", Content = "自动保存" };
        HeadlessUi.Show(toggle);

        Toggle(toggle).Toggle();
        Assert.False(toggle.IsChecked);
        Assert.Equal(Provider.GetLang(toggle).Loading, ControlAutomationPeer.CreatePeerForElement(toggle).GetItemStatus());
        var contents = toggle.GetLogicalDescendants().OfType<Border>().Where(b => b.Name == SwitchParts.Content).ToList();
        Assert.Equal(["开", "关"], contents.Select(c => ((ContentPresenter)c.Child!).Content));
        Assert.All(contents, c => Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(c)));
        Assert.Equal([0d, 1d], contents.Select(c => c.Opacity)); // 只显示当前状态那份

        toggle.IsLoading = false;
        toggle.IsReadOnly = true;
        Toggle(toggle).Toggle();
        Assert.False(toggle.IsChecked);
    });

    [Fact]
    [Upstream("Switch", "部件属性：轨道文字的外层与字号层分开")]
    public Task Track_content_has_an_outer_layer_and_a_text_layer() => HeadlessUi.Run(() =>
    {
        var toggle = new Switch { CheckedContent = "开", UncheckedContent = "关" };
        HeadlessUi.Show(toggle);

        foreach (var content in toggle.GetLogicalDescendants().OfType<Border>().Where(b => b.Name == SwitchParts.Content))
        {
            var text = Assert.IsType<ContentPresenter>(content.Child);
            Assert.Equal(SwitchParts.ContentText, text.Name);
            Assert.True(toggle.TryFindResource("Rune.FontSize.Xs", toggle.ActualThemeVariant, out var xs));
            Assert.Equal(xs, text.FontSize); // 字号缩在字号层：外层的内边距仍按开关的字号算
        }
    });

    [Fact]
    public Task Sizes_are_in_em_and_the_track_widens_for_its_content() => HeadlessUi.Run(() =>
    {
        // 时长 token 设成 0：Avalonia 12 的无头平台推不动动画时钟（DESIGN §14 Collapse），只看终态
        var plain = new Switch { Resources = { ["Rune.Duration.Sm"] = TimeSpan.Zero } };
        var wide = new Switch { CheckedContent = "已开启", UncheckedContent = "关" };
        var window = HeadlessUi.Show(new StackPanel { Children = { plain, wide } });
        var em = plain.FontSize;

        var track = Part<Border>(plain, "PART_Track");
        Assert.Equal(2.25 * em, track.Bounds.Width, tolerance: 0.6);
        Assert.Equal(1.25 * em, track.Bounds.Height, tolerance: 0.6);
        Assert.Equal(0.875 * em, Part<Border>(plain, SwitchParts.Thumb).Bounds.Width, tolerance: 0.6);
        Assert.Equal(0.1875 * em, Part<Border>(plain, SwitchParts.Thumb).Bounds.X, tolerance: 0.6);
        Assert.True(plain.Bounds.Height >= 24); // 可点击区域不小于 24px
        Assert.True(Part<Border>(wide, "PART_Track").Bounds.Width > 2.25 * em);

        plain.IsChecked = true;
        HeadlessUi.Flush(window);
        Assert.Equal(2.25 * em - 1.0625 * em, Part<Border>(plain, SwitchParts.Thumb).Bounds.X, tolerance: 0.6); // 滑到结束边
    });

    [Fact]
    public Task Toggling_can_be_cancelled_for_controlled_use() => HeadlessUi.Run(() =>
    {
        var toggle = new Switch { Tone = StatusTone.Success };
        toggle.Toggling += (_, e) => e.Cancel = e.IsChecked;
        HeadlessUi.Show(toggle);

        Toggle(toggle).Toggle();
        Assert.False(toggle.IsChecked); // 拦下了：接口回来再写 IsChecked
        Assert.Equal(Resource(toggle, "Rune.Brush.SuccessText"), ColorOf(toggle.ToneFill));
    });
}
