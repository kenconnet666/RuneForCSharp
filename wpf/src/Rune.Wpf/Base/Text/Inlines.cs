using System.Windows;
using System.Windows.Documents;

namespace Rune.Wpf;

/// <summary>
/// 上下标的共同部分：一段文字（Run），字号是外面的 0.75 倍；上标贴着文字的顶、下标贴着文字的底（BaselineAlignment 的 TextTop、TextBottom）。
/// 平台的 Superscript、Subscript 按字体自己的上下标位置再挪一次，叠上缩小的字号后抬得太高、行也撑高了，不用它们。
/// 是 Run 而不是 Span：基线对齐只看文字段自己的，不从外层 Span 继承。
/// </summary>
public abstract class ScriptRun : Run
{
    static ScriptRun() =>
        // 字号 = 继承来的 × 0.75（同 Code 的 0.9；基础值变了时回调里再强制一次）
        FontSizeProperty.OverrideMetadata(typeof(ScriptRun), new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => d.CoerceValue(FontSizeProperty), (d, value) => EmSize.ScaleFontSize(d, value, 0.75)));

    /// <summary>新建：先按默认字号算一次（字号一直是默认值时没有变化回调）。</summary>
    protected ScriptRun() => CoerceValue(FontSizeProperty);
}

/// <summary>
/// 下标（vue-tsx-pro 的 Sub）：化学式、变量的下标。字号 0.75 倍、贴着文字的底；是一段行内文字，放在 Paragraph、Text 的内容里：
/// <code>&lt;rune:Paragraph&gt;H&lt;rune:Sub&gt;2&lt;/rune:Sub&gt;O&lt;/rune:Paragraph&gt;</code>
/// </summary>
public class Sub : ScriptRun
{
    static Sub() => BaselineAlignmentProperty.OverrideMetadata(typeof(Sub), new FrameworkPropertyMetadata(BaselineAlignment.TextBottom));
}

/// <summary>上标（vue-tsx-pro 的 Sup）：单位的幂、脚注的编号。字号 0.75 倍、贴着文字的顶；是一段行内文字。</summary>
public class Sup : ScriptRun
{
    static Sup() => BaselineAlignmentProperty.OverrideMetadata(typeof(Sup), new FrameworkPropertyMetadata(BaselineAlignment.TextTop));
}

/// <summary>文档里删掉的内容（vue-tsx-pro 的 Del）：划线、次要色；是一段行内文字。不再有效的内容（原价）用 <see cref="Strike"/>。</summary>
public class Del : Span
{
    static Del() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Del), new FrameworkPropertyMetadata(typeof(Del)));
}

/// <summary>
/// 文档里加进来的内容（vue-tsx-pro 的 Ins）：浅绿底（successSoft），不用下划线（和链接分不清）；是一段行内文字。
/// 行内文字没有盒子：TS 的左右 0.15em 内边距与圆角画不出来（同 <see cref="Mark"/>）。
/// </summary>
public class Ins : Span
{
    static Ins() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Ins), new FrameworkPropertyMetadata(typeof(Ins)));
}

/// <summary>不再准确、不再有效的内容（vue-tsx-pro 的 Strike）：原价、过期的说明，只是划线；文档的增删用 <see cref="Del"/>、<see cref="Ins"/>。</summary>
public class Strike : Span
{
    static Strike() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Strike), new FrameworkPropertyMetadata(typeof(Strike)));
}
