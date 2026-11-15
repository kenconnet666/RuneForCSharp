using Avalonia;
using Avalonia.Media.Transformation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 表单里的一组字段（vue-tsx-pro 的 FormSection）：标题、说明、结束边的操作，下面一条细线，再下面是字段的栅格。长表单按主题分组。
/// <code>
/// &lt;rune:FormSection Title="项目基本信息" Columns="2"&gt;
///   &lt;rune:Field Label="项目名称"&gt;…&lt;/rune:Field&gt;
///   &lt;rune:GridItem Span="full"&gt;&lt;rune:Field Label="简要描述"&gt;…&lt;/rune:Field&gt;&lt;/rune:GridItem&gt;
/// &lt;/rune:FormSection&gt;
/// </code>
/// <see cref="Columns"/> 是最多几列：每列不窄于 <see cref="MinColumnWidth"/>（默认 26em），窄时自动减少列数；跨列用 GridItem。
/// 列距 xl 比行距 lg 大（一眼看出哪几项是同一列），只有一个字段时也只占一列宽（auto-fill）。
/// 也是一层字段布局：标签位置、宽度、对齐、只读写在这里只作用于这一组，没写的沿用 Form 的；不可用写 IsEnabled。
/// <see cref="IsCollapsible"/> 时标题是展开收起的按钮（读屏带展开状态），收起的字段不能被 Tab 到。
/// 读屏：一组（group），以标题为名称、说明为帮助文字；标题带级别（<see cref="HeadingLevel"/>，默认 3）。
/// 上游 id 当锚点、配 TableOfContents 的分节导航是网页的，桌面上用 BringIntoView。部件：<see cref="FormSectionParts"/>，默认外观在 FormSection.axaml。
/// </summary>
[PseudoClasses(":open", ":collapsible")]
public class FormSection : Control
{
    /// <summary>标题；不写时没有标题栏，只是一层栅格与字段布局。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<FormSection, string?>(nameof(Title));

    /// <summary>标题下面的说明。</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<FormSection, string?>(nameof(Description));

    /// <summary>标题栏结束边的内容：链接、按钮（"查看关联明细""添加"）。</summary>
    public static readonly StyledProperty<Control?> ExtraProperty = AvaloniaProperty.Register<FormSection, Control?>(nameof(Extra));

    /// <summary>标题的级别（2–6），默认 3。</summary>
    public static readonly StyledProperty<int> HeadingLevelProperty =
        AvaloniaProperty.Register<FormSection, int>(nameof(HeadingLevel), 3, validate: level => level is >= 2 and <= 6);

    /// <summary>最多几列，默认 1；窄时按 MinColumnWidth 自动减少。</summary>
    public static readonly StyledProperty<int> ColumnsProperty = AvaloniaProperty.Register<FormSection, int>(nameof(Columns), 1, validate: count => count >= 1);

    /// <summary>每列的最小宽度，默认 26em：标签在起始边时放得下标签、控件与横排的三四个选项。</summary>
    public static readonly StyledProperty<Length> MinColumnWidthProperty = AvaloniaProperty.Register<FormSection, Length>(nameof(MinColumnWidth), new Length("26em"));

    /// <summary>可以展开收起：标题变成按钮。</summary>
    public static readonly StyledProperty<bool> IsCollapsibleProperty = AvaloniaProperty.Register<FormSection, bool>(nameof(IsCollapsible));

    /// <summary>是否展开（IsCollapsible 时；默认展开、双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<FormSection, bool>(nameof(IsOpen), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>这一组字段的标签放在哪；不写时沿用 Form 的。</summary>
    public static readonly StyledProperty<FieldLabelPlacement> LabelPlacementProperty = Field.LabelPlacementProperty.AddOwner<FormSection>();

    /// <summary>这一组字段的标签列宽度。</summary>
    public static readonly StyledProperty<double> LabelWidthProperty = Field.LabelWidthProperty.AddOwner<FormSection>();

    /// <summary>这一组字段的标签文字对齐。</summary>
    public static readonly StyledProperty<FieldLabelAlign> LabelAlignProperty = Field.LabelAlignProperty.AddOwner<FormSection>();

    /// <summary>这一组只读（审批时整组只读）。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = Field.IsReadOnlyProperty.AddOwner<FormSection>();

    readonly Flex layout = new() { Direction = FlexDirection.Column, Gap = Space.Lg };
    readonly Border header = new() { Name = FormSectionParts.Header };
    readonly Flex headerText = new() { Direction = FlexDirection.Column, Gap = Space.Xs };
    readonly Text heading = new() { Name = FormSectionParts.Heading };
    readonly ButtonBase trigger = new() { Name = FormSectionParts.Trigger };
    readonly Text triggerText = new();
    readonly Icon chevron = new() { Name = FormSectionParts.Chevron, Data = Lucide.ChevronDown, Tone = TextTone.Muted };
    readonly Text description = new() { Name = FormSectionParts.Description, Size = TextSize.Sm, Tone = TextTone.Muted };
    readonly ContentControl extra = new() { Name = FormSectionParts.Extra, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
    readonly Grid body = new() { Name = FormSectionParts.Body, RowGap = Space.Lg, ColumnGap = Space.Xl, Align = GridAlign.Start, AutoFill = true };
    readonly Collapse collapse = new() { Name = FormSectionParts.Collapse };

    /// <summary>新建：一列，展开着。</summary>
    public FormSection()
    {
        var row = new Flex { Justify = FlexJustify.Between, Align = FlexAlign.End, Wrap = true, ColumnGap = Space.Lg, RowGap = Space.Xs };
        row.Children.AddRange([headerText, extra]);
        header.Child = row;
        trigger.Content = new Flex { Gap = Space.Xs, Align = FlexAlign.Center, Children = { triggerText, chevron } };
        trigger.Click += (_, _) => SetCurrentValue(IsOpenProperty, !IsOpen);
        LogicalChildren.Add(layout);
        VisualChildren.Add(layout);
        Update();
    }

    /// <summary>标题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>标题栏结束边的内容。</summary>
    public Control? Extra { get => GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }

    /// <summary>标题的级别。</summary>
    public int HeadingLevel { get => GetValue(HeadingLevelProperty); set => SetValue(HeadingLevelProperty, value); }

    /// <summary>最多几列。</summary>
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>每列的最小宽度。</summary>
    public Length MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    /// <summary>可以展开收起。</summary>
    public bool IsCollapsible { get => GetValue(IsCollapsibleProperty); set => SetValue(IsCollapsibleProperty, value); }

    /// <summary>是否展开。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>标签放在哪。</summary>
    public FieldLabelPlacement LabelPlacement { get => GetValue(LabelPlacementProperty); set => SetValue(LabelPlacementProperty, value); }

    /// <summary>标签列的宽度。</summary>
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>标签文字的对齐。</summary>
    public FieldLabelAlign LabelAlign { get => GetValue(LabelAlignProperty); set => SetValue(LabelAlignProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>字段（放进栅格）。</summary>
    [Content]
    public global::Avalonia.Controls.Controls Children => body.Children;

    /// <summary>字段的栅格（测试用）。</summary>
    internal Grid Body => body;

    /// <summary>展开收起的按钮（测试用）。</summary>
    internal ButtonBase Trigger => trigger;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == DescriptionProperty || change.Property == ExtraProperty || change.Property == HeadingLevelProperty
            || change.Property == ColumnsProperty || change.Property == MinColumnWidthProperty || change.Property == IsCollapsibleProperty || change.Property == IsOpenProperty)
            Update();
    }

    /// <summary>各部分的内容与显示：标题栏（标题或按钮、说明、操作）、栅格的列、展开收起。</summary>
    void Update()
    {
        var hasTitle = !string.IsNullOrEmpty(Title);
        var hasDescription = !string.IsNullOrEmpty(Description);
        heading.Text = Title;
        triggerText.Text = Title;
        Control titleContent = IsCollapsible ? trigger : heading;
        AutomationProperties.SetHeadingLevel(titleContent, HeadingLevel);
        headerText.Children.Clear();
        if (hasTitle) headerText.Children.Add(titleContent);
        description.Text = Description;
        if (hasDescription) headerText.Children.Add(description);
        extra.Content = Extra;
        extra.IsVisible = Extra is not null;
        header.IsVisible = hasTitle || Extra is not null;

        body.Columns = Columns > 1 ? null : 1;
        body.MinItemWidth = Columns > 1 ? MinColumnWidth : (Length?)null;
        body.MaxColumns = Columns > 1 ? Columns : null;

        var open = !IsCollapsible || IsOpen;
        // 收起时箭头朝行尾（从左往右时朝右）
        chevron.RenderTransform = IsOpen ? null : TransformOperations.Parse(FlowDirection == FlowDirection.RightToLeft ? "rotate(90deg)" : "rotate(-90deg)");
        trigger.SetValue(Popover.TriggerExpandedProperty, IsCollapsible ? IsOpen : null);
        collapse.IsOpen = open;
        layout.Children.Clear();
        layout.Children.Add(header);
        if (IsCollapsible)
        {
            collapse.Content = body;
            layout.Children.Add(collapse);
        }
        else
        {
            collapse.Content = null;
            layout.Children.Add(body);
        }
        PseudoClasses.Set(":collapsible", IsCollapsible);
        PseudoClasses.Set(":open", open);
        AutomationProperties.SetName(this, hasTitle ? Title : null);
        AutomationProperties.SetHelpText(this, hasDescription ? Description : null);
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        layout.Measure(availableSize);
        return layout.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        layout.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SectionPeer(this);

    /// <summary>有标题时是一组（group）；没有标题时只是一层栅格，读屏不经过它。</summary>
    sealed class SectionPeer(FormSection owner) : ControlAutomationPeer(owner)
    {
        bool Titled => !string.IsNullOrEmpty(owner.Title);

        protected override AutomationControlType GetAutomationControlTypeCore() => Titled ? AutomationControlType.Group : AutomationControlType.Pane;

        protected override bool IsControlElementCore() => Titled;
    }
}

/// <summary>FormSection 的部件名：<c>rune|FormSection rune|Border#PART_Header</c>。外层（TS 的 root）就是 FormSection 自己。</summary>
public static class FormSectionParts
{
    /// <summary>标题栏（TS 的 propsHeader）：Border，下面一条 borderMuted 细线、下边距 sm。</summary>
    public const string Header = "PART_Header";

    /// <summary>标题（TS 的 propsHeading）：Text，md 字号、medium 字重、行高 tight。</summary>
    public const string Heading = "PART_Heading";

    /// <summary>展开收起的按钮（TS 的 propsTrigger，IsCollapsible 时代替标题）：ButtonBase，悬停 fill 底。</summary>
    public const string Trigger = "PART_Trigger";

    /// <summary>展开箭头（TS 的 propsChevron）：Icon，textMuted；收起时转向行尾。</summary>
    public const string Chevron = "PART_Chevron";

    /// <summary>说明（TS 的 propsDescription）：Text，sm、muted。</summary>
    public const string Description = "PART_Description";

    /// <summary>标题栏结束边的内容（TS 的 propsExtra）：ContentControl。</summary>
    public const string Extra = "PART_Extra";

    /// <summary>字段的栅格（TS 的 propsBody）：Grid，行距 lg、列距 xl。</summary>
    public const string Body = "PART_Body";

    /// <summary>展开收起层（TS 的 propsCollapse，IsCollapsible 时）：Collapse。</summary>
    public const string Collapse = "PART_Collapse";
}
