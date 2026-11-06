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
        var side = (int)MathF.Ceiling(36 * scale); // 控件高度：lg = 16px → 36px
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

    [Upstream("IconButton", "IconButton：名称来自 aria-label")]

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

    [Upstream("CloseButton", "CloseButton、ClearButton")]

    [Fact]
    public Task Presets_take_their_icon_and_name_from_the_language_pack() => WinFormsUi.Run(() =>
    {
        var close = new CloseButtonControl();
        var clear = new ClearButtonControl();
        using var form = WinFormsUi.Show(close, clear);
        var lang = RuneLang.Current.Peek();
        try
        {
            RuneLang.Current.Value = Lang.ZhCn;
            Assert.Equal((Lucide.X, "关闭"), (close.Icon, close.AccessibilityObject.Name));
            RuneLang.Current.Value = Lang.EnUs;
            Assert.Equal((Lucide.CircleX, "Clear"), (clear.Icon, clear.AccessibilityObject.Name));
            close.AccessibleName = "关闭提示";
            Assert.Equal("关闭提示", close.AccessibilityObject.Name);
            // 预设的图标是默认值，设计器不写
            Assert.False(TypeDescriptor.GetProperties(close)[nameof(CloseButtonControl.Icon)]!.ShouldSerializeValue(close));
        }
        finally
        {
            RuneLang.Current.Value = lang;
        }
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

    [Fact]
    public Task Control_heights_sit_on_a_4px_grid() => WinFormsUi.Run(() =>
    {
        // vue-tsx-pro 0.11：字号的 2.25 倍（xs 档 2 倍）取整到 4px → xs–xl 依次 24 / 28 / 32 / 36 / 40
        var buttons = new[] { TextSize.Xs, TextSize.Sm, TextSize.Md, TextSize.Lg, TextSize.Xl }
            .Select(size => new IconButtonControl { Icon = Lucide.Settings, TextSize = size }).ToArray();
        using var form = WinFormsUi.Show(buttons);
        var scale = buttons[0].DeviceDpi / 96f;

        Assert.Equal(new[] { 24, 28, 32, 36, 40 }.Select(side => (int)MathF.Ceiling(side * scale)), buttons.Select(button => button.Width - button.Padding.Horizontal));
    });
}
