using Avalonia;

namespace Rune.Avalonia;

/// <summary>
/// 文字表格（vue-tsx-pro docs-kit 的 TextTable）：说明文档里的对照表，表头和单元格都是文字，反引号里的是行内代码（<see cref="CodeText"/>）。
/// <code>
/// table.Head = ["方法", "含义"];
/// table.TextRows = [["`Has(值)`", "含有这个值"]];
/// </code>
/// 本身就是 <see cref="Table"/>（外观属性同 Table），按 <see cref="Head"/>、<see cref="TextRows"/> 生成表头与各行；第一列通常是名字，默认不折行。
/// 按列定义、要排序或合并单元格的表格用 ColumnTable、DataTable。
/// </summary>
public class TextTable : Table
{
    /// <summary>表头。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> HeadProperty = AvaloniaProperty.Register<TextTable, IReadOnlyList<string>>(nameof(Head), []);

    /// <summary>各行：每格一段文字。</summary>
    public static readonly StyledProperty<IReadOnlyList<IReadOnlyList<string>>> TextRowsProperty =
        AvaloniaProperty.Register<TextTable, IReadOnlyList<IReadOnlyList<string>>>(nameof(TextRows), []);

    /// <summary>第一列不折行，默认是。</summary>
    public static readonly StyledProperty<bool> NowrapFirstProperty = AvaloniaProperty.Register<TextTable, bool>(nameof(NowrapFirst), true);

    /// <summary>表头。</summary>
    public IReadOnlyList<string> Head { get => GetValue(HeadProperty); set => SetValue(HeadProperty, value); }

    /// <summary>各行（TS 的 rows；Table 自己有 Rows）。</summary>
    public IReadOnlyList<IReadOnlyList<string>> TextRows { get => GetValue(TextRowsProperty); set => SetValue(TextRowsProperty, value); }

    /// <summary>第一列不折行。</summary>
    public bool NowrapFirst { get => GetValue(NowrapFirstProperty); set => SetValue(NowrapFirstProperty, value); }

    /// <summary>Table 的类型键：TextTable 用 Table 的主题。</summary>
    protected override Type StyleKeyOverride => typeof(Table);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeadProperty || change.Property == TextRowsProperty || change.Property == NowrapFirstProperty) Rebuild();
    }

    void Rebuild()
    {
        var head = new TableRow();
        foreach (var cell in Head) head.Cells.Add(new TableHeaderCell { Content = cell });
        var body = new TableBody();
        foreach (var row in TextRows)
        {
            var line = new TableRow();
            for (var index = 0; index < row.Count; index++)
                line.Cells.Add(new TableCell { Nowrap = index == 0 && NowrapFirst, Content = new CodeText { Source = row[index] } });
            body.Rows.Add(line);
        }
        Sections.Clear();
        Sections.Add(new TableHead { Rows = { head } });
        Sections.Add(body);
    }
}
