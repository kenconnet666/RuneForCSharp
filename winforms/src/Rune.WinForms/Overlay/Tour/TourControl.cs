using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>引导的一步（vue-tsx-pro 的 TourStep）。</summary>
public sealed class TourStep
{
    /// <summary>高亮的目标；不写（或还没显示出来）时卡片居中、整窗遮罩。</summary>
    public Control? Target { get; init; }

    /// <summary>按需取目标（目标在打开引导后才建出来时）；写了时代替 Target。</summary>
    public Func<Control?>? TargetFactory { get; init; }

    /// <summary>标题（卡片的读屏名称）。</summary>
    public required string Title { get; init; }

    /// <summary>说明。</summary>
    public string? Content { get; init; }

    /// <summary>标题上方的封面：插图、截图（每次显示这一步时调用，建一个新控件）。</summary>
    public Func<Control>? Cover { get; init; }

    /// <summary>卡片在目标的哪一侧，默认下方；空间不够时翻转。</summary>
    public FloatingPlacement? Placement { get; init; }
}

/// <summary>
/// 新手引导（vue-tsx-pro 的 Tour）：一步一步高亮窗体里的控件并说明。遮罩盖住整个窗体，目标处镂空；步骤卡片贴着目标显示。
/// <code>
/// var tour = new TourControl { Steps = [new() { Target = search, Title = "搜索", Content = "按名称或编号查找订单" }, new() { Title = "就这些" }] };
/// form.Controls.Add(tour);   tour.IsOpen = true;
/// </code>
/// <list type="bullet">
/// <item>模态：遮罩是半透明的分层窗口（目标处挖空），卡片用浮层（lg 圆角、lg 阴影）；打开期间宿主窗体不接受输入（同 DialogControl），Esc 关闭，关闭后焦点还给打开前的控件。</item>
/// <item>目标：控件（或按需取的函数）；宿主窗体移动、变大小时镂空与卡片跟着动；没有目标时卡片居中、整窗遮罩。</item>
/// <item>卡片：封面、标题、说明、步数、上一步 / 下一步（最后一步是“完成”）、跳过。键盘 → / ← 下一步 / 上一步（从右到左时对调，焦点在输入框里时不处理）。</item>
/// <item>点“完成”时发 <see cref="Finished"/>；<see cref="OpenChanged"/> 报告关闭的原因（跳过是 close，完成是 confirm，Esc 是 escape）。</item>
/// </list>
/// 引导放在哪里都行（自己不可见、不参与排版）。
/// </summary>
[DefaultEvent(nameof(Finished))]
[Description("新手引导：遮罩镂空高亮目标，卡片说明，一步一步往下走。")]
public class TourControl : Control
{
    readonly FloatingSurface surface;
    readonly LayeredWindow scrim = new();
    readonly TourLayout layout;
    readonly H2Control title = new() { TextSize = TextSize.Md };
    readonly ParagraphControl content = new() { Tone = TextTone.Muted };
    readonly FlexControl footer = new() { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true };
    readonly TextControl progress = new() { TextSize = TextSize.Sm, Tone = TextTone.Muted };
    readonly ButtonControl skip = new() { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, TextSize = TextSize.Sm };
    readonly ButtonControl previous = new() { Variant = ButtonVariant.Outline, TextSize = TextSize.Sm };
    readonly ButtonControl next = new() { TextSize = TextSize.Sm };
    Form? host;
    Control? returnFocus;
    Control? cover;
    bool open;

    /// <summary>新建：遮罩、卡片（封面、标题、说明、底栏）。</summary>
    public TourControl()
    {
        // 自己不参与排版：引导画在遮罩、卡片两层窗口里
        Visible = false;
        SetStyle(ControlStyles.Selectable, false);
        surface = new FloatingSurface(this);
        surface.Form.AccessibleRole = AccessibleRole.Dialog;
        surface.Form.KeyPreview = true;
        layout = new TourLayout(this);
        footer.Controls.AddRange([progress, skip, previous, next]);
        footer.SetGrow(progress, true);
        layout.Controls.AddRange([title, content, footer]);
        layout.Dock = DockStyle.Fill;
        surface.Form.Controls.Add(layout);
        surface.Form.Escape += (_, _) => Close(OpenChangeReason.Escape);
        surface.Form.KeyDown += (_, e) => e.Handled = e.SuppressKeyPress = HandleKey(e.KeyData, surface.Form.ActiveControl);
        skip.Click += (_, _) => Close(OpenChangeReason.Close);
        previous.Click += (_, _) => Go(Current - 1);
        next.Click += (_, _) => Next();
        Sync();
    }

    /// <summary>各步。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<TourStep> Steps { get; set { field = value; Sync(); } } = [];

    /// <summary>开着。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => open; set => SetOpen(value, null); }

    /// <summary>当前第几步，从 0 开始。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Current
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, Math.Max(0, Steps.Count - 1));
            Sync();
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>镂空比目标大出多少（逻辑像素），默认 6。</summary>
    [Category("Appearance")]
    [DefaultValue(6)]
    public int SpotlightPadding { get; set; } = 6;

    /// <summary>下一步可用，默认是：完成某个操作后才能继续时先设为 false。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool CanGoNext { get; set { field = value; Sync(); } } = true;

    /// <summary>当前步变了。</summary>
    public event EventHandler? CurrentChanged;

    /// <summary>点了最后一步的“完成”。</summary>
    public event EventHandler? Finished;

    /// <summary>用户关了（跳过、完成、Esc）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>卡片的部件（测试用）。</summary>
    internal (H2Control Title, ParagraphControl Content, TextControl Progress, ButtonControl Skip, ButtonControl Previous, ButtonControl Next, Control? Cover) Parts =>
        (title, content, progress, skip, previous, next, cover);

    /// <summary>卡片的窗口（测试用）。</summary>
    internal Form PanelForm => surface.Form;

    /// <summary>镂空在屏幕上的位置（没有目标时为 null，测试用）。</summary>
    internal Rectangle? Spotlight { get; private set; }

    TourStep? Step => Steps.Count == 0 ? null : Steps[Math.Clamp(Current, 0, Steps.Count - 1)];

    bool Last => Current >= Steps.Count - 1;

    void Go(int step) => Current = step;

    /// <summary>下一步；最后一步时完成：关闭并发 Finished。</summary>
    void Next()
    {
        if (!Last)
        {
            Go(Current + 1);
            return;
        }
        SetOpen(false, OpenChangeReason.Confirm);
        Finished?.Invoke(this, EventArgs.Empty);
    }

    void Close(OpenChangeReason reason) => SetOpen(false, reason);

    /// <summary>→ / ← 切换步骤（从右到左时对调）；焦点在输入框里时留给它，下一步不可用时不前进；处理了返回 true（测试也从这里进来）。</summary>
    internal bool HandleKey(Keys key, Control? focused)
    {
        if (key is not (Keys.Right or Keys.Left) || focused is TextBoxBase) return false;
        var forward = (key == Keys.Right) != (RightToLeft == RightToLeft.Yes);
        if (forward && CanGoNext) Next();
        else if (!forward && Current > 0) Go(Current - 1);
        else return false;
        return true;
    }

    void SetOpen(bool value, OpenChangeReason? reason)
    {
        if (value == open) return;
        open = value;
        if (value) Open();
        else Dismiss();
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(value, known));
    }

    void Open()
    {
        host = FindForm();
        if (host is null) return;
        returnFocus = host.ActiveControl;
        host.Move += OnHostChanged;
        host.Resize += OnHostChanged;
        Sync();
        // 同 ShowDialog：打开期间宿主窗体不接受输入（Win32 层禁用，不改 WinForms 的 Enabled）
        EnableWindow(host.Handle, false);
    }

    void Dismiss()
    {
        surface.Hide();
        scrim.Dismiss();
        Spotlight = null;
        if (host is null) return;
        host.Move -= OnHostChanged;
        host.Resize -= OnHostChanged;
        EnableWindow(host.Handle, true);
        host.Activate();
        returnFocus?.Focus();
        (host, returnFocus) = (null, null);
    }

    void OnHostChanged(object? sender, EventArgs e) => Present();

    /// <summary>换了一步：卡片的内容与按钮，再摆遮罩与卡片。</summary>
    void Sync()
    {
        if (layout is null) return;
        var lang = ProviderControl.LangOf(this);
        var step = Step;
        if (cover is not null)
        {
            layout.Controls.Remove(cover);
            cover.Dispose();
        }
        cover = step?.Cover?.Invoke();
        if (cover is not null) layout.Controls.Add(cover);
        title.Text = step?.Title ?? "";
        surface.Form.AccessibleName = step?.Title;
        surface.Form.AccessibleDescription = step?.Content;
        content.Text = step?.Content ?? "";
        content.Visible = step?.Content is not null;
        progress.Text = lang.TourProgress(Current + 1, Steps.Count);
        skip.Text = lang.TourSkip;
        skip.Visible = !Last;
        previous.Text = lang.TourPrevious;
        previous.Visible = Current > 0;
        next.Text = Last ? lang.TourFinish : lang.TourNext;
        next.Enabled = CanGoNext;
        layout.PerformLayout();
        if (open) Present();
    }

    /// <summary>
    /// 遮罩铺满宿主窗体的客户区（目标处挖掉一个 md 圆角的矩形），卡片贴着目标（默认下方，放不下时翻转），没有目标时居中；
    /// 卡片宽 min(22em, 窗体宽 − 2em)，高按内容。
    /// </summary>
    void Present()
    {
        if (host is null || !host.IsHandleCreated) return;
        var context = ElementContext.Of(this);
        var area = host.RectangleToScreen(host.ClientRectangle);
        var step = Step;
        var target = step?.TargetFactory?.Invoke() ?? step?.Target;
        Spotlight = target is { Visible: true, IsHandleCreated: true } ? Rectangle.Inflate(target.RectangleToScreen(target.ClientRectangle), LogicalToDeviceUnits(SpotlightPadding), LogicalToDeviceUnits(SpotlightPadding)) : null;
        var bitmap = LayeredWindow.Canvas(area.Size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(context.Theme.Colors.Overlay);
            if (Spotlight is { } hole)
            {
                // 挖空：用透明色直接覆盖（不混合）
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.CompositingMode = CompositingMode.SourceCopy;
                var local = hole with { X = hole.X - area.X, Y = hole.Y - area.Y };
                using var shape = Drawing.RoundedRectangle(local, context.Theme.Radii.Md * context.Em);
                using var clear = new SolidBrush(Color.Transparent);
                graphics.FillPath(clear, shape);
            }
        }
        if (scrim.Owner != host) scrim.Owner = host;
        scrim.Present(bitmap, area.Location, new Rectangle(area.Location, area.Size), null, TimeSpan.Zero);
        var border = FloatingSurface.Border(context);
        surface.Form.Font = Font;
        var em = context.Em;
        var width = (int)Math.Min(22 * em, Math.Max(0, area.Width - 2 * em));
        // 窗口显示之前里面的控件还没建句柄，量出的高度偏小：显示之后再量一次，变了就重新摆
        for (var pass = 0; pass < 2; pass++)
        {
            var height = layout.PreferredHeight(width - 2 * border) + 2 * border;
            Rectangle panel;
            if (Spotlight is { } hole)
            {
                panel = FloatingLayout.Position(step?.Placement ?? FloatingPlacement.Bottom, RightToLeft == RightToLeft.Yes, hole, new Size(width, height), LogicalToDeviceUnits(8), area, crossAxisFallback: true).Panel;
            }
            else panel = new Rectangle(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
            if (pass > 0 && surface.Backdrop?.Panel == panel) break;
            surface.Show(panel, border, context, null);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (open && host is not null) EnableWindow(host.Handle, true);
            surface.Dispose();
            scrim.Dispose();
        }
        base.Dispose(disposing);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    static extern bool EnableWindow(IntPtr hWnd, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool enable);

    /// <summary>卡片里的排版：封面贴着卡片的上、左、右边，下面是四周 lg 的一列（标题、说明、底栏，之间 sm，底栏上方多留 xs）。</summary>
    sealed class TourLayout(TourControl owner) : Control
    {
        public override LayoutEngine LayoutEngine => ManualLayout.Instance;

        int Px(float space) => (int)MathF.Round(space * owner.EmPixels());

        /// <summary>封面的高度（按卡片的宽度量）。</summary>
        int CoverHeight(int width) => owner.cover is { } cover ? cover.GetPreferredSize(new Size(width, 0)).Height : 0;

        IEnumerable<Control> Column => new Control[] { owner.title, owner.content, owner.footer }.Where(control => control.Visible);

        public int PreferredHeight(int width)
        {
            var (sm, lg) = (Px(Tokens.Space.Sm), Px(Tokens.Space.Lg));
            var inner = Math.Max(0, width - 2 * lg);
            var top = owner.cover is null ? lg : CoverHeight(width) + Px(Tokens.Space.Xs) + sm;
            var column = Column.ToList();
            var heights = column.Sum(control => control.GetPreferredSize(new Size(inner, 0)).Height) + sm * Math.Max(0, column.Count - 1) + Px(Tokens.Space.Xs);
            return top + heights + lg;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var (sm, lg) = (Px(Tokens.Space.Sm), Px(Tokens.Space.Lg));
            var width = ClientSize.Width;
            var inner = Math.Max(0, width - 2 * lg);
            var y = lg;
            if (owner.cover is { } cover)
            {
                var height = CoverHeight(width);
                cover.Bounds = new Rectangle(0, 0, width, height);
                y = height + Px(Tokens.Space.Xs) + sm;
            }
            foreach (var control in Column)
            {
                // 底栏上方多留 xs
                if (control == owner.footer) y += Px(Tokens.Space.Xs);
                var height = control.GetPreferredSize(new Size(inner, 0)).Height;
                control.Bounds = new Rectangle(lg, y, inner, height);
                y += height + sm;
            }
        }
    }
}
