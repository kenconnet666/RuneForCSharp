using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 文字提示（vue-tsx-pro 的 Tooltip）：放进窗体的组件，同平台的 ToolTip 一样给每个控件多出几个属性（设计器里是"Tooltip 在 tooltipProvider1 上"）。
/// <code>
/// var tips = new TooltipProvider(components);
/// tips.SetTooltip(copyButton, "复制（Ctrl+C）");
/// tips.SetTooltipPlacement(moreButton, FloatingPlacement.RightStart);
/// tips.SetTooltipArrow(moreButton, true);
/// </code>
/// <list type="bullet">
/// <item>外观：反色底、sm 字号、sm 圆角，最宽 20em；淡入（durations.md、enter）。窗口是组件库画的（平台的提示窗口画不出圆角与箭头）。</item>
/// <item>时机：指针停留 <see cref="Delay"/>（默认 500 毫秒）后显示，刚关掉一个提示（300 毫秒内）时直接显示；指针离开 100 毫秒后隐藏，
/// 中间移到提示上不隐藏；按下触发器隐藏；同一时间只有一个。</item>
/// <item>键盘：Tab、方向键聚焦触发器时立即显示，失焦、Esc 隐藏。</item>
/// <item>位置：<see cref="SetTooltipPlacement"/>（12 种，默认 top），放不下时翻到另一边、贴着屏幕边挪回来；<see cref="Offset"/> 是与触发器的距离；
/// <see cref="SetTooltipArrow"/> 显示指向触发器的箭头。</item>
/// <item>读屏：提示的文字是触发器的说明（AccessibleDescription，TS 的 aria-describedby），使用方自己写了的不动。</item>
/// </list>
/// </summary>
[ToolboxItem(true)]
[ProvideProperty("Tooltip", typeof(Control))]
[ProvideProperty("TooltipPlacement", typeof(Control))]
[ProvideProperty("TooltipArrow", typeof(Control))]
[Description("文字提示：悬停或键盘聚焦时显示在控件旁边。")]
public class TooltipProvider : Component, IExtenderProvider
{
    /// <summary>刚关掉一个提示后多久之内，下一个直接显示（同 Radix 的 skipDelayDuration）。</summary>
    static readonly TimeSpan SkipDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>指针离开后多久隐藏：留出移到提示上的时间。</summary>
    const int CloseDelay = 100;

    /// <summary>所有提示共用一个窗口（同一时间只显示一个）。</summary>
    static LayeredWindow? window;

    /// <summary>正显示着提示的控件与它的提供者。</summary>
    static (TooltipProvider Provider, Control Control)? shown;

    static DateTime lastClosed;

    /// <summary>最近一次输入是键盘（键盘聚焦才显示，同 :focus-visible）。</summary>
    static bool keyboardMostRecent;

    static bool filterInstalled;

    readonly Dictionary<Control, Entry> entries = [];
    readonly System.Windows.Forms.Timer timer = new();
    Action? pending;

    /// <summary>新建（代码里用）。</summary>
    public TooltipProvider()
    {
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var run = pending;
            pending = null;
            run?.Invoke();
        };
        InstallFilter();
    }

    /// <summary>新建并放进容器（设计器生成的代码用，窗体关闭时一起释放）。</summary>
    public TooltipProvider(IContainer container) : this() => container.Add(this);

    /// <summary>显示了一个提示（<see cref="Show"/>、悬停、聚焦）。</summary>
    [Category("Behavior")]
    [Description("显示了一个提示。")]
    public event EventHandler<TooltipEventArgs>? Opened;

    /// <summary>隐藏了一个提示。</summary>
    [Category("Behavior")]
    [Description("隐藏了一个提示。")]
    public event EventHandler<TooltipEventArgs>? Closed;

    /// <summary>指针停留多久后显示（毫秒），默认 500。</summary>
    [Category("Behavior")]
    [Description("指针停留多久后显示（毫秒）。")]
    [DefaultValue(500)]
    public int Delay { get; set; } = 500;

    /// <summary>与触发器的距离（设备无关像素），默认 6；有箭头时另加箭头伸出的一段。</summary>
    [Category("Appearance")]
    [Description("与触发器的距离（设备无关像素）。")]
    [DefaultValue(6f)]
    public float Offset { get; set; } = 6;

    /// <summary>显示提示；false 时只是不显示（TS 的 disabled）。</summary>
    [Category("Behavior")]
    [Description("显示提示；关掉时悬停、聚焦都不显示。")]
    [DefaultValue(true)]
    public bool Active
    {
        get;
        set
        {
            field = value;
            if (!value) Hide();
        }
    } = true;

    /// <summary>正显示着提示的控件；没有时为 null。</summary>
    [Browsable(false)]
    public Control? Current => shown is { } current && current.Provider == this ? current.Control : null;

    /// <inheritdoc />
    public bool CanExtend(object extendee) => extendee is Control and not Form;

    /// <summary>取控件的提示文字。</summary>
    [Category("Misc")]
    [Description("指针停留或键盘聚焦时显示的提示。")]
    [DefaultValue(null)]
    [Localizable(true)]
    public string? GetTooltip(Control control) => entries.TryGetValue(control, out var entry) ? entry.Text : null;

    /// <summary>设控件的提示文字；null 或空时去掉提示。</summary>
    public void SetTooltip(Control control, string? text)
    {
        var entry = Attach(control);
        if (entry.OwnsDescription || string.IsNullOrEmpty(control.AccessibleDescription))
        {
            control.AccessibleDescription = string.IsNullOrEmpty(text) ? null : text;
            entry.OwnsDescription = true;
        }
        entry.Text = text;
        if (Current == control) Show(control);
    }

    /// <summary>取控件的提示位置。</summary>
    [Category("Misc")]
    [Description("提示的位置：top、top-start、bottom-end、left、right-start……")]
    public FloatingPlacement GetTooltipPlacement(Control control) => entries.TryGetValue(control, out var entry) ? entry.Placement : FloatingPlacement.Top;

    /// <summary>设控件的提示位置。</summary>
    public void SetTooltipPlacement(Control control, FloatingPlacement placement) => Attach(control).Placement = placement;

    /// <summary>取控件的提示是否带箭头。</summary>
    [Category("Misc")]
    [Description("提示带指向控件的箭头。")]
    [DefaultValue(false)]
    public bool GetTooltipArrow(Control control) => entries.TryGetValue(control, out var entry) && entry.Arrow;

    /// <summary>设控件的提示是否带箭头。</summary>
    public void SetTooltipArrow(Control control, bool arrow) => Attach(control).Arrow = arrow;

    bool ShouldSerializeTooltipPlacement(Control control) => GetTooltipPlacement(control) != FloatingPlacement.Top;

    void ResetTooltipPlacement(Control control) => SetTooltipPlacement(control, FloatingPlacement.Top);

    /// <summary>立即显示控件的提示（受控，TS 的 _open）；没有提示文字时不显示。</summary>
    public void Show(Control control)
    {
        if (!entries.TryGetValue(control, out var entry) || string.IsNullOrEmpty(entry.Text) || !control.IsHandleCreated) return;
        Schedule(null, 0);
        if (shown is { } previous && previous.Control != control) previous.Provider.Hide();
        window ??= new LayeredWindow();
        var (bitmap, at, panel, side) = Render(control, entry);
        var duration = Motion.Enabled ? Tokens.Duration.Md : TimeSpan.Zero;
        window.Present(bitmap, at, panel, side, Current == control ? TimeSpan.Zero : duration);
        var opened = Current != control;
        shown = (this, control);
        if (opened) Opened?.Invoke(this, new TooltipEventArgs(control));
    }

    /// <summary>隐藏正显示的提示（只隐藏这个提供者的）。</summary>
    public void Hide()
    {
        Schedule(null, 0);
        if (Current is not { } control) return;
        window?.Dismiss();
        shown = null;
        lastClosed = DateTime.UtcNow;
        Closed?.Invoke(this, new TooltipEventArgs(control));
    }

    /// <summary>提示窗口（测试用）。</summary>
    internal static LayeredWindow? Window => window;

    Entry Attach(Control control)
    {
        if (entries.TryGetValue(control, out var entry)) return entry;
        entry = new Entry();
        entries[control] = entry;
        control.MouseEnter += OnMouseEnter;
        control.MouseLeave += OnMouseLeave;
        control.MouseDown += OnMouseDown;
        control.GotFocus += OnGotFocus;
        control.LostFocus += OnLostFocus;
        control.Disposed += OnDisposed;
        return entry;
    }

    void Detach(Control control)
    {
        if (!entries.Remove(control)) return;
        control.MouseEnter -= OnMouseEnter;
        control.MouseLeave -= OnMouseLeave;
        control.MouseDown -= OnMouseDown;
        control.GotFocus -= OnGotFocus;
        control.LostFocus -= OnLostFocus;
        control.Disposed -= OnDisposed;
        if (Current == control) Hide();
    }

    void OnDisposed(object? sender, EventArgs e) => Detach((Control)sender!);

    bool CanShow(Control control) => Active && control.Enabled && !string.IsNullOrEmpty(GetTooltip(control));

    void OnMouseEnter(object? sender, EventArgs e)
    {
        var control = (Control)sender!;
        if (!CanShow(control)) return;
        // 刚有提示显示着、或刚关掉（skipDelay 内）：直接显示，在一排按钮上移动不用每次等
        var warm = shown is not null || DateTime.UtcNow - lastClosed < SkipDelay;
        Schedule(() => Show(control), warm ? 0 : Delay);
    }

    void OnMouseLeave(object? sender, EventArgs e) => CloseSoon();

    void OnMouseDown(object? sender, MouseEventArgs e) => Hide();

    void OnGotFocus(object? sender, EventArgs e)
    {
        var control = (Control)sender!;
        if (keyboardMostRecent && CanShow(control)) Show(control);
    }

    void OnLostFocus(object? sender, EventArgs e)
    {
        if (Current == sender) Hide();
    }

    /// <summary>指针离开触发器：稍后隐藏；这段时间里移到了提示上就不隐藏，离开提示后再隐藏。</summary>
    void CloseSoon() => Schedule(() =>
    {
        if (window is { Visible: true } && window.Bounds.Contains(Cursor.Position))
        {
            CloseSoon();
            return;
        }
        if (Current is { } control && control.IsHandleCreated && control.RectangleToScreen(control.ClientRectangle).Contains(Cursor.Position)) return;
        Hide();
    }, CloseDelay);

    void Schedule(Action? action, int delay)
    {
        timer.Stop();
        pending = action;
        if (action is null) return;
        if (delay <= 0)
        {
            pending = null;
            action();
            return;
        }
        timer.Interval = delay;
        timer.Start();
    }

    /// <summary>
    /// 画好提示（面板、文字、箭头），算出窗口在屏幕上的位置：要的那一边放不下时翻到对面，再贴着屏幕边挪回来（与边缘至少隔 8 像素）。
    /// 箭头伸出面板的一段在窗口里四周都留着（透明），面板与触发器之间隔开 <see cref="Offset"/> 加箭头伸出的一段。
    /// </summary>
    (Bitmap Bitmap, Point At, Rectangle Panel, string? ArrowSide) Render(Control control, Entry entry)
    {
        var context = ElementContext.Of(control).Sized(TextSize.Sm);
        var colors = context.Theme.Colors;
        context = context with { ForeColor = colors.InverseText };
        var em = context.Em;
        var text = new Text { Content = entry.Text ?? "" };
        var paddingX = (int)MathF.Round(0.5f * em);
        var paddingY = (int)MathF.Round(0.25f * em);
        var content = text.Measure(context, new Size((int)(20 * em) - 2 * paddingX, int.MaxValue));
        var panelSize = new Size(content.Width + 2 * paddingX, content.Height + 2 * paddingY);
        var arrow = entry.Arrow ? 0.625f * em : 0;
        var overhang = (int)MathF.Ceiling(0.71f * arrow);
        var gap = context.Scale(Offset) + 0.7f * arrow;

        var target = control.RectangleToScreen(control is IElementHost ? Deflate(control.ClientRectangle, control.Padding) : control.ClientRectangle);
        var area = Screen.FromControl(control).WorkingArea;
        var (side, panel) = FloatingLayout.Position(entry.Placement, context.RightToLeft, target, panelSize, gap, area);

        var bitmap = LayeredWindow.Canvas(new Size(panelSize.Width + 2 * overhang, panelSize.Height + 2 * overhang));
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var inner = new Rectangle(overhang, overhang, panelSize.Width, panelSize.Height);
            using var fill = new SolidBrush(colors.InverseSurface);
            if (entry.Arrow) PaintArrow(graphics, fill, inner, side, target, panel.Location, arrow);
            var radius = context.Theme.Radii.Sm * em;
            using (var path = Drawing.RoundedRectangle(inner, radius)) graphics.FillPath(fill, path);
            PaintText(graphics, text, context, inner, paddingX, paddingY, colors.InverseSurface);
        }
        return (bitmap, new Point(panel.X - overhang, panel.Y - overhang), panel, entry.Arrow ? side : null);
    }

    static Rectangle Deflate(Rectangle bounds, Padding padding) =>
        new(bounds.X + padding.Left, bounds.Y + padding.Top, Math.Max(0, bounds.Width - padding.Horizontal), Math.Max(0, bounds.Height - padding.Vertical));

    /// <summary>箭头：旋转 45° 的小方块，跨在朝向触发器的那条边上、对准触发器的中心（离面板两端至少一个箭头宽），尖角的圆角 2 像素。</summary>
    static void PaintArrow(Graphics graphics, Brush fill, Rectangle inner, string side, Rectangle target, Point panelOnScreen, float size)
    {
        var vertical = side is "top" or "bottom";
        var center = vertical ? target.Left + target.Width / 2f - panelOnScreen.X : target.Top + target.Height / 2f - panelOnScreen.Y;
        var length = vertical ? inner.Width : inner.Height;
        var cross = Math.Clamp(center, 1.5f * size, Math.Max(1.5f * size, length - 1.5f * size));
        var point = side switch
        {
            "top" => new PointF(inner.Left + cross, inner.Bottom),
            "bottom" => new PointF(inner.Left + cross, inner.Top),
            "left" => new PointF(inner.Right, inner.Top + cross),
            _ => new PointF(inner.Left, inner.Top + cross),
        };
        var state = graphics.Save();
        graphics.TranslateTransform(point.X, point.Y);
        graphics.RotateTransform(45);
        using var path = Drawing.RoundedRectangle(new RectangleF(-size / 2, -size / 2, size, size), 0);
        graphics.FillPath(fill, path);
        graphics.Restore(state);
    }

    /// <summary>
    /// 文字：GDI 的文字（TextRenderer，与别的元素一致）画在不透明的底上，再贴进面板。直接画到透明位图上时 GDI 把文字像素的不透明度写成 0。
    /// </summary>
    static void PaintText(Graphics graphics, Text text, ElementContext context, Rectangle inner, int paddingX, int paddingY, Color background)
    {
        var area = new Rectangle(inner.X + paddingX, inner.Y + paddingY, inner.Width - 2 * paddingX, inner.Height - 2 * paddingY);
        if (area.Width <= 0 || area.Height <= 0) return;
        using var layer = new Bitmap(area.Width, area.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        // 位图默认 96 DPI：GDI 按它把磅值换成像素，与量文字时（宿主的 DPI）对不上
        layer.SetResolution(context.Dpi, context.Dpi);
        using (var g = Graphics.FromImage(layer))
        {
            g.Clear(background);
            text.Bounds = new Rectangle(Point.Empty, area.Size);
            text.Paint(g, context);
        }
        graphics.DrawImageUnscaled(layer, area.Location);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var control in entries.Keys.ToArray()) Detach(control);
            timer.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>记下最近的输入是键盘还是指针；Esc 隐藏正显示的提示（吃掉这次按键：提示在对话框里时不关对话框）。</summary>
    static void InstallFilter()
    {
        if (filterInstalled) return;
        filterInstalled = true;
        Application.AddMessageFilter(new InputFilter());
    }

    internal sealed class InputFilter : IMessageFilter
    {
        const int WmKeyDown = 0x100, WmSysKeyDown = 0x104, WmLButtonDown = 0x201, WmRButtonDown = 0x204, WmMButtonDown = 0x207;

        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WmKeyDown or WmSysKeyDown:
                    keyboardMostRecent = true;
                    if ((Keys)(int)m.WParam == Keys.Escape && shown is { } current)
                    {
                        current.Provider.Hide();
                        return true;
                    }
                    break;
                case WmLButtonDown or WmRButtonDown or WmMButtonDown:
                    keyboardMostRecent = false;
                    break;
            }
            return false;
        }
    }

    sealed class Entry
    {
        public string? Text;
        public FloatingPlacement Placement = FloatingPlacement.Top;
        public bool Arrow;
        public bool OwnsDescription;
    }
}

/// <summary>提示显示、隐藏的参数。</summary>
/// <param name="control">提示所属的控件。</param>
public sealed class TooltipEventArgs(Control control) : EventArgs
{
    /// <summary>提示所属的控件。</summary>
    public Control Control { get; } = control;
}
