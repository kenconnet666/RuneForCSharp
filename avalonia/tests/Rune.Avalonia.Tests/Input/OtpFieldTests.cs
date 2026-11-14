using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>OtpField：一个真正的输入框（读屏看到它、由字段标签命名）；只留数字或字母数字，填满时 Completed；格子显示字符、遮罩、分组。</summary>
public class OtpFieldTests
{
    [Fact]
    [Upstream("OtpField", "一个原生 input")]
    public Task One_real_input_named_by_the_field() => HeadlessUi.Run(() =>
    {
        var otp = new OtpField();
        HeadlessUi.Show(new Field { Label = "短信验证码", Content = otp });

        Assert.Equal(AutomationControlType.Edit, otp.Peer().GetAutomationControlType());
        Assert.Equal("短信验证码", otp.Peer().GetName());
        Assert.Equal(0, otp.MaxLength);
        Assert.Equal(6, otp.SlotsPart!.Shown.Count);
        Assert.All(otp.SlotsPart.Shown, Assert.Null);
    });

    [Fact]
    [Upstream("OtpField", "输入（含粘贴）只留数字，填满时调用 onComplete；格子显示字符")]
    public Task Input_keeps_digits_and_completes_when_full() => HeadlessUi.Run(() =>
    {
        var completed = new List<string>();
        var otp = new OtpField();
        otp.Completed += (_, code) => completed.Add(code);
        AutomationProperties.SetName(otp, "验证码");
        HeadlessUi.Show(otp);

        otp.Text = "12a3";
        Assert.Equal("123", otp.Text);
        Assert.Empty(completed);
        // 粘贴带分隔的码：先过滤再截取，不丢位
        otp.Text = "123 456 789";
        Assert.Equal("123456", otp.Text);
        Assert.Equal(["123456"], completed);
        Assert.Equal(["1", "2", "3", "4", "5", "6"], otp.SlotsPart!.Shown);
    });

    [Fact]
    [Upstream("OtpField", "字母数字转大写；mask 显示圆点、type=password；groups 在组之间加分隔")]
    public Task Alphanumeric_mask_and_groups() => HeadlessUi.Run(() =>
    {
        var otp = new OtpField { Length = 4, Charset = OtpCharset.Alphanumeric, Groups = [2, 2], Mask = true };
        HeadlessUi.Show(otp);

        Assert.Equal('•', otp.PasswordChar);
        otp.Text = "ab-9";
        Assert.Equal("AB9", otp.Text);
        Assert.Equal(["•", "•", "•", null], otp.SlotsPart!.Shown);
        Assert.Equal([1], otp.SlotsPart.GroupEnds);
    });

    [Fact]
    public Task Focus_marks_the_next_empty_slot() => HeadlessUi.Run(() =>
    {
        var otp = new OtpField { Text = "12" };
        var window = HeadlessUi.Show(otp);
        Assert.Null(otp.SlotsPart!.Active);
        otp.Focus();
        HeadlessUi.Flush(window);
        Assert.Equal(2, otp.SlotsPart.Active);
        otp.IsReadOnly = true;
        Assert.Null(otp.SlotsPart.Active);
    });
}
