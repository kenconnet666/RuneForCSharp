using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Rune.Wpf.Tests;

/// <summary>Avatar：首字母、读屏的名称与装饰、图片与后备内容、soft / solid 的配色、边长与圆角。</summary>
public class AvatarTests
{
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    static StackPanel Themed(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    [Upstream("Avatar", "initials(%s) = %s")]
    [Theory]
    [InlineData("张三", "三")]
    [InlineData("Ada Lovelace", "AL")]
    [InlineData("grace", "G")]
    [InlineData("  ", "")]
    public void Initials_take_the_given_name_or_first_and_last_words(string name, string expected) => Assert.Equal(expected, Avatar.Initials(name));

    [Upstream("Avatar", "role=")]
    [Fact]
    public Task Named_avatars_are_images_and_anonymous_ones_are_decorative() => WpfUi.Run(() =>
    {
        var named = new Avatar { DisplayName = "张三" };
        var anonymous = new Avatar();
        var photo = new Avatar { DisplayName = "Ada Lovelace", Source = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, new byte[64], 16) };
        WpfUi.Show(Themed(named, anonymous, photo));

        var peer = UIElementAutomationPeer.CreatePeerForElement(named);
        Assert.Equal((AutomationControlType.Image, "张三", true), (peer.GetAutomationControlType(), peer.GetName(), peer.IsControlElement()));
        Assert.Equal("三", Descendants(named).OfType<TextBlock>().Single().Text);
        Assert.False(UIElementAutomationPeer.CreatePeerForElement(anonymous).IsControlElement()); // 没有名字：装饰
        Assert.Single(Descendants(anonymous).OfType<Icon>()); // 后备内容是人像图标
        Assert.False(photo.FallbackShown); // 有图片时显示图片
        photo.Source = null;
        Assert.True(photo.FallbackShown);
        WpfUi.Flush();
        Assert.Equal("AL", Descendants(photo).OfType<TextBlock>().Single().Text);
    });

    [Upstream("Avatar", "variant × tone 用状态色调的配色")]
    [Fact]
    public Task Soft_accent_by_default_and_solid_tones() => WpfUi.Run(() =>
    {
        var plain = new Avatar { DisplayName = "张三" };
        var solid = new Avatar { DisplayName = "张三", Variant = AvatarVariant.Solid, Tone = StatusTone.Success, Size = TextSize.Lg, Radius = Radius.Md };
        var host = Themed(plain, solid);
        WpfUi.Show(host);
        Color Brush(string key) => ((SolidColorBrush)host.FindResource(key)).Color;

        Assert.Equal(Brush("Rune.Brush.AccentSoft"), ((SolidColorBrush)plain.Background).Color);
        Assert.Equal(Brush("Rune.Brush.AccentText"), ((SolidColorBrush)plain.Foreground).Color);
        Assert.Equal(Brush("Rune.Brush.Success"), ((SolidColorBrush)solid.Background).Color);
        Assert.Equal(Brush("Rune.Brush.SuccessContrast"), ((SolidColorBrush)solid.Foreground).Color);
        // 边长 2.5em
        Assert.Equal(new Size(2.5 * plain.FontSize, 2.5 * plain.FontSize), plain.RenderSize);
        Assert.Equal(40, solid.RenderSize.Width);
    });
}
