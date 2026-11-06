using System.Windows;
using System.Windows.Documents;

namespace Rune.Wpf;

/// <summary>
/// 带行内代码的一段文字（vue-tsx-pro 的 CodeText）：反引号包起来的部分渲染成 <see cref="Code"/>，文案写在数据里时不用拆成多个节点。
/// <code>
/// &lt;rune:CodeText Source="输入 `button` 就能找到" /&gt;
/// </code>
/// 是一个 <see cref="Text"/>（字号、色调、截断照常），内容由 <see cref="Source"/> 生成。TS 的 text 属性在这里叫 Source（Text 是 TextBlock 自己的）。
/// 行内代码引用部件样式键 <see cref="CodeTextParts.Code"/>（TS 的 propsCode）。
/// </summary>
public class CodeText : Text
{
    /// <summary>文字：反引号里的是行内代码。</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(CodeText), new FrameworkPropertyMetadata(null, (d, _) => ((CodeText)d).Rebuild()));

    static CodeText() => DefaultStyleKeyProperty.OverrideMetadata(typeof(CodeText), new FrameworkPropertyMetadata(typeof(Text)));

    /// <summary>文字：反引号里的是行内代码。</summary>
    public string? Source { get => (string?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    void Rebuild()
    {
        Inlines.Clear();
        foreach (var (part, index) in (Source ?? "").Split('`').Select((part, index) => (part, index)))
        {
            if (part.Length == 0) continue;
            if (index % 2 == 0)
            {
                Inlines.Add(new Run(part));
                continue;
            }
            var code = new Code { Inlines = { new Run(part) } };
            code.SetResourceReference(StyleProperty, CodeTextParts.Code);
            Inlines.Add(code);
        }
    }
}

/// <summary>CodeText 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:CodeTextParts.Code}" TargetType="rune:Code"&gt;</c>。</summary>
public static class CodeTextParts
{
    /// <summary>反引号里的行内代码（TS 的 propsCode）。</summary>
    public static readonly PartKey Code = new(typeof(CodeText), nameof(Code), typeof(Code));
}
