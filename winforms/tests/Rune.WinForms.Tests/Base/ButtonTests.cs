using System.ComponentModel;

namespace Rune.WinForms.Tests;

/// <summary>
/// Button：生成的配方给颜色、悬停在 durations.sm 内过渡到悬停色、尺寸按 em、加载中点击无效且指示防闪烁、
/// 波纹扩散后淡出移除、读屏是按钮、设计器只写改过的属性。
/// </summary>
public class ButtonTests
{
    static Color Pixel(Control control, int x, int y)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap.GetPixel(x, y);
    }

    /// <summary>按钮左边内边距里、竖直居中的一点：只有底色（圆角在上下两端，这里是直边）。</summary>
    static Color Fill(ButtonControl button) => Pixel(button, button.Padding.Left + button.LogicalToDeviceUnits(4), button.Height / 2);

    [Fact]
    public Task Recipe_colors_and_hover_transition() => WinFormsUi.Run(() =>
    {
        var button = new ButtonControl { Text = "确定" };
        using var form = WinFormsUi.Show(button);
        var colors = BuiltinThemes.Light.Colors;

        Assert.Equal(colors.Accent.ToArgb(), Fill(button).ToArgb());
        button.Element.IsHovered = true;
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(250));
        Assert.Equal(colors.AccentHover.ToArgb(), Fill(button).ToArgb());

        button.Variant = ButtonVariant.Soft;
        button.Tone = StatusTone.Danger;
        button.Element.IsHovered = false;
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(250));
        Assert.Equal(colors.DangerSoft.ToArgb(), Fill(button).ToArgb());
    });

    [Fact]
    public Task Sizes_are_em() => WinFormsUi.Run(() =>
    {
        var button = new ButtonControl { Text = "lg", TextSize = TextSize.Lg };
        using var form = WinFormsUi.Show(button);
        var scale = button.DeviceDpi / 96f;

        // 最小高度 max(2.25em, 24px)，加上上下各 4 像素的焦点环留白
        Assert.True(button.Height >= (int)(2.25f * 16 * scale) + 2 * button.Padding.Top - 1);
    });

    [Fact]
    public Task Loading_blocks_clicks_and_shows_the_spinner_after_a_delay() => WinFormsUi.Run(() =>
    {
        var clicks = 0;
        var button = new ButtonControl { Text = "保存" };
        button.Click += (_, _) => clicks++;
        using var form = WinFormsUi.Show(button);

        button.IsLoading = true;
        button.PerformClick();
        Assert.Equal(0, clicks);
        Assert.False(button.Element.ShowsSpinner); // 200 毫秒内不出现

        WinFormsUi.Wait(TimeSpan.FromMilliseconds(350));
        Assert.True(button.Element.ShowsSpinner);
        Assert.True(button.AccessibilityObject.State.HasFlag(AccessibleStates.Busy));

        button.IsLoading = false;
        button.PerformClick();
        Assert.Equal(1, clicks);
        Assert.True(button.Element.ShowsSpinner); // 出现了至少显示 0.5 秒
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(600));
        Assert.False(button.Element.ShowsSpinner);
    });

    [Fact]
    public Task Ripples_expand_and_fade_out() => WinFormsUi.Run(() =>
    {
        var button = new ButtonControl { Text = "发布" };
        using var form = WinFormsUi.Show(button);

        var release = button.Element.StartRipple(new PointF(5, 5));
        Assert.Equal(1, button.Element.RippleCount);
        release();
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(400));
        WinFormsUi.Paint(button); // 淡出完的在绘制时移除
        Assert.Equal(0, button.Element.RippleCount);

        button.Ripple = false;
        Assert.False(button.Element.RippleOn);
    });

    [Fact]
    public Task Screen_readers_see_a_push_button() => WinFormsUi.Run(() =>
    {
        var clicks = 0;
        var button = new ButtonControl { Text = "发布" };
        button.Click += (_, _) => clicks++;
        using var form = WinFormsUi.Show(button);

        Assert.Equal(AccessibleRole.PushButton, button.AccessibilityObject.Role);
        Assert.Equal("发布", button.AccessibilityObject.Name);
        button.AccessibilityObject.DoDefaultAction();
        Assert.Equal(1, clicks);
        Assert.True(button.CanSelect);
    });

    [Fact]
    public Task Designer_serializes_only_changed_values() => WinFormsUi.Run(() =>
    {
        using var button = new ButtonControl();
        var properties = TypeDescriptor.GetProperties(button);
        Assert.False(properties[nameof(ButtonControl.Variant)]!.ShouldSerializeValue(button));
        Assert.False(properties[nameof(ButtonControl.Tone)]!.ShouldSerializeValue(button));
        button.Variant = ButtonVariant.Outline;
        Assert.True(properties[nameof(ButtonControl.Variant)]!.ShouldSerializeValue(button));
    });
}
