using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 引用（vue-tsx-pro 的 Blockquote）：起始边一条竖线（从右到左时在右边），里面的文字是次要文字色。提示、警告用 Alert，不用引用。
/// 是一个放子控件的面板（同 Panel）：子控件的文字颜色跟随它（ForeColor 是环境属性）。
/// <code>
/// var quote = new BlockquoteControl();
/// quote.Controls.Add(new ParagraphControl { Text = "渐进式框架……", Dock = DockStyle.Top });
/// </code>
/// 竖线 borderWidths.md、颜色 borderStrong；上下内边距 space.xs、起始边 space.lg（按字体的 em，随字体与 DPI 重算）。
/// </summary>
public class BlockquoteControl : Panel, IScopeAware
{
    readonly Effect theme;

    /// <summary>新建：自动大小，跟随主题。</summary>
    public BlockquoteControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        theme = new Effect(ApplyTheme);
        ApplyTheme();
        UpdatePadding();
    }

    /// <summary>内边距由字体与 DPI 算出（起始边留出竖线与 space.lg），不在设计器里改。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding Padding { get => base.Padding; set => base.Padding = value; }

    float Em => this.EmPixels();

    float Rule => Tokens.BorderWidth.Md * DeviceDpi / 96f;

    /// <summary>主题变了：文字颜色换成 textMuted、重画竖线。</summary>
    void IScopeAware.ScopeChanged() => ApplyTheme();

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ApplyTheme();
    }

    void ApplyTheme()
    {
        using (theme.Track()) ForeColor = ProviderControl.ThemeOf(this).Colors.TextMuted;
        Invalidate();
    }

    void UpdatePadding()
    {
        var start = (int)MathF.Round(Rule + Tokens.Space.Lg * Em);
        var block = (int)MathF.Round(Tokens.Space.Xs * Em);
        base.Padding = RightToLeft == RightToLeft.Yes ? new Padding(0, block, start, block) : new Padding(start, block, 0, block);
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var width = (int)MathF.Ceiling(Rule);
        var x = RightToLeft == RightToLeft.Yes ? Width - width : 0;
        using var brush = new SolidBrush(ProviderControl.ThemeOf(this).Colors.BorderStrong);
        e.Graphics.FillRectangle(brush, x, 0, width, Height);
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdatePadding();
    }

    /// <inheritdoc />
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        UpdatePadding();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdatePadding();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) theme.Dispose();
        base.Dispose(disposing);
    }
}
