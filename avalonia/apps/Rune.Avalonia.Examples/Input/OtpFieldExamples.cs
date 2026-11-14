using Avalonia.Controls;
using Avalonia.Threading;

namespace Rune.Avalonia.Examples;

/// <summary>OtpField 页的示例（vue-tsx-pro 文档站 input/OtpField）。</summary>
static class OtpFieldExamples
{
    public static Example[] All =>
    [
        new("OtpField", "sms", "短信登录", "只有一个真正的输入框（平台的 TextBox），透明地盖在格子上（同 input-otp 的做法）：整段粘贴、退格、输入法、撤销都是输入框的，读屏看到的是一个输入框。只留数字，填满时触发 Completed，这里自动校验，错了清空重填（试试 246810）。", Sms),
        new("OtpField", "variants", "遮罩、分组与状态", "Mask 显示成圆点（支付密码、动态口令）；Charset=Alphanumeric 允许字母（转成大写）；Groups 在组之间加短横线，长的码好核对。放进 Field 时标签、说明、错误、不可用照常关联。", Variants),
    ];

    static Control Sms()
    {
        var code = new OtpField();
        var field = new Field { Label = "短信验证码", Description = "6 位数字，5 分钟内有效" };
        var send = new Button { Variant = ButtonVariant.Soft, Content = "获取验证码" };
        var message = new Small { Tone = TextTone.Muted };
        var countdown = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            countdown--;
            send.Content = countdown > 0 ? $"{countdown} 秒后重发" : "获取验证码";
            send.IsEnabled = countdown <= 0;
            if (countdown <= 0) timer.Stop();
        };
        send.Click += (_, _) =>
        {
            countdown = 60;
            send.Content = $"{countdown} 秒后重发";
            send.IsEnabled = false;
            message.Text = "验证码已发送（试试 246810）";
            message.Tone = TextTone.Muted;
            timer.Start();
        };
        // 假的校验接口：246810 是对的
        code.Completed += async (_, value) =>
        {
            code.IsEnabled = false;
            field.Error = null;
            message.Text = "正在校验…";
            await Task.Delay(500);
            code.IsEnabled = true;
            if (value == "246810")
            {
                message.Text = "登录成功";
                message.Tone = TextTone.Success;
                return;
            }
            message.Text = "";
            field.Error = "验证码不对，请重新输入";
            code.Text = "";
            code.Focus();
        };
        var row = new Flex { Align = FlexAlign.Center, Gap = Space.Md, Wrap = true, Children = { code, send } };
        field.Content = row;
        return new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children = { new Field { Label = "手机号", Content = new TextField { Text = "13800001111", Prefix = "+86" } }, field, message },
            },
        };
    }

    static Control Variants()
    {
        var pin = new OtpField { Mask = true };
        var pinField = new Field { Label = "支付密码", Description = "6 位数字，显示成圆点", Content = pin };
        pin.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) pinField.Description = pin.Text?.Length == 6 ? "已输入 6 位" : "6 位数字，显示成圆点";
        };
        var recovery = new OtpField { Length = 8, Charset = OtpCharset.Alphanumeric, Groups = [4, 4] };
        var recoveryField = new Field { Label = "恢复码", Description = "字母与数字，转成大写：（空）", Content = recovery };
        recovery.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) recoveryField.Description = $"字母与数字，转成大写：{(string.IsNullOrEmpty(recovery.Text) ? "（空）" : recovery.Text)}";
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                pinField, recoveryField,
                new Flex
                {
                    Gap = Space.Lg, Wrap = true,
                    Children =
                    {
                        new Field { Label = "Size=\"sm\"", Content = new OtpField { Text = "1234", Length = 4, Size = TextSize.Sm } },
                        new Field { Label = "出错", Error = "验证码已过期", Content = new OtpField { Text = "4821", Length = 4 } },
                        new Field { Label = "不可用", IsEnabled = false, Content = new OtpField { Text = "99", Length = 4 } },
                    },
                },
            },
        };
    }
}
