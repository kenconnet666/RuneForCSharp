using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 文字表格（vue-tsx-pro docs-kit 的 TextTable）：说明文档里的对照表，表头和单元格都是文字，反引号里的是行内代码（<see cref="CodeTextControl"/>）。
/// <code>
/// var table = new TextTableControl { Head = ["方法", "含义"], TextRows = [["`Has(值)`", "含有这个值"]], Dock = DockStyle.Top };
/// </code>
/// 本身就是 <see cref="TableControl"/>（外观属性同 Table），按 <see cref="Head"/>、<see cref="TextRows"/> 生成表头与各行；第一列通常是名字，默认不折行。
/// </summary>
public class TextTableControl : TableControl
{
    /// <summary>表头。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Head { get; set { field = value; Rebuild(); } } = [];

    /// <summary>各行：每格一段文字（TS 的 rows）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<IReadOnlyList<string>> TextRows { get; set { field = value; Rebuild(); } } = [];

    /// <summary>第一列不折行，默认是。</summary>
    [Category("Layout")]
    [Description("第一列不折行。")]
    [DefaultValue(true)]
    public bool NowrapFirst { get; set { field = value; Rebuild(); } } = true;

    void Rebuild()
    {
        Controls.Cast<Control>().DisposeAll();
        var head = new TableRowControl();
        foreach (var cell in Head) head.Controls.Add(new TableHeaderCellControl { Text = cell });
        var body = new TableBodyControl();
        foreach (var row in TextRows)
        {
            var line = new TableRowControl();
            for (var index = 0; index < row.Count; index++)
            {
                var cell = new TableCellControl { Nowrap = index == 0 && NowrapFirst };
                cell.Controls.Add(new CodeTextControl { Source = row[index] });
                line.Controls.Add(cell);
            }
            body.Controls.Add(line);
        }
        Controls.Add(new TableHeadControl { Controls = { head } });
        Controls.Add(body);
    }
}
