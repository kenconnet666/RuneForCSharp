using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 跑马灯（vue-tsx-pro 的 Marquee）：内容循环滚动（合作伙伴 logo、公告、标签墙）。内容后面接一份复制，平移到正好一份的长度时与开头重合，无缝循环。
/// <code>
/// var partners = new MarqueeControl { AccessibleName = "合作伙伴", Dock = DockStyle.Top };
/// partners.Items.AddRange([logo1, logo2, logo3]);
/// </code>
/// <list type="bullet">
/// <item>方向 left / right / up / down（竖着滚时自己给高度）；<see cref="Speed"/> 是每秒多少像素。</item>
/// <item>指针悬停、焦点在里面时暂停（<see cref="PauseOnHover"/>）；<see cref="IsPaused"/> 由外面控制（WCAG 2.2.2：给用户一个暂停按钮）。</item>
/// <item>系统关闭动画时不滚动，改成可以手动滚动（自动滚动条，能聚焦）。</item>
/// <item>复制的那份是内容的位图快照（内容变了大小时重拍），画在自己的背景上：点不到、读屏里没有，读屏只念一遍。</item>
/// </list>
/// 子控件各有窗口，盖不了它们，没有两端渐隐（上游的 fade）。读屏里是一组。
/// </summary>
[ToolboxItem(true)]
[Description("跑马灯：内容循环滚动。")]
public class MarqueeControl : Panel
{
    readonly FlexControl group = new() { Align = FlexAlign.Center, AutoSize = true };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    readonly System.Diagnostics.Stopwatch clock = new();
    Bitmap? snapshot;
    double distance;

    /// <summary>新建：内容在一个横排的 Flex 里，后面画一份复制。</summary>
    public MarqueeControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Grouping;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Controls.Add(group);
        group.SizeChanged += (_, _) => Retake();
        timer.Tick += (_, _) => Frame();
        Configure();
    }

    /// <summary>内容（一项一项横排或竖排）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ControlCollection Items => group.Controls;

    /// <summary>滚动方向，默认 left。</summary>
    [Category("Behavior")]
    public MarqueeDirection Direction { get; set { field = value; Configure(); } }

    bool ShouldSerializeDirection() => Direction.Value is not null;

    void ResetDirection() => Direction = default;

    /// <summary>速度：每秒多少像素，默认 40。</summary>
    [Category("Behavior")]
    [DefaultValue(40d)]
    public double Speed { get; set; } = MarqueeMotion.Speed;

    /// <summary>指针悬停、焦点在里面时暂停，默认 true。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool PauseOnHover { get; set; } = true;

    /// <summary>暂停。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsPaused { get; set { field = value; Tick(); } }

    /// <summary>项之间（以及两份之间）的间距，默认 lg。</summary>
    [Category("Layout")]
    public Space Gap { get; set { field = value; Configure(); } } = Space.Lg;

    bool ShouldSerializeGap() => Gap != Space.Lg;

    void ResetGap() => Gap = Space.Lg;

    /// <summary>两份之间的间距（像素）。</summary>
    int Spacing => (int)MathF.Round((Gap.Factor ?? 0) * this.EmPixels());

    /// <summary>一份内容（含它后面的间距）的长度。</summary>
    internal int Length => (Direction.IsVertical ? group.Height : group.Width) + Spacing;

    /// <summary>第一份内容现在的位移（测试用）。</summary>
    internal int Offset => Direction.IsVertical ? group.Top : group.Left;

    /// <summary>复制的那份画不画（测试用）。</summary>
    internal bool CopyShown => Motion.Enabled && snapshot is not null;

    /// <summary>该接帧：显示着、系统允许动画、没暂停。</summary>
    bool Live => Visible && IsHandleCreated && Motion.Enabled && !IsPaused;

    /// <summary>在动：该接帧，且指针不在上面、焦点不在里面（指针停在子控件上时自己收不到鼠标事件，每帧看指针的位置）。</summary>
    internal bool Running => Live && !(PauseOnHover && (ContainsFocus || ClientRectangle.Contains(PointToClient(Cursor.Position))));

    /// <summary>往前走一段（测试里不等帧）。</summary>
    internal void Advance(TimeSpan elapsed)
    {
        if (Running) distance += elapsed.TotalSeconds * Speed;
        Place();
    }

    /// <summary>方向与间距；系统关闭动画时不复制、改成可以手动滚动。</summary>
    void Configure()
    {
        group.Direction = Direction.IsVertical ? FlexDirection.Column : FlexDirection.Row;
        group.Gap = Gap;
        var still = !Motion.Enabled;
        AutoScroll = still;
        TabStop = still;
        SetStyle(ControlStyles.Selectable, still);
        Place();
        Tick();
    }

    /// <summary>横着滚时高度跟着内容；竖着滚时高度自己给。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = group.GetPreferredSize(Size.Empty);
        return Direction.IsVertical ? new Size(size.Width, Height) : new Size(Width, size.Height);
    }

    void Place()
    {
        if (!Motion.Enabled) return;
        var offset = (int)Math.Round(MarqueeMotion.Offset(distance, Length, Direction.IsReverse));
        group.Location = Direction.IsVertical ? new Point(0, offset) : new Point(offset, 0);
        Invalidate();
    }

    /// <summary>重拍复制用的快照：内容的大小变了（加了项、换了字号）。</summary>
    void Retake()
    {
        snapshot?.Dispose();
        snapshot = null;
        if (group.Width > 0 && group.Height > 0 && group.IsHandleCreated)
        {
            snapshot = new Bitmap(group.Width, group.Height);
            group.DrawToBitmap(snapshot, new Rectangle(Point.Empty, group.Size));
        }
        Invalidate();
    }

    /// <summary>复制画在第一份后面隔一个间距的地方。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!CopyShown) return;
        var at = Direction.IsVertical ? new Point(group.Left, group.Bottom + Spacing) : new Point(group.Right + Spacing, group.Top);
        e.Graphics.DrawImageUnscaled(snapshot!, at);
    }

    /// <summary>该接帧时开着计时器；悬停、聚焦的那段每帧照样看，只是不往前走。</summary>
    void Tick()
    {
        if (Live == timer.Enabled) return;
        if (Live)
        {
            clock.Restart();
            timer.Start();
        }
        else timer.Stop();
    }

    void Frame()
    {
        var elapsed = clock.Elapsed;
        clock.Restart();
        Advance(elapsed);
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Retake();
        Tick();
    }

    /// <inheritdoc />
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) Retake();
        Tick();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            snapshot?.Dispose();
        }
        base.Dispose(disposing);
    }
}
