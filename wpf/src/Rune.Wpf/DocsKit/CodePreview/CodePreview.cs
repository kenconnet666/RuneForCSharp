using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace Rune.Wpf;

/// <summary>
/// 代码示例卡片（vue-tsx-pro docs-kit 的 CodePreview）：上面是运行中的效果，下面是它的源码（默认收起，可以展开、复制）。文档站的每个示例都是一张。
/// <code>
/// &lt;rune:CodePreview Code="{Binding Source}" CodeLanguage="xml" Align="center"&gt;&lt;rune:Button&gt;保存&lt;/rune:Button&gt;&lt;/rune:CodePreview&gt;
/// </code>
/// 效果与源码由使用方各自给出（组件不执行代码）；源码也可以给 <see cref="CodeFactory"/> 按需取：展开或复制时才调用，只调用一次（失败的下次再取）。
/// 外框是 Card（outline、正文无内边距），里面从上到下：效果区（读屏里是名为"预览"的一组，宽内容在里面横向滚动）、
/// 工具栏（<see cref="Actions"/>、"显示代码"、复制）、源码（Collapse 里贴着卡片的 CodeBlock，不带自己的复制按钮），之间各一道 borderMuted 的分隔线
/// （上游的工具栏与源码在 Card 的页脚里）。收起时源码不高亮：指针移到、焦点进入工具栏或展开时才高亮。部件样式键：<see cref="CodePreviewParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class CodePreview : CompositeControl
{
    /// <summary>源码。</summary>
    public static readonly DependencyProperty CodeProperty = Register(nameof(Code), typeof(string), null, (preview, _) => preview.UpdateCode());

    /// <summary>按需取的源码（TS 的 code 是函数时）：展开或复制时才调用。</summary>
    public static readonly DependencyProperty CodeFactoryProperty = Register(nameof(CodeFactory), typeof(Func<Task<string>>), null, (preview, _) => preview.UpdateCode());

    /// <summary>源码的语言，默认 csharp（上游是 tsx；FrameworkElement 自己有 Language）。</summary>
    public static readonly DependencyProperty CodeLanguageProperty = Register(nameof(CodeLanguage), typeof(string), "csharp", (preview, _) => preview.UpdateCode());

    /// <summary>显示源码（TS 的 _showCode），默认收起；双向绑定。</summary>
    public static readonly DependencyProperty IsCodeShownProperty = DependencyProperty.Register(nameof(IsCodeShown), typeof(bool), typeof(CodePreview),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((CodePreview)d).OnCodeShownChanged()));

    /// <summary>效果区的排列，默认 start。</summary>
    public static readonly DependencyProperty AlignProperty = Register(nameof(Align), typeof(CodePreviewAlign), default(CodePreviewAlign), (preview, _) => preview.UpdateAlign());

    /// <summary>源码超过这么多行时折叠（见 CodeBlock 的 MaxLines）；不写时不折叠。</summary>
    public static readonly DependencyProperty MaxLinesProperty = Register(nameof(MaxLines), typeof(int?), null, (preview, _) => preview.UpdateCode());

    /// <summary>工具栏上额外的操作，排在"显示代码"前面。</summary>
    public static readonly DependencyProperty ActionsProperty = Register(nameof(Actions), typeof(object), null, (preview, _) => preview.actions.Content = preview.Actions);

    /// <summary>运行中的效果。</summary>
    public static readonly DependencyProperty ContentProperty = Register(nameof(Content), typeof(object), null, (preview, _) => preview.content.Content = preview.Content);

    readonly Card card = new() { Variant = CardVariant.Outline, Padding = Space.None };
    readonly Border preview = new PreviewBorder();
    readonly ScrollArea scroll = new() { Axis = ScrollAreaAxis.X };
    readonly Flex layout = new() { Direction = FlexDirection.Column, Gap = Space.Md };
    readonly ContentControl content = new() { Focusable = false };
    readonly Border toolbar = new();
    readonly ContentControl actions = new() { Focusable = false };
    readonly Button toggle = new() { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Size = TextSize.Sm, Icon = Lucide.Code };
    readonly CopyButton copy = new() { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Size = TextSize.Sm };
    readonly Collapse collapse = new();
    readonly Border codeFrame = new();
    readonly CodeBlock code = new() { Flush = true, Copyable = false };
    Task<string>? loading;
    Func<Task<string>>? loadingFrom;
    string? loaded;
    bool wanted;

    /// <summary>新建：源码收起。</summary>
    public CodePreview()
    {
        foreach (var (part, key) in new (FrameworkElement, PartKey)[]
        {
            (card, CodePreviewParts.Card), (preview, CodePreviewParts.Preview), (scroll, CodePreviewParts.PreviewScroll), (layout, CodePreviewParts.Layout),
            (toolbar, CodePreviewParts.Toolbar), (toggle, CodePreviewParts.Toggle), (copy, CodePreviewParts.Copy), (collapse, CodePreviewParts.Collapse),
            (codeFrame, CodePreviewParts.CodeFrame), (code, CodePreviewParts.Code),
        }) part.SetResourceReference(StyleProperty, key);
        layout.Children.Add(content);
        scroll.Content = layout;
        preview.Child = scroll;
        var tools = new Flex { Justify = FlexJustify.End, Gap = Space.Xs };
        tools.Children.Add(actions);
        tools.Children.Add(toggle);
        tools.Children.Add(copy);
        toolbar.Child = tools;
        codeFrame.Child = code;
        collapse.Content = codeFrame;
        var stack = new StackPanel();
        stack.Children.Add(preview);
        stack.Children.Add(toolbar);
        stack.Children.Add(collapse);
        card.Content = stack;
        toggle.Click += (_, _) => SetCurrentValue(IsCodeShownProperty, !IsCodeShown);
        // 工具栏上的意图（要看、要复制）：开始取源码与高亮，点下去时多半已经好了；一页十来个示例，打开页面时不必一次高亮全部
        toolbar.MouseEnter += (_, _) => Prepare();
        toolbar.GotKeyboardFocus += (_, _) => Prepare();
        copy.ValueFactory = Source;
        Root = card;
        Loaded += (_, _) => UpdateTexts();
        UpdateAlign();
        UpdateTexts();
        UpdateCode();
        UpdateMetrics();
    }

    /// <summary>源码。</summary>
    public string? Code { get => (string?)GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    /// <summary>按需取的源码。</summary>
    public Func<Task<string>>? CodeFactory { get => (Func<Task<string>>?)GetValue(CodeFactoryProperty); set => SetValue(CodeFactoryProperty, value); }

    /// <summary>源码的语言。</summary>
    public string CodeLanguage { get => (string)GetValue(CodeLanguageProperty); set => SetValue(CodeLanguageProperty, value); }

    /// <summary>显示源码。</summary>
    public bool IsCodeShown { get => (bool)GetValue(IsCodeShownProperty); set => SetValue(IsCodeShownProperty, value); }

    /// <summary>效果区的排列。</summary>
    public CodePreviewAlign Align { get => (CodePreviewAlign)GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>源码折叠的行数。</summary>
    public int? MaxLines { get => (int?)GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }

    /// <summary>工具栏上额外的操作。</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <summary>运行中的效果。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>"显示代码"按钮（测试用）。</summary>
    internal Button Toggle => toggle;

    /// <summary>工具栏（测试用）。</summary>
    internal Border ToolbarPart => toolbar;

    /// <summary>效果区（测试用）。</summary>
    internal Border PreviewPart => preview;

    /// <summary>源码（测试用）。</summary>
    internal CodeBlock CodeBlock => code;

    static DependencyProperty Register(string name, Type type, object? fallback, Action<CodePreview, DependencyPropertyChangedEventArgs> changed) =>
        DependencyProperty.Register(name, type, typeof(CodePreview), new FrameworkPropertyMetadata(fallback, (d, e) => changed((CodePreview)d, e)));

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) UpdateMetrics();
    }

    /// <summary>效果的排布：外边距 xl（上游的内边距）、项之间 md；工具栏内边距 xs。随字号。</summary>
    void UpdateMetrics()
    {
        layout.Margin = new Thickness(this.Resource<double>(ThemeKeys.Space.Xl) * FontSize);
        toolbar.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * FontSize);
    }

    /// <summary>靠起始边时效果占满宽度（里面的网格、卡片按整块宽度排）；居中时按内容宽度居中。</summary>
    void UpdateAlign() => layout.Align = Align == CodePreviewAlign.Center ? FlexAlign.Center : FlexAlign.Stretch;

    void OnCodeShownChanged()
    {
        UpdateTexts();
        if (IsCodeShown) _ = Load();
    }

    /// <summary>按钮的文字与展开状态；效果区是名为"预览"的一组。</summary>
    void UpdateTexts()
    {
        var messages = DocsKitMessages.For(Provider.GetLang(this));
        toggle.Content = IsCodeShown ? messages.HideCode : messages.ShowCode;
        AutomationProperties.SetName(preview, messages.Preview);
        collapse.IsOpen = IsCodeShown;
        toggle.SetValue(Popover.TriggerExpandedProperty, IsCodeShown);
        code.Highlight = IsCodeShown || wanted;
    }

    void Prepare()
    {
        if (wanted) return;
        wanted = true;
        code.Highlight = true;
        _ = Load();
    }

    void UpdateCode()
    {
        code.Highlight = IsCodeShown || wanted;
        code.CodeLanguage = CodeLanguage;
        code.MaxLines = MaxLines;
        code.Code = Code ?? (CodeFactory is not null && loadingFrom == CodeFactory ? loaded : null) ?? "";
    }

    /// <summary>复制取的源码：写了 Code 用它，否则按需取。</summary>
    Task<string> Source() => Code is { } text ? Task.FromResult(text) : Load();

    /// <summary>按需取源码：同一个函数只取一次；失败的不记，下次再取（不然一次断网之后源码区一直是空的）。</summary>
    Task<string> Load()
    {
        if (Code is { } text) return Task.FromResult(text);
        if (CodeFactory is not { } factory) return Task.FromResult("");
        if (loadingFrom == factory && loading is not null) return loading;
        loadingFrom = factory;
        loaded = null;
        loading = Fetch(factory);
        return loading;

        async Task<string> Fetch(Func<Task<string>> from)
        {
            try
            {
                var result = await from();
                if (loadingFrom == from)
                {
                    loaded = result;
                    UpdateCode();
                }
                return result;
            }
            catch
            {
                if (loadingFrom == from) (loadingFrom, loading) = (null, null);
                throw;
            }
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    /// <summary>效果区：读屏里是名为"预览"的一组。</summary>
    sealed class PreviewPeer(Border owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;
    }

    /// <summary>效果区的读屏节点（WPF 的 Border 默认不进控件视图）。</summary>
    sealed class PreviewBorder : Border
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new PreviewPeer(this);
    }
}

/// <summary>CodePreview 效果区的排列（vue-tsx-pro 的 CodePreviewAligns）。</summary>
/// <param name="Value">取值（start、center）。</param>
[TypeConverter(typeof(VariantConverter<CodePreviewAlign>))]
public readonly record struct CodePreviewAlign(string Value) : IVariant<CodePreviewAlign>
{
    /// <summary>从起始边排起、占满宽度（默认）：表单、卡片、长内容。</summary>
    public static readonly CodePreviewAlign Start = new("start");

    /// <summary>居中：单个按钮、图标这类小东西。</summary>
    public static readonly CodePreviewAlign Center = new("center");

    /// <inheritdoc />
    public static IReadOnlyList<CodePreviewAlign> Known { get; } = [Start, Center];

    /// <inheritdoc />
    public static CodePreviewAlign Parse(string value)
    {
        var align = new CodePreviewAlign(value);
        Variants.WarnIfUnknown(align);
        return align;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>CodePreview 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:CodePreviewParts.Toolbar}" TargetType="Border"&gt;</c>。</summary>
public static class CodePreviewParts
{
    /// <summary>外框（TS 的 propsCard）。</summary>
    public static readonly PartKey Card = new(typeof(CodePreview), nameof(Card), typeof(Card));

    /// <summary>效果区（TS 的 propsPreview）。</summary>
    public static readonly PartKey Preview = new(typeof(CodePreview), nameof(Preview), typeof(Border));

    /// <summary>效果区里横向滚动的 ScrollArea（TS 的 propsPreviewScroll）。</summary>
    public static readonly PartKey PreviewScroll = new(typeof(CodePreview), nameof(PreviewScroll), typeof(ScrollArea));

    /// <summary>效果的排布（TS 的 propsLayout）。</summary>
    public static readonly PartKey Layout = new(typeof(CodePreview), nameof(Layout), typeof(Flex));

    /// <summary>工具栏（TS 的 propsToolbar）。</summary>
    public static readonly PartKey Toolbar = new(typeof(CodePreview), nameof(Toolbar), typeof(Border));

    /// <summary>"显示代码"按钮（TS 的 propsToggle）。</summary>
    public static readonly PartKey Toggle = new(typeof(CodePreview), nameof(Toggle), typeof(Button));

    /// <summary>复制按钮（TS 的 propsCopy）。</summary>
    public static readonly PartKey Copy = new(typeof(CodePreview), nameof(Copy), typeof(CopyButton));

    /// <summary>源码的收起展开（TS 的 propsCollapse）。</summary>
    public static readonly PartKey Collapse = new(typeof(CodePreview), nameof(Collapse), typeof(Collapse));

    /// <summary>源码外面的一层：上边的分隔线（上游画在源码上，Collapse 没有描边）。</summary>
    public static readonly PartKey CodeFrame = new(typeof(CodePreview), nameof(CodeFrame), typeof(Border));

    /// <summary>源码（TS 的 propsCode）。</summary>
    public static readonly PartKey Code = new(typeof(CodePreview), nameof(Code), typeof(CodeBlock));
}
