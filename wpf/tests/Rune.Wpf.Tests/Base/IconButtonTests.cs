using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Rune.Wpf.Tests;

/// <summary>
/// IconButton 与 ButtonBase：图标按钮宽高相等（宽度按 em，有下限）、默认 ghost 中性色、读屏名称来自 AutomationProperties.Name、
/// 部件样式键是自己的；ButtonBase 没有外观，但有波纹、加载中拦下点击。
/// </summary>
public class IconButtonTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static Color BrushColor(Brush? brush) => Assert.IsType<SolidColorBrush>(brush, exactMatch: false).Color;

    static T Part<T>(Control control, string name) => (T)control.Template.FindName(name, control);

    [Fact]
    public Task Icon_buttons_are_square_ghost_and_neutral() => WpfUi.Run(() =>
    {
        var large = new IconButton { Icon = Lucide.Settings, Size = TextSize.Lg, HorizontalAlignment = HorizontalAlignment.Left };
        var small = new IconButton { Icon = Lucide.Settings, Size = new TextSize("8"), HorizontalAlignment = HorizontalAlignment.Left };
        var host = Host(large, small);
        WpfUi.Show(host);

        Assert.Equal((ButtonVariant.Ghost, StatusTone.Neutral, Radius.Md), (large.Variant, large.Tone, large.Radius));
        Assert.Equal((36d, 36d), (large.ActualWidth, large.ActualHeight)); // 控件高度：lg = 16px → 36px
        Assert.Equal((24d, 24d), (small.ActualWidth, small.ActualHeight)); // 8px 的 2.25em 不到 24px，取最小点击区域
        Assert.Equal(16, Part<Icon>(large, "PART_Icon").ActualWidth);      // 图标 1em
        Assert.Equal(BrushColor((Brush)host.FindResource("Rune.Brush.Text")), BrushColor(large.Foreground));

        large.Width = 50; // 使用方写的宽度照常生效
        Assert.Equal(50, large.Width);
    });

    [Upstream("IconButton", "IconButton：名称来自 aria-label")]
    [Fact]
    public Task The_accessible_name_comes_from_automation_properties() => WpfUi.Run(() =>
    {
        var button = new IconButton { Icon = Lucide.Trash, Tone = StatusTone.Danger };
        AutomationProperties.SetName(button, "删除");
        var host = Host(button);
        WpfUi.Show(host);

        var peer = UIElementAutomationPeer.CreatePeerForElement(button);
        Assert.Equal(("删除", AutomationControlType.Button), (peer.GetName(), peer.GetAutomationControlType()));
        Assert.Equal(BrushColor((Brush)host.FindResource("Rune.Brush.DangerText")), BrushColor(button.Foreground));
    });

    [Upstream("CloseButton", "CloseButton、ClearButton")]
    [Fact]
    public Task Presets_take_their_icon_and_name_from_the_language_pack() => WpfUi.Run(() =>
    {
        var close = new CloseButton();
        var clear = new ClearButton();
        var english = new StackPanel { Children = { clear } };
        Provider.SetLang(english, Lang.EnUs);
        var host = Host(close, english);
        Provider.SetLang(host, Lang.ZhCn);
        WpfUi.Show(host);

        static string Name(UIElement element) => UIElementAutomationPeer.CreatePeerForElement(element).GetName();
        Assert.Equal((Lucide.X, "关闭"), (close.Icon, Name(close)));
        Assert.Equal((Lucide.CircleX, "Clear"), (clear.Icon, Name(clear))); // 内层的语言包
        AutomationProperties.SetName(close, "关闭提示");
        Assert.Equal("关闭提示", Name(close));
    });

    [Fact]
    public Task Button_base_has_no_look_but_ripples_and_blocks_clicks_while_loading() => WpfUi.Run(() =>
    {
        var clicks = 0;
        var button = new ButtonBase { Content = new TextBlock { Text = "个人版" }, Width = 160, Height = 60 };
        button.Click += (_, _) => clicks++;
        WpfUi.Show(Host(button));
        var invoke = (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke);

        Assert.Equal((Colors.Transparent, new Thickness(0)), (BrushColor(button.Background), button.Padding));
        button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
        Assert.Single(Part<Canvas>(button, "PART_Ripples").Children.OfType<Ellipse>());

        invoke.Invoke();
        WpfUi.Flush();
        Assert.Equal(1, clicks);
        button.IsLoading = true;
        invoke.Invoke();
        WpfUi.Flush();
        Assert.Equal(1, clicks);
    });

    [Fact]
    public Task Link_buttons_keep_the_block_padding() => WpfUi.Run(() =>
    {
        var link = new Rune.Wpf.Button { Content = "查看文档", Variant = ButtonVariant.Link, Size = TextSize.Lg };
        WpfUi.Show(Host(link));

        // TS 的 link 只去掉左右内边距与最小高度，上下 0.25em 保留
        Assert.Equal((new Thickness(0, 4, 0, 4), 0d), (link.Padding, link.MinHeight));
    });
}
