using global::Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 引用（vue-tsx-pro 的 Blockquote）：起始边一条竖线（从右到左时在右边），次要文字色。提示、警告用 Alert，不用引用。
/// <code>
/// &lt;rune:Blockquote&gt;
///   &lt;StackPanel&gt;&lt;rune:Paragraph&gt;渐进式框架……&lt;/rune:Paragraph&gt;&lt;rune:Small Tone="muted"&gt;—— Vue 文档&lt;/rune:Small&gt;&lt;/StackPanel&gt;
/// &lt;/rune:Blockquote&gt;
/// </code>
/// 竖线 borderWidths.md、颜色 borderStrong；上下内边距 space.xs、起始边 space.lg（em）。
/// </summary>
public class Blockquote : ContentControl;
