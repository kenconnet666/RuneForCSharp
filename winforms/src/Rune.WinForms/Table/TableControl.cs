using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 静态表格（vue-tsx-pro 的 Table）：表格的各个部分各一个控件，内容直接写好（说明文字里的表格、属性表）。
/// <code>
/// var table = new TableControl { AccessibleName = "属性" };
/// var head = new TableHeadControl();
/// head.Controls.Add(new TableRowControl { Controls = { new TableHeaderCellControl { Text = "名称" }, new TableHeaderCellControl { Text = "默认值" } } });
/// table.Controls.AddRange([head, body]);
/// </code>
/// 外观：<see cref="Density"/>、<see cref="Variant"/>（surface / ghost）、<see cref="Striped"/>。列宽按内容分配（同 CSS 的 table-layout: auto），
/// 放不下时单元格换行；表头单元格不换行，<see cref="TableHeaderCellControl.ColumnWidth"/> 定宽。指针下的行 fillHover 底。
/// 部分、行、单元格都是子控件，位置由表格统一排；行自己画底色与分隔线。没做：最小宽度也放不下时的横向滚动（外面套 ScrollAreaControl）。
/// </summary>
[ToolboxItem(true)]
[Description("静态表格：表头、表体、表尾里放行，行里放单元格。")]
public class TableControl : Control
{
    /// <summary>新建：外框与底色自己画。</summary>
    public TableControl()
    {
        AutoSize = true;
        AccessibleRole = AccessibleRole.Table;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    /// <summary>密度，默认 standard。</summary>
    [Category("Appearance")]
    public TableDensity Density
    {
        get => field.Value is null ? TableDensity.Standard : field;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>样式，默认 surface。</summary>
    [Category("Appearance")]
    public TableVariant Variant
    {
        get => field.Value is null ? TableVariant.Surface : field;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>斑马纹：表体的偶数行 fill 底。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool Striped
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    bool ShouldSerializeDensity() => Density != TableDensity.Standard;

    bool ShouldSerializeVariant() => Variant != TableVariant.Surface;

    /// <summary>所有行，按表头、表体、表尾的顺序。</summary>
    [Browsable(false)]
    public IReadOnlyList<(TableSectionControl Section, TableRowControl Row)> Rows =>
        [.. Controls.OfType<TableSectionControl>().OrderBy(section => section.Order).SelectMany(section => section.Controls.OfType<TableRowControl>().Select(row => (section, row)))];

    TableCaptionControl? Caption => Controls.OfType<TableCaptionControl>().FirstOrDefault();

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    /// <summary>外框的宽：surface 1 像素，ghost 没有。</summary>
    int Border => Variant == TableVariant.Ghost ? 0 : LogicalToDeviceUnits(1);

    /// <summary>外框里面的圆角矩形（行的底色按它裁，lg 圆角）。</summary>
    internal GraphicsPath InnerPath(Point offset)
    {
        var radius = Variant == TableVariant.Ghost ? 0 : ProviderControl.ThemeOf(this).Radii.Lg * Em - Border;
        return Drawing.RoundedRectangle(new RectangleF(Border - offset.X, Border - offset.Y, Width - 2 * Border, Height - 2 * Border), Math.Max(0, radius));
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Sync();
    }

    /// <summary>行、单元格变了：斑马纹、单元格按所在的部分换外观，重新排。</summary>
    internal void Sync()
    {
        var bodyIndex = 0;
        foreach (var (section, row) in Rows)
        {
            row.Apply(this, section, Striped && section is TableBodyControl && bodyIndex++ % 2 == 1);
            foreach (var cell in row.Controls.OfType<TableCellControl>()) cell.Apply(this, section);
        }
        Caption?.Apply(this);
        PerformLayout();
        Parent?.PerformLayout();
        Invalidate(true);
    }

    /// <summary>字号：紧凑时 sm，其余跟着窗体。</summary>
    internal float FontPixels => Density == TableDensity.Compact ? Tokens.FontSize.Sm * DeviceDpi / 96f : Em;

    /// <summary>单元格的内边距：左右 space.md，上下按密度（都按单元格自己的字号）。</summary>
    internal Padding CellPadding(TableCellControl cell)
    {
        var em = cell.Font.SizeInPoints * DeviceDpi / 72f;
        var block = Density == TableDensity.Compact ? 0.25f : Density == TableDensity.Comfortable ? 0.75f : 0.5f;
        var (x, y) = ((int)MathF.Round(0.75f * em), (int)MathF.Round(block * em));
        return new Padding(x, y, x, y);
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : Width;
        return new Size(width, Arrange(width, place: false));
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Arrange(Width, place: true);
    }

    /// <summary>排版：标题在上，下面各部分、各行；列宽按内容比例分满宽度。返回总高度。</summary>
    int Arrange(int width, bool place)
    {
        var border = Border;
        var inner = Math.Max(0, width - 2 * border);
        var y = border;
        if (Caption is { Visible: true } caption)
        {
            var height = caption.GetPreferredSize(new Size(inner, 0)).Height;
            if (place) caption.Bounds = new Rectangle(border, y, inner, height);
            y += height;
        }
        var rows = Rows;
        var columns = Columns(rows, inner);
        var line = LogicalToDeviceUnits(1);
        foreach (var group in rows.GroupBy(item => item.Section))
        {
            var sectionTop = y;
            foreach (var (section, row) in group)
            {
                var cells = row.Controls.OfType<TableCellControl>().ToArray();
                var height = 0;
                for (var i = 0; i < cells.Length && i < columns.Length; i++)
                {
                    var padding = CellPadding(cells[i]);
                    height = Math.Max(height, cells[i].ContentHeight(columns[i].Width - padding.Horizontal) + padding.Vertical + line);
                }
                if (place)
                {
                    row.Bounds = new Rectangle(0, y - sectionTop, inner, height);
                    // 表尾的分隔线在上边，其余在下边
                    var top = section is TableFootControl ? line : 0;
                    for (var i = 0; i < cells.Length; i++)
                    {
                        if (i >= columns.Length)
                        {
                            cells[i].Bounds = Rectangle.Empty;
                            continue;
                        }
                        var padding = CellPadding(cells[i]);
                        cells[i].Bounds = new Rectangle(columns[i].X + padding.Left, padding.Top + top,
                            Math.Max(0, columns[i].Width - padding.Horizontal), Math.Max(0, height - padding.Vertical - line));
                    }
                }
                y += height;
            }
            if (place) group.Key.Bounds = new Rectangle(border, sectionTop, inner, y - sectionTop);
        }
        return y + border;
    }

    /// <summary>
    /// 列的起点与宽：CSS 的 table-layout: auto（表格宽 100%）。放得下时按各列内容的比例分多出来的宽度；放不下时按最小宽度插值；
    /// 再放不下时最小宽度。表头单元格写了 ColumnWidth 的列定宽。边界按累计值取整，各列加起来正好是表格宽。
    /// </summary>
    (int X, int Width)[] Columns(IReadOnlyList<(TableSectionControl Section, TableRowControl Row)> rows, int available)
    {
        var count = rows.Count == 0 ? 0 : rows.Max(item => item.Row.Controls.OfType<TableCellControl>().Count());
        var max = new float[count];
        var min = new float[count];
        var fixedWidths = new float?[count];
        foreach (var (section, row) in rows)
        {
            var cells = row.Controls.OfType<TableCellControl>().ToArray();
            for (var i = 0; i < cells.Length; i++)
            {
                var padding = CellPadding(cells[i]).Horizontal;
                var (least, most) = cells[i].ContentWidths(cells[i].Nowrap || (cells[i] is TableHeaderCellControl && section is TableHeadControl));
                max[i] = Math.Max(max[i], most + padding);
                min[i] = Math.Max(min[i], least + padding);
                if (cells[i] is TableHeaderCellControl { ColumnWidth: { } width }) fixedWidths[i] = LogicalToDeviceUnits(width);
            }
        }
        for (var i = 0; i < count; i++)
            if (fixedWidths[i] is { } width) max[i] = min[i] = width;
        var (sumMax, sumMin) = (max.Sum(), min.Sum());
        var result = new (int X, int Width)[count];
        var x = 0f;
        for (var i = 0; i < count; i++)
        {
            var width = sumMax <= available ? max[i] + (sumMax > 0 ? (available - sumMax) * max[i] / sumMax : 0)
                : sumMin <= available ? min[i] + (available - sumMin) * (max[i] - min[i]) / Math.Max(1, sumMax - sumMin)
                : min[i];
            var (left, right) = ((int)MathF.Round(x), (int)MathF.Round(x + width));
            result[i] = (left, right - left);
            x += width;
        }
        return result;
    }

    /// <summary>外框：surface 时 border 边框、lg 圆角、surface 底；ghost 时页面底色、没有边框。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        // 像素 k 占 [k, k+1]：整数坐标的边线正好盖满像素，不发虚
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var ghost = Variant == TableVariant.Ghost;
        using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), ghost ? 0 : theme.Radii.Lg * Em);
        using var fill = new SolidBrush(ghost ? theme.Colors.Background : theme.Colors.Surface);
        graphics.FillPath(fill, path);
        if (ghost) return;
        var border = Border;
        using var outline = Drawing.RoundedRectangle(new RectangleF(border / 2f, border / 2f, Width - border, Height - border), theme.Radii.Lg * Em - border / 2f);
        using var pen = new Pen(theme.Colors.Border, border);
        graphics.DrawPath(pen, outline);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new TableAccessibleObject(this);

    sealed class TableAccessibleObject(TableControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name : owner.Caption?.Text;
    }
}

/// <summary>表格标题（caption）：同时是表格的读屏名称；在表格上方，textMuted、sm，上下 space.sm、左右 space.md。文字写 Text。</summary>
[ToolboxItem(true)]
[Description("表格标题：在表格上方，也是表格的读屏名称。")]
public class TableCaptionControl : Control
{
    /// <summary>新建：透明底、自己画字。</summary>
    public TableCaptionControl()
    {
        AccessibleRole = AccessibleRole.StaticText;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak;

    /// <summary>sm 字号。</summary>
    internal void Apply(TableControl table) => Font = Fonts.Get(table.Font.FontFamily.Name, Tokens.FontSize.Sm * 72 / 96, table.Font.Style);

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        (Parent as TableControl)?.Sync();
    }

    /// <summary>高：行数 × 行高 normal，加上下 space.sm。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var (x, y) = ((int)MathF.Round(0.75f * Em), (int)MathF.Round(0.5f * Em));
        var width = proposedSize.Width > 0 ? proposedSize.Width : Width;
        return new Size(width, TableCellControl.TextHeight(Text, Font, Math.Max(1, width - 2 * x), Flags, Em) + 2 * y);
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var (x, y) = ((int)MathF.Round(0.75f * Em), (int)MathF.Round(0.5f * Em));
        TableCellControl.DrawText(e.Graphics, Text, Font, new Rectangle(x, y, Width - 2 * x, Height - 2 * y), ProviderControl.ThemeOf(this).Colors.TextMuted, Flags, Em);
    }
}

/// <summary>表格的一个部分：表头、表体、表尾里放行（<see cref="TableRowControl"/>）。位置由表格排。</summary>
public abstract class TableSectionControl : Control
{
    /// <summary>新建：透明，行盖满它。</summary>
    protected TableSectionControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    /// <summary>在表格里的先后：表头、表体、表尾。</summary>
    internal abstract int Order { get; }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        (Parent as TableControl)?.Sync();
    }

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        (Parent as TableControl)?.Sync();
    }
}

/// <summary>表头（thead）：列标题。</summary>
[ToolboxItem(true)]
[Description("表头：列标题所在的行。")]
public class TableHeadControl : TableSectionControl
{
    internal override int Order => 0;
}

/// <summary>表体（tbody）。</summary>
[ToolboxItem(true)]
[Description("表体：数据行。")]
public class TableBodyControl : TableSectionControl
{
    internal override int Order => 1;
}

/// <summary>表尾（tfoot）：合计行。</summary>
[ToolboxItem(true)]
[Description("表尾：合计行，加粗、上边一道分隔线。")]
public class TableFootControl : TableSectionControl
{
    internal override int Order => 2;
}

/// <summary>
/// 行（tr）：底色——表头 fill；表体透出表格（斑马纹时隔行 fill，指针下的行 fillHover）；分隔线——表头下 border，表体行下 borderMuted，表尾上 border。
/// 底色按表格的圆角裁。
/// </summary>
[ToolboxItem(true)]
[Description("行：里面放单元格。")]
public class TableRowControl : Control
{
    TableControl? table;
    TableSectionControl? section;
    bool hovered;

    /// <summary>新建：透明底（先画出表格），自己画底色与分隔线。</summary>
    public TableRowControl()
    {
        AccessibleRole = AccessibleRole.Row;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    /// <summary>斑马纹的浅色行。</summary>
    [Browsable(false)]
    public bool IsStriped { get; private set; }

    internal void Apply(TableControl owner, TableSectionControl part, bool striped)
    {
        (table, section, IsStriped) = (owner, part, striped);
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        // 指针在单元格上也算在行上
        e.Control!.MouseEnter += (_, _) => SetHovered(true);
        e.Control.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        (Parent?.Parent as TableControl)?.Sync();
    }

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        (Parent?.Parent as TableControl)?.Sync();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        SetHovered(true);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
    }

    void SetHovered(bool value)
    {
        if (hovered == value) return;
        hovered = value;
        if (section is TableBodyControl) Invalidate(true);
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (table is null) return;
        var colors = ProviderControl.ThemeOf(this).Colors;
        var graphics = e.Graphics;
        var offset = table.PointToClient(PointToScreen(Point.Empty));
        using var clip = table.InnerPath(new Point(offset.X, offset.Y));
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.SetClip(clip, CombineMode.Intersect);
        Color? background = section is TableHeadControl ? colors.Fill
            : hovered && section is TableBodyControl ? colors.FillHover
            : IsStriped ? colors.Fill : null;
        if (background is { } color)
        {
            using var fill = new SolidBrush(color);
            graphics.FillRectangle(fill, ClientRectangle);
        }
        var line = LogicalToDeviceUnits(1);
        using var brush = new SolidBrush(section is TableBodyControl ? colors.BorderMuted : colors.Border);
        graphics.FillRectangle(brush, section is TableFootControl ? new Rectangle(0, 0, Width, line) : new Rectangle(0, Height - line, Width, line));
    }
}

/// <summary>
/// 单元格（td）：左右 space.md、上下按密度；对齐按 <see cref="Align"/>；默认可以换行，<see cref="Nowrap"/> 时一行（超出时省略号）。
/// 里面是文字（<see cref="Control.Text"/>，行高 normal）或子控件（横排、竖直居中）。
/// </summary>
[ToolboxItem(true)]
[Description("单元格：里面放文字或子控件。")]
public class TableCellControl : Control
{
    /// <summary>新建：透明底（透出行）。</summary>
    public TableCellControl()
    {
        AccessibleRole = AccessibleRole.Cell;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    /// <summary>对齐，默认 start；数字、金额用 end。</summary>
    [Category("Appearance")]
    public TableAlign Align
    {
        get => field.Value is null ? TableAlign.Start : field;
        set
        {
            field = value;
            PerformLayout();
            Invalidate();
        }
    }

    /// <summary>不换行（超出时省略号）。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool Nowrap
    {
        get;
        set
        {
            field = value;
            Table?.Sync();
        }
    }

    bool ShouldSerializeAlign() => Align != TableAlign.Start;

    /// <summary>表头 textMuted，其余 text；里面的子控件（文字）跟着它。</summary>
    public override Color ForeColor
    {
        get
        {
            var colors = ProviderControl.ThemeOf(this).Colors;
            return SectionKind == "head" ? colors.TextMuted : colors.Text;
        }
        set => base.ForeColor = value;
    }

    /// <summary>所在的部分：head、body、foot。</summary>
    [Browsable(false)]
    public string SectionKind { get; private set; } = "body";

    TableControl? Table => Parent?.Parent?.Parent as TableControl;

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    bool Wraps => !Nowrap && !(this is TableHeaderCellControl && SectionKind == "head");

    TextFormatFlags Flags
    {
        get
        {
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | (Wraps ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
            return flags | (Align == TableAlign.Center ? TextFormatFlags.HorizontalCenter : Align == TableAlign.End ? TextFormatFlags.Right : TextFormatFlags.Left);
        }
    }

    /// <summary>按所在的部分设字体：表头 sm、中等字重（GDI 按常规画）；表尾加粗；表体里的行标题中等字重（同上，按常规）。</summary>
    internal void Apply(TableControl table, TableSectionControl section)
    {
        SectionKind = section is TableHeadControl ? "head" : section is TableFootControl ? "foot" : "body";
        var points = SectionKind == "head" ? Tokens.FontSize.Sm * 72 / 96 : table.FontPixels * 72 / DeviceDpi;
        var style = SectionKind == "foot" ? table.Font.Style | FontStyle.Bold : table.Font.Style & ~FontStyle.Bold;
        Font = Fonts.Get(table.Font.FontFamily.Name, points, style);
        AccessibleRole = this is not TableHeaderCellControl ? AccessibleRole.Cell : SectionKind == "head" ? AccessibleRole.ColumnHeader : AccessibleRole.RowHeader;
        OnForeColorChanged(EventArgs.Empty);
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Table?.Sync();
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        Table?.Sync();
    }

    /// <summary>内容的最小宽度（能换行时最长的一段）与最大宽度（不换行）。</summary>
    internal (float Min, float Max) ContentWidths(bool singleLine)
    {
        if (Controls.Count > 0)
        {
            var width = Children().Sum(child => child.GetPreferredSize(Size.Empty).Width);
            return (width, width);
        }
        if (Text.Length == 0) return (0, 0);
        var max = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        var min = singleLine ? max : TextRenderer.MeasureText(Text, Font, new Size(1, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak).Width;
        return (min, max);
    }

    /// <summary>给定宽度时内容的高：文字按行数 × 行高 normal；子控件取最高的，至少一行字高。</summary>
    internal int ContentHeight(int width)
    {
        var line = (int)MathF.Round(1.5f * Em);
        if (Controls.Count > 0) return Math.Max(line, Children().Select(child => child.GetPreferredSize(Size.Empty).Height).DefaultIfEmpty(0).Max());
        return TextHeight(Text, Font, Math.Max(1, width), Flags, Em);
    }

    IEnumerable<Control> Children() => Controls.Cast<Control>().Where(child => child.Visible);

    /// <summary>子控件横排、竖直居中，按 <see cref="Align"/> 靠边。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var children = Children().Select(child => (Child: child, Size: child.GetPreferredSize(Size.Empty))).ToArray();
        var total = children.Sum(item => item.Size.Width);
        var x = Align == TableAlign.Center ? (Width - total) / 2 : Align == TableAlign.End ? Width - total : 0;
        foreach (var (child, size) in children)
        {
            child.Bounds = new Rectangle(x, (Height - size.Height) / 2, size.Width, size.Height);
            x += size.Width;
        }
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Controls.Count > 0 || Text.Length == 0) return;
        var color = Enabled ? ForeColor : ProviderControl.ThemeOf(this).Colors.TextDisabled;
        var height = TextHeight(Text, Font, Width, Flags, Em);
        DrawText(e.Graphics, Text, Font, new Rectangle(0, (Height - height) / 2, Width, height), color, Flags, Em);
    }

    /// <summary>文字的高：行数 × 行高 normal（1.5em，GDI 的行距更小）。</summary>
    internal static int TextHeight(string text, Font font, int width, TextFormatFlags flags, float em)
    {
        var lines = text.Length == 0 ? 1 : Math.Max(1, (int)MathF.Round((float)TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue), flags).Height / Fonts.LineHeight(font)));
        return lines * (int)MathF.Round(1.5f * em);
    }

    /// <summary>按行高 normal 画：GDI 按自己的行距排多行，这里把它放在行高的盒子里竖直居中（一行时与浏览器一致）。</summary>
    internal static void DrawText(Graphics graphics, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags, float em)
    {
        var measured = TextRenderer.MeasureText(text, font, new Size(Math.Max(1, bounds.Width), int.MaxValue), flags).Height;
        var box = TextHeight(text, font, bounds.Width, flags, em);
        var top = bounds.Y + (box - measured) / 2;
        TextRenderer.DrawText(graphics, text, font, new Rectangle(bounds.X, top, bounds.Width, Math.Max(measured, bounds.Bottom - top)), color, flags);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new CellAccessibleObject(this);

    sealed class CellAccessibleObject(TableCellControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name
            : owner.Text is { Length: > 0 } text ? text : string.Join(" ", owner.Controls.Cast<Control>().Select(child => child.AccessibilityObject.Name).Where(text => text is { Length: > 0 }));
    }
}

/// <summary>表头单元格（th）：表头里是列标题（fill 底、textMuted、sm、不换行），表体与表尾里是行标题。</summary>
[ToolboxItem(true)]
[Description("表头单元格：表头里是列标题，表体与表尾里是行标题。")]
public class TableHeaderCellControl : TableCellControl
{
    /// <summary>列宽（逻辑像素）：日期、数字、操作这类按内容定宽的列；不写的列分剩下的（TS 的 width：控件自己有 Width）。</summary>
    [Category("Layout")]
    [DefaultValue(null)]
    public int? ColumnWidth { get; set; }

    /// <summary>对应的方向（TS 的 scope）：col（列标题）或 row（行标题）；不写时按所在的部分。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Scope { get; set; }
}
