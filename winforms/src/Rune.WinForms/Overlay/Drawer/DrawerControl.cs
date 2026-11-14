using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 抽屉（vue-tsx-pro 的 Drawer）：贴着窗体一侧的模态面板。内核同 <see cref="DialogControl"/>（遮罩、焦点、Esc、点遮罩、BeforeClose、确认、命令式 ShowAsync）。
/// <code>
/// var drawer = new DrawerControl { Side = DrawerSide.End, Title = "筛选", Content = filters };
/// drawer.PanelWidthChanged += (_, _) =&gt; settings.DrawerWidth = drawer.PanelWidth;
/// </code>
/// <see cref="Side"/> 是哪一侧（默认结束边，从右到左排版时左右互换）：左右两侧占满窗体客户区的高度，宽度按档位 DialogSize（默认 xs，不超出窗体），
/// 写了 <see cref="PanelWidth"/> 时按它（上游的 width：Control 自己有 Width）；上下两侧占满宽度、高度按内容（不超出窗体）。面板是直角。
/// 左右两侧默认 <see cref="Resizable"/>：靠正文的一边是宽度调整手柄（<see cref="ResizeHandleControl"/>，拖动、聚焦后 ← / →、双击回到打开时的宽度），
/// 能一直拖到只剩 <see cref="MinPanelWidth"/>（默认 48）那么一窄条；拖窄以后内容保持能读的最小宽度（20em），多出来的被面板裁掉。
/// 与上游的出入：面板是两层窗口，没有滑入动画；贴边一侧的描边照常画（在窗体边上）。
/// </summary>
[Description("抽屉：贴着窗体一侧的模态面板，靠正文的一边可以拖动调整宽度。")]
public class DrawerControl : DialogControl
{
    /// <summary>给拖动留出的窗体宽度（设备无关像素）：手柄最多拖到客户区宽减它（同上游的 innerWidth - 48）。</summary>
    const int Reserve = 48;

    readonly ResizeHandleControl handle = new();
    Rectangle panelOnScreen;
    int? initialWidth;

    /// <summary>新建：结束边、xs 宽、能拖动调整宽度。</summary>
    public DrawerControl()
    {
        handle.Override = (() => panelOnScreen.Width, width => PanelWidth = width);
        PanelForm.Controls.Add(handle);
        handle.BringToFront();
        ApplySide();
    }

    /// <summary>从哪一侧滑入，默认结束边。</summary>
    [Category("Appearance")]
    public DrawerSide Side { get => field.Value is null ? DrawerSide.End : field; set { field = value; ApplySide(); } }

    /// <summary>左右两侧时靠正文的一边能拖动调整宽度，默认 true。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Resizable { get; set { field = value; ApplySide(); } } = true;

    /// <summary>面板的宽度（设备像素，左右两侧）：写了就不按档位；拖动后变（<see cref="PanelWidthChanged"/>）。</summary>
    [Category("Layout")]
    [DefaultValue(null)]
    public int? PanelWidth
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (IsOpen) PresentPanel();
            PanelWidthChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>拖动时的最小宽度（设备无关像素），默认 48：只留一窄条（几乎关上，还能拖回来）。</summary>
    [Category("Layout")]
    [DefaultValue(48)]
    public int MinPanelWidth { get; set; } = 48;

    /// <summary>拖动、按键调整了面板宽度。</summary>
    public event EventHandler? PanelWidthChanged;

    /// <summary>宽度调整手柄（测试用）。</summary>
    internal ResizeHandleControl ResizeHandle => handle;

    /// <summary>面板在屏幕上的矩形（测试用）。</summary>
    internal Rectangle PanelOnScreen => panelOnScreen;

    bool ShouldSerializeSide() => Side != DrawerSide.End;

    bool Resizing => Side.Inline && Resizable;

    /// <summary>贴在窗体左边：起始边（从右到左排版时是结束边）。</summary>
    bool AtLeft => (Side == DrawerSide.Start) != (RightToLeft == RightToLeft.Yes);

    void ApplySide()
    {
        handle.Visible = Resizing;
        handle.AccessibleName = ProviderControl.LangOf(this).DrawerResize;
        // 手柄贴在靠正文的一边：面板在左边时是它的右边
        handle.Edge = AtLeft == (RightToLeft != RightToLeft.Yes) ? PaneSide.End : PaneSide.Start;
        if (IsOpen) PresentPanel();
    }

    /// <inheritdoc />
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        ApplySide();
    }

    /// <inheritdoc />
    private protected override bool SquarePanel => true;

    /// <summary>左右两侧：PanelWidth 或档位的宽度（不超出窗体）、客户区高；上下两侧：客户区宽、高度按内容（不超出）。</summary>
    private protected override Rectangle PanelBounds(Rectangle area, ElementContext context, int border)
    {
        var em = context.Em;
        // 双击回到打开时的宽度（第一次摆面板时记下）
        initialWidth ??= PanelWidth;
        // 拖窄以后内容保持能读的最小宽度，多出来的被面板裁掉
        ContentLayout.MinimumSize = new Size(Resizing ? (int)Math.Min(20 * em, Math.Max(0, area.Width - 3 * em)) : 0, 0);
        if (!Side.Inline)
        {
            var height = Math.Min(area.Height, ContentHeight(area.Width - 2 * border) + 2 * border);
            return panelOnScreen = new Rectangle(area.X, Side == DrawerSide.Top ? area.Y : area.Bottom - height, area.Width, height);
        }
        var width = Math.Min(area.Width, PanelWidth ?? (int)MathF.Round(context.Scale(DialogSize.Pixels)));
        return panelOnScreen = new Rectangle(AtLeft ? area.X : area.Right - width, area.Y, width, area.Height);
    }

    /// <summary>手柄贴着面板靠正文的一边、上下铺满（内容窗口的坐标）；能拖到的范围是最小宽度到客户区宽减 48。</summary>
    private protected override void ArrangeExtras()
    {
        if (!Resizing) return;
        var context = ElementContext.Of(this);
        var form = PanelForm;
        var thickness = (int)Math.Round(Tokens.Space.Sm * context.Em);
        handle.MinLength = (int)MathF.Round(context.Scale(MinPanelWidth));
        handle.MaxLength = Math.Max(handle.MinLength, (FindForm()?.ClientSize.Width ?? form.Width) - (int)MathF.Round(context.Scale(Reserve)));
        handle.DefaultLength = initialWidth;
        handle.Bounds = new Rectangle(AtLeft ? form.ClientSize.Width - thickness : 0, 0, thickness, form.ClientSize.Height);
        handle.BringToFront();
    }
}
