namespace Rune.Avalonia;

/// <summary>
/// grid 排版的计算（CSS grid 里 vue-tsx-pro 的 Grid 用到的部分）：列数、自动放置（逐行 / 逐列、dense 回填）、行高。
/// 只算数，量与摆由面板做。三个库各有一份，除命名空间外逐字相同（tools/twins）。
/// </summary>
static class GridLayout
{
    /// <summary>一项要占的格子：跨几列（0 是占满一整行）、跨几行。</summary>
    public readonly record struct Item(int Span, int RowSpan);

    /// <summary>放好的位置：第几行、第几列起，跨几列、几行。</summary>
    public readonly record struct Cell(int Row, int Column, int Span, int RowSpan);

    /// <summary>
    /// 列数：给了最小宽度时按宽度放得下几列（CSS 的 repeat(auto-fit, minmax(min(最小宽度, 100%), 1fr))），
    /// 否则是 columns；都没有时一列。宽度无穷时（横向不限的父元素）每项一列。
    /// </summary>
    public static int Columns(double width, double gap, int? columns, double? minItemWidth, int count)
    {
        if (minItemWidth is not { } min) return Math.Max(1, columns ?? 1);
        if (double.IsInfinity(width)) return Math.Max(1, count);
        return Math.Max(1, (int)Math.Floor((width + gap) / (Math.Min(min, width) + gap)));
    }

    /// <summary>
    /// 自动放置：逐行（或逐列）找第一个放得下的位置；dense 时每项都从头找（回填前面留下的空位，顺序可能变）。
    /// 跨列数不超过列数，0（full）占满一整行。逐列时行数按格子总数算（TS 里逐列排的网格都给了行数，这里没有 rows，按列数折算）。
    /// </summary>
    public static Cell[] Place(IReadOnlyList<Item> items, int columns, bool columnFlow, bool dense)
    {
        var cells = new Cell[items.Count];
        var taken = new HashSet<(int Row, int Column)>();
        var rows = columnFlow ? Math.Max(1, (int)Math.Ceiling(items.Sum(item => (double)Width(item, columns) * item.RowSpan) / columns)) : 0;
        var (row, column) = (0, 0);
        for (var i = 0; i < items.Count; i++)
        {
            var span = Width(items[i], columns);
            var rowSpan = Math.Max(1, items[i].RowSpan);
            if (dense) (row, column) = (0, 0);
            while (!Fits(row, column, span, rowSpan))
            {
                if (columnFlow)
                {
                    if (++row + rowSpan > rows) (row, column) = (0, column + 1);
                }
                else if (++column + span > columns) (row, column) = (row + 1, 0);
            }
            cells[i] = new Cell(row, column, span, rowSpan);
            for (var r = row; r < row + rowSpan; r++)
                for (var c = column; c < column + span; c++) taken.Add((r, c));
        }
        return cells;

        bool Fits(int row, int column, int span, int rowSpan)
        {
            // 逐行时不能超出列数；逐列时放不下就开新的列（CSS 的隐式列）
            if (!columnFlow && column + span > columns) return false;
            for (var r = row; r < row + rowSpan; r++)
                for (var c = column; c < column + span; c++)
                    if (taken.Contains((r, c))) return false;
            return true;
        }
    }

    static int Width(Item item, int columns) => item.Span <= 0 ? columns : Math.Min(item.Span, columns);

    /// <summary>用到了几列（auto-fit：项目比放得下的列数少时，空列收起）。</summary>
    public static int UsedColumns(IEnumerable<Cell> cells) => cells.Select(cell => cell.Column + cell.Span).DefaultIfEmpty(1).Max();

    /// <summary>
    /// 行高：只占一行的项取最高的；跨几行的项比它跨的几行（加上中间的间距）还高时，差的部分补给最后一行。
    /// </summary>
    public static double[] Rows(IReadOnlyList<Cell> cells, IReadOnlyList<double> heights, double gap)
    {
        var count = cells.Select(cell => cell.Row + cell.RowSpan).DefaultIfEmpty(0).Max();
        var rows = new double[count];
        for (var i = 0; i < cells.Count; i++)
            if (cells[i].RowSpan == 1) rows[cells[i].Row] = Math.Max(rows[cells[i].Row], heights[i]);
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].RowSpan == 1) continue;
            var last = cells[i].Row + cells[i].RowSpan - 1;
            var spanned = Enumerable.Range(cells[i].Row, cells[i].RowSpan).Sum(r => rows[r]) + gap * (cells[i].RowSpan - 1);
            if (heights[i] > spanned) rows[last] += heights[i] - spanned;
        }
        return rows;
    }

    /// <summary>列宽：等宽（minmax(0, 1fr)），宽度无穷时取 natural（各项按跨度摊下来的最大宽度）。</summary>
    public static double ColumnWidth(double width, double gap, int columns, double natural) =>
        double.IsInfinity(width) ? natural : Math.Max(0, (width - gap * (columns - 1)) / columns);

    /// <summary>格子里一项的位置与尺寸（align-items / justify-items）：stretch 填满格子。</summary>
    public static (double Offset, double Size) Align(double size, double cell, GridAlign align) => align.Value switch
    {
        "start" => (0, Math.Min(size, cell)),
        "center" => ((cell - Math.Min(size, cell)) / 2, Math.Min(size, cell)),
        "end" => (cell - Math.Min(size, cell), Math.Min(size, cell)),
        _ => (0, cell),
    };
}
