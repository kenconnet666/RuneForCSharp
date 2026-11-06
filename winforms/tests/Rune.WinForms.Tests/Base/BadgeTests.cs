using System.ComponentModel;

namespace Rune.WinForms.Tests;

/// <summary>
/// Badge：默认值（不写进设计器代码）、样式 × 色调取当前主题的颜色并随主题重画、图标部件逐字段合并、
/// 尺寸按 em、读屏读成静态文字。
/// </summary>
public class BadgeTests
{
    static Color Pixel(Control control, int x, int y)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap.GetPixel(x, y);
    }

    [Fact]
    public Task Defaults_and_designer_serialization() => WinFormsUi.Run(() =>
    {
        using var badge = new BadgeControl { Text = "草稿" };
        Assert.Equal((StatusVariant.Soft, StatusTone.Neutral, TextSize.Xs, Radius.Sm), (badge.Variant, badge.Tone, badge.TextSize, badge.Radius));
        var properties = TypeDescriptor.GetProperties(badge);
        Assert.False(properties[nameof(BadgeControl.Variant)]!.ShouldSerializeValue(badge));
        badge.Variant = StatusVariant.Solid;
        Assert.True(properties[nameof(BadgeControl.Variant)]!.ShouldSerializeValue(badge));
    });

    [Fact]
    public Task Colors_follow_variant_tone_and_theme() => WinFormsUi.Run(() =>
    {
        var badge = new BadgeControl { Text = "12", Variant = StatusVariant.Solid, Tone = StatusTone.Danger };
        using var form = WinFormsUi.Show(badge);
        try
        {
            // 左边内边距里的一点：只有底色
            var inside = (badge.Width / 10, badge.Height / 2);
            Assert.Equal(BuiltinThemes.Light.Colors.Danger.ToArgb(), Pixel(badge, inside.Item1, inside.Item2).ToArgb());
            RuneTheme.Current.Value = BuiltinThemes.Dark;
            WinFormsUi.Pump();
            Assert.Equal(BuiltinThemes.Dark.Colors.Danger.ToArgb(), Pixel(badge, inside.Item1, inside.Item2).ToArgb());
        }
        finally
        {
            RuneTheme.Current.Value = BuiltinThemes.Light;
        }
    });

    [Fact]
    public Task Icon_part_merges_field_by_field_and_takes_the_dot_place() => WinFormsUi.Run(() =>
    {
        using var badge = new BadgeControl { Text = "已通过", Icon = Lucide.CircleCheck, Dot = true, PropsIcon = new IconProps { StrokeWidth = 3 } };
        var icon = badge.Element.IconPart!;
        Assert.Equal(Lucide.CircleCheck, icon.Data);
        Assert.Equal(3, icon.StrokeWidth);

        // 没有图标时才画圆点：圆点占 0.5em + 0.25em 的宽度
        using var plain = new BadgeControl { Text = "运行中" };
        using var dotted = new BadgeControl { Text = "运行中", Dot = true };
        var em = 12f * plain.DeviceDpi / 96;
        Assert.InRange(dotted.PreferredSize.Width - plain.PreferredSize.Width, (int)(0.75f * em) - 1, (int)(0.75f * em) + 1);
    });

    [Fact]
    public Task Sizes_are_in_em_and_screen_readers_see_static_text() => WinFormsUi.Run(() =>
    {
        var badge = new BadgeControl { Text = "已发货", TextSize = TextSize.Lg };
        using var form = WinFormsUi.Show(badge);

        Assert.True(badge.Height >= 1.75f * 16 * badge.DeviceDpi / 96 - 1);
        Assert.Equal(AccessibleRole.StaticText, badge.AccessibilityObject.Role);
        Assert.Equal("已发货", badge.AccessibilityObject.Name);
    });
}
