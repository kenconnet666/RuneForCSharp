using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Tour：卡片的标题、说明、步数与按钮；上一步 / 下一步、→ / ←（输入框里不处理），下一步可以先不可用；完成、跳过、Esc 的原因；镂空在目标处、卡片贴着目标，没有目标时居中；封面在标题之前。</summary>
public class TourTests
{
    static void Invoke(UIElement button)
    {
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button).GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
    }

    static void Press(UIElement from, Key key)
    {
        from.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(from)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        WpfUi.Flush();
    }

    static void Press(Tour tour, Key key) => Press(tour.Parts.Panel, key);

    /// <summary>引导与一个“搜索”按钮（第二步的目标）；不给步骤时是欢迎（带封面）、搜索、结束三步。</summary>
    static (Tour Tour, Button Search, Window Window) Create(params TourStep[] steps)
    {
        var search = new Button { Content = "搜索", Width = 120, Height = 32, Margin = new Thickness(40, 60, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        var tour = new Tour
        {
            Steps = steps.Length > 0 ? steps :
            [
                new TourStep { Title = "欢迎", Content = "花一分钟认识这几个常用功能。", Cover = new Border { Height = 40 } },
                new TourStep { Target = search, Title = "搜索", Content = "按订单号查找订单。" },
                new TourStep { Title = "就这些" },
            ],
        };
        var host = new StackPanel { Children = { search, tour } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        return (tour, search, WpfUi.Show(host));
    }

    [Fact]
    [Upstream("Tour", "部件属性：标题、说明、底部一行、步数")]
    public Task The_panel_shows_title_content_progress_and_buttons() => WpfUi.Run(() =>
    {
        var (tour, _, window) = Create();
        tour.IsOpen = true;
        WpfUi.Flush();

        var parts = tour.Parts;
        Assert.Equal(("欢迎", "1 / 3"), (parts.Title.Text, parts.Progress.Text));
        Assert.Equal("花一分钟认识这几个常用功能。", AutomationProperties.GetHelpText(parts.Panel));
        Assert.Equal(Visibility.Visible, parts.Skip.Visibility);
        Assert.Equal(Visibility.Collapsed, parts.Previous.Visibility);
        Assert.Equal("下一步", parts.Next.Content);
        // 封面在标题之前
        Assert.True(parts.Cover.IsVisible && parts.Cover.TranslatePoint(default, parts.Panel).Y < parts.Title.TranslatePoint(default, parts.Panel).Y);
    });

    [Fact]
    [Upstream("Tour", "部件属性：跳过、上一步、下一步；点击由组件管理，下一步可以先不可用")]
    public Task Buttons_move_between_steps_and_next_can_wait() => WpfUi.Run(() =>
    {
        var (tour, _, window) = Create();
        tour.IsOpen = true;
        WpfUi.Flush();

        Invoke(tour.Parts.Next);
        Assert.Equal(1, tour.Current);
        Assert.Equal(Visibility.Visible, tour.Parts.Previous.Visibility);
        Invoke(tour.Parts.Previous);
        Assert.Equal(0, tour.Current);
        tour.CanGoNext = false;
        Assert.False(tour.Parts.Next.IsEnabled);
        Press(tour, Key.Right);
        Assert.Equal(0, tour.Current);
    });

    [Fact]
    [Upstream("Tour", "封面在标题之前；→ / ← 切换步骤，焦点在输入框里时不处理")]
    public Task Arrow_keys_switch_steps_except_in_text_boxes() => WpfUi.Run(() =>
    {
        var input = new TextBox();
        var (tour, _, window) = Create(new TourStep { Title = "一" }, new TourStep { Title = "二", Content = input }, new TourStep { Title = "三" });
        tour.IsOpen = true;
        WpfUi.Flush();

        Press(tour, Key.Right);
        Assert.Equal(1, tour.Current);
        WpfUi.Flush();
        Press(input, Key.Right);
        Assert.Equal(1, tour.Current);
        Press(tour, Key.Left);
        Assert.Equal(0, tour.Current);
    });

    [Fact]
    public Task Finish_skip_and_escape_report_why() => WpfUi.Run(() =>
    {
        var (tour, _, window) = Create();
        var reasons = new List<OpenChangeReason>();
        var finished = 0;
        tour.OpenChanged += (_, e) => reasons.Add(e.Reason);
        tour.Finished += (_, _) => finished++;

        tour.IsOpen = true;
        WpfUi.Flush();
        Invoke(tour.Parts.Skip);
        tour.Current = 2;
        tour.IsOpen = true;
        WpfUi.Flush();
        Assert.Equal(("完成", Visibility.Collapsed), (tour.Parts.Next.Content, tour.Parts.Skip.Visibility));
        Invoke(tour.Parts.Next);
        tour.IsOpen = true;
        WpfUi.Flush();
        Press(tour, Key.Escape);
        Assert.False(tour.IsOpen);
        Assert.Equal([OpenChangeReason.Close, OpenChangeReason.Confirm, OpenChangeReason.Escape], reasons);
        Assert.Equal(1, finished);
    });

    [Fact]
    public Task The_spotlight_surrounds_the_target_and_the_panel_sits_below() => WpfUi.Run(() =>
    {
        var (tour, search, window) = Create();
        tour.Current = 1;
        tour.IsOpen = true;
        WpfUi.Flush();

        var root = (UIElement)window.Content;
        var expected = new Rect(search.TranslatePoint(default, root), search.RenderSize);
        expected.Inflate(6, 6);
        Assert.Equal(expected, tour.Spotlight);
        var panelTop = tour.Parts.Panel.TranslatePoint(default, (UIElement)VisualTreeHelperRoot(tour.Parts.Panel)).Y;
        Assert.True(panelTop >= tour.Spotlight!.Value.Bottom);
        tour.Current = 2;
        WpfUi.Flush();
        Assert.Null(tour.Spotlight);
    });

    static System.Windows.Media.Visual VisualTreeHelperRoot(System.Windows.Media.Visual visual)
    {
        var at = visual;
        while (System.Windows.Media.VisualTreeHelper.GetParent(at) is System.Windows.Media.Visual parent) at = parent;
        return at;
    }

    [Fact]
    public void Floating_geometry_flips_falls_back_and_shifts()
    {
        var viewport = new FloatingBox(0, 0, 800, 600);
        var below = FloatingGeometry.Place(FloatingPlacement.Bottom, false, new FloatingBox(100, 100, 80, 30), 200, 100, 8, viewport);
        Assert.Equal(("bottom", new FloatingBox(40, 138, 200, 100)), below);
        // 下方放不下：翻到上方
        var flipped = FloatingGeometry.Place(FloatingPlacement.Bottom, false, new FloatingBox(100, 540, 80, 30), 200, 100, 8, viewport);
        Assert.Equal(("top", 432d), (flipped.Side, flipped.Panel.Y));
        // 上下都放不下：crossAxisFallback 时放到右边
        Assert.Equal("right", FloatingGeometry.Place(FloatingPlacement.Bottom, false, new FloatingBox(100, 250, 80, 100), 200, 300, 8, viewport, crossAxisFallback: true).Side);
        // 左边放不下：沿对齐的方向挪回来，离边 8
        Assert.Equal(8, FloatingGeometry.Place(FloatingPlacement.BottomEnd, false, new FloatingBox(0, 100, 40, 30), 200, 100, 8, viewport).Panel.X);
    }
}
