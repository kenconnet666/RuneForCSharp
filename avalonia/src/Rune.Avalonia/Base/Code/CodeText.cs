using global::Avalonia;
using global::Avalonia.Controls.Documents;

namespace Rune.Avalonia;

/// <summary>
/// 带行内代码的一段文字（vue-tsx-pro 的 CodeText）：反引号包起来的部分渲染成 <see cref="Code"/>，文案写在数据里时不用拆成多个节点。
/// <code>
/// &lt;rune:CodeText Source="输入 `button` 就能找到" /&gt;
/// </code>
/// 是一个 <see cref="Text"/>（字号、色调、截断照常），内容由 <see cref="Source"/> 生成。TS 的 text 属性在这里叫 Source（Text 是 TextBlock 自己的）。
/// 行内代码是部件 <see cref="CodeTextParts.Code"/>：样式写 <c>rune|CodeText rune|Code#PART_Code</c>（TS 的 propsCode）。
/// </summary>
public class CodeText : Text
{
    /// <summary>文字：反引号里的是行内代码。</summary>
    public static readonly StyledProperty<string?> SourceProperty = AvaloniaProperty.Register<CodeText, string?>(nameof(Source));

    /// <summary>文字：反引号里的是行内代码。</summary>
    public string? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != SourceProperty) return;
        var inlines = new InlineCollection();
        foreach (var (part, index) in (Source ?? "").Split('`').Select((part, index) => (part, index)))
        {
            if (part.Length == 0) continue;
            inlines.Add(index % 2 == 1 ? new Code { Name = CodeTextParts.Code, Inlines = { new Run(part) } } : new Run(part));
        }
        Inlines = inlines;
    }
}

/// <summary>CodeText 的部件名。</summary>
public static class CodeTextParts
{
    /// <summary>反引号里的行内代码（TS 的 propsCode），每一段都叫这个名字。</summary>
    public const string Code = "PART_Code";
}
