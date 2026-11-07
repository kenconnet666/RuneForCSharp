using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>TableOfContents：标题默认取语言包；当前节是顶边越过视口顶边的最后一节（滚到底时取视口里最后一节）；点目录项把那一节滚到顶上；按级别缩进。</summary>
public class TableOfContentsTests
{
    static (TableOfContents Toc, ScrollViewer Scroll) Build()
    {
        HeadingEntry[] headings = [new("a", "第一节", 2), new("b", "第二节", 3), new("c", "第三节", 2)];
        var article = new StackPanel();
        foreach (var heading in headings)
            article.Children.AddRange([new TextBlock { Name = heading.Id, Text = heading.Label, Height = 20 }, new Border { Height = 200 }]);
        var scroll = new ScrollArea { Height = 150, Content = article };
        var toc = new TableOfContents { Items = headings, ScrollRoot = scroll };
        return (toc, scroll);
    }

    [Fact]
    public Task The_current_section_follows_scrolling_and_links_scroll_to_it() => HeadlessUi.Run(() =>
    {
        var (toc, scroll) = Build();
        var window = HeadlessUi.Show(new StackPanel { Children = { scroll, toc } });

        Assert.Equal(Provider.GetLang(toc).TableOfContents, toc.Peer().GetName());
        Assert.Equal("a", toc.Current);
        var links = toc.Links.ToArray();
        Assert.Equal("当前位置", links[0].Peer().GetItemStatus());
        Assert.Equal(AutomationControlType.Hyperlink, links[0].Peer().GetAutomationControlType());
        Assert.True(links[1].Padding.Left > links[0].Padding.Left); // 三级标题多缩进一档

        ((IInvokeProvider)links[1].Peer()).Invoke();
        HeadlessUi.Flush(window);
        Assert.Equal(220, scroll.Offset.Y);
        Assert.Equal("b", toc.Current);

        // 滚到底：第三节到不了顶边，取视口里的最后一节
        scroll.Offset = new Vector(0, scroll.Extent.Height);
        HeadlessUi.Flush(window);
        Assert.Equal("c", toc.Current);
    });
}
