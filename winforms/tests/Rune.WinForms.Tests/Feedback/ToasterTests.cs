using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>ToasterControl、ToastItemControl：区域是带名称的一组；新通知礼貌播报、重要的立即播报；焦点在通知里时 Esc 关掉它、离场后移除；只显示最新的 Max 条；停在父控件的一角。</summary>
public class ToasterTests
{
    static (ToasterController Toaster, ToasterControl View, Form Form) Show(int max = 3)
    {
        var toaster = new ToasterController(TimeSpan.FromMinutes(1));
        var view = new ToasterControl { Controller = toaster, Max = max };
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        host.Controls.Add(view);
        return (toaster, view, WinFormsUi.Show(host));
    }

    static ToastItemControl Item(ToasterControl view, string title) => view.Items.Single(item => item.Alert.Title == title);

    static bool Escape(Control target)
    {
        var message = new Message();
        var args = new object[] { message, Keys.Escape };
        return (bool)typeof(Control).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args)!;
    }

    [Fact]
    [Upstream("Toaster", "区域是带名称的 landmark，列表礼貌播报；重要的通知立即播报；焦点在通知里时 Esc 关掉它")]
    [Upstream("Toast", "焦点在通知里时 Esc 关掉它")]
    public Task Region_live_and_escape() => WinFormsUi.Run(() =>
    {
        var (toaster, view, form) = Show();
        using var _ = form;
        var saved = toaster.Success("已保存");
        toaster.Error("发送失败");
        WinFormsUi.Pump();

        Assert.Equal(AccessibleRole.Grouping, view.AccessibilityObject.Role);
        Assert.Equal("通知", view.AccessibilityObject.Name);
        Assert.Equal(AlertLive.Polite, Item(view, "已保存").Alert.Live);
        Assert.Equal(AlertLive.Assertive, Item(view, "发送失败").Alert.Live);

        Assert.True(Escape(Item(view, "已保存")));
        Assert.True(toaster.Toasts.Single(toast => toast.Id == saved).Closing);
        WinFormsUi.Until(() => toaster.Toasts.All(toast => toast.Id != saved));
        Assert.Single(view.Items);
    });

    [Fact]
    public Task Shows_the_newest_max_in_the_corner() => WinFormsUi.Run(() =>
    {
        var (toaster, view, form) = Show(max: 2);
        using var _ = form;
        toaster.Info("一");
        toaster.Info("二");
        var third = toaster.Info("三");
        var busy = toaster.Loading("正在导出");
        WinFormsUi.Pump();

        Assert.Equal(["三", "正在导出"], view.Items.Select(item => item.Alert.Title));
        Assert.Equal(2, toaster.Limit);
        // 停在父控件的右下角（区域自己带着 lg 的边）
        var parent = view.Parent!.ClientRectangle;
        Assert.Equal((parent.Right, parent.Bottom), (view.Right, view.Bottom));
        var loading = Item(view, "正在导出");
        Assert.True(loading.Alert.IsLoading);
        Assert.False(Escape(loading));
        Assert.False(toaster.Toasts.Single(toast => toast.Id == busy).Closing);
        toaster.Dismiss(third);
        WinFormsUi.Until(() => view.Items.Count == 2 && view.Items[0].Alert.Title == "二");
    });
}
