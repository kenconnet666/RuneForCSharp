namespace Rune.WinForms;

/// <summary>
/// 输入框下面的选项弹出层（vue-tsx-pro 的 ListPopover）：TagsInputControl、ComboboxControl 共用。不激活的两层窗口（FloatingSurface），
/// 贴着外壳下方、起始边对齐（放不下时翻到上方）、与外壳同宽、隔 6；里面是嵌入的 <see cref="ListboxControl"/>，焦点一直在输入框里。
/// </summary>
sealed class ListPopover : IDisposable
{
    /// <summary>与外壳的距离（设备无关像素）。</summary>
    const float Offset = 6;

    readonly Control owner;
    readonly Control input;
    readonly FloatingSurface surface;

    /// <summary>建好弹出层：<paramref name="input"/> 是焦点所在的输入框；点了外面、按了 Esc 时分别调 <paramref name="outside"/>、<paramref name="escape"/>。</summary>
    public ListPopover(Control owner, Control input, Action outside, Action escape)
    {
        this.owner = owner;
        this.input = input;
        surface = new FloatingSurface(owner);
        surface.Form.Controls.Add(Listbox);
        surface.Outside += (_, _) =>
        {
            if (!Presenting) outside();
        };
        surface.Form.Escape += (_, _) => escape();
    }

    /// <summary>列表。</summary>
    public ListboxControl Listbox { get; } = new() { Embedded = true, Dock = DockStyle.Fill };

    /// <summary>
    /// 正在显示浮层窗口：平台可能把焦点从输入框拿走一下、让宿主窗体失活，这时的失焦与"点了外面"都不算（使用方的失焦处理先看它）。
    /// </summary>
    public bool Presenting { get; private set; }

    /// <summary>浮层（测试用）。</summary>
    public FloatingSurface Surface => surface;

    /// <summary>显示（列表按内容的高度、与外壳同宽）；显示完把焦点还给输入框。</summary>
    public void Show()
    {
        if (!owner.IsHandleCreated) return;
        var context = ElementContext.Of(owner);
        var border = FloatingSurface.Border(context);
        var listHeight = Listbox.GetPreferredSize(new Size(owner.Width - 2 * border, 0)).Height;
        var panelSize = new Size(owner.Width, listHeight + 2 * border);
        var target = owner.RectangleToScreen(owner.ClientRectangle);
        var (_, panel) = FloatingLayout.Position(FloatingPlacement.BottomStart, context.RightToLeft, target, panelSize, context.Scale(Offset), Screen.FromControl(owner).WorkingArea);
        surface.Form.AccessibleName = owner.AccessibleName;
        var focused = input.Focused;
        Presenting = true;
        try
        {
            surface.Show(panel, border, context, null);
            if (focused && !input.Focused) input.Focus();
        }
        finally
        {
            Presenting = false;
        }
    }

    /// <summary>藏起。</summary>
    public void Hide() => surface.Hide();

    /// <inheritdoc />
    public void Dispose() => surface.Dispose();
}
