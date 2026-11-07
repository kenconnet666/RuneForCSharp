using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 浮层的两层窗口（<see cref="PopoverControl"/>、<see cref="SelectControl"/> 共用）：底下的 <see cref="LayeredWindow"/> 画阴影、面板（surfaceRaised、
/// xs 描边、lg 圆角、lg 阴影，逐像素透明）与可选的箭头；上面的 <see cref="FloatingForm"/> 放内容，盖在面板的 inset 以内
/// （inset 小于圆角时内容窗口按圆角裁掉，直角不露出来）。
/// 激活模式（弹出框、可搜索的下拉）：内容窗口激活，焦点移到第一个能聚焦的控件，失去激活即"点了外面"（宿主窗体的标题栏会变成非激活）。
/// 不激活模式（下拉的选项列表）：焦点留在弹出它的控件上；点了别处由消息过滤器发现，宿主窗体失去激活、移动时也算点了外面。
/// </summary>
sealed class FloatingSurface : IDisposable
{
    readonly OutsideFilter filter;
    Form? owner;

    /// <summary>新建（还没显示）。</summary>
    /// <param name="origin">浮层是从哪个控件弹出来的（作用域、点在它上面不算外面）。</param>
    public FloatingSurface(Control origin)
    {
        Form = new FloatingForm(origin);
        filter = new OutsideFilter(this);
        Form.Deactivate += (_, _) =>
        {
            if (Form.Activates && Form.Visible) Outside?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>放内容的窗口。</summary>
    public FloatingForm Form { get; }

    /// <summary>画面板的窗口；没显示过时为 null。</summary>
    public LayeredWindow? Backdrop { get; private set; }

    /// <summary>显示着。</summary>
    public bool Visible => Form.Visible;

    /// <summary>点了外面（激活模式：内容窗口失去激活；不激活模式：点了别的窗口、宿主窗体失去激活或移动）。</summary>
    public event EventHandler? Outside;

    /// <summary>
    /// 画好面板并显示两层窗口：面板在屏幕上的 <paramref name="panel"/>，内容窗口盖在 <paramref name="inset"/> 以内；
    /// 画面窗口四周留出阴影与箭头伸出的一段。激活模式下焦点移到内容里第一个能聚焦的控件（没有时在内容窗口上）。
    /// </summary>
    /// <param name="panel">面板在屏幕上的矩形（设备像素）。</param>
    /// <param name="inset">内容窗口离面板外沿的距离（描边加内边距）。</param>
    /// <param name="context">弹出它的控件的环境（主题、字号、DPI、书写方向）。</param>
    /// <param name="arrow">箭头：所在的边（top 是面板在触发器上方）、触发器在屏幕上的矩形、边长；null 时没有。</param>
    /// <param name="elevation">面板的圆角与阴影档位；默认 lg（弹出框、下拉），对话框是 xl。</param>
    public void Show(Rectangle panel, int inset, ElementContext context, (string Side, Rectangle Target, float Size)? arrow, Elevation elevation = Elevation.Lg)
    {
        var origin = Form.Origin;
        if (origin.FindForm() is not { } host) return;
        var theme = context.Theme;
        var em = context.Em;
        var border = Border(context);
        var radius = (elevation == Elevation.Xl ? theme.Radii.Xl : theme.Radii.Lg) * em;
        var shadow = elevation == Elevation.Xl ? theme.Shadows.Xl : theme.Shadows.Lg;
        var overhang = (int)MathF.Ceiling(Math.Max(arrow is { } a ? 0.71f * a.Size : 0,
            shadow.Where(layer => !layer.Inset).Select(layer => context.Scale(Math.Max(Math.Abs(layer.X), Math.Abs(layer.Y)) + layer.Spread + layer.Blur)).DefaultIfEmpty(0).Max()));
        var bitmap = LayeredWindow.Canvas(new Size(panel.Width + 2 * overhang, panel.Height + 2 * overhang));
        var inner = new RectangleF(overhang, overhang, panel.Width, panel.Height);
        Drawing.Shadow(bitmap, inner, radius, shadow, context.Scale(1));
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(theme.Colors.SurfaceRaised);
            using var pen = new Pen(theme.Colors.Border, border);
            using (var path = Drawing.RoundedRectangle(inner, radius)) graphics.FillPath(fill, path);
            var half = border / 2f;
            using (var path = Drawing.RoundedRectangle(RectangleF.Inflate(inner, -half, -half), Math.Max(0, radius - half))) graphics.DrawPath(pen, path);
            if (arrow is { } spec) PaintArrow(graphics, fill, pen, inner, spec.Side, spec.Target, panel.Location, spec.Size);
        }

        Backdrop ??= new LayeredWindow();
        if (Backdrop.Owner != host) Backdrop.Owner = host;
        Backdrop.Present(bitmap, new Point(panel.X - overhang, panel.Y - overhang), panel, arrow?.Side, TimeSpan.Zero);
        Form.Font = origin.Font;
        Form.ForeColor = theme.Colors.Text;
        Form.BackColor = theme.Colors.SurfaceRaised;
        Form.RightToLeft = context.RightToLeft ? RightToLeft.Yes : RightToLeft.No;
        Form.Bounds = Rectangle.Inflate(panel, -inset, -inset);
        Clip(radius - inset);
        if (!Form.Visible) Form.Show(Backdrop);
        Watch(host);
        if (!Form.Activates) return;
        Form.Activate();
        if (!Form.SelectNextControl(null, true, true, true, false)) Form.Focus();
    }

    /// <summary>藏起两层窗口；返回焦点原来是否在里面（关上后要不要还给触发控件）。</summary>
    public bool Hide()
    {
        var focusWasInside = Form.ContainsFocus || System.Windows.Forms.Form.ActiveForm == Form;
        Watch(null);
        Form.Hide();
        Backdrop?.Dismiss();
        return focusWasInside;
    }

    /// <summary>描边宽度：borderWidths.xs，至少 1 像素。</summary>
    public static int Border(ElementContext context) => Math.Max(1, (int)MathF.Round(context.Scale(Tokens.BorderWidth.Xs)));

    /// <summary>内容窗口按面板里面的圆角裁掉（圆角不大于 0 时不裁）。</summary>
    void Clip(float radius)
    {
        var old = Form.Region;
        if (radius <= 0)
        {
            Form.Region = null;
        }
        else
        {
            using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Form.Width, Form.Height), radius);
            Form.Region = new Region(path);
        }
        old?.Dispose();
    }

    /// <summary>显示着时盯住点击与宿主窗体（不激活模式要靠它们发现"点了外面"）；null 时停止。</summary>
    void Watch(Form? host)
    {
        if (owner == host) return;
        if (owner is not null)
        {
            owner.Deactivate -= OnOwnerDeactivate;
            owner.LocationChanged -= OnOwnerMoved;
            Application.RemoveMessageFilter(filter);
        }
        owner = host;
        if (owner is null) return;
        owner.Deactivate += OnOwnerDeactivate;
        owner.LocationChanged += OnOwnerMoved;
        Application.AddMessageFilter(filter);
    }

    /// <summary>宿主窗体失去激活：不激活模式下是切到了别的窗口（激活模式下是内容窗口拿到了激活，不算）。</summary>
    void OnOwnerDeactivate(object? sender, EventArgs e)
    {
        if (!Form.Activates) RaiseOutside();
    }

    /// <summary>宿主窗体移动了：浮层不跟着走，关掉。</summary>
    void OnOwnerMoved(object? sender, EventArgs e) => RaiseOutside();

    void RaiseOutside()
    {
        if (Form.Visible) Outside?.Invoke(this, EventArgs.Empty);
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
    public void Dispose()
    {
        Watch(null);
        Form.Dispose();
        Backdrop?.Dispose();
    }

    /// <summary>面板的圆角与阴影档位。</summary>
    public enum Elevation
    {
        /// <summary>lg：弹出框、下拉。</summary>
        Lg,

        /// <summary>xl：对话框。</summary>
        Xl,
    }

    /// <summary>按下鼠标的位置不在浮层、也不在弹出它的控件上：点了外面。</summary>
    sealed class OutsideFilter(FloatingSurface owner) : IMessageFilter
    {
        const int WmLButtonDown = 0x201, WmRButtonDown = 0x204, WmMButtonDown = 0x207, WmNcLButtonDown = 0xA1, WmNcRButtonDown = 0xA4;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg is not (WmLButtonDown or WmRButtonDown or WmMButtonDown or WmNcLButtonDown or WmNcRButtonDown)) return false;
            var target = Control.FromChildHandle(m.HWnd);
            var origin = owner.Form.Origin;
            var inside = target is not null && (target == owner.Form || owner.Form.Contains(target) || target == owner.Backdrop || target == origin || origin.Contains(target));
            if (!inside) owner.RaiseOutside();
            return false;
        }
    }
}
