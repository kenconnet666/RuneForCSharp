using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

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
/// 读屏里每一项是一组，名称是标签，里面是值。部件样式键：<see cref="DescriptionsParts"/>。
/// </summary>
[ContentProperty(nameof(Items))]
public class Descriptions : Control
{
    /// <summary>标题。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(Descriptions), new FrameworkPropertyMetadata(null, (d, _) => ((Descriptions)d).UpdateTitle()));

    /// <summary>分几栏，默认 1；auto 时按宽度定。</summary>
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(DescriptionsColumns), typeof(Descriptions), new FrameworkPropertyMetadata(default(DescriptionsColumns), FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Columns 为 auto 时每栏的最小宽度（像素），默认 240。</summary>
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(Descriptions), new FrameworkPropertyMetadata(240d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Columns 为 auto 时最多几栏，默认 4。</summary>
    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
        nameof(MaxColumns), typeof(int), typeof(Descriptions), new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>标签与值的排列，默认 horizontal。</summary>
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(Layout), typeof(DescriptionsLayout), typeof(Descriptions), new FrameworkPropertyMetadata(default(DescriptionsLayout), (d, _) => ((Descriptions)d).Rebuild()));

    /// <summary>带边框（表格样式）：标签有底色，各项之间有分隔线。</summary>
    public static readonly DependencyProperty BorderedProperty = DependencyProperty.Register(
        nameof(Bordered), typeof(bool), typeof(Descriptions), new FrameworkPropertyMetadata(false, (d, _) => ((Descriptions)d).Rebuild()));

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(Descriptions), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    readonly Flex root = new() { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Gap = Space.Md };
    readonly Text title = new() { Name = "PART_Title", Visibility = Visibility.Collapsed };
    readonly Border list = new() { Name = "PART_List" };
    readonly Grid grid = new();
    readonly Flex inline = new() { Wrap = true, ColumnGap = Space.Xl, RowGap = Space.Xs };
    readonly List<(DescriptionItem Item, GridItem Cell)> built = [];
    int columns = 1;

    static Descriptions()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Descriptions), new FrameworkPropertyMetadata(typeof(Descriptions)));
        FocusableProperty.OverrideMetadata(typeof(Descriptions), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Descriptions), new FrameworkPropertyMetadata(false));
        FontSizeProperty.OverrideMetadata(typeof(Descriptions), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((Descriptions)d).UpdateMetrics()));
    }

    /// <summary>新建：没有项。</summary>
    public Descriptions()
    {
        title.SetResourceReference(StyleProperty, DescriptionsParts.Title);
        list.SetResourceReference(StyleProperty, DescriptionsParts.List);
        root.Children.Add(title);
        root.Children.Add(list);
        Link.SetUnderlineScope(this, LinkUnderline.Hover);
        Items.CollectionChanged += (_, _) => Rebuild();
        AddVisualChild(root);
        AddLogicalChild(root);
        Rebuild();
    }

    /// <summary>各项。</summary>
    public ObservableCollection<DescriptionItem> Items { get; } = [];

    /// <summary>标题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>分几栏。</summary>
    public DescriptionsColumns Columns { get => (DescriptionsColumns)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>auto 时每栏的最小宽度。</summary>
    public double MinItemWidth { get => (double)GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }

    /// <summary>auto 时最多几栏。</summary>
    public int MaxColumns { get => (int)GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }

    /// <summary>标签与值的排列。</summary>
    public DescriptionsLayout Layout { get => (DescriptionsLayout)GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <summary>带边框。</summary>
    public bool Bordered { get => (bool)GetValue(BorderedProperty); set => SetValue(BorderedProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>当前的栏数（测试用）。</summary>
    internal int ColumnCount => columns;

    /// <summary>各项的格子（测试用）。</summary>
    internal IReadOnlyList<GridItem> Cells => [.. built.Select(entry => entry.Cell)];

    DescriptionsLayout ShownLayout => Layout.Value is null ? DescriptionsLayout.Horizontal : Layout;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <summary>档位的字号在主题样式的触发器里；写成像素数时是本地值。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TextSize)e.NewValue).Pixels is { } pixels) d.SetValue(FontSizeProperty, pixels);
        else if (((TextSize)e.OldValue).Pixels is not null) d.ClearValue(FontSizeProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == Provider.LangProperty) Rebuild();
    }

    void UpdateTitle()
    {
        title.Text = Title;
        title.Visibility = string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>按项重建：每一项是 [标签][值（与复制按钮）]，放进网格（行内时放进换行的一排）。</summary>
    void Rebuild()
    {
        // 拆开旧的格子：使用方给的标签、值（控件）要能挂到新的格子上
        foreach (var (_, cell) in built)
        {
            var row = (Flex)((Border)cell.Child).Child;
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
        list.Child = null;
        var layout = ShownLayout;
        var lang = Provider.GetLang(this);
        foreach (var item in Items)
        {
            var label = new Border { Name = "PART_Label", Child = Content(item.Label) };
            var value = new Border { Name = "PART_Value", Child = item.Copyable ? WithCopy(item, lang) : Content(item.Value) };
            label.SetResourceReference(StyleProperty, DescriptionsParts.Label);
            value.SetResourceReference(StyleProperty, DescriptionsParts.Value);
            var row = new Flex
            {
                Direction = layout == DescriptionsLayout.Vertical ? FlexDirection.Column : FlexDirection.Row,
                Align = Bordered || layout == DescriptionsLayout.Vertical ? FlexAlign.Stretch : FlexAlign.Baseline,
            };
            row.Children.Add(label);
            row.Children.Add(value);
            if (layout != DescriptionsLayout.Vertical) Flex.SetGrow(value, true);
            var cell = new ItemBorder(label) { Name = "PART_Item", Child = row };
            cell.SetResourceReference(StyleProperty, DescriptionsParts.Item);
            AutomationProperties.SetName(cell, PlainText(item.Label));
            if (Bordered)
            {
                // 边框样式的颜色：分隔线 borderMuted、标签格 fill 底（部件样式里写的盖不过，要改用 Provider 的主题）
                cell.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.Brush.BorderMuted);
                label.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.Fill);
            }
            var gridItem = new GridItem { Child = cell };
            built.Add((item, gridItem));
            if (layout == DescriptionsLayout.Inline) inline.Children.Add(gridItem);
            else grid.Children.Add(gridItem);
        }
        list.Child = layout == DescriptionsLayout.Inline ? inline : grid;
        if (Bordered) list.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.Brush.BorderMuted);
        else list.ClearValue(Border.BorderBrushProperty);
        UpdateMetrics();
    }

    /// <summary>值后面跟复制按钮：按钮紧跟在文字后面，文字换行时按钮在第一行。</summary>
    UIElement WithCopy(DescriptionItem item, Lang lang)
    {
        var copy = new CopyButton { Name = "PART_Copy", Size = TextSize.Xs, Value = item.CopyText ?? (item.Value is string or IFormattable ? Convert.ToString(item.Value, System.Globalization.CultureInfo.CurrentCulture) : "") };
        copy.SetResourceReference(StyleProperty, DescriptionsParts.Copy);
        AutomationProperties.SetName(copy, lang.DescriptionsCopy(PlainText(item.Label) is { Length: > 0 } label ? label : lang.Copy));
        var row = new CopyRow { Align = FlexAlign.Baseline, Gap = Space.Xs };
        row.Children.Add(Content(item.Value));
        row.Children.Add(copy);
        return row;
    }

    /// <summary>文字放进会换行的 Text；控件原样放。</summary>
    static UIElement Content(object? content) => content switch
    {
        UIElement element => element,
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
        var line = this.Resource<double>(ThemeKeys.BorderWidth.Xs);
        var layout = ShownLayout;
        title.FontSize = this.Resource<double>(ThemeKeys.FontSize.Md);
        (grid.ColumnGap, grid.RowGap) = Bordered ? (Space.None, Space.None) : (Space.Xl, Space.Md);
        list.BorderThickness = new Thickness(Bordered ? line : 0);
        list.CornerRadius = new CornerRadius(Bordered ? this.Resource<double>(ThemeKeys.Radius.Md) * em : 0);
        var padding = Bordered ? new Thickness(this.Resource<double>(ThemeKeys.Space.Md) * em, this.Resource<double>(ThemeKeys.Space.Sm) * em, this.Resource<double>(ThemeKeys.Space.Md) * em, this.Resource<double>(ThemeKeys.Space.Sm) * em) : default;
        foreach (var (_, gridItem) in built)
        {
            var cell = (Border)gridItem.Child;
            var row = (Flex)cell.Child;
            var (label, value) = ((Border)row.Children[0], (Border)row.Children[1]);
            row.Gap = Bordered ? Space.None : layout == DescriptionsLayout.Horizontal ? Space.Md : Space.Xs;
            label.Padding = value.Padding = padding;
            label.MinWidth = Bordered ? 8 * em : 0;
            // 复制按钮上下各收 0.25em：不把这一行撑高
            if (value.Child is CopyRow { Children: [_, CopyButton copy] }) copy.Margin = new Thickness(0, -0.25 * em, 0, -0.25 * em);
            // 每格画上、起始边的线，往回挪一条线宽叠在前一格的线上；外框由列表画，盖住第一行、第一列的线
            cell.BorderThickness = Bordered ? new Thickness(line, line, 0, 0) : default;
            cell.Margin = Bordered ? new Thickness(-line, -line, 0, 0) : default;
        }
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        var shown = Columns.Value is null ? (DescriptionsColumns)1 : Columns;
        var count = ShownLayout == DescriptionsLayout.Inline ? 1 : shown.Resolve(constraint.Width, FontSize, MinItemWidth, MaxColumns);
        if (count != columns || grid.Columns != count)
        {
            columns = count;
            grid.Columns = count;
        }
        foreach (var (item, gridItem) in built) gridItem.Span = Math.Clamp(item.Span, 1, count);
        root.Measure(constraint);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        root.Arrange(new Rect(arrangeBounds));
        // 边框样式按圆角裁掉格子（TS 的 overflow: hidden）：标签格的底色不盖住外框的圆角
        var radius = list.CornerRadius.TopLeft;
        list.Clip = Bordered ? new RectangleGeometry(new Rect(list.RenderSize), radius, radius) : null;
        return arrangeBounds;
    }

    /// <summary>值与复制按钮的一行（重建时只拆它，使用方自己的 Flex 不动）。</summary>
    sealed class CopyRow : Flex;

    /// <summary>一项：读屏里是一组，名称是标签；标签已经是名称，不再单独念。</summary>
    sealed class ItemBorder(UIElement label) : Border
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new ItemPeer(this, label);

        sealed class ItemPeer(ItemBorder owner, UIElement label) : FrameworkElementAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

            protected override string GetClassNameCore() => "DescriptionItem";

            protected override List<AutomationPeer>? GetChildrenCore() =>
                base.GetChildrenCore()?.Where(peer => peer is not UIElementAutomationPeer { Owner: var element } || !IsWithin(element, label)).ToList();

            static bool IsWithin(DependencyObject element, UIElement ancestor) => element == ancestor || ancestor.IsAncestorOf(element);
        }
    }
}

/// <summary>描述列表里的一项（vue-tsx-pro 的 DescriptionItem）。</summary>
public class DescriptionItem
{
    /// <summary>名称：文字或元素。</summary>
    public object? Label { get; set; }

    /// <summary>值：文字、数字或元素。</summary>
    public object? Value { get; set; }

    /// <summary>占几列，默认 1；超过栏数时按栏数。</summary>
    public int Span { get; set; } = 1;

    /// <summary>值后面放复制按钮。</summary>
    public bool Copyable { get; set; }

    /// <summary>复制的文字；不写时是值的文字（值是文字或数字时）。</summary>
    public string? CopyText { get; set; }
}

/// <summary>Descriptions 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:DescriptionsParts.Label}" TargetType="Border"&gt;</c>。</summary>
public static class DescriptionsParts
{
    /// <summary>标题（TS 的 propsTitle）。</summary>
    public static readonly PartKey Title = new(typeof(Descriptions), nameof(Title), typeof(Text));

    /// <summary>列表（TS 的 propsList）。</summary>
    public static readonly PartKey List = new(typeof(Descriptions), nameof(List), typeof(Border));

    /// <summary>一项（TS 的 propsItem）。</summary>
    public static readonly PartKey Item = new(typeof(Descriptions), nameof(Item), typeof(Border));

    /// <summary>名称（TS 的 propsLabel）。</summary>
    public static readonly PartKey Label = new(typeof(Descriptions), nameof(Label), typeof(Border));

    /// <summary>值（TS 的 propsValue）。</summary>
    public static readonly PartKey Value = new(typeof(Descriptions), nameof(Value), typeof(Border));

    /// <summary>复制按钮（TS 的 propsCopy）。</summary>
    public static readonly PartKey Copy = new(typeof(Descriptions), nameof(Copy), typeof(CopyButton));
}
