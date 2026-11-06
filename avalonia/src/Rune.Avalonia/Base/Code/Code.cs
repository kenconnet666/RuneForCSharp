using global::Avalonia;
using global::Avalonia.Controls.Documents;
using global::Avalonia.LogicalTree;

namespace Rune.Avalonia;

/// <summary>
/// 行内代码（vue-tsx-pro 的 Code）：等宽字体、浅底，字号是外面的 0.9 倍。是一段行内文字（Span），跟着所在的段落换行：
/// <code>
/// &lt;rune:Paragraph&gt;输入 &lt;rune:Code&gt;button&lt;/rune:Code&gt; 就能找到&lt;/rune:Paragraph&gt;
/// </code>
/// 行内文字没有盒子：TS 的左右内边距与圆角画不出来（DESIGN §14），底色贴着文字。多行代码用 CodeBlock。
/// </summary>
public class Code : Span
{
    const double Scale = 0.9;

    IDisposable? fontSize;

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (((ILogical)this).LogicalParent is AvaloniaObject parent) fontSize = FontScale.Bind(this, parent, Scale);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        fontSize?.Dispose();
        fontSize = null;
    }
}

/// <summary>
/// 高亮标记（vue-tsx-pro 的 Mark）：搜索命中、正文里要强调的词。底色取主题的 highlight，文字颜色不变。
/// 是一段行内文字（Span），跟着所在的段落换行；TS 的左右 0.15em 内边距与圆角画不出来（同 <see cref="Code"/>）。
/// </summary>
public class Mark : Span;
