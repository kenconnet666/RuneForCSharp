namespace Rune.WinForms.Examples;

/// <summary>OtpField 页的示例（vue-tsx-pro 文档站 input/OtpField）。</summary>
static class OtpFieldExamples
{
    public static Example[] All =>
    [
        new("OtpField", "sms", "短信登录", "只有一个真正的输入框（平台的 TextBox，放在看不见的地方），格子是控件画的（同 input-otp 的做法）：整段粘贴、退格、输入法、撤销都是输入框的，读屏看到的是一个输入框。只留数字，填满时触发 Completed，这里自动校验，错了清空重填（试试 246810）。", Sms),
        new("OtpField", "variants", "遮罩、分组与状态", "Mask 显示成圆点（输入框也是密码框）；Charset=Alphanumeric 允许字母（转成大写）；Groups 在组之间加短横线，长的码好核对。放进 FieldControl 时标签、说明、错误、不可用照常关联。", Variants),
    ];

    static Control Sms()
    {
        var code = new OtpFieldControl();
        var send = new ButtonControl { Variant = ButtonVariant.Soft, Text = "获取验证码" };
        var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md, Wrap = true }, code, send);
        var field = new FieldControl { Label = "短信验证码", Description = "6 位数字，5 分钟内有效", Content = row };
        var message = new SmallControl { Tone = TextTone.Muted };
        var countdown = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) =>
        {
            countdown--;
            send.Text = countdown > 0 ? $"{countdown} 秒后重发" : "获取验证码";
            send.Enabled = countdown <= 0;
            if (countdown <= 0) timer.Stop();
        };
        send.Click += (_, _) =>
        {
            countdown = 60;
            send.Text = $"{countdown} 秒后重发";
            send.Enabled = false;
            message.Text = "验证码已发送（试试 246810）";
            message.Tone = TextTone.Muted;
            timer.Start();
        };
        // 假的校验接口：246810 是对的
        code.Completed += async (_, value) =>
        {
            code.Enabled = false;
            field.Error = null;
            message.Text = "正在校验…";
            await Task.Delay(500);
            code.Enabled = true;
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
        code.Disposed += (_, _) => timer.Dispose();
        return Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Dock = DockStyle.Top },
            new FieldControl { Label = "手机号", Content = new TextFieldControl { Text = "13800001111", Prefix = "+86" } }, field, message));
    }

    static Control Variants()
    {
        var pin = new OtpFieldControl { Mask = true };
        var pinField = new FieldControl { Label = "支付密码", Description = "6 位数字，显示成圆点", Content = pin };
        pin.TextChanged += (_, _) => pinField.Description = pin.Text.Length == 6 ? "已输入 6 位" : "6 位数字，显示成圆点";
        var recovery = new OtpFieldControl { Length = 8, Charset = OtpCharset.Alphanumeric, Groups = [4, 4] };
        var recoveryField = new FieldControl { Label = "恢复码", Description = "字母与数字，转成大写：（空）", Content = recovery };
        recovery.TextChanged += (_, _) => recoveryField.Description = $"字母与数字，转成大写：{(recovery.Text.Length == 0 ? "（空）" : recovery.Text)}";
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch },
            pinField, recoveryField,
            Build.Flex(new FlexControl { Gap = Space.Lg, Wrap = true },
                new FieldControl { Label = "TextSize=Sm", Content = new OtpFieldControl { Text = "1234", Length = 4, TextSize = TextSize.Sm } },
                new FieldControl { Label = "出错", Error = "验证码已过期", Content = new OtpFieldControl { Text = "4821", Length = 4 } },
                new FieldControl { Label = "不可用", Enabled = false, Content = new OtpFieldControl { Text = "99", Length = 4 } }));
    }
}
