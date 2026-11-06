using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>
/// Text 的示例（vue-tsx-pro 文档站 base/Text）。inline 要等 Kbd、Code、Mark、Blockquote，links 等 Link，headings 等 Heading。
/// </summary>
static class TextExamples
{
    public static Example[] All =>
    [
        new("Text", "scale", "字号与字重", "Size 对应主题的字号档位，Weight 对应字重；不写时继承外面，Small 默认 xs，Strong 默认 semibold。", () => new TextScale()),
        new("Text", "tones", "色调", "Tone 取主题的文字色，与 Button、Badge 的色调同名：neutral 是正文色，muted 用于次要说明，其余是品牌色与提示色的文字色。", () => new TextTones()),
        new("Text", "numeric", "编号与数字", "Mono 用等宽字体（编号、件号），Numeric 用等宽数字（金额、计数）：上下对齐、变化时不跳动。", () => new TextNumeric()),
        new("Text", "overflow", "截断与换行", "Truncate 单行省略，LineClamp 限制行数；Wrap 的 Pretty、Balance 是浏览器的排版优化，桌面上按普通换行。", () => new TextOverflow()),
    ];
}

/// <summary>Text/scale：字号与字重。</summary>
public partial class TextScale : UserControl
{
    /// <summary>加载示例。</summary>
    public TextScale() => InitializeComponent();
}

/// <summary>Text/tones：色调。</summary>
public partial class TextTones : UserControl
{
    /// <summary>加载示例。</summary>
    public TextTones() => InitializeComponent();
}

/// <summary>Text/numeric：编号与数字。</summary>
public partial class TextNumeric : UserControl
{
    /// <summary>加载示例。</summary>
    public TextNumeric() => InitializeComponent();
}

/// <summary>Text/overflow：截断与换行。</summary>
public partial class TextOverflow : UserControl
{
    /// <summary>一段长文字。</summary>
    public const string Long = "符文（rune）是编译期的标记：组件里写 count++，编译后变成对响应式引用的读写；模板里直接用 count，不需要 .value，也不会丢失响应性。";

    /// <summary>加载示例。</summary>
    public TextOverflow() => InitializeComponent();
}
