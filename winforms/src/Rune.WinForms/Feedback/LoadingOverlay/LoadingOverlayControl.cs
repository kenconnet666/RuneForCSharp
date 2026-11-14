using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 加载遮罩（vue-tsx-pro 的 LoadingOverlay）：盖住一块区域（卡片、表单、图表），内容保留在下面，中间显示加载指示。
/// <code>
/// var area = new LoadingOverlayControl { Label = "正在保存" };
/// area.Controls.Add(formCard);
/// area.IsLoading = true;   // 保存时
/// </code>
/// 读屏的"忙"按 <see cref="IsLoading"/> 立即生效（AccessibleStates.Busy，说明是 <see cref="Label"/>，默认"加载中"）；遮罩防闪烁（<see cref="DelayedLoading"/>：
/// 200 毫秒内完成的不出现，出现后至少显示 500 毫秒），durations.md 内淡入。子控件各有自己的窗口，容器画不到它们上面：遮罩画在一个属于窗体的分层窗口里
/// （同 <see cref="WatermarkControl"/>），盖住这个控件看得见的部分，跟着移动、缩放、显示隐藏；它挡住指针（加载一开始就挡：遮罩出现前是一层几乎全透明的），
/// 键盘仍能操作下面的内容，需要禁止时由内容自己处理（表单的 Enabled）。遮罩是 surface 70% 的底、居中 xl 的 accent 加载指示（每秒约 30 帧转动）。
/// 出入：没有模糊（画不到别的窗口的内容上）；离场不淡出。
/// </summary>
[Description("加载遮罩：盖住一块区域，内容保留在下面，中间显示加载指示。")]
public class LoadingOverlayControl : Panel, IScopeAware
{
    /// <summary>加载指示转一圈的时间（同 Spinner）。</summary>
    static readonly TimeSpan Period = TimeSpan.FromMilliseconds(800);

    readonly LayeredWindow overlay = new(topmost: false);
    readonly List<Control> watched = [];
    readonly VerticalStack layout = new(() => 0);
    readonly System.Windows.Forms.Timer frames = new() { Interval = 33 };
    readonly System.Diagnostics.Stopwatch spin = System.Diagnostics.Stopwatch.StartNew();
    DelayedLoading delayed;
    Form? form;

    /// <summary>新建：透明底，子控件竖着排（同网页里包着内容的 div），大小跟着内容。</summary>
    public LoadingOverlayControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        delayed = new DelayedLoading(_ => Present());
        frames.Tick += (_, _) => Present();
    }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => layout;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => layout.Measure(this, proposedSize);

    /// <summary>加载中：盖上遮罩。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsLoading
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            delayed.Update(value);
            Present();
        }
    }

    /// <summary>读屏念的说明；不写时取语言包（加载中）。</summary>
    [Category("Accessibility")]
    [DefaultValue(null)]
    public string? Label { get; set; }

    /// <summary>遮罩出现了（测试用：过了 delay 之后）。</summary>
    internal bool OverlayShown => delayed.Shown && overlay.Visible;

    /// <summary>挡指针的窗口在（测试用：加载一开始就在）。</summary>
    internal bool Blocking => overlay.Visible;

    /// <summary>画遮罩的分层窗口（测试用）。</summary>
    internal LayeredWindow Overlay => overlay;

    void IScopeAware.ScopeChanged() => Present();

    /// <summary>
    /// 遮罩窗口盖住控件看得见的部分：加载中、遮罩还没到时是几乎全透明的一层（只为挡指针），到了以后画底色与加载指示并淡入；
    /// 没在加载、遮罩也走了，或看不见时藏起来。
    /// </summary>
    void Present()
    {
        var shown = delayed.Shown;
        if (!IsLoading && !shown || form is not { Visible: true, IsHandleCreated: true } || form.WindowState == FormWindowState.Minimized || !Visible || !IsHandleCreated)
        {
            frames.Stop();
            overlay.Dismiss();
            return;
        }
        var area = VisibleArea(RectangleToScreen(ClientRectangle));
        if (area.Width <= 0 || area.Height <= 0)
        {
            overlay.Dismiss();
            return;
        }
        if (overlay.Owner != form) overlay.Owner = form;
        var bitmap = Render(area.Size, shown);
        // 刚出现时淡入；转动的每一帧只换画面，不重新淡入
        var appearing = shown && !frames.Enabled;
        if (appearing || !overlay.Visible || overlay.Bounds != area)
            overlay.Present(bitmap, area.Location, new Rectangle(area.Location, area.Size), null, appearing && Motion.Enabled ? Tokens.Duration.Md : TimeSpan.Zero);
        else overlay.Redraw(bitmap);
        if (shown) frames.Start();
        else frames.Stop();
    }

    /// <summary>画一帧：遮罩出现前是 1/255 不透明的底（看不见，但分层窗口只有不透明的像素才接点击）；出现后是 surface 70% 的底与转动的加载指示。</summary>
    Bitmap Render(Size size, bool shown)
    {
        var colors = ProviderControl.ThemeOf(this).Colors;
        var bitmap = LayeredWindow.Canvas(size);
        using var graphics = Graphics.FromImage(bitmap);
        if (!shown)
        {
            graphics.Clear(Color.FromArgb(1, colors.Surface));
            return bitmap;
        }
        graphics.Clear(Color.FromArgb((int)Math.Round(255 * 0.7), colors.Surface));
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var em = Tokens.FontSize.Xl * DeviceDpi / 96f;
        var angle = (float)(spin.Elapsed.TotalMilliseconds % Period.TotalMilliseconds / Period.TotalMilliseconds * 360);
        graphics.TranslateTransform(size.Width / 2f, size.Height / 2f);
        graphics.RotateTransform(angle);
        graphics.TranslateTransform(-em / 2, -em / 2);
        graphics.ScaleTransform(em / IconData.Size, em / IconData.Size);
        using var path = Lucide.LoaderCircle.CreatePath();
        using var pen = new Pen(colors.Accent, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        graphics.DrawPath(pen, path);
        return bitmap;
    }

    /// <summary>控件在屏幕上露出来的部分：被滚出去、被外层裁掉的不算。</summary>
    Rectangle VisibleArea(Rectangle bounds)
    {
        for (var parent = Parent; parent is not null; parent = parent.Parent)
            bounds.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
        return bounds;
    }

    /// <summary>重新盯住自己与各层父控件、所在窗体：位置、大小、显示隐藏变了都要挪遮罩窗口。</summary>
    void Watch()
    {
        Unwatch();
        for (Control? control = this; control is not null; control = control.Parent)
        {
            control.Move += OnMoved;
            control.Resize += OnMoved;
            control.VisibleChanged += OnMoved;
            if (control != this) control.ParentChanged += OnReparented;
            watched.Add(control);
        }
        form = FindForm();
        if (form is not null) form.Shown += OnMoved;
    }

    void Unwatch()
    {
        foreach (var control in watched)
        {
            control.Move -= OnMoved;
            control.Resize -= OnMoved;
            control.VisibleChanged -= OnMoved;
            control.ParentChanged -= OnReparented;
        }
        watched.Clear();
        if (form is not null) form.Shown -= OnMoved;
        form = null;
    }

    void OnMoved(object? sender, EventArgs e) => Present();

    void OnReparented(object? sender, EventArgs e)
    {
        Watch();
        Present();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Watch();
        Present();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // 创建时已经在加载：遮罩立即出现（没有内容可看，空白比指示更糟）
        if (IsLoading && !delayed.Shown)
        {
            delayed.Stop();
            delayed = new DelayedLoading(_ => Present(), loading: true);
        }
        Present();
    }

    /// <inheritdoc />
    protected override void OnHandleDestroyed(EventArgs e)
    {
        frames.Stop();
        overlay.Dismiss();
        base.OnHandleDestroyed(e);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new OverlayAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Unwatch();
            delayed.Stop();
            frames.Dispose();
            overlay.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>读屏：加载中是忙的（TS 的 aria-busy），说明念加载指示的文字。</summary>
    sealed class OverlayAccessibleObject(LoadingOverlayControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleStates State => base.State | (owner.IsLoading ? AccessibleStates.Busy : AccessibleStates.None);

        public override string? Description => owner.IsLoading ? owner.Label ?? ProviderControl.LangOf(owner).Loading : base.Description;
    }
}
