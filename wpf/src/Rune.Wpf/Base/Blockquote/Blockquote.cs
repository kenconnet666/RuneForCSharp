using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf;

/// <summary>
/// 引用（vue-tsx-pro 的 Blockquote）：起始边一条竖线（从右到左时在右边），次要文字色。提示、警告用 Alert，不用引用。
/// <code>
/// &lt;rune:Blockquote Source="Vue 文档"&gt;&lt;rune:Paragraph&gt;渐进式框架……&lt;/rune:Paragraph&gt;&lt;/rune:Blockquote&gt;
/// </code>
/// <see cref="Source"/> 是给人看的出处（作者、书名、来源的名字）：引用下面一行，前面带破折号，小一号、不用斜体；要链接时放 Link。
/// 竖线 borderWidths.md、颜色 borderStrong；上下内边距 space.xs、起始边 space.lg（em）。
/// </summary>
public class Blockquote : ContentControl
{
    static Blockquote()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Blockquote), new FrameworkPropertyMetadata(typeof(Blockquote)));
        FocusableProperty.OverrideMetadata(typeof(Blockquote), new FrameworkPropertyMetadata(false));
        EmSize.Register(typeof(Blockquote));
    }

    /// <summary>出处：文字或控件（Link）；不写时没有这一行。</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object), typeof(Blockquote));

    /// <summary>出处。</summary>
    public object? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
}

/// <summary>Blockquote 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:BlockquoteParts.Source}" TargetType="StackPanel"&gt;</c>。</summary>
public static class BlockquoteParts
{
    /// <summary>出处那一行（TS 的 propsSource）：StackPanel，里面是破折号与出处。</summary>
    public static readonly PartKey Source = new(typeof(Blockquote), nameof(Source), typeof(StackPanel));
}
