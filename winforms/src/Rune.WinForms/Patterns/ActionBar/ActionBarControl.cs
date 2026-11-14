using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 操作栏（vue-tsx-pro 的 ActionBar）：长表单、详情页底部的"取消 / 保存草稿 / 提交"，起始边是说明（未保存的改动、已选几项），按钮靠结束边排开、放不下时换行。
/// <code>
/// var bar = new ActionBarControl { Start = "有 3 处改动还没保存", Dock = DockStyle.Bottom };
/// bar.Actions.AddRange([cancel, submit]);
/// </code>
/// 外观：surface 底、上边 xs 的 borderMuted 描边，上下 space.md、左右 space.lg；说明是次要色、小一号。
/// 出入：WinForms 没有网页的 position: sticky（子控件是窗口，和容器的排版抢位置不可靠），上游的"滚动时贴在滚动区域的底边"做法是
/// 把它停靠在滚动区域外面的底边（DockStyle.Bottom），一直看得见；也就没有"盖着下面的内容时带阴影"。
/// </summary>
[ToolboxItem(true)]
[Description("操作栏：起始边的说明、结束边的按钮。")]
public class ActionBarControl : FlexControl
{
    readonly TextControl start = new() { Tone = TextTone.Muted, TextSize = TextSize.Sm, Visible = false };
    readonly FlexControl actions = new() { Wrap = FlexWrap.Wrap, Align = FlexAlign.Center, Justify = FlexJustify.End, Gap = Space.Sm, AutoSize = true };

    /// <summary>新建：横排换行，说明占满剩下的宽度，按钮靠结束边。</summary>
    public ActionBarControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Align = FlexAlign.Center;
        Wrap = FlexWrap.Wrap;
        Gap = Space.Md;
        AutoSize = true;
        Controls.AddRange([start, actions]);
        SetGrow(start, true);
        SetGrow(actions, true);
        UpdatePadding();
    }

    /// <summary>起始边的说明：未保存的改动、已选几项、校验汇总。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Start
    {
        get;
        set
        {
            field = value;
            start.Text = value ?? "";
            start.Visible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>按钮（靠结束边排开）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ControlCollection Actions => actions.Controls;

    /// <summary>说明（测试用）。</summary>
    internal TextControl StartPart => start;

    int Line => Math.Max(1, (int)MathF.Round(Tokens.BorderWidth.Xs * DeviceDpi / 96f));

    /// <summary>上下 space.md、左右 space.lg；上边让出描边。</summary>
    void UpdatePadding()
    {
        var em = this.EmPixels();
        var (md, lg) = ((int)MathF.Round(Tokens.Space.Md * em), (int)MathF.Round(Tokens.Space.Lg * em));
        Padding = new Padding(lg, md + Line, lg, md);
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdatePadding();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdatePadding();
    }

    /// <summary>surface 底、上边一条 borderMuted 描边。</summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var colors = ProviderControl.ThemeOf(this).Colors;
        using (var fill = new SolidBrush(colors.Surface)) e.Graphics.FillRectangle(fill, ClientRectangle);
        using var border = new SolidBrush(colors.BorderMuted);
        e.Graphics.FillRectangle(border, 0, 0, Width, Line);
    }
}
