using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 代码块（vue-tsx-pro 的 CodeBlock）：等宽字体的代码，标题栏、复制、行号、强调与增删的行、长代码折叠。
/// <code>
/// new CodeBlockControl { Code = source, Language = "cs", Title = "Program.cs", LineNumbers = true, HighlightLines = "2,4-5", MaxLines = 20 };
/// </code>
/// 高亮：<see cref="Highlighter"/>（默认 <see cref="CodeHighlighting.Builtin"/>），异步：先显示纯文本，结果到了再替换；同一批属性变化只取一次；
/// 没给语言、语言不认识、<see cref="Highlight"/> 为 false、高亮失败时按纯文本显示。长行在 <see cref="ScrollAreaControl"/> 里横向滚动（<see cref="Wrap"/> 时换行）；
/// 复制按钮在右上角，有标题时放进标题栏；超过 <see cref="MaxLines"/> 行时折叠，底部的按钮展开或收起。代码总是从左到右排。
/// 与上游的出入：代码不能选中（复制按钮复制全部）；代码文字用 GDI 画，行高按 1.5 倍字号排。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Code))]
[Description("代码块：高亮、复制、标题栏、行号、行高亮、增删、长代码折叠。")]
public class CodeBlockControl : Control, IScopeAware
{
    readonly CodeView view;
    readonly ScrollAreaControl scroll = new() { Axis = ScrollAreaAxis.X };
    readonly CopyButtonControl copy = new();
    readonly ExpandBar expand;
    int highlights;
    bool highlightQueued;

    /// <summary>新建：组装标题栏、代码区与展开按钮。</summary>
    public CodeBlockControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        // 高度按代码要的（宽度跟着外面给的）
        AutoSize = true;
        view = new CodeView(this);
        expand = new ExpandBar(this) { Visible = false };
        scroll.Content = view;
        copy.TextSize = TextSize.Sm;
        copy.Tone = StatusTone.Neutral;
        copy.Padding = Padding.Empty;
        Controls.AddRange([scroll, copy, expand]);
        copy.BringToFront();
    }

    /// <summary>代码文本。</summary>
    [Category("Data")]
    [Description("代码文本。")]
    [DefaultValue("")]
    [Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string Code { get; set { field = value ?? ""; Rehighlight(); } } = "";

    /// <summary>语言（ts、cs、xml……），交给高亮器；不写时不高亮。</summary>
    [Category("Data")]
    [Description("语言：ts、cs、xml、json、css、sql、bash……")]
    [DefaultValue(null)]
    public string? Language { get; set { field = value; Rehighlight(); } }

    /// <summary>高亮：false 时按纯文本显示，改成 true 才交给高亮器，默认 true。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Highlight { get; set { field = value; Rehighlight(); } } = true;

    /// <summary>高亮器；默认内置的 <see cref="CodeHighlighting.Builtin"/>，设成 null 时不高亮。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CodeHighlighter? Highlighter { get; set { field = value; Rehighlight(); } } = CodeHighlighting.Builtin;

    /// <summary>标题栏：通常是文件名；有标题时复制按钮放进标题栏。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Title { get; set { field = value; Relayout(); } }

    /// <summary>长行自动换行，而不是横向滚动；不写时取作用域的默认值（<see cref="CodeBlockDefaults"/>），再没有时 false。</summary>
    [Category("Behavior")]
    public bool Wrap { get => wrap ?? ScopeDefaults?.Wrap ?? false; set { wrap = value; Relayout(); } }

    /// <summary>显示复制按钮；不写时取作用域的默认值，再没有时 true。</summary>
    [Category("Behavior")]
    public bool Copyable { get => copyable ?? ScopeDefaults?.Copyable ?? true; set { copyable = value; Relayout(); } }

    /// <summary>显示行号；不写时取作用域的默认值，再没有时 false。</summary>
    [Category("Appearance")]
    public bool LineNumbers { get => lineNumbers ?? ScopeDefaults?.LineNumbers ?? false; set { lineNumbers = value; Relayout(); } }

    /// <summary>强调的行：<c>"2,4-5"</c>。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? HighlightLines { get; set { field = value; Relayout(); } }

    /// <summary>新增的行（diff）：codeInserted 底色。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? AddedLines { get; set { field = value; Relayout(); } }

    /// <summary>删除的行（diff）：codeDeleted 底色。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? RemovedLines { get; set { field = value; Relayout(); } }

    /// <summary>超过这么多行时折叠，底部的按钮展开；不写时取作用域的默认值，再没有时不折叠。</summary>
    [Category("Behavior")]
    public int? MaxLines { get => maxLines.Written ? maxLines.Value : ScopeDefaults?.MaxLines; set { maxLines = (true, value); Relayout(); } }

    /// <summary>嵌在已经有外框的容器里：去掉自己的边框、圆角和底色；不写时取作用域的默认值，再没有时 false。</summary>
    [Category("Appearance")]
    public bool Flush { get => flush ?? ScopeDefaults?.Flush ?? false; set { flush = value; Relayout(); } }

    bool? wrap, copyable, lineNumbers, flush;
    (bool Written, int? Value) maxLines;

    /// <summary>作用域合并的默认值（上游 components.CodeBlock.defaults）：没写的档位取它。</summary>
    CodeBlockDefaults? ScopeDefaults => ProviderControl.Of(this)?.ResolveDefaults<CodeBlockDefaults>();

    bool ShouldSerializeWrap() => wrap is not null;

    void ResetWrap() => wrap = null;

    bool ShouldSerializeCopyable() => copyable is not null;

    void ResetCopyable() => copyable = null;

    bool ShouldSerializeLineNumbers() => lineNumbers is not null;

    void ResetLineNumbers() => lineNumbers = null;

    bool ShouldSerializeMaxLines() => maxLines.Written;

    void ResetMaxLines() => maxLines = default;

    bool ShouldSerializeFlush() => flush is not null;

    void ResetFlush() => flush = null;

    /// <summary>所在作用域的默认值、主题变了：重新排版、重画。</summary>
    void IScopeAware.ScopeChanged() => Relayout();

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 换了位置可能换了作用域
        Relayout();
    }

    /// <summary>展开着（折叠的长代码）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsExpanded { get; set { field = value; Relayout(); } }

    /// <summary>代码的行数（结尾的换行不算新的一行）。</summary>
    [Browsable(false)]
    public int LineCount => CodeHighlighting.SplitLines(Code).Length;

    /// <summary>当前的高亮结果（测试用）；还没有或纯文本时为 null。</summary>
    internal IReadOnlyList<CodeToken>? Tokens { get; private set; }

    /// <summary>复制按钮、滚动区域、展开按钮（测试用）。</summary>
    internal (CopyButtonControl Copy, ScrollAreaControl Scroll, Control Expand) Parts => (copy, scroll, expand);

    /// <summary>展开按钮上的文字（测试用）。</summary>
    internal string ExpandText => IsExpanded ? ProviderControl.LangOf(this).CodeCollapse : ProviderControl.LangOf(this).CodeExpand;

    /// <summary>代码区最多显示几行（折叠时；不折叠时为 null，测试用）。</summary>
    internal int? VisibleLines => Collapsed ? MaxLines : null;

    bool Collapsible => MaxLines is { } max && LineCount > max;

    bool Collapsed => Collapsible && !IsExpanded;

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    ElementContext Context => ElementContext.Of(this);

    /// <summary>标题栏的高度：至少 0.85em 的控件高度，放得下复制按钮（加一道分隔线）；没有标题时 0。</summary>
    int HeaderHeight => Title is null ? 0
        : Math.Max((int)MathF.Ceiling(ControlHeight.Of(Context, 0.86f * Em, Tokens_.Control)), Copyable ? copy.GetPreferredSize(Size.Empty).Height : 0) + 1;

    int ExpandHeight => Collapsible ? (int)MathF.Ceiling(ControlHeight.Of(Context, TextSize.Sm.Pixels!.Value * DeviceDpi / 96f, Tokens_.Control)) + 1 : 0;

    int Border => Flush ? 0 : LogicalToDeviceUnits(1);

    /// <summary>
    /// 代码的字体：主题的等宽字体。代码字体的连字（Cascadia Code 把 => 画成 ⇒）会让照着抄的人看错，上游一律关掉；
    /// GDI 关不掉 OpenType 特性，装了 Cascadia Mono（同一套字形、不带连字）时换成它。
    /// </summary>
    string MonoFamily
    {
        get
        {
            var family = Fonts.Installed(ProviderControl.ThemeOf(this).Fonts.Mono);
            return family == "Cascadia Code" && Fonts.Installed(NoLigatures) == "Cascadia Mono" ? "Cascadia Mono" : family;
        }
    }

    static readonly string[] NoLigatures = ["Cascadia Mono"];

    /// <summary>代码、语言、高亮器变了：先回到纯文本，再交给高亮器（同一批变化合并成一次）；先发出的慢结果晚到时不覆盖新结果，高亮失败时保留纯文本。</summary>
    void Rehighlight()
    {
        ++highlights;
        Tokens = null;
        copy.Value = Code;
        Relayout();
        if (highlightQueued || !IsHandleCreated) return;
        highlightQueued = true;
        BeginInvoke(StartHighlight);
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!highlightQueued) Rehighlight();
    }

    async void StartHighlight()
    {
        highlightQueued = false;
        var version = highlights;
        if (Highlighter is not { } highlighter || Language is not { Length: > 0 } language || !Highlight) return;
        IReadOnlyList<CodeToken>? result;
        try
        {
            result = await highlighter(Code, language);
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"CodeBlock 高亮 {language} 代码失败，显示纯文本：{error}");
            return;
        }
        if (version != highlights || result is null || IsDisposed) return;
        Tokens = result;
        view.Invalidate();
    }

    void Relayout()
    {
        scroll.Axis = Wrap ? ScrollAreaAxis.Y : ScrollAreaAxis.X;
        copy.Visible = Copyable;
        expand.Visible = Collapsible;
        view.Reset();
        PerformLayout();
        Parent?.PerformLayout();
        Invalidate();
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : Width;
        var body = view.BodyHeight(Math.Max(1, width - 2 * Border));
        return new Size(width, 2 * Border + HeaderHeight + body + ExpandHeight);
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var inner = new Rectangle(Border, Border, Math.Max(0, Width - 2 * Border), Math.Max(0, Height - 2 * Border));
        var header = HeaderHeight;
        var footer = ExpandHeight;
        scroll.Bounds = new Rectangle(inner.X, inner.Y + header, inner.Width, Math.Max(0, inner.Height - header - footer));
        expand.Bounds = new Rectangle(inner.X, inner.Bottom - footer, inner.Width, footer);
        var size = copy.GetPreferredSize(Size.Empty);
        var gap = (int)MathF.Round(0.25f * Em);
        copy.Bounds = Title is null
            ? new Rectangle(inner.Right - size.Width - gap, inner.Y + header + gap, size.Width, size.Height)
            : new Rectangle(inner.Right - size.Width - (int)MathF.Round(0.25f * 0.86f * Em), inner.Y + (header - 1 - size.Height) / 2, size.Width, size.Height);
        copy.BringToFront();
    }

    /// <summary>外层（边框、圆角、surface 底色）与标题栏（surfaceMuted、下边一道分隔线、等宽 0.85em 的标题）。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var colors = ProviderControl.ThemeOf(this).Colors;
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var radius = Flush ? 0 : ProviderControl.ThemeOf(this).Radii.Md * Em;
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using var outline = Drawing.RoundedRectangle(bounds, radius);
        if (!Flush)
        {
            using var fill = new SolidBrush(colors.Surface);
            graphics.FillPath(fill, outline);
        }
        if (Title is not null)
        {
            var header = new Rectangle(Border, Border, Width - 2 * Border, HeaderHeight - 1);
            var state = graphics.Save();
            graphics.SetClip(outline);
            using (var fill = new SolidBrush(colors.SurfaceMuted)) graphics.FillRectangle(fill, header);
            using (var line = new Pen(colors.BorderMuted, LogicalToDeviceUnits(1))) graphics.DrawLine(line, header.Left, header.Bottom + 0.5f, header.Right, header.Bottom + 0.5f);
            graphics.Restore(state);
            var font = Fonts.Get(MonoFamily, 0.86f * Font.SizeInPoints, FontStyle.Regular);
            var left = (int)MathF.Round(0.86f * Em);
            var titleBounds = new Rectangle(header.X + left, header.Y, Math.Max(0, header.Width - left - (Copyable ? copy.Width + left : left)), header.Height);
            TextRenderer.DrawText(graphics, Title, font, titleBounds, colors.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
        if (Border > 0)
        {
            using var pen = new Pen(colors.BorderMuted, Border);
            graphics.DrawPath(pen, outline);
        }
    }

    /// <summary>控件高度的系数（tokens 的 sizes.control）。</summary>
    static class Tokens_
    {
        public const float Control = 2.25f;
    }

    /// <summary>代码区：等宽 0.9em、行高 1.5、上下 0.75em 左右 1em；按行的底色铺满整行，行号 codeComment；高亮段按 code* 着色。</summary>
    internal sealed class CodeView : Control
    {
        readonly CodeBlockControl owner;
        string[]? lines;

        public CodeView(CodeBlockControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            RightToLeft = RightToLeft.No;
        }

        public void Reset()
        {
            lines = null;
            Invalidate();
        }

        string[] Lines => lines ??= CodeHighlighting.SplitLines(owner.Code.Replace("\r\n", "\n"));

        Font CodeFont => Fonts.Get(owner.MonoFamily, 0.9f * owner.Font.SizeInPoints, FontStyle.Regular);

        float Em => 0.9f * owner.Em;

        int LineHeight => (int)MathF.Round(1.5f * Em);

        int PaddingX => (int)MathF.Round(Em);

        int PaddingY => (int)MathF.Round(0.75f * Em);

        /// <summary>有复制按钮、没有标题时右边让出按钮的位置（space.md + 控件高度）。</summary>
        int PaddingEnd => owner.Copyable && owner.Title is null ? (int)MathF.Round(0.75f * Em + ControlHeight.Of(owner.Context, Em, Tokens_.Control)) : PaddingX;

        int NumberWidth => owner.LineNumbers ? (int)MathF.Ceiling(MathF.Max(1.2f * Em, Measure(Lines.Length.ToString())) + Em) : 0;

        int Measure(string text) => text.Length == 0 ? 0 : TextRenderer.MeasureText(text, CodeFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;

        /// <summary>代码区要的高度（折叠时最多 MaxLines 行）。</summary>
        public int BodyHeight(int width)
        {
            var rows = owner.Wrap ? Rows(width).Count : Lines.Length;
            if (owner.Collapsed) rows = Math.Min(rows, owner.MaxLines!.Value);
            return rows * LineHeight + 2 * PaddingY;
        }

        /// <inheritdoc />
        public override Size GetPreferredSize(Size proposedSize)
        {
            var width = owner.Wrap ? Math.Max(1, proposedSize.Width) : PaddingX + NumberWidth + Lines.Select(Measure).DefaultIfEmpty(0).Max() + PaddingEnd;
            var rows = owner.Wrap ? Rows(width).Count : Lines.Length;
            return new Size(width, rows * LineHeight + 2 * PaddingY);
        }

        /// <summary>要画的行：不换行时一行一行；换行时按宽度在字符之间断开（等宽字体，按量出的宽度贪心地放）。</summary>
        List<(int Line, int Start, string Text)> Rows(int width)
        {
            var rows = new List<(int, int, string)>();
            var room = Math.Max(1, width - PaddingX - NumberWidth - PaddingEnd);
            for (var index = 0; index < Lines.Length; index++)
            {
                var line = Lines[index];
                if (!owner.Wrap || Measure(line) <= room)
                {
                    rows.Add((index, 0, line));
                    continue;
                }
                var start = 0;
                while (start < line.Length)
                {
                    var length = 1;
                    while (start + length < line.Length && Measure(line.Substring(start, length + 1)) <= room) length++;
                    rows.Add((index, start, line.Substring(start, length)));
                    start += length;
                }
            }
            return rows;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ProviderControl.ThemeOf(owner).Colors;
            var graphics = e.Graphics;
            var font = CodeFont;
            var marks = (Highlighted: CodeHighlighting.ParseLines(owner.HighlightLines), Added: CodeHighlighting.ParseLines(owner.AddedLines), Removed: CodeHighlighting.ParseLines(owner.RemovedLines));
            var offsets = new int[Lines.Length];
            for (int i = 1, sum = 0; i < Lines.Length; i++) offsets[i] = sum += Lines[i - 1].Length + 1;
            var y = PaddingY;
            var lineHeight = LineHeight;
            var previous = -1;
            foreach (var (line, start, text) in Rows(Width))
            {
                var number = line + 1;
                var background = marks.Added.Contains(number) ? colors.CodeInserted : marks.Removed.Contains(number) ? colors.CodeDeleted
                    : marks.Highlighted.Contains(number) ? colors.Fill : (Color?)null;
                if (background is { } fill)
                {
                    using var brush = new SolidBrush(fill);
                    graphics.FillRectangle(brush, 0, y, Width, lineHeight);
                }
                var x = PaddingX;
                if (owner.LineNumbers)
                {
                    if (line != previous)
                        TextRenderer.DrawText(graphics, number.ToString(), font, new Rectangle(x, y, NumberWidth - (int)Em, lineHeight), colors.CodeComment,
                            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    x += NumberWidth;
                }
                DrawSegments(graphics, font, text, offsets[line] + start, x, y, lineHeight, colors);
                previous = line;
                y += lineHeight;
            }
        }

        /// <summary>一行代码：高亮段按种类着色，其余按正文颜色；段的位置按整段代码算。</summary>
        void DrawSegments(Graphics graphics, Font font, string text, int offset, int x, int y, int height, ThemeColors colors)
        {
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            void Draw(string segment, Color color)
            {
                if (segment.Length == 0) return;
                TextRenderer.DrawText(graphics, segment, font, new Point(x, y + (height - font.Height) / 2), color, flags & ~TextFormatFlags.VerticalCenter);
                x += Measure(segment);
            }
            var position = 0;
            foreach (var token in owner.Tokens ?? [])
            {
                var start = token.Start - offset;
                var end = start + token.Length;
                if (end <= position || start >= text.Length) continue;
                start = Math.Max(start, position);
                end = Math.Min(end, text.Length);
                Draw(text[position..start], colors.Text);
                Draw(text[start..end], ColorOf(token.Kind, colors));
                position = end;
            }
            Draw(text[position..], colors.Text);
        }

        static Color ColorOf(CodeTokenKind kind, ThemeColors colors) => kind switch
        {
            CodeTokenKind.Comment or CodeTokenKind.Punctuation => colors.CodeComment,
            CodeTokenKind.Keyword => colors.CodeKeyword,
            CodeTokenKind.String => colors.CodeString,
            CodeTokenKind.Number => colors.CodeNumber,
            CodeTokenKind.Function => colors.CodeFunction,
            CodeTokenKind.Type => colors.CodeType,
            CodeTokenKind.Tag => colors.CodeTag,
            _ => colors.CodeAttribute,
        };
    }

    /// <summary>展开、收起长代码的按钮：surfaceMuted 底（悬停 fill）、上边一道分隔线、sm 字号的 textMuted 文字加箭头（展开后翻转）。</summary>
    sealed class ExpandBar : Control
    {
        readonly CodeBlockControl owner;
        bool hovered;

        public ExpandBar(CodeBlockControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            owner.IsExpanded = !owner.IsExpanded;
            AccessibleName = owner.ExpandText;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ProviderControl.ThemeOf(owner).Colors;
            var graphics = e.Graphics;
            graphics.Clear(hovered ? colors.Fill : colors.SurfaceMuted);
            using (var line = new Pen(colors.BorderMuted, LogicalToDeviceUnits(1))) graphics.DrawLine(line, 0, 0.5f, Width, 0.5f);
            var font = Fonts.Get(owner.Font.FontFamily.Name, TextSize.Sm.Pixels!.Value * 72 / 96, FontStyle.Regular);
            var text = owner.ExpandText;
            AccessibleName = text;
            var size = TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding);
            var icon = (int)MathF.Round(font.SizeInPoints * DeviceDpi / 72f);
            var gap = (int)MathF.Round(0.25f * icon);
            var x = (Width - size.Width - gap - icon) / 2;
            TextRenderer.DrawText(graphics, text, font, new Point(x, (Height - size.Height) / 2), colors.TextMuted, TextFormatFlags.NoPadding);
            var chevron = new Icon { Data = Lucide.ChevronDown, Bounds = new Rectangle(x + size.Width + gap, (Height - icon) / 2, icon, icon) };
            var state = graphics.Save();
            if (owner.IsExpanded)
            {
                // 展开后箭头翻转（朝上）
                graphics.TranslateTransform(chevron.Bounds.X + icon / 2f, chevron.Bounds.Y + icon / 2f);
                graphics.RotateTransform(180);
                graphics.TranslateTransform(-(chevron.Bounds.X + icon / 2f), -(chevron.Bounds.Y + icon / 2f));
            }
            chevron.Paint(graphics, ElementContext.Of(owner) with { Font = font, ForeColor = colors.TextMuted });
            graphics.Restore(state);
        }
    }
}

/// <summary>CodeBlock 在作用域里的默认值（上游 <c>components.CodeBlock.defaults</c>，CodeBlockOptions）：<c>scope.Defaults.Set(new CodeBlockDefaults { LineNumbers = true });</c></summary>
public sealed record CodeBlockDefaults : IScopeRecord<CodeBlockDefaults>
{
    /// <summary>长行自动换行。</summary>
    public bool? Wrap { get; init; }

    /// <summary>显示复制按钮。</summary>
    public bool? Copyable { get; init; }

    /// <summary>显示行号。</summary>
    public bool? LineNumbers { get; init; }

    /// <summary>超过这么多行时折叠。</summary>
    public int? MaxLines { get; init; }

    /// <summary>去掉自己的边框、圆角和底色。</summary>
    public bool? Flush { get; init; }

    /// <inheritdoc />
    public CodeBlockDefaults Merge(CodeBlockDefaults? over) => over is null ? this : new()
    {
        Wrap = over.Wrap ?? Wrap,
        Copyable = over.Copyable ?? Copyable,
        LineNumbers = over.LineNumbers ?? LineNumbers,
        MaxLines = over.MaxLines ?? MaxLines,
        Flush = over.Flush ?? Flush,
    };
}
