namespace Rune.WinForms.Tests;

/// <summary>LinkControl：能聚焦，Enter、点击都先 Click 再打开（不可用时不打开）；读屏里是链接，外部链接补"在新窗口打开"。</summary>
public class LinkTests
{
    sealed class RecordingLinkControl : LinkControl
    {
        public List<Uri> Opened { get; } = [];

        protected override void Navigate(Uri href) => Opened.Add(href);
    }

    /// <summary>画出来的“墨量”：各像素（1 - 亮度）之和，颜色越深、画得越多越大。</summary>
    static float Ink(LinkControl link)
    {
        using var bitmap = new Bitmap(link.Width, link.Height);
        link.DrawToBitmap(bitmap, new Rectangle(Point.Empty, link.Size));
        var ink = 0f;
        for (var x = 0; x < bitmap.Width; x++)
            for (var y = 0; y < bitmap.Height; y++)
                ink += 1 - bitmap.GetPixel(x, y).GetBrightness();
        return ink;
    }

    [Fact]
    public Task Underline_turns_solid_and_muted_links_turn_to_text_on_hover() => WinFormsUi.Run(() =>
    {
        var link = new LinkControl { Text = "文档" };
        var muted = new LinkControl { Text = "帮助中心", Tone = TextTone.Muted, Underline = LinkUnderline.None };
        using var form = WinFormsUi.Show(link, muted);

        Assert.True(link.Element.UnderlineShown);
        var (plainLink, plainMuted) = (Ink(link), Ink(muted));
        link.Element.IsHovered = muted.Element.IsHovered = true;
        Assert.True(Ink(link) > plainLink); // 下划线平时半透明，悬停时实色
        Assert.True(Ink(muted) > plainMuted); // 次要链接悬停换成正文色
    });

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

    [Fact]
    [Upstream("Link", "下划线：容器（LinkUnderlineScope、卡片页头）给的默认是 hover")]
    public Task Containers_give_hover_underline_unless_the_link_says_otherwise() => WinFormsUi.Run(() =>
    {
        var body = new LinkControl { Text = "正文" };
        var scoped = new LinkControl { Text = "容器里" };
        var explicitAlways = new LinkControl { Text = "容器里显式", Underline = LinkUnderline.Always };
        var scope = new FlexControl();
        scope.Controls.AddRange([scoped, explicitAlways]);
        LinkControl.SetUnderlineScope(scope, LinkUnderline.Hover);
        var extra = new LinkControl { Text = "全部" };
        var cardBody = new LinkControl { Text = "卡片正文" };
        var card = new CardControl { Header = "最近订单", Extra = extra };
        card.Controls.Add(cardBody);
        using var form = WinFormsUi.Show(body, scope, card);

        Assert.Equal(LinkUnderline.Always, body.Underline);
        Assert.Equal(LinkUnderline.Hover, scoped.Underline); // 容器给 hover
        Assert.Equal(LinkUnderline.Always, explicitAlways.Underline); // 显式写的优先
        Assert.Equal(LinkUnderline.Hover, extra.Underline); // 卡片页头的操作
        Assert.Equal(LinkUnderline.Always, cardBody.Underline); // 卡片正文照旧
        card.Extra = null;
        Assert.Equal(LinkUnderline.Always, extra.Underline); // 拿出页头就不算了
    });
}
