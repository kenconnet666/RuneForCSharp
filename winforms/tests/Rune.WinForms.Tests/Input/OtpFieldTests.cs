namespace Rune.WinForms.Tests;

/// <summary>OtpFieldControl：一个真正的输入框（读屏看到它、由字段标签命名）；只留数字或字母数字，填满时 Completed；格子显示字符、遮罩、分组。</summary>
public class OtpFieldTests
{
    static ProviderControl Host(params Control[] children)
    {
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        host.Controls.AddRange(children);
        return host;
    }

    [Fact]
    [Upstream("OtpField", "一个原生 input")]
    public Task One_real_input_named_by_the_field() => WinFormsUi.Run(() =>
    {
        var otp = new OtpFieldControl();
        using var form = WinFormsUi.Show(Host(new FieldControl { Label = "短信验证码", Content = otp }));

        Assert.Single(otp.Controls.OfType<TextBox>());
        Assert.Equal("短信验证码", otp.Input.AccessibleName);
        Assert.Equal(32767, otp.Input.MaxLength);
        Assert.Equal(6, otp.Shown.Count);
        Assert.All(otp.Shown, Assert.Null);
    });

    [Fact]
    [Upstream("OtpField", "输入（含粘贴）只留数字，填满时调用 onComplete；格子显示字符")]
    public Task Input_keeps_digits_and_completes_when_full() => WinFormsUi.Run(() =>
    {
        var completed = new List<string>();
        var otp = new OtpFieldControl { AccessibleName = "验证码" };
        otp.Completed += (_, code) => completed.Add(code);
        using var form = WinFormsUi.Show(Host(otp));

        otp.Input.Text = "12a3";
        Assert.Equal("123", otp.Text);
        Assert.Empty(completed);
        // 粘贴带分隔的码：先过滤再截取，不丢位
        otp.Input.Text = "123 456 789";
        Assert.Equal("123456", otp.Text);
        Assert.Equal(["123456"], completed);
        Assert.Equal(["1", "2", "3", "4", "5", "6"], otp.Shown);
    });

    [Fact]
    [Upstream("OtpField", "字母数字转大写；mask 显示圆点、type=password；groups 在组之间加分隔")]
    public Task Alphanumeric_mask_and_groups() => WinFormsUi.Run(() =>
    {
        var otp = new OtpFieldControl { Length = 4, Charset = OtpCharset.Alphanumeric, Groups = [2, 2], Mask = true };
        using var form = WinFormsUi.Show(Host(otp));

        Assert.True(otp.Input.UseSystemPasswordChar);
        otp.Text = "ab-9";
        Assert.Equal("AB9", otp.Text);
        Assert.Equal(["•", "•", "•", null], otp.Shown);
        Assert.Equal([1], otp.GroupEnds);
        WinFormsUi.Paint(otp);
    });

    [Fact]
    public Task Focus_marks_the_next_empty_slot_and_read_only_stops_editing() => WinFormsUi.Run(() =>
    {
        var otp = new OtpFieldControl { Text = "12" };
        using var form = WinFormsUi.Show(Host(otp));
        otp.Input.Focus();
        WinFormsUi.Pump();
        Assert.Equal(2, otp.Active);
        otp.IsReadOnly = true;
        Assert.Null(otp.Active);
        Assert.True(otp.Input.ReadOnly);
    });
}
