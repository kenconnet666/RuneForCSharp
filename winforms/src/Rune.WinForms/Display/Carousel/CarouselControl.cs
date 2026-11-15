using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Automation;

namespace Rune.WinForms;

/// <summary>
/// 轮播（vue-tsx-pro 的 Carousel）：横幅、商品图、图片组。一行张片，上一张 / 下一张、页点、可关的自动播放；参考 WAI-ARIA APG 的 Carousel 模式。
/// <code>
/// var banners = new CarouselControl { AccessibleName = "活动", Autoplay = CarouselControl.DefaultAutoplay, Dock = DockStyle.Top };
/// banners.Slides.Add(new AspectRatioControl { Ratio = 3, Controls = { new ImageControl { Source = … } } });
/// var products = new CarouselControl { SlidesPerView = 3, Gap = 16, Loop = false };      一次看三张
/// </code>
/// 上游是浏览器的横向滚动加吸附；桌面上张片排成一条，按位移切换（lg 的过渡，系统关闭动画时直接到位）：拖动、触摸板横向滑动（或 Shift + 滚轮）、
/// 焦点在张片区时 ← / →（从右到左排版时反过来）都能换，停下时吸附到整张。<see cref="Index"/> 是最前面那张的序号（一次看几张时最大是张数 − SlidesPerView，向上取整），
/// 变了报告 <see cref="IndexChanged"/>。<see cref="Loop"/>（默认 true）到头后从另一头开始；不循环时两头的按钮不可用。高度跟着最高的那张（自动大小）。
/// 自动播放：<see cref="Autoplay"/> 是间隔（TS 的 autoplay: true 是 5 秒，即 <see cref="DefaultAutoplay"/>）；指针停在上面、键盘焦点进到里面时暂停，
/// 底部有暂停 / 播放按钮；系统关闭动画时一开始就是暂停的。
/// 读屏：轮播是带名称的一组（名称默认“轮播”），每张包在一组里、名称“第 2 张，共 5 张”；不自动播放时换张会念出来。
/// WinForms 的子控件各有窗口，盖在张片上的按钮与控制条透不出下面的图（透出的是轮播外面的底），按钮也没有阴影。
/// </summary>
[DefaultEvent(nameof(IndexChanged))]
[Description("轮播：一行张片，上一张 / 下一张、页点、可关的自动播放。")]
public class CarouselControl : Control
{
    /// <summary>TS 的 autoplay: true：每 5 秒换一张。</summary>
    public static readonly TimeSpan DefaultAutoplay = TimeSpan.FromSeconds(5);

    const int WM_MOUSEHWHEEL = 0x020E;

    readonly Track track;
    readonly IconButtonControl previous = new() { Icon = Lucide.ChevronLeft, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Radius = Radius.Full };
    readonly IconButtonControl next = new() { Icon = Lucide.ChevronRight, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Radius = Radius.Full };
    readonly ControlsBar controls = new();
    readonly IconButtonControl pause = new() { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Radius = Radius.Full };
    readonly System.Windows.Forms.Timer timer = new();
    bool paused;
    bool keyboardFocused;

    /// <summary>新建：张片区、两侧按钮、底部的控制条。</summary>
    public CarouselControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        TabStop = false;
        AutoSize = true;
        track = new Track(this);
        Slides = new SlideCollection(this);
        previous.Click += (_, _) => Go(Index - 1);
        next.Click += (_, _) => Go(Index + 1);
        pause.Click += (_, _) =>
        {
            paused = !paused;
            // 明确点了“播放”：焦点还在这个按钮上也恢复
            if (!paused) keyboardFocused = false;
            Sync();
        };
        controls.Row.Controls.Add(pause);
        // 页点变宽（当前的拉长）后控制条跟着变宽、重新居中
        controls.Row.SizeChanged += (_, _) => PerformLayout();
        // 先加的在上面：按钮与控制条盖在张片区上
        Controls.AddRange([previous, next, controls, track]);
        timer.Tick += (_, _) => Tick();
        // 系统关闭了动画：一开始就暂停（用户点了播放照样播）
        paused = !Motion.Enabled;
        Sync();
    }

    /// <summary>每一张（控件原样放进去，各自包在一组里）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Collection<Control> Slides { get; }

    /// <summary>最前面那张的序号。</summary>
    [Category("Behavior")]
    [Description("最前面那张的序号。")]
    [DefaultValue(0)]
    public int Index
    {
        get;
        set
        {
            value = Math.Clamp(value, 0, Last);
            if (field == value) return;
            field = value;
            track.ScrollTo(value, animate: true);
            if (!Playing && IsHandleCreated)
            {
                Announced = ProviderControl.LangOf(this).SlideCounter(value + 1, Count);
                AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, Announced);
            }
            Sync();
            IndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>一次看几张，默认 1（可以是 2.5：露出下一张的一半）。</summary>
    [Category("Layout")]
    [Description("一次看几张（可以是 2.5：露出下一张的一半）。")]
    [DefaultValue(1d)]
    public double SlidesPerView
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
            Rebuild();
        }
    } = 1;

    /// <summary>张与张之间的间距（逻辑像素，按 DPI 放大），默认 0。</summary>
    [Category("Layout")]
    [Description("张与张之间的间距（逻辑像素）。")]
    [DefaultValue(0)]
    public int Gap { get; set { field = value; Relayout(); } }

    /// <summary>到头后接着从另一头开始，默认 true。</summary>
    [Category("Behavior")]
    [Description("到头后接着从另一头开始。")]
    [DefaultValue(true)]
    public bool Loop { get; set { field = value; Sync(); } } = true;

    /// <summary>自动播放的间隔；null（默认）不自动播放。</summary>
    [Category("Behavior")]
    [Description("自动播放的间隔；不写时不自动播放。")]
    [DefaultValue(null)]
    public TimeSpan? Autoplay { get; set { field = value; Sync(); } }

    /// <summary>两侧的上一张 / 下一张按钮，默认显示。</summary>
    [Category("Appearance")]
    [Description("两侧的上一张 / 下一张按钮。")]
    [DefaultValue(true)]
    public bool Arrows { get; set { field = value; Sync(); } } = true;

    /// <summary>底部的页点，默认显示。</summary>
    [Category("Appearance")]
    [Description("底部的页点。")]
    [DefaultValue(true)]
    public bool Dots { get; set { field = value; Sync(); } } = true;

    /// <summary>换了一张（按钮、页点、拖动、方向键、自动播放、写 Index）。</summary>
    public event EventHandler? IndexChanged;

    /// <summary>张数。</summary>
    int Count => Slides.Count;

    /// <summary>最后一个序号：一次看 1.5 张这种向上取整，滚到底就算到了最后。</summary>
    int Last => Math.Max(0, (int)Math.Ceiling(Count - SlidesPerView));

    /// <summary>间距（设备像素）。</summary>
    int GapPixels => LogicalToDeviceUnits(Gap);

    /// <summary>指针在上面（指针停在子控件上时自己收不到鼠标事件，看指针的位置）。</summary>
    bool Hovered => IsHandleCreated && ClientRectangle.Contains(PointToClient(Cursor.Position));

    bool AutoplayOn => Autoplay is { } interval && interval > TimeSpan.Zero && Last > 0;

    /// <summary>正在自动播放：有间隔、不止一页、没暂停、指针不在上面、键盘焦点不在里面。</summary>
    internal bool Playing => AutoplayOn && !paused && !Hovered && !keyboardFocused;

    /// <summary>上一张、下一张、暂停按钮（测试用）。</summary>
    internal (IconButtonControl Previous, IconButtonControl Next, IconButtonControl Pause) Buttons => (previous, next, pause);

    /// <summary>页点（测试用）。</summary>
    internal IReadOnlyList<Dot> DotButtons => [.. controls.Row.Controls.OfType<Dot>()];

    /// <summary>包着每一张的组（测试用）。</summary>
    internal IReadOnlyList<Control> SlideHosts => [.. track.Controls.Cast<Control>()];

    /// <summary>张片区（测试用）。</summary>
    internal Control Viewport => track;

    /// <summary>读屏最后念的话（测试用）。</summary>
    internal string? Announced { get; private set; }

    /// <summary>按 <see cref="Slides"/> 重建每一张的组与页点。</summary>
    void Rebuild()
    {
        var lang = ProviderControl.LangOf(this);
        foreach (var host in track.Controls.Cast<SlideHost>().ToArray())
        {
            // 先放手：控件还要放进新的组
            host.Controls.Clear();
            host.Dispose();
        }
        for (var index = 0; index < Slides.Count; index++)
            track.Controls.Add(new SlideHost(Slides[index]) { AccessibleName = lang.SlideCounter(index + 1, Slides.Count) });
        foreach (var dot in controls.Row.Controls.OfType<Dot>().ToArray()) dot.Dispose();
        for (var index = 0; index <= Last; index++)
        {
            var page = index;
            var dot = new Dot { AccessibleName = lang.SlideCounter(page + 1, Slides.Count) };
            dot.Click += (_, _) => Go(page);
            controls.Row.Controls.Add(dot);
        }
        if (Index > Last) Index = Last;
        Relayout();
    }

    /// <summary>换到第 target 张：Loop 时绕回另一头，否则停在两头。</summary>
    internal void Go(int target)
    {
        var pages = Last + 1;
        Index = Loop ? (target % pages + pages) % pages : Math.Clamp(target, 0, Last);
    }

    void Relayout()
    {
        track.PerformLayout();
        Sync();
        if (Parent is { } parent) parent.PerformLayout(this, nameof(Bounds));
        PerformLayout();
    }

    /// <summary>按钮、页点、名称跟着状态；重新开始自动播放的计时。</summary>
    void Sync()
    {
        var lang = ProviderControl.LangOf(this);
        var pages = Last + 1;
        previous.Visible = next.Visible = Arrows && pages > 1;
        previous.Enabled = Loop || Index > 0;
        next.Enabled = Loop || Index < Last;
        previous.AccessibleName = lang.CarouselPrevious;
        next.AccessibleName = lang.CarouselNext;
        pause.Visible = AutoplayOn;
        pause.Icon = paused ? Lucide.Play : Lucide.Pause;
        pause.AccessibleName = paused ? lang.CarouselPlay : lang.CarouselPause;
        var dots = Dots && pages > 1;
        foreach (var dot in controls.Row.Controls.OfType<Dot>())
        {
            dot.Visible = dots;
            dot.IsCurrent = controls.Row.Controls.IndexOf(dot) - 1 == Index;
        }
        controls.Visible = dots || AutoplayOn;
        timer.Stop();
        if (!AutoplayOn || paused) return;
        timer.Interval = Math.Max(1, (int)Autoplay!.Value.TotalMilliseconds);
        timer.Start();
    }

    /// <summary>到点换下一张；指针在上面、键盘焦点在里面时这一轮先不换（定时器接着走）。</summary>
    void Tick()
    {
        if (Playing) Go(Index + 1);
    }

    /// <summary>
    /// 键盘焦点进来才暂停（点了上一张、页点后按钮留着焦点，不能因此一直停着）：进来时没按着鼠标、窗口显示焦点提示（用过键盘，同 :focus-visible）；
    /// 窗口刚打开时焦点自动落进来不算。
    /// </summary>
    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        keyboardFocused = MouseButtons == MouseButtons.None && ShowFocusCues;
        Sync();
    }

    /// <inheritdoc />
    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        keyboardFocused = false;
        Sync();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 语言包取自外层：放进界面后重新生成名称
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Sync();
    }

    /// <summary>Shift + 滚轮换张（张片里的控件不处理滚轮时会转到这里）。</summary>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!ModifierKeys.HasFlag(Keys.Shift) || e.Delta == 0) return;
        Go(Index + (e.Delta < 0 ? 1 : -1));
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
    }

    /// <summary>触摸板横向滑动（WinForms 没有横向滚轮的事件）：往右是下一张。</summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEHWHEEL)
        {
            var delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
            if (delta != 0) Go(Index + (delta > 0 ? 1 : -1));
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>宽度取布局给的，高度是最高的那张（按一张的宽度量）。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = VerticalStack.Limit(this, proposedSize);
        if (width == int.MaxValue) width = Width;
        var slide = track.SlideWidth(width);
        var height = track.Controls.Cast<SlideHost>().Select(host => host.GetPreferredSize(new Size(slide, 0)).Height).DefaultIfEmpty().Max();
        return new Size(width, height);
    }

    /// <summary>张片区铺满；两侧按钮离边 sm、竖直居中；控制条水平居中、离底 sm。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var sm = (int)MathF.Round(Tokens.Space.Sm * this.EmPixels());
        track.Bounds = ClientRectangle;
        foreach (var (arrow, left) in new[] { (previous, true), (next, false) })
        {
            var size = arrow.GetPreferredSize(Size.Empty);
            arrow.Bounds = new Rectangle(left == (RightToLeft != RightToLeft.Yes) ? sm : Width - sm - size.Width, (Height - size.Height) / 2, size.Width, size.Height);
        }
        var bar = controls.GetPreferredSize(Size.Empty);
        controls.Bounds = new Rectangle((Width - bar.Width) / 2, Height - sm - bar.Height, bar.Width, bar.Height);
        Clip(previous, previous.Height / 2f);
        Clip(next, next.Height / 2f);
        Clip(controls, controls.Height / 2f);
    }

    /// <summary>
    /// 盖在张片上的按钮与控制条裁成圆角：子控件透明时透出的是轮播外面的底，不裁的话圆形按钮四角是一块方形的底。
    /// 区域裁剪没有抗锯齿，边缘比画出来的略硬。
    /// </summary>
    static void Clip(Control control, float radius)
    {
        if (control.Width <= 0 || control.Height <= 0) return;
        using var shape = Drawing.RoundedRectangle(new RectangleF(0, 0, control.Width, control.Height), radius);
        var old = control.Region;
        control.Region = new Region(shape);
        old?.Dispose();
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new CarouselAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>读屏：带名称的一组（TS 的 aria-roledescription="carousel"），名称默认“轮播”。</summary>
    sealed class CarouselAccessibleObject(CarouselControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Grouping;

        public override string? Name => owner.AccessibleName is { Length: > 0 } name ? name : ProviderControl.LangOf(owner).Carousel;
    }

    /// <summary>每一张：增删改都重建张片区与页点。</summary>
    sealed class SlideCollection(CarouselControl owner) : Collection<Control>
    {
        protected override void InsertItem(int index, Control item)
        {
            base.InsertItem(index, item);
            owner.Rebuild();
        }

        protected override void SetItem(int index, Control item)
        {
            base.SetItem(index, item);
            owner.Rebuild();
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            owner.Rebuild();
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            owner.Rebuild();
        }
    }

    /// <summary>包着一张的组（TS 的 aria-roledescription="slide"），名称“第 2 张，共 5 张”；里面的控件铺满。</summary>
    sealed class SlideHost : Control
    {
        public SlideHost(Control slide)
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AccessibleRole = AccessibleRole.Grouping;
            Controls.Add(slide);
        }

        Control? Slide => Controls.Count > 0 ? Controls[0] : null;

        public override Size GetPreferredSize(Size proposedSize) =>
            Slide is not { } slide ? Size.Empty : slide.AutoSize ? slide.GetPreferredSize(proposedSize) : slide.Size;

        /// <summary>自动大小（或铺满）的那张撑满高度；固定高度的保持自己的高度、靠上（否则量出来的高度被排版改掉，再量就是 0）。</summary>
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (Slide is not { } slide) return;
            slide.Bounds = new Rectangle(0, 0, Width, slide.AutoSize || slide.Dock == DockStyle.Fill ? Height : slide.Height);
        }
    }

    /// <summary>
    /// 页点：点击区域够大（宽 1.25em、高 1.5em，至少 24），看得见的是中间的小圆点（0.5em、borderStrong，悬停 textMuted）；
    /// 当前的那个拉宽到 2em、圆点 1.25em、accent（宽度 md 的过渡）。键盘聚焦时外面一圈焦点环。
    /// </summary>
    internal sealed class Dot : Control
    {
        readonly Signal<double> current = new(0);
        readonly Effect frame;
        bool hovered;

        public Dot()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor
                     | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            BackColor = Color.Transparent;
            AccessibleRole = AccessibleRole.PushButton;
            Cursor = Cursors.Hand;
            frame = new Effect(Measure);
            Measure();
        }

        /// <summary>当前的那一个（TS 的 aria-current）。</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsCurrent
        {
            get;
            set
            {
                if (field == value) return;
                field = value;
                Animator.Animate(current, value ? 1 : 0, IsHandleCreated ? Tokens.Duration.Md : TimeSpan.Zero, Easing.Standard);
                Invalidate();
            }
        }

        float Em => this.EmPixels();

        /// <summary>每一帧：宽度从 1.25em 到 2em，父容器跟着重排。</summary>
        void Measure()
        {
            double progress;
            using (frame.Track()) progress = current.Value;
            var size = new Size((int)Math.Round((1.25 + 0.75 * progress) * Em), (int)Math.Round(Math.Max(1.5 * Em, LogicalToDeviceUnits(24))));
            if (Size != size) Size = size;
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Measure();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            Measure();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is not (Keys.Space or Keys.Enter)) return;
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }

        public void PerformClick() => OnClick(EventArgs.Empty);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var colors = ElementContext.Of(this).Theme.Colors;
            var em = Em;
            var progress = (float)current.Peek();
            var mark = new SizeF((0.5f + 0.75f * progress) * em, 0.5f * em);
            var box = new RectangleF((Width - mark.Width) / 2, (Height - mark.Height) / 2, mark.Width, mark.Height);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = Drawing.RoundedRectangle(box, mark.Height / 2))
            using (var fill = new SolidBrush(IsCurrent ? colors.Accent : hovered ? colors.TextMuted : colors.BorderStrong))
                e.Graphics.FillPath(fill, shape);
            if (!Focused || !ShowFocusCues) return;
            var width = Math.Max(1, LogicalToDeviceUnits(2));
            using var ring = Drawing.RoundedRectangle(new RectangleF(width / 2f, width / 2f, Width - width, Height - width), em * 0.25f);
            using var pen = new Pen(colors.FocusRing, width);
            e.Graphics.DrawPath(pen, ring);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Animator.Stop(current);
            base.Dispose(disposing);
        }
    }

    /// <summary>底部的控制条：圆角 full、surface 75% 的底，左右 xs；里面是暂停按钮与页点。</summary>
    sealed class ControlsBar : Control
    {
        public ControlsBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            TabStop = false;
            Controls.Add(Row);
        }

        public FlexControl Row { get; } = new() { Align = FlexAlign.Center, AutoSize = true };

        int Inset => (int)MathF.Round(Tokens.Space.Xs * this.EmPixels());

        public override Size GetPreferredSize(Size proposedSize)
        {
            var row = Row.GetPreferredSize(Size.Empty);
            return new Size(row.Width + 2 * Inset, row.Height);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var row = Row.GetPreferredSize(Size.Empty);
            Row.Bounds = new Rectangle(Inset, (Height - row.Height) / 2, row.Width, row.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var surface = ElementContext.Of(this).Theme.Colors.Surface;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var shape = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), Height / 2f);
            using var fill = new SolidBrush(Color.FromArgb(191, surface));
            e.Graphics.FillPath(fill, shape);
        }
    }

    /// <summary>
    /// 张片区：张片排成一行（每张宽 =（宽 − (SlidesPerView − 1) × Gap）÷ SlidesPerView），按位移切换；能聚焦（← / → 换张），区外的部分由窗口裁掉。
    /// 拖动（挪开 3 以内不算）跟着指针移，松开吸附到最近的一张：张片里的控件各有窗口，鼠标事件在它们身上，所以挂在每一层子控件上。
    /// </summary>
    sealed class Track : Control
    {
        readonly CarouselControl owner;
        readonly Signal<double> offset = new(0);
        readonly Effect frame;
        // 张片换组重建时控件还是那些，挂过的不再挂
        readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> hooked = [];
        Point? pressedAt;
        double pressedOffset;
        bool dragging;

        public Track(CarouselControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            TabStop = true;
            frame = new Effect(OnFrame);
            using (frame.Track()) _ = offset.Value;
            Hook(this);
        }

        int SlideWidthNow => SlideWidth(Width);

        public int SlideWidth(int width) => (int)Math.Max(0, Math.Round((width - (owner.SlidesPerView - 1) * owner.GapPixels) / owner.SlidesPerView));

        double MaxOffset => Math.Max(0, Controls.Count * (SlideWidthNow + owner.GapPixels) - owner.GapPixels - Width);

        double OffsetOf(int index) => Math.Min(index * (SlideWidthNow + owner.GapPixels), MaxOffset);

        /// <summary>滚到第 index 张（lg 的过渡；拖动后吸附也走这里）。</summary>
        public void ScrollTo(int index, bool animate) =>
            Animator.Animate(offset, OffsetOf(index), animate && IsHandleCreated ? Tokens.Duration.Lg : TimeSpan.Zero, Easing.Standard);

        void OnFrame()
        {
            using (frame.Track()) _ = offset.Value;
            PerformLayout();
        }

        /// <summary>停在哪一张：离哪张对齐的位置最近（滚到底就是最后一张）。</summary>
        int Nearest()
        {
            var at = offset.Peek();
            if (at >= MaxOffset - 1) return owner.Last;
            var best = 0;
            for (var index = 1; index <= owner.Last; index++)
                if (Math.Abs(at - OffsetOf(index)) < Math.Abs(at - OffsetOf(best))) best = index;
            return best;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var width = SlideWidthNow;
            var rtl = owner.RightToLeft == RightToLeft.Yes;
            for (var index = 0; index < Controls.Count; index++)
            {
                var x = (int)Math.Round(index * (width + owner.GapPixels) - offset.Peek());
                Controls[index].Bounds = new Rectangle(rtl ? Width - x - width : x, 0, width, Height);
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            // 宽度变了（窗口缩放）：停在当前这一张
            ScrollTo(owner.Index, animate: false);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            Hook(e.Control!);
        }

        /// <summary>拖动挂在每一层子控件上（后加的子控件也挂上）。</summary>
        void Hook(Control control)
        {
            if (control != this && hooked.TryAdd(control, this))
            {
                control.MouseDown += Pressed;
                control.MouseMove += Moved;
                control.MouseUp += Released;
                control.ControlAdded += (_, e) => Hook(e.Control!);
            }
            foreach (Control child in control.Controls) Hook(child);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Pressed(this, e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Moved(this, e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Released(this, e);
        }

        void Pressed(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            pressedAt = Cursor.Position;
            pressedOffset = offset.Peek();
        }

        void Moved(object? sender, MouseEventArgs e)
        {
            if (pressedAt is not { } from || MouseButtons != MouseButtons.Left) return;
            var dx = Cursor.Position.X - from.X;
            if (owner.RightToLeft == RightToLeft.Yes) dx = -dx;
            if (!dragging)
            {
                if (Math.Abs(dx) < LogicalToDeviceUnits(3)) return;
                dragging = true;
            }
            Animator.Animate(offset, Math.Clamp(pressedOffset - dx, 0, MaxOffset), TimeSpan.Zero);
        }

        void Released(object? sender, MouseEventArgs e)
        {
            pressedAt = null;
            if (!dragging) return;
            dragging = false;
            var target = Nearest();
            if (target == owner.Index) ScrollTo(target, animate: true);
            else owner.Index = target;
        }

        protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is not (Keys.Left or Keys.Right)) return;
            var forward = (e.KeyCode == Keys.Right) != (owner.RightToLeft == RightToLeft.Yes);
            owner.Go(owner.Index + (forward ? 1 : -1));
            e.Handled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Animator.Stop(offset);
            base.Dispose(disposing);
        }
    }
}
