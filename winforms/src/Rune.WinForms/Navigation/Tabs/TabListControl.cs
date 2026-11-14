using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Automation;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 一排标签：line 时整行底边（竖排时结束边）一道 borderMuted 的线、当前标签那一段 2 像素的品牌色指示条（切换时滑过去）；soft 时 fill 底、xs 内边距、md 圆角。
/// 方向键在标签之间移动，到两端后从另一端继续（<see cref="Loop"/>）。名称写 AccessibleName。
/// 横排时是一个标签栏：放不下时标签横向滚动，两头出现滚动按钮（只给指针用）；当前标签滚到外面时滚进来。栏的结束端依次是
/// "全部标签页"菜单（<see cref="ShowAllMenu"/>）与新建按钮（订阅了 <see cref="AddRequested"/> 才有）。订阅了 <see cref="Reordered"/> 才能排序：
/// 拖动标签越过别的标签的中线就换位，标签上按 Alt + ←→ 往前、往后挪一位并告诉读屏新位置；TabListControl 自己挪动标签，事件里是新的顺序。
/// 竖排时只有标签（没有标签栏）。WinForms 的子控件各自画在自己的窗口上，滚动列表的两头没有渐隐。
/// </summary>
[ToolboxItem(true)]
[Description("一排标签：方向键在标签之间移动。")]
public class TabListControl : FlexControl
{
    /// <summary>拖动越过这么远（像素）才算拖动，不然是点击。</summary>
    const int DragThreshold = 4;

    readonly Signal<double> indicatorStart = new(0);
    readonly Signal<double> indicatorLength = new(0);
    readonly Effect paint;
    readonly BarLayout barLayout;
    readonly ScrollButtonControl previous;
    readonly ScrollButtonControl next;
    MenuControl? allMenu;
    MenuRadioGroupControl? allGroup;
    IconButtonControl? add;
    EventHandler? addRequested;
    TabsControl? owner;
    TabItemControl? current;
    List<TabItemControl> shown = [];
    string? shownValue;
    bool placed;
    bool reveal;
    int offset;
    Rectangle viewport;
    TabItemControl? pressed;
    Point pressedAt;
    bool dragging;

    /// <summary>新建：横排、拉伸对齐。</summary>
    public TabListControl()
    {
        // 各部分建好之前不排
        SuspendLayout();
        barLayout = new BarLayout(this);
        Align = FlexAlign.Stretch;
        AutoSize = true;
        AccessibleRole = AccessibleRole.PageTabList;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        paint = new Effect(Invalidate);
        previous = ScrollButton(Lucide.ChevronLeft, -1);
        next = ScrollButton(Lucide.ChevronRight, 1);
        ResumeLayout(false);
    }

    /// <summary>到两端后是否从另一端继续，默认 true。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Loop { get; set; } = true;

    /// <summary>标签栏结束端"全部标签页"的菜单（标签多、放不下时一眼找到并切过去）。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool ShowAllMenu
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            SyncBar();
        }
    }

    /// <summary>点了新建按钮（TS 的 onAdd）：使用方加一个标签。订阅了才有新建按钮。</summary>
    [Category("Action")]
    public event EventHandler? AddRequested
    {
        add
        {
            addRequested += value;
            SyncBar();
        }
        remove
        {
            addRequested -= value;
            SyncBar();
        }
    }

    /// <summary>拖动或 Alt + ←→ 改了顺序（TS 的 onReorder）：标签已经挪好，参数是新的顺序。订阅了才能排序。</summary>
    [Category("Action")]
    public event EventHandler<TabsReorderedEventArgs>? Reordered;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => IsVertical ? base.LayoutEngine : barLayout;

    bool IsVertical => owner?.Orientation == TabsOrientation.Vertical;

    bool IsSoft => owner?.Variant == TabsVariant.Soft;

    float Em => this.EmPixels();

    /// <summary>读屏念的那句话（测试用）。</summary>
    internal string? Announcement { get; private set; }

    /// <summary>"全部标签页"菜单（测试用）。</summary>
    internal MenuControl? AllMenu => allMenu;

    /// <summary>新建按钮（测试用）。</summary>
    internal IconButtonControl? AddButton => add;

    /// <summary>两头的滚动按钮（测试用）。</summary>
    internal (IconButtonControl Previous, IconButtonControl Next) ScrollButtons => (previous, next);

    /// <summary>标签滚动了多远（测试用）。</summary>
    internal int ScrollOffset => offset;

    /// <summary>标签：可见的，按显示的顺序。</summary>
    List<TabItemControl> Items() => [.. Controls.OfType<TabItemControl>().Where(tab => tab.Visible || !Visible)];

    /// <summary>跟着 TabsControl 的样式与方向：间距、soft 的内边距与宽度；选中值变了把当前标签滚进来。</summary>
    internal void Apply(TabsControl tabs)
    {
        owner = tabs;
        var vertical = IsVertical;
        Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
        Gap = !IsSoft && !vertical ? Space.Md : Space.Xs;
        var inset = IsSoft ? (int)MathF.Round(0.25f * Em) : 0;
        Padding = new Padding(inset);
        if (shownValue != tabs.SelectedValue)
        {
            shownValue = tabs.SelectedValue;
            reveal = true;
        }
        if (allGroup is not null) allGroup.Value = tabs.SelectedValue;
        SyncBar();
        SyncTabStops();
        PerformLayout();
        Invalidate();
    }

    /// <summary>整组一个 Tab 停靠点：当前聚焦过的，没有时选中的，再没有时第一个可用的。</summary>
    void SyncTabStops()
    {
        var items = Items();
        var stop = current is not null && items.Contains(current) ? current
            : items.FirstOrDefault(tab => tab.IsSelected && tab.Enabled) ?? items.FirstOrDefault(tab => tab.Enabled);
        foreach (var item in items) item.TabStop = item == stop;
    }

    /// <summary>标签栏里的按钮：按需建菜单与新建按钮，竖排时都藏起来。它们不参加竖排的排列，横排时由 <see cref="LayoutBar"/> 摆。</summary>
    void SyncBar()
    {
        var lang = ProviderControl.LangOf(this);
        previous.AccessibleName = lang.TabScrollPrevious;
        next.AccessibleName = lang.TabScrollNext;
        if (ShowAllMenu && allMenu is null)
        {
            allGroup = new MenuRadioGroupControl();
            allGroup.ValueChanged += (_, _) =>
            {
                if (allGroup?.Value is { } value && owner is not null && value != owner.SelectedValue) owner.Value = value;
            };
            allMenu = new MenuControl { Placement = FloatingPlacement.BottomEnd, Trigger = new IconButtonControl { Icon = Lucide.ChevronDown, TextSize = TextSize.Sm } };
            allMenu.Items.Add(allGroup);
            AddExtra(allMenu);
            SyncMenuItems();
        }
        else if (!ShowAllMenu && allMenu is not null)
        {
            RemoveExtra(allMenu);
            (allMenu, allGroup) = (null, null);
        }
        if (addRequested is not null && add is null)
        {
            add = new IconButtonControl { Icon = Lucide.Plus, TextSize = TextSize.Sm };
            add.Click += (_, _) => addRequested?.Invoke(this, EventArgs.Empty);
            AddExtra(add);
        }
        else if (addRequested is null && add is not null)
        {
            RemoveExtra(add);
            add = null;
        }
        if (add is not null) add.AccessibleName = lang.TabAdd;
        if (allMenu?.Trigger is { } trigger) trigger.AccessibleName = lang.TabList;
        if (allGroup is not null) allGroup.Label = lang.TabList;
        if (allMenu is not null) allMenu.Visible = !IsVertical;
        if (add is not null) add.Visible = !IsVertical;
        PerformLayout();
    }

    void AddExtra(Control extra)
    {
        Controls.Add(extra);
        SetOverlay(extra, true);
    }

    void RemoveExtra(Control extra)
    {
        Controls.Remove(extra);
        extra.Dispose();
    }

    /// <summary>"全部标签页"菜单的项：按显示的顺序列出标签的文字（标签、文字变了时重建）。</summary>
    internal void SyncMenuItems()
    {
        if (allGroup is null) return;
        allGroup.Controls.OfType<MenuRadioItemControl>().DisposeAll();
        foreach (var tab in Controls.OfType<TabItemControl>())
            allGroup.Controls.Add(new MenuRadioItemControl { Value = tab.Value, Text = tab.Label, Enabled = tab.Enabled });
        allGroup.Value = owner?.SelectedValue;
    }

    ScrollButtonControl ScrollButton(IconData icon, int direction)
    {
        var button = new ScrollButtonControl { Icon = icon, TextSize = TextSize.Sm, Visible = false };
        button.Click += (_, _) => ScrollBy(direction);
        Controls.Add(button);
        SetOverlay(button, true);
        return button;
    }

    /// <summary>滚动按钮：往前（-1）或往后（1）滚大半屏。</summary>
    void ScrollBy(int direction)
    {
        offset += (int)MathF.Round(direction * viewport.Width * 0.8f);
        PerformLayout();
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override ControlCollection CreateControlsInstance() => new TabControls(this);

    /// <summary>子控件集合：拿掉标签之前记下它有没有焦点（拿的过程中焦点就没了，OnControlRemoved 里已经看不出来）。</summary>
    sealed class TabControls(TabListControl owner) : ControlCollection(owner)
    {
        public override void Remove(Control? value)
        {
            if (value is TabItemControl { ContainsFocus: true } tab) owner.removedWithFocus = tab;
            base.Remove(value);
        }
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is TabItemControl tab)
        {
            tab.GotFocus += OnItemFocused;
            tab.MouseDown += OnTabPressed;
            tab.MouseMove += OnTabMoved;
            tab.MouseUp += (_, _) => EndDrag();
            if (!moving)
            {
                shown = Items();
                SyncMenuItems();
            }
        }
        TabsControl.Owner(this)?.Sync();
    }

    /// <summary>
    /// 标签被拿掉了（挪动排序不算）：关掉的是当前标签时选它后面（没有时前面）的一个，关掉的标签有焦点时焦点交给当前标签；"全部标签页"菜单跟着重建。
    /// 整组卸载、销毁时不另选。
    /// </summary>
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        if (e.Control is not TabItemControl tab || moving || Disposing || IsDisposed) return;
        var (index, selected, focused) = (shown.IndexOf(tab), tab.IsSelected, tab == removedWithFocus);
        removedWithFocus = null;
        shown = Items();
        if (tab == current) current = null;
        SyncMenuItems();
        if (index < 0 || owner is not { } tabs) return;
        var rest = shown;
        if (selected && rest.Count > 0 && rest[Math.Min(index, rest.Count - 1)].Value is { } value) tabs.Value = value;
        if (focused && rest.FirstOrDefault(item => item.Value == tabs.SelectedValue) is { } next)
        {
            current = next;
            SyncTabStops();
            next.Focus();
        }
    }

    bool moving;
    Control? removedWithFocus;

    void OnItemFocused(object? sender, EventArgs e)
    {
        current = sender as TabItemControl;
        SyncTabStops();
    }

    /// <summary>把标签挪到第 to 个（按显示的顺序），报告新顺序；键盘挪的告诉读屏新位置。</summary>
    void MoveTab(TabItemControl tab, int to, bool announce)
    {
        var order = Items();
        var from = order.IndexOf(tab);
        if (Reordered is null || from < 0 || to < 0 || to >= order.Count || to == from) return;
        moving = true;
        try
        {
            Controls.SetChildIndex(tab, Controls.GetChildIndex(order[to]));
        }
        finally
        {
            moving = false;
        }
        shown = Items();
        reveal = true;
        PerformLayout();
        Invalidate(true);
        Reordered.Invoke(this, new TabsReorderedEventArgs([.. shown.Select(item => item.Value ?? "")]));
        SyncMenuItems();
        if (!announce) return;
        Announcement = ProviderControl.LangOf(this).TabMoved(tab.Label, to + 1, order.Count);
        AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, Announcement);
    }

    /// <summary>拖动排序：按下标签时记下。</summary>
    void OnTabPressed(object? sender, MouseEventArgs e)
    {
        if (Reordered is null || e.Button != MouseButtons.Left || sender is not TabItemControl tab) return;
        pressed = tab;
        pressedAt = new Point(tab.Left + e.X, tab.Top + e.Y);
    }

    /// <summary>拖过门槛后跟着指针：越过别的标签的中线就换位（同 Sortable）。标签按下时接管了指针，挪动不换窗口，指针不丢。</summary>
    void OnTabMoved(object? sender, MouseEventArgs e)
    {
        if (pressed is null || sender is not TabItemControl tab || (e.Button & MouseButtons.Left) == 0) return;
        var at = new Point(tab.Left + e.X, tab.Top + e.Y);
        if (!dragging)
        {
            if (Math.Abs(at.X - pressedAt.X) < DragThreshold && Math.Abs(at.Y - pressedAt.Y) < DragThreshold) return;
            dragging = true;
            pressed.IsDragging = true;
        }
        var order = Items();
        var from = order.IndexOf(pressed);
        var over = order.FirstOrDefault(item => at.X >= item.Left && at.X <= item.Right);
        if (over is null || over == pressed) return;
        var to = order.IndexOf(over);
        // 往后挪时要过了它的中线，往前挪时要回到中线之前
        if (to > from == at.X > over.Left + over.Width / 2) MoveTab(pressed, to, announce: false);
    }

    void EndDrag()
    {
        if (pressed is not null) pressed.IsDragging = false;
        pressed = null;
        dragging = false;
    }

    /// <summary>方向键、Home、End 在标签之间移动焦点（不可用的跳过）；自动切换时切到新聚焦的标签。Alt + ←→ 挪动标签（排序）。</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        var items = Items().Where(tab => tab.Enabled).ToList();
        var index = items.FindIndex(item => item.Focused);
        if (index < 0) return base.ProcessDialogKey(keyData);
        var rtl = RightToLeft == RightToLeft.Yes;
        if (Reordered is not null && keyData is (Keys.Alt | Keys.Left) or (Keys.Alt | Keys.Right))
        {
            var forward = (keyData == (Keys.Alt | Keys.Right)) != rtl;
            MoveTab(items[index], Items().IndexOf(items[index]) + (forward ? 1 : -1), announce: true);
            return true;
        }
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
        if (target is not { } nextIndex) return base.ProcessDialogKey(keyData);
        if (nextIndex == index) return true;
        if (nextIndex < 0 || nextIndex >= items.Count)
        {
            if (!Loop) return true;
            nextIndex = (nextIndex + items.Count) % items.Count;
        }
        current = items[nextIndex];
        SyncTabStops();
        current.Focus();
        if (owner?.Activation == TabsActivation.Automatic && current.Value is { } value) owner.Value = value;
        return true;
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => IsVertical ? base.GetPreferredSize(proposedSize) : MeasureBar(proposedSize);

    int Inset => IsSoft ? (int)MathF.Round(0.25f * Em) : 0;

    int TabGap => (int)MathF.Round((IsSoft ? 0.25f : 0.75f) * Em);

    int BarGap => (int)MathF.Round(0.25f * Em);

    static Size SizeOf(Control control)
    {
        var size = control.GetPreferredSize(Size.Empty);
        return new Size(Math.Max(size.Width, control.MinimumSize.Width), Math.Max(size.Height, control.MinimumSize.Height));
    }

    /// <summary>栏结束端的按钮：菜单、新建（显示着的）。</summary>
    List<Control> Trailing() => [.. new Control?[] { allMenu, add }.OfType<Control>().Where(control => control.Visible || !Visible)];

    /// <summary>横排想要的大小：标签都放得下时的宽度（标签、内边距、结束端的按钮），不超过给的宽度（放不下时列表收缩、滚动）；高度是最高的一个。</summary>
    Size MeasureBar(Size proposed)
    {
        var tabs = Items().Select(SizeOf).ToList();
        var trailing = Trailing().Select(SizeOf).ToList();
        var inset = Inset;
        var width = tabs.Sum(size => size.Width) + TabGap * Math.Max(0, tabs.Count - 1) + 2 * inset + trailing.Sum(size => size.Width + BarGap);
        var height = Math.Max(tabs.Select(size => size.Height).DefaultIfEmpty().Max() + 2 * inset, trailing.Select(size => size.Height).DefaultIfEmpty().Max());
        return new Size(proposed.Width is > 1 and < int.MaxValue ? Math.Min(width, proposed.Width) : width, height);
    }

    /// <summary>
    /// 横排的标签栏：[往前滚][列表][往后滚][菜单][新建]，间距 space.xs、竖直居中。放不下时列表收缩、出现滚动按钮，
    /// 标签按滚动的距离错开，露在列表外面的部分用窗口区域裁掉。
    /// </summary>
    void LayoutBar()
    {
        var tabs = Items();
        var sizes = tabs.Select(SizeOf).ToList();
        var trailing = Trailing();
        var inset = Inset;
        var (tabGap, barGap) = (TabGap, BarGap);
        var content = sizes.Sum(size => size.Width) + tabGap * Math.Max(0, tabs.Count - 1);
        var trailingWidth = trailing.Sum(control => SizeOf(control).Width + barGap);
        var overflowing = content > Width - trailingWidth - 2 * inset;
        if (previous.Visible != overflowing) previous.Visible = next.Visible = overflowing;
        var (pw, nw) = overflowing ? (SizeOf(previous).Width, SizeOf(next).Width) : (0, 0);
        var boxX = overflowing ? pw + barGap : 0;
        var width = overflowing ? Math.Max(0, Width - trailingWidth - boxX - nw - barGap - 2 * inset) : content;
        viewport = new Rectangle(boxX + inset, 0, width, Height);
        var starts = new int[tabs.Count];
        for (var i = 1; i < tabs.Count; i++) starts[i] = starts[i - 1] + sizes[i - 1].Width + tabGap;
        // 切到一个滚到外面的标签：把它滚进来，旁边留 16 像素
        if (reveal && tabs.FindIndex(tab => tab.IsSelected) is var selected and >= 0)
        {
            reveal = false;
            var margin = LogicalToDeviceUnits(16);
            if (starts[selected] < offset) offset = starts[selected] - margin;
            else if (starts[selected] + sizes[selected].Width > offset + width) offset = starts[selected] + sizes[selected].Width - width + margin;
        }
        offset = Math.Clamp(offset, 0, Math.Max(0, content - width));
        var height = Height - 2 * inset;
        for (var i = 0; i < tabs.Count; i++)
        {
            var bounds = new Rectangle(viewport.X - offset + starts[i], inset, sizes[i].Width, height);
            tabs[i].SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            Clip(tabs[i], bounds);
        }
        var y = (Control control) => (Height - SizeOf(control).Height) / 2;
        if (overflowing)
        {
            previous.SetBounds(0, y(previous), pw, SizeOf(previous).Height);
            next.SetBounds(viewport.Right + inset + barGap, y(next), nw, SizeOf(next).Height);
            previous.Enabled = offset > 0;
            next.Enabled = offset < content - width;
        }
        var x = viewport.Right + inset + (overflowing ? barGap + nw : 0);
        foreach (var control in trailing)
        {
            var size = SizeOf(control);
            control.SetBounds(x + barGap, y(control), size.Width, size.Height);
            x += barGap + size.Width;
        }
        PlaceIndicator();
    }

    /// <summary>标签露在列表外面的部分裁掉（窗口区域）；整个在里面时不裁。</summary>
    void Clip(TabItemControl tab, Rectangle bounds)
    {
        var visible = Rectangle.Intersect(bounds, viewport);
        if (visible == bounds)
        {
            if (tab.Region is null) return;
            var old = tab.Region;
            tab.Region = null;
            old.Dispose();
            return;
        }
        var region = tab.Region;
        tab.Region = new Region(visible with { X = visible.X - bounds.X, Y = visible.Y - bounds.Y });
        region?.Dispose();
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (!IsVertical) return;
        // 竖排没有标签栏：滚动按钮藏起来，横排时的裁剪去掉
        viewport = ClientRectangle;
        offset = 0;
        previous.Visible = next.Visible = false;
        foreach (var tab in Items()) Clip(tab, tab.Bounds);
        PlaceIndicator();
    }

    /// <summary>字号变了（换了 DPI）：标签的宽度都变了，当前标签重新滚进来。</summary>
    protected override void OnFontChanged(EventArgs e)
    {
        reveal = true;
        base.OnFontChanged(e);
    }

    /// <summary>指示条对准当前标签（列表内容里的位置）：第一次直接到位，之后滑过去（Animator，durations.md、standard）。</summary>
    internal void PlaceIndicator()
    {
        if (owner is null || IsSoft || Items().FirstOrDefault(tab => tab.IsSelected) is not { } selected) return;
        var (start, length) = IsVertical ? (selected.Top, selected.Height) : (selected.Left - viewport.X + offset, selected.Width);
        var duration = placed ? Tokens.Duration.Md : TimeSpan.Zero;
        placed = true;
        Animator.Animate(indicatorStart, start, duration, Easing.Standard);
        Animator.Animate(indicatorLength, length, duration, Easing.Standard);
    }

    /// <summary>soft：fill 底、md 圆角（只在列表后面）。line：分隔线（横排时整个标签栏）与 2 像素的品牌色指示条（裁在列表里）。</summary>
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
            var items = Items();
            if (items.Count == 0) return;
            var inset = Inset;
            var box = IsVertical
                ? new RectangleF(0, 0, Width - 0.5f, items.Max(tab => tab.Bottom) + inset - 0.5f)
                : new RectangleF(viewport.X - inset, 0, viewport.Width + 2 * inset - 0.5f, Height - 0.5f);
            var radius = ProviderControl.ThemeOf(this).Radii.Md * Em;
            using var path = Drawing.RoundedRectangle(box, radius);
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
        graphics.SetClip(viewport);
        graphics.FillRectangle(accent, IsVertical ? new RectangleF(Width - bar, start, bar, length) : new RectangleF(viewport.X - offset + start, Height - bar, length, bar));
        graphics.ResetClip();
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

    /// <summary>横排时的排列：交给 <see cref="LayoutBar"/>。</summary>
    sealed class BarLayout(TabListControl owner) : LayoutEngine
    {
        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            owner.LayoutBar();
            return owner.AutoSize;
        }
    }

    /// <summary>滚动按钮：只给指针用（键盘在标签之间移动时，当前标签会自己滚进来），不能聚焦。</summary>
    internal sealed class ScrollButtonControl : IconButtonControl
    {
        public ScrollButtonControl()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }
    }
}

/// <summary>标签改了顺序的参数。</summary>
/// <param name="values">新的顺序：各标签的值。</param>
public sealed class TabsReorderedEventArgs(IReadOnlyList<string> values) : EventArgs
{
    /// <summary>新的顺序：各标签的值。</summary>
    public IReadOnlyList<string> Values { get; } = values;
}
