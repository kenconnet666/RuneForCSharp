using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 按模型只读地显示一份值（vue-tsx-pro 的 SchemaView）：审批单的查看、打印、提交前的核对。标签与值排成 Descriptions，按分组各一块；
/// 选择题显示选项的文字（含登记的字典），开关、复选框显示是否，人员显示名字，附件显示文件名（有地址时是链接），
/// 嵌套的对象展开成"对象 · 字段"，对象的列表（列表、明细表）排成表格，写了合计的列有合计。权限与条件同 SchemaForm：不显示的不列。
/// <code>
/// new SchemaView { Values = order, Access = new Dictionary&lt;string, SchemaFieldAccess&gt; { ["Cost"] = SchemaFieldAccess.Hidden } }
/// </code>
/// 字段从 <see cref="Schema"/> 推（<see cref="SchemaModel"/>），不写时按 Values 的类型。部件：每一块是 <see cref="SchemaViewParts.Section"/>（Descriptions）。
/// </summary>
public class SchemaView : Control
{
    /// <summary>要显示的值（一个模型对象）。</summary>
    public static readonly StyledProperty<object?> ValuesProperty = AvaloniaProperty.Register<SchemaView, object?>(nameof(Values));

    /// <summary>生成字段的类型（上游的 schema）；不写时按 Values 的类型。</summary>
    public static readonly StyledProperty<Type?> SchemaProperty = AvaloniaProperty.Register<SchemaView, Type?>(nameof(Schema));

    /// <summary>字段的权限（按路径，同 SchemaForm）：Hidden 的不列。</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, SchemaFieldAccess>?> AccessProperty =
        AvaloniaProperty.Register<SchemaView, IReadOnlyDictionary<string, SchemaFieldAccess>?>(nameof(Access));

    /// <summary>只显示这些顶层字段，按这里的顺序。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> PickProperty = AvaloniaProperty.Register<SchemaView, IReadOnlyList<string>?>(nameof(Pick));

    /// <summary>不显示这些顶层字段。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> OmitProperty = AvaloniaProperty.Register<SchemaView, IReadOnlyList<string>?>(nameof(Omit));

    /// <summary>数据来源（字典的选项文字）：盖在 Provider 的 FormSources 之上。</summary>
    public static readonly StyledProperty<FormSources?> SourcesProperty = AvaloniaProperty.Register<SchemaView, FormSources?>(nameof(Sources));

    /// <summary>每一块（Descriptions）的栏数，默认按宽度自动（上游 propsDescriptions 的 columns）。</summary>
    public static readonly StyledProperty<DescriptionsColumns> ColumnsProperty = AvaloniaProperty.Register<SchemaView, DescriptionsColumns>(nameof(Columns), DescriptionsColumns.Auto);

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch };

    /// <summary>新建。</summary>
    public SchemaView()
    {
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>要显示的值。</summary>
    public object? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>生成字段的类型。</summary>
    public Type? Schema { get => GetValue(SchemaProperty); set => SetValue(SchemaProperty, value); }

    /// <summary>字段的权限。</summary>
    public IReadOnlyDictionary<string, SchemaFieldAccess>? Access { get => GetValue(AccessProperty); set => SetValue(AccessProperty, value); }

    /// <summary>只显示这些顶层字段。</summary>
    public IReadOnlyList<string>? Pick { get => GetValue(PickProperty); set => SetValue(PickProperty, value); }

    /// <summary>不显示这些顶层字段。</summary>
    public IReadOnlyList<string>? Omit { get => GetValue(OmitProperty); set => SetValue(OmitProperty, value); }

    /// <summary>数据来源。</summary>
    public FormSources? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <summary>每一块的栏数。</summary>
    public DescriptionsColumns Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>值改了（模型里的字段变了，没换对象）时重画。</summary>
    public void Refresh() => Render();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty || change.Property == SchemaProperty || change.Property == AccessProperty || change.Property == PickProperty
            || change.Property == OmitProperty || change.Property == SourcesProperty || change.Property == ColumnsProperty
            || change.Property == Provider.LangProperty || change.Property == Provider.FormSourcesProperty) Render();
    }

    void Render()
    {
        root.Children.Clear();
        if (Values is not { } values) return;
        var lang = Provider.GetLang(this);
        var sources = FormSources.Merge(Provider.GetFormSources(this), Sources);
        var fields = SchemaModel.Of(Schema ?? values.GetType());
        var states = new Dictionary<string, SchemaFieldState>();
        SchemaModel.Walk(fields, values, Access ?? new Dictionary<string, SchemaFieldAccess>(), (path, _, state) => states[path] = state);
        bool Visible(string path) => !states.TryGetValue(path, out var state) || state.Visible;

        IEnumerable<DescriptionItem> Items(SchemaField field, object? value, string path, string label)
        {
            if (!Visible(path)) return [];
            if (field.Control == SchemaControl.Object)
                return (field.Fields ?? []).SelectMany(child => Items(child, value is null ? null : FormController.Read(value, child.Key), $"{path}.{child.Key}", $"{label} · {child.Label}"));
            if (field.Control is SchemaControl.List or SchemaControl.Table)
            {
                var rows = value is IEnumerable list and not string ? list.Cast<object?>().ToList() : [];
                return [new DescriptionItem { Label = label, Value = TableOf(field, rows, Visible, path, sources, lang), Span = int.MaxValue }];
            }
            var span = field.Span is SchemaFieldAttribute.Full ? int.MaxValue : field.Span ?? 1;
            return [new DescriptionItem { Label = label, Value = Display(field, value, sources, lang), Span = span }];
        }

        // 分组各一块（页签里的字段也照分组排：只读、打印时一次看全）
        foreach (var group in SchemaModel.Pick(fields, Pick, Omit).GroupBy(field => field.Group ?? field.Tab))
        {
            var block = new Descriptions { Name = SchemaViewParts.Section, Columns = Columns, Title = group.Key };
            block.Items.AddRange(group.SelectMany(field => Items(field, FormController.Read(values, field.Key), field.Key, field.Label)));
            root.Children.Add(block);
        }
    }

    /// <summary>一个值显示成什么（列表、对象另排）：开关是"是 / 否"，选择题是选项的文字，人员是名字，附件是文件名（有地址时是外部链接），数字带单位，空的是占位。</summary>
    internal static object Display(SchemaField field, object? value, FormSources sources, Lang lang)
    {
        if (field.Control is SchemaControl.Switch or SchemaControl.Checkbox) return value is true ? lang.FormValueTrue : lang.FormValueFalse;
        if (SchemaModel.IsBlank(value)) return lang.FormValueEmpty;
        var items = value is IEnumerable list and not string ? list.Cast<object?>().ToList() : [value];
        switch (field.Control)
        {
            case SchemaControl.Select or SchemaControl.Radio or SchemaControl.Multiselect or SchemaControl.Checkboxes:
                return string.Join("、", items.Select(item => sources.OptionText(field, item)));
            case SchemaControl.People:
                return string.Join("、", items.Select(item => item is PeopleEntry person ? person.Name : Convert.ToString(item, System.Globalization.CultureInfo.CurrentCulture)));
            case SchemaControl.File:
                var files = new Flex { Align = FlexAlign.Center, Wrap = true };
                for (var index = 0; index < items.Count; index++)
                {
                    if (index > 0) files.Children.Add(new TextBlock { Text = "、" });
                    files.Children.Add(items[index] is UploadFile { Url: { } url } file
                        ? new Link { Href = url, External = true, Content = file.Name }
                        : new TextBlock { Text = items[index] is UploadFile named ? named.Name : Convert.ToString(items[index], System.Globalization.CultureInfo.CurrentCulture) });
                }
                return files;
            default:
                var text = Text(value);
                return field.Unit is { } unit ? $"{text} {unit}" : text;
        }
    }

    static string Text(object? value) => value switch
    {
        DateOnly date => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        DateTime time => time.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.CurrentCulture),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture) ?? "",
    };

    /// <summary>对象的列表排成表格：列是行里显示着的字段，写了合计的列靠结束边、有合计。</summary>
    static object TableOf(SchemaField field, IReadOnlyList<object?> rows, Func<string, bool> visible, string path, FormSources sources, Lang lang)
    {
        if (rows.Count == 0) return lang.FormValueEmpty;
        var columns = (field.Fields ?? []).Where(child => Enumerable.Range(0, rows.Count).Any(index => visible($"{path}.{index}.{child.Key}"))).ToList();
        TableAlign AlignOf(SchemaField column) => column.Sum ? TableAlign.End : TableAlign.Start;
        var head = new TableRow();
        foreach (var column in columns) head.Cells.Add(new TableHeaderCell { Align = AlignOf(column), Content = column.Label });
        var body = new TableBody();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = new TableRow();
            foreach (var column in columns)
                row.Cells.Add(new TableCell
                {
                    Align = AlignOf(column),
                    Content = visible($"{path}.{index}.{column.Key}") && rows[index] is { } item ? Display(column, FormController.Read(item, column.Key), sources, lang) : null,
                });
            body.Rows.Add(row);
        }
        var table = new Table { Sections = { new TableHead { Rows = { head } }, body } };
        AutomationProperties.SetName(table, field.Label);
        if (columns.Any(column => column.Sum))
        {
            var total = new TableRow();
            for (var at = 0; at < columns.Count; at++)
                total.Cells.Add(new TableCell
                {
                    Name = SchemaViewParts.TableTotal, Align = AlignOf(columns[at]), FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
                    Content = columns[at].Sum ? SchemaModel.ColumnTotal(columns[at], rows) : at == 0 ? lang.TableSummary : null,
                });
            table.Sections.Add(new TableFoot { Rows = { total } });
        }
        return table;
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

/// <summary>SchemaView 的部件名。</summary>
public static class SchemaViewParts
{
    /// <summary>每一块（Descriptions；TS 的 propsDescriptions）。</summary>
    public const string Section = "PART_Section";

    /// <summary>明细表合计行的格（TableCell，半粗、等宽数字）。</summary>
    public const string TableTotal = "PART_TableTotal";
}
