namespace Rune.WinForms.Tests;

/// <summary>CollapseControl：收起时子控件隐藏、不占高度；展开后长到内容高；收起的过程中子控件仍可见；第一次展开才建句柄。</summary>
public class CollapseTests
{
    [Fact]
    [Upstream("Collapse", "收起时隐藏内容")]
    public Task Content_is_hidden_once_fully_collapsed() => WinFormsUi.Run(() =>
    {
        var button = new ButtonControl { Text = "内容", AutoSize = false, Height = 40 };
        var collapse = new CollapseControl { Dock = DockStyle.Top };
        collapse.Controls.Add(button);
        using var form = WinFormsUi.Show(collapse);

        Assert.False(button.Visible); // 不能 Tab 聚焦，读屏读不到
        Assert.Equal(0, collapse.Height);

        collapse.IsOpen = true;
        Assert.True(button.Visible);
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(400));
        Assert.Equal(40, collapse.Height);
        Assert.Equal(collapse.ClientSize.Width, button.Width); // 撑满宽度

        collapse.IsOpen = false;
        Assert.Equal(Motion.Enabled, button.Visible); // 过渡结束才隐藏；系统关闭动画时立即隐藏
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(400));
        Assert.False(button.Visible);
        Assert.Equal(0, collapse.Height);
    });

    [Fact]
    public Task Starting_open_shows_the_content_at_full_height() => WinFormsUi.Run(() =>
    {
        var collapse = new CollapseControl { IsOpen = true, Dock = DockStyle.Top };
        collapse.Controls.Add(new ButtonControl { Text = "内容", AutoSize = false, Height = 40 });
        using var form = WinFormsUi.Show(collapse);

        Assert.True(collapse.Controls[0].Visible);
        Assert.Equal(40, collapse.Height);
    });

    [Fact]
    [Upstream("Collapse", "lazy：第一次展开时才渲染内容")]
    public Task Content_is_created_on_first_open_and_kept() => WinFormsUi.Run(() =>
    {
        // WinForms 没有 lazy：隐藏的子控件本来就不建句柄，第一次展开才建，之后收起也保留
        var content = new TextControl { Text = "内容" };
        var collapse = new CollapseControl { Dock = DockStyle.Top };
        collapse.Controls.Add(content);
        using var form = WinFormsUi.Show(collapse);

        Assert.False(content.IsHandleCreated);
        collapse.IsOpen = true;
        WinFormsUi.Pump();
        Assert.True(content.IsHandleCreated);
        collapse.IsOpen = false;
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(400));
        Assert.True(content.IsHandleCreated);
    });
}
