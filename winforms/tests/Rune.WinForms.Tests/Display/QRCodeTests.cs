namespace Rune.WinForms.Tests;

/// <summary>QRCodeControl：读屏是带名称的图片、边长含静区；有 logo 时用 H 纠错；过期时状态层显示提示与刷新。</summary>
public class QRCodeTests
{
    static ProviderControl Host(params Control[] children)
    {
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        host.Controls.AddRange(children);
        return host;
    }

    [Fact]
    [Upstream("QRCode", "svg 是 role=")]
    public Task Image_named_qr_code_with_the_quiet_zone() => WinFormsUi.Run(() =>
    {
        var code = new QRCodeControl { Value = "https://example.com", QuietZone = 4 };
        using var form = WinFormsUi.Show(Host(code));

        Assert.Equal(AccessibleRole.Graphic, code.AccessibilityObject.Role);
        Assert.Equal("二维码", code.AccessibilityObject.Name);
        Assert.Equal(QrEncoder.Encode("https://example.com").Size + 8, code.Extent);
        // 默认边长 10em
        Assert.Equal(code.Width, code.Height);
        Assert.InRange(code.Width, (int)(10 * code.Font.SizeInPoints * code.DeviceDpi / 72f) - 1, (int)(10 * code.Font.SizeInPoints * code.DeviceDpi / 72f) + 1);
    });

    [Fact]
    [Upstream("QRCode", "有 logo 时纠错用 H")]
    public Task Logo_switches_to_high_correction() => WinFormsUi.Run(() =>
    {
        const string value = "https://example.com/invite/8f3a";
        var plain = new QRCodeControl { Value = value };
        var logo = new IconControl { Data = Lucide.Box };
        var branded = new QRCodeControl { Value = value, Logo = logo, AccessibleName = "带 logo" };
        using var form = WinFormsUi.Show(Host(plain, branded));

        Assert.Equal(QrErrorCorrection.H, branded.Matrix!.Ecc);
        Assert.Equal(QrEncoder.Encode(value, QrErrorCorrection.H).Size + 4, branded.Extent);
        Assert.True(branded.Extent > plain.Extent);
        Assert.Equal("带 logo", branded.AccessibilityObject.Name);
        // logo 在正中间
        Assert.InRange(logo.Left + logo.Width / 2, branded.Width / 2 - 2, branded.Width / 2 + 2);
    });

    [Fact]
    [Upstream("QRCode", "已过期：状态层显示提示，点刷新调用 onRefresh")]
    public Task Expired_shows_a_notice_and_refresh_raises_the_event() => WinFormsUi.Run(() =>
    {
        var refreshed = 0;
        var code = new QRCodeControl { Value = "ticket" };
        code.RefreshRequested += (_, _) => refreshed++;
        using var form = WinFormsUi.Show(Host(code));
        Assert.False(code.OverlayPart.Visible);

        code.Status = QRCodeStatus.Expired;
        Assert.True(code.OverlayPart.Visible);
        Assert.Contains(code.OverlayPart.Controls.Cast<Control>(), child => child.Text == "二维码已过期");
        Assert.Equal("二维码已过期", code.AccessibilityObject.Description);
        code.OverlayPart.Controls.OfType<ButtonControl>().Single().PerformClick();
        Assert.Equal(1, refreshed);
    });

    [Fact]
    public Task Loading_and_scanned_overlays() => WinFormsUi.Run(() =>
    {
        var code = new QRCodeControl { Value = "ticket", Status = QRCodeStatus.Loading };
        using var form = WinFormsUi.Show(Host(code));
        Assert.IsType<SpinnerControl>(code.OverlayPart.Controls.Cast<Control>().Single());
        Assert.Equal("加载中", code.AccessibilityObject.Description);
        code.Status = QRCodeStatus.Scanned;
        Assert.Contains(code.OverlayPart.Controls.Cast<Control>(), child => child.Text == "已扫描，请在手机上确认");
        // 已过期但没有订阅 RefreshRequested：没有按钮
        code.Status = QRCodeStatus.Expired;
        Assert.Empty(code.OverlayPart.Controls.OfType<ButtonControl>());
        WinFormsUi.Paint(code);
    });
}
