namespace Rune.WinForms.Tests;

/// <summary>TableOfContentsControl：标题默认取语言包；当前节是顶边越过视口顶边的最后一节（滚到底时取视口里最后一节）；点目录项把那一节滚到顶上；按级别缩进。</summary>
public class TableOfContentsTests
{
    [Fact]
    public Task The_current_section_follows_scrolling_and_links_scroll_to_it() => WinFormsUi.Run(() =>
    {
        HeadingEntry[] headings = [new("a", "第一节", 2), new("b", "第二节", 3), new("c", "第三节", 2)];
        var article = new FlexControl { Direction = FlexDirection.Column, AutoSize = true };
        foreach (var heading in headings)
            article.Controls.AddRange([new Panel { Name = heading.Id, Height = 20 }, new Panel { Height = 200 }]);
        var scroll = new ScrollAreaControl { Content = article, Width = 300, Height = 150 };
        var toc = new TableOfContentsControl { Items = headings, ScrollRoot = scroll, Top = 160, Width = 200 };
        using var form = WinFormsUi.Show(scroll, toc);

        Assert.Equal(ProviderControl.LangOf(toc).TableOfContents, toc.AccessibilityObject.Name);
        Assert.Equal("a", toc.Current);
        var links = toc.Links.ToArray();
        Assert.True(links[0].AccessibilityObject.State.HasFlag(AccessibleStates.Selected));
        Assert.Equal(AccessibleRole.Link, links[0].AccessibilityObject.Role);

        links[1].AccessibilityObject.DoDefaultAction();
        Assert.Equal(220, scroll.ScrollOffset.Y);
        Assert.Equal("b", toc.Current);

        // 滚到底：第三节到不了顶边，取视口里的最后一节
        scroll.ScrollOffset = new Point(0, int.MaxValue);
        Assert.Equal("c", toc.Current);
    });
}
