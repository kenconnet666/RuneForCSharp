using System.Windows.Automation;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>PageHeader：标题按级别是 H1–H3；订阅了 Back 才有返回按钮（名称取语言包），点了发 Back；给了 BackHref 时是链接按钮。</summary>
public class PageHeaderTests
{
    [Fact]
    public Task Back_appears_with_a_handler_and_raises_back() => WpfUi.Run(() =>
    {
        var header = new PageHeader { Title = "采购单" };
        var host = new StackPanel { Children = { header } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
        Assert.IsType<H1>(header.TitlePart);
        Assert.Null(header.BackPart);

        var backs = 0;
        header.Back += (_, _) => backs++;
        var back = Assert.IsType<IconButton>(header.BackPart);
        Assert.Equal(PatternsMessages.For(Provider.GetLang(header)).PageHeaderBack, AutomationProperties.GetName(back));
        back.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.Equal(1, backs);

        header.BackHref = new Uri("https://example.com/orders");
        Assert.IsType<IconLinkButton>(header.BackPart);
        header.HeadingLevel = 2;
        Assert.IsType<H2>(header.TitlePart);
        Assert.Equal("采购单", header.TitlePart.Text);
    });
}
