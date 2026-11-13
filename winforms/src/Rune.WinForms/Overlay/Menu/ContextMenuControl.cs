using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 右键菜单（vue-tsx-pro 的 ContextMenu）：在一块区域里右键（或键盘的 Shift+F10、菜单键）时弹出菜单；菜单项与 <see cref="MenuControl"/> 完全相同。
/// <code>
/// var region = new ContextMenuControl { Content = fileCards };
/// region.Items.Add(new MenuItemControl { Text = "重命名" });
/// </code>
/// 指针右键时从指针所在处弹出；键盘打开时从获得焦点的控件下方弹出。关闭后焦点回到打开前的控件。
/// 区域本身不可聚焦：键盘用户要能打开，里面得有可聚焦的控件。在菜单上右键不再打开；不可用（Enabled=false）时不弹出。
/// 接平台的 WM_CONTEXTMENU（子控件没有自己的右键菜单时交给父控件）。菜单的读屏名称默认是语言包的"更多操作"，<see cref="Label"/> 改。
/// </summary>
[ToolboxItem(true)]
[Description("右键菜单：在区域里右键弹出菜单。")]
public class ContextMenuControl : Control
{
    const int WmContextMenu = 0x007B;

    readonly MenuControl menu = new() { Size = Size.Empty };
    Control? origin;

    /// <summary>新建：内容与（不占位的）菜单。</summary>
    public ContextMenuControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Controls.Add(menu);
        menu.OpenChanged += (_, e) =>
        {
            OpenChanged?.Invoke(this, e);
            if (!e.IsOpen) RestoreFocus();
        };
    }

    /// <summary>区域里的内容（铺满区域）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content
    {
        get;
        set
        {
            if (field == value) return;
            if (field is not null) Controls.Remove(field);
            field = value;
            if (value is null) return;
            value.Dock = DockStyle.Fill;
            Controls.Add(value);
        }
    }

    /// <summary>菜单项：同 <see cref="MenuControl.Items"/>。</summary>
    [Browsable(false)]
    public ControlCollection Items => menu.Items;

    /// <summary>菜单的读屏名称；不写时取语言包。</summary>
    [Category("Accessibility")]
    [DefaultValue(null)]
    public string? Label { get; set; }

    /// <summary>菜单开着。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => menu.IsOpen; set => menu.IsOpen = value; }

    /// <summary>用户开、关了菜单。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>里面的菜单（测试用）。</summary>
    internal MenuControl Menu => menu;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => Content?.GetPreferredSize(proposedSize) ?? Size.Empty;

    /// <summary>WM_CONTEXTMENU：坐标是 -1 时是键盘（Shift+F10、菜单键）打开的。</summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmContextMenu)
        {
            var source = FromChildHandle(m.WParam) ?? this;
            var atPointer = (long)m.LParam != -1;
            if (Request(source, atPointer)) return;
        }
        base.WndProc(ref m);
    }

    /// <summary>
    /// 右键、Shift+F10、菜单键：从指针所在处（键盘时获得焦点的控件下方）弹出；已经开着时换到新的位置。在菜单上、不可用时不弹出。
    /// </summary>
    /// <param name="source">请求从哪个控件来。</param>
    /// <param name="atPointer">指针右键（不是键盘）。</param>
    /// <returns>弹出了（平台自己的右键菜单不再出现）。</returns>
    internal bool Request(Control source, bool atPointer)
    {
        if (!Enabled || source.FindForm() == menu.Surface) return false;
        origin = FindForm()?.ActiveControl;
        menu.AccessibleName = Label ?? ProviderControl.LangOf(this).ContextMenuLabel;
        menu.OpenAt(atPointer ? this : source, atPointer);
        return true;
    }

    /// <summary>关上后：焦点还给打开前的控件。</summary>
    void RestoreFocus()
    {
        var keep = origin;
        origin = null;
        if (keep is null || keep.IsDisposed) return;
        FindForm()?.Activate();
        keep.Focus();
    }
}
