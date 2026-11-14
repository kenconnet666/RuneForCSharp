using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 窗口顶部的加载条（vue-tsx-pro 的 LoadingBar）：换页、整页刷新数据时最上方一条细线从起始边往结束边走，显示 <see cref="LoadingBarController"/> 的进度。
/// <code>
/// form.Controls.Add(new LoadingBarControl { Controller = App.Loading, Dock = DockStyle.Top });
/// </code>
/// 高 borderWidths.md、accent 色，出错是 danger；进度、显隐在 durations.md 内过渡（系统关闭动画时直接到位）。
/// 子控件各有窗口、盖不到别的控件上面，所以它占顶上一条（停靠在顶边）；GDI+ 画不出模糊的光，也画不出控件外面，没有网页上那圈光。
/// 只是视觉提示：读屏里是进度条（名称"加载中"），空闲时是看不见的；真正的"加载中"应由内容区域告诉读屏。
/// </summary>
[ToolboxItem(true)]
[Description("窗口顶部的加载条：显示 LoadingBarController 的进度。")]
public class LoadingBarControl : Control
{
    readonly Signal<double> fraction = new(0);
    readonly Signal<double> alpha = new(0);
    readonly Effect paint;

    /// <summary>新建：高 borderWidths.md，透明底。</summary>
    public LoadingBarControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        TabStop = false;
        paint = new Effect(Invalidate);
        Height = Thickness;
    }

    /// <summary>控制器。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LoadingBarController? Controller
    {
        get;
        set
        {
            if (field is not null) field.PropertyChanged -= OnProgressChanged;
            field = value;
            if (value is not null) value.PropertyChanged += OnProgressChanged;
            Update(animate: false);
        }
    }

    int Thickness => Math.Max(1, (int)MathF.Round(Tokens.BorderWidth.Md * DeviceDpi / 96f));

    void OnProgressChanged(object? sender, PropertyChangedEventArgs e) => Update(animate: true);

    /// <summary>进度、显隐跟着控制器：durations.md 内过渡；归零是在淡出之后，不倒着走回去。</summary>
    void Update(bool animate)
    {
        var controller = Controller;
        var duration = animate && IsHandleCreated ? Tokens.Duration.Md : TimeSpan.Zero;
        Animator.Animate(alpha, controller is { IsVisible: true } ? 1 : 0, duration);
        var to = (controller?.Progress ?? 0) / 100;
        Animator.Animate(fraction, to, to > fraction.Peek() ? duration : TimeSpan.Zero);
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Height = Thickness;
    }

    /// <summary>从起始边画到进度处（从右到左排版时从右边起）。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var width = (int)Math.Round(Width * fraction.Value);
        var opacity = alpha.Value;
        if (width <= 0 || opacity <= 0) return;
        var colors = ProviderControl.ThemeOf(this).Colors;
        var color = Controller is { IsFailed: true } ? colors.Danger : colors.Accent;
        using var brush = new SolidBrush(Color.FromArgb((int)Math.Round(color.A * opacity), color));
        var left = RightToLeft == RightToLeft.Yes ? Width - width : 0;
        e.Graphics.FillRectangle(brush, left, 0, width, Height);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new LoadingBarAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Controller = null;
            paint.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>读屏：进度条，名称"加载中"，值是进度的百分比；空闲时是看不见的。</summary>
    sealed class LoadingBarAccessibleObject(LoadingBarControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.ProgressBar;

        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? ProviderControl.LangOf(owner).Loading : owner.AccessibleName; set => owner.AccessibleName = value; }

        public override string? Value { get => $"{Math.Round(owner.Controller?.Progress ?? 0)}%"; set { } }

        public override AccessibleStates State => owner.Controller is { IsVisible: true } ? base.State : base.State | AccessibleStates.Invisible;
    }
}
