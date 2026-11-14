using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 代码示例卡片（vue-tsx-pro docs-kit 的 CodePreview）：上面是运行中的效果，下面是它的源码（默认收起，可以展开、复制）。文档站的每个示例都是一张。
/// <code>
/// var card = new CodePreviewControl { Code = source, Content = new ButtonControl { Text = "保存" }, Align = CodePreviewAlign.Center, Dock = DockStyle.Top };
/// </code>
/// 本身就是外框 Card（outline、正文无内边距；上游没声明的属性落到 Card 上），正文从上到下：效果区（读屏里是名为"预览"的一组，宽内容在里面横向滚动）、
/// 工具栏（<see cref="Actions"/>、"显示代码"、复制）、源码（Collapse 里贴着卡片的 CodeBlock，不带自己的复制按钮），之间各一道分隔线。
/// 源码也可以给 <see cref="CodeFactory"/> 按需取：展开或复制时才调用，只调用一次（失败的下次再取）。收起时源码不高亮：指针移到、焦点进入工具栏或展开时才高亮。
/// </summary>
public class CodePreviewControl : CardControl
{
    readonly PreviewScroll scroll = new() { Axis = ScrollAreaAxis.X };
    readonly FlexControl layout = new() { Direction = FlexDirection.Column, Gap = Space.Md, AutoSize = true };
    readonly FlexControl toolbar = new() { Justify = FlexJustify.End, Gap = Space.Xs, AutoSize = true };
    readonly ButtonControl toggle = new() { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, TextSize = TextSize.Sm, Icon = Lucide.Code, Expanded = false };
    readonly CopyButtonControl copy = new() { TextSize = TextSize.Sm };
    readonly CollapseControl collapse = new();
    readonly CodeBlockControl code = new() { Flush = true, Copyable = false, Language = "csharp" };
    Task<string>? loading;
    Func<Task<string>>? loadingFrom;
    string? loaded;
    bool wanted;

    /// <summary>新建：outline、正文无内边距，源码收起。</summary>
    public CodePreviewControl()
    {
        Variant = CardVariant.Outline;
        CardPadding = Space.None;
        scroll.Content = layout;
        toolbar.Controls.Add(toggle);
        toolbar.Controls.Add(copy);
        collapse.Controls.Add(new SeparatorControl());
        collapse.Controls.Add(code);
        Controls.AddRange([scroll, new SeparatorControl(), toolbar, collapse]);
        toggle.Click += (_, _) => IsCodeShown = !IsCodeShown;
        // 工具栏上的意图（要看、要复制）：开始取源码与高亮，点下去时多半已经好了；一页十来个示例，打开页面时不必一次高亮全部
        toolbar.MouseEnter += (_, _) => Prepare();
        toolbar.Enter += (_, _) => Prepare();
        copy.ValueFactory = Source;
        UpdateTexts();
        UpdateMetrics();
    }

    /// <summary>源码。</summary>
    [Category("Appearance")]
    [Description("源码。")]
    [DefaultValue(null)]
    public string? Code { get; set { field = value; UpdateCode(); } }

    /// <summary>按需取的源码（TS 的 code 是函数时）：展开或复制时才调用。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Task<string>>? CodeFactory { get; set { field = value; UpdateCode(); } }

    /// <summary>源码的语言，默认 csharp（上游是 tsx）。</summary>
    [Category("Appearance")]
    [Description("源码的语言。")]
    [DefaultValue("csharp")]
    public string Language { get => code.Language ?? "csharp"; set => code.Language = value; }

    /// <summary>显示源码（TS 的 _showCode），默认收起。</summary>
    [Category("Behavior")]
    [Description("显示源码。")]
    [DefaultValue(false)]
    public bool IsCodeShown
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            UpdateTexts();
            if (value) _ = Load();
            IsCodeShownChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>效果区的排列，默认 start。</summary>
    [Category("Layout")]
    [Description("效果区的排列：start 占满宽度，center 居中。")]
    public CodePreviewAlign Align { get; set { field = value; layout.Align = value == CodePreviewAlign.Center ? FlexAlign.Center : FlexAlign.Stretch; } }

    /// <summary>源码超过这么多行时折叠；不写时不折叠。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public int? MaxLines { get => code.MaxLines; set => code.MaxLines = value; }

    /// <summary>工具栏上额外的操作，排在"显示代码"前面。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Actions
    {
        get;
        set
        {
            if (field is { } old) toolbar.Controls.Remove(old);
            field = value;
            if (value is null) return;
            toolbar.Controls.Add(value);
            toolbar.Controls.SetChildIndex(value, 0);
        }
    }

    /// <summary>运行中的效果。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content
    {
        get;
        set
        {
            if (field is { } old) layout.Controls.Remove(old);
            field = value;
            if (value is not null) layout.Controls.Add(value);
        }
    }

    /// <summary>显示源码变了。</summary>
    public event EventHandler? IsCodeShownChanged;

    /// <summary>"显示代码"按钮（测试用）。</summary>
    internal ButtonControl Toggle => toggle;

    /// <summary>工具栏（测试用）。</summary>
    internal FlexControl Toolbar => toolbar;

    /// <summary>效果区（测试用）。</summary>
    internal ScrollAreaControl Preview => scroll;

    /// <summary>源码（测试用）。</summary>
    internal CodeBlockControl CodeBlock => code;

    bool ShouldSerializeAlign() => Align.Value is not null;

    void ResetAlign() => Align = default;

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        UpdateTexts();
    }

    /// <summary>效果的排布外边距 xl（上游的内边距）；工具栏内边距 xs。随字号。</summary>
    void UpdateMetrics()
    {
        var em = this.EmPixels();
        layout.Margin = new Padding((int)MathF.Round(1.5f * em));
        toolbar.Padding = new Padding((int)MathF.Round(0.25f * em));
    }

    /// <summary>按钮的文字与展开状态；效果区是名为"预览"的一组。</summary>
    void UpdateTexts()
    {
        var messages = DocsKitMessages.For(ProviderControl.LangOf(this));
        toggle.Text = IsCodeShown ? messages.HideCode : messages.ShowCode;
        toggle.Expanded = IsCodeShown;
        scroll.AccessibleRole = AccessibleRole.Grouping;
        scroll.AccessibleName = messages.Preview;
        collapse.IsOpen = IsCodeShown;
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
                if (loadingFrom == from && !IsDisposed)
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

    /// <summary>效果区：横向滚动，高度跟着内容（ScrollAreaControl 本身的高度由外面给）。</summary>
    sealed class PreviewScroll : ScrollAreaControl
    {
        public override Size GetPreferredSize(Size proposedSize) =>
            Content is { } content ? new Size(proposedSize.Width, content.GetPreferredSize(Size.Empty).Height + content.Margin.Vertical) : Size.Empty;
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
