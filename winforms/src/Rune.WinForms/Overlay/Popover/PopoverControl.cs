using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>为什么开、关（vue-tsx-pro 的 OpenChangeReason）。</summary>
public enum OpenChangeReason
{
    /// <summary>点了触发器。</summary>
    Trigger,

    /// <summary>点了浮层外面（浮层窗口失去了激活）。</summary>
    Outside,

    /// <summary>按了 Esc。</summary>
    Escape,

    /// <summary>代码里改了 IsOpen。</summary>
    Programmatic,
}

/// <summary>浮层开关变了的事件参数。</summary>
/// <param name="isOpen">现在开着。</param>
/// <param name="reason">为什么。</param>
public sealed class OpenChangedEventArgs(bool isOpen, OpenChangeReason reason) : EventArgs
{
    /// <summary>现在开着。</summary>
    public bool IsOpen { get; } = isOpen;

    /// <summary>为什么。</summary>
    public OpenChangeReason Reason { get; } = reason;
}

/// <summary>
/// 弹出框（vue-tsx-pro 的 Popover，非模态）：点触发器在旁边弹出一块内容（筛选条件、简短表单、详情），点外面或 Esc 关闭。
/// <code>
/// var sharing = new PopoverControl { Trigger = new ButtonControl { Icon = Lucide.Share2, Text = "分享" }, Content = sharePanel, Arrow = true };
/// sharing.OpenChanged += (_, e) => …;
/// </code>
/// 触发器照常排在原处（控件的大小就是触发器的大小），点它（Click）开关。浮层是两层窗口：底下的分层窗口画面板（surfaceRaised、xs 描边、lg 圆角、lg 阴影）
/// 与箭头，上面的普通窗口放内容，只盖内边距（space.md）以内。位置 <see cref="Placement"/>（12 种，默认 bottom-start，空间不够时翻到另一边、
/// 贴着屏幕边挪回来），与触发器隔 <see cref="Offset"/>，<see cref="Arrow"/> 时有指向触发器的箭头。打开时内容窗口激活、焦点移到第一个能聚焦的控件，
/// 关上后回到触发器。内容窗口失去激活（点了外面、切到别的窗口）、Esc 关闭；<see cref="OpenChanged"/> 报告原因。
/// 读屏：内容窗口是对话框，以触发器为名称。
/// </summary>
public class PopoverControl : Control, IBaseline, IScopeAware
{
    readonly FloatingForm surface;
    LayeredWindow? backdrop;
    long closedAt = long.MinValue / 2;
    bool open;

    /// <summary>新建：没有触发器与内容，位置 bottom-start，距离 6。</summary>
    public PopoverControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        surface = new FloatingForm(this) { AccessibleRole = AccessibleRole.Dialog };
        surface.Deactivate += (_, _) =>
        {
            if (IsOpen) SetOpen(false, OpenChangeReason.Outside);
        };
        surface.Escape += (_, _) => SetOpen(false, OpenChangeReason.Escape);
    }

    /// <summary>触发器（一般是按钮）：照常排在原处，点它开关。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Trigger
    {
        get;
        set
        {
            if (field == value) return;
            if (field is not null)
            {
                field.Click -= OnTriggerClick;
                Controls.Remove(field);
            }
            field = value;
            if (value is not null)
            {
                value.Click += OnTriggerClick;
                Controls.Add(value);
            }
            PerformLayout();
        }
    }

    /// <summary>浮层里的内容（铺满内容窗口；大小取它想要的大小，至少 MinimumSize）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content
    {
        get;
        set
        {
            if (field == value) return;
            if (field is not null) surface.Controls.Remove(field);
            field = value;
            if (value is null) return;
            value.Dock = DockStyle.Fill;
            surface.Controls.Add(value);
        }
    }

    /// <summary>开着。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => open; set => SetOpen(value, OpenChangeReason.Programmatic); }

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    [Category("Behavior")]
    [Description("弹出位置：边加对齐，共 12 种，默认 bottom-start。")]
    public FloatingPlacement Placement { get; set; } = FloatingPlacement.BottomStart;

    /// <summary>与触发器的距离（设备无关像素），默认 6；有箭头时另加箭头伸出的一段。</summary>
    [Category("Behavior")]
    [Description("与触发器的距离（设备无关像素），默认 6。")]
    [DefaultValue(6f)]
    public float Offset { get; set; } = 6;

    /// <summary>显示指向触发器的箭头。</summary>
    [Category("Appearance")]
    [Description("显示指向触发器的箭头。")]
    [DefaultValue(false)]
    public bool Arrow { get; set; }

    bool ShouldSerializePlacement() => Placement != FloatingPlacement.BottomStart;

    void ResetPlacement() => Placement = FloatingPlacement.BottomStart;

    /// <summary>开关变了（触发器、点外面、Esc、代码里改）。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>放内容的窗口（测试用）。</summary>
    internal Form Surface => surface;

    /// <summary>画面板的窗口（测试用）；没打开过时为 null。</summary>
    internal LayeredWindow? Backdrop => backdrop;

    /// <inheritdoc />
    public int? Baseline => (Trigger as IBaseline)?.Baseline;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => Trigger?.GetPreferredSize(proposedSize) ?? Size.Empty;

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (Trigger is not { } trigger) return;
        // 触发器自己变了大小（文字变了）：跟着变，再铺满
        if (levent.AffectedControl == trigger && AutoSize) Size = GetPreferredSize(Size.Empty);
        trigger.Bounds = ClientRectangle;
    }

    void IScopeAware.ScopeChanged()
    {
        ProviderControl.NotifyDescendants(surface);
        if (IsOpen) Present();
    }

    /// <summary>
    /// 点了触发器：开关。浮层开着时点触发器，内容窗口先因为失去激活（点了外面）关了，紧接着这次点击又会把它打开：刚这样关掉的那一下不再打开。
    /// </summary>
    void OnTriggerClick(object? sender, EventArgs e)
    {
        if (!IsOpen && Environment.TickCount64 - closedAt < 300) return;
        SetOpen(!IsOpen, OpenChangeReason.Trigger);
    }

    void SetOpen(bool next, OpenChangeReason reason)
    {
        if (next == open) return;
        open = next;
        if (next) Present();
        else Dismiss();
        OpenChanged?.Invoke(this, new OpenChangedEventArgs(next, reason));
    }

    /// <summary>
    /// 画好面板、摆好两层窗口并显示：面板 = 内容 + 内边距 + 描边，位置同提示（<see cref="FloatingLayout.Position"/>）；
    /// 画面窗口四周留出阴影与箭头伸出的一段。焦点移到内容里第一个能聚焦的控件（没有时在内容窗口上）。
    /// </summary>
    void Present()
    {
        if (!IsHandleCreated || FindForm() is not { } owner) return;
        var context = ElementContext.Of(this);
        var theme = context.Theme;
        var em = context.Em;
        var border = Math.Max(1, (int)MathF.Round(context.Scale(Tokens.BorderWidth.Xs)));
        var inset = (int)MathF.Round(Tokens.Space.Md * em) + border;
        var content = Content is { } control ? Measure(control) : Size.Empty;
        var panelSize = new Size(content.Width + 2 * inset, content.Height + 2 * inset);
        var arrow = Arrow ? 0.625f * em : 0;
        var gap = context.Scale(Offset) + 0.7f * arrow;
        var target = RectangleToScreen(ClientRectangle);
        var (side, panel) = FloatingLayout.Position(Placement, context.RightToLeft, target, panelSize, gap, Screen.FromControl(this).WorkingArea);

        var shadow = theme.Shadows.Lg;
        var overhang = (int)MathF.Ceiling(Math.Max(0.71f * arrow,
            shadow.Where(layer => !layer.Inset).Select(layer => context.Scale(Math.Max(Math.Abs(layer.X), Math.Abs(layer.Y)) + layer.Spread + layer.Blur)).DefaultIfEmpty(0).Max()));
        var bitmap = LayeredWindow.Canvas(new Size(panelSize.Width + 2 * overhang, panelSize.Height + 2 * overhang));
        var inner = new RectangleF(overhang, overhang, panelSize.Width, panelSize.Height);
        var radius = theme.Radii.Lg * em;
        Drawing.Shadow(bitmap, inner, radius, shadow, context.Scale(1));
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(theme.Colors.SurfaceRaised);
            using var pen = new Pen(theme.Colors.Border, border);
            using (var path = Drawing.RoundedRectangle(inner, radius)) graphics.FillPath(fill, path);
            var half = border / 2f;
            using (var path = Drawing.RoundedRectangle(RectangleF.Inflate(inner, -half, -half), Math.Max(0, radius - half))) graphics.DrawPath(pen, path);
            if (Arrow) PaintArrow(graphics, fill, pen, inner, side, target, panel.Location, arrow);
        }

        backdrop ??= new LayeredWindow();
        if (backdrop.Owner != owner) backdrop.Owner = owner;
        backdrop.Present(bitmap, new Point(panel.X - overhang, panel.Y - overhang), panel, Arrow ? side : null, TimeSpan.Zero);
        surface.Font = Font;
        surface.ForeColor = theme.Colors.Text;
        surface.BackColor = theme.Colors.SurfaceRaised;
        surface.RightToLeft = context.RightToLeft ? RightToLeft.Yes : RightToLeft.No;
        surface.AccessibleName = Trigger is { } trigger ? trigger.AccessibleName ?? trigger.Text : null;
        surface.Bounds = Rectangle.Inflate(panel, -inset, -inset);
        if (!surface.Visible) surface.Show(backdrop);
        surface.Activate();
        if (!surface.SelectNextControl(null, true, true, true, false)) surface.Focus();
    }

    static Size Measure(Control content)
    {
        var size = content.GetPreferredSize(Size.Empty);
        return new Size(Math.Max(size.Width, content.MinimumSize.Width), Math.Max(size.Height, content.MinimumSize.Height));
    }

    /// <summary>藏起两层窗口；焦点在里面（Esc、代码里关）时回到触发器。</summary>
    void Dismiss()
    {
        closedAt = Environment.TickCount64;
        var focusWasInside = surface.ContainsFocus || Form.ActiveForm == surface;
        surface.Hide();
        backdrop?.Dismiss();
        if (!focusWasInside) return;
        FindForm()?.Activate();
        Trigger?.Focus();
    }

    /// <summary>
    /// 箭头（TS 的 propsArrow）：旋转 45° 的小方块，跨在朝向触发器的那条边上、对准触发器的中心（离面板两端至少一个箭头宽），
    /// 底色同面板，朝外的两条边描边（盖住面板在那里的描边）。
    /// </summary>
    static void PaintArrow(Graphics graphics, Brush fill, Pen pen, RectangleF inner, string side, Rectangle target, Point panelOnScreen, float size)
    {
        var vertical = side is "top" or "bottom";
        var center = vertical ? target.Left + target.Width / 2f - panelOnScreen.X : target.Top + target.Height / 2f - panelOnScreen.Y;
        var length = vertical ? inner.Width : inner.Height;
        var cross = Math.Clamp(center, 1.5f * size, Math.Max(1.5f * size, length - 1.5f * size));
        var (point, angle) = side switch
        {
            "top" => (new PointF(inner.Left + cross, inner.Bottom), 45f),
            "bottom" => (new PointF(inner.Left + cross, inner.Top), 225f),
            "left" => (new PointF(inner.Right, inner.Top + cross), -45f),
            _ => (new PointF(inner.Left, inner.Top + cross), 135f),
        };
        var state = graphics.Save();
        graphics.TranslateTransform(point.X, point.Y);
        graphics.RotateTransform(angle);
        var half = size / 2;
        graphics.FillRectangle(fill, -half, -half, size, size);
        // 旋转前的右边、下边：转过去后朝外
        var edge = half - pen.Width / 2;
        graphics.DrawLines(pen, [new PointF(edge, -half), new PointF(edge, edge), new PointF(-half, edge)]);
        graphics.Restore(state);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Trigger is { } trigger) trigger.Click -= OnTriggerClick;
            surface.Dispose();
            backdrop?.Dispose();
        }
        base.Dispose(disposing);
    }
}
