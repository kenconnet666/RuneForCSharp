using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

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
/// 读屏：有标题时是一组（group），以标题为名称、说明为帮助文字；标题带级别（<see cref="HeadingLevel"/>，默认 3）。
/// 部件样式键见 <see cref="FormSectionParts"/>，默认外观在 FormSection.xaml。
/// </summary>
[ContentProperty(nameof(Children))]
public class FormSection : FrameworkElement
{
    /// <summary>标题；不写时没有标题栏，只是一层栅格与字段布局。</summary>
    public static readonly DependencyProperty TitleProperty = Register(nameof(Title), typeof(string), null);

    /// <summary>标题下面的说明。</summary>
    public static readonly DependencyProperty DescriptionProperty = Register(nameof(Description), typeof(string), null);

    /// <summary>标题栏结束边的内容：链接、按钮（"查看关联明细""添加"）。</summary>
    public static readonly DependencyProperty ExtraProperty = Register(nameof(Extra), typeof(UIElement), null);

    /// <summary>标题的级别（2–6），默认 3。</summary>
    public static readonly DependencyProperty HeadingLevelProperty = DependencyProperty.Register(
        nameof(HeadingLevel), typeof(int), typeof(FormSection), new PropertyMetadata(3, (d, _) => ((FormSection)d).Update()), value => value is >= 2 and <= 6);

    /// <summary>最多几列，默认 1；窄时按 MinColumnWidth 自动减少。</summary>
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(FormSection), new PropertyMetadata(1, (d, _) => ((FormSection)d).Update()), value => value is >= 1);

    /// <summary>每列的最小宽度，默认 26em：标签在起始边时放得下标签、控件与横排的三四个选项。</summary>
    public static readonly DependencyProperty MinColumnWidthProperty = Register(nameof(MinColumnWidth), typeof(Length), new Length("26em"));

    /// <summary>可以展开收起：标题变成按钮。</summary>
    public static readonly DependencyProperty IsCollapsibleProperty = Register(nameof(IsCollapsible), typeof(bool), false);

    /// <summary>是否展开（IsCollapsible 时；默认展开、双向绑定）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(FormSection), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((FormSection)d).Update()));

    /// <summary>这一组字段的标签放在哪；不写时沿用 Form 的。</summary>
    public static readonly DependencyProperty LabelPlacementProperty = Field.LabelPlacementProperty.AddOwner(typeof(FormSection));

    /// <summary>这一组字段的标签列宽度。</summary>
    public static readonly DependencyProperty LabelWidthProperty = Field.LabelWidthProperty.AddOwner(typeof(FormSection));

    /// <summary>这一组字段的标签文字对齐。</summary>
    public static readonly DependencyProperty LabelAlignProperty = Field.LabelAlignProperty.AddOwner(typeof(FormSection));

    /// <summary>这一组只读（审批时整组只读）。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = Field.IsReadOnlyProperty.AddOwner(typeof(FormSection));

    readonly Flex layout = new() { Direction = FlexDirection.Column, Gap = Space.Lg };
    readonly Border header = new();
    readonly Flex headerText = new() { Direction = FlexDirection.Column, Gap = Space.Xs };
    readonly Text heading = new();
    readonly ButtonBase trigger = new();
    readonly Text triggerText = new();
    readonly Icon chevron = new() { Data = Lucide.ChevronDown, Tone = TextTone.Muted, RenderTransformOrigin = new Point(0.5, 0.5) };
    readonly Text description = new() { Size = TextSize.Sm, Tone = TextTone.Muted };
    readonly ContentControl extra = new() { Focusable = false, VerticalAlignment = VerticalAlignment.Center };
    readonly Grid body = new() { RowGap = Space.Lg, ColumnGap = Space.Xl, Align = GridAlign.Start };
    readonly Collapse collapse = new();

    /// <summary>标题栏下边距的 em 系数（主题的 space.sm）。</summary>
    static readonly DependencyProperty HeaderGapFactorProperty = DependencyProperty.Register(
        "HeaderGapFactor", typeof(double), typeof(FormSection), new PropertyMetadata(0.5, (d, _) => ((FormSection)d).Resize()));

    static FormSection() =>
        TextElement.FontSizeProperty.OverrideMetadata(typeof(FormSection), new FrameworkPropertyMetadata((d, _) => ((FormSection)d).Resize()));

    static DependencyProperty Register(string name, Type type, object? fallback) => DependencyProperty.Register(
        name, type, typeof(FormSection), new PropertyMetadata(fallback, (d, _) => ((FormSection)d).Update()));

    /// <summary>新建：一列，展开着；部件引用主题的部件样式。</summary>
    public FormSection()
    {
        header.SetResourceReference(StyleProperty, FormSectionParts.Header);
        heading.SetResourceReference(StyleProperty, FormSectionParts.Heading);
        trigger.SetResourceReference(StyleProperty, FormSectionParts.Trigger);
        triggerText.SetResourceReference(StyleProperty, FormSectionParts.Heading);
        chevron.SetResourceReference(StyleProperty, FormSectionParts.Chevron);
        description.SetResourceReference(StyleProperty, FormSectionParts.Description);
        body.SetResourceReference(StyleProperty, FormSectionParts.Body);
        collapse.SetResourceReference(StyleProperty, FormSectionParts.Collapse);
        body.SetValue(Grid.AutoFillProperty, true);
        SetResourceReference(HeaderGapFactorProperty, ThemeKeys.Space.Sm);
        var row = new Flex { Justify = FlexJustify.Between, Align = FlexAlign.End, Wrap = FlexWrap.Wrap, ColumnGap = Space.Lg, RowGap = Space.Xs };
        row.Children.Add(headerText);
        row.Children.Add(extra);
        header.Child = row;
        var triggerContent = new Flex { Gap = Space.Xs, Align = FlexAlign.Center };
        triggerContent.Children.Add(triggerText);
        triggerContent.Children.Add(chevron);
        trigger.Content = triggerContent;
        trigger.Click += (_, _) => SetCurrentValue(IsOpenProperty, !IsOpen);
        AddVisualChild(layout);
        AddLogicalChild(layout);
        Update();
        Resize();
    }

    /// <summary>标题栏下边距 sm（随字号）：Border 不是 Control，EmSize 管不到它的 Padding。</summary>
    void Resize() => header.Padding = new Thickness(0, 0, 0, (double)GetValue(HeaderGapFactorProperty) * TextElement.GetFontSize(this));

    /// <summary>标题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>标题栏结束边的内容。</summary>
    public UIElement? Extra { get => (UIElement?)GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }

    /// <summary>标题的级别。</summary>
    public int HeadingLevel { get => (int)GetValue(HeadingLevelProperty); set => SetValue(HeadingLevelProperty, value); }

    /// <summary>最多几列。</summary>
    public int Columns { get => (int)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>每列的最小宽度。</summary>
    public Length MinColumnWidth { get => (Length)GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    /// <summary>可以展开收起。</summary>
    public bool IsCollapsible { get => (bool)GetValue(IsCollapsibleProperty); set => SetValue(IsCollapsibleProperty, value); }

    /// <summary>是否展开。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>标签放在哪。</summary>
    public FieldLabelPlacement LabelPlacement { get => (FieldLabelPlacement)GetValue(LabelPlacementProperty); set => SetValue(LabelPlacementProperty, value); }

    /// <summary>标签列的宽度。</summary>
    public double LabelWidth { get => (double)GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>标签文字的对齐。</summary>
    public FieldLabelAlign LabelAlign { get => (FieldLabelAlign)GetValue(LabelAlignProperty); set => SetValue(LabelAlignProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>字段（放进栅格）。</summary>
    public UIElementCollection Children => body.Children;

    /// <summary>展开收起时（IsCollapsible）用到的部件、栅格（测试用）。</summary>
    internal (Border Header, Text Heading, ButtonBase Trigger, Grid Body) Parts => (header, heading, trigger, body);

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => layout;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { layout }.GetEnumerator();

    /// <summary>各部分的内容与显示：标题栏（标题或按钮、说明、操作）、栅格的列、展开收起。</summary>
    void Update()
    {
        var hasTitle = !string.IsNullOrEmpty(Title);
        var hasDescription = !string.IsNullOrEmpty(Description);
        heading.Text = Title ?? "";
        triggerText.Text = Title ?? "";
        UIElement titleContent = IsCollapsible ? trigger : heading;
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level1 + (HeadingLevel - 1));
        AutomationProperties.SetHeadingLevel(trigger, AutomationHeadingLevel.Level1 + (HeadingLevel - 1));
        headerText.Children.Clear();
        if (hasTitle) headerText.Children.Add(titleContent);
        description.Text = Description ?? "";
        if (hasDescription) headerText.Children.Add(description);
        extra.Content = Extra;
        extra.Visibility = Extra is null ? Visibility.Collapsed : Visibility.Visible;
        header.Visibility = hasTitle || Extra is not null ? Visibility.Visible : Visibility.Collapsed;

        body.Columns = Columns > 1 ? null : 1;
        body.MinItemWidth = Columns > 1 ? MinColumnWidth : (Length?)null;
        body.SetValue(Grid.MaxColumnsProperty, Columns > 1 ? Columns : null);

        // 收起时箭头朝行尾（从左往右时朝右；从右往左时整行镜像，同一个角度也朝行尾）
        chevron.RenderTransform = IsOpen ? Transform.Identity : new RotateTransform(-90);
        trigger.SetValue(Popover.TriggerExpandedProperty, IsCollapsible ? IsOpen : null);
        collapse.IsOpen = !IsCollapsible || IsOpen;
        layout.Children.Clear();
        layout.Children.Add(header);
        collapse.Content = null;
        if (IsCollapsible)
        {
            collapse.Content = body;
            layout.Children.Add(collapse);
        }
        else layout.Children.Add(body);
        AutomationProperties.SetName(this, hasTitle ? Title : "");
        AutomationProperties.SetHelpText(this, hasDescription ? Description : "");
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        layout.Measure(availableSize);
        return layout.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        layout.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SectionPeer(this);

    /// <summary>有标题时是一组（group）；没有标题时只是一层栅格，读屏不经过它。</summary>
    sealed class SectionPeer(FormSection owner) : FrameworkElementAutomationPeer(owner)
    {
        bool Titled => !string.IsNullOrEmpty(owner.Title);

        protected override AutomationControlType GetAutomationControlTypeCore() => Titled ? AutomationControlType.Group : AutomationControlType.Pane;

        protected override string GetClassNameCore() => nameof(FormSection);

        protected override bool IsControlElementCore() => Titled;
    }
}

/// <summary>FormSection 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:FormSectionParts.Header}" TargetType="Border"&gt;</c>。外层就是 FormSection 自己。</summary>
public static class FormSectionParts
{
    /// <summary>标题栏（TS 的 propsHeader）：Border，下面一条 borderMuted 细线、下边距 sm。</summary>
    public static readonly PartKey Header = new(typeof(FormSection), nameof(Header), typeof(Border));

    /// <summary>标题（TS 的 propsHeading）：Text，md 字号、medium 字重、行高 tight；可折叠时是按钮里的文字。</summary>
    public static readonly PartKey Heading = new(typeof(FormSection), nameof(Heading), typeof(Text));

    /// <summary>展开收起的按钮（TS 的 propsTrigger，IsCollapsible 时代替标题）：ButtonBase，悬停 fill 底。</summary>
    public static readonly PartKey Trigger = new(typeof(FormSection), nameof(Trigger), typeof(ButtonBase));

    /// <summary>展开箭头（TS 的 propsChevron）：Icon，textMuted；收起时转向行尾。</summary>
    public static readonly PartKey Chevron = new(typeof(FormSection), nameof(Chevron), typeof(Icon));

    /// <summary>说明（TS 的 propsDescription）：Text，sm、muted。</summary>
    public static readonly PartKey Description = new(typeof(FormSection), nameof(Description), typeof(Text));

    /// <summary>字段的栅格（TS 的 propsBody）：Grid，行距 lg、列距 xl。</summary>
    public static readonly PartKey Body = new(typeof(FormSection), nameof(Body), typeof(Grid));

    /// <summary>展开收起层（TS 的 propsCollapse，IsCollapsible 时）：Collapse。</summary>
    public static readonly PartKey Collapse = new(typeof(FormSection), nameof(Collapse), typeof(Collapse));
}
