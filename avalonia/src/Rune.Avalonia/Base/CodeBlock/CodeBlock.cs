using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 代码块（vue-tsx-pro 的 CodeBlock）：等宽字体的代码，标题栏、复制、行号、强调与增删的行、长代码折叠。
/// <code>
/// &lt;rune:CodeBlock Code="{Binding Source}" Language="cs" /&gt;
/// &lt;rune:CodeBlock Code="…" Language="ts" Title="main.ts" LineNumbers="True" HighlightLines="2,4-5" MaxLines="20" /&gt;
/// </code>
/// <list type="bullet">
/// <item>高亮：<see cref="Highlighter"/>（默认 <see cref="CodeHighlighting.Builtin"/>：TS / JS、C#、XML / XAML、JSON、CSS、SQL、bash）。异步：先显示纯文本，结果到了再替换；
/// 没给语言、语言不认识、<see cref="Highlight"/> 为 false 时按纯文本显示。颜色取主题的 code* token。</item>
/// <item>长行在 <see cref="ScrollArea"/> 里横向滚动（<see cref="Wrap"/> 时换行）；复制按钮在右上角，有标题时放进标题栏。</item>
/// <item>按行显示（有行号、强调或增删的行时）：每行一层，底色铺满整行；行号不可选、对读屏隐藏。</item>
/// <item>超过 <see cref="MaxLines"/> 行时折叠，底部的按钮展开或收起（语言包的"展开全部""收起"）。</item>
/// <item><see cref="Flush"/>：嵌在已有外框的容器里，去掉自己的边框、圆角和底色。代码总是从左到右排。</item>
/// </list>
/// 部件是子元素（<see cref="CodeBlockParts"/>），默认外观在 CodeBlockParts.axaml，Provider 的样式能改。
/// </summary>
public class CodeBlock : Control
{
    /// <summary>代码文本。</summary>
    public static readonly StyledProperty<string> CodeProperty = AvaloniaProperty.Register<CodeBlock, string>(nameof(Code), "");

    /// <summary>语言（ts、cs、xml……），交给高亮器；不写时不高亮。</summary>
    public static readonly StyledProperty<string?> LanguageProperty = AvaloniaProperty.Register<CodeBlock, string?>(nameof(Language));

    /// <summary>高亮：false 时按纯文本显示，改成 true 才交给高亮器（看不见的代码可以等要显示时再高亮），默认 true。</summary>
    public static readonly StyledProperty<bool> HighlightProperty = AvaloniaProperty.Register<CodeBlock, bool>(nameof(Highlight), true);

    /// <summary>标题栏：通常是文件名；有标题时复制按钮放进标题栏。</summary>
    public static readonly StyledProperty<object?> TitleProperty = AvaloniaProperty.Register<CodeBlock, object?>(nameof(Title));

    /// <summary>长行自动换行，而不是横向滚动。</summary>
    public static readonly StyledProperty<bool> WrapProperty = AvaloniaProperty.Register<CodeBlock, bool>(nameof(Wrap));

    /// <summary>显示复制按钮，默认 true。</summary>
    public static readonly StyledProperty<bool> CopyableProperty = AvaloniaProperty.Register<CodeBlock, bool>(nameof(Copyable), true);

    /// <summary>显示行号。</summary>
    public static readonly StyledProperty<bool> LineNumbersProperty = AvaloniaProperty.Register<CodeBlock, bool>(nameof(LineNumbers));

    /// <summary>强调的行：<c>"2,4-5"</c>。</summary>
    public static readonly StyledProperty<string?> HighlightLinesProperty = AvaloniaProperty.Register<CodeBlock, string?>(nameof(HighlightLines));

    /// <summary>新增的行（diff）：codeInserted 底色。</summary>
    public static readonly StyledProperty<string?> AddedLinesProperty = AvaloniaProperty.Register<CodeBlock, string?>(nameof(AddedLines));

    /// <summary>删除的行（diff）：codeDeleted 底色。</summary>
    public static readonly StyledProperty<string?> RemovedLinesProperty = AvaloniaProperty.Register<CodeBlock, string?>(nameof(RemovedLines));

    /// <summary>超过这么多行时折叠，底部的按钮展开；不写时不折叠。</summary>
    public static readonly StyledProperty<int?> MaxLinesProperty = AvaloniaProperty.Register<CodeBlock, int?>(nameof(MaxLines));

    /// <summary>嵌在卡片、标签页这类已经有外框的容器里：去掉自己的边框、圆角和底色。</summary>
    public static readonly StyledProperty<bool> FlushProperty = AvaloniaProperty.Register<CodeBlock, bool>(nameof(Flush));

    /// <summary>高亮器；默认内置的 <see cref="CodeHighlighting.Builtin"/>，设成 null 时不高亮。</summary>
    public static readonly StyledProperty<CodeHighlighter?> HighlighterProperty =
        AvaloniaProperty.Register<CodeBlock, CodeHighlighter?>(nameof(Highlighter), CodeHighlighting.Builtin, inherits: true);

    /// <summary>展开着（折叠的长代码）。</summary>
    public static readonly DirectProperty<CodeBlock, bool> IsExpandedProperty = AvaloniaProperty.RegisterDirect<CodeBlock, bool>(nameof(IsExpanded), block => block.IsExpanded);

    readonly Border root = new() { Name = CodeBlockParts.Root, ClipToBounds = true };
    readonly DockPanel layout = new() { LastChildFill = true };
    readonly Border header = new() { Name = CodeBlockParts.Header };
    readonly DockPanel headerRow = new();
    readonly ContentPresenter title = new() { Name = CodeBlockParts.Title, VerticalAlignment = VerticalAlignment.Center };
    readonly Panel body = new() { Name = CodeBlockParts.Body };
    readonly ScrollArea scroll = new() { Name = CodeBlockParts.Scroll, Axis = ScrollAreaAxis.X, IsViewportFocusable = true };
    readonly Border pre = new() { Name = CodeBlockParts.Pre };
    readonly CopyButton copy = new() { Name = CodeBlockParts.Copy, Size = TextSize.Sm, Tone = StatusTone.Neutral };
    readonly ButtonBase expand = new() { Name = CodeBlockParts.Expand };
    readonly TextBlock expandText = new();
    readonly Icon expandIcon = new() { Name = CodeBlockParts.ExpandIcon, Data = Lucide.ChevronDown, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>代码字体的连字（Cascadia Code 把 => 画成 ⇒）会让照着抄的人看错，一律关掉。</summary>
    static readonly FontFeatureCollection NoLigatures = FontFeatureCollection.Parse("-liga, -calt");

    IReadOnlyList<CodeToken>? tokens;
    int highlights;
    bool highlightQueued;
    IDisposable? preFont;
    IDisposable? headerFont;

    static CodeBlock()
    {
        AffectsMeasure<CodeBlock>(CodeProperty, LineNumbersProperty, MaxLinesProperty, WrapProperty);
    }

    /// <summary>新建：组装标题栏、代码区与展开按钮。</summary>
    public CodeBlock()
    {
        headerRow.Children.Add(title);
        header.Child = headerRow;
        scroll.Content = pre;
        body.Children.Add(scroll);
        expand.Content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4, Children = { expandText, expandIcon } };
        expand.Click += (_, _) => IsExpanded = !IsExpanded;
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(expand, Dock.Bottom);
        layout.Children.Add(header);
        layout.Children.Add(expand);
        layout.Children.Add(body);
        root.Child = layout;
        // 代码总是从左到右排：从右到左的页面里也不跟着镜像
        pre.FlowDirection = FlowDirection.LeftToRight;
        // pre 的字号跟着外面变（0.9 倍）：em 尺寸重算
        pre.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextElement.FontSizeProperty) ApplyMetrics();
        };
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Rebuild();
    }

    /// <summary>代码文本。</summary>
    public string Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    /// <summary>语言。</summary>
    public string? Language { get => GetValue(LanguageProperty); set => SetValue(LanguageProperty, value); }

    /// <summary>高亮。</summary>
    public bool Highlight { get => GetValue(HighlightProperty); set => SetValue(HighlightProperty, value); }

    /// <summary>标题栏。</summary>
    public object? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>长行自动换行。</summary>
    public bool Wrap { get => GetValue(WrapProperty); set => SetValue(WrapProperty, value); }

    /// <summary>显示复制按钮。</summary>
    public bool Copyable { get => GetValue(CopyableProperty); set => SetValue(CopyableProperty, value); }

    /// <summary>显示行号。</summary>
    public bool LineNumbers { get => GetValue(LineNumbersProperty); set => SetValue(LineNumbersProperty, value); }

    /// <summary>强调的行。</summary>
    public string? HighlightLines { get => GetValue(HighlightLinesProperty); set => SetValue(HighlightLinesProperty, value); }

    /// <summary>新增的行。</summary>
    public string? AddedLines { get => GetValue(AddedLinesProperty); set => SetValue(AddedLinesProperty, value); }

    /// <summary>删除的行。</summary>
    public string? RemovedLines { get => GetValue(RemovedLinesProperty); set => SetValue(RemovedLinesProperty, value); }

    /// <summary>超过这么多行时折叠。</summary>
    public int? MaxLines { get => GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }

    /// <summary>去掉自己的边框、圆角和底色。</summary>
    public bool Flush { get => GetValue(FlushProperty); set => SetValue(FlushProperty, value); }

    /// <summary>高亮器。</summary>
    public CodeHighlighter? Highlighter { get => GetValue(HighlighterProperty); set => SetValue(HighlighterProperty, value); }

    /// <summary>展开着。</summary>
    public bool IsExpanded
    {
        get;
        set
        {
            if (!SetAndRaise(IsExpandedProperty, ref field, value)) return;
            UpdateCollapse();
        }
    }

    /// <summary>代码的行数（结尾的换行不算新的一行）。</summary>
    public int LineCount => CodeHighlighting.SplitLines(Code).Length;

    /// <summary>代码区里的行（按行显示时每行一层，测试用）。</summary>
    internal IReadOnlyList<Control> Rows => pre.Child is StackPanel lines ? lines.Children : [];

    /// <summary>当前的高亮结果（测试用）；还没有或纯文本时为 null。</summary>
    internal IReadOnlyList<CodeToken>? Tokens => tokens;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == CodeProperty || property == LanguageProperty || property == HighlightProperty || property == HighlighterProperty) Rehighlight();
        else if (property == LineNumbersProperty || property == HighlightLinesProperty || property == AddedLinesProperty || property == RemovedLinesProperty
                 || property == WrapProperty || property == TitleProperty || property == CopyableProperty || property == MaxLinesProperty || property == FlushProperty)
            Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // 代码相对周围的文字略小（等宽字体的字面显得大），标题栏再小一点：是比例不是字号档位
        preFont = FontScale.Bind(pre, this, 0.9);
        headerFont = FontScale.Bind(header, this, 0.86);
        // 挂上树才拿得到作用域的语言包与 token
        ApplyMetrics();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        preFont?.Dispose();
        headerFont?.Dispose();
    }

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
        Dispatcher.UIThread.Post(StartHighlight);
    }

    async void StartHighlight()
    {
        highlightQueued = false;
        var version = highlights;
        if (Highlighter is not { } highlighter || Language is not { Length: > 0 } language || !Highlight) return;
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
        header.IsVisible = hasTitle;
        title.Content = Title;
        headerRow.Children.Remove(copy);
        body.Children.Remove(copy);
        copy.Value = Code;
        copy.IsVisible = Copyable;
        if (hasTitle)
        {
            DockPanel.SetDock(copy, Dock.Right);
            headerRow.Children.Insert(0, copy);
        }
        else body.Children.Add(copy);
        root.Classes.Set("flush", Flush);
        pre.Classes.Set("with-copy", Copyable && !hasTitle);
        pre.Classes.Set("wrap", Wrap);

        var marks = (Highlighted: CodeHighlighting.ParseLines(HighlightLines), Added: CodeHighlighting.ParseLines(AddedLines), Removed: CodeHighlighting.ParseLines(RemovedLines));
        var byLine = LineNumbers || marks.Highlighted.Count + marks.Added.Count + marks.Removed.Count > 0;
        pre.Classes.Set("lines", byLine);
        pre.Child = byLine ? BuildLines(marks) : Text(Code, 0);
        scroll.Axis = Wrap ? ScrollAreaAxis.Y : ScrollAreaAxis.X;
        ApplyMetrics();
    }

    /// <summary>
    /// em 尺寸按各层的字号算（Border 没有字号属性，主题里的 em 写不上）：圆角 md、标题栏左 1em 右 0.25em 与控件高度、
    /// pre 上下 0.75em 左右 1em（有复制按钮没有标题时右边让出 space.md + 控件高度；按行显示时左右给每一行）、行高 normal、
    /// 行号至少两个数字宽（约 1.2em）与代码隔 1em、复制按钮离角 0.25em。最后按行数折叠。
    /// </summary>
    void ApplyMetrics()
    {
        var em = TextElement.GetFontSize(this);
        var code = TextElement.GetFontSize(pre);
        var title = TextElement.GetFontSize(header);
        root.CornerRadius = Flush ? default : new CornerRadius(this.Resource("Rune.Radius.Md", 0.375) * em);
        header.Padding = new Thickness(title, 0, 0.25 * title, 0);
        header.MinHeight = ControlHeight.Of(title, ControlHeight.DefaultFactor);
        var byLine = pre.Classes.Contains("lines");
        var end = pre.Classes.Contains("with-copy") ? 0.75 * code + ControlHeight.Of(code, ControlHeight.DefaultFactor) : code;
        pre.Padding = new Thickness(byLine ? 0 : code, 0.75 * code, byLine ? 0 : end, 0.75 * code);
        var lineHeight = Leading.GetFactor(pre) * code;
        foreach (var block in pre.GetLogicalDescendants().OfType<SelectableTextBlock>().Append(pre.Child as SelectableTextBlock))
            if (block is not null) block.LineHeight = lineHeight;
        foreach (var row in Rows.OfType<DockPanel>())
            foreach (var part in row.Children)
                part.Margin = part.Name == CodeBlockParts.LineNumber ? new Thickness(code, 0, code, 0)
                    : new Thickness(LineNumbers ? 0 : code, 0, byLine ? end : 0, 0);
        foreach (var number in pre.GetLogicalDescendants().OfType<TextBlock>().Where(block => block.Name == CodeBlockParts.LineNumber))
            number.MinWidth = 1.2 * code;
        // 在代码区右上角时离角 space.xs；放进标题栏时不留（标题栏自己有内边距）
        copy.Margin = copy.Parent == body ? new Thickness(0.25 * em) : default;
        UpdateCollapse();
    }

    /// <summary>按行显示：每行一层（铺满整行的底色），行号在前、不可选、对读屏隐藏。</summary>
    StackPanel BuildLines((HashSet<int> Highlighted, HashSet<int> Added, HashSet<int> Removed) marks)
    {
        var lines = new StackPanel { Name = CodeBlockParts.Lines };
        var offset = 0;
        var source = Code.Replace("\r\n", "\n");
        foreach (var (line, index) in CodeHighlighting.SplitLines(source).Select((line, index) => (line, index)))
        {
            var number = index + 1;
            var row = new DockPanel { Name = CodeBlockParts.Line };
            row.Classes.Set("highlighted", marks.Highlighted.Contains(number));
            row.Classes.Set("added", marks.Added.Contains(number));
            row.Classes.Set("removed", marks.Removed.Contains(number));
            if (LineNumbers)
            {
                var label = new TextBlock { Name = CodeBlockParts.LineNumber, Text = number.ToString(), TextAlignment = TextAlignment.Right };
                global::Avalonia.Automation.AutomationProperties.SetAccessibilityView(label, global::Avalonia.Automation.AccessibilityView.Raw);
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
    SelectableTextBlock Text(string text, int offset)
    {
        var block = new SelectableTextBlock { Name = CodeBlockParts.Code, TextWrapping = Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, FontFeatures = NoLigatures };
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
            if (start > position) block.Inlines!.Add(new Run(text[position..start]));
            var run = new Run(text[start..end]);
            run.Bind(TextElement.ForegroundProperty, this.GetResourceObservable(BrushKey(token.Kind)));
            block.Inlines!.Add(run);
            position = end;
        }
        if (position < text.Length) block.Inlines!.Add(new Run(text[position..]));
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

    /// <summary>超过 <see cref="MaxLines"/> 行时折叠：代码区最高 MaxLines 行加上下内边距（0.75em），底部出现展开按钮，箭头展开后翻转。</summary>
    void UpdateCollapse()
    {
        var collapsible = MaxLines is { } max && LineCount > max;
        expand.IsVisible = collapsible;
        var lang = Provider.GetLang(this);
        expandText.Text = IsExpanded ? lang.CodeCollapse : lang.CodeExpand;
        expandIcon.RenderTransform = IsExpanded ? TransformOperations.Parse("rotate(180deg)") : TransformOperations.Identity;
        var em = TextElement.GetFontSize(pre);
        var lineHeight = Leading.GetFactor(pre) * em;
        pre.MaxHeight = collapsible && !IsExpanded ? MaxLines!.Value * lineHeight + 2 * 0.75 * em : double.PositiveInfinity;
    }

    /// <inheritdoc />
    protected override global::Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new CodeBlockPeer(this);

    /// <summary>读屏：分组（代码本身在里面的文字里）。</summary>
    sealed class CodeBlockPeer(CodeBlock owner) : global::Avalonia.Automation.Peers.ControlAutomationPeer(owner)
    {
        protected override global::Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => global::Avalonia.Automation.Peers.AutomationControlType.Group;
    }
}

/// <summary>代码块的部件名：样式里用 <c>rune|CodeBlock Border#PART_Header</c> 选中（TS 的 propsHeader、propsTitle……）。</summary>
public static class CodeBlockParts
{
    /// <summary>外层（边框、圆角、底色；TS 的 propsRoot）。</summary>
    public const string Root = "PART_Root";

    /// <summary>标题栏。</summary>
    public const string Header = "PART_Header";

    /// <summary>标题文字。</summary>
    public const string Title = "PART_Title";

    /// <summary>代码区（包着滚动区域与右上角的复制按钮）。</summary>
    public const string Body = "PART_Body";

    /// <summary>横向滚动区域（TS 的 propsScroll）。</summary>
    public const string Scroll = "PART_Scroll";

    /// <summary>pre：代码的内边距、字体、行高（TS 的 pre 本身）。</summary>
    public const string Pre = "PART_Pre";

    /// <summary>代码文字（TS 的 propsCode）。</summary>
    public const string Code = "PART_Code";

    /// <summary>按行显示时所有行的容器。</summary>
    public const string Lines = "PART_Lines";

    /// <summary>按行显示时的每一行（TS 的 propsLine）。</summary>
    public const string Line = "PART_Line";

    /// <summary>每行的行号（TS 的 propsLineNumber）。</summary>
    public const string LineNumber = "PART_LineNumber";

    /// <summary>复制按钮（TS 的 propsCopy）。</summary>
    public const string Copy = "PART_Copy";

    /// <summary>展开、收起长代码的按钮（TS 的 propsExpand）。</summary>
    public const string Expand = "PART_Expand";

    /// <summary>展开按钮里的箭头（TS 的 propsExpandIcon）。</summary>
    public const string ExpandIcon = "PART_ExpandIcon";
}
