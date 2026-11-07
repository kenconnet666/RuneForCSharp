using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 多行文本框（vue-tsx-pro 的 Textarea）：外壳与 TextFieldControl 相同（<see cref="TextInputControl"/>），高度随内容增长，在 <see cref="Rows"/> 与 <see cref="MaxRows"/> 行之间，
/// 超过 MaxRows 后出现滚动条。
/// <code>
/// var note = new TextareaControl { Rows = 3, MaxRows = 8, ShowCount = true, MaxLength = 200 };
/// </code>
/// 高度：至少 Rows 行加上文字区上下各 space.xs 的内边距（TS 的 rows 加自动增高），至多 MaxRows 行（含内边距）。<see cref="Fixed"/> 时固定为 Rows 行。
/// 清空按钮贴在第一行（右上角），字数贴在右下角。出入：平台的 TextBox 改不了行距，行高是字体自己的（TS 是 lineHeights.normal）；滚动条是系统的；不能拖动调整高度。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(TextChanged))]
[Description("多行文本框：高度随内容增长，在最少、最多行数之间。")]
public class TextareaControl : TextInputControl
{
    /// <summary>新建：输入框占满，清空按钮贴顶、字数贴底。</summary>
    public TextareaControl() : base(new TextBox { Multiline = true, WordWrap = true, AcceptsReturn = true, ScrollBars = ScrollBars.None })
    {
        Align = FlexAlign.Stretch;
        Controls.AddRange([Input, ClearButton, Count]);
        SetGrow(Input, true);
        SetAlignSelf(ClearButton, FlexAlign.Start);
        SetAlignSelf(Count, FlexAlign.End);
        Input.TextChanged += (_, _) => FitHeight();
        Input.SizeChanged += (_, _) => FitHeight();
    }

    /// <summary>最少显示几行，默认 3。</summary>
    [Category("Layout")]
    [DefaultValue(3)]
    public int Rows
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
            Sync();
        }
    } = 3;

    /// <summary>最多增高到几行，之后出现滚动条；不写时不限。</summary>
    [Category("Layout")]
    [DefaultValue(null)]
    public int? MaxRows
    {
        get;
        set
        {
            if (value is < 1) throw new ArgumentOutOfRangeException(nameof(value), value, "至少 1 行");
            field = value;
            Sync();
        }
    }

    /// <summary>不随内容增高，固定为 Rows 行。</summary>
    [Category("Layout")]
    [DefaultValue(false)]
    public bool Fixed
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>上下多留 space.xs（多行时内容从顶部开始）；清空按钮下移 space.xs、字数离底 space.xs。</summary>
    protected override void Sync()
    {
        base.Sync();
        var em = Em;
        var md = (int)MathF.Round(Tokens.Space.Md * em);
        var xs = (int)MathF.Round(Tokens.Space.Xs * em);
        Padding = new Padding(md, xs, md, xs);
        ClearButton.Margin = new Padding(-xs, xs, -xs, 0);
        Count.Margin = new Padding(0, 0, 0, xs);
        FitHeight();
    }

    /// <summary>一行的高度：Edit 控件按字体的行高（tmHeight）排行，不是 Font.GetHeight 的行距。</summary>
    int LineHeight()
    {
        using var graphics = Input.IsHandleCreated ? Input.CreateGraphics() : null;
        return graphics is null
            ? (int)MathF.Ceiling(Input.Font.GetHeight(DeviceDpi))
            : TextRenderer.MeasureText(graphics, "国", Input.Font, Size.Empty, TextFormatFlags.NoPadding).Height;
    }

    /// <summary>文字区的高度：内容的行数夹在 Rows 与 MaxRows 之间（固定时正好 Rows 行），上下各 space.xs；放不下时出现滚动条。</summary>
    void FitHeight()
    {
        var line = LineHeight();
        var inset = (int)MathF.Round(Tokens.Space.Xs * Em);
        var lines = Input.IsHandleCreated ? Input.GetLineFromCharIndex(Math.Max(0, Input.TextLength)) + 1 : Math.Max(1, Text.Split('\n').Length);
        // Edit 控件在文字上下各留一个像素
        var edge = 2 * LogicalToDeviceUnits(1);
        var min = Rows * line + edge;
        var max = Fixed ? min : MaxRows is { } rows ? Math.Max(min, rows * line - 2 * inset + edge) : int.MaxValue;
        var height = Math.Clamp(lines * line + edge, min, max);
        Input.Margin = new Padding(0, inset, 0, inset);
        var scrolls = lines * line + edge > height;
        if (Input.ScrollBars != (scrolls ? ScrollBars.Vertical : ScrollBars.None)) Input.ScrollBars = scrolls ? ScrollBars.Vertical : ScrollBars.None;
        if (Input.MinimumSize.Height == height && Input.MaximumSize.Height == height) return;
        Input.MinimumSize = new Size(0, height);
        Input.MaximumSize = new Size(0, height);
        PerformLayout();
    }
}
