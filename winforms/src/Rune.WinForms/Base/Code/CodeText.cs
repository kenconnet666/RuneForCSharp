using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 带行内代码的一段文字（vue-tsx-pro 的 CodeText）：反引号包起来的部分画成 <see cref="Code"/>，文案写在数据里时不用拆成多个控件。
/// 无窗口元素，放进窗体用 <see cref="CodeTextControl"/>。各段排成一行（WinForms 没有行内元素，不跨行换行）。
/// </summary>
public class CodeText : Element
{
    List<Element> parts = [];

    /// <summary>文字：反引号里的是行内代码。</summary>
    public string Source
    {
        get;
        set
        {
            field = value;
            parts = [.. value.Split('`').Select((part, index) => (part, index)).Where(p => p.part.Length > 0)
                .Select(p => p.index % 2 == 1 ? (Element)new Code { Content = p.part } : new Text { Content = p.part, Wrap = TextWrap.NoWrap })];
            InvalidateMeasure();
        }
    } = "";

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var sizes = parts.Select(part => part.Measure(context, new Size(int.MaxValue, int.MaxValue))).ToList();
        return new Size(sizes.Sum(size => size.Width), sizes.Count == 0 ? Fonts.LineHeight(context.Font) : sizes.Max(size => size.Height));
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var x = Bounds.X;
        foreach (var part in parts)
        {
            var size = part.Measure(context, new Size(int.MaxValue, int.MaxValue));
            part.Bounds = new Rectangle(x, Bounds.Y + (Bounds.Height - size.Height) / 2, size.Width, size.Height);
            part.Paint(graphics, context);
            x += size.Width;
        }
    }
}

/// <summary>放进窗体的带行内代码的文字（单元素宿主）。</summary>
public class CodeTextControl : ElementControl<CodeText>
{
    /// <summary>新建：不能获得焦点，读屏当作静态文字（名称是去掉反引号的文字）。</summary>
    public CodeTextControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.StaticText;
    }

    /// <summary>文字：反引号里的是行内代码。</summary>
    [Category("Appearance")]
    [Description("文字：反引号里的是行内代码。")]
    [DefaultValue("")]
    public string Source
    {
        get => Element.Source;
        set
        {
            Element.Source = value;
            AccessibleName = value.Replace("`", "");
        }
    }
}
