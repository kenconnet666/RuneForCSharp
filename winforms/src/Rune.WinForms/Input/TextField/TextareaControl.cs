using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 多行文本框（vue-tsx-pro 的 Textarea）：外壳与 TextFieldControl 相同（<see cref="TextInputControl"/>），高度随内容增长，在 <see cref="Rows"/> 与 <see cref="MaxRows"/> 行之间，
/// 超过 MaxRows 后出现滚动条。
/// <code>
/// var note = new TextareaControl { Rows = 3, MaxRows = 8, ShowCount = true, MaxLength = 200 };
/// </code>
/// 高度：至少 Rows 行加上文字区上下各 space.xs 的内边距（TS 的 rows 加自动增高），至多 MaxRows 行（含内边距）。<see cref="Fixed"/> 时固定为 Rows 行。
/// 清空按钮贴在第一行（右上角），字数贴在右下角，两个在同一列。出入：平台的 TextBox 改不了行距，行高是字体自己的（TS 是 lineHeights.normal）；滚动条是系统的；不能拖动调整高度。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(TextChanged))]
[Description("多行文本框：高度随内容增长，在最少、最多行数之间。")]
public class TextareaControl : TextInputControl
{
    readonly TrailingColumn trailing;

    /// <summary>新建：输入框占满；清空按钮贴顶、字数贴底，叠在结束边的同一列（都有时不各占一列，不然清空按钮被挤到字数左边）。</summary>
    public TextareaControl() : base(new TextBox { Multiline = true, WordWrap = true, AcceptsReturn = true, ScrollBars = ScrollBars.None })
    {
        Align = FlexAlign.Stretch;
        // 中间一段空白撑开：只有一个时清空照样贴顶、字数照样贴底（两端对齐在只剩一个时会贴到开头）
        var push = new Control { Size = Size.Empty };
        trailing = new TrailingColumn(this) { Direction = FlexDirection.Column, Align = FlexAlign.End };
        trailing.Controls.AddRange([ClearButton, push, Count]);
        trailing.SetGrow(push, true);
        Controls.AddRange([Input, trailing]);
        SetGrow(Input, true);
        // 字数、空白不是外壳的直接子控件，外壳接的"点不能聚焦的部件就聚焦输入框"够不到它们
        Count.MouseDown += (_, _) => Input.Focus();
        push.MouseDown += (_, _) => Input.Focus();
        Input.TextChanged += (_, _) => FitHeight();
        Input.SizeChanged += (_, _) => FitHeight();
        Sync();
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
        // 两个都不显示时整列藏起来：空着的一列也会在输入框后面多出一个间距（基类的构造函数里也会调到这里，那时还没建这一列）
        if (trailing is not null) trailing.Visible = ClearShown || ShowCount;
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

    /// <summary>
    /// 结束边那一列（清空在上、字数在下）：想要的高度按该显示的部件算。容器还没显示时子控件的 Visible 都读成 false，
    /// Flex 分不出哪个是故意藏起来的，会把藏着的清空按钮也算进高度，把文本框撑高。
    /// </summary>
    sealed class TrailingColumn(TextareaControl owner) : FlexControl
    {
        public override Size GetPreferredSize(Size proposedSize)
        {
            var (width, height) = (0, 0);
            if (owner.ClearShown) Add(owner.ClearButton);
            if (owner.ShowCount) Add(owner.Count);
            return new Size(width, height);

            void Add(Control part)
            {
                var size = part.GetPreferredSize(Size.Empty) + part.Margin.Size;
                (width, height) = (Math.Max(width, size.Width), height + size.Height);
            }
        }
    }
}
