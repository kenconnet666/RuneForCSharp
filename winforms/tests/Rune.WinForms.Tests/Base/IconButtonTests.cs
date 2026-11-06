using System.ComponentModel;

namespace Rune.WinForms.Tests;

/// <summary>
/// IconButton：宽高相等（max(2.25em, 24px)）、默认 ghost 中性色且悬停变色、读屏名称来自 AccessibleName、设计器按自己的默认值判断改没改；
/// 链接样式的 Button 保留上下 0.25em 的内边距。
/// </summary>
public class IconButtonTests
{
    static Color Pixel(Control control, int x, int y)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap.GetPixel(x, y);
    }

    [Fact]
    public Task Icon_buttons_are_square_ghost_and_neutral() => WinFormsUi.Run(() =>
    {
        var large = new IconButtonControl { Icon = Lucide.Settings, TextSize = TextSize.Lg };
        var small = new IconButtonControl { Icon = Lucide.Settings, TextSize = new TextSize("8") };
        using var form = WinFormsUi.Show(large, small);
        var scale = large.DeviceDpi / 96f;

        Assert.Equal((ButtonVariant.Ghost, StatusTone.Neutral), (large.Variant, large.Tone));
        var side = (int)MathF.Ceiling(36 * scale); // max(2.25em, 24px)，lg = 16px
        Assert.Equal((side, side), (large.Width - large.Padding.Horizontal, large.Height - large.Padding.Vertical));
        Assert.Equal((int)MathF.Ceiling(24 * scale), small.Width - small.Padding.Horizontal); // 8px 的 2.25em 不到 24px
        Assert.Equal(large.Element.IconPart!.Bounds.Width, large.Element.IconPart.Bounds.Height);

        // 左边内边距里、竖直居中的一点：平时透明（窗体底色），悬停时是 fillHover
        var x = large.Padding.Left + large.LogicalToDeviceUnits(3);
        Assert.Equal(form.BackColor.ToArgb(), Pixel(large, x, large.Height / 2).ToArgb());
        large.Element.IsHovered = true;
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(250));
        Assert.Equal(BuiltinThemes.Light.Colors.FillHover.ToArgb(), Pixel(large, x, large.Height / 2).ToArgb());
    });

    [Fact]
    public Task The_accessible_name_comes_from_accessible_name() => WinFormsUi.Run(() =>
    {
        var clicks = 0;
        var button = new IconButtonControl { Icon = Lucide.Trash, Tone = StatusTone.Danger, AccessibleName = "删除" };
        button.Click += (_, _) => clicks++;
        using var form = WinFormsUi.Show(button);

        Assert.Equal((AccessibleRole.PushButton, "删除"), (button.AccessibilityObject.Role, button.AccessibilityObject.Name));
        button.AccessibilityObject.DoDefaultAction();
        Assert.Equal(1, clicks);
    });

    [Fact]
    public Task Designer_compares_with_the_icon_button_defaults() => WinFormsUi.Run(() =>
    {
        using var button = new IconButtonControl();
        var properties = TypeDescriptor.GetProperties(button);
        Assert.False(properties[nameof(IconButtonControl.Variant)]!.ShouldSerializeValue(button));
        Assert.False(properties[nameof(IconButtonControl.Tone)]!.ShouldSerializeValue(button));
        button.Variant = ButtonVariant.Solid;
        Assert.True(properties[nameof(IconButtonControl.Variant)]!.ShouldSerializeValue(button));
    });

    [Fact]
    public Task Link_buttons_keep_the_block_padding() => WinFormsUi.Run(() =>
    {
        var link = new ButtonControl { Text = "查看文档", Variant = ButtonVariant.Link, TextSize = TextSize.Lg };
        using var form = WinFormsUi.Show(link);
        var scale = link.DeviceDpi / 96f;
        var font = Fonts.Get(link.Font.FontFamily.Name, 12, link.Font.Style & ~FontStyle.Bold);
        var line = TextRenderer.MeasureText("Ag", font, default(System.Drawing.Size), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height;

        // TS 的 link 只去掉左右内边距与最小高度：上下 0.25em 加描边保留
        Assert.InRange(link.Height - link.Padding.Vertical - (line + 2 * (4 + 1) * scale), -1, 1);
    });
}
