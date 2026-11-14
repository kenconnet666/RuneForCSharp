using System.Windows;

namespace Rune.Wpf;

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
    public static readonly DependencyProperty HeadProperty = DependencyProperty.Register(nameof(Head), typeof(IReadOnlyList<string>), typeof(TextTable),
        new FrameworkPropertyMetadata(Array.Empty<string>(), (d, _) => ((TextTable)d).Rebuild()));

    /// <summary>各行：每格一段文字（TS 的 rows；Table 自己有 Rows）。</summary>
    public static readonly DependencyProperty TextRowsProperty = DependencyProperty.Register(nameof(TextRows), typeof(IReadOnlyList<IReadOnlyList<string>>), typeof(TextTable),
        new FrameworkPropertyMetadata(Array.Empty<IReadOnlyList<string>>(), (d, _) => ((TextTable)d).Rebuild()));

    /// <summary>第一列不折行，默认是。</summary>
    public static readonly DependencyProperty NowrapFirstProperty = DependencyProperty.Register(nameof(NowrapFirst), typeof(bool), typeof(TextTable),
        new FrameworkPropertyMetadata(true, (d, _) => ((TextTable)d).Rebuild()));

    /// <summary>表头。</summary>
    public IReadOnlyList<string> Head { get => (IReadOnlyList<string>)GetValue(HeadProperty); set => SetValue(HeadProperty, value); }

    /// <summary>各行。</summary>
    public IReadOnlyList<IReadOnlyList<string>> TextRows { get => (IReadOnlyList<IReadOnlyList<string>>)GetValue(TextRowsProperty); set => SetValue(TextRowsProperty, value); }

    /// <summary>第一列不折行。</summary>
    public bool NowrapFirst { get => (bool)GetValue(NowrapFirstProperty); set => SetValue(NowrapFirstProperty, value); }

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
        var section = new TableHead();
        section.Rows.Add(head);
        Sections.Clear();
        Sections.Add(section);
        Sections.Add(body);
    }
}
