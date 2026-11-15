using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using LayoutGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia;

/// <summary>
/// 字段对比（vue-tsx-pro 的 FieldCompare）：两份记录（编辑前后、测算的两个版本、审批前后）按字段列出原值与新值，数值字段带上变化量与变化率。
/// <code>
/// &lt;rune:FieldCompare Fields="{Binding QuoteFields}" Before="{Binding V1}" After="{Binding V2}" /&gt;                      表格：字段、原值、新值、变化
/// &lt;rune:FieldCompare Fields="{Binding QuoteFields}" Before="{Binding Saved}" After="{Binding Draft}" Layout="List" /&gt;   列表：只列改了的
/// </code>
/// 字段是 <see cref="CompareField"/>：键（取同名属性或字典的键）、显示名、取值函数、数值格式（<see cref="NumericFormat"/>：金额、百分比、缩放）、状态映射、好坏。
/// 表格默认列出全部字段（没改的淡显，改了的新值中等字重），列表默认只列改了的；<see cref="OnlyChanged"/> 可以改。数值的变化量按字段的格式、带正负号，
/// 后面括号里是变化率（百分比字段没有），写了 Better 时按好坏着色。没有原记录（新建）时列表只有新值；没有改动时显示“没有改动”。
/// 列表的值是 <see cref="ValueChange"/>（原值划掉、箭头、新值，读屏念“改为”）。计算在三个库共用的 <see cref="FieldComparison"/> 里。
/// </summary>
public class FieldCompare : Control
{
    /// <summary>对比哪些字段，按这个顺序。</summary>
    public static readonly StyledProperty<IReadOnlyList<CompareField>?> FieldsProperty = AvaloniaProperty.Register<FieldCompare, IReadOnlyList<CompareField>?>(nameof(Fields));

    /// <summary>原来的记录；没有（新建）时每个字段都当作空。</summary>
    public static readonly StyledProperty<object?> BeforeProperty = AvaloniaProperty.Register<FieldCompare, object?>(nameof(Before));

    /// <summary>现在的记录。</summary>
    public static readonly StyledProperty<object?> AfterProperty = AvaloniaProperty.Register<FieldCompare, object?>(nameof(After));

    /// <summary>排法，默认表格。</summary>
    public static readonly StyledProperty<FieldCompareLayout> LayoutProperty = AvaloniaProperty.Register<FieldCompare, FieldCompareLayout>(nameof(Layout));

    /// <summary>只列改了的字段；默认表格列出全部、列表只列改了的。</summary>
    public static readonly StyledProperty<bool?> OnlyChangedProperty = AvaloniaProperty.Register<FieldCompare, bool?>(nameof(OnlyChanged));

    readonly ContentControl root = new();

    /// <summary>新建：没有字段时显示“没有改动”。</summary>
    public FieldCompare()
    {
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>对比哪些字段。</summary>
    public IReadOnlyList<CompareField>? Fields { get => GetValue(FieldsProperty); set => SetValue(FieldsProperty, value); }

    /// <summary>原来的记录。</summary>
    public object? Before { get => GetValue(BeforeProperty); set => SetValue(BeforeProperty, value); }

    /// <summary>现在的记录。</summary>
    public object? After { get => GetValue(AfterProperty); set => SetValue(AfterProperty, value); }

    /// <summary>排法。</summary>
    public FieldCompareLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <summary>只列改了的字段。</summary>
    public bool? OnlyChanged { get => GetValue(OnlyChangedProperty); set => SetValue(OnlyChangedProperty, value); }

    /// <summary>列出来的行（测试用）。</summary>
    internal IReadOnlyList<FieldCompareRow> Rows { get; private set; } = [];

    /// <summary>当前的内容：表格、列表或“没有改动”（测试用）。</summary>
    internal Control? Shown => root.Content as Control;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FieldsProperty || change.Property == BeforeProperty || change.Property == AfterProperty
            || change.Property == LayoutProperty || change.Property == OnlyChangedProperty || change.Property == TextElement.FontSizeProperty)
            Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    void Rebuild()
    {
        var all = FieldComparison.Diff(Fields ?? [], Before, After);
        Rows = OnlyChanged ?? Layout == FieldCompareLayout.List ? [.. all.Where(row => row.Changed)] : all;
        var lang = Provider.GetLang(this);
        root.Content = Rows.Count == 0 ? new Text { Text = lang.FieldCompareNone, Tone = TextTone.Muted }
            : Layout == FieldCompareLayout.List ? List(lang) : Table(lang);
    }

    /// <summary>一份记录里这个字段的显示：Cell 优先，状态是 StatusBadge，其余是按格式的文字。</summary>
    object? Display(FieldCompareRow row, int side, Lang lang)
    {
        var record = side == 0 ? Before : After;
        if (row.Field.Cell is { } cell && record is not null) return cell(record, side);
        var value = side == 0 ? row.Before : row.After;
        if (row.Field.Statuses is { } statuses && value is string key) return new StatusBadge { Statuses = statuses, Value = key };
        return FieldComparison.Text(row.Field, value, lang.Code, lang.Currency);
    }

    Text? Change(FieldCompareRow row, Lang lang) => FieldComparison.Change(row, lang.Code, lang.Currency) is { } change
        ? new Text { Text = change.Text, Tone = change.Tone, Size = TextSize.Sm, Numeric = true }
        : null;

    /// <summary>值放进单元格：文字套一层 Text（按要的色调、字重），控件原样。</summary>
    static object? Styled(object? content, TextTone tone, TextWeight weight) => content is string text ? new Text { Text = text, Tone = tone, Weight = weight } : content;

    /// <summary>表格：紧凑的 Table，字段、原值（淡显）、新值（改了的中等字重，没改的淡显）、变化（有数值改动时才有这一列）。</summary>
    Table Table(Lang lang)
    {
        var headers = lang.FieldCompareHeaders;
        var numeric = Rows.Any(row => row.Changed && row.Delta is not null);
        var head = new TableRow();
        foreach (var header in headers.Take(numeric ? 4 : 3)) head.Cells.Add(new TableHeaderCell { Content = header });
        var body = new TableBody();
        foreach (var row in Rows)
        {
            var line = new TableRow();
            line.Classes.Set("changed", row.Changed);
            line.Cells.Add(new TableCell { Content = row.Field.Header });
            line.Cells.Add(new TableCell { Content = Styled(Display(row, 0, lang), TextTone.Muted, default) });
            line.Cells.Add(new TableCell { Content = Styled(Display(row, 1, lang), row.Changed ? default : TextTone.Muted, row.Changed ? TextWeight.Medium : default) });
            if (numeric) line.Cells.Add(new TableCell { Content = Change(row, lang) });
            body.Rows.Add(line);
        }
        return new Table { Density = TableDensity.Compact, Sections = { new TableHead { Rows = { head } }, body } };
    }

    /// <summary>列表：字段名一列（muted）、值一列（原值 → 新值，后面跟变化），列间距 md、行间距 xs。</summary>
    LayoutGrid List(Lang lang)
    {
        var em = TextElement.GetFontSize(this);
        var grid = new LayoutGrid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = this.Resource<double>(ThemeKeys.Space.Md) * em,
            RowSpacing = this.Resource<double>(ThemeKeys.Space.Xs) * em,
        };
        for (var index = 0; index < Rows.Count; index++)
        {
            var row = Rows[index];
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = new Text { Text = row.Field.Header, Tone = TextTone.Muted };
            var value = new Flex { Align = FlexAlign.Baseline, Wrap = true, ColumnGap = Space.Xs };
            value.Children.Add(new ValueChange { Before = Before is null ? null : Display(row, 0, lang), After = Display(row, 1, lang) });
            if (Change(row, lang) is { } change) value.Children.Add(change);
            LayoutGrid.SetRow(label, index);
            LayoutGrid.SetRow(value, index);
            LayoutGrid.SetColumn(value, 1);
            grid.Children.AddRange([label, value]);
        }
        return grid;
    }

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
}
