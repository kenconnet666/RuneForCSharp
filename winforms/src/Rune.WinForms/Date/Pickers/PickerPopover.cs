namespace Rune.WinForms;

/// <summary>
/// 日期、时间选择器共用的打开按钮与弹层（vue-tsx-pro 的 usePickerPopover：DatePicker、DateRangePicker、TimePicker）。
/// 输入框外壳是定位的锚点，后缀里的按钮打开（放进外壳的 Suffix），弹层是两层窗口（<see cref="FloatingSurface"/>，同 PopoverControl）：
/// 点外面、Esc 关闭；关闭时焦点在弹层里就回到按钮（选好了时交给选择器指定的元素）；分段里按 Alt + ↓ 打开（同原生 select）。
/// 开关状态由选择器自己管，这里只报告"想开、想关"，并负责摆放、显示、藏起弹层。
/// </summary>
sealed class PickerPopover : IDisposable
{
    /// <summary>与外壳的距离（逻辑像素）：同 Select。</summary>
    const float Offset = 6;

    readonly SegmentedFieldControlBase owner;
    readonly FloatingSurface surface;
    long closedAt = long.MinValue / 2;

    /// <summary>接到选择器上：按钮的点击、弹层的点外面与 Esc、分段里的 Alt + ↓。</summary>
    /// <param name="owner">选择器（输入框外壳就是它，弹层的定位锚点）。</param>
    /// <param name="icon">按钮的图标。</param>
    /// <param name="toggle">想开（true）或想关（false）与原因：选择器据此改开关并报告。</param>
    public PickerPopover(SegmentedFieldControlBase owner, IconData icon, Action<bool, OpenChangeReason> toggle)
    {
        this.owner = owner;
        surface = new FloatingSurface(owner);
        Button = new IconButtonControl { Icon = icon, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Radius = Radius.Full };
        Button.Click += (_, _) =>
        {
            // 开着时点按钮：内容窗口先因为失去激活（点了外面）关了，紧接着这次点击又会把它打开；刚这样关掉的那一下不再打开
            if (!surface.Visible && Environment.TickCount64 - closedAt < 300) return;
            toggle(!surface.Visible, OpenChangeReason.Trigger);
        };
        surface.Form.AccessibleRole = AccessibleRole.Dialog;
        surface.Outside += (_, _) => toggle(false, OpenChangeReason.Outside);
        surface.Form.Escape += (_, _) => toggle(false, OpenChangeReason.Escape);
        foreach (var group in owner.Groups)
            group.KeyDown += (_, e) =>
            {
                // 分段里按 Alt + ↓：打开
                if (e.KeyCode != Keys.Down || !e.Alt || !Button.Enabled) return;
                toggle(true, OpenChangeReason.Trigger);
                e.Handled = e.SuppressKeyPress = true;
            };
    }

    /// <summary>后缀里的打开按钮（选择器把它放进 Suffix）。</summary>
    public IconButtonControl Button { get; }

    /// <summary>弹层的内容窗口（测试用）。</summary>
    public Form Surface => surface.Form;

    /// <summary>弹层与外壳同宽（时间列表）；默认按内容宽。</summary>
    public bool MatchWidth { get; init; }

    /// <summary>弹层的内边距，默认 sm（时间列表是 xs）。</summary>
    public Space Inset { get; init; } = Space.Sm;

    /// <summary>按钮的名称与能不能点（不可用、只读时按钮也不可用）。</summary>
    public void Sync(string label, bool editable)
    {
        Button.AccessibleName = label;
        Button.Enabled = editable;
        ControlShell.FitInnerButton(Button, owner.EmPixels());
    }

    /// <summary>
    /// 显示弹层：内容（选择器建好的）+ 内边距（<see cref="Inset"/>）+ 描边，贴着外壳（<paramref name="placement"/>，放不下时翻到另一边）；
    /// 显示后焦点交给 <paramref name="focus"/> 给出的控件（日历当前的那一格所在的网格：网格要等显示、建好窗口句柄后才有）。
    /// </summary>
    public void Show(Control content, FloatingPlacement placement, string label, Func<Control?> focus)
    {
        if (!owner.IsHandleCreated) return;
        surface.Form.SuspendLayout();
        foreach (var old in surface.Form.Controls.Cast<Control>().ToList()) old.Dispose();
        surface.Form.Controls.Clear();
        surface.Form.Controls.Add(content);
        surface.Form.ResumeLayout();
        // 日历要有窗口句柄才建网格（状态等今天、值都给了才开始）：先建出来，量的尺寸才对。
        // 内容窗口还没显示，CreateControl 不给看不见的窗口建子控件的句柄，逐个建
        CreateHandles(content);
        var context = ElementContext.Of(owner);
        var inset = (int)MathF.Round((Inset.Factor ?? 0) * context.Em) + FloatingSurface.Border(context);
        var target = owner.RectangleToScreen(owner.ClientRectangle);
        var width = Math.Max(0, target.Width - 2 * inset);
        var size = MatchWidth ? content.GetPreferredSize(new Size(width, 0)) with { Width = width } : content.GetPreferredSize(Size.Empty);
        // 同宽时宽度由外壳定：内容不再按自己想要的大小改回去
        if (MatchWidth) content.AutoSize = false;
        content.Bounds = new Rectangle(Point.Empty, size);
        var (_, panel) = FloatingLayout.Position(placement, context.RightToLeft, target, new Size(size.Width + 2 * inset, size.Height + 2 * inset), context.Scale(Offset), Screen.FromControl(owner).WorkingArea);
        surface.Form.AccessibleName = label;
        surface.Show(panel, inset, context, null);
        focus()?.Focus();
    }

    static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls) CreateHandles(child);
    }

    /// <summary>藏起弹层；焦点原来在里面时交给 <paramref name="focus"/>（没有时还给按钮）。</summary>
    public void Hide(Control? focus = null)
    {
        closedAt = Environment.TickCount64;
        if (!surface.Hide()) return;
        owner.FindForm()?.Activate();
        (focus ?? Button).Focus();
    }

    public void Dispose() => surface.Dispose();
}
