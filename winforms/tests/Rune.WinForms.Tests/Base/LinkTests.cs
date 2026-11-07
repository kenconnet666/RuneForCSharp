namespace Rune.WinForms.Tests;

/// <summary>LinkControl：能聚焦，Enter、点击都先 Click 再打开（不可用时不打开）；读屏里是链接，外部链接补"在新窗口打开"。</summary>
public class LinkTests
{
    sealed class RecordingLinkControl : LinkControl
    {
        public List<Uri> Opened { get; } = [];

        protected override void Navigate(Uri href) => Opened.Add(href);
    }

    [Upstream("Link", "普通链接")]
    [Upstream("Link", "外部链接")]
    [Fact]
    public Task Opens_with_the_system_and_external_links_say_so() => WinFormsUi.Run(() =>
    {
        var clicks = 0;
        var link = new RecordingLinkControl { Text = "文档", Href = new Uri("https://example.com/docs") };
        var external = new RecordingLinkControl { Text = "Vue 文档", Href = new Uri("https://vuejs.org"), External = true };
        link.Click += (_, _) => clicks++;
        using var form = WinFormsUi.Show(link, external);

        Assert.True(link.CanSelect);
        link.PerformClick();
        Assert.Equal((1, new Uri("https://example.com/docs")), (clicks, Assert.Single(link.Opened)));
        Assert.Equal(AccessibleRole.Link, external.AccessibilityObject.Role);
        Assert.EndsWith(RuneLang.Current.Value.LinkNewWindow, external.AccessibilityObject.Name);
        Assert.True(external.Width > new RecordingLinkControl { Text = "Vue 文档" }.GetPreferredSize(default).Width); // 外链图标占了位置

        link.Enabled = false;
        link.PerformClick();
        Assert.Single(link.Opened);
    });

    [Fact]
    public Task Baseline_lines_up_with_text_in_a_baseline_flex() => WinFormsUi.Run(() =>
    {
        var before = new TextControl { Text = "放在句子中间，" };
        var link = new LinkControl { Text = "查看设计规范", External = true };
        var row = new FlexControl { Align = FlexAlign.Baseline, Dock = DockStyle.Top };
        row.Controls.AddRange([before, link]);
        using var form = WinFormsUi.Show(row);

        int Baseline(Control control) => control.Top + ((IBaseline)control).Baseline!.Value;
        Assert.Equal(Baseline(before), Baseline(link)); // 外链图标比文字高时，链接的文字也落在同一条基线上
    });
}
