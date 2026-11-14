using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Avatar：首字母、读屏的名称与装饰、图片与后备内容、soft / solid 的配色、边长与圆角。</summary>
public class AvatarTests
{
    [Upstream("Avatar", "initials(%s) = %s")]
    [Theory]
    [InlineData("张三", "三")]
    [InlineData("Ada Lovelace", "AL")]
    [InlineData("grace", "G")]
    [InlineData("  ", "")]
    [InlineData("😀 Smith", "😀S")] // 按码点取：表情是两个码元
    public void Initials_take_the_given_name_or_first_and_last_words(string name, string expected) => Assert.Equal(expected, Avatar.Initials(name));

    [Upstream("Avatar", "role=")]
    [Fact]
    public Task Named_avatars_are_images_and_anonymous_ones_are_decorative() => HeadlessUi.Run(() =>
    {
        var named = new Avatar { DisplayName = "张三" };
        var anonymous = new Avatar();
        var photo = new Avatar { DisplayName = "Ada Lovelace", Source = new WriteableBitmap(new PixelSize(4, 4), new Vector(96, 96), PixelFormats.Bgra8888) };
        HeadlessUi.Show(new StackPanel { Children = { named, anonymous, photo } });

        var peer = ControlAutomationPeer.CreatePeerForElement(named);
        Assert.Equal((AutomationControlType.Image, "张三", true), (peer.GetAutomationControlType(), peer.GetName(), peer.IsControlElement()));
        Assert.Equal("三", named.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.False(ControlAutomationPeer.CreatePeerForElement(anonymous).IsControlElement()); // 没有名字：装饰
        named.Decorative = true; // 名字写在旁边：只是装饰，首字母照旧
        Assert.False(peer.IsControlElement());
        Assert.Equal("三", named.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        named.Decorative = false;
        Assert.Single(anonymous.GetVisualDescendants().OfType<Icon>()); // 后备内容是人像图标
        Assert.False(photo.FallbackShown); // 有图片时显示图片
        photo.Source = null;
        Assert.True(photo.FallbackShown);
        Assert.Equal("AL", photo.GetVisualDescendants().OfType<TextBlock>().Single(t => t.IsEffectivelyVisible).Text);
    });

    [Upstream("Avatar", "variant × tone 用状态色调的配色")]
    [Fact]
    public Task Soft_accent_by_default_and_solid_tones() => HeadlessUi.Run(() =>
    {
        var plain = new Avatar { DisplayName = "张三" };
        var solid = new Avatar { DisplayName = "张三", Variant = AvatarVariant.Solid, Tone = StatusTone.Success, Size = TextSize.Lg, Radius = Radius.Md };
        HeadlessUi.Show(new StackPanel { Children = { plain, solid } });
        static Border Root(Avatar avatar) => avatar.GetVisualChildren().OfType<Border>().Single();

        Assert.Equal(plain.Palette!.Soft, Root(plain).Background);
        Assert.Equal(plain.Palette.Text, plain.GetValue(TextElement.ForegroundProperty));
        Assert.Equal(solid.Palette!.Solid, Root(solid).Background);
        Assert.Equal(solid.Palette.Contrast, solid.GetValue(TextElement.ForegroundProperty));
        // 边长 2.5em；默认圆形，md 是圆角方形
        Assert.Equal(new Size(2.5 * plain.FontSize, 2.5 * plain.FontSize), plain.Bounds.Size);
        Assert.Equal(40, solid.Bounds.Width);
        Assert.Equal(1.25 * plain.FontSize, Root(plain).CornerRadius.TopLeft, 3);
        Assert.True(Root(solid).CornerRadius.TopLeft < 20);
    });
}
