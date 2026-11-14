using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Rune.Wpf.Examples;

/// <summary>QRCode 页的示例（vue-tsx-pro 文档站 display/QRCode）。上游的"下载 SVG"在桌面上是"复制 SVG"（QrEncoder.Svg 生成文档）。</summary>
static class QRCodeExamples
{
    public static Example[] All =>
    [
        new("QRCode", "share", "分享链接", "Value 按 UTF-8 编码（中文、链接都行），自动选最小的版本（21 × 21 到 177 × 177 个模块）与罚分最低的掩码。Ecc 是纠错等级：越高能扛的污损越多，码也越密，默认 M。Width 是边长（默认 10em），QuietZone 是四周静区的模块数（默认 2）。画出来是一条路径，放大不糊；要自己处理时 QrEncoder.Encode 拿到矩阵，QrEncoder.Svg 导出 SVG。", Share),
        new("QRCode", "logo", "中间放 logo 与换颜色", "Logo 是图片或任意控件（Avatar、Icon），放在中间的底板上，此时纠错默认用 H（能恢复约 30%）；LogoSize 是占边长的比例，默认 0.22，太大会扫不出来。颜色默认跟主题：浅色主题下是 surface 底、文字色模块；深色主题下仍是白底深色模块，反色的码很多扫码器认不出。Foreground、Background 可以改，保持深色模块配浅色底。", Branded),
        new("QRCode", "status", "扫码登录的状态", "Status：Loading 取票据时转圈，Expired 盖上\"二维码已过期\"，订阅了 Refresh 时有刷新按钮，Scanned 是手机已经扫到、等确认。状态层换状态时读屏会念；二维码本身是带名称的图片（AutomationProperties.Name，默认\"二维码\"）。", Login),
    ];

    static FrameworkElement Share()
    {
        var code = new QRCode { Value = "https://zerodep.dev/invite/8f3a2c", Width = 126 };
        var text = new TextField { Text = code.Value };
        var ecc = new ToggleGroup
        {
            IsRequired = true, Size = TextSize.Sm, Value = "M",
            Children = { new ToggleItem { Value = "L", Content = "L 7%" }, new ToggleItem { Value = "M", Content = "M 15%" }, new ToggleItem { Value = "Q", Content = "Q 25%" }, new ToggleItem { Value = "H", Content = "H 30%" } },
        };
        var info = new Small { Tone = TextTone.Muted };
        void Update()
        {
            code.Value = text.Text;
            code.Ecc = Enum.Parse<QrErrorCorrection>(ecc.Value ?? "M");
            info.Text = code.Matrix is { } qr ? $"版本 {qr.Version}，{qr.Size} × {qr.Size} 个模块" : "没有内容";
        }
        text.TextChanged += (_, _) => Update();
        DependencyPropertyDescriptor.FromProperty(ToggleGroup.ValueProperty, typeof(ToggleGroup)).AddValueChanged(ecc, (_, _) => Update());
        Update();
        var copy = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.Copy, Content = "复制 SVG" };
        copy.Click += (_, _) =>
        {
            if (code.Matrix is { } qr) Clipboard.SetText(QrEncoder.Svg(qr, code.QuietZone, "#1c2024", "#ffffff"));
        };
        var form = new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, MinWidth = 224,
            Children =
            {
                new Field { Label = "内容", Content = text },
                new Field { Label = "纠错等级", Content = ecc },
                new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Children = { info, copy } },
            },
        };
        var row = new Flex { Gap = Space.Lg, Align = FlexAlign.Center, Wrap = true, Children = { code, form } };
        Flex.SetGrow(form, true);
        return row;
    }

    static FrameworkElement Branded()
    {
        static FrameworkElement Item(string title, QRCode code) =>
            new Flex { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Xs, Children = { code, new Small { Text = title, Tone = TextTone.Muted } } };
        return new Flex
        {
            Gap = Space.Lg, Wrap = true,
            Children =
            {
                Item("图标", new QRCode { Value = "https://zerodep.dev/app", Logo = new Icon { Data = Lucide.Box, Tone = TextTone.Accent } }),
                Item("头像", new QRCode { Value = "https://zerodep.dev/u/lin-xiaoman", Logo = new Avatar { DisplayName = "林小满", Size = TextSize.Sm }, LogoSize = 0.26 }),
                Item("自定义颜色", new QRCode
                {
                    Value = "https://zerodep.dev/store/42", Logo = new Icon { Data = Lucide.Store, Foreground = new SolidColorBrush(Color.FromRgb(0x1d, 0x4e, 0xd8)) },
                    Foreground = new SolidColorBrush(Color.FromRgb(0x1d, 0x4e, 0xd8)), Background = new SolidColorBrush(Color.FromRgb(0xef, 0xf6, 0xff)),
                }),
            },
        };
    }

    static FrameworkElement Login()
    {
        var code = new QRCode { Width = 154, Status = QRCodeStatus.Loading };
        var hint = new Flex();
        var scan = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Content = "模拟扫码", IsEnabled = false };
        var expire = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Content = "立即过期" };
        DispatcherTimer? fetching = null;

        void Show(QRCodeStatus status)
        {
            code.Status = status;
            scan.IsEnabled = status == QRCodeStatus.Active;
            hint.Children.Clear();
            if (status != QRCodeStatus.Active)
            {
                hint.Children.Add(new Small { Text = "打开手机 App，扫一扫", Tone = TextTone.Muted });
                return;
            }
            // 60 秒后过期：倒计时到点时换成已过期
            var countdown = new Countdown { To = DateTimeOffset.Now.AddSeconds(60), Formatter = left => $"{left.Minutes * 60 + left.Seconds} 秒后过期" };
            countdown.Ended += (_, _) => Show(QRCodeStatus.Expired);
            countdown.SetResourceReference(Control.ForegroundProperty, ThemeKeys.Brush.TextMuted);
            hint.Children.Add(countdown);
        }

        /// 假的票据接口：600 ms 后给一个新票据
        void Renew()
        {
            fetching?.Stop();
            Show(QRCodeStatus.Loading);
            fetching = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            fetching.Tick += (_, _) =>
            {
                fetching.Stop();
                code.Value = $"https://zerodep.dev/login?ticket={Guid.NewGuid():N}"[..48];
                Show(QRCodeStatus.Active);
            };
            fetching.Start();
        }

        code.Refresh += (_, _) => Renew();
        scan.Click += (_, _) => Show(QRCodeStatus.Scanned);
        expire.Click += (_, _) => Show(QRCodeStatus.Expired);
        code.Value = "https://zerodep.dev/login?ticket=pending";
        Renew();
        return new Card
        {
            Padding = Space.Lg,
            Content = new Flex
            {
                Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm,
                Children = { new Text { Text = "扫码登录", Weight = TextWeight.Semibold }, code, hint, new Flex { Gap = Space.Xs, Children = { scan, expire } } },
            },
        };
    }
}
