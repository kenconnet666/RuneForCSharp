using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf;

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
public class QRCode : FrameworkElement
{
    /// <summary>要编码的文字（链接、口令），按 UTF-8。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(QRCode),
        new FrameworkPropertyMetadata(null, (d, _) => ((QRCode)d).Encode()));

    /// <summary>纠错等级；不写时 M，有 logo 时 H。</summary>
    public static readonly DependencyProperty EccProperty = DependencyProperty.Register(nameof(Ecc), typeof(QrErrorCorrection?), typeof(QRCode),
        new FrameworkPropertyMetadata(null, (d, _) => ((QRCode)d).Encode()));

    /// <summary>四周静区的宽度（模块数），默认 2。</summary>
    public static readonly DependencyProperty QuietZoneProperty = DependencyProperty.Register(nameof(QuietZone), typeof(int), typeof(QRCode),
        new FrameworkPropertyMetadata(2, (d, _) => ((QRCode)d).Encode()), zone => (int)zone >= 0);

    /// <summary>模块的颜色；不写时是纸面的墨色。</summary>
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(QRCode),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((QRCode)d).UpdatePaper()));

    /// <summary>底色；不写时是纸面的底色。</summary>
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(QRCode),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((QRCode)d).UpdatePaper()));

    /// <summary>中间的 logo：图片（ImageSource）或任意元素（Avatar、Icon）。</summary>
    public static readonly DependencyProperty LogoProperty = DependencyProperty.Register(nameof(Logo), typeof(object), typeof(QRCode),
        new FrameworkPropertyMetadata(null, (d, _) => ((QRCode)d).OnLogoChanged()));

    /// <summary>logo 占边长的比例，默认 0.22；太大会扫不出来。</summary>
    public static readonly DependencyProperty LogoSizeProperty = DependencyProperty.Register(nameof(LogoSize), typeof(double), typeof(QRCode),
        new FrameworkPropertyMetadata(0.22, FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>状态，默认 Active。</summary>
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(QRCodeStatus), typeof(QRCode),
        new FrameworkPropertyMetadata(QRCodeStatus.Active, (d, _) => ((QRCode)d).UpdateOverlay()));

    /// <summary>字号（默认边长 10em 按它算）：同 TextElement.FontSize，继承外面的。</summary>
    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(QRCode),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));



    readonly VisualCollection children;
    readonly Border logoHost = new() { Visibility = Visibility.Collapsed };
    readonly Flex overlay = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Justify = FlexJustify.Center, Gap = Space.Xs };
    // 状态层盖在 logo 上面：90% 的纸面底色
    readonly Border veil = new();
    QrMatrix? matrix;
    Geometry? modules;
    EventHandler? refresh;

    static QRCode()
    {
        // 同 TS 的 inline-block：是自己的边长，不被父级拉伸
        HorizontalAlignmentProperty.OverrideMetadata(typeof(QRCode), new FrameworkPropertyMetadata(HorizontalAlignment.Left));
        VerticalAlignmentProperty.OverrideMetadata(typeof(QRCode), new FrameworkPropertyMetadata(VerticalAlignment.Top));
        Provider.LangProperty.OverrideMetadata(typeof(QRCode), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((QRCode)d).UpdateOverlay()));
    }

    /// <summary>新建。</summary>
    public QRCode()
    {
        children = new VisualCollection(this);
        veil.Child = overlay;
        AutomationProperties.SetLiveSetting(overlay, AutomationLiveSetting.Polite);
        foreach (var child in new UIElement[] { logoHost, veil })
        {
            children.Add(child);
            AddLogicalChild(child);
        }
        // 主题换了：纸面的颜色跟着换（深色主题下固定白底）
        Loaded += (_, _) => UpdatePaper();
        Encode();
        UpdateOverlay();
    }

    /// <summary>要编码的文字。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>纠错等级。</summary>
    public QrErrorCorrection? Ecc { get => (QrErrorCorrection?)GetValue(EccProperty); set => SetValue(EccProperty, value); }

    /// <summary>静区的宽度（模块数）。</summary>
    public int QuietZone { get => (int)GetValue(QuietZoneProperty); set => SetValue(QuietZoneProperty, value); }

    /// <summary>模块的颜色。</summary>
    public Brush? Foreground { get => (Brush?)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>底色。</summary>
    public Brush? Background { get => (Brush?)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>中间的 logo。</summary>
    public object? Logo { get => GetValue(LogoProperty); set => SetValue(LogoProperty, value); }

    /// <summary>logo 占边长的比例。</summary>
    public double LogoSize { get => (double)GetValue(LogoSizeProperty); set => SetValue(LogoSizeProperty, value); }

    /// <summary>状态。</summary>
    public QRCodeStatus Status { get => (QRCodeStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

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

    static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    void Encode()
    {
        matrix = string.IsNullOrEmpty(Value) ? null : QrEncoder.Encode(Value, Ecc ?? (Logo is null ? QrErrorCorrection.M : QrErrorCorrection.H));
        // 整个码是一条路径（一行里连续的深色模块合成一个矩形），坐标是模块数，画的时候再缩放
        modules = matrix is null ? null : Geometry.Parse(QrEncoder.SvgPath(matrix, QuietZone));
        modules?.Freeze();
        InvalidateVisual();
    }

    void OnLogoChanged()
    {
        logoHost.Child = Logo switch
        {
            ImageSource image => new System.Windows.Controls.Image { Source = image, Stretch = Stretch.Uniform },
            UIElement element => element,
            _ => null,
        };
        logoHost.Visibility = logoHost.Child is null ? Visibility.Collapsed : Visibility.Visible;
        // 有没有 logo 决定默认的纠错等级
        if (Ecc is null) Encode();
        UpdatePaper();
    }

    /// <summary>状态层：加载中转圈、已扫描对勾与提示、已过期提示与刷新按钮；正常时空着、不挡指针。</summary>
    void UpdateOverlay()
    {
        var lang = Provider.GetLang(this);
        overlay.Children.Clear();
        var status = new List<string>();
        switch (Status)
        {
            case QRCodeStatus.Loading:
                overlay.Children.Add(new Spinner { Size = TextSize.Lg, Label = lang.Loading });
                status.Add(lang.Loading);
                break;
            case QRCodeStatus.Scanned:
                overlay.Children.Add(new Icon { Data = Lucide.Check, Size = TextSize.Lg });
                overlay.Children.Add(new Text { Text = lang.QrScanned, Size = TextSize.Sm, Align = TextAlign.Center });
                status.Add(lang.QrScanned);
                break;
            case QRCodeStatus.Expired:
                overlay.Children.Add(new Text { Text = lang.QrExpired, Size = TextSize.Sm, Align = TextAlign.Center });
                status.Add(lang.QrExpired);
                if (refresh is not null)
                {
                    var button = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.RotateCw, Content = lang.QrRefresh };
                    button.Click += (_, _) => refresh?.Invoke(this, EventArgs.Empty);
                    overlay.Children.Add(button);
                }
                break;
        }
        veil.IsHitTestVisible = Status != QRCodeStatus.Active;
        AutomationProperties.SetName(overlay, string.Join(" ", status));
        UpdatePaper();
    }

    /// <summary>纸面的颜色（<see cref="Paper"/>）；写了 Background、Foreground 时用写的。</summary>
    (Brush Paper, Brush Ink) PaperColors() => (Background ?? Paper.Background(this), Foreground ?? Paper.Ink(this));

    void UpdatePaper()
    {
        var (paper, ink) = PaperColors();
        logoHost.Background = paper;
        veil.Background = Status == QRCodeStatus.Active ? null : paper is SolidColorBrush solid ? Freeze(new SolidColorBrush(solid.Color) { Opacity = 0.9 * solid.Opacity }) : paper;
        overlay.SetValue(TextElement.ForegroundProperty, ink);
        InvalidateVisual();
    }

    double Side => double.IsNaN(Width) ? 10 * FontSize : Width;

    /// <inheritdoc />
    protected override int VisualChildrenCount => children.Count;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => children[index];

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => children.GetEnumerator();

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var side = Side;
        logoHost.Measure(new Size(side * LogoSize, side * LogoSize));
        veil.Measure(new Size(side, side));
        return new Size(side, side);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var side = Math.Min(finalSize.Width, finalSize.Height);
        var logo = side * LogoSize;
        logoHost.Padding = new Thickness(logo * 0.03);
        logoHost.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * FontSize);
        logoHost.Arrange(new Rect((finalSize.Width - logo) / 2, (finalSize.Height - logo) / 2, logo, logo));
        veil.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Md) * FontSize);
        veil.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Sm) * FontSize);
        veil.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>纸面的圆角底（md）与模块；状态层的底色在 veil 上（要盖住 logo）。</summary>
    protected override void OnRender(DrawingContext context)
    {
        var (paper, ink) = PaperColors();
        var box = new Rect(RenderSize);
        var radius = this.Resource<double>(ThemeKeys.Radius.Md) * FontSize;
        context.DrawRoundedRectangle(paper, null, box, radius, radius);
        if (modules is null || Extent == 0) return;
        var scale = Math.Min(box.Width, box.Height) / Extent;
        context.PushTransform(new ScaleTransform(scale, scale));
        context.DrawGeometry(ink, null, modules);
        context.Pop();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new QRCodePeer(this);

    /// <summary>读屏：一张图，名称默认"二维码"；状态层（加载中、已过期、已扫描）是它的子节点；logo 不念。</summary>
    sealed class QRCodePeer(QRCode owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetClassNameCore() => nameof(QRCode);

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).QrCode;

        protected override List<AutomationPeer>? GetChildrenCore() => CreatePeerForElement(owner.veil) is { } veil ? [veil] : null;
    }
}
