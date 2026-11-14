using System.ComponentModel;
using System.Drawing.Drawing2D;
using Microsoft.Web.WebView2.WinForms;

namespace Rune.WinForms;

/// <summary>
/// EmbedControl、VideoControl 共用的外壳：一块（可定比例的）框，里面放网页内核（WebView2）；第一次加载完之前骨架盖在内核上面
/// （不少页面加载中是一片白，暗色主题里很刺眼）。内核是子窗口，按外框的圆角设窗口区域裁掉四角。
/// 内核的用户数据放在 %LocalAppData%\Rune\WebView2（默认放在程序旁边，装在 Program Files 里时写不进去）。
/// </summary>
public abstract class WebShellControl : Panel, IScopeAware
{
    static readonly string DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rune", "WebView2");

    readonly Effect paint;
    readonly CornerClip cornerClip = new();
    readonly SkeletonControl placeholder = new() { Shape = SkeletonShape.Image, AutoSize = false };

    /// <summary>新建：定比例（16 / 9），骨架盖在内核上面。</summary>
    protected WebShellControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AccessibleRole = AccessibleRole.Grouping;
        paint = new Effect(Invalidate);
        View = new WebView2 { CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = DataFolder } };
        View.NavigationCompleted += (_, e) =>
        {
            if (PageReady) return;
            PageReady = true;
            placeholder.Visible = false;
            OnPageLoaded(e.IsSuccess);
        };
        // 内核不能藏起来等：隐藏的子控件不建窗口，也就不加载；改用骨架（同级、在上面）盖住它
        Controls.Add(View);
        Controls.Add(placeholder);
        placeholder.BringToFront();
    }

    /// <summary>宽高比（宽 ÷ 高），默认 16 / 9；null 时不定比例，高度由 Height 给。</summary>
    [Category("Layout")]
    [Description("宽高比（宽 ÷ 高）；null 时不定比例，自己给高度。")]
    public double? Ratio
    {
        get;
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "宽高比要大于 0");
            field = value;
            AutoSize = value is not null;
            PerformLayout();
        }
    } = 16d / 9;

    bool ShouldSerializeRatio() => Ratio != 16d / 9;

    void ResetRatio() => Ratio = 16d / 9;

    /// <summary>网页内核。</summary>
    protected WebView2 View { get; }

    /// <summary>外框画不画描边（xs）。</summary>
    protected bool Outlined { get; init; }

    /// <summary>加载完了（第一次导航结束，成功或失败）。</summary>
    internal bool PageReady { get; private set; }

    /// <summary>加载中的骨架（测试用）。</summary>
    internal SkeletonControl Placeholder => placeholder;

    float Em => this.EmPixels();

    int Line => Outlined ? Math.Max(1, (int)MathF.Round(Tokens.BorderWidth.Xs * DeviceDpi / 96f)) : 0;

    float CornerRadius => (Radius.Md.In(ProviderControl.ThemeOf(this).Radii) ?? 0) * Em;

    /// <summary>重新加载：内容变了（换了地址）时回到骨架。</summary>
    protected void Reload()
    {
        PageReady = false;
        placeholder.Visible = true;
        Load(View);
    }

    /// <summary>让内核加载内容（导航到地址）。</summary>
    protected abstract void Load(WebView2 view);

    /// <summary>第一次加载完。</summary>
    protected virtual void OnPageLoaded(bool success)
    {
    }

    /// <summary>外框的底色与描边色（主题变了会重画）。</summary>
    protected abstract (Color Fill, Color Border) FrameColors(ThemeColors colors);

    /// <summary>宽度取布局给的宽度，高度 = 宽度 ÷ 比例；不定比例时不自动大小（AutoSize 为 false）。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (Ratio is not { } ratio) return Size;
        var width = VerticalStack.Limit(this, proposedSize);
        if (width == int.MaxValue) width = Width;
        return new Size(width, (int)Math.Round(width / ratio));
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var line = Line;
        var inner = new Rectangle(line, line, Math.Max(0, Width - 2 * line), Math.Max(0, Height - 2 * line));
        View.Bounds = placeholder.Bounds = inner;
        cornerClip.Apply([View, placeholder], inner, Math.Max(0, CornerRadius - line));
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var (fill, border) = FrameColors(ProviderControl.ThemeOf(this).Colors);
        var graphics = e.Graphics;
        var line = Line;
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var shape = Drawing.RoundedRectangle(new RectangleF(line / 2f, line / 2f, Width - line, Height - line), CornerRadius))
        {
            using (var brush = new SolidBrush(fill)) graphics.FillPath(brush, shape);
            if (line > 0)
                using (var pen = new Pen(border, line)) graphics.DrawPath(pen, shape);
        }
        graphics.Restore(state);
    }

    /// <summary>所在作用域的主题变了：圆角、颜色重算。</summary>
    void IScopeAware.ScopeChanged()
    {
        PerformLayout();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 换了位置可能换了作用域
        ((IScopeAware)this).ScopeChanged();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        PerformLayout();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }
}
