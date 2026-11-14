using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>二维码的状态（vue-tsx-pro 的 QRCodeStatus）。</summary>
public enum QRCodeStatus
{
    /// <summary>正常显示（默认）。</summary>
    Active,

    /// <summary>加载中：盖一层转圈。</summary>
    Loading,

    /// <summary>已过期：盖一层提示；订阅了 RefreshRequested 时有"刷新"按钮。</summary>
    Expired,

    /// <summary>已扫描：盖一层对勾与"请在手机上确认"。</summary>
    Scanned,
}

/// <summary>
/// 二维码（vue-tsx-pro 的 QRCode）：把文字（链接、口令）画成二维码；中间可以放 logo；扫码登录这类场景有加载中、已过期（可刷新）、已扫描的状态。
/// <code>
/// var code = new QRCodeControl { Value = "https://example.com" };
/// var login = new QRCodeControl { Value = ticket, Status = QRCodeStatus.Expired };
/// login.RefreshRequested += (_, _) =&gt; Renew();
/// </code>
/// 编码器是孪生文件 <see cref="QrEncoder"/>：UTF-8 字节模式，自动选最小的版本与罚分最低的掩码；有 logo 时纠错默认 H（logo 默认占边长的 22%）。
/// 四周留 <see cref="QuietZone"/> 个模块的静区（默认 2）。边长默认 10em（AutoSize）；要别的边长关掉 AutoSize 写 Size。
/// 颜色是"纸面"：浅色主题下是主题的 surface 底、text 色模块，深色主题下仍是白底深色模块（反色的二维码很多扫码器认不出）；Foreground、Background 可以改
/// （ForeColor、BackColor 是跟着父级的环境属性，分不出设没设过，所以另起两个）。读屏：一张图，名称默认"二维码"，状态在描述里。
/// </summary>
[DefaultProperty(nameof(Value))]
[DefaultEvent(nameof(RefreshRequested))]
[Description("二维码：把文字画成二维码，可以放 logo、显示扫码登录的状态。")]
public class QRCodeControl : Control, IScopeAware
{
    /// <summary>纸面在深色主题下的墨色（底色是白色）。</summary>
    static readonly Color DarkInk = Color.FromArgb(0x1c, 0x20, 0x24);

    readonly FlexControl overlay = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Xs, AutoSize = true, BackColor = Color.Transparent, Visible = false };
    QrMatrix? matrix;
    GraphicsPath? modules;
    Control? logoControl;
    EventHandler? refresh;

    /// <summary>新建：边长 10em。</summary>
    public QRCodeControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        AutoSize = true;
        Controls.Add(overlay);
    }

    /// <summary>要编码的文字（链接、口令），按 UTF-8。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value { get; set { field = value; Encode(); } }

    /// <summary>纠错等级；不写时 M，有 logo 时 H。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public QrErrorCorrection? Ecc { get; set { field = value; Encode(); } }

    /// <summary>四周静区的宽度（模块数），默认 2。</summary>
    [Category("Appearance")]
    [DefaultValue(2)]
    public int QuietZone
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
            Encode();
        }
    } = 2;

    /// <summary>模块的颜色；不写时是纸面的墨色。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public Color? Foreground { get; set { field = value; Invalidate(true); } }

    /// <summary>底色；不写时是纸面的底色。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public Color? Background { get; set { field = value; Invalidate(true); } }

    /// <summary>中间的 logo：图片（Image）或任意控件（AvatarControl、IconControl）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? Logo
    {
        get;
        set
        {
            if (logoControl is not null) Controls.Remove(logoControl);
            field = value;
            logoControl = value as Control;
            if (logoControl is not null) Controls.Add(logoControl);
            // 有没有 logo 决定默认的纠错等级
            Encode();
            PerformLayout();
        }
    }

    /// <summary>logo 占边长的比例，默认 0.22；太大会扫不出来。</summary>
    [Category("Appearance")]
    [DefaultValue(0.22)]
    public double LogoSize { get; set { field = value; PerformLayout(); Invalidate(); } } = 0.22;

    /// <summary>状态，默认 Active。</summary>
    [Category("Behavior")]
    [DefaultValue(QRCodeStatus.Active)]
    public QRCodeStatus Status { get; set { field = value; UpdateOverlay(); } }

    /// <summary>已过期时点"刷新"（TS 的 onRefresh；Control 自己有 Refresh 方法，所以叫 RefreshRequested）：订阅了才显示刷新按钮。</summary>
    [Category("Action")]
    public event EventHandler? RefreshRequested
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
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public QrMatrix? Matrix => matrix;

    /// <summary>边长（模块数，含两边的静区）；没有内容时是 0。</summary>
    [Browsable(false)]
    public int Extent => matrix is null ? 0 : matrix.Size + 2 * QuietZone;

    /// <summary>状态层（测试用）。</summary>
    internal FlexControl OverlayPart => overlay;

    /// <summary>状态层读屏念的文字（测试用）。</summary>
    internal string StatusText { get; private set; } = "";

    float Em => this.EmPixels();

    void IScopeAware.ScopeChanged()
    {
        UpdateOverlay();
        Invalidate(true);
    }

    void Encode()
    {
        matrix = string.IsNullOrEmpty(Value) ? null : QrEncoder.Encode(Value, Ecc ?? (Logo is null ? QrErrorCorrection.M : QrErrorCorrection.H));
        modules?.Dispose();
        modules = matrix is null ? null : ModulesPath(matrix, QuietZone);
        Invalidate();
    }

    /// <summary>一行里连续的深色模块合成一个矩形，整个码是一条路径（同 TS 的 modulesPath）；坐标是模块数，画的时候再缩放。</summary>
    static GraphicsPath ModulesPath(QrMatrix qr, int margin)
    {
        var path = new GraphicsPath();
        for (var y = 0; y < qr.Size; y++)
            for (var x = 0; x < qr.Size; x++)
            {
                if (!qr.IsDark(x, y)) continue;
                var run = 1;
                while (qr.IsDark(x + run, y)) run++;
                path.AddRectangle(new RectangleF(x + margin, y + margin, run, 1));
                x += run;
            }
        return path;
    }

    /// <summary>状态层：加载中转圈、已扫描对勾与提示、已过期提示与刷新按钮；正常时隐藏。状态写进读屏的描述并通知。</summary>
    void UpdateOverlay()
    {
        var lang = ProviderControl.LangOf(this);
        foreach (Control child in overlay.Controls.Cast<Control>().ToList()) child.Dispose();
        var status = Status switch
        {
            QRCodeStatus.Loading => lang.Loading,
            QRCodeStatus.Scanned => lang.QrScanned,
            QRCodeStatus.Expired => lang.QrExpired,
            _ => "",
        };
        switch (Status)
        {
            case QRCodeStatus.Loading:
                overlay.Controls.Add(new SpinnerControl { IconSize = TextSize.Lg, Label = lang.Loading });
                break;
            case QRCodeStatus.Scanned:
                overlay.Controls.Add(new IconControl { Data = Lucide.Check, IconSize = TextSize.Lg });
                overlay.Controls.Add(new SmallControl { Text = status, TextSize = TextSize.Sm, Align = TextAlign.Center });
                break;
            case QRCodeStatus.Expired:
                overlay.Controls.Add(new SmallControl { Text = status, TextSize = TextSize.Sm, Align = TextAlign.Center });
                if (refresh is not null)
                {
                    var button = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.RotateCw, Text = lang.QrRefresh };
                    button.Click += (_, _) => refresh?.Invoke(this, EventArgs.Empty);
                    overlay.Controls.Add(button);
                }
                break;
        }
        // 状态层的字是纸面的墨色（深色主题下底也是白的）
        foreach (Control child in overlay.Controls) child.ForeColor = Ink;
        overlay.Visible = Status != QRCodeStatus.Active;
        if (StatusText != status)
        {
            StatusText = status;
            if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.DescriptionChange, -1);
        }
        PerformLayout();
        Invalidate(true);
    }

    bool DarkTheme => ProviderControl.ThemeOf(this).IsDark;

    Color Paper => Background ?? (DarkTheme ? Color.White : ProviderControl.ThemeOf(this).Colors.Surface);

    Color Ink => Foreground ?? (DarkTheme ? DarkInk : ProviderControl.ThemeOf(this).Colors.Text);

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        var side = (int)Math.Round(10 * Em);
        return new Size(side, side);
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (AutoSize) Size = GetPreferredSize(Size.Empty);
    }

    float Side => Math.Min(Width, Height);

    RectangleF LogoBox()
    {
        var logo = (float)(Side * LogoSize);
        return new RectangleF((Width - logo) / 2, (Height - logo) / 2, logo, logo);
    }

    /// <summary>logo 控件放在底板里（留 3% 的边）；状态层居中。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (logoControl is not null)
        {
            var box = RectangleF.Inflate(LogoBox(), -LogoBox().Width * 0.03f, -LogoBox().Height * 0.03f);
            var size = logoControl.AutoSize ? logoControl.GetPreferredSize(Size.Empty) : Size.Round(box.Size);
            logoControl.Bounds = new Rectangle((int)(box.X + (box.Width - size.Width) / 2), (int)(box.Y + (box.Height - size.Height) / 2), size.Width, size.Height);
            logoControl.Visible = Status == QRCodeStatus.Active;
        }
        var overlaySize = overlay.GetPreferredSize(new Size(Width, 0));
        overlay.Bounds = new Rectangle((Width - overlaySize.Width) / 2, (Height - overlaySize.Height) / 2, overlaySize.Width, overlaySize.Height);
    }

    /// <summary>纸面的圆角底（md）、模块、logo 底板（图片 logo 画在上面）；有状态时盖一层 90% 的纸面底色。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var theme = ProviderControl.ThemeOf(this);
        var radius = (Radius.Md.In(theme.Radii) ?? 0) * Em;
        var box = new RectangleF(0, 0, Side, Side);
        using (var paper = new SolidBrush(Paper))
        using (var path = Drawing.RoundedRectangle(box, radius))
            graphics.FillPath(paper, path);
        if (modules is not null && Extent > 0)
        {
            var state = graphics.Save();
            // 模块边缘不抗锯齿（同 TS 的 shape-rendering: crispEdges）
            graphics.SmoothingMode = SmoothingMode.None;
            var scale = Side / Extent;
            graphics.ScaleTransform(scale, scale);
            using var ink = new SolidBrush(Ink);
            graphics.FillPath(ink, modules);
            graphics.Restore(state);
        }
        if (Logo is not null)
        {
            var logo = LogoBox();
            using (var plate = new SolidBrush(Paper))
            using (var path = Drawing.RoundedRectangle(logo, (Radius.Sm.In(theme.Radii) ?? 0) * Em))
                graphics.FillPath(plate, path);
            if (Logo is Image image) graphics.DrawImage(image, RectangleF.Inflate(logo, -logo.Width * 0.03f, -logo.Height * 0.03f));
        }
        if (Status == QRCodeStatus.Active) return;
        using (var veil = new SolidBrush(Color.FromArgb((int)(255 * 0.9), Paper)))
        using (var path = Drawing.RoundedRectangle(box, radius))
            graphics.FillPath(veil, path);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) modules?.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new QRCodeAccessibleObject(this);

    /// <summary>读屏：一张图，名称默认"二维码"；状态（加载中、已过期、已扫描）是描述，刷新按钮是子控件。</summary>
    sealed class QRCodeAccessibleObject(QRCodeControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Graphic;

        public override string? Name => owner.AccessibleName is { Length: > 0 } name ? name : ProviderControl.LangOf(owner).QrCode;

        public override string? Description => owner.StatusText is { Length: > 0 } status ? status : owner.AccessibleDescription;
    }
}
