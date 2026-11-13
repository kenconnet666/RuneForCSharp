using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 子菜单（vue-tsx-pro 的 MenuSub，WAI-ARIA 的子菜单）：菜单里的一项（文字写 Text），打开另一列菜单（往书写方向的结束边弹出）。
/// <code>
/// var move = new MenuSubControl { Text = "移动到", Icon = Lucide.FolderInput };
/// move.Items.Add(new MenuItemControl { Text = "项目资料" });
/// </code>
/// →（从右到左排版时 ←）、Enter、空格打开并聚焦第一项；子菜单里 ←（→）或 Esc 关闭并回到这一项，不关外面的菜单。
/// 指针停 100 毫秒打开；离开后 400 毫秒内移进子菜单就不关（上游的安全三角在这里简化成宽限时间）。子菜单里执行一项后，整串菜单都关闭。
/// 子菜单的窗口激活后焦点在里面第一项（WinForms 的浮层窗口激活即拿焦点，指针打开时也一样）。读屏：这一项展开时是展开的菜单项，子菜单以它为名称。
/// </summary>
[ToolboxItem(true)]
[Description("子菜单：菜单里打开另一列菜单的一项。")]
public class MenuSubControl : MenuItemControlBase, IMenuHost
{
    readonly FloatingSurface surface;
    readonly ScrollAreaControl scroll = new() { IsViewportFocusable = false, Dock = DockStyle.Fill };
    readonly MenuList list = new();
    readonly System.Windows.Forms.Timer openTimer = new() { Interval = 100 };
    readonly System.Windows.Forms.Timer graceTimer = new() { Interval = 400 };
    bool open;

    /// <summary>新建：这一项与它的子菜单。</summary>
    public MenuSubControl()
    {
        Chevron.Visible = true;
        CloseOnSelect = false;
        surface = new FloatingSurface(this);
        surface.Form.AccessibleRole = AccessibleRole.MenuPopup;
        scroll.Content = list;
        surface.Form.Controls.Add(scroll);
        surface.Outside += (_, _) => BeginInvoke(OnOutside);
        surface.Form.Escape += (_, _) => CloseToItem(OpenChangeReason.Escape);
        surface.Form.MouseEnter += (_, _) => graceTimer.Stop();
        list.MouseEnter += (_, _) => graceTimer.Stop();
        openTimer.Tick += (_, _) =>
        {
            openTimer.Stop();
            SetOpen(true, OpenChangeReason.Trigger);
        };
        graceTimer.Tick += (_, _) =>
        {
            graceTimer.Stop();
            SetOpen(false, OpenChangeReason.Outside);
        };
    }

    /// <summary>子菜单里的项。</summary>
    [Browsable(false)]
    public ControlCollection Items => list.Controls;

    /// <summary>子菜单开着。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => open; set => SetOpen(value, null); }

    /// <summary>用户开、关了子菜单；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <inheritdoc />
    [Browsable(false)]
    public Form Surface => surface.Form;

    /// <summary>子菜单的那一列。</summary>
    internal MenuList SubList => list;

    /// <summary>所在的那一列菜单（这一项属于外面那一列）。</summary>
    IMenuHost? ParentHost => Host;

    /// <summary>打开子菜单不是执行：不关外面的菜单，打开并聚焦第一项。</summary>
    public override void PerformClick()
    {
        if (Enabled) SetOpen(true, OpenChangeReason.Trigger);
    }

    internal override void Run() => SetOpen(true, OpenChangeReason.Trigger);

    /// <summary>→（从右到左时 ←）打开。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == (RightToLeft == RightToLeft.Yes ? Keys.Left : Keys.Right))
        {
            PerformClick();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private protected override void OnHoverChanged(bool hovering)
    {
        if (hovering)
        {
            graceTimer.Stop();
            if (Enabled) openTimer.Start();
            return;
        }
        openTimer.Stop();
        if (open) graceTimer.Start();
    }

    /// <summary>外面的菜单关了（这一项跟着藏起来）：子菜单跟着关。</summary>
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) SetOpen(false, null);
    }

    void SetOpen(bool next, OpenChangeReason? reason)
    {
        if (next == open || (next && !Enabled)) return;
        open = next;
        openTimer.Stop();
        graceTimer.Stop();
        if (next) Present();
        else surface.Hide();
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(next, known));
    }

    /// <summary>往书写方向的结束边弹出（顶边对齐这一项，隔 2 像素），至少 12em 宽；激活后焦点在第一项。</summary>
    void Present()
    {
        if (!IsHandleCreated) return;
        var context = ElementContext.Of(this);
        var em = context.Em;
        list.Font = Font;
        list.UpdateMetrics(em);
        var inset = FloatingSurface.Border(context);
        var content = list.GetPreferredSize(Size.Empty);
        var area = Screen.FromControl(this).WorkingArea;
        var size = new Size(Math.Max(content.Width, (int)MathF.Ceiling(12 * em)) + 2 * inset, Math.Min(content.Height + 2 * inset, area.Height - 16));
        var placement = context.RightToLeft ? FloatingPlacement.LeftStart : FloatingPlacement.RightStart;
        var (_, panel) = FloatingLayout.Position(placement, false, RectangleToScreen(ClientRectangle), size, context.Scale(2), area);
        surface.Form.AccessibleName = Text;
        surface.Show(panel, inset, context, null);
        list.Items().FirstOrDefault()?.Focus();
    }

    /// <summary>只关这一层（←、Esc），焦点回到这一项。</summary>
    internal void CloseToItem(OpenChangeReason reason)
    {
        SetOpen(false, reason);
        FindForm()?.Activate();
        Focus();
    }

    /// <summary>子菜单的窗口失去激活：激活到了它自己的子菜单时不算；否则关上，激活也没回到外面那一列时外面也关。</summary>
    void OnOutside()
    {
        if (!open || OwnsActiveForm()) return;
        SetOpen(false, OpenChangeReason.Outside);
        ParentHost?.OutsideFromSubmenu();
    }

    bool OwnsActiveForm()
    {
        for (Control? node = Form.ActiveForm; node is not null; node = node is FloatingForm floating ? floating.Origin.FindForm() : null)
            if (node == surface.Form) return true;
        return false;
    }

    /// <inheritdoc />
    public void CloseFromItem()
    {
        SetOpen(false, OpenChangeReason.Select);
        ParentHost?.CloseFromItem();
    }

    /// <inheritdoc />
    public void OutsideFromSubmenu()
    {
        if (!open || OwnsActiveForm()) return;
        SetOpen(false, OpenChangeReason.Outside);
        ParentHost?.OutsideFromSubmenu();
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new SubAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            openTimer.Dispose();
            graceTimer.Dispose();
            surface.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>读屏：有子菜单的菜单项，开着时是展开的。</summary>
    sealed class SubAccessibleObject(MenuSubControl owner) : MenuItemAccessibleObject(owner)
    {
        public override AccessibleStates State => base.State | AccessibleStates.HasPopup | (owner.open ? AccessibleStates.Expanded : AccessibleStates.Collapsed);
    }
}
