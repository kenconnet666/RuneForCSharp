using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Automation;

namespace Rune.WinForms;

/// <summary>
/// 批量操作栏（vue-tsx-pro 的 SelectionBar）：表格勾选了行之后出现在表格上方，“已选 N 项 · 取消选择”，结束边是批量操作。
/// <code>
/// var bar = new SelectionBarControl { Count = selected.Count };
/// bar.Actions.Add(new ButtonControl { Text = "批量导出", TextSize = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral });
/// bar.ClearRequested += (_, _) => ClearSelection();
/// </code>
/// accentSoft 底、md 圆角、控件高度，字号 sm；数目 semibold、accent、等宽数字；“取消选择”是 link 按钮，点了报告 <see cref="ClearRequested"/>（清空选择由使用方做）。
/// <see cref="Count"/> 为 0 时不显示、不占位（WinForms 的控件不能半透明，不淡入淡出）。<see cref="Label"/> 换掉默认的数目文字（跨页全选时写“已选全部 1,286 项”）。
/// 数目变了经读屏的通知念出来。
/// </summary>
[DefaultEvent(nameof(ClearRequested))]
[Description("批量操作栏：已选 N 项、取消选择、批量操作；没有选择时不显示。")]
public class SelectionBarControl : Control
{
    readonly FlexControl row = new() { Align = FlexAlign.Center, Wrap = true, Gap = Space.Md };
    readonly TextControl count = new() { Weight = TextWeight.Semibold, Tone = TextTone.Accent, Numeric = true, TextSize = TextSize.Sm };
    readonly ButtonControl clear = new() { Variant = ButtonVariant.Link, TextSize = TextSize.Sm };
    readonly FlexControl actions = new() { Align = FlexAlign.Center, Wrap = true, Gap = Space.Sm, Justify = FlexJustify.End };

    /// <summary>新建：数目、取消选择、批量操作排成一行（放不下时换行）；没有选择时不显示。</summary>
    public SelectionBarControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        AutoSize = true;
        row.SetGrow(actions, true);
        row.Controls.AddRange([count, clear, actions]);
        Controls.Add(row);
        clear.Click += (_, e) => ClearRequested?.Invoke(this, e);
        Visible = false;
    }

    /// <summary>已选几项；0 时不显示。</summary>
    [Category("Behavior")]
    [Description("已选几项；0 时不显示。")]
    [DefaultValue(0)]
    public int Count
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync(announce: true);
        }
    }

    /// <summary>已选数目的文字，默认取语言包（已选 N 项）。</summary>
    [Category("Appearance")]
    [Description("已选数目的文字；不写时是“已选 N 项”。")]
    [DefaultValue(null)]
    public string? Label { get; set { field = value; Sync(announce: true); } }

    /// <summary>批量操作的按钮。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ControlCollection Actions => actions.Controls;

    /// <summary>点了“取消选择”：清空选择由使用方做。</summary>
    public event EventHandler? ClearRequested;

    /// <summary>读屏最后念的话（测试用）。</summary>
    internal string? Announced { get; private set; }

    /// <summary>数目的文字（测试用）。</summary>
    internal string CountText => count.Text;

    /// <summary>取消选择按钮（测试用）。</summary>
    internal ButtonControl ClearButton => clear;

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 语言包、字号取自外层：放进界面后重新生成文字、算尺寸
        ApplyMetrics();
        Sync(announce: false);
    }

    /// <summary>有选择时显示（数目停在最后一次非 0 的值）；数目或文字变了，读屏念出新的（给了 Label 念它，否则念“已选 N 项”）。</summary>
    void Sync(bool announce)
    {
        var lang = ProviderControl.LangOf(this);
        if (Count > 0) count.Text = Label ?? lang.TableSelectedCount(Count);
        clear.Text = lang.TableClearSelection;
        if (announce && IsHandleCreated)
        {
            Announced = Label ?? lang.TableSelectedCount(Count);
            AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, Announced);
        }
        Visible = Count > 0;
    }

    /// <summary>字号 sm；内边距 xs × md，最小高度是控件高度（em）。</summary>
    void ApplyMetrics()
    {
        var em = this.EmPixels() * Tokens.FontSize.Sm / Tokens.FontSize.Md;
        var (xs, md) = ((int)MathF.Round(Tokens.Space.Xs * em), (int)MathF.Round(Tokens.Space.Md * em));
        Padding = new Padding(md, xs, md, xs);
        MinimumSize = new Size(0, (int)MathF.Round(ControlHeight.Of(ElementContext.Of(this), em, Tokens.Size.Control)));
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyMetrics();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyMetrics();
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        var inner = row.GetPreferredSize(new Size(Math.Max(0, proposedSize.Width - Padding.Horizontal), 0));
        return new Size(inner.Width + Padding.Horizontal, Math.Max(MinimumSize.Height, inner.Height + Padding.Vertical));
    }

    /// <summary>一行内容摆在内边距以内（Control 的 DisplayRectangle 不扣 Padding，停靠用不上），竖直居中。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var width = Math.Max(0, Width - Padding.Horizontal);
        var height = row.GetPreferredSize(new Size(width, 0)).Height;
        row.SetBounds(Padding.Left, Math.Max(Padding.Top, (Height - height) / 2), width, height);
    }

    /// <summary>accentSoft 底、md 圆角。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var context = ElementContext.Of(this);
        var em = this.EmPixels() * Tokens.FontSize.Sm / Tokens.FontSize.Md;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), context.Theme.Radii.Md * em);
        using var fill = new SolidBrush(context.Theme.Colors.AccentSoft);
        e.Graphics.FillPath(fill, shape);
    }
}
