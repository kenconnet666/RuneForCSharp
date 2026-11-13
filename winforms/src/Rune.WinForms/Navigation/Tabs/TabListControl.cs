using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;


/// <summary>
/// 一排标签：line 时底边（竖排时结束边）一道 borderMuted 的线、当前标签那一段 2 像素的品牌色指示条（切换时滑过去）；soft 时 fill 底、xs 内边距、md 圆角。
/// 方向键在标签之间移动，到两端后从另一端继续（<see cref="Loop"/>）。名称写 AccessibleName。
/// </summary>
[ToolboxItem(true)]
[Description("一排标签：方向键在标签之间移动。")]
public class TabListControl : FlexControl
{
    readonly Signal<double> indicatorStart = new(0);
    readonly Signal<double> indicatorLength = new(0);
    readonly Effect paint;
    TabsControl? owner;
    TabItemControl? current;
    bool placed;

    /// <summary>新建：横排、拉伸对齐。</summary>
    public TabListControl()
    {
        Align = FlexAlign.Stretch;
        AutoSize = true;
        AccessibleRole = AccessibleRole.PageTabList;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        paint = new Effect(Invalidate);
    }

    /// <summary>到两端后是否从另一端继续，默认 true。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Loop { get; set; } = true;

    bool IsVertical => owner?.Orientation == TabsOrientation.Vertical;

    bool IsSoft => owner?.Variant == TabsVariant.Soft;

    /// <summary>跟着 TabsControl 的样式与方向：间距、soft 的内边距与宽度。</summary>
    internal void Apply(TabsControl tabs)
    {
        owner = tabs;
        var vertical = IsVertical;
        Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
        Gap = !IsSoft && !vertical ? Space.Md : Space.Xs;
        var inset = IsSoft ? (int)MathF.Round(0.25f * Font.SizeInPoints * DeviceDpi / 72f) : 0;
        Padding = new Padding(inset);
        SyncTabStops();
        Invalidate();
    }

    /// <summary>标签：可见的。</summary>
    List<TabItemControl> Items() => [.. Controls.OfType<TabItemControl>().Where(tab => tab.Visible)];

    /// <summary>整组一个 Tab 停靠点：当前聚焦过的，没有时选中的，再没有时第一个可用的。</summary>
    void SyncTabStops()
    {
        var items = Items();
        var stop = current is not null && items.Contains(current) ? current
            : items.FirstOrDefault(tab => tab.IsSelected && tab.Enabled) ?? items.FirstOrDefault(tab => tab.Enabled);
        foreach (var item in items) item.TabStop = item == stop;
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        e.Control!.GotFocus += OnItemFocused;
        TabsControl.Owner(this)?.Sync();
    }

    void OnItemFocused(object? sender, EventArgs e)
    {
        current = sender as TabItemControl;
        SyncTabStops();
    }

    /// <summary>方向键、Home、End 在标签之间移动焦点（不可用的跳过）；自动切换时切到新聚焦的标签。</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        var items = Items().Where(tab => tab.Enabled).ToList();
        var index = items.FindIndex(item => item.Focused);
        if (index < 0) return base.ProcessDialogKey(keyData);
        var rtl = RightToLeft == RightToLeft.Yes;
        int? target = keyData switch
        {
            Keys.Right when !IsVertical => rtl ? index - 1 : index + 1,
            Keys.Left when !IsVertical => rtl ? index + 1 : index - 1,
            Keys.Down when IsVertical => index + 1,
            Keys.Up when IsVertical => index - 1,
            Keys.Home => 0,
            Keys.End => items.Count - 1,
            Keys.Left or Keys.Right or Keys.Up or Keys.Down => index,
            _ => null,
        };
        if (target is not { } next) return base.ProcessDialogKey(keyData);
        if (next == index) return true;
        if (next < 0 || next >= items.Count)
        {
            if (!Loop) return true;
            next = (next + items.Count) % items.Count;
        }
        current = items[next];
        SyncTabStops();
        current.Focus();
        if (owner?.Activation == TabsActivation.Automatic && current.Value is { } value) owner.Value = value;
        return true;
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        PlaceIndicator();
    }

    /// <summary>指示条对准当前标签：第一次直接到位，之后滑过去（Animator，durations.md、standard）。</summary>
    internal void PlaceIndicator()
    {
        if (owner is null || IsSoft || Items().FirstOrDefault(tab => tab.IsSelected) is not { } selected) return;
        var (start, length) = IsVertical ? (selected.Top, selected.Height) : (selected.Left, selected.Width);
        var duration = placed ? Tokens.Duration.Md : TimeSpan.Zero;
        placed = true;
        Animator.Animate(indicatorStart, start, duration, Easing.Standard);
        Animator.Animate(indicatorLength, length, duration, Easing.Standard);
    }

    /// <summary>soft：fill 底、md 圆角。line：分隔线与 2 像素的品牌色指示条。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        if (owner is null) return;
        var colors = ProviderControl.ThemeOf(this).Colors;
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (IsSoft)
        {
            // 底色只在标签后面（列表本身被拉满一行，上游的列表按内容宽）
            var items = Items();
            if (items.Count == 0) return;
            var right = items.Max(tab => tab.Right) + Padding.Right;
            var bottom = items.Max(tab => tab.Bottom) + Padding.Bottom;
            var radius = ProviderControl.ThemeOf(this).Radii.Md * Font.SizeInPoints * DeviceDpi / 72f;
            using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, IsVertical ? Width - 0.5f : right - 0.5f, bottom - 0.5f), radius);
            using var fill = new SolidBrush(colors.Fill);
            graphics.FillPath(fill, path);
            return;
        }
        var line = LogicalToDeviceUnits(1);
        var bar = LogicalToDeviceUnits(2);
        using (var divider = new SolidBrush(colors.BorderMuted))
            graphics.FillRectangle(divider, IsVertical ? new Rectangle(Width - line, 0, line, Height) : new Rectangle(0, Height - line, Width, line));
        var start = (float)indicatorStart.Value;
        var length = (float)indicatorLength.Value;
        if (length <= 0) return;
        using var accent = new SolidBrush(colors.Accent);
        graphics.FillRectangle(accent, IsVertical ? new RectangleF(Width - bar, start, bar, length) : new RectangleF(start, Height - bar, length, bar));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Animator.Stop(indicatorStart);
            Animator.Stop(indicatorLength);
            paint.Dispose();
        }
        base.Dispose(disposing);
    }
}
