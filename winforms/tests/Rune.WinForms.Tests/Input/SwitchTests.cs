using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Rune.WinForms.Tests;

/// <summary>SwitchControl：点击、空格切换；读屏的复选状态；只读与切换中不切换、轨道里的文字只显示当前那份；尺寸按 em；色调实底；不可用时变淡。</summary>
public class SwitchTests
{
    const uint WmKeyDown = 0x100, WmKeyUp = 0x101, WmLButtonDown = 0x201, WmLButtonUp = 0x202;
    const int VkSpace = 0x20;

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);

    /// <summary>真实的鼠标消息：在控件的 (x, y) 按下、松开。</summary>
    static void Click(Control control, int x, int y)
    {
        var position = (nint)((y << 16) | (x & 0xFFFF));
        SendMessage(control.Handle, WmLButtonDown, 1, position);
        SendMessage(control.Handle, WmLButtonUp, 0, position);
    }

    static void PressSpace(Control control)
    {
        SendMessage(control.Handle, WmKeyDown, VkSpace, 0);
        SendMessage(control.Handle, WmKeyUp, VkSpace, unchecked((nint)0xC0000001));
    }

    static ElementContext ContextOf(Control control) => new(control.DeviceDpi, control.Font, control.ForeColor, false);

    static Color Pixel(Control control, int x, int y)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap.GetPixel(x, y);
    }

    static void AssertNear(Color expected, Color actual, int tolerance = 8)
    {
        Assert.InRange(Math.Abs(expected.R - actual.R), 0, tolerance);
        Assert.InRange(Math.Abs(expected.G - actual.G), 0, tolerance);
        Assert.InRange(Math.Abs(expected.B - actual.B), 0, tolerance);
    }

    /// <summary>轨道在控件里的位置。</summary>
    static RectangleF Track(SwitchControl control) => control.Element.Parts(ContextOf(control), 0).Track;

    [Fact]
    [Upstream("Switch", "role=")]
    public Task Click_space_and_screen_readers_toggle_it() => WinFormsUi.Run(() =>
    {
        var toggle = new SwitchControl { Text = "接收通知", IsInvalid = true };
        using var form = WinFormsUi.Show(toggle);
        var colors = RuneTheme.Current.Value.Colors;

        Assert.Equal(AccessibleRole.CheckButton, toggle.AccessibilityObject.Role);
        Assert.Equal("接收通知", toggle.AccessibilityObject.Name);
        Assert.False(toggle.AccessibilityObject.State.HasFlag(AccessibleStates.Checked));
        var track = Track(toggle);
        AssertNear(colors.Danger, Pixel(toggle, (int)track.X + 1, (int)(track.Y + track.Height / 2))); // 出错：轨道描边是危险色（边缘抗锯齿，允许一点偏差）

        var changes = 0;
        toggle.IsCheckedChanged += (_, _) => changes++;
        Click(toggle, (int)track.X + 4, (int)(track.Y + track.Height / 2));
        Assert.True(toggle.IsChecked);
        Assert.True(toggle.AccessibilityObject.State.HasFlag(AccessibleStates.Checked));
        Assert.Equal(1, changes);

        PressSpace(toggle);
        Assert.False(toggle.IsChecked);
        Assert.Equal(2, changes);

        toggle.AccessibilityObject.DoDefaultAction();
        Assert.True(toggle.IsChecked);
        Assert.True(toggle.CanSelect);
    });

    [Fact]
    [Upstream("Switch", "loading：aria-busy")]
    public Task Loading_and_readonly_do_not_toggle_and_track_content_is_hidden_from_screen_readers() => WinFormsUi.Run(() =>
    {
        var toggle = new SwitchControl { CheckedContent = "开", UncheckedContent = "关", Text = "自动保存" };
        using var form = WinFormsUi.Show(toggle);
        var (on, off) = toggle.Element.ContentParts;
        toggle.IsLoading = true;

        toggle.Toggle();
        Assert.False(toggle.IsChecked);
        Assert.True(toggle.AccessibilityObject.State.HasFlag(AccessibleStates.Busy)); // 读屏的“忙”立即生效，指示才防闪烁
        Assert.False(toggle.Element.ShowsSpinner);
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(350));
        Assert.True(toggle.Element.ShowsSpinner);

        Assert.Equal(["开", "关"], new[] { on, off }.Select(part => ((Text)part!).Content));
        Assert.True(toggle.AccessibilityObject.GetChildCount() <= 0); // 轨道里的文字不进无障碍树（-1 是“没有子项信息”）
        Assert.Same(off, toggle.Element.VisibleContent); // 只显示当前状态那份
        toggle.IsChecked = true;
        Assert.Same(on, toggle.Element.VisibleContent);

        toggle.IsChecked = false;
        toggle.IsLoading = false;
        toggle.IsReadOnly = true;
        toggle.Toggle();
        Assert.False(toggle.IsChecked);
        Assert.True(toggle.AccessibilityObject.State.HasFlag(AccessibleStates.ReadOnly));
    });

    [Fact]
    [Upstream("Switch", "部件属性：轨道文字的外层与字号层分开")]
    public Task Track_content_has_its_own_text_props() => WinFormsUi.Run(() =>
    {
        var toggle = new SwitchControl { CheckedContent = "开", UncheckedContent = "关" };
        using var form = WinFormsUi.Show(toggle);

        foreach (var part in new[] { toggle.Element.ContentParts.Checked, toggle.Element.ContentParts.Unchecked })
        {
            var text = Assert.IsType<Text>(part);
            Assert.Equal((TextSize.Xs, TextWeight.Semibold), (text.Size, text.Weight));
        }
        // 字号缩在内容上：滑块两侧的留白（0.5em + 1.375em）仍按开关自己的字号算
        var em = toggle.Font.SizeInPoints * toggle.DeviceDpi / 72;
        var unbounded = new Size(int.MaxValue, int.MaxValue);
        var widest = new[] { toggle.Element.ContentParts.Checked, toggle.Element.ContentParts.Unchecked }.Max(part => part!.Measure(ContextOf(toggle), unbounded).Width);
        Assert.Equal(widest + 1.875 * em, Track(toggle).Width, tolerance: 0.6);

        // PropsContentText 逐字段盖在默认上：没写的字号、字重仍在
        toggle.PropsContentText = new TextProps { Tone = TextTone.Danger };
        var overridden = Assert.IsType<Text>(toggle.Element.ContentParts.Checked);
        Assert.Equal((TextTone.Danger, TextSize.Xs, TextWeight.Semibold), (overridden.Tone, overridden.Size, overridden.Weight));
    });

    [Fact]
    public Task Sizes_are_in_em_and_the_track_widens_for_its_content() => WinFormsUi.Run(() =>
    {
        var plain = new SwitchControl();
        var wide = new SwitchControl { CheckedContent = "已开启", UncheckedContent = "关", Top = 100 };
        var icons = new SwitchControl { CheckedContent = Lucide.Moon, UncheckedContent = Lucide.Sun, Top = 200 };
        using var form = WinFormsUi.Show(plain, wide, icons);
        var em = plain.Font.SizeInPoints * plain.DeviceDpi / 72;
        var context = ContextOf(plain);

        var (track, thumb) = plain.Element.Parts(context, 0);
        Assert.Equal(2.25 * em, track.Width, tolerance: 0.6);
        Assert.Equal(1.25 * em, track.Height, tolerance: 0.6);
        Assert.Equal(0.875 * em, thumb.Width, tolerance: 0.6);
        Assert.Equal(0.1875 * em, thumb.X - track.X, tolerance: 0.6);
        Assert.True(plain.Height - plain.Padding.Vertical >= 24 * plain.DeviceDpi / 96); // 可点击区域不小于 24px
        Assert.True(Track(wide).Width > 2.25 * em);
        Assert.True(Track(icons).Width > 2.25 * em);

        // 滑到结束边：离轨道末端 1.0625em（0.1875em + 0.875em 的滑块的另一侧）
        var end = plain.Element.Parts(context, 1);
        Assert.Equal(2.25 * em - 1.0625 * em, end.Thumb.X - end.Track.X, tolerance: 0.6);

        // 从右到左时整体镜像
        var rtl = plain.Element.Parts(context with { RightToLeft = true }, 0);
        Assert.Equal(plain.Element.Bounds.Right - 0.1875 * em - 0.875 * em, rtl.Thumb.X, tolerance: 0.6);
    });

    [Fact]
    public Task The_thumb_slides_to_the_other_end_in_durations_sm() => WinFormsUi.Run(() =>
    {
        var toggle = new SwitchControl();
        using var form = WinFormsUi.Show(toggle);

        toggle.IsChecked = true;
        Assert.Equal(Motion.Enabled, toggle.Element.Position < 1); // 动画刚开始，没有跳到终点；系统关闭动画时直接到终点
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(300));
        Assert.Equal(1, toggle.Element.Position);

        // 还没显示出来时直接到位
        Assert.Equal(1, new SwitchControl { IsChecked = true }.Element.Position);
    });

    [Fact]
    public Task Toggling_can_be_cancelled_for_controlled_use() => WinFormsUi.Run(() =>
    {
        var toggle = new SwitchControl { Tone = StatusTone.Success };
        toggle.Toggling += (_, e) => e.Cancel = e.IsChecked;
        using var form = WinFormsUi.Show(toggle);
        var colors = RuneTheme.Current.Value.Colors;

        toggle.Toggle();
        Assert.False(toggle.IsChecked); // 拦下了：接口回来再写 IsChecked
        Assert.Equal(colors.SuccessText, ToneFill.Colors(StatusTone.Success, colors).Fill);

        toggle.IsChecked = true;
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(300));
        WinFormsUi.Paint(toggle);
        var track = Track(toggle);
        Assert.Equal(colors.SuccessText.ToArgb(), Pixel(toggle, (int)track.X + 3, (int)(track.Y + track.Height / 2)).ToArgb()); // 开：色调实底
    });

    [Fact]
    public Task Tones_map_to_the_fill_and_the_color_on_it() => WinFormsUi.Run(() =>
    {
        var colors = RuneTheme.Current.Value.Colors;

        Assert.Equal((colors.InverseSurface, colors.InverseText), ToneFill.Colors(StatusTone.Neutral, colors));
        Assert.Equal((colors.Accent, colors.AccentContrast), ToneFill.Colors(default, colors)); // 不写按 accent
        Assert.Equal((colors.DangerText, colors.Background), ToneFill.Colors(StatusTone.Danger, colors));
    });

    [Fact]
    public Task Hover_mixes_text_color_into_the_track() => WinFormsUi.Run(() =>
    {
        var toggle = new SwitchControl();
        using var form = WinFormsUi.Show(toggle);
        var track = Track(toggle);
        var (x, y) = ((int)track.Right - 3, (int)(track.Y + track.Height / 2));
        var colors = RuneTheme.Current.Value.Colors;

        toggle.Element.IsHovered = true;
        Assert.Equal(Drawing.Mix(colors.BorderStrong, colors.Text, 0.15).ToArgb(), Pixel(toggle, x, y).ToArgb()); // 关：borderStrong 混进 15% 的正文色
        toggle.Element.IsHovered = false;
        Assert.Equal(colors.BorderStrong.ToArgb(), Pixel(toggle, x, y).ToArgb());
    });

    [Fact]
    public Task Disabled_cannot_toggle_and_fades_to_opacities_disabled() => WinFormsUi.Run(() =>
    {
        var toggle = new SwitchControl();
        using var form = WinFormsUi.Show(toggle);
        var track = Track(toggle);
        var (x, y) = ((int)track.Right - 3, (int)(track.Y + track.Height / 2)); // 关着时滑块在起始边，取结束边的一点
        var colors = RuneTheme.Current.Value.Colors;
        Assert.Equal(colors.BorderStrong.ToArgb(), Pixel(toggle, x, y).ToArgb());

        toggle.Enabled = false;
        toggle.Toggle();
        Assert.False(toggle.IsChecked);
        var faded = Pixel(toggle, x, y);
        var expected = Drawing.Mix(form.BackColor, colors.BorderStrong, Tokens.Opacity.Disabled);
        Assert.Equal(expected.ToArgb(), faded.ToArgb());
    });

    [Fact]
    public Task Designer_serializes_only_changed_values() => WinFormsUi.Run(() =>
    {
        using var toggle = new SwitchControl();
        var properties = TypeDescriptor.GetProperties(toggle);
        Assert.False(properties[nameof(SwitchControl.Tone)]!.ShouldSerializeValue(toggle));
        Assert.False(properties[nameof(SwitchControl.CheckedContent)]!.ShouldSerializeValue(toggle));
        toggle.Tone = StatusTone.Danger;
        toggle.CheckedContent = "开";
        Assert.True(properties[nameof(SwitchControl.Tone)]!.ShouldSerializeValue(toggle));
        Assert.True(properties[nameof(SwitchControl.CheckedContent)]!.ShouldSerializeValue(toggle));
        Assert.Equal("开", properties[nameof(SwitchControl.CheckedContent)]!.Converter.ConvertToString(toggle.CheckedContent));
    });
}
