using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Toaster、ToastItem：区域是带名称的一组；新通知礼貌播报、重要的立即播报；焦点在通知里时 Esc 关掉它、离场后移除；只显示最新的 Max 条；悬停暂停计时。</summary>
public class ToasterTests
{
    static (ToasterController Toaster, Toaster View) Show(int max = 3)
    {
        var toaster = new ToasterController(TimeSpan.FromMinutes(1));
        var view = new Toaster { Controller = toaster, Max = max };
        var host = new Grid { Width = 800, Height = 600, Children = { view } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
        return (toaster, view);
    }

    static ToastItem Item(Toaster view, string title) => view.Items.Single(item => item.Alert.Title == title);

    static void Escape(UIElement target)
    {
        var source = PresentationSource.FromVisual(target)!;
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    [Fact]
    [Upstream("Toaster", "区域是带名称的 landmark，列表礼貌播报；重要的通知立即播报；焦点在通知里时 Esc 关掉它")]
    [Upstream("Toast", "焦点在通知里时 Esc 关掉它")]
    public Task Region_live_and_escape() => WpfUi.Run(() =>
    {
        var (toaster, view) = Show();
        var saved = toaster.Success("已保存");
        toaster.Error("发送失败");
        WpfUi.Flush();

        Assert.Equal("通知", view.RegionPart.Peer().GetName());
        Assert.Equal(AutomationControlType.Group, view.RegionPart.Peer().GetAutomationControlType());
        Assert.Equal(AlertLive.Polite, Item(view, "已保存").Alert.Live);
        Assert.Equal(AlertLive.Assertive, Item(view, "发送失败").Alert.Live);

        Escape(Item(view, "已保存"));
        Assert.True(toaster.Toasts.Single(toast => toast.Id == saved).Closing);
        WpfUi.Until(() => toaster.Toasts.All(toast => toast.Id != saved));
        Assert.Single(view.Items);
    });

    [Fact]
    public Task Shows_the_newest_max_and_loading_cannot_close() => WpfUi.Run(() =>
    {
        var (toaster, view) = Show(max: 2);
        toaster.Info("一");
        toaster.Info("二");
        var third = toaster.Info("三");
        var busy = toaster.Loading("正在导出");
        WpfUi.Flush();

        Assert.Equal(["三", "正在导出"], view.Items.Select(item => item.Alert.Title));
        Assert.Equal(2, toaster.Limit);
        var loading = Item(view, "正在导出");
        Assert.True(loading.Alert.IsLoading);
        Escape(loading);
        Assert.False(toaster.Toasts.Single(toast => toast.Id == busy).Closing);
        toaster.Dismiss(third);
        WpfUi.Until(() => view.Items.Count == 2 && view.Items[0].Alert.Title == "二");
    });

    [Fact]
    public Task Pointer_over_pauses_the_countdown() => WpfUi.Run(() =>
    {
        var (toaster, view) = Show();
        toaster.Info("一");
        WpfUi.Flush();
        Assert.False(toaster.Paused);

        view.RegionPart.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
        Assert.True(toaster.Paused);
        view.RegionPart.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
        Assert.False(toaster.Paused);
    });
}
