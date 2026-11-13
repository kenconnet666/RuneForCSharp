namespace Rune.WinForms.Tests;

/// <summary>BannerControl：标题与正文同一行、操作在后面、整行居中；订阅了 CloseRequested 才有关闭按钮，点了就请求移除（没有淡出）。</summary>
public class BannerTests
{
    [Fact]
    [Upstream("Banner", "标题加粗、与正文同一行；操作在后面；页面加载时就在的不带角色，live 时带")]
    public Task Title_and_text_share_a_centered_line_with_the_action_after() => WinFormsUi.Run(() =>
    {
        var notice = new ButtonControl { Text = "查看详情" };
        var first = new BannerControl { Icon = null, Title = "系统维护", Description = "今晚 22:00–23:00 暂停服务", Action = notice, Dock = DockStyle.Top };
        var second = new BannerControl { Live = AlertLive.Polite, Tone = StatusTone.Success, Description = "已恢复服务", Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(second, first);

        var (title, text) = first.TextBounds;
        Assert.Equal(title.Top + title.Height / 2, text.Top + text.Height / 2, 1f); // 同一行
        Assert.True(text.Left > title.Right && notice.Left > text.Right); // 标题、正文、操作依次排开
        Assert.InRange(title.Left - (first.Width - notice.Right), -1, 1); // 整行居中
        Assert.Null(first.ShownIcon); // 写 null 不显示图标
        Assert.Equal(("系统维护", "今晚 22:00–23:00 暂停服务"), (first.AccessibilityObject.Name, first.AccessibilityObject.Description));
        Assert.Equal(AlertLive.Off, first.Live);
        Assert.Null(first.CloseButton);
        Assert.Equal(Lucide.CircleCheck, second.ShownIcon);
    });

    [Fact]
    [Upstream("Banner", "关闭：先离场（等过渡结束）再调用 onClose；使用方没移除时重新出现")]
    public Task Closing_asks_the_owner_to_remove_it() => WinFormsUi.Run(() =>
    {
        var banner = new BannerControl { Tone = StatusTone.Warning, Description = "试用还剩 3 天", Dock = DockStyle.Top };
        var closed = 0;
        banner.CloseRequested += (_, _) => closed++;
        using var form = WinFormsUi.Show(banner);

        var close = banner.CloseButton!;
        Assert.Equal("关闭", close.AccessibilityObject.Name);
        Assert.True(close.Right < banner.Width && close.Left > banner.Width / 2); // 贴在结束边
        close.PerformClick();
        Assert.Equal(1, closed);
        Assert.True(banner.Visible); // 使用方没移除：还在
    });
}
