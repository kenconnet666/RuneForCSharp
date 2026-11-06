using System.Windows;
using System.Windows.Documents;

namespace Rune.Wpf;

/// <summary>
/// 行内代码（vue-tsx-pro 的 Code）：等宽字体、浅底，字号是外面的 0.9 倍。是一段行内文字（Span），跟着所在的段落换行：
/// <code>
/// &lt;rune:Paragraph&gt;输入 &lt;rune:Code&gt;button&lt;/rune:Code&gt; 就能找到&lt;/rune:Paragraph&gt;
/// </code>
/// 行内文字没有盒子：TS 的左右内边距与圆角画不出来（DESIGN §14），底色贴着文字。多行代码用 CodeBlock。
/// </summary>
public class Code : Span
{
    static Code()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Code), new FrameworkPropertyMetadata(typeof(Code)));
        // 字号 = 继承来的 × 0.9（EmSize 的相对字号；基础值变了时回调里再强制一次）
        FontSizeProperty.OverrideMetadata(typeof(Code), new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => d.CoerceValue(FontSizeProperty), (d, value) => EmSize.ScaleFontSize(d, value, 0.9)));
    }

    /// <summary>新建：先按默认字号算一次（字号一直是默认值时没有变化回调）。</summary>
    public Code() => CoerceValue(FontSizeProperty);
}

/// <summary>
/// 高亮标记（vue-tsx-pro 的 Mark）：搜索命中、正文里要强调的词。底色取主题的 highlight，文字颜色不变。
/// 是一段行内文字（Span），跟着所在的段落换行；TS 的左右 0.15em 内边距与圆角画不出来（同 <see cref="Code"/>）。
/// </summary>
public class Mark : Span
{
    static Mark() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Mark), new FrameworkPropertyMetadata(typeof(Mark)));
}
