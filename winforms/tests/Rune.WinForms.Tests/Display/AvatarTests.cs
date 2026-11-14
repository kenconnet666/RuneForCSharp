namespace Rune.WinForms.Tests;

/// <summary>AvatarControl：首字母、读屏的名称与装饰、soft / solid 的配色、边长。</summary>
public class AvatarTests
{
    [Upstream("Avatar", "initials(%s) = %s")]
    [Theory]
    [InlineData("张三", "三")]
    [InlineData("Ada Lovelace", "AL")]
    [InlineData("grace", "G")]
    [InlineData("  ", "")]
    [InlineData("😀 Smith", "😀S")] // 按码点取：表情是两个码元
    public void Initials_take_the_given_name_or_first_and_last_words(string name, string expected) => Assert.Equal(expected, AvatarInitials.Of(name));

    [Upstream("Avatar", "role=")]
    [Fact]
    public Task Named_avatars_are_graphics_and_anonymous_ones_are_decorative() => WinFormsUi.Run(() =>
    {
        var named = new AvatarControl { DisplayName = "张三" };
        var anonymous = new AvatarControl();
        using var form = WinFormsUi.Show(named, anonymous);
        var em = named.Font.SizeInPoints * named.DeviceDpi / 72f;

        Assert.Equal((AccessibleRole.Graphic, "张三"), (named.AccessibilityObject.Role, named.AccessibilityObject.Name));
        Assert.Equal("三", named.Element.FallbackText);
        Assert.Equal(AccessibleRole.None, anonymous.AccessibilityObject.Role); // 没有名字：装饰
        named.Decorative = true; // 名字写在旁边：只是装饰，首字母照旧
        Assert.Equal(AccessibleRole.None, named.AccessibilityObject.Role);
        Assert.Equal("三", named.Element.FallbackText);
        named.Decorative = false;
        Assert.Equal("", anonymous.Element.FallbackText); // 画人像图标
        Assert.Equal(new Size((int)MathF.Round(2.5f * em), (int)MathF.Round(2.5f * em)), named.Size);
    });

    [Upstream("Avatar", "variant × tone 用状态色调的配色")]
    [Fact]
    public Task Soft_accent_is_the_default() => WinFormsUi.Run(() =>
    {
        var avatar = new AvatarControl { DisplayName = "张三" };
        using var form = WinFormsUi.Show(avatar);

        Assert.Equal((AvatarVariant.Soft, StatusTone.Accent, Radius.Full), (avatar.Variant, avatar.Tone, avatar.Radius));
        // 画出来的底色是 accentSoft（中心附近、首字母以外的像素）
        using var bitmap = new Bitmap(avatar.Width, avatar.Height);
        avatar.DrawToBitmap(bitmap, new Rectangle(Point.Empty, avatar.Size));
        Assert.Equal(RuneTheme.Current.Value.Colors.AccentSoft.ToArgb(), bitmap.GetPixel(avatar.Width / 2, 2 + avatar.Height / 8).ToArgb());
    });
}
