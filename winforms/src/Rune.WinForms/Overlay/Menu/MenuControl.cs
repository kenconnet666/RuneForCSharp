using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>菜单项所在的一列菜单（<see cref="MenuControl"/> 或子菜单）：执行了一项后关闭，点了外面时关闭。</summary>
interface IMenuHost
{
    /// <summary>执行了一项：关闭（整串菜单），原因是 select，焦点回到触发器。</summary>
    void CloseFromItem();

    /// <summary>子菜单因为点了外面关了：激活没有回到这一列时，这一列也关（原因 outside）。</summary>
    void OutsideFromSubmenu();

    /// <summary>这一列的浮层窗口。</summary>
    Form Surface { get; }
}

/// <summary>
/// 一列菜单项（菜单、子菜单的内容）：竖着排，上下左右 space.xs 的内边距；↑↓ 在项之间移动（循环、跳过不可用的）、Home / End 到两端，
/// 按字母跳到以它开头的项（规则同 Listbox，<see cref="Typeahead"/>）。WinForms 没有漫游焦点：菜单项把这些键交给它。
/// </summary>
sealed class MenuList : FlexControl
{
    readonly Typeahead search = new();

    /// <summary>新建：竖排、撑满宽度、按内容大小。</summary>
    public MenuList()
    {
        Direction = FlexDirection.Column;
        Align = FlexAlign.Stretch;
        AutoSize = true;
    }

    /// <summary>这一列里能停留的项（含分组里的；不含子菜单里的）：可见、可用。</summary>
    public List<MenuItemControlBase> Items() => [.. Descendants(this).OfType<MenuItemControlBase>().Where(item => item.Visible && item.Enabled)];

    static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(child => Descendants(child).Prepend(child));

    /// <summary>上下内边距 space.xs（按字号）。</summary>
    public void UpdateMetrics(float em)
    {
        var pad = (int)MathF.Round(Tokens.Space.Xs * em);
        Padding = new Padding(pad);
    }

    /// <summary>方向键、Home、End：移到下一项（循环）、两端。</summary>
    /// <returns>处理了。</returns>
    public bool MoveFocus(MenuItemControlBase from, Keys key)
    {
        var items = Items();
        var index = items.IndexOf(from);
        if (index < 0 || items.Count == 0) return false;
        int? target = key switch
        {
            Keys.Down => (index + 1) % items.Count,
            Keys.Up => (index - 1 + items.Count) % items.Count,
            Keys.Home => 0,
            Keys.End => items.Count - 1,
            _ => null,
        };
        if (target is not { } next) return false;
        items[next].Focus();
        return true;
    }

    /// <summary>按了一个字：跳到以它开头的项。</summary>
    /// <returns>跳了。</returns>
    public bool TypeAhead(MenuItemControlBase from, char key)
    {
        var items = Items();
        var match = search.Next(key.ToString(), [.. items.Select(item => item.Text)], items.IndexOf(from), Environment.TickCount64);
        if (match < 0) return false;
        items[match].Focus();
        return true;
    }
}

/// <summary>
/// 菜单（vue-tsx-pro 的 Menu，WAI-ARIA 的 Menu Button 模式）：按钮弹出一列操作。
/// <code>
/// var menu = new MenuControl { Trigger = new IconButtonControl { Icon = Lucide.Ellipsis, AccessibleName = "更多操作" } };
/// var rename = new MenuItemControl { Text = "重命名", Icon = Lucide.Pencil, Shortcut = "F2" };
/// rename.Click += (_, _) => …;
/// menu.Items.AddRange([rename, new SeparatorControl { Decorative = false }, new MenuItemControl { Text = "删除", Tone = MenuItemTone.Danger }]);
/// </code>
/// 键盘：触发器上 Enter / 空格 / ↓ 打开并聚焦第一项，↑ 打开并聚焦最后一项；菜单里 ↑↓ 移动（循环）、Home / End、按字母跳到以它开头的项，
/// Enter / 空格执行，Esc 关闭并回到触发器，Tab 关闭并继续往后。执行普通项后关闭；复选项默认不关，单选项默认关闭；都可以用 CloseOnSelect 改。
/// 菜单项的快捷键（<see cref="MenuItemControlBase.Shortcut"/>）在所在窗体里随时有效。浮层同 <see cref="PopoverControl"/>：两层窗口，点外面关闭，位置、翻转同弹出框。
/// 读屏：浮层窗口是菜单，以触发器为名称（写了 AccessibleName 时用它）。
/// </summary>
[ToolboxItem(true)]
[Description("菜单：按钮弹出一列操作。")]
public class MenuControl : Control, IMenuHost, IScopeAware
{
    readonly FloatingSurface surface;
    readonly ScrollAreaControl scroll = new() { IsViewportFocusable = false };
    readonly MenuList list = new();
    readonly HotkeyFilter filter;
    long closedAt = long.MinValue / 2;
    bool open;
    bool focusLast;
    (Control Target, bool AtPointer)? anchor;

    /// <summary>新建：没有触发器与菜单项，位置 bottom-start。</summary>
    public MenuControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        surface = new FloatingSurface(this);
        surface.Form.AccessibleRole = AccessibleRole.MenuPopup;
        scroll.Content = list;
        scroll.Dock = DockStyle.Fill;
        surface.Form.Controls.Add(scroll);
        surface.Outside += (_, _) => OnOutside();
        surface.Form.Escape += (_, _) => SetOpen(false, OpenChangeReason.Escape);
        filter = new HotkeyFilter(this);
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
                field.KeyDown -= OnTriggerKeyDown;
                Controls.Remove(field);
            }
            field = value;
            if (value is not null)
            {
                value.Click += OnTriggerClick;
                value.KeyDown += OnTriggerKeyDown;
                Controls.Add(value);
            }
            PerformLayout();
        }
    }

    /// <summary>菜单项：MenuItemControl、MenuLinkItemControl、MenuCheckboxItemControl、MenuRadioGroupControl、MenuGroupControl、MenuSubControl、SeparatorControl。</summary>
    [Browsable(false)]
    public ControlCollection Items => list.Controls;

    /// <summary>开着。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => open; set => SetOpen(value, null); }

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    [Category("Behavior")]
    public FloatingPlacement Placement { get; set; } = FloatingPlacement.BottomStart;

    /// <summary>字号档位，菜单项随之缩放；不写时跟着菜单（所在窗体）的字号。</summary>
    [Category("Appearance")]
    public TextSize TextSize { get; set; }

    bool ShouldSerializePlacement() => Placement != FloatingPlacement.BottomStart;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    /// <summary>用户开、关了（触发器、点外面、Esc、执行了一项、Tab 移走）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <inheritdoc />
    [Browsable(false)]
    public Form Surface => surface.Form;

    /// <summary>这一列（测试用）。</summary>
    internal MenuList List => list;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => Trigger?.GetPreferredSize(proposedSize) ?? Size.Empty;

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (Trigger is not { } trigger) return;
        if (levent.AffectedControl == trigger && AutoSize) Size = GetPreferredSize(Size.Empty);
        trigger.Bounds = ClientRectangle;
    }

    void IScopeAware.ScopeChanged()
    {
        ProviderControl.NotifyDescendants(surface.Form);
        if (IsOpen) Present();
    }

    /// <summary>点了触发器：开关（浮层刚因为点了外面关掉的那一下不再打开，同 PopoverControl）。</summary>
    void OnTriggerClick(object? sender, EventArgs e)
    {
        if (!IsOpen && Environment.TickCount64 - closedAt < 300) return;
        SetOpen(!IsOpen, OpenChangeReason.Trigger);
    }

    /// <summary>触发器上 ↓ / ↑：打开并聚焦第一项 / 最后一项。</summary>
    void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is not (Keys.Down or Keys.Up)) return;
        focusLast = e.KeyCode == Keys.Up;
        SetOpen(true, OpenChangeReason.Trigger);
        e.Handled = true;
    }

    /// <summary>以别的控件为锚点打开（右键菜单）：在指针所在处，或在控件的下方。</summary>
    internal void OpenAt(Control target, bool atPointer)
    {
        anchor = (target, atPointer);
        if (open) Present();
        else SetOpen(true, OpenChangeReason.Trigger);
    }

    /// <summary>用户打开、关上，并报告原因；代码里改 IsOpen 时 reason 为 null，不报告。</summary>
    internal void SetOpen(bool next, OpenChangeReason? reason)
    {
        if (next == open) return;
        open = next;
        if (next) Present();
        else Dismiss(restoreFocus: reason != OpenChangeReason.Focus);
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(next, known));
    }

    /// <summary>面板 = 一列菜单项 + 描边（不留内边距：内边距在一列里面，滚动条贴着边缘），至少 12em 宽；位置同弹出框。</summary>
    void Present()
    {
        if (!IsHandleCreated) return;
        var context = ElementContext.Of(this);
        var em = context.Em;
        list.Font = TextSize.Pixels is { } pixels ? Fonts.Get(Font.FontFamily.Name, pixels * 72 / 96, Font.Style) : Font;
        list.UpdateMetrics(list.Font.SizeInPoints * DeviceDpi / 72f);
        var inset = FloatingSurface.Border(context);
        var content = list.GetPreferredSize(Size.Empty);
        var area = Screen.FromControl(this).WorkingArea;
        var panelSize = new Size(Math.Max(content.Width, (int)MathF.Ceiling(12 * em)) + 2 * inset, Math.Min(content.Height + 2 * inset, area.Height - 16));
        var target = anchor switch
        {
            { AtPointer: true } => new Rectangle(Cursor.Position, new Size(1, 1)),
            { Target: var control } => control.RectangleToScreen(control.ClientRectangle),
            null => RectangleToScreen(ClientRectangle),
        };
        var placement = anchor is null ? Placement : FloatingPlacement.BottomStart;
        var (_, panel) = FloatingLayout.Position(placement, context.RightToLeft, target, panelSize, anchor is { AtPointer: true } ? 0 : context.Scale(6), area);
        surface.Form.AccessibleName = AccessibleName ?? (Trigger is { } trigger ? trigger.AccessibleName ?? trigger.Text : null);
        surface.Show(panel, inset, context, null);
        var items = list.Items();
        (focusLast ? items.LastOrDefault() : items.FirstOrDefault())?.Focus();
        focusLast = false;
    }

    /// <summary>藏起浮层；焦点在里面（Esc、执行了一项）时回到触发器（右键菜单回到打开前的控件，由它安排）。Tab 关的不还。</summary>
    void Dismiss(bool restoreFocus)
    {
        closedAt = Environment.TickCount64;
        anchor = null;
        if (!surface.Hide() || !restoreFocus) return;
        FindForm()?.Activate();
        Trigger?.Focus();
    }

    /// <summary>
    /// 浮层失去激活：是子菜单拿到了激活时不算点了外面（子菜单的窗口沿 Origin 能找回这一列）；稍后看清激活到了哪个窗口再决定。
    /// </summary>
    void OnOutside() => BeginInvoke(() =>
    {
        if (!IsOpen || OwnsActiveForm()) return;
        SetOpen(false, OpenChangeReason.Outside);
    });

    /// <summary>当前激活的窗口是这一列或它的子菜单。</summary>
    internal bool OwnsActiveForm()
    {
        for (Control? node = Form.ActiveForm; node is not null; node = node is FloatingForm floating ? floating.Origin.FindForm() : null)
            if (node == surface.Form) return true;
        return false;
    }

    /// <inheritdoc />
    public void CloseFromItem() => SetOpen(false, OpenChangeReason.Select);

    /// <inheritdoc />
    public void OutsideFromSubmenu()
    {
        if (IsOpen && !OwnsActiveForm()) SetOpen(false, OpenChangeReason.Outside);
    }

    /// <summary>菜单里按了 Tab：关上，焦点从触发器往后（Shift+Tab 往前）走一步。</summary>
    internal void TabOut(bool backward)
    {
        SetOpen(false, OpenChangeReason.Focus);
        if (Trigger is not { } trigger || FindForm() is not { } form) return;
        form.Activate();
        form.SelectNextControl(trigger, !backward, true, true, true);
    }

    /// <summary>按了键：是哪一项的快捷键时执行它（测试直接调）。</summary>
    /// <returns>执行了一项。</returns>
    internal bool OnHotkey(Keys keyData, bool win = false)
    {
        var item = ItemsWithin(list).FirstOrDefault(item => item.Hotkey is { } hotkey && hotkey.Matches(keyData, win) && item.Enabled);
        item?.Run();
        return item is not null;
    }

    /// <summary>容器里的菜单项，含分组、子菜单里的。</summary>
    internal static IEnumerable<MenuItemControlBase> ItemsWithin(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is MenuItemControlBase item) yield return item;
            foreach (var nested in ItemsWithin(child)) yield return nested;
            if (child is MenuSubControl sub)
                foreach (var nested in ItemsWithin(sub.SubList)) yield return nested;
        }
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Application.AddMessageFilter(filter);
    }

    /// <inheritdoc />
    protected override void OnHandleDestroyed(EventArgs e)
    {
        Application.RemoveMessageFilter(filter);
        base.OnHandleDestroyed(e);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.RemoveMessageFilter(filter);
            if (Trigger is { } trigger)
            {
                trigger.Click -= OnTriggerClick;
                trigger.KeyDown -= OnTriggerKeyDown;
            }
            surface.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>应用里按下的键：焦点在所在窗体或菜单里时看是不是哪一项的快捷键（同 CommandPaletteControl）。</summary>
    sealed class HotkeyFilter(MenuControl owner) : IMessageFilter
    {
        const int WmKeyDown = 0x100, WmSysKeyDown = 0x104, VkLWin = 0x5B, VkRWin = 0x5C;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg is not (WmKeyDown or WmSysKeyDown)) return false;
            var form = FromChildHandle(m.HWnd)?.FindForm();
            if (form is null || (form != owner.FindForm() && form != owner.surface.Form)) return false;
            var win = GetKeyState(VkLWin) < 0 || GetKeyState(VkRWin) < 0;
            return owner.OnHotkey((Keys)(int)m.WParam | ModifierKeys, win);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern short GetKeyState(int virtualKey);
    }
}
