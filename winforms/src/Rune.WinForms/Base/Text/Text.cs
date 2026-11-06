namespace Rune.WinForms;

/// <summary>
/// 正文文字（vue-tsx-pro 的 Text）：字号、字重、色调、对齐、截断。无窗口元素，放进窗体用 <see cref="TextControl"/>。
/// <see cref="Paragraph"/>、<see cref="Strong"/>、<see cref="Em"/>、<see cref="Small"/> 是它的子类，各有自己的默认值：
/// 档位不写（default）时取组件的默认值，组件也没有时继承宿主的字体与颜色。
/// <code>
/// new TextControl { Text = "更新于 3 分钟前", Tone = Tone.Muted, Size = TextSize.Sm };
/// new ParagraphControl { Text = "很长的描述…", LineClamp = 2, MaximumSize = new Size(320, 0) };
/// </code>
/// GDI 的字体只有常规与粗体：semibold、bold 画成粗体，light、medium 画成常规。数字等宽（Numeric）不用另外处理：
/// 界面字体（Segoe UI、微软雅黑）的数字默认就是等宽的。
/// </summary>
public class Text : Element
{
    /// <summary>上一次量的输入与结果（字体是共用的实例，按引用比）。</summary>
    (string Content, Font Font, int Width, TextFormatFlags Flags, Size Size)? measured;

    /// <summary>文字内容（TS 的 children）。</summary>
    public string Content
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateMeasure();
        }
    } = "";

    /// <summary>字号：档位（xs–xl，取主题字号）或像素数；不写时取组件的默认值，再没有就跟随宿主的字体。</summary>
    public TextSize Size
    {
        get => field.Value is null ? DefaultSize : field;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>字重档位；不写时取组件的默认值，再没有就跟随宿主的字体。</summary>
    public TextWeight Weight
    {
        get => field.Value is null ? DefaultWeight : field;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>色调；不写时跟随宿主的文字颜色。</summary>
    public TextTone Tone
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Invalidate();
        }
    }

    /// <summary>水平对齐（按书写方向）；不写时同起始边。</summary>
    public TextAlign? Align
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Invalidate();
        }
    }

    /// <summary>单行省略。</summary>
    public bool Truncate
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>最多显示几行，超出省略。</summary>
    public int? LineClamp
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>换行方式；不写时取组件的默认值，再没有就换行。</summary>
    public TextWrap? Wrap
    {
        get => field ?? DefaultWrap;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>数字等宽（金额、计数）。界面字体的数字默认等宽，GDI 也没有 OpenType 特性可开，这里只记下意图。</summary>
    public bool Numeric { get; set; }

    /// <summary>等宽字体（主题的 fonts.mono）：编号、件号、代码片段。</summary>
    public bool Mono
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>组件默认的字号（Small 是 xs）。</summary>
    protected internal virtual TextSize DefaultSize => default;

    /// <summary>组件默认的字重（Strong 是 semibold）。</summary>
    protected internal virtual TextWeight DefaultWeight => default;

    /// <summary>组件默认的换行（Paragraph 是 pretty）。</summary>
    protected internal virtual TextWrap? DefaultWrap => null;

    /// <summary>组件默认是否斜体（Em）。</summary>
    protected virtual bool Italic => false;

    /// <summary>用主题的标题字体（fonts.heading，H1–H6）。</summary>
    protected virtual bool HeadingFont => false;

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var font = ResolveFont(context);
        var lineHeight = Fonts.LineHeight(font);
        if (Content.Length == 0) return new Size(0, lineHeight);
        var flags = Flags(context);
        // 同样的输入不重量：按钮这类在测量与绘制时各量一次，换主题重画时又量一次
        if (measured is { } last && last.Content == Content && last.Font == font && last.Width == available.Width && last.Flags == flags) return Clamp(last.Size);
        var size = TextRenderer.MeasureText(Content, font, new Size(available.Width, int.MaxValue), flags);
        measured = (Content, font, available.Width, flags, size);
        return Clamp(size);

        Size Clamp(Size size)
        {
            if (LineClamp is { } lines) size.Height = Math.Min(size.Height, lines * lineHeight);
            if (Truncate) size.Width = Math.Min(size.Width, available.Width);
            return size;
        }
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context) =>
        TextRenderer.DrawText(graphics, Content, ResolveFont(context), Bounds, Tone.ColorIn(context), Flags(context));

    /// <summary>宿主的字体按字号、字重、等宽、斜体改过之后的字体（共用，不要释放）。</summary>
    Font ResolveFont(ElementContext context)
    {
        var host = context.Font;
        var points = Size.Pixels is { } pixels ? pixels * 72 / 96 : host.SizeInPoints;
        var style = host.Style;
        if (Weight.Number is { } weight) style = weight >= Tokens.FontWeight.Semibold ? style | FontStyle.Bold : style & ~FontStyle.Bold;
        if (Italic) style |= FontStyle.Italic;
        // 只有写了 Mono（或是标题）才读主题：没写的文字换主题时不重画
        var family = Mono ? Fonts.Installed(context.Theme.Fonts.Mono) : HeadingFont ? Fonts.Installed(context.Theme.Fonts.Heading) : host.FontFamily.Name;
        return family == host.FontFamily.Name && points == host.SizeInPoints && style == host.Style ? host : Fonts.Get(family, points, style);
    }

    TextFormatFlags Flags(ElementContext context)
    {
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        if (context.RightToLeft) flags |= TextFormatFlags.RightToLeft;
        flags |= (Align, context.RightToLeft) switch
        {
            (TextAlign.Center, _) => TextFormatFlags.HorizontalCenter,
            (TextAlign.End, false) or (TextAlign.Start or null, true) => TextFormatFlags.Right,
            _ => TextFormatFlags.Left,
        };
        // 多行省略：TextBoxControl 让最后一行放不下时不画半行、在可见的最后一行加省略号
        if (Truncate) return flags | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        if (LineClamp is not null) return flags | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.EndEllipsis;
        return Wrap == TextWrap.NoWrap ? flags | TextFormatFlags.SingleLine : flags | TextFormatFlags.WordBreak;
    }
}

/// <summary>段落（vue-tsx-pro 的 Paragraph）：默认换行（TS 的 pretty）。</summary>
public class Paragraph : Text
{
    /// <inheritdoc />
    protected internal override TextWrap? DefaultWrap => TextWrap.Pretty;
}

/// <summary>重要的内容（vue-tsx-pro 的 Strong）：默认字重 semibold。</summary>
public class Strong : Text
{
    /// <inheritdoc />
    protected internal override TextWeight DefaultWeight => TextWeight.Semibold;
}

/// <summary>语气上的强调（vue-tsx-pro 的 Em）：斜体。</summary>
public class Em : Text
{
    /// <inheritdoc />
    protected override bool Italic => true;
}

/// <summary>附注、版权、细则（vue-tsx-pro 的 Small）：默认字号 xs。</summary>
public class Small : Text
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Xs;
}

/// <summary>
/// 标题（vue-tsx-pro 的 H1–H6）：默认字号按级别（H1 xxxl、H2 xxl、H3 xl、H4 lg、H5 md、H6 sm），字重 semibold，标题字体。
/// GDI 画不出 TS 的 tight 行高与收紧的字间距；读屏没有标题级别（MSAA 没有这个概念）。
/// </summary>
public abstract class Heading : Text
{
    /// <inheritdoc />
    protected internal override TextWeight DefaultWeight => TextWeight.Semibold;

    /// <inheritdoc />
    protected override bool HeadingFont => true;
}

/// <summary>一级标题：页面标题，默认 xxxl。</summary>
public class H1 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Xxxl;
}

/// <summary>二级标题：一节，默认 xxl。</summary>
public class H2 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Xxl;
}

/// <summary>三级标题：小节，默认 xl。</summary>
public class H3 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Xl;
}

/// <summary>四级标题：段落标题，默认 lg。</summary>
public class H4 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Lg;
}

/// <summary>五级标题：卡片标题，默认 md。</summary>
public class H5 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Md;
}

/// <summary>六级标题：最小的标题，默认 sm。</summary>
public class H6 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Sm;
}
