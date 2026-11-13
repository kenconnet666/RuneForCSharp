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
    /// <summary>主题资源的颜色（按控件的主题变体查）。</summary>
    static IToggleProvider Toggle(Switch owner) => (IToggleProvider)ControlAutomationPeer.CreatePeerForElement(owner);

    static Color Mix(Color a, Color b, double amount)
    {
        byte Channel(byte x, byte y) => (byte)Math.Round(x * amount + y * (1 - amount));
        return Color.FromArgb(Channel(a.A, b.A), Channel(a.R, b.R), Channel(a.G, b.G), Channel(a.B, b.B));
    }

    [Fact]
    public Task Hover_mixes_text_color_into_the_track() => HeadlessUi.Run(() =>
    {
        var off = new Switch { Content = "通知" };
        var on = new Switch { Content = "同步", IsChecked = true };
        var window = HeadlessUi.Show(new StackPanel { Children = { off, on } });
        Color Track(Switch owner) => Lookup.ColorOf(owner.Part<Border>("PART_Track").GetBaseValue(Border.BackgroundProperty).Value as IBrush);
        var text = off.ThemeColor("Rune.Brush.Text");

        Assert.Equal(off.ThemeColor("Rune.Brush.BorderStrong"), Track(off));
        window.MouseMove(off.Part<Border>("PART_Track").TranslatePoint(new Point(2, 2), window)!.Value);
        HeadlessUi.Flush(window);
        Assert.Equal(Mix(off.ThemeColor("Rune.Brush.BorderStrong"), text, 0.85), Track(off)); // 关：borderStrong 混进 15% 的正文色
        window.MouseMove(on.Part<Border>("PART_Track").TranslatePoint(new Point(2, 2), window)!.Value);
        HeadlessUi.Flush(window);
        Assert.Equal(Mix(Lookup.ColorOf(on.ToneFill), text, 0.88), Track(on)); // 开：色调实底混进 12%
    });

    [Fact]
    [Upstream("Switch", "role=")]
    public Task Click_space_and_screen_readers_toggle_it() => HeadlessUi.Run(() =>
    {
        var toggle = new Switch { Content = "接收通知", IsInvalid = true };
        var window = HeadlessUi.Show(new StackPanel { Children = { toggle } });

        Assert.Equal(AutomationControlType.Button, ControlAutomationPeer.CreatePeerForElement(toggle).GetAutomationControlType());
        Assert.Equal("接收通知", ControlAutomationPeer.CreatePeerForElement(toggle).GetName());
        Assert.Equal(ToggleState.Off, Toggle(toggle).ToggleState);
        Assert.Equal(toggle.ThemeColor("Rune.Brush.Danger"), Lookup.ColorOf(toggle.Part<Border>("PART_Track").BorderBrush)); // 出错：轨道描边是危险色

        window.MouseDown(new Point(10, 10), MouseButton.Left);
        window.MouseUp(new Point(10, 10), MouseButton.Left);
        Assert.True(toggle.IsChecked);
        Assert.Contains(":checked", toggle.Classes);
        window.MouseMove(new Point(toggle.Bounds.Right + 50, 10)); // 指针移开：悬停时轨道另有颜色
        HeadlessUi.Flush(window);
        Assert.Same(toggle.ToneFill, toggle.Part<Border>("PART_Track").Background); // 开：色调实底

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

        var track = plain.Part<Border>("PART_Track");
        Assert.Equal(2.25 * em, track.Bounds.Width, tolerance: 0.6);
        Assert.Equal(1.25 * em, track.Bounds.Height, tolerance: 0.6);
        Assert.Equal(0.875 * em, plain.Part<Border>(SwitchParts.Thumb).Bounds.Width, tolerance: 0.6);
        Assert.Equal(0.1875 * em, plain.Part<Border>(SwitchParts.Thumb).Bounds.X, tolerance: 0.6);
        Assert.True(plain.Bounds.Height >= 24); // 可点击区域不小于 24px
        Assert.True(wide.Part<Border>("PART_Track").Bounds.Width > 2.25 * em);

        plain.IsChecked = true;
        HeadlessUi.Flush(window);
        Assert.Equal(2.25 * em - 1.0625 * em, plain.Part<Border>(SwitchParts.Thumb).Bounds.X, tolerance: 0.6); // 滑到结束边
    });

    [Fact]
    public Task Toggling_can_be_cancelled_for_controlled_use() => HeadlessUi.Run(() =>
    {
        var toggle = new Switch { Tone = StatusTone.Success };
        toggle.Toggling += (_, e) => e.Cancel = e.IsChecked;
        HeadlessUi.Show(toggle);

        Toggle(toggle).Toggle();
        Assert.False(toggle.IsChecked); // 拦下了：接口回来再写 IsChecked
        Assert.Equal(toggle.ThemeColor("Rune.Brush.SuccessText"), Lookup.ColorOf(toggle.ToneFill));
    });
}
