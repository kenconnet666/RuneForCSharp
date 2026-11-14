using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 滚动区域（vue-tsx-pro 的 ScrollArea）：放一个内容控件（<see cref="Content"/>），超出时滚动，滚动条是浮在内容上的细条，出现、消失都不占位。
/// <code>
/// var area = new ScrollAreaControl { Height = 320, Content = inboxList, AccessibleName = "收件箱" };
/// var shelf = new ScrollAreaControl { Axis = ScrollAreaAxis.X, Type = ScrollbarType.Always, Content = playlists };
/// </code>
/// 内容的大小：纵向滚动（默认）时宽同区域、高按内容自己要的；横向时反过来；两个方向都按内容自己要的（都不小于区域）。
/// 滚轮、键盘（区域聚焦时方向键、PageUp / PageDown、Home / End）、拖动滑块都能滚；内容里的控件得到焦点时滚到看得见。
/// 滚动条在指针移到区域上或正在滚动时出现（<see cref="Type"/>），停下 <see cref="HideDelay"/> 后隐藏。
/// 与上游的出入：滚动条是叠在内容上的子控件（WinForms 的子控件不能半透明），滑块的颜色按区域的底色混出来，出现、消失没有淡入淡出；
/// 只有滑块是控件，点击轨道的空白处不翻页（空白处是内容）。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Axis))]
[Description("滚动区域：浮层滚动条，出现、消失都不占位。")]
public class ScrollAreaControl : Control
{
    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => ManualLayout.Instance;

    readonly OverlayThumb vertical;
    readonly OverlayThumb horizontal;
    readonly Control[] focusRing;
    readonly System.Windows.Forms.Timer hide = new();
    Point offset;
    Size extent;
    bool scrolled;

    /// <summary>新建：纵向滚动。</summary>
    public ScrollAreaControl()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.ContainerControl | ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
        // 背景跟着外面（卡片的表面色）：不画自己的底色
        BackColor = Color.Transparent;
        TabStop = false;
        vertical = new OverlayThumb(this, vertical: true);
        horizontal = new OverlayThumb(this, vertical: false);
        focusRing = [.. Enumerable.Range(0, 4).Select(_ => new Control { Visible = false, TabStop = false })];
        Controls.AddRange([vertical, horizontal, .. focusRing]);
        hide.Tick += (_, _) =>
        {
            hide.Stop();
            scrolled = false;
            UpdateActive();
        };
    }

    /// <summary>内容超出、滚动、指针进出时（测试用）：滚动条是否显示着。</summary>
    [Browsable(false)]
    public bool IsScrollbarVisible { get; private set; }

    /// <summary>纵向的滑块（测试用）。</summary>
    internal Control VerticalThumb => vertical;

    /// <summary>横向的滑块（测试用）。</summary>
    internal Control HorizontalThumb => horizontal;

    /// <summary>滚动位置或内容的排版变了（同平台 ScrollViewer 的 ScrollChanged）。</summary>
    [Category("Action")]
    public event EventHandler? ScrollChanged;

    /// <summary>内容在某个方向超出了区域。</summary>
    [Browsable(false)]
    public bool IsOverflowing { get; private set; }

    /// <summary>滚动的位置（设备像素）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Point ScrollOffset
    {
        get => offset;
        set
        {
            var next = new Point(Math.Clamp(value.X, 0, MaxOffset.Width), Math.Clamp(value.Y, 0, MaxOffset.Height));
            if (next == offset) return;
            offset = next;
            Arrange();
            scrolled = true;
            Reveal();
        }
    }

    /// <summary>内容控件（只有一个：要放多个时放进一个 Flex 或 Grid）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content
    {
        get;
        set
        {
            if (field == value) return;
            if (field is not null)
            {
                Controls.Remove(field);
                field.Enter -= OnContentEnter;
            }
            field = value;
            if (value is not null)
            {
                value.Dock = DockStyle.None;
                Controls.Add(value);
                value.SendToBack();
                value.Enter += OnContentEnter;
            }
            offset = Point.Empty;
            PerformLayout();
        }
    }

    /// <summary>可以滚动的方向，默认 y（横向超出的裁掉）。</summary>
    [Category("Behavior")]
    [Description("可以滚动的方向：y、x、both。")]
    public ScrollAreaAxis Axis
    {
        get => field.Value is null ? ScrollAreaAxis.Y : field;
        set
        {
            field = value;
            offset = Point.Empty;
            PerformLayout();
        }
    }

    /// <summary>滚动条什么时候显示，默认 hover。</summary>
    [Category("Appearance")]
    [Description("滚动条什么时候显示：hover、scroll、always。")]
    public ScrollbarType Type
    {
        get => field.Value is null ? ScrollbarType.Hover : field;
        set
        {
            field = value;
            UpdateActive();
        }
    }

    /// <summary>停止滚动、指针离开后多久隐藏，默认 600 毫秒。</summary>
    [Category("Behavior")]
    [Description("停止滚动、指针离开后多久隐藏。")]
    public TimeSpan HideDelay { get; set; } = TimeSpan.FromMilliseconds(600);

    /// <summary>内容超出时区域能用 Tab 聚焦（键盘滚动），默认 true；区域里另有自己管焦点的控件时关掉。</summary>
    [Category("Behavior")]
    [Description("内容超出时区域能用 Tab 聚焦（键盘滚动）。")]
    [DefaultValue(true)]
    public bool IsViewportFocusable
    {
        get;
        set
        {
            field = value;
            TabStop = value && IsOverflowing;
        }
    } = true;

    bool ShouldSerializeAxis() => Axis != ScrollAreaAxis.Y;

    void ResetAxis() => Axis = ScrollAreaAxis.Y;

    bool ShouldSerializeType() => Type != ScrollbarType.Hover;

    void ResetType() => Type = ScrollbarType.Hover;

    bool ShouldSerializeHideDelay() => HideDelay != TimeSpan.FromMilliseconds(600);

    void ResetHideDelay() => HideDelay = TimeSpan.FromMilliseconds(600);

    bool ScrollsX => Axis.Value is "x" or "both";

    bool ScrollsY => Axis.Value is "y" or "both";

    Size MaxOffset => new(Math.Max(0, extent.Width - ClientSize.Width), Math.Max(0, extent.Height - ClientSize.Height));

    /// <summary>滚动条的粗细：0.5em 的滑块加两侧各 2 像素。</summary>
    int Thickness => (int)MathF.Round(0.5f * this.EmPixels()) + 2 * Inset;

    int Inset => LogicalToDeviceUnits(2);

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (Content is not { } content) return;
        var view = ClientSize;
        var wanted = content.GetPreferredSize(ScrollsX ? Size.Empty : new Size(view.Width, 0));
        extent = new Size(ScrollsX ? Math.Max(view.Width, wanted.Width) : view.Width, ScrollsY ? Math.Max(view.Height, wanted.Height) : view.Height);
        IsOverflowing = (ScrollsX && extent.Width > view.Width) || (ScrollsY && extent.Height > view.Height);
        TabStop = IsViewportFocusable && IsOverflowing;
        offset = new Point(Math.Clamp(offset.X, 0, MaxOffset.Width), Math.Clamp(offset.Y, 0, MaxOffset.Height));
        Arrange();
        UpdateActive();
    }

    /// <summary>内容按滚动位置挪开；滑块按"视口 / 内容"的比例与位置摆到轨道上（至少 20 像素长）。</summary>
    void Arrange()
    {
        if (Content is { } content) content.Bounds = new Rectangle(-offset.X, -offset.Y, extent.Width, extent.Height);
        var both = Axis.Value == "both";
        var thickness = Thickness;
        var view = ClientSize;
        vertical.Place(new Rectangle(view.Width - thickness, 0, thickness, view.Height - (both ? thickness : 0)), view.Height, extent.Height, offset.Y, Inset);
        horizontal.Place(new Rectangle(0, view.Height - thickness, view.Width - (both ? thickness : 0), thickness), view.Width, extent.Width, offset.X, Inset);
        var ring = LogicalToDeviceUnits(2);
        focusRing[0].Bounds = new Rectangle(0, 0, view.Width, ring);
        focusRing[1].Bounds = new Rectangle(0, view.Height - ring, view.Width, ring);
        focusRing[2].Bounds = new Rectangle(0, 0, ring, view.Height);
        focusRing[3].Bounds = new Rectangle(view.Width - ring, 0, ring, view.Height);
        ScrollChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>滚动或指针进入：显示一阵（scroll 型只认滚动），停下后按 <see cref="HideDelay"/> 隐藏。</summary>
    internal void Reveal()
    {
        UpdateActive();
        hide.Stop();
        hide.Interval = Math.Max(1, (int)HideDelay.TotalMilliseconds);
        hide.Start();
    }

    /// <summary>指针在区域上（含内容里的控件、滑块）。</summary>
    bool PointerInside => IsHandleCreated && RectangleToScreen(ClientRectangle).Contains(Cursor.Position);

    void UpdateActive()
    {
        var type = Type.Value;
        var active = IsOverflowing && (type == "always" || scrolled || (type == "hover" && PointerInside) || vertical.Dragging || horizontal.Dragging);
        IsScrollbarVisible = active;
        vertical.Visible = active && ScrollsY && extent.Height > ClientSize.Height;
        horizontal.Visible = active && ScrollsX && extent.Width > ClientSize.Width;
        if (vertical.Visible) vertical.BringToFront();
        if (horizontal.Visible) horizontal.BringToFront();
    }

    /// <inheritdoc />
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var step = SystemInformation.MouseWheelScrollLines * Font.Height * e.Delta / 120;
        ScrollOffset = ScrollsY ? offset with { Y = offset.Y - step } : offset with { X = offset.X - step };
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Reveal();
    }

    /// <summary>内容里的控件的指针移动：区域本身收不到（指针在子控件上），由子控件转过来。</summary>
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        Track(e.Control!);
    }

    void Track(Control control)
    {
        if (control is OverlayThumb || focusRing.Contains(control)) return;
        control.MouseEnter += OnDescendantMouseEnter;
        control.ControlAdded += (_, e) => Track(e.Control!);
        foreach (Control child in control.Controls) Track(child);
    }

    void OnDescendantMouseEnter(object? sender, EventArgs e) => Reveal();

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);

    /// <summary>区域聚焦时键盘滚动：方向键一行，PageUp / PageDown 一页（九成），Home / End 到头。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var line = Font.Height * 3;
        var page = (int)((ScrollsY ? ClientSize.Height : ClientSize.Width) * 0.9);
        Point? next = e.KeyCode switch
        {
            Keys.Down => offset with { Y = offset.Y + line },
            Keys.Up => offset with { Y = offset.Y - line },
            Keys.Right => offset with { X = offset.X + line },
            Keys.Left => offset with { X = offset.X - line },
            Keys.PageDown => ScrollsY ? offset with { Y = offset.Y + page } : offset with { X = offset.X + page },
            Keys.PageUp => ScrollsY ? offset with { Y = offset.Y - page } : offset with { X = offset.X - page },
            Keys.Home => Point.Empty,
            Keys.End => new Point(MaxOffset.Width, MaxOffset.Height),
            _ => null,
        };
        if (next is not { } target) return;
        ScrollOffset = target;
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        ShowFocusRing(ShowFocusCues);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        ShowFocusRing(false);
    }

    /// <summary>键盘聚焦时里侧 2 像素的焦点环（叠在内容边上的四条细控件：画在区域自己身上会被内容盖住）。</summary>
    void ShowFocusRing(bool visible)
    {
        var color = ProviderControl.ThemeOf(this).Colors.FocusRing;
        foreach (var edge in focusRing)
        {
            edge.BackColor = color;
            edge.Visible = visible;
            if (visible) edge.BringToFront();
        }
    }

    /// <summary>内容里的控件得到焦点：滚到看得见。</summary>
    void OnContentEnter(object? sender, EventArgs e)
    {
        if (Content is null || FocusedIn(Content) is not { } focused) return;
        var bounds = RectangleToClient(focused.RectangleToScreen(focused.ClientRectangle));
        var target = offset;
        if (bounds.Bottom > ClientSize.Height) target.Y += bounds.Bottom - ClientSize.Height;
        if (bounds.Top < 0) target.Y += bounds.Top;
        if (bounds.Right > ClientSize.Width) target.X += bounds.Right - ClientSize.Width;
        if (bounds.Left < 0) target.X += bounds.Left;
        ScrollOffset = target;
    }

    /// <summary>子树里有焦点的那个控件。</summary>
    static Control? FocusedIn(Control root) =>
        root.Focused ? root : root.Controls.Cast<Control>().Where(child => child.ContainsFocus).Select(FocusedIn).FirstOrDefault(found => found is not null);

    /// <summary>拖动滑块：位移按"可滚动距离 / 滑块可移动距离"换算成滚动距离。</summary>
    internal void Drag(bool vertical, int start, int delta, int trackLength, int thumbLength)
    {
        var range = vertical ? MaxOffset.Height : MaxOffset.Width;
        var travel = Math.Max(1, trackLength - thumbLength);
        var value = start + (int)Math.Round(delta * (double)range / travel);
        ScrollOffset = vertical ? offset with { Y = value } : offset with { X = value };
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) hide.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>
    /// 一个方向的滑块：轨道里 textMuted 半透明的圆条（颜色按区域的底色混出来；悬停、拖动时更深），离轨道两侧各 2 像素，至少 20 像素长。
    /// </summary>
    sealed class OverlayThumb : Control
    {
        readonly ScrollAreaControl owner;
        readonly bool isVertical;
        Rectangle track;
        int dragStart;
        int dragOrigin;
        bool hovered;

        public OverlayThumb(ScrollAreaControl owner, bool vertical)
        {
            this.owner = owner;
            isVertical = vertical;
            Visible = false;
            TabStop = false;
            Cursor = Cursors.Default;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        public bool Dragging { get; private set; }

        /// <summary>按视口、内容、滚动位置摆到轨道上。</summary>
        public void Place(Rectangle track, int view, int size, int position, int inset)
        {
            this.track = track;
            var trackLength = isVertical ? track.Height : track.Width;
            var length = size > 0 ? Math.Max(owner.LogicalToDeviceUnits(20), view * trackLength / Math.Max(1, size)) : trackLength;
            length = Math.Min(length, trackLength);
            var travel = trackLength - length;
            var start = size > view ? (int)Math.Round((double)position / (size - view) * travel) : 0;
            Bounds = isVertical
                ? new Rectangle(track.X + inset, track.Y + start + inset, Math.Max(1, track.Width - 2 * inset), Math.Max(1, length - 2 * inset))
                : new Rectangle(track.X + start + inset, track.Y + inset, Math.Max(1, length - 2 * inset), Math.Max(1, track.Height - 2 * inset));
            using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), Math.Min(Width, Height) / 2f);
            Region = new Region(path);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ProviderControl.ThemeOf(owner).Colors;
            var under = owner.Content?.BackColor is { A: 255 } back ? back : colors.Background;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Drawing.Mix(under, colors.TextMuted, hovered || Dragging ? 0.75 : 0.5));
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Dragging = true;
            dragStart = isVertical ? Cursor.Position.Y : Cursor.Position.X;
            dragOrigin = isVertical ? owner.ScrollOffset.Y : owner.ScrollOffset.X;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!Dragging) return;
            var delta = (isVertical ? Cursor.Position.Y : Cursor.Position.X) - dragStart;
            owner.Drag(isVertical, dragOrigin, delta, isVertical ? track.Height : track.Width, isVertical ? Height : Width);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Dragging = false;
            Capture = false;
            owner.Reveal();
        }
    }
}
