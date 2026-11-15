using System.ComponentModel;
using System.Diagnostics;

namespace Rune.WinForms;

/// <summary>
/// 回到顶部（vue-tsx-pro 的 BackTop）：长内容滚下去之后，角落出现一个按钮，点了平滑滚回顶部（系统关闭动画时直接跳回）。
/// <code>
/// page.Controls.Add(new BackTopControl { Target = scrollArea, Threshold = 200 });
/// </code>
/// 是一个 <see cref="FloatButtonControl"/>（向上的箭头，名称与提示取语言包“回到顶部”）。滚动的容器是 <see cref="Target"/>；不写时是外层最近的 ScrollAreaControl，
/// 再没有时是窗体里的第一个（TS 的整页）。滚过 <see cref="Threshold"/>（默认 400，按 96 DPI 算）才出现（WinForms 的控件不能半透明，直接出现、不淡入），
/// 没出现时不占位。滚回去之后焦点移到容器开头第一个能聚焦的控件，键盘用户不用再从按钮的位置往回 Tab。
/// </summary>
[Description("回到顶部：滚过 Threshold 才出现的浮动按钮，点了平滑滚回顶部。")]
public class BackTopControl : FloatButtonControl
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    ScrollAreaControl? scroller;
    Control? watched;
    Stopwatch? clock;
    int from;

    /// <summary>新建：向上的箭头，没出现。</summary>
    public BackTopControl()
    {
        Icon = Lucide.ArrowUp;
        Click += (_, _) => ScrollToTop();
        timer.Tick += (_, _) => Step();
        UpdateShown();
    }

    /// <summary>滚动的容器；不写时找外层最近的，再没有时是窗体里的第一个。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ScrollAreaControl? Target { get; set { field = value; Attach(); } }

    /// <summary>滚过多少后出现（按 96 DPI 的像素），默认 400。</summary>
    [Category("Behavior")]
    [Description("滚过多少后出现（按 96 DPI 的像素）。")]
    [DefaultValue(400)]
    public int Threshold { get; set { field = value; UpdateShown(); } } = 400;

    /// <summary>出现着（滚过了 Threshold）。</summary>
    [Browsable(false)]
    public bool IsShown { get; private set; }

    /// <summary>名称默认取语言包。</summary>
    private protected override void SyncTip()
    {
        if (string.IsNullOrEmpty(AccessibleName) && Parent is not null) AccessibleName = ProviderControl.LangOf(this).BackToTop;
        else base.SyncTip();
    }

    /// <summary>
    /// 换了父控件：重新找滚动容器。没出现时自己是隐藏的、不建句柄，窗体里的第一个滚动容器等父控件建好句柄（窗体显示出来）再找。
    /// </summary>
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        SyncTip();
        Attach();
        if (watched is not null) watched.HandleCreated -= OnParentReady;
        watched = Parent;
        if (watched is not null) watched.HandleCreated += OnParentReady;
    }

    void OnParentReady(object? sender, EventArgs e) => watched?.BeginInvoke(Attach);

    void Attach()
    {
        var next = Target ?? Ancestor() ?? (FindForm() is { } form ? First(form) : null);
        if (scroller == next) return;
        if (scroller is not null) scroller.ScrollChanged -= OnScrolled;
        scroller = next;
        if (scroller is not null) scroller.ScrollChanged += OnScrolled;
        UpdateShown();
    }

    ScrollAreaControl? Ancestor()
    {
        for (var node = Parent; node is not null; node = node.Parent)
            if (node is ScrollAreaControl area) return area;
        return null;
    }

    static ScrollAreaControl? First(Control parent)
    {
        foreach (Control child in parent.Controls)
            if ((child as ScrollAreaControl ?? First(child)) is { } found) return found;
        return null;
    }

    void OnScrolled(object? sender, EventArgs e) => UpdateShown();

    /// <summary>滚过了就出现，没出现时不占位、不能点。</summary>
    void UpdateShown()
    {
        IsShown = (scroller?.ScrollOffset.Y ?? 0) > LogicalToDeviceUnits(Threshold);
        Visible = IsShown;
    }

    /// <summary>平滑滚回顶部（lg、先慢后快再慢；系统关闭动画时直接跳回），到顶后焦点移到开头。</summary>
    void ScrollToTop()
    {
        if (scroller is null) return;
        from = scroller.ScrollOffset.Y;
        if (!Motion.Enabled)
        {
            Finish();
            return;
        }
        clock = Stopwatch.StartNew();
        timer.Start();
    }

    void Step()
    {
        if (scroller is null || clock is null) return;
        var t = Math.Min(1, clock.Elapsed / Tokens.Duration.Lg);
        var eased = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        scroller.ScrollOffset = scroller.ScrollOffset with { Y = (int)Math.Round(from * (1 - eased)) };
        if (t >= 1) Finish();
    }

    void Finish()
    {
        timer.Stop();
        clock = null;
        if (scroller is null) return;
        scroller.ScrollOffset = scroller.ScrollOffset with { Y = 0 };
        // 到顶之后再聚焦：聚焦会把控件滚进视野，先聚焦就跳过了平滑滚动
        scroller.SelectNextControl(null, true, true, true, false);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            if (watched is not null) watched.HandleCreated -= OnParentReady;
            if (scroller is not null) scroller.ScrollChanged -= OnScrolled;
        }
        base.Dispose(disposing);
    }
}
