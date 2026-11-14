using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Rune.WinForms;

/// <summary>不再准确、不再有效的内容（vue-tsx-pro 的 Strike）：原价、过期的说明，只是划线；文档的增删用 <see cref="Del"/>、<see cref="Ins"/>。</summary>
public class Strike : Text
{
    /// <summary>新建：划线。</summary>
    public Strike() => Strikethrough = true;
}

/// <summary>文档里删掉的内容（vue-tsx-pro 的 Del）：划线、次要色。</summary>
public class Del : Text
{
    /// <summary>新建：划线、次要色。</summary>
    public Del()
    {
        Strikethrough = true;
        Tone = TextTone.Muted;
    }
}

/// <summary>
/// 文档里加进来的内容（vue-tsx-pro 的 Ins）：浅绿底（successSoft）、左右内边距 0.15em、圆角 radii.xs，不用下划线（和链接分不清）。
/// 同 <see cref="Mark"/>，是单独的元素，不跟着一段文字换行。
/// </summary>
public class Ins : Element
{
    readonly Text text = new() { Wrap = TextWrap.NoWrap };

    /// <summary>文字。</summary>
    public string Content { get => text.Content; set { text.Content = value; InvalidateMeasure(); } }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available) => ChipText.Measure(text, context, 0.15f);

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context) =>
        ChipText.Paint(graphics, text, context, Bounds, 0.15f, Radius.Xs, context.Theme.Colors.SuccessSoft);
}

/// <summary>
/// 上下标的共同部分：字号是宿主的 0.75 倍；高度按宿主字号的一行算（和旁边的文字一样高，不撑高所在的一行），
/// 上标贴着顶、下标贴着底。WinForms 的文字没有行内元素：是单独的元素，和 Code、Mark 一样放在一行里。
/// </summary>
public abstract class Script : Element
{
    readonly Text text = new() { Wrap = TextWrap.NoWrap };
    // 量宿主字号下一行有多高
    readonly Text line = new() { Content = "0", Wrap = TextWrap.NoWrap };

    /// <summary>文字。</summary>
    public string Content { get => text.Content; set { text.Content = value; InvalidateMeasure(); } }

    /// <summary>上标（贴着顶）还是下标（贴着底）。</summary>
    protected abstract bool Raised { get; }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var small = text.Measure(ChipText.Scaled(context, 0.75f), new Size(int.MaxValue, int.MaxValue));
        return new Size(small.Width, Math.Max(small.Height, line.Measure(context, new Size(int.MaxValue, int.MaxValue)).Height));
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var inner = ChipText.Scaled(context, 0.75f);
        var size = text.Measure(inner, new Size(int.MaxValue, int.MaxValue));
        text.Bounds = new Rectangle(Bounds.X, Raised ? Bounds.Y : Bounds.Bottom - size.Height, Bounds.Width, size.Height);
        text.Paint(graphics, inner);
    }
}

/// <summary>下标（vue-tsx-pro 的 Sub）：化学式、变量的下标。</summary>
public class Sub : Script
{
    /// <inheritdoc />
    protected override bool Raised => false;
}

/// <summary>上标（vue-tsx-pro 的 Sup）：单位的幂、脚注的编号。</summary>
public class Sup : Script
{
    /// <inheritdoc />
    protected override bool Raised => true;
}

/// <summary>划掉的内容（<see cref="Rune.WinForms.Strike"/>）。</summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Text))]
[Description("不再有效的内容（原价）：划线。")]
public class StrikeControl : TextControlBase<Strike>;

/// <summary>删掉的内容（<see cref="Rune.WinForms.Del"/>）。</summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Text))]
[Description("文档里删掉的内容：划线、次要色。")]
public class DelControl : TextControlBase<Del>;

/// <summary>放进窗体的行内文字元素（Ins、Sub、Sup）：不能获得焦点，读屏当作静态文字，Text 交给元素。</summary>
/// <typeparam name="T">元素。</typeparam>
public abstract class InlineTextControl<T> : ElementControl<T> where T : Element, new()
{
    /// <summary>新建：不能获得焦点，读屏当作静态文字。</summary>
    protected InlineTextControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.StaticText;
    }

    /// <summary>文字。</summary>
    [Category("Appearance")]
    [AllowNull]
    public override string Text { get => base.Text; set => base.Text = value; }

    /// <summary>把文字交给元素。</summary>
    protected abstract void SetContent(T element, string text);

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        SetContent(Element, Text);
    }
}

/// <summary>加进来的内容（<see cref="Rune.WinForms.Ins"/>）。</summary>
[ToolboxItem(true)]
[Description("文档里加进来的内容：浅绿底。")]
public class InsControl : InlineTextControl<Ins>
{
    /// <inheritdoc />
    protected override void SetContent(Ins element, string text) => element.Content = text;
}

/// <summary>下标（<see cref="Rune.WinForms.Sub"/>）。</summary>
[ToolboxItem(true)]
[Description("下标：化学式、变量的下标。")]
public class SubControl : InlineTextControl<Sub>
{
    /// <inheritdoc />
    protected override void SetContent(Sub element, string text) => element.Content = text;
}

/// <summary>上标（<see cref="Rune.WinForms.Sup"/>）。</summary>
[ToolboxItem(true)]
[Description("上标：单位的幂、脚注的编号。")]
public class SupControl : InlineTextControl<Sup>
{
    /// <inheritdoc />
    protected override void SetContent(Sup element, string text) => element.Content = text;
}
