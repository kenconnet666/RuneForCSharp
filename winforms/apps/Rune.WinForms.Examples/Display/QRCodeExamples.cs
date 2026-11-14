namespace Rune.WinForms.Examples;

/// <summary>QRCode 页的示例（vue-tsx-pro 文档站 display/QRCode）。上游的"下载 SVG"在桌面上是"复制 SVG"（QrEncoder.Svg 生成文档）。</summary>
static class QRCodeExamples
{
    public static Example[] All =>
    [
        new("QRCode", "share", "分享链接", "Value 按 UTF-8 编码（中文、链接都行），自动选最小的版本（21 × 21 到 177 × 177 个模块）与罚分最低的掩码。Ecc 是纠错等级：越高能扛的污损越多，码也越密，默认 M。边长默认 10em（AutoSize），QuietZone 是四周静区的模块数（默认 2）。要自己处理时 QrEncoder.Encode 拿到矩阵，QrEncoder.Svg 导出 SVG。", Share),
        new("QRCode", "logo", "中间放 logo 与换颜色", "Logo 是图片或任意控件（AvatarControl、IconControl），放在中间的底板上，此时纠错默认用 H（能恢复约 30%）；LogoSize 是占边长的比例，默认 0.22，太大会扫不出来。颜色默认跟主题：浅色主题下是 surface 底、文字色模块；深色主题下仍是白底深色模块。Foreground、Background 可以改，保持深色模块配浅色底。", Branded),
        new("QRCode", "status", "扫码登录的状态", "Status：Loading 取票据时转圈，Expired 盖上\"二维码已过期\"，订阅了 RefreshRequested 时有刷新按钮，Scanned 是手机已经扫到、等确认。状态是读屏的描述，换状态时通知；二维码本身是带名称的图片（AccessibleName，默认\"二维码\"）。", Login),
    ];

    static Control Share()
    {
        var code = new QRCodeControl { Value = "https://zerodep.dev/invite/8f3a2c", AutoSize = false };
        code.Size = new Size(code.DeviceDpi * 126 / 96, code.DeviceDpi * 126 / 96);
        var text = new TextFieldControl { Text = code.Value };
        var ecc = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, AccessibleName = "纠错等级" };
        ecc.Controls.AddRange([.. new[] { ("L", "L 7%"), ("M", "M 15%"), ("Q", "Q 25%"), ("H", "H 30%") }.Select(item => new ToggleItemControl { Value = item.Item1, Text = item.Item2 })]);
        ecc.Value = "M";
        var info = new SmallControl { Tone = TextTone.Muted };
        void Update()
        {
            code.Value = text.Text;
            code.Ecc = Enum.Parse<QrErrorCorrection>(ecc.Value ?? "M");
            info.Text = code.Matrix is { } qr ? $"版本 {qr.Version}，{qr.Size} × {qr.Size} 个模块" : "没有内容";
        }
        text.TextChanged += (_, _) => Update();
        ecc.ValueChanged += (_, _) => Update();
        Update();
        var copy = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.Copy, Text = "复制 SVG" };
        copy.Click += (_, _) =>
        {
            if (code.Matrix is { } qr) Clipboard.SetText(QrEncoder.Svg(qr, code.QuietZone, "#1c2024", "#ffffff"));
        };
        var footer = Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm }, info, copy);
        var form = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch },
            new FieldControl { Label = "内容", Content = text }, new FieldControl { Label = "纠错等级", Content = ecc }, footer);
        var row = Build.Flex(new FlexControl { Gap = Space.Lg, Align = FlexAlign.Center, Dock = DockStyle.Top }, code, form);
        row.SetGrow(form, true);
        return row;
    }

    static Control Branded()
    {
        static Control Item(string title, QRCodeControl code) =>
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Xs }, code, new SmallControl { Text = title, Tone = TextTone.Muted });
        var blue = Color.FromArgb(0x1d, 0x4e, 0xd8);
        return Build.Flex(new FlexControl { Gap = Space.Lg, Wrap = true },
            Item("图标", new QRCodeControl { Value = "https://zerodep.dev/app", Logo = new IconControl { Data = Lucide.Box, Tone = TextTone.Accent } }),
            Item("头像", new QRCodeControl { Value = "https://zerodep.dev/u/lin-xiaoman", Logo = new AvatarControl { DisplayName = "林小满", TextSize = TextSize.Sm }, LogoSize = 0.26 }),
            Item("自定义颜色", new QRCodeControl
            {
                Value = "https://zerodep.dev/store/42", Logo = new IconControl { Data = Lucide.Store, ForeColor = blue },
                Foreground = blue, Background = Color.FromArgb(0xef, 0xf6, 0xff),
            }));
    }

    static Control Login()
    {
        var code = new QRCodeControl { Status = QRCodeStatus.Loading, Value = "https://zerodep.dev/login?ticket=pending", AutoSize = false };
        code.Size = new Size(code.DeviceDpi * 154 / 96, code.DeviceDpi * 154 / 96);
        var hint = new FlexControl { AutoSize = true };
        var scan = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Text = "模拟扫码", Enabled = false };
        var expire = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Text = "立即过期" };
        var fetching = new System.Windows.Forms.Timer { Interval = 600 };

        void Show(QRCodeStatus status)
        {
            code.Status = status;
            scan.Enabled = status == QRCodeStatus.Active;
            foreach (var child in hint.Controls.Cast<Control>().ToList()) child.Dispose();
            if (status != QRCodeStatus.Active)
            {
                hint.Controls.Add(new SmallControl { Text = "打开手机 App，扫一扫", Tone = TextTone.Muted });
                return;
            }
            // 60 秒后过期：倒计时到点时换成已过期
            var countdown = new CountdownControl { To = DateTimeOffset.Now.AddSeconds(60), Formatter = left => $"{left.Minutes * 60 + left.Seconds} 秒后过期" };
            countdown.ForeColor = TextTone.Muted.ColorIn(ElementContext.Of(hint));
            countdown.Ended += (_, _) => Show(QRCodeStatus.Expired);
            hint.Controls.Add(countdown);
        }

        // 假的票据接口：600 ms 后给一个新票据
        void Renew()
        {
            fetching.Stop();
            Show(QRCodeStatus.Loading);
            fetching.Start();
        }
        fetching.Tick += (_, _) =>
        {
            fetching.Stop();
            code.Value = $"https://zerodep.dev/login?ticket={Guid.NewGuid():N}"[..48];
            Show(QRCodeStatus.Active);
        };

        code.RefreshRequested += (_, _) => Renew();
        scan.Click += (_, _) => Show(QRCodeStatus.Scanned);
        expire.Click += (_, _) => Show(QRCodeStatus.Expired);
        code.Disposed += (_, _) => fetching.Dispose();
        Renew();
        return Build.Card(new CardControl { CardPadding = Space.Lg }, Build.Flex(new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm, Dock = DockStyle.Top },
            new TextControl { Text = "扫码登录", Weight = TextWeight.Semibold }, code, hint, Build.Flex(new FlexControl { Gap = Space.Xs }, scan, expire)));
    }
}
