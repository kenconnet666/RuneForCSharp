namespace Rune.WinForms.Tests;

/// <summary>PageHeaderControl：标题按级别是 H1–H3；订阅了 Back 才有返回按钮（名称取语言包），点了发 Back；给了 BackHref 时是链接按钮。</summary>
public class PageHeaderTests
{
    [Fact]
    public Task Back_appears_with_a_handler_and_raises_back() => WinFormsUi.Run(() =>
    {
        var header = new PageHeaderControl { Title = "采购单", Width = 600 };
        using var form = WinFormsUi.Show(header);
        Assert.IsType<H1Control>(header.Parts.Title);
        Assert.Null(header.Parts.Back);

        var backs = 0;
        header.Back += (_, _) => backs++;
        var back = Assert.IsType<IconButtonControl>(header.Parts.Back);
        Assert.Equal(PatternsMessages.For(ProviderControl.LangOf(header)).PageHeaderBack, back.AccessibleName);
        back.PerformClick();
        Assert.Equal(1, backs);

        header.BackHref = new Uri("https://example.com/orders");
        Assert.IsType<IconLinkButtonControl>(header.Parts.Back);
        header.HeadingLevel = 2;
        Assert.IsType<H2Control>(header.Parts.Title);
        Assert.Equal("采购单", header.Parts.Title.Text);
    });
}
