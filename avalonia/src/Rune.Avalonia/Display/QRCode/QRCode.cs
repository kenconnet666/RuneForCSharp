using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>二维码的状态（vue-tsx-pro 的 QRCodeStatus）。</summary>
public enum QRCodeStatus
{
    /// <summary>正常显示（默认）。</summary>
    Active,

    /// <summary>加载中：盖一层转圈。</summary>
    Loading,

    /// <summary>已过期：盖一层提示；订阅了 Refresh 时有"刷新"按钮。</summary>
    Expired,

    /// <summary>已扫描：盖一层对勾与"请在手机上确认"。</summary>
    Scanned,
}

/// <summary>
/// 二维码（vue-tsx-pro 的 QRCode）：把文字（链接、口令）画成二维码；中间可以放 logo；扫码登录这类场景有加载中、已过期（可刷新）、已扫描的状态。
/// <code>
/// &lt;rune:QRCode Value="https://example.com" /&gt;
/// &lt;rune:QRCode Value="{Binding Url}" Logo="{Binding Logo}" Width="192" /&gt;
/// &lt;rune:QRCode Value="{Binding Ticket}" Status="{Binding State}" Refresh="OnRenew" /&gt;
/// </code>
/// 编码器是孪生文件 <see cref="QrEncoder"/>：UTF-8 字节模式，自动选最小的版本与罚分最低的掩码；有 logo 时纠错默认 H（logo 默认占边长的 22%）。
/// 四周留 <see cref="QuietZone"/> 个模块的静区（默认 2）。边长默认 10em，写 Width 改。
/// 颜色是"纸面"：浅色主题下是主题的 surface 底、text 色模块，深色主题下仍是白底深色模块（反色的二维码很多扫码器认不出）；Foreground、Background 可以改。
/// 读屏：一张图，名称默认"二维码"（写 AutomationProperties.Name 改）；状态层换状态时播报。
/// </summary>
public class QRCode : Control
{
    /// <summary>要编码的文字（链接、口令），按 UTF-8。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<QRCode, string?>(nameof(Value));

    /// <summary>纠错等级；不写时 M，有 logo 时 H。</summary>
    public static readonly StyledProperty<QrErrorCorrection?> EccProperty = AvaloniaProperty.Register<QRCode, QrErrorCorrection?>(nameof(Ecc));

    /// <summary>四周静区的宽度（模块数），默认 2。</summary>
    public static readonly StyledProperty<int> QuietZoneProperty = AvaloniaProperty.Register<QRCode, int>(nameof(QuietZone), 2, validate: zone => zone >= 0);

    /// <summary>模块的颜色；不写时是纸面的墨色。</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<QRCode, IBrush?>(nameof(Foreground));

    /// <summary>底色；不写时是纸面的底色。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<QRCode, IBrush?>(nameof(Background));

    /// <summary>中间的 logo：图片（IImage）或任意控件（Avatar、Icon）。</summary>
    public static readonly StyledProperty<object?> LogoProperty = AvaloniaProperty.Register<QRCode, object?>(nameof(Logo));

    /// <summary>logo 占边长的比例，默认 0.22；太大会扫不出来。</summary>
    public static readonly StyledProperty<double> LogoSizeProperty = AvaloniaProperty.Register<QRCode, double>(nameof(LogoSize), 0.22);

    /// <summary>状态，默认 Active。</summary>
    public static readonly StyledProperty<QRCodeStatus> StatusProperty = AvaloniaProperty.Register<QRCode, QRCodeStatus>(nameof(Status));

    /// <summary>字号（默认边长 10em 按它算）：同 TextElement.FontSize，继承外面的。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<QRCode>();



    readonly Border logoHost = new() { IsVisible = false, Name = QRCodeParts.Logo };
    readonly Flex overlay = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Justify = FlexJustify.Center, Gap = Space.Xs };
    // 状态层盖在 logo 上面：90% 的纸面底色
    readonly Border veil = new() { Name = QRCodeParts.Overlay };
    QrMatrix? matrix;
    Geometry? modules;
    EventHandler? refresh;

    static QRCode()
    {
        AffectsRender<QRCode>(ForegroundProperty, BackgroundProperty);
        AffectsMeasure<QRCode>(FontSizeProperty);
        // 同 TS 的 inline-block：是自己的边长，不被父级拉伸
        HorizontalAlignmentProperty.OverrideDefaultValue<QRCode>(HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<QRCode>(VerticalAlignment.Top);
    }

    /// <summary>新建。</summary>
    public QRCode()
    {
        veil.Child = overlay;
        AutomationProperties.SetLiveSetting(overlay, AutomationLiveSetting.Polite);
        foreach (var child in new Control[] { logoHost, veil })
        {
            LogicalChildren.Add(child);
            VisualChildren.Add(child);
        }
        this.RedrawOnResourceChanges(this);
        // 深浅主题换了：纸面的底色跟着换（深色主题下固定白底）
        ActualThemeVariantChanged += (_, _) =>
        {
            UpdatePaper();
            InvalidateVisual();
        };
        Encode();
        UpdateOverlay();
    }

    /// <summary>要编码的文字。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>纠错等级。</summary>
    public QrErrorCorrection? Ecc { get => GetValue(EccProperty); set => SetValue(EccProperty, value); }

    /// <summary>静区的宽度（模块数）。</summary>
    public int QuietZone { get => GetValue(QuietZoneProperty); set => SetValue(QuietZoneProperty, value); }

    /// <summary>模块的颜色。</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>底色。</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>中间的 logo。</summary>
    public object? Logo { get => GetValue(LogoProperty); set => SetValue(LogoProperty, value); }

    /// <summary>logo 占边长的比例。</summary>
    public double LogoSize { get => GetValue(LogoSizeProperty); set => SetValue(LogoSizeProperty, value); }

    /// <summary>状态。</summary>
    public QRCodeStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>已过期时点"刷新"（TS 的 onRefresh）：订阅了才显示刷新按钮。</summary>
    public event EventHandler? Refresh
    {
        add
        {
            refresh += value;
            UpdateOverlay();
        }
        remove
        {
            refresh -= value;
            UpdateOverlay();
        }
    }

    /// <summary>编码结果；没有内容时为 null。</summary>
    public QrMatrix? Matrix => matrix;

    /// <summary>边长（模块数，含两边的静区）；没有内容时是 0。</summary>
    public int Extent => matrix is null ? 0 : matrix.Size + 2 * QuietZone;

    /// <summary>状态层（测试用）。</summary>
    internal Flex OverlayPart => overlay;

    /// <summary>logo 底板（测试用）。</summary>
    internal Border LogoPart => logoHost;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == EccProperty || change.Property == QuietZoneProperty) Encode();
        else if (change.Property == LogoProperty)
        {
            UpdateLogo();
            // 有没有 logo 决定默认的纠错等级
            if (Ecc is null) Encode();
        }
        else if (change.Property == LogoSizeProperty) InvalidateArrange();
        else if (change.Property == StatusProperty || change.Property == Provider.LangProperty) UpdateOverlay();
    }

    void Encode()
    {
        matrix = string.IsNullOrEmpty(Value) ? null : QrEncoder.Encode(Value, Ecc ?? (Logo is null ? QrErrorCorrection.M : QrErrorCorrection.H));
        // 整个码是一条路径（一行里连续的深色模块合成一个矩形），坐标是模块数，画的时候再缩放
        modules = matrix is null ? null : Geometry.Parse(QrEncoder.SvgPath(matrix, QuietZone));
        InvalidateVisual();
    }

    void UpdateLogo()
    {
        logoHost.Child = Logo switch
        {
            IImage image => new global::Avalonia.Controls.Image { Source = image, Stretch = Stretch.Uniform },
            Control control => control,
            _ => null,
        };
        logoHost.IsVisible = logoHost.Child is not null;
        // logo 对读屏隐藏：名称已经是"二维码"
        AutomationProperties.SetAccessibilityView(logoHost, AccessibilityView.Raw);
        UpdatePaper();
    }

    /// <summary>状态层：加载中转圈、已扫描对勾与提示、已过期提示与刷新按钮；正常时空着、不挡指针。</summary>
    void UpdateOverlay()
    {
        var lang = Provider.GetLang(this);
        overlay.Children.Clear();
        switch (Status)
        {
            case QRCodeStatus.Loading:
                overlay.Children.Add(new Spinner { Size = TextSize.Lg, Label = lang.Loading });
                break;
            case QRCodeStatus.Scanned:
                overlay.Children.Add(new Icon { Data = Lucide.Check, Size = TextSize.Lg });
                overlay.Children.Add(new Text { Text = lang.QrScanned, Size = TextSize.Sm, Align = TextAlign.Center });
                break;
            case QRCodeStatus.Expired:
                overlay.Children.Add(new Text { Text = lang.QrExpired, Size = TextSize.Sm, Align = TextAlign.Center });
                if (refresh is not null)
                {
                    var button = new Button { Name = QRCodeParts.Refresh, Size = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.RotateCw, Content = lang.QrRefresh };
                    button.Click += (_, _) => refresh?.Invoke(this, EventArgs.Empty);
                    overlay.Children.Add(button);
                }
                break;
        }
        var shown = Status != QRCodeStatus.Active;
        veil.IsHitTestVisible = shown;
        AutomationProperties.SetName(overlay, shown ? string.Join(" ", overlay.Children.Select(StatusText).Where(text => text is { Length: > 0 })) : null);
        UpdatePaper();

        string? StatusText(Control child) => child switch
        {
            Spinner => lang.Loading,
            Text text => text.Text,
            _ => null,
        };
    }

    /// <summary>纸面的颜色（<see cref="Paper"/>）；写了 Background、Foreground 时用写的。</summary>
    (IBrush Paper, IBrush Ink) PaperColors() => (Background ?? Paper.Background(this), Foreground ?? Paper.Ink(this));

    void UpdatePaper()
    {
        var (paper, ink) = PaperColors();
        logoHost.Background = paper;
        veil.Background = Status == QRCodeStatus.Active ? null : paper is ISolidColorBrush solid ? new ImmutableSolidColorBrush(solid.Color, 0.9 * solid.Opacity) : paper;
        overlay.SetValue(TextElement.ForegroundProperty, ink);
    }

    double Side => double.IsNaN(Width) ? 10 * FontSize : Width;

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var side = Side;
        logoHost.Measure(new LayoutSize(side * LogoSize, side * LogoSize));
        veil.Measure(new LayoutSize(side, side));
        return new LayoutSize(side, side);
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        var side = Math.Min(finalSize.Width, finalSize.Height);
        var logo = side * LogoSize;
        logoHost.Padding = new Thickness(logo * 0.03);
        logoHost.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * FontSize);
        logoHost.Arrange(new Rect((finalSize.Width - logo) / 2, (finalSize.Height - logo) / 2, logo, logo));
        var radius = this.Resource<double>(ThemeKeys.Radius.Md) * FontSize;
        veil.CornerRadius = new CornerRadius(radius);
        veil.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Sm) * FontSize);
        veil.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>纸面的圆角底（md）与模块；状态层的底色在 veil 上（要盖住 logo）。</summary>
    public override void Render(DrawingContext context)
    {
        var (paper, ink) = PaperColors();
        var box = new Rect(Bounds.Size);
        var radius = this.Resource<double>(ThemeKeys.Radius.Md) * FontSize;
        context.DrawRectangle(paper, null, new RoundedRect(box, radius));
        if (modules is not null && Extent > 0)
        {
            var scale = Math.Min(box.Width, box.Height) / Extent;
            using (context.PushTransform(global::Avalonia.Matrix.CreateScale(scale, scale)))
                context.DrawGeometry(ink, null, modules);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new QRCodePeer(this);

    /// <summary>读屏：一张图，名称默认"二维码"；状态层（加载中、已过期、已扫描）是它的子节点。</summary>
    sealed class QRCodePeer(QRCode owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetClassNameCore() => nameof(QRCode);

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).QrCode;
    }
}

/// <summary>QRCode 的部件名：样式里用 <c>rune|QRCode Border#PART_Logo</c> 这样选中。</summary>
public static class QRCodeParts
{
    /// <summary>logo 底板（TS 的 propsLogo）。</summary>
    public const string Logo = "PART_Logo";

    /// <summary>状态层（TS 的 propsOverlay）。</summary>
    public const string Overlay = "PART_Overlay";

    /// <summary>刷新按钮（TS 的 propsRefresh）。</summary>
    public const string Refresh = "PART_Refresh";
}
