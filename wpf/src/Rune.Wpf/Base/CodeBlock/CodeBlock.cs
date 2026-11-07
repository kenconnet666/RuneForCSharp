using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 代码块（vue-tsx-pro 的 CodeBlock）：等宽字体的代码，标题栏、复制、行号、强调与增删的行、长代码折叠。
/// <code>
/// &lt;rune:CodeBlock Code="{Binding Source}" CodeLanguage="cs" /&gt;
/// &lt;rune:CodeBlock Code="…" CodeLanguage="ts" Title="main.ts" LineNumbers="True" HighlightLines="2,4-5" MaxLines="20" /&gt;
/// </code>
/// <list type="bullet">
/// <item>高亮：<see cref="Highlighter"/>（默认 <see cref="CodeHighlighting.Builtin"/>）。异步：先显示纯文本，结果到了再替换；同一批属性变化只取一次；
/// 没给语言、语言不认识、<see cref="Highlight"/> 为 false、高亮失败时按纯文本显示。颜色取主题的 code* token。</item>
/// <item>长行在 <see cref="ScrollArea"/> 里横向滚动（<see cref="Wrap"/> 时换行）；复制按钮在右上角，有标题时放进标题栏。</item>
/// <item>按行显示（有行号、强调或增删的行时）：每行一层，底色铺满整行；行号对读屏隐藏。</item>
/// <item>超过 <see cref="MaxLines"/> 行时折叠，底部的按钮展开或收起。<see cref="Flush"/> 去掉自己的边框、圆角和底色。代码总是从左到右排。</item>
/// </list>
/// 部件引用部件样式键（<see cref="CodeBlockParts"/>），Provider 的部件样式能改；em 尺寸在代码里按各层的字号算。
/// </summary>
public class CodeBlock : FrameworkElement
{
    /// <summary>代码文本。</summary>
    public static readonly DependencyProperty CodeProperty = Register(nameof(Code), "", rehighlight: true);

    /// <summary>代码的语言（ts、cs、xml……），交给高亮器；不写时不高亮（FrameworkElement 自己有 Language：界面文字的语言）。</summary>
    public static readonly DependencyProperty CodeLanguageProperty = Register<string?>(nameof(CodeLanguage), null, rehighlight: true);

    /// <summary>高亮：false 时按纯文本显示，改成 true 才交给高亮器，默认 true。</summary>
    public static readonly DependencyProperty HighlightProperty = Register(nameof(Highlight), true, rehighlight: true);

    /// <summary>标题栏：通常是文件名；有标题时复制按钮放进标题栏。</summary>
    public static readonly DependencyProperty TitleProperty = Register<object?>(nameof(Title), null);

    /// <summary>长行自动换行，而不是横向滚动。</summary>
    public static readonly DependencyProperty WrapProperty = Register(nameof(Wrap), false);

    /// <summary>显示复制按钮，默认 true。</summary>
    public static readonly DependencyProperty CopyableProperty = Register(nameof(Copyable), true);

    /// <summary>显示行号。</summary>
    public static readonly DependencyProperty LineNumbersProperty = Register(nameof(LineNumbers), false);

    /// <summary>强调的行：<c>"2,4-5"</c>。</summary>
    public static readonly DependencyProperty HighlightLinesProperty = Register<string?>(nameof(HighlightLines), null);

    /// <summary>新增的行（diff）：codeInserted 底色。</summary>
    public static readonly DependencyProperty AddedLinesProperty = Register<string?>(nameof(AddedLines), null);

    /// <summary>删除的行（diff）：codeDeleted 底色。</summary>
    public static readonly DependencyProperty RemovedLinesProperty = Register<string?>(nameof(RemovedLines), null);

    /// <summary>超过这么多行时折叠，底部的按钮展开；不写时不折叠。</summary>
    public static readonly DependencyProperty MaxLinesProperty = Register<int?>(nameof(MaxLines), null);

    /// <summary>嵌在已经有外框的容器里：去掉自己的边框、圆角和底色。</summary>
    public static readonly DependencyProperty FlushProperty = Register(nameof(Flush), false);

    /// <summary>高亮器；默认内置的 <see cref="CodeHighlighting.Builtin"/>，设成 null 时不高亮。</summary>
    public static readonly DependencyProperty HighlighterProperty = DependencyProperty.Register(nameof(Highlighter), typeof(CodeHighlighter), typeof(CodeBlock),
        new FrameworkPropertyMetadata(CodeHighlighting.Builtin, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((CodeBlock)d).Rehighlight()));

    /// <summary>展开着（折叠的长代码）。</summary>
    public static readonly DependencyProperty IsExpandedProperty = Register(nameof(IsExpanded), false);

    readonly Border root = new() { ClipToBounds = true };
    readonly DockPanel layout = new();
    readonly Border header = new();
    readonly DockPanel headerRow = new();
    readonly ContentPresenter title = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly System.Windows.Controls.Grid body = new();
    readonly ScrollArea scroll = new() { Axis = ScrollAreaAxis.X };
    readonly Border pre = new() { FlowDirection = FlowDirection.LeftToRight };
    readonly CopyButton copy = new() { Size = TextSize.Sm, Tone = StatusTone.Neutral, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    readonly ButtonBase expand = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBlock expandText = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly Icon expandIcon = new() { Data = Lucide.ChevronDown, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), RenderTransformOrigin = new Point(0.5, 0.5) };
    IReadOnlyList<CodeToken>? tokens;
    int highlights;
    bool highlightQueued;
    bool byLine;

    static CodeBlock() => TextElement.FontSizeProperty.OverrideMetadata(typeof(CodeBlock),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((CodeBlock)d).ApplyMetrics()));

    /// <summary>新建：组装标题栏、代码区与展开按钮。</summary>
    public CodeBlock()
    {
        root.SetResourceReference(StyleProperty, CodeBlockParts.Root);
        header.SetResourceReference(StyleProperty, CodeBlockParts.Header);
        title.SetResourceReference(StyleProperty, CodeBlockParts.Title);
        pre.SetResourceReference(StyleProperty, CodeBlockParts.Pre);
        // 代码字体的连字（Cascadia Code 把 => 画成 ⇒）会让照着抄的人看错，一律关掉
        Typography.SetStandardLigatures(pre, false);
        Typography.SetContextualAlternates(pre, false);
        body.SetResourceReference(StyleProperty, CodeBlockParts.Body);
        scroll.SetResourceReference(StyleProperty, CodeBlockParts.Scroll);
        expand.SetResourceReference(StyleProperty, CodeBlockParts.Expand);
        expandIcon.SetResourceReference(StyleProperty, CodeBlockParts.ExpandIcon);
        headerRow.Children.Add(title);
        header.Child = headerRow;
        scroll.Content = pre;
        body.Children.Add(scroll);
        expand.Content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Children = { expandText, expandIcon } };
        expand.Click += (_, _) => IsExpanded = !IsExpanded;
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(expand, Dock.Bottom);
        layout.Children.Add(header);
        layout.Children.Add(expand);
        layout.Children.Add(body);
        root.Child = layout;
        AddVisualChild(root);
        AddLogicalChild(root);
        Loaded += (_, _) => ApplyMetrics();
        Rebuild();
    }

    /// <summary>代码文本。</summary>
    public string Code { get => (string)GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    /// <summary>代码的语言。</summary>
    public string? CodeLanguage { get => (string?)GetValue(CodeLanguageProperty); set => SetValue(CodeLanguageProperty, value); }

    /// <summary>高亮。</summary>
    public bool Highlight { get => (bool)GetValue(HighlightProperty); set => SetValue(HighlightProperty, value); }

    /// <summary>标题栏。</summary>
    public object? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>长行自动换行。</summary>
    public bool Wrap { get => (bool)GetValue(WrapProperty); set => SetValue(WrapProperty, value); }

    /// <summary>显示复制按钮。</summary>
    public bool Copyable { get => (bool)GetValue(CopyableProperty); set => SetValue(CopyableProperty, value); }

    /// <summary>显示行号。</summary>
    public bool LineNumbers { get => (bool)GetValue(LineNumbersProperty); set => SetValue(LineNumbersProperty, value); }

    /// <summary>强调的行。</summary>
    public string? HighlightLines { get => (string?)GetValue(HighlightLinesProperty); set => SetValue(HighlightLinesProperty, value); }

    /// <summary>新增的行。</summary>
    public string? AddedLines { get => (string?)GetValue(AddedLinesProperty); set => SetValue(AddedLinesProperty, value); }

    /// <summary>删除的行。</summary>
    public string? RemovedLines { get => (string?)GetValue(RemovedLinesProperty); set => SetValue(RemovedLinesProperty, value); }

    /// <summary>超过这么多行时折叠。</summary>
    public int? MaxLines { get => (int?)GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }

    /// <summary>去掉自己的边框、圆角和底色。</summary>
    public bool Flush { get => (bool)GetValue(FlushProperty); set => SetValue(FlushProperty, value); }

    /// <summary>高亮器。</summary>
    public CodeHighlighter? Highlighter { get => (CodeHighlighter?)GetValue(HighlighterProperty); set => SetValue(HighlighterProperty, value); }

    /// <summary>展开着。</summary>
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>代码的行数（结尾的换行不算新的一行）。</summary>
    public int LineCount => CodeHighlighting.SplitLines(Code).Length;

    /// <summary>按行显示时的每一行（测试用）。</summary>
    internal IReadOnlyList<DockPanel> Rows => pre.Child is StackPanel lines ? [.. lines.Children.OfType<DockPanel>()] : [];

    /// <summary>当前的高亮结果（测试用）；还没有或纯文本时为 null。</summary>
    internal IReadOnlyList<CodeToken>? Tokens => tokens;

    /// <summary>代码文字（测试用；按行显示时是第一行）。</summary>
    internal TextBlock CodeText => pre.Child as TextBlock ?? Rows[0].Children.OfType<TextBlock>().Last();

    /// <summary>复制按钮、标题栏、标题、代码区、pre、展开按钮与箭头（测试用）。</summary>
    internal (CopyButton Copy, Border Header, ContentPresenter Title, System.Windows.Controls.Grid Body, Border Pre, ButtonBase Expand, TextBlock ExpandText, Icon ExpandIcon) Parts =>
        (copy, header, title, body, pre, expand, expandText, expandIcon);

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    static DependencyProperty Register<T>(string name, T value, bool rehighlight = false) => DependencyProperty.Register(name, typeof(T), typeof(CodeBlock),
        new FrameworkPropertyMetadata(value, (d, _) =>
        {
            var block = (CodeBlock)d;
            if (rehighlight) block.Rehighlight();
            else block.Rebuild();
        }));

    /// <summary>
    /// 代码、语言、高亮器变了：先回到纯文本，再交给高亮器（同一批属性变化合并成一次，投递到界面线程之后再取）；
    /// 先发出的慢结果晚到时不覆盖新结果，高亮失败时保留纯文本。
    /// </summary>
    void Rehighlight()
    {
        ++highlights;
        tokens = null;
        Rebuild();
        if (highlightQueued) return;
        highlightQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, StartHighlight);
    }

    async void StartHighlight()
    {
        highlightQueued = false;
        var version = highlights;
        if (Highlighter is not { } highlighter || CodeLanguage is not { Length: > 0 } language || !Highlight) return;
        IReadOnlyList<CodeToken>? result;
        try
        {
            result = await highlighter(Code, language);
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"CodeBlock 高亮 {language} 代码失败，显示纯文本：{error}");
            return;
        }
        if (version != highlights || result is null) return;
        tokens = result;
        Rebuild();
    }

    /// <summary>按现在的属性重新组装：标题栏、复制按钮的位置、代码（整段或按行）、折叠与展开按钮。</summary>
    void Rebuild()
    {
        var hasTitle = Title is not null;
        header.Visibility = hasTitle ? Visibility.Visible : Visibility.Collapsed;
        title.Content = Title;
        headerRow.Children.Remove(copy);
        body.Children.Remove(copy);
        copy.Value = Code;
        copy.Visibility = Copyable ? Visibility.Visible : Visibility.Collapsed;
        if (hasTitle)
        {
            DockPanel.SetDock(copy, Dock.Right);
            headerRow.Children.Insert(0, copy);
            copy.ClearValue(StyleProperty);
        }
        else
        {
            body.Children.Add(copy);
            copy.SetResourceReference(StyleProperty, CodeBlockParts.Copy);
        }

        var marks = (Highlighted: CodeHighlighting.ParseLines(HighlightLines), Added: CodeHighlighting.ParseLines(AddedLines), Removed: CodeHighlighting.ParseLines(RemovedLines));
        byLine = LineNumbers || marks.Highlighted.Count + marks.Added.Count + marks.Removed.Count > 0;
        pre.Child = byLine ? BuildLines(marks) : Text(Code, 0);
        scroll.Axis = Wrap ? ScrollAreaAxis.Y : ScrollAreaAxis.X;
        ApplyMetrics();
    }

    /// <summary>按行显示：每行一层（铺满整行的底色），行号在前、对读屏隐藏。</summary>
    StackPanel BuildLines((HashSet<int> Highlighted, HashSet<int> Added, HashSet<int> Removed) marks)
    {
        var lines = new StackPanel();
        var offset = 0;
        foreach (var (line, index) in CodeHighlighting.SplitLines(Code.Replace("\r\n", "\n")).Select((line, index) => (line, index)))
        {
            var number = index + 1;
            var row = new DockPanel { LastChildFill = true, Background = Brushes.Transparent };
            row.SetResourceReference(StyleProperty, CodeBlockParts.Line);
            var background = marks.Added.Contains(number) ? "Rune.Brush.CodeInserted"
                : marks.Removed.Contains(number) ? "Rune.Brush.CodeDeleted"
                : marks.Highlighted.Contains(number) ? "Rune.Brush.Fill" : null;
            if (background is not null) row.SetResourceReference(Panel.BackgroundProperty, background);
            row.Tag = marks.Added.Contains(number) ? "added" : marks.Removed.Contains(number) ? "removed" : marks.Highlighted.Contains(number) ? "highlighted" : null;
            if (LineNumbers)
            {
                var label = new LineNumberText { Text = number.ToString(), TextAlignment = TextAlignment.Right };
                label.SetResourceReference(StyleProperty, CodeBlockParts.LineNumber);
                DockPanel.SetDock(label, Dock.Left);
                row.Children.Add(label);
            }
            row.Children.Add(Text(line, offset));
            lines.Children.Add(row);
            offset += line.Length + 1;
        }
        return lines;
    }

    /// <summary>一段代码：有高亮结果时按段着色（段的位置按整段代码算，<paramref name="offset"/> 是这一段在整段里的起点）。</summary>
    TextBlock Text(string text, int offset)
    {
        var block = new TextBlock { TextWrapping = Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap };
        block.SetResourceReference(StyleProperty, CodeBlockParts.Code);
        if (tokens is null)
        {
            block.Text = text;
            return block;
        }
        var position = 0;
        foreach (var token in tokens)
        {
            var start = token.Start - offset;
            var end = start + token.Length;
            if (end <= position || start >= text.Length) continue;
            start = Math.Max(start, position);
            end = Math.Min(end, text.Length);
            if (start > position) block.Inlines.Add(new Run(text[position..start]));
            var run = new Run(text[start..end]);
            run.SetResourceReference(TextElement.ForegroundProperty, BrushKey(token.Kind));
            block.Inlines.Add(run);
            position = end;
        }
        if (position < text.Length) block.Inlines.Add(new Run(text[position..]));
        return block;
    }

    /// <summary>高亮种类的画刷（同上游 Shiki 主题的映射）。</summary>
    static string BrushKey(CodeTokenKind kind) => kind switch
    {
        CodeTokenKind.Comment or CodeTokenKind.Punctuation => "Rune.Brush.CodeComment",
        CodeTokenKind.Keyword => "Rune.Brush.CodeKeyword",
        CodeTokenKind.String => "Rune.Brush.CodeString",
        CodeTokenKind.Number => "Rune.Brush.CodeNumber",
        CodeTokenKind.Function => "Rune.Brush.CodeFunction",
        CodeTokenKind.Type => "Rune.Brush.CodeType",
        CodeTokenKind.Tag => "Rune.Brush.CodeTag",
        _ => "Rune.Brush.CodeAttribute",
    };

    /// <summary>
    /// em 尺寸按各层的字号算：代码 0.9 倍、标题栏 0.85 倍（等宽字体的字面显得大）；圆角 md、标题栏左 1em 右 0.25em 与控件高度、
    /// pre 上下 0.75em 左右 1em（有复制按钮没有标题时右边让出 space.md + 控件高度；按行显示时左右给每一行）、行高 normal、
    /// 行号至少两个数字宽（约 1.2em）与代码隔 1em、复制按钮离角 0.25em。最后按行数折叠。
    /// </summary>
    void ApplyMetrics()
    {
        var em = TextElement.GetFontSize(this);
        var code = 0.9 * em;
        var titleSize = 0.85 * em;
        TextElement.SetFontSize(pre, code);
        TextElement.SetFontSize(header, titleSize);
        root.CornerRadius = Flush ? default : new CornerRadius((TryFindResource("Rune.Radius.Md") as double? ?? 0.375) * em);
        root.BorderThickness = Flush ? default : new Thickness(1);
        if (Flush) root.Background = Brushes.Transparent;
        else root.ClearValue(Border.BackgroundProperty);
        header.Padding = new Thickness(titleSize, 0, 0.25 * titleSize, 0);
        header.MinHeight = ControlHeight.Of(titleSize, ControlHeight.DefaultFactor);
        var withCopy = Copyable && Title is null;
        var end = withCopy ? 0.75 * code + ControlHeight.Of(code, ControlHeight.DefaultFactor) : code;
        pre.Padding = new Thickness(byLine ? 0 : code, 0.75 * code, byLine ? 0 : end, 0.75 * code);
        var lineHeight = (TryFindResource("Rune.LineHeight.Normal") as double? ?? Leading.Normal) * code;
        TextBlock.SetLineHeight(pre, lineHeight);
        TextBlock.SetLineStackingStrategy(pre, LineStackingStrategy.BlockLineHeight);
        foreach (var row in Rows)
        {
            if (LineNumbers && row.Children[0] is TextBlock number)
            {
                number.Margin = new Thickness(code, 0, code, 0);
                number.MinWidth = 1.2 * code;
            }
            ((FrameworkElement)row.Children[^1]).Margin = new Thickness(LineNumbers ? 0 : code, 0, end, 0);
        }
        copy.Margin = copy.Parent == body ? new Thickness(0.25 * em) : default;
        UpdateCollapse(code, lineHeight);
    }

    /// <summary>超过 <see cref="MaxLines"/> 行时折叠：代码区最高 MaxLines 行加上下内边距（0.75em），底部出现展开按钮，箭头展开后翻转。</summary>
    void UpdateCollapse(double code, double lineHeight)
    {
        var collapsible = MaxLines is { } max && LineCount > max;
        expand.Visibility = collapsible ? Visibility.Visible : Visibility.Collapsed;
        var lang = Provider.GetLang(this);
        expandText.Text = IsExpanded ? lang.CodeCollapse : lang.CodeExpand;
        expandIcon.RenderTransform = IsExpanded ? new RotateTransform(180) : Transform.Identity;
        expand.MinHeight = ControlHeight.Of(TextElement.GetFontSize(expand), ControlHeight.DefaultFactor);
        pre.MaxHeight = collapsible && !IsExpanded ? MaxLines!.Value * lineHeight + 2 * 0.75 * code : double.PositiveInfinity;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CodeBlockPeer(this);

    /// <summary>行号：不是代码，读屏不念（WPF 没有 AccessibilityView，读屏节点自己说不是控件、不是内容）。</summary>
    internal sealed class LineNumberText : TextBlock
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new HiddenPeer(this);

        sealed class HiddenPeer(TextBlock owner) : TextBlockAutomationPeer(owner)
        {
            protected override bool IsControlElementCore() => false;

            protected override bool IsContentElementCore() => false;
        }
    }

    /// <summary>读屏：分组（代码本身在里面的文字里）。</summary>
    sealed class CodeBlockPeer(CodeBlock owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(CodeBlock);
    }
}

/// <summary>代码块的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:CodeBlockParts.Header}" TargetType="Border"&gt;</c>。</summary>
public static class CodeBlockParts
{
    /// <summary>外层（边框、圆角、底色；TS 的 propsRoot）。</summary>
    public static readonly PartKey Root = new(typeof(CodeBlock), nameof(Root), typeof(Border));

    /// <summary>标题栏。</summary>
    public static readonly PartKey Header = new(typeof(CodeBlock), nameof(Header), typeof(Border));

    /// <summary>标题文字。</summary>
    public static readonly PartKey Title = new(typeof(CodeBlock), nameof(Title), typeof(ContentPresenter));

    /// <summary>代码区（包着滚动区域与右上角的复制按钮）。</summary>
    public static readonly PartKey Body = new(typeof(CodeBlock), nameof(Body), typeof(System.Windows.Controls.Grid));

    /// <summary>横向滚动区域（TS 的 propsScroll）。</summary>
    public static readonly PartKey Scroll = new(typeof(CodeBlock), nameof(Scroll), typeof(ScrollArea));

    /// <summary>pre：代码的字体、颜色（TS 的 pre 本身）。</summary>
    public static readonly PartKey Pre = new(typeof(CodeBlock), nameof(Pre), typeof(Border));

    /// <summary>代码文字（TS 的 propsCode）。</summary>
    public static readonly PartKey Code = new(typeof(CodeBlock), nameof(Code), typeof(TextBlock));

    /// <summary>按行显示时的每一行（TS 的 propsLine）。</summary>
    public static readonly PartKey Line = new(typeof(CodeBlock), nameof(Line), typeof(DockPanel));

    /// <summary>每行的行号（TS 的 propsLineNumber）。</summary>
    public static readonly PartKey LineNumber = new(typeof(CodeBlock), nameof(LineNumber), typeof(TextBlock));

    /// <summary>代码区右上角的复制按钮（TS 的 propsCopy）。</summary>
    public static readonly PartKey Copy = new(typeof(CodeBlock), nameof(Copy), typeof(CopyButton));

    /// <summary>展开、收起长代码的按钮（TS 的 propsExpand）。</summary>
    public static readonly PartKey Expand = new(typeof(CodeBlock), nameof(Expand), typeof(ButtonBase));

    /// <summary>展开按钮里的箭头（TS 的 propsExpandIcon）。</summary>
    public static readonly PartKey ExpandIcon = new(typeof(CodeBlock), nameof(ExpandIcon), typeof(Icon));
}
