using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Empty：图标（默认 Inbox，null 不显示）、标题、说明、操作居中排成一列；没给的不占位；内边距与尺寸按字号。</summary>
public class EmptyTests
{
    [Fact]
    public Task Icon_title_description_and_action_stack_centered() => WpfUi.Run(() =>
    {
        var full = new Empty { Title = "还没有订单", Description = "下单后会显示在这里。", Action = new Button { Content = "去逛逛" } };
        var bare = new Empty { Icon = null, Title = "没有找到", Size = TextSize.Sm };
        var host = new StackPanel { Width = 400, Children = { full, bare } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
        var em = full.FontSize;

        var icon = WpfUi.Descendants(full).OfType<Icon>().First();
        Assert.Equal((Lucide.Inbox, TextTone.Muted), (icon.Data, icon.Tone));
        Assert.Equal(2.5 * em, icon.ActualWidth, 3);
        var origin = icon.TranslatePoint(default, full);
        Assert.Equal(200, origin.X + icon.ActualWidth / 2, 1); // 居中
        Assert.Equal(1.5 * em, origin.Y, 1); // 上内边距 space.xl
        Assert.Equal(TextWeight.Medium, WpfUi.Descendants(full).OfType<Text>().First(t => t is not Paragraph && t.Text == "还没有订单").Weight);
        Assert.Equal(TextTone.Muted, WpfUi.Descendants(full).OfType<Paragraph>().Single().Tone);

        Assert.Equal(Visibility.Collapsed, WpfUi.Descendants(bare).OfType<Icon>().First().Visibility); // null 不显示图标
        Assert.Equal(Visibility.Collapsed, WpfUi.Descendants(bare).OfType<Paragraph>().Single().Visibility); // 没给的不占位
        Assert.Equal(13, bare.FontSize); // 字号档位 sm
    });
}
