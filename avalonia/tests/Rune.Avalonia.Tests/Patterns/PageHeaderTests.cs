using Avalonia.Automation;

namespace Rune.Avalonia.Tests;

/// <summary>PageHeader：标题按级别是 H1–H3；订阅了 Back 才有返回按钮（名称取语言包），点了发 Back；给了 BackHref 时是链接按钮。</summary>
public class PageHeaderTests
{
    [Fact]
    public Task Back_appears_with_a_handler_and_raises_back() => HeadlessUi.Run(() =>
    {
        var header = new PageHeader { Title = "采购单" };
        HeadlessUi.Show(header);
        Assert.IsType<H1>(header.TitlePart);
        Assert.Null(header.BackPart);

        var backs = 0;
        header.Back += (_, _) => backs++;
        var back = Assert.IsType<IconButton>(header.BackPart);
        Assert.Equal(PatternsMessages.For(Provider.GetLang(header)).PageHeaderBack, AutomationProperties.GetName(back));
        back.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(1, backs);

        header.BackHref = new Uri("https://example.com/orders");
        Assert.IsType<IconLinkButton>(header.BackPart);
        header.HeadingLevel = 2;
        Assert.IsType<H2>(header.TitlePart);
        Assert.Equal("采购单", header.TitlePart.Text);
    });
}
