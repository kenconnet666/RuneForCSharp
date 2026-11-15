using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>TourControl：卡片的标题、说明、步数与按钮；上一步 / 下一步、→ / ←（输入框里不处理），下一步可以先不可用；完成、跳过、Esc 的原因；镂空在目标处、卡片贴着目标，没有目标时居中。</summary>
public class TourTests
{
    static (TourControl Tour, ButtonControl Search, Form Form) Create()
    {
        var search = new ButtonControl { Text = "搜索", Left = 40, Top = 60, Width = 120 };
        var tour = new TourControl
        {
            Steps =
            [
                new TourStep { Title = "欢迎", Content = "花一分钟认识这几个常用功能。", Cover = () => new Panel { Height = 40 } },
                new TourStep { Target = search, Title = "搜索", Content = "按订单号查找订单。" },
                new TourStep { Title = "就这些" },
            ],
        };
        var form = WinFormsUi.Show(search, tour);
        form.ClientSize = new Size(form.LogicalToDeviceUnits(800), form.LogicalToDeviceUnits(600));
        return (tour, search, form);
    }

    static void Escape(TourControl tour)
    {
        typeof(Form).GetMethod("ProcessDialogKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(tour.PanelForm, [Keys.Escape]);
        WinFormsUi.Pump();
    }

    [Fact]
    [Upstream("Tour", "部件属性：标题、说明、底部一行、步数")]
    public Task The_panel_shows_title_content_progress_and_buttons() => WinFormsUi.Run(() =>
    {
        var (tour, _, form) = Create();
        using var _ = form;
        tour.IsOpen = true;
        WinFormsUi.Pump();

        var parts = tour.Parts;
        Assert.Equal(("欢迎", "1 / 3", "花一分钟认识这几个常用功能。"), (parts.Title.Text, parts.Progress.Text, parts.Content.Text));
        Assert.Equal(("欢迎", AccessibleRole.Dialog), (tour.PanelForm.AccessibleName, tour.PanelForm.AccessibleRole));
        Assert.True(parts.Skip.Visible);
        Assert.False(parts.Previous.Visible);
        Assert.Equal("下一步", parts.Next.Text);
        // 封面在标题之前
        Assert.True(parts.Cover!.Top < parts.Title.Top);
        tour.IsOpen = false;
    });

    [Fact]
    [Upstream("Tour", "部件属性：跳过、上一步、下一步；点击由组件管理，下一步可以先不可用")]
    public Task Buttons_move_between_steps_and_next_can_wait() => WinFormsUi.Run(() =>
    {
        var (tour, _, form) = Create();
        using var _ = form;
        tour.IsOpen = true;
        WinFormsUi.Pump();

        tour.Parts.Next.PerformClick();
        Assert.Equal(1, tour.Current);
        Assert.True(tour.Parts.Previous.Visible);
        tour.Parts.Previous.PerformClick();
        Assert.Equal(0, tour.Current);
        tour.CanGoNext = false;
        Assert.False(tour.Parts.Next.Enabled);
        Assert.False(tour.HandleKey(Keys.Right, null));
        Assert.Equal(0, tour.Current);
        tour.IsOpen = false;
    });

    [Fact]
    [Upstream("Tour", "封面在标题之前；→ / ← 切换步骤，焦点在输入框里时不处理")]
    public Task Arrow_keys_switch_steps_except_in_text_boxes() => WinFormsUi.Run(() =>
    {
        var (tour, _, form) = Create();
        using var _ = form;
        tour.IsOpen = true;
        WinFormsUi.Pump();

        Assert.True(tour.HandleKey(Keys.Right, null));
        Assert.Equal(1, tour.Current);
        Assert.False(tour.HandleKey(Keys.Right, new TextBox()));
        Assert.Equal(1, tour.Current);
        Assert.True(tour.HandleKey(Keys.Left, null));
        Assert.Equal(0, tour.Current);
        tour.IsOpen = false;
    });

    [Fact]
    public Task Finish_skip_and_escape_report_why() => WinFormsUi.Run(() =>
    {
        var (tour, _, form) = Create();
        using var _ = form;
        var reasons = new List<OpenChangeReason>();
        var finished = 0;
        tour.OpenChanged += (_, e) => reasons.Add(e.Reason);
        tour.Finished += (_, _) => finished++;

        tour.IsOpen = true;
        WinFormsUi.Pump();
        tour.Parts.Skip.PerformClick();
        tour.Current = 2;
        tour.IsOpen = true;
        WinFormsUi.Pump();
        Assert.Equal(("完成", false), (tour.Parts.Next.Text, tour.Parts.Skip.Visible));
        tour.Parts.Next.PerformClick();
        tour.IsOpen = true;
        WinFormsUi.Pump();
        Escape(tour);
        Assert.False(tour.IsOpen);
        Assert.Equal([OpenChangeReason.Close, OpenChangeReason.Confirm, OpenChangeReason.Escape], reasons);
        Assert.Equal(1, finished);
    });

    [Fact]
    public Task The_spotlight_surrounds_the_target_and_the_panel_sits_below() => WinFormsUi.Run(() =>
    {
        var (tour, search, form) = Create();
        using var _ = form;
        tour.Current = 1;
        tour.IsOpen = true;
        WinFormsUi.Pump();

        var padding = tour.LogicalToDeviceUnits(6);
        Assert.Equal(Rectangle.Inflate(search.RectangleToScreen(search.ClientRectangle), padding, padding), tour.Spotlight);
        Assert.True(tour.PanelForm.Top >= tour.Spotlight!.Value.Bottom);
        tour.Current = 2;
        WinFormsUi.Pump();
        Assert.Null(tour.Spotlight);
        var area = form.RectangleToScreen(form.ClientRectangle);
        Assert.InRange(tour.PanelForm.Left + tour.PanelForm.Width / 2 - (area.Left + area.Width / 2), -tour.LogicalToDeviceUnits(4), tour.LogicalToDeviceUnits(4));
        tour.IsOpen = false;
    });
}
