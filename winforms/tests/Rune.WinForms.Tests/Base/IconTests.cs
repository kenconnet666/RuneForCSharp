using System.ComponentModel;
using System.ComponentModel.Design.Serialization;

namespace Rune.WinForms.Tests;

/// <summary>
/// Icon 元素与 IconControl：大小跟随字体或档位并按 DPI 换算、色调取当前主题且只有读主题的图标换主题时重画、
/// 从右到左默认不翻转、读屏当作图片、设计器能按名字选 Lucide 图标并生成 <c>Lucide.X</c>。
/// </summary>
public class IconTests
{
    static int Dip(float dip, Control control) => (int)MathF.Ceiling(dip * control.DeviceDpi / 96f);

    static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    [Fact]
    public Task Size_follows_the_font_a_step_or_pixels() => WinFormsUi.Run(() =>
    {
        var inherit = new IconControl { Data = Lucide.Search };
        inherit.Font = new Font(inherit.Font.FontFamily, 15f);
        var step = new IconControl { Data = Lucide.Search, IconSize = TextSize.Lg };
        var pixels = new IconControl { Data = Lucide.Search, IconSize = 24 };
        using var form = WinFormsUi.Show(inherit, step, pixels);

        var em = (int)MathF.Ceiling(15f * inherit.DeviceDpi / 72f);
        Assert.Equal(new Size(em, em), inherit.Size);
        Assert.Equal(new Size(Dip(16, step), Dip(16, step)), step.Size); // fontSizes.lg
        Assert.Equal(new Size(Dip(24, pixels), Dip(24, pixels)), pixels.Size);

        pixels.IconSize = 32;
        Assert.Equal(new Size(Dip(32, pixels), Dip(32, pixels)), pixels.Size);
    });

    [Fact]
    public Task Tone_colors_come_from_the_current_theme_and_default_follows_fore_color() => WinFormsUi.Run(() =>
    {
        // minus 是 24 格中线上的一条横线，正中的像素整个被线盖住，颜色不受抗锯齿影响
        var plain = new CountingIcon { Data = Lucide.Minus, IconSize = 48, ForeColor = Color.Purple };
        var danger = new CountingIcon { Data = Lucide.Minus, IconSize = 48, Tone = Tone.Danger, Left = 100 };
        using var form = WinFormsUi.Show(plain, danger);
        try
        {
            using (var bitmap = Render(plain)) Assert.Equal(Color.Purple.ToArgb(), bitmap.GetPixel(plain.Width / 2, plain.Height / 2).ToArgb());
            using (var bitmap = Render(danger)) Assert.Equal(BuiltinThemes.Light.Colors.DangerText.ToArgb(), bitmap.GetPixel(danger.Width / 2, danger.Height / 2).ToArgb());
            plain.Invalidations = danger.Invalidations = 0;

            RuneTheme.Current.Value = BuiltinThemes.Dark;
            WinFormsUi.Pump();

            // 只有读了主题的图标失效；跟随 ForeColor 的不读主题
            Assert.Equal(1, danger.Invalidations);
            Assert.Equal(0, plain.Invalidations);
            using (var bitmap = Render(danger)) Assert.Equal(BuiltinThemes.Dark.Colors.DangerText.ToArgb(), bitmap.GetPixel(danger.Width / 2, danger.Height / 2).ToArgb());
        }
        finally
        {
            RuneTheme.Current.Value = BuiltinThemes.Light;
        }
    });

    [Fact]
    public Task Icons_do_not_flip_in_right_to_left_unless_asked() => WinFormsUi.Run(() =>
    {
        // chevron-right 的尖在 (15, 12)：中间那一行的颜色在右半边，翻转后在左半边
        var plain = new IconControl { Data = Lucide.ChevronRight, IconSize = 48, ForeColor = Color.Purple, RightToLeft = RightToLeft.Yes };
        var flipped = new IconControl { Data = Lucide.ChevronRight, IconSize = 48, ForeColor = Color.Purple, RightToLeft = RightToLeft.Yes, FlipInRtl = true, Left = 100 };
        using var form = WinFormsUi.Show(plain, flipped);

        Assert.Equal((0, 1), Halves(plain));
        Assert.Equal((1, 0), Halves(flipped));

        static (int Left, int Right) Halves(Control icon)
        {
            using var bitmap = Render(icon);
            var row = icon.Height / 2;
            var columns = Enumerable.Range(0, icon.Width).Where(x => bitmap.GetPixel(x, row).ToArgb() == Color.Purple.ToArgb()).ToList();
            return (columns.Any(x => x < icon.Width / 2) ? 1 : 0, columns.Any(x => x >= icon.Width / 2) ? 1 : 0);
        }
    });

    [Fact]
    public Task Screen_readers_see_a_graphic_that_cannot_take_focus() => WinFormsUi.Run(() =>
    {
        var named = new IconControl { Data = Lucide.Trash, AccessibleName = "删除" };
        using var form = WinFormsUi.Show(named);

        Assert.Equal(AccessibleRole.Graphic, named.AccessibilityObject.Role);
        Assert.Equal("删除", named.AccessibilityObject.Name);
        Assert.False(named.CanSelect);
    });

    [Fact]
    public void Designer_picks_lucide_icons_by_name_and_writes_Lucide_members()
    {
        var converter = TypeDescriptor.GetConverter(typeof(IconData));
        Assert.IsType<IconDataConverter>(converter);
        Assert.Equal("Search", converter.ConvertToString(Lucide.Search));
        Assert.Same(Lucide.Search, converter.ConvertFromString("Search"));
        Assert.Throws<ArgumentException>(() => converter.ConvertFromString("NoSuchIcon"));

        var lucide = (InstanceDescriptor)converter.ConvertTo(Lucide.Search, typeof(InstanceDescriptor))!;
        Assert.Equal(typeof(Lucide).GetProperty(nameof(Lucide.Search)), lucide.MemberInfo);
        var custom = (InstanceDescriptor)converter.ConvertTo(new IconData("logo", "M4 4L20 20"), typeof(InstanceDescriptor))!;
        Assert.Equal("M4 4L20 20", ((IconData)custom.Invoke()!).Path);

        // 列出的是现名，改名留下的旧名不列
        var names = converter.GetStandardValues()!.Cast<IconData>().Select(icon => converter.ConvertToString(icon)).ToList();
        Assert.Contains("Search", names);
        Assert.DoesNotContain("Trash2", names);
    }

    [Fact]
    public Task Designer_serializes_only_changed_properties() => WinFormsUi.Run(() =>
    {
        using var icon = new IconControl();
        var properties = TypeDescriptor.GetProperties(icon);
        Assert.False(properties[nameof(IconControl.Tone)]!.ShouldSerializeValue(icon));
        Assert.False(properties[nameof(IconControl.IconSize)]!.ShouldSerializeValue(icon));
        Assert.False(properties[nameof(IconControl.StrokeWidth)]!.ShouldSerializeValue(icon));

        icon.Tone = Tone.Danger;
        icon.IconSize = TextSize.Lg;
        Assert.True(properties[nameof(IconControl.Tone)]!.ShouldSerializeValue(icon));
        Assert.True(properties[nameof(IconControl.IconSize)]!.ShouldSerializeValue(icon));
        Assert.Equal("danger", properties[nameof(IconControl.Tone)]!.Converter.ConvertToString(icon.Tone));
    });

    /// <summary>记录自己被失效了几次。</summary>
    sealed class CountingIcon : IconControl
    {
        // 字段而不是属性：控件的公开属性会被设计器序列化（WFO1000）
        public int Invalidations;

        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);
            Invalidations++;
        }
    }
}
