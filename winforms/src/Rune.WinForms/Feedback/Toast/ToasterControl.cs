using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 通知的显示区域（vue-tsx-pro 的 Toaster）：放一个在窗体（或盖住页面的面板）上，交给它 <see cref="ToasterController"/>，在任何地方调控制器显示通知。
/// <code>
/// public static readonly ToasterController Toaster = new();
/// form.Controls.Add(new ToasterControl { Controller = Toaster, Placement = ToastPlacement.BottomEnd });
/// Toaster.Success("已保存");
/// </code>
/// 停在父控件客户区的一角（<see cref="Placement"/>，四周 lg 的边，宽 26em），在最上层；大小正好是通知本身（窗口区域裁到每条通知），空白处透出下面的界面、不挡点击。
/// 每条通知就是 AlertControl（surface 外观），图标、标题、正文、操作、关闭按钮都复用它。
/// <list type="bullet">
/// <item>计时：指针在通知上、焦点在区域里、窗体不在前台时暂停。<see cref="Max"/>（默认 3）条以外的先不显示，前面的关闭后补上。</item>
/// <item>无障碍：区域是带名称的一组（默认“通知”），F8 跳到通知区域，焦点在一条通知里时 Esc 关闭它；新通知读屏礼貌地念，重要的立即念。
/// 关闭一条后焦点交给相邻的通知，没有时回到按 F8 之前的位置。</item>
/// <item>动画：进场从所在的边滑进来，离场高度收起后从控制器里移除；横向拖动超过宽度的 35% 就关闭，否则弹回。</item>
/// <item><see cref="Stack"/>：叠成一摞（更早的变窄、错开露出一条边），悬停或聚焦时展开；<see cref="Progress"/>：自动关闭的通知底部显示剩余时间，暂停时一起停。</item>
/// </list>
/// WinForms 的控件不能半透明、画不出阴影：没有淡入淡出与阴影，叠放时更早的用变窄代替缩小。
/// </summary>
[Description("通知的显示区域：停在一角，显示 ToasterController 的通知。")]
public class ToasterControl : Control, IMessageFilter
{
    const int WmKeyDown = 0x100;

    readonly Dictionary<string, ToastItemControl> items = [];
    readonly System.Windows.Forms.Timer hover = new() { Interval = 150 };
    readonly FloatCorner corner;
    ToasterController? subscribed;
    Form? form;
    bool hovering;
    bool focused;
    bool windowActive = true;
    bool filtering;
    Control? outsideFocus;

    /// <summary>新建：停在父控件的一角。</summary>
    public ToasterControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.ContainerControl, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        TabStop = true;
        AccessibleRole = AccessibleRole.Grouping;
        Size = Size.Empty;
        corner = new FloatCorner(this, () => new CornerSpot(Placement.Inline, Placement.FromTop, 0));
        hover.Tick += (_, _) => SetHover(Pointed);
    }

    /// <summary>通知控制器。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ToasterController? Controller { get; set { field = value; Subscribe(IsHandleCreated ? value : null); } }

    /// <summary>显示位置，默认 bottom-end。</summary>
    [Category("Layout")]
    [Description("显示位置：top-start、top、top-end、bottom-start、bottom、bottom-end。")]
    public ToastPlacement Placement { get; set { field = value; Relayout(); } } = ToastPlacement.BottomEnd;

    bool ShouldSerializePlacement() => Placement != ToastPlacement.BottomEnd;

    void ResetPlacement() => Placement = ToastPlacement.BottomEnd;

    /// <summary>最多同时显示几条，默认 3。</summary>
    [Category("Behavior")]
    [Description("最多同时显示几条；更早的先不显示。")]
    [DefaultValue(3)]
    public int Max
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
            if (subscribed is not null) subscribed.Limit = value;
            Sync();
        }
    } = 3;

    /// <summary>叠成一摞，默认 false（平铺）。</summary>
    [Category("Appearance")]
    [Description("叠成一摞，悬停或聚焦时展开。")]
    [DefaultValue(false)]
    public bool Stack { get; set { field = value; Relayout(); } }

    /// <summary>自动关闭的通知底部显示剩余时间，默认 false。</summary>
    [Category("Appearance")]
    [Description("自动关闭的通知底部显示剩余时间。")]
    [DefaultValue(false)]
    public bool Progress { get; set { field = value; Sync(); } }

    /// <summary>区域的名称；不写时“通知”。</summary>
    [Category("Accessibility")]
    [DefaultValue(null)]
    public new string? AccessibleName { get => base.AccessibleName; set => base.AccessibleName = value; }

    /// <summary>显示着的通知，旧的在前（测试用）。</summary>
    internal IReadOnlyList<ToastItemControl> Items => [.. items.Values.OrderBy(item => item.Order)];

    /// <summary>叠放展开着（悬停或焦点在里面）。</summary>
    internal bool Expanded => hovering || focused;

    internal ToasterController? Subscribed => subscribed;

    internal float Em => this.EmPixels();

    /// <summary>指针在某条通知上（指针在子控件上时自己收不到鼠标事件，看指针的位置）。</summary>
    bool Pointed => IsHandleCreated && Visible && items.Values.Any(item => item.Visible && item.Bounds.Contains(PointToClient(Cursor.Position)));

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Subscribe(Controller);
    }

    /// <inheritdoc />
    protected override void OnHandleDestroyed(EventArgs e)
    {
        Subscribe(null);
        base.OnHandleDestroyed(e);
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (form is not null)
        {
            form.Activated -= OnActivated;
            form.Deactivate -= OnDeactivated;
        }
        form = FindForm();
        if (form is not null)
        {
            form.Activated += OnActivated;
            form.Deactivate += OnDeactivated;
        }
        Relayout();
    }

    void OnActivated(object? sender, EventArgs e)
    {
        windowActive = true;
        UpdatePause();
    }

    void OnDeactivated(object? sender, EventArgs e)
    {
        windowActive = false;
        UpdatePause();
    }

    /// <summary>换控制器：只给显示出来的计时；离开界面时恢复不限条数、继续计时。</summary>
    void Subscribe(ToasterController? controller)
    {
        if (subscribed == controller) return;
        if (subscribed is not null)
        {
            subscribed.Changed -= OnChanged;
            subscribed.Limit = int.MaxValue;
            subscribed.Resume();
        }
        subscribed = controller;
        foreach (var item in items.Values) item.Dispose();
        items.Clear();
        SetFilter(controller is not null);
        hover.Enabled = controller is not null;
        if (controller is null) return;
        controller.Changed += OnChanged;
        controller.Limit = Max;
        Sync();
    }

    void OnChanged(object? sender, EventArgs e) => Sync();

    /// <summary>
    /// 按控制器排出显示的通知（最新的 Max 条）：新的进场，变了的更新，不在了的拿掉。挤到 Max 以外又被关掉的没有显示，等不到离场结束的那次移除，这里直接移除。
    /// </summary>
    void Sync()
    {
        if (subscribed is not { } controller || IsDisposed) return;
        var toasts = controller.Toasts;
        var visible = toasts.Skip(Math.Max(0, toasts.Count - Max)).ToList();
        foreach (var hidden in toasts.Take(toasts.Count - visible.Count).Where(toast => toast.Closing).ToList())
            BeginInvoke(() => controller.Remove(hidden.Id));
        foreach (var id in items.Keys.Except(visible.Select(toast => toast.Id)).ToList())
        {
            items[id].Dispose();
            items.Remove(id);
        }
        for (var index = 0; index < visible.Count; index++)
        {
            var toast = visible[index];
            if (!items.TryGetValue(toast.Id, out var item))
            {
                item = new ToastItemControl(this, toast.Id);
                items[toast.Id] = item;
                Controls.Add(item);
            }
            item.Order = index;
            item.Update(toast);
        }
        UpdatePause();
        Relayout();
    }

    void SetHover(bool value)
    {
        if (hovering == value) return;
        hovering = value;
        UpdatePause();
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        focused = true;
        UpdatePause();
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        focused = false;
        UpdatePause();
        Relayout();
    }

    /// <summary>悬停、焦点在区域里、窗体不在前台时暂停计时。</summary>
    void UpdatePause()
    {
        if (subscribed is null) return;
        if (hovering || focused || !windowActive) subscribed.Pause();
        else subscribed.Resume();
        foreach (var item in items.Values) item.SyncProgress();
    }

    void SetFilter(bool on)
    {
        if (on == filtering) return;
        filtering = on;
        if (on) Application.AddMessageFilter(this);
        else Application.RemoveMessageFilter(this);
    }

    /// <summary>F8 跳到通知区域（所在窗体在前台、有通知时；输入框里也能用），记下原来的焦点，最后一条关掉后还回去。</summary>
    bool IMessageFilter.PreFilterMessage(ref Message m)
    {
        if (m.Msg != WmKeyDown || (Keys)(int)m.WParam != Keys.F8 || items.Count == 0 || form is null || Form.ActiveForm != form || ContainsFocus) return false;
        outsideFocus = form.ActiveControl;
        Items.LastOrDefault(item => !item.Closing)?.FocusFirst();
        return true;
    }

    /// <summary>关掉一条：焦点在它里面时交给相邻的通知，没有时回到进入区域之前的位置。</summary>
    internal void Close(ToastItemControl item)
    {
        if (item.ContainsFocus)
        {
            var list = Items.Where(other => other == item || !other.Closing).ToList();
            var at = list.IndexOf(item);
            var neighbor = list.ElementAtOrDefault(at + 1) ?? list.ElementAtOrDefault(at - 1);
            if (neighbor is not null && neighbor != item) neighbor.FocusFirst();
            else if (outsideFocus is { IsDisposed: false, CanFocus: true } previous) previous.Focus();
        }
        subscribed?.Dismiss(item.Id);
    }

    /// <summary>离场结束：从控制器里移除。</summary>
    internal void Removed(ToastItemControl item) => subscribed?.Remove(item.Id);

    /// <summary>
    /// 排版：宽 min(26em, 父控件宽)，四周 lg 的边。通知排成一列（在顶部时最新的在最上面）；叠放且没展开时，最新的一条在原位，
    /// 更早的叠在它后面（每深一层错开 0.75em、两边各收 2.5%，第三层之后隐藏）。自己的大小正好包住通知，窗口区域裁到每条通知。
    /// </summary>
    internal void Relayout()
    {
        if (IsDisposed) return;
        var em = Em;
        var inset = (int)MathF.Round(Tokens.Space.Lg * em);
        var room = Parent?.DisplayRectangle.Width ?? int.MaxValue;
        var width = Math.Min((int)MathF.Round(26 * em), room);
        var inner = Math.Max(0, width - 2 * inset);
        var list = Items.ToList();
        var fromTop = Placement.FromTop;
        var stacked = Stack && !Expanded;
        var heights = list.Select(item => item.HeightFor(inner)).ToList();
        var front = list.LastOrDefault(item => !item.Closing);
        var frontHeight = front is null ? 0 : heights[list.IndexOf(front)];
        var total = stacked ? frontHeight : heights.Sum();
        var size = new Size(width, list.Count == 0 ? 0 : total + 2 * inset);
        // 深度只数还在显示的：正在离场的不占一层
        var depth = list.Count(item => !item.Closing);
        var depths = list.Select(item => item.Closing ? depth : --depth).ToList();
        var y = fromTop ? inset : size.Height - inset;
        var shape = new Region(Rectangle.Empty);
        for (var index = list.Count - 1; index >= 0; index--)
        {
            var item = list[index];
            var height = heights[index];
            int top;
            if (stacked && item != front && front is not null)
            {
                var level = depths[index];
                var shift = (int)MathF.Round(0.75f * em * level);
                top = fromTop ? inset + shift : size.Height - inset - height - shift;
                var narrow = (int)MathF.Round(inner * 0.025f * level);
                item.Visible = level <= 2;
                item.Place(new Rectangle(inset + narrow, top, inner - 2 * narrow, height));
                item.SendToBack();
            }
            else
            {
                if (fromTop)
                {
                    top = y;
                    y += height;
                }
                else
                {
                    y -= height;
                    top = y;
                }
                item.Visible = true;
                item.Place(new Rectangle(inset, top, inner, height));
                item.BringToFront();
            }
            if (!item.Visible) continue;
            using var path = item.Shape;
            shape.Union(path);
        }
        if (Size != size) Size = size;
        var old = Region;
        Region = shape;
        old?.Dispose();
        corner.Place();
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new RegionAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SetFilter(false);
            hover.Dispose();
            Subscribe(null);
        }
        base.Dispose(disposing);
    }

    /// <summary>区域对读屏是一组（TS 的 section aria-label），名称默认“通知”。</summary>
    sealed class RegionAccessibleObject(ToasterControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Grouping;

        public override string? Name => owner.AccessibleName is { Length: > 0 } name ? name : ProviderControl.LangOf(owner).ToasterLabel;
    }
}

/// <summary>
/// 显示区域里的一条通知（vue-tsx-pro 的 ToastItem）：AlertControl（surface）加上下 xs 的间距；进场从所在的边滑进来，
/// 离场高度收起（间距一起收掉）后从控制器里移除。焦点在里面时 Esc 关闭；横向拖动超过宽度的 35% 关闭，否则弹回。
/// </summary>
public sealed class ToastItemControl : Control
{
    readonly ToasterControl owner;
    readonly Signal<double> openness = new(1);
    readonly Signal<double> enter = new(0);
    readonly Signal<double> swipe = new(0);
    readonly Effect frame;
    readonly ProgressBar progress = new();
    readonly System.Windows.Forms.Timer ticker = new() { Interval = 30 };
    AlertControl alert;
    ToastRecord? toast;
    bool entered;
    bool removing;
    bool closeShown;
    bool iconOverridden;
    Point? pressedAt;
    bool swiping;
    double progressStart;
    long progressStamp;
    int progressCountdown = -1;

    internal ToastItemControl(ToasterControl owner, string id)
    {
        this.owner = owner;
        Id = id;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.ContainerControl, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        alert = NewAlert();
        Controls.Add(progress);
        Controls.Add(alert);
        frame = new Effect(OnFrame);
        using (frame.Track()) _ = (openness.Value, enter.Value, swipe.Value);
        ticker.Tick += (_, _) => PaintProgress();
    }

    /// <summary>通知的编号。</summary>
    public string Id { get; }

    /// <summary>在显示的通知里排第几（旧的在前）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int Order { get; set; }

    /// <summary>里面的 AlertControl（测试用）。</summary>
    internal AlertControl Alert => alert;

    /// <summary>剩余时间条（测试用）。</summary>
    internal Control ProgressBarPart => progress;

    /// <summary>正在离场。</summary>
    internal bool Closing => toast?.Closing == true;

    /// <summary>窗口区域里这一条占的形状（在区域里的坐标）：露出来的那段 AlertControl，md 圆角。</summary>
    internal GraphicsPath Shape
    {
        get
        {
            var height = Math.Min(alert.Height, Math.Max(0, Height - Gap));
            var radius = ElementContext.Of(this).Theme.Radii.Md * owner.Em;
            return Drawing.RoundedRectangle(new RectangleF(Left, Top + Gap, Width, height), radius);
        }
    }

    int Gap => (int)MathF.Round(Tokens.Space.Xs * owner.Em);

    AlertControl NewAlert()
    {
        var created = new AlertControl { Variant = AlertVariant.Surface };
        created.MouseDown += OnPressed;
        created.MouseMove += OnMoved;
        created.MouseUp += OnReleased;
        return created;
    }

    /// <summary>按通知的内容更新 AlertControl；第一次进场，关闭了就离场。</summary>
    internal void Update(ToastRecord record)
    {
        toast = record;
        var options = record.Options;
        // AlertControl 写过图标就回不到“按色调选”：要按色调选时换一个新的
        if (iconOverridden && options.ShowIcon != false && options.Icon is null) ReplaceAlert();
        alert.Tone = record.Tone;
        // 重要的通知打断播报；其余礼貌地念（标题、说明变了时念）
        alert.Live = record.Important ? AlertLive.Assertive : AlertLive.Polite;
        alert.IsLoading = record.Loading;
        alert.Action = options.Action as Control;
        if (options.ShowIcon == false) SetIcon(null);
        else if (options.Icon is { } icon) SetIcon(icon);
        alert.Title = options.Title ?? "";
        alert.Description = options.Description?.ToString() ?? "";
        SyncClose(record.Closable && !record.Closing);
        SyncProgress();
        if (!entered) SlideIn();
        if (record.Closing) Collapse();
    }

    void SetIcon(IconData? icon)
    {
        alert.Icon = icon;
        iconOverridden = true;
    }

    void ReplaceAlert()
    {
        var old = alert;
        alert = NewAlert();
        Controls.Add(alert);
        Controls.Remove(old);
        if (closeShown) old.CloseRequested -= OnCloseRequested;
        closeShown = false;
        old.Action = null;
        old.Dispose();
        iconOverridden = false;
        progress.BringToFront();
    }

    /// <summary>能关闭时 AlertControl 有关闭按钮：订阅了 CloseRequested 才显示它，不能关闭时退订。</summary>
    void SyncClose(bool closable)
    {
        if (closable == closeShown) return;
        closeShown = closable;
        if (closable) alert.CloseRequested += OnCloseRequested;
        else alert.CloseRequested -= OnCloseRequested;
    }

    void OnCloseRequested(object? sender, EventArgs e) => Dismiss();

    /// <summary>进场：从所在的边滑进来（md、enter）。</summary>
    void SlideIn()
    {
        entered = true;
        Animator.Animate(enter, (owner.Placement.FromTop ? -0.75 : 0.75) * owner.Em, TimeSpan.Zero);
        Animator.Animate(enter, 0, IsHandleCreated ? Tokens.Duration.Md : TimeSpan.Zero, Easing.Enter);
    }

    /// <summary>离场：高度收起（md），结束后从控制器里移除。</summary>
    async void Collapse()
    {
        if (removing) return;
        removing = true;
        var duration = Motion.Enabled ? Tokens.Duration.Md : TimeSpan.Zero;
        Animator.Animate(openness, 0, duration, Easing.Standard);
        await Task.Delay(duration);
        if (!IsDisposed) owner.Removed(this);
    }

    /// <summary>关闭这一条（关闭按钮、Esc、滑走）。</summary>
    void Dismiss()
    {
        if (toast is not { Closable: true, Closing: false }) return;
        owner.Close(this);
    }

    /// <summary>焦点交给这一条：第一个能聚焦的（操作、关闭按钮）。</summary>
    internal void FocusFirst() => SelectNextControl(null, true, true, true, true);

    /// <summary>这一条要多高：（AlertControl + 上下 xs）× 展开的比例。</summary>
    internal int HeightFor(int width) => (int)Math.Round((alert.GetPreferredSize(new Size(width, 0)).Height + 2 * Gap) * openness.Peek());

    /// <summary>区域给的位置；进场、滑动的位移叠在上面。</summary>
    internal void Place(Rectangle rectangle)
    {
        var bounds = rectangle with { X = rectangle.X + (int)Math.Round(swipe.Peek()), Y = rectangle.Y + (int)Math.Round(enter.Peek()) };
        if (Bounds != bounds) Bounds = bounds;
        LayoutContent();
    }

    void OnFrame()
    {
        using (frame.Track()) _ = (openness.Value, enter.Value, swipe.Value);
        owner.Relayout();
    }

    /// <summary>AlertControl 在上下 xs 之间（收起时裁掉下面）；剩余时间条贴着通知底边，左右离 md、离底 xs 加描边。</summary>
    void LayoutContent()
    {
        var gap = Gap;
        var height = alert.GetPreferredSize(new Size(Width, 0)).Height;
        alert.Bounds = new Rectangle(0, gap, Width, height);
        var inset = (int)MathF.Round(Tokens.Space.Md * owner.Em);
        var bar = Math.Max(1, LogicalToDeviceUnits(2));
        progress.Full = new Rectangle(inset, gap + height - bar - Math.Max(1, LogicalToDeviceUnits(1)), Math.Max(0, Width - 2 * inset), bar);
        PaintProgress();
    }

    /// <summary>剩余时间条：自动关闭、开了 Progress 才显示；每一轮计时从满开始，暂停时停住。</summary>
    internal void SyncProgress()
    {
        var countdown = toast?.Countdown;
        progress.Visible = owner.Progress && countdown is not null;
        if (countdown is not { } current)
        {
            ticker.Stop();
            return;
        }
        if (current.Id != progressCountdown)
        {
            progressCountdown = current.Id;
            progressStart = 1;
            progressStamp = TimeProvider.System.GetTimestamp();
        }
        if (owner.Subscribed?.Paused == true)
        {
            if (progressStamp != 0) progressStart = Fraction(current);
            progressStamp = 0;
        }
        else if (progressStamp == 0) progressStamp = TimeProvider.System.GetTimestamp();
        ticker.Enabled = progress.Visible && progressStamp != 0;
        PaintProgress();
    }

    double Fraction(ToastCountdown countdown) =>
        progressStamp == 0 ? progressStart : Math.Max(0, progressStart - TimeProvider.System.GetElapsedTime(progressStamp) / countdown.Duration);

    /// <summary>剩余的比例换成宽度（从结束边往起始边收）。</summary>
    void PaintProgress()
    {
        if (toast?.Countdown is not { } countdown || !progress.Visible) return;
        var fraction = Fraction(countdown);
        var full = progress.Full;
        var width = (int)Math.Round(full.Width * fraction);
        var x = RightToLeft == RightToLeft.Yes ? full.Right - width : full.X;
        progress.Bounds = new Rectangle(x, full.Y, width, full.Height);
        progress.BringToFront();
        if (fraction <= 0) ticker.Stop();
    }

    /// <summary>焦点在通知里时 Esc 关闭它（不能关闭的不处理）。</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData != Keys.Escape || toast is not { Closable: true, Closing: false }) return base.ProcessCmdKey(ref msg, keyData);
        Dismiss();
        return true;
    }

    void OnPressed(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || toast is not { Closable: true, Closing: false }) return;
        pressedAt = Cursor.Position;
    }

    /// <summary>横向拖动跟手；挪开 4 以内不算。</summary>
    void OnMoved(object? sender, MouseEventArgs e)
    {
        if (pressedAt is not { } from || MouseButtons != MouseButtons.Left) return;
        var dx = Cursor.Position.X - from.X;
        if (!swiping)
        {
            if (Math.Abs(dx) < LogicalToDeviceUnits(4)) return;
            swiping = true;
        }
        Animator.Animate(swipe, dx, TimeSpan.Zero);
    }

    /// <summary>松开：超过宽度的 35% 就沿滑动方向滑出再关闭，不够就弹回。</summary>
    async void OnReleased(object? sender, MouseEventArgs e)
    {
        pressedAt = null;
        if (!swiping) return;
        swiping = false;
        var width = Math.Max(1, Width);
        if (Math.Abs(swipe.Peek()) <= width * 0.35)
        {
            Animator.Animate(swipe, 0, Tokens.Duration.Md, Easing.Enter);
            return;
        }
        Animator.Animate(swipe, Math.Sign(swipe.Peek()) * width, Tokens.Duration.Sm, Easing.Exit);
        await Task.Delay(Motion.Enabled ? Tokens.Duration.Sm : TimeSpan.Zero);
        Dismiss();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Animator.Stop(openness);
            Animator.Stop(enter);
            Animator.Stop(swipe);
            ticker.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>剩余时间条：圆角 full，textMuted 与表面色各半（控件不能半透明）。</summary>
    sealed class ProgressBar : Control
    {
        public ProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            Visible = false;
        }

        /// <summary>满的时候的位置。</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Rectangle Full { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var colors = ElementContext.Of(this).Theme.Colors;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var shape = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), Height / 2f);
            using var fill = new SolidBrush(Drawing.Mix(colors.Surface, colors.TextMuted, 0.5f));
            e.Graphics.FillPath(fill, shape);
        }
    }
}
