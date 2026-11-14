using global::Avalonia;
using global::Avalonia.Controls.Documents;
using global::Avalonia.LogicalTree;

namespace Rune.Avalonia;

/// <summary>
/// 上下标的共同部分：一段文字（Run），字号是外面的 0.75 倍，基线挪到上标、下标的位置。是 Run 而不是 Span：
/// 平台的基线对齐只看文字段自己的，不从外层 Span 继承。
/// </summary>
public abstract class ScriptRun : Run
{
    IDisposable? fontSize;

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (((ILogical)this).LogicalParent is AvaloniaObject parent) fontSize = FontScale.Bind(this, parent, 0.75);
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
/// 下标（vue-tsx-pro 的 Sub）：化学式、变量的下标。字号 0.75 倍、落到行的底部；是一段行内文字，放在 Paragraph、Text 的内容里：
/// <code>&lt;rune:Paragraph&gt;H&lt;rune:Sub&gt;2&lt;/rune:Sub&gt;O&lt;/rune:Paragraph&gt;</code>
/// </summary>
public class Sub : ScriptRun;

/// <summary>上标（vue-tsx-pro 的 Sup）：单位的幂、脚注的编号。字号 0.75 倍、贴着行的顶部；是一段行内文字。</summary>
public class Sup : ScriptRun;

/// <summary>文档里删掉的内容（vue-tsx-pro 的 Del）：划线、次要色；是一段行内文字。不再有效的内容（原价）用 <see cref="Strike"/>。</summary>
public class Del : Span;

/// <summary>
/// 文档里加进来的内容（vue-tsx-pro 的 Ins）：浅绿底（successSoft），不用下划线（和链接分不清）；是一段行内文字。
/// 行内文字没有盒子：TS 的左右 0.15em 内边距与圆角画不出来（同 <see cref="Mark"/>）。
/// </summary>
public class Ins : Span;

/// <summary>不再准确、不再有效的内容（vue-tsx-pro 的 Strike）：原价、过期的说明，只是划线；文档的增删用 <see cref="Del"/>、<see cref="Ins"/>。</summary>
public class Strike : Span;
