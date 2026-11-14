using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>QRCode：读屏是带名称的图片、边长含静区；有 logo 时用 H 纠错、logo 对读屏隐藏；过期时状态层显示提示与刷新。</summary>
public class QRCodeTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    [Fact]
    [Upstream("QRCode", "svg 是 role=")]
    public Task Image_named_qr_code_with_the_quiet_zone() => WpfUi.Run(() =>
    {
        var code = new QRCode { Value = "https://example.com", QuietZone = 4 };
        WpfUi.Show(Host(code));

        var peer = code.Peer();
        Assert.Equal(AutomationControlType.Image, peer.GetAutomationControlType());
        Assert.Equal("二维码", peer.GetName());
        Assert.Equal(QrEncoder.Encode("https://example.com").Size + 8, code.Extent);
        // 默认边长 10em
        Assert.Equal(10 * code.FontSize, code.ActualWidth, 1);
    });

    [Fact]
    [Upstream("QRCode", "有 logo 时纠错用 H")]
    public Task Logo_switches_to_high_correction_and_is_hidden_from_screen_readers() => WpfUi.Run(() =>
    {
        const string value = "https://example.com/invite/8f3a";
        var plain = new QRCode { Value = value };
        var branded = new QRCode { Value = value, Logo = new Icon { Data = Lucide.Box } };
        AutomationProperties.SetName(branded, "带 logo");
        WpfUi.Show(Host(plain, branded));

        Assert.Equal(QrErrorCorrection.H, branded.Matrix!.Ecc);
        Assert.Equal(QrEncoder.Encode(value, QrErrorCorrection.H).Size + 4, branded.Extent);
        Assert.True(branded.Extent > plain.Extent);
        Assert.Equal("带 logo", branded.Peer().GetName());
        // logo 不在读屏树里
        Assert.DoesNotContain(branded.Peer().GetChildren() ?? [], child => child is FrameworkElementAutomationPeer { Owner: var owner } && owner == branded.LogoPart);
        Assert.Equal(Visibility.Visible, branded.LogoPart.Visibility);
    });

    [Fact]
    [Upstream("QRCode", "已过期：状态层显示提示，点刷新调用 onRefresh")]
    public Task Expired_shows_a_notice_and_refresh_raises_the_event() => WpfUi.Run(() =>
    {
        var refreshed = 0;
        var code = new QRCode { Value = "ticket" };
        code.Refresh += (_, _) => refreshed++;
        WpfUi.Show(Host(code));
        Assert.Empty(code.OverlayPart.Children);

        code.Status = QRCodeStatus.Expired;
        Assert.Contains(code.OverlayPart.Children.Cast<UIElement>(), child => child is Text { Text: "二维码已过期" });
        Assert.Equal("二维码已过期", AutomationProperties.GetName(code.OverlayPart));
        var button = code.OverlayPart.Children.OfType<Button>().Single();
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.Equal(1, refreshed);
    });

    [Fact]
    public Task Loading_and_scanned_overlays() => WpfUi.Run(() =>
    {
        var code = new QRCode { Value = "ticket", Status = QRCodeStatus.Loading };
        WpfUi.Show(Host(code));
        Assert.IsType<Spinner>(code.OverlayPart.Children.Cast<UIElement>().Single());
        Assert.Equal("加载中", AutomationProperties.GetName(code.OverlayPart));
        code.Status = QRCodeStatus.Scanned;
        Assert.Contains(code.OverlayPart.Children.OfType<Text>(), text => text.Text == "已扫描，请在手机上确认");
        // 已过期但没有订阅 Refresh：没有按钮
        code.Status = QRCodeStatus.Expired;
        Assert.Empty(code.OverlayPart.Children.OfType<Button>());
    });
}
