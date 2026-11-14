using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 代码示例卡片（vue-tsx-pro docs-kit 的 CodePreview）：上面是运行中的效果，下面是它的源码（默认收起，可以展开、复制）。文档站的每个示例都是一张。
/// <code>
/// &lt;rune:CodePreview Code="{Binding Source}" Language="xml" Align="center"&gt;&lt;rune:Button&gt;保存&lt;/rune:Button&gt;&lt;/rune:CodePreview&gt;
/// </code>
/// 效果与源码由使用方各自给出（组件不执行代码）；源码也可以给 <see cref="CodeFactory"/> 按需取：展开或复制时才调用，只调用一次（失败的下次再取）。
/// 外框是 Card（outline、正文无内边距），里面从上到下：效果区（读屏里是名为"预览"的一组，宽内容在里面横向滚动）、
/// 工具栏（<see cref="Actions"/>、"显示代码"、复制）、源码（Collapse 里贴着卡片的 CodeBlock，不带自己的复制按钮），之间各一道 borderMuted 的分隔线
/// （上游的工具栏与源码在 Card 的页脚里，页脚上边一道线，源码上边一道线）。
/// 部件：<c>rune|CodePreview rune|Card#PART_Card</c>、<c>Border#PART_Preview</c>、<c>rune|ScrollArea#PART_PreviewScroll</c>、<c>rune|Flex#PART_Layout</c>、
/// <c>Border#PART_Toolbar</c>、<c>rune|Button#PART_Toggle</c>、<c>rune|CopyButton#PART_Copy</c>、<c>rune|Collapse#PART_Collapse</c>、<c>rune|CodeBlock#PART_Code</c>。
/// </summary>
public class CodePreview : Control
{
    /// <summary>源码。</summary>
    public static readonly StyledProperty<string?> CodeProperty = AvaloniaProperty.Register<CodePreview, string?>(nameof(Code));

    /// <summary>按需取的源码（TS 的 code 是函数时）：展开或复制时才调用。</summary>
    public static readonly StyledProperty<Func<Task<string>>?> CodeFactoryProperty = AvaloniaProperty.Register<CodePreview, Func<Task<string>>?>(nameof(CodeFactory));

    /// <summary>源码的语言，默认 csharp（上游是 tsx）。</summary>
    public static readonly StyledProperty<string> LanguageProperty = AvaloniaProperty.Register<CodePreview, string>(nameof(Language), "csharp");

    /// <summary>显示源码（TS 的 _showCode），默认收起；双向绑定。</summary>
    public static readonly StyledProperty<bool> IsCodeShownProperty =
        AvaloniaProperty.Register<CodePreview, bool>(nameof(IsCodeShown), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>效果区的排列，默认 start。</summary>
    public static readonly StyledProperty<CodePreviewAlign> AlignProperty = AvaloniaProperty.Register<CodePreview, CodePreviewAlign>(nameof(Align));

    /// <summary>源码超过这么多行时折叠（见 CodeBlock 的 MaxLines）；不写时不折叠。</summary>
    public static readonly StyledProperty<int?> MaxLinesProperty = AvaloniaProperty.Register<CodePreview, int?>(nameof(MaxLines));

    /// <summary>工具栏上额外的操作，排在"显示代码"前面。</summary>
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<CodePreview, object?>(nameof(Actions));

    /// <summary>运行中的效果。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<CodePreview, object?>(nameof(Content));

    readonly Card card = new() { Name = CodePreviewParts.Card, Variant = CardVariant.Outline, Padding = Space.None };
    readonly Border preview = new() { Name = CodePreviewParts.Preview };
    readonly ScrollArea scroll = new() { Name = CodePreviewParts.PreviewScroll, Axis = ScrollAreaAxis.X };
    readonly Flex layout = new() { Name = CodePreviewParts.Layout, Direction = FlexDirection.Column, Gap = Space.Md };
    readonly ContentControl content = new();
    readonly Border toolbar = new() { Name = CodePreviewParts.Toolbar };
    readonly ContentControl actions = new();
    readonly Button toggle = new() { Name = CodePreviewParts.Toggle, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Size = TextSize.Sm, Icon = Lucide.Code };
    readonly CopyButton copy = new() { Name = CodePreviewParts.Copy, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Size = TextSize.Sm };
    readonly Collapse collapse = new() { Name = CodePreviewParts.Collapse };
    readonly CodeBlock code = new() { Name = CodePreviewParts.Code, Flush = true, Copyable = false };
    Task<string>? loading;
    Func<Task<string>>? loadingFrom;
    string? loaded;
    bool wanted;

    static CodePreview() => AffectsMeasure<CodePreview>(AlignProperty);

    /// <summary>新建：源码收起。</summary>
    public CodePreview()
    {
        layout.Children.Add(content);
        scroll.Content = layout;
        preview.Child = scroll;
        toolbar.Child = new Flex { Justify = FlexJustify.End, Gap = Space.Xs, Children = { actions, toggle, copy } };
        // 源码上边的分隔线画在外面包的一层上（Collapse 没有描边）
        collapse.Content = new Border { Classes = { "code-frame" }, Child = code };
        card.Content = new StackPanel { Children = { preview, toolbar, collapse } };
        toggle.Click += (_, _) => SetCurrentValue(IsCodeShownProperty, !IsCodeShown);
        // 工具栏上的意图（要看、要复制）：开始取源码与高亮，点下去时多半已经好了；一页十来个示例，打开页面时不必一次高亮全部
        toolbar.PointerEntered += (_, _) => Prepare();
        toolbar.GotFocus += (_, _) => Prepare();
        copy.ValueFactory = Source;
        LogicalChildren.Add(card);
        VisualChildren.Add(card);
        UpdateAlign();
        UpdateTexts();
        UpdateCode();
        UpdateMetrics();
    }

    /// <summary>源码。</summary>
    public string? Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    /// <summary>按需取的源码。</summary>
    public Func<Task<string>>? CodeFactory { get => GetValue(CodeFactoryProperty); set => SetValue(CodeFactoryProperty, value); }

    /// <summary>源码的语言。</summary>
    public string Language { get => GetValue(LanguageProperty); set => SetValue(LanguageProperty, value); }

    /// <summary>显示源码。</summary>
    public bool IsCodeShown { get => GetValue(IsCodeShownProperty); set => SetValue(IsCodeShownProperty, value); }

    /// <summary>效果区的排列。</summary>
    public CodePreviewAlign Align { get => GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>源码折叠的行数。</summary>
    public int? MaxLines { get => GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }

    /// <summary>工具栏上额外的操作。</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <summary>运行中的效果。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>外框（测试与样式用）。</summary>
    internal Card Card => card;

    /// <summary>"显示代码"按钮。</summary>
    internal Button Toggle => toggle;

    /// <summary>源码。</summary>
    internal CodeBlock CodeBlock => code;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty) content.Content = Content;
        else if (change.Property == TextElement.FontSizeProperty) UpdateMetrics();
        else if (change.Property == ActionsProperty) actions.Content = Actions;
        else if (change.Property == AlignProperty) UpdateAlign();
        else if (change.Property == IsCodeShownProperty) UpdateTexts();
        else if (change.Property == CodeProperty || change.Property == CodeFactoryProperty || change.Property == LanguageProperty || change.Property == MaxLinesProperty) UpdateCode();
        if (change.Property == IsCodeShownProperty && IsCodeShown) _ = Load();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(global::Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        UpdateTexts();
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        card.Measure(availableSize);
        return card.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        card.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>效果的排布：外边距 xl（上游的内边距）；工具栏内边距 xs。随字号，按模板优先级（使用方的样式盖得过）。</summary>
    void UpdateMetrics()
    {
        var em = GetValue(TextElement.FontSizeProperty);
        layout.SetValue(MarginProperty, new Thickness(this.Resource<double>(ThemeKeys.Space.Xl) * em), BindingPriority.Template);
        toolbar.SetValue(Border.PaddingProperty, new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * em), BindingPriority.Template);
    }

    /// <summary>靠起始边时效果占满宽度（里面的网格、卡片按整块宽度排）；居中时按内容宽度居中。</summary>
    void UpdateAlign() => layout.Align = Align == CodePreviewAlign.Center ? FlexAlign.Center : FlexAlign.Stretch;

    /// <summary>按钮的文字与读屏：显示 / 隐藏代码、展开状态；效果区是名为"预览"的一组。</summary>
    void UpdateTexts()
    {
        var messages = DocsKitMessages.For(Provider.GetLang(this));
        toggle.Content = IsCodeShown ? messages.HideCode : messages.ShowCode;
        AutomationProperties.SetName(preview, messages.Preview);
        AutomationProperties.SetControlTypeOverride(preview, global::Avalonia.Automation.Peers.AutomationControlType.Group);
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
        code.Language = Language;
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

/// <summary>CodePreview 的部件名。</summary>
public static class CodePreviewParts
{
    /// <summary>外框（TS 的 propsCard）。</summary>
    public const string Card = "PART_Card";

    /// <summary>效果区（TS 的 propsPreview）。</summary>
    public const string Preview = "PART_Preview";

    /// <summary>效果区里横向滚动的 ScrollArea（TS 的 propsPreviewScroll）。</summary>
    public const string PreviewScroll = "PART_PreviewScroll";

    /// <summary>效果的排布（TS 的 propsLayout）：内边距 xl、项之间 md。</summary>
    public const string Layout = "PART_Layout";

    /// <summary>工具栏（TS 的 propsToolbar）。</summary>
    public const string Toolbar = "PART_Toolbar";

    /// <summary>"显示代码"按钮（TS 的 propsToggle）。</summary>
    public const string Toggle = "PART_Toggle";

    /// <summary>复制按钮（TS 的 propsCopy）。</summary>
    public const string Copy = "PART_Copy";

    /// <summary>源码的收起展开（TS 的 propsCollapse）。</summary>
    public const string Collapse = "PART_Collapse";

    /// <summary>源码（TS 的 propsCode）。</summary>
    public const string Code = "PART_Code";
}
