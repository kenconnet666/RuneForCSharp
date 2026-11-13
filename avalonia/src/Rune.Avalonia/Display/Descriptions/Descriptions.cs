using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 描述列表（vue-tsx-pro 的 Descriptions）：键值对的详情（订单信息、用户资料），可分栏、可跨列，标签在左（横排）、在上（竖排）或各项一行排开（行内）。
/// <code>
/// &lt;rune:Descriptions Title="订单信息" Columns="3"&gt;
///   &lt;rune:DescriptionItem Label="订单号" Value="A-1024" Copyable="True" /&gt;
///   &lt;rune:DescriptionItem Label="收货地址" Value="{Binding Address}" Span="3" /&gt;
/// &lt;/rune:Descriptions&gt;
/// </code>
/// 固定栏数时，列表窄于 32em 排成一列（TS 的容器查询）；<see cref="Columns"/> 写 auto 时按列表的宽度定栏数：每栏不窄于 <see cref="MinItemWidth"/>，
/// 最多 <see cref="MaxColumns"/> 栏。跨列（Span）按算出的栏数封顶。栏间距 space.xl、行间距 space.md；横排时标签与值之间 space.md、按基线对齐，
/// 竖排时上下 space.xs；行内时各项之间 space.xl（换行 space.xs），项内 space.xs。标签次要色，值正文色、占满剩下的宽度。
/// <see cref="Bordered"/>：表格样式，外框 borderMuted、圆角 md，格子之间有分隔线，标签格 fill 底、至少 8em，格子内边距 space.sm / space.md。
/// 项写 Copyable 时值后面有复制按钮（xs，名称"复制 + 标签"），复制的是 CopyText，没写时是值的文字。里面的链接悬停才有下划线。
/// 读屏里每一项是一组，名称是标签，里面是值。
/// 部件：<c>rune|Descriptions rune|Text#PART_Title</c>、<c>Border#PART_List</c>、<c>Border#PART_Item</c>、<c>Border#PART_Label</c>、<c>Border#PART_Value</c>、<c>rune|CopyButton#PART_Copy</c>。
/// </summary>
public class Descriptions : Control
{
    /// <summary>标题。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Descriptions, string?>(nameof(Title));

    /// <summary>分几栏，默认 1；auto 时按宽度定。</summary>
    public static readonly StyledProperty<DescriptionsColumns> ColumnsProperty = AvaloniaProperty.Register<Descriptions, DescriptionsColumns>(nameof(Columns));

    /// <summary>Columns 为 auto 时每栏的最小宽度（像素），默认 240。</summary>
    public static readonly StyledProperty<double> MinItemWidthProperty = AvaloniaProperty.Register<Descriptions, double>(nameof(MinItemWidth), 240);

    /// <summary>Columns 为 auto 时最多几栏，默认 4。</summary>
    public static readonly StyledProperty<int> MaxColumnsProperty = AvaloniaProperty.Register<Descriptions, int>(nameof(MaxColumns), 4);

    /// <summary>标签与值的排列，默认 horizontal。</summary>
    public static readonly StyledProperty<DescriptionsLayout> LayoutProperty = AvaloniaProperty.Register<Descriptions, DescriptionsLayout>(nameof(Layout));

    /// <summary>带边框（表格样式）：标签有底色，各项之间有分隔线。</summary>
    public static readonly StyledProperty<bool> BorderedProperty = AvaloniaProperty.Register<Descriptions, bool>(nameof(Bordered));

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Descriptions, TextSize>(nameof(Size));

    /// <summary>字号（继承），间距按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Descriptions>();

    readonly Flex root = new() { Direction = FlexDirection.Column, Align = FlexAlign.Stretch };
    readonly Text title = new() { Name = DescriptionsParts.Title, IsVisible = false };
    readonly Border list = new() { Name = DescriptionsParts.List };
    readonly Grid grid = new();
    readonly Flex inline = new() { Wrap = true };
    readonly List<(DescriptionItem Item, GridItem Cell)> built = [];
    readonly TemplateResources resources;
    int columns = 1;

    static Descriptions() => AffectsMeasure<Descriptions>(FontSizeProperty);

    /// <summary>新建：没有项。</summary>
    public Descriptions()
    {
        resources = new TemplateResources(this);
        root.Children.AddRange([title, list]);
        Link.SetUnderlineScope(this, LinkUnderline.Hover);
        Items.CollectionChanged += (_, _) => Rebuild();
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Rebuild();
    }

    /// <summary>各项。</summary>
    [Content]
    public AvaloniaList<DescriptionItem> Items { get; } = [];

    /// <summary>标题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>分几栏。</summary>
    public DescriptionsColumns Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>auto 时每栏的最小宽度。</summary>
    public double MinItemWidth { get => GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }

    /// <summary>auto 时最多几栏。</summary>
    public int MaxColumns { get => GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }

    /// <summary>标签与值的排列。</summary>
    public DescriptionsLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <summary>带边框。</summary>
    public bool Bordered { get => GetValue(BorderedProperty); set => SetValue(BorderedProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>当前的栏数（测试用）。</summary>
    internal int ColumnCount => columns;

    /// <summary>各项的格子（测试用）。</summary>
    internal IReadOnlyList<GridItem> Cells => [.. built.Select(entry => entry.Cell)];

    DescriptionsLayout ShownLayout => Layout.Value is null ? DescriptionsLayout.Horizontal : Layout;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == TitleProperty)
        {
            title.Text = Title;
            title.IsVisible = !string.IsNullOrEmpty(Title);
        }
        else if (change.Property == LayoutProperty || change.Property == BorderedProperty || change.Property == Provider.LangProperty) Rebuild();
        else if (change.Property == FontSizeProperty) UpdateMetrics();
        else if (change.Property == ColumnsProperty || change.Property == MinItemWidthProperty || change.Property == MaxColumnsProperty) InvalidateMeasure();
    }

    /// <summary>按项重建：每一项是 [标签][值（与复制按钮）]，放进网格（行内时放进换行的一排）。</summary>
    void Rebuild()
    {
        // 拆开旧的格子：使用方给的标签、值（控件）要能挂到新的格子上
        foreach (var (_, cell) in built)
        {
            var row = (Flex)((Border)cell.Child!).Child!;
            foreach (var part in row.Children.OfType<Border>())
            {
                if (part.Child is CopyRow copyRow) copyRow.Children.Clear();
                part.Child = null;
            }
            cell.Child = null;
        }
        built.Clear();
        grid.Children.Clear();
        inline.Children.Clear();
        var layout = ShownLayout;
        var lang = Provider.GetLang(this);
        foreach (var item in Items)
        {
            var label = new Border { Name = DescriptionsParts.Label, Child = Content(item.Label) };
            var value = new Border { Name = DescriptionsParts.Value, Child = item.Copyable ? WithCopy(item, lang) : Content(item.Value) };
            // 标签已经是这一组的名称，不再单独念
            AutomationProperties.SetAccessibilityView(label, AccessibilityView.Raw);
            var row = new Flex
            {
                Direction = layout == DescriptionsLayout.Vertical ? FlexDirection.Column : FlexDirection.Row,
                Align = Bordered ? FlexAlign.Stretch : layout == DescriptionsLayout.Vertical ? FlexAlign.Stretch : FlexAlign.Baseline,
                Children = { label, value },
            };
            if (layout != DescriptionsLayout.Vertical) Flex.SetGrow(value, true);
            var cell = new ItemBorder { Name = DescriptionsParts.Item, Child = row };
            AutomationProperties.SetName(cell, PlainText(item.Label));
            var gridItem = new GridItem { Child = cell };
            built.Add((item, gridItem));
            if (layout == DescriptionsLayout.Inline) inline.Children.Add(gridItem);
            else grid.Children.Add(gridItem);
        }
        list.Child = layout == DescriptionsLayout.Inline ? inline : grid;
        PseudoClasses.Set(":bordered", Bordered);
        UpdateMetrics();
        InvalidateMeasure();
    }

    /// <summary>值后面跟复制按钮：按钮紧跟在文字后面，文字换行时按钮在第一行。</summary>
    Control WithCopy(DescriptionItem item, Lang lang)
    {
        var copy = new CopyButton { Name = DescriptionsParts.Copy, Size = TextSize.Xs, Value = item.CopyText ?? (item.Value is string or IFormattable ? Convert.ToString(item.Value, System.Globalization.CultureInfo.CurrentCulture) : "") };
        AutomationProperties.SetName(copy, lang.DescriptionsCopy(PlainText(item.Label) is { Length: > 0 } label ? label : lang.Copy));
        return new CopyRow { Align = FlexAlign.Baseline, Gap = Space.Xs, Children = { Content(item.Value), copy } };
    }

    /// <summary>文字放进会换行的 Text（值很长时按字断开，TS 的 overflow-wrap: anywhere）；控件原样放。</summary>
    static Control Content(object? content) => content switch
    {
        Control control => control,
        null => new Text(),
        _ => new Text { Text = Convert.ToString(content, System.Globalization.CultureInfo.CurrentCulture) },
    };

    static string PlainText(object? content) => content switch
    {
        string text => text,
        TextBlock block => block.Text ?? "",
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.CurrentCulture),
        _ => "",
    };

    /// <summary>间距与格子：非边框时栏间距 space.xl、行间距 space.md；边框时没有间距，格子有内边距与分隔线。标题固定 md 的字号。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        var line = this.Resource("Rune.BorderWidth.Xs", 1.0);
        var layout = ShownLayout;
        root.Gap = Space.Md;
        title.FontSize = this.Resource("Rune.FontSize.Md", 14.0);
        (grid.ColumnGap, grid.RowGap) = Bordered ? (Space.None, Space.None) : (Space.Xl, Space.Md);
        (inline.ColumnGap, inline.RowGap) = (Space.Xl, Space.Xs);
        list.BorderThickness = new Thickness(Bordered ? line : 0);
        list.CornerRadius = new CornerRadius(Bordered ? this.Resource("Rune.Radius.Md", 0.375) * em : 0);
        list.ClipToBounds = Bordered;
        var padding = Bordered ? new Thickness(this.Resource("Rune.Space.Md", 0.75) * em, this.Resource("Rune.Space.Sm", 0.5) * em) : default;
        foreach (var (_, gridItem) in built)
        {
            var cell = (Border)gridItem.Child!;
            var row = (Flex)cell.Child!;
            var (label, value) = ((Border)row.Children[0], (Border)row.Children[1]);
            row.Gap = Bordered ? Space.None : layout == DescriptionsLayout.Horizontal ? Space.Md : Space.Xs;
            label.Padding = value.Padding = padding;
            label.MinWidth = Bordered ? 8 * em : 0;
            // 复制按钮上下各收 0.25em：不把这一行撑高
            if (value.Child is CopyRow { Children: [_, CopyButton copy] }) copy.Margin = new Thickness(0, -0.25 * em);
            // 每格画上、起始边的线，往回挪一条线宽叠在前一格的线上；外框由列表画，盖住第一行、第一列的线
            cell.BorderThickness = Bordered ? new Thickness(line, line, 0, 0) : default;
            cell.Margin = Bordered ? new Thickness(-line, -line, 0, 0) : default;
        }
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var shown = Columns.Value is null ? (DescriptionsColumns)1 : Columns;
        var count = ShownLayout == DescriptionsLayout.Inline ? 1 : shown.Resolve(availableSize.Width, FontSize, MinItemWidth, MaxColumns);
        if (count != columns || grid.Columns != count)
        {
            columns = count;
            grid.Columns = count;
        }
        foreach (var (item, gridItem) in built) gridItem.Span = Math.Clamp(item.Span, 1, count);
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>值与复制按钮的一行（重建时只拆它，使用方自己的 Flex 不动）。</summary>
    sealed class CopyRow : Flex
    {
        protected override Type StyleKeyOverride => typeof(Flex);
    }

    /// <summary>一项：样式按 Border 匹配（部件选择器写 Border#PART_Item），读屏里是一组，名称是标签。</summary>
    sealed class ItemBorder : Border
    {
        protected override Type StyleKeyOverride => typeof(Border);

        protected override AutomationPeer OnCreateAutomationPeer() => new ItemPeer(this);

        sealed class ItemPeer(ItemBorder owner) : ControlAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

            protected override bool IsControlElementCore() => true;
        }
    }
}

/// <summary>描述列表里的一项（vue-tsx-pro 的 DescriptionItem）。</summary>
public class DescriptionItem
{
    /// <summary>名称：文字或控件。</summary>
    public object? Label { get; set; }

    /// <summary>值：文字、数字或控件。</summary>
    public object? Value { get; set; }

    /// <summary>占几列，默认 1；超过栏数时按栏数。</summary>
    public int Span { get; set; } = 1;

    /// <summary>值后面放复制按钮。</summary>
    public bool Copyable { get; set; }

    /// <summary>复制的文字；不写时是值的文字（值是文字或数字时）。</summary>
    public string? CopyText { get; set; }
}

/// <summary>Descriptions 的部件名。</summary>
public static class DescriptionsParts
{
    /// <summary>标题（TS 的 propsTitle）。</summary>
    public const string Title = "PART_Title";

    /// <summary>列表（TS 的 propsList）。</summary>
    public const string List = "PART_List";

    /// <summary>一项（TS 的 propsItem）。</summary>
    public const string Item = "PART_Item";

    /// <summary>名称（TS 的 propsLabel）。</summary>
    public const string Label = "PART_Label";

    /// <summary>值（TS 的 propsValue）。</summary>
    public const string Value = "PART_Value";

    /// <summary>复制按钮（TS 的 propsCopy）。</summary>
    public const string Copy = "PART_Copy";
}
