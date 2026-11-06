using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 长文的容器（vue-tsx-pro 的 Prose，article）：段落、标题、列表依次排下来时的间距与行长。是一个竖排的 FlexControl：块之间默认 space.lg（1em）；
/// <see cref="LimitLineLength"/> 时最宽约 70 个字宽（70ch，写进 MaximumSize）。读屏里是文档。
/// TS 的行高 relaxed 在 WinForms 里画不出来（TextRenderer 不能设行距，DESIGN §14 行高）。
/// </summary>
public class ProseControl : FlexControl
{
    /// <summary>新建：竖排、块之间 space.lg，读屏里是文档。</summary>
    public ProseControl()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Lg;
        AccessibleRole = AccessibleRole.Document;
        UpdateMaximumWidth();
    }

    /// <summary>限制行长（约 70 个字宽），默认 true；放在窄栏里时可以关掉。</summary>
    [Category("Layout")]
    [Description("限制行长（约 70 个字宽）；放在窄栏里时可以关掉。")]
    [DefaultValue(true)]
    public bool LimitLineLength { get; set { field = value; UpdateMaximumWidth(); } } = true;

    /// <summary>70ch：字体里"0"的宽度的 70 倍（加上左右内边距）。</summary>
    void UpdateMaximumWidth()
    {
        var width = LimitLineLength ? 70 * TextRenderer.MeasureText("0", Font, Size.Empty, TextFormatFlags.NoPadding).Width + Padding.Horizontal : 0;
        MaximumSize = MaximumSize with { Width = width };
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateMaximumWidth();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateMaximumWidth();
    }
}
