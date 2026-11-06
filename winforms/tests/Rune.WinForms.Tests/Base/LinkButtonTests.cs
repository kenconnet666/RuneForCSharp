namespace Rune.WinForms.Tests;

/// <summary>
/// LinkButtonControl、IconLinkButtonControl：外观同按钮，点击先 Click 再打开链接（加载中不打开），读屏是链接，
/// 外部链接显示外链图标（写了 IconEnd 时用写的）、名称后补"在新窗口打开"。
/// </summary>
public class LinkButtonTests
{
    sealed class RecordingLinkButtonControl : LinkButtonControl
    {
        public List<Uri> Opened { get; } = [];

        protected override void Navigate(Uri href) => Opened.Add(href);
    }

    [Fact]
    public Task Clicking_runs_click_then_opens_the_link() => WinFormsUi.Run(() =>
    {
        var clicks = 0;
        var link = new RecordingLinkButtonControl { Text = "文档", Href = new Uri("https://example.com/docs") };
        link.Click += (_, _) => clicks++;
        using var form = WinFormsUi.Show(link);

        Assert.Equal((ButtonVariant.Solid, StatusTone.Accent), (link.Variant, link.Tone));
        link.PerformClick();
        Assert.Equal((1, new Uri("https://example.com/docs")), (clicks, Assert.Single(link.Opened)));

        link.IsLoading = true;
        link.PerformClick();
        Assert.Single(link.Opened);
    });

    [Fact]
    public Task External_links_show_the_icon_and_tell_screen_readers() => WinFormsUi.Run(() =>
    {
        var external = new LinkButtonControl { Text = "Vue 文档", External = true };
        var plain = new LinkButtonControl { Text = "Vue 文档" };
        var icon = new IconLinkButtonControl { Icon = Lucide.Rss, External = true, AccessibleName = "订阅" };
        using var form = WinFormsUi.Show(external, plain, icon);
        var lang = RuneLang.Current.Peek();
        try
        {
            RuneLang.Current.Value = Lang.ZhCn;
            Assert.Equal((AccessibleRole.Link, "Vue 文档（在新窗口打开）"), (external.AccessibilityObject.Role, external.AccessibilityObject.Name));
            Assert.Equal("订阅（在新窗口打开）", icon.AccessibilityObject.Name);
            // 外链图标占了位置：同样的文字，外部链接更宽
            Assert.True(external.Width > plain.Width);
            Assert.Null(external.IconEnd);
        }
        finally
        {
            RuneLang.Current.Value = lang;
        }
    });
}
