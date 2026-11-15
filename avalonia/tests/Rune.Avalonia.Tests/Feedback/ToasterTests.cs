using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Toaster、ToastItem：区域是带名称的 landmark；新通知礼貌播报、重要的立即播报；焦点在通知里时 Esc 关掉它、离场后移除；只显示最新的 Max 条。</summary>
public class ToasterTests
{
    static (ToasterController Toaster, Toaster View, Window Window) Show(int max = 3)
    {
        var toaster = new ToasterController(TimeSpan.FromMinutes(1));
        var view = new Toaster { Controller = toaster, Max = max };
        var window = HeadlessUi.Show(new Panel { Width = 800, Height = 600, Children = { view } });
        return (toaster, view, window);
    }

    static ToastItem Item(Toaster view, string title) => view.Items.Single(item => item.Alert.Title == title);

    [Fact]
    [Upstream("Toaster", "区域是带名称的 landmark，列表礼貌播报；重要的通知立即播报；焦点在通知里时 Esc 关掉它")]
    [Upstream("Toast", "焦点在通知里时 Esc 关掉它")]
    public Task Region_live_and_escape() => HeadlessUi.Run(() =>
    {
        var (toaster, view, window) = Show();
        var saved = toaster.Success("已保存");
        toaster.Error("发送失败");
        HeadlessUi.Flush(window);

        var region = view.RegionPart;
        Assert.Equal("通知", region.Peer().GetName());
        Assert.Equal(AutomationLandmarkType.Region, AutomationProperties.GetLandmarkType(region));
        Assert.Equal(AlertLive.Polite, Item(view, "已保存").Alert.Live);
        Assert.Equal(AlertLive.Assertive, Item(view, "发送失败").Alert.Live);

        var item = Item(view, "已保存");
        item.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = item });
        Assert.True(toaster.Toasts.Single(toast => toast.Id == saved).Closing);
        HeadlessUi.Until(() => toaster.Toasts.All(toast => toast.Id != saved));
        Assert.Single(view.Items);
    });

    [Fact]
    public Task Shows_the_newest_max_and_closes_through_the_alert() => HeadlessUi.Run(() =>
    {
        var (toaster, view, window) = Show(max: 2);
        toaster.Info("一");
        toaster.Info("二");
        var third = toaster.Info("三");
        var busy = toaster.Loading("正在导出");
        HeadlessUi.Flush(window);

        Assert.Equal(["三", "正在导出"], view.Items.Select(item => item.Alert.Title));
        Assert.Equal(2, toaster.Limit);
        // 加载中的不能关：没有关闭按钮，Esc 不管用
        var loading = Item(view, "正在导出");
        Assert.True(loading.Alert.IsLoading);
        loading.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = loading });
        Assert.False(toaster.Toasts.Single(toast => toast.Id == busy).Closing);
        toaster.Dismiss(third);
        HeadlessUi.Until(() => view.Items.Count == 2 && view.Items[0].Alert.Title == "二");
    });

    [Fact]
    public Task Pointer_over_pauses_the_countdown() => HeadlessUi.Run(() =>
    {
        var (toaster, view, window) = Show();
        toaster.Info("一");
        HeadlessUi.Flush(window);
        Assert.False(toaster.Paused);

        var item = view.Items[0];
        var center = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        HeadlessUi.Flush(window);
        Assert.True(toaster.Paused);
        window.MouseMove(new Point(5, 5));
        HeadlessUi.Flush(window);
        Assert.False(toaster.Paused);
    });

    [Fact]
    public Task Closing_without_motion_removes_after_the_sync() => HeadlessUi.Run(() =>
    {
        // 系统关闭动画（CI 上就是）：离场同步结束，移除要等这一轮排完
        var toaster = new ToasterController(TimeSpan.FromMinutes(1));
        var view = new Toaster { Controller = toaster };
        var host = new Panel { Width = 800, Height = 600, Children = { view } };
        foreach (var key in new[] { ThemeKeys.Duration.Sm, ThemeKeys.Duration.Md }) host.Resources[key] = TimeSpan.Zero;
        HeadlessUi.Show(host);
        var first = toaster.Info("一");
        toaster.Info("二");
        Item(view, "一").RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = Item(view, "一") });
        Assert.True(toaster.Toasts.Single(toast => toast.Id == first).Closing);
        HeadlessUi.Until(() => view.Items.Count == 1);
    });
}
