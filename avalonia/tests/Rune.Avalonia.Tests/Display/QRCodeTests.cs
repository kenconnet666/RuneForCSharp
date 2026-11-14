using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>QRCode：读屏是带名称的图片、边长含静区；有 logo 时用 H 纠错、logo 对读屏隐藏；过期时状态层显示提示与刷新。</summary>
public class QRCodeTests
{
    [Fact]
    [Upstream("QRCode", "svg 是 role=")]
    public Task Image_named_qr_code_with_the_quiet_zone() => HeadlessUi.Run(() =>
    {
        var code = new QRCode { Value = "https://example.com", QuietZone = 4 };
        HeadlessUi.Show(code);

        var peer = code.Peer();
        Assert.Equal(AutomationControlType.Image, peer.GetAutomationControlType());
        Assert.Equal("二维码", peer.GetName());
        Assert.Equal(QrEncoder.Encode("https://example.com").Size + 8, code.Extent);
        // 默认边长 10em
        Assert.Equal(10 * code.FontSize, code.Bounds.Width, 1);
    });

    [Fact]
    [Upstream("QRCode", "有 logo 时纠错用 H")]
    public Task Logo_switches_to_high_correction_and_is_hidden_from_screen_readers() => HeadlessUi.Run(() =>
    {
        const string value = "https://example.com/invite/8f3a";
        var plain = new QRCode { Value = value };
        var branded = new QRCode { Value = value, Logo = new Icon { Data = Lucide.Box } };
        AutomationProperties.SetName(branded, "带 logo");
        HeadlessUi.Show(new StackPanel { Children = { plain, branded } });

        Assert.Equal(QrErrorCorrection.H, branded.Matrix!.Ecc);
        Assert.Equal(QrEncoder.Encode(value, QrErrorCorrection.H).Size + 4, branded.Extent);
        Assert.True(branded.Extent > plain.Extent);
        Assert.Equal("带 logo", branded.Peer().GetName());
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(branded.LogoPart));
        Assert.True(branded.LogoPart.IsVisible);
    });

    [Fact]
    [Upstream("QRCode", "已过期：状态层显示提示，点刷新调用 onRefresh")]
    public Task Expired_shows_a_notice_and_refresh_raises_the_event() => HeadlessUi.Run(() =>
    {
        var refreshed = 0;
        var code = new QRCode { Value = "ticket" };
        code.Refresh += (_, _) => refreshed++;
        HeadlessUi.Show(code);
        Assert.Empty(code.OverlayPart.Children);

        code.Status = QRCodeStatus.Expired;
        Assert.Contains(code.OverlayPart.Children, child => child is Text { Text: "二维码已过期" });
        Assert.Equal("二维码已过期", AutomationProperties.GetName(code.OverlayPart));
        var button = code.OverlayPart.Children.OfType<Button>().Single();
        button.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(1, refreshed);
    });

    [Fact]
    public Task Loading_and_scanned_overlays() => HeadlessUi.Run(() =>
    {
        var code = new QRCode { Value = "ticket", Status = QRCodeStatus.Loading };
        HeadlessUi.Show(code);
        Assert.IsType<Spinner>(code.OverlayPart.Children.Single());
        Assert.Equal("加载中", AutomationProperties.GetName(code.OverlayPart));
        code.Status = QRCodeStatus.Scanned;
        Assert.Contains(code.OverlayPart.GetLogicalDescendants().OfType<Text>(), text => text.Text == "已扫描，请在手机上确认");
        // 已过期但没有订阅 Refresh：没有按钮
        code.Status = QRCodeStatus.Expired;
        Assert.Empty(code.OverlayPart.Children.OfType<Button>());
    });
}
