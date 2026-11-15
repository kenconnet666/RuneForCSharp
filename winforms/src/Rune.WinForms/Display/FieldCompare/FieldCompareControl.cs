using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 字段对比（vue-tsx-pro 的 FieldCompare）：两份记录（编辑前后、测算的两个版本、审批前后）按字段列出原值与新值，数值字段带上变化量与变化率。
/// <code>
/// var compare = new FieldCompareControl { Fields = quoteFields, Before = v1, After = v2 };                                       // 表格
/// var changes = new FieldCompareControl { Fields = quoteFields, Before = saved, After = draft, CompareLayout = FieldCompareLayout.List }; // 列表
/// </code>
/// 字段是 <see cref="CompareField"/>：键（取同名属性或字典的键）、显示名、取值函数、数值格式（<see cref="NumericFormat"/>：金额、百分比、缩放）、状态映射、好坏。
/// 表格默认列出全部字段（没改的淡显，改了的新值中等字重），列表默认只列改了的；<see cref="OnlyChanged"/> 可以改。数值的变化量按字段的格式、带正负号，
/// 后面括号里是变化率（百分比字段没有），写了 Better 时按好坏着色。没有原记录（新建）时列表只有新值；没有改动时显示“没有改动”。
/// 列表的值是 <see cref="ValueChangeControl"/>（原值划掉、箭头、新值，只放文字：状态显示名字）。计算在三个库共用的 <see cref="FieldComparison"/> 里。
/// </summary>
[Description("字段对比：两份记录按字段列出原值与新值，数值字段带变化量与变化率。")]
public class FieldCompareControl : Control
{
    Control? shown;

    /// <summary>新建：大小跟着内容。</summary>
    public FieldCompareControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        Rebuild();
    }

    /// <summary>对比哪些字段，按这个顺序。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<CompareField>? Fields { get; set { field = value; Rebuild(); } }

    /// <summary>原来的记录；没有（新建）时每个字段都当作空。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? Before { get; set { field = value; Rebuild(); } }

    /// <summary>现在的记录。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? After { get; set { field = value; Rebuild(); } }

    /// <summary>排法，默认表格（TS 的 layout；控件自己有 Layout 事件）。</summary>
    [Category("Appearance")]
    [Description("排法：Table 字段、原值、新值、变化四列；List 一行一个字段。")]
    [DefaultValue(FieldCompareLayout.Table)]
    public FieldCompareLayout CompareLayout { get; set { field = value; Rebuild(); } }

    /// <summary>只列改了的字段；默认表格列出全部、列表只列改了的。</summary>
    [Category("Behavior")]
    [Description("只列改了的字段；不写时表格列出全部、列表只列改了的。")]
    [DefaultValue(null)]
    public bool? OnlyChanged { get; set { field = value; Rebuild(); } }

    /// <summary>列出来的行（测试用）。</summary>
    internal IReadOnlyList<FieldCompareRow> Rows { get; private set; } = [];

    /// <summary>当前的内容：表格、列表或“没有改动”（测试用）。</summary>
    internal Control? Shown => shown;

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 语言包取自外层的作用域：放进界面后重新生成文字
        Rebuild();
    }

    void Rebuild()
    {
        var all = FieldComparison.Diff(Fields ?? [], Before, After);
        Rows = OnlyChanged ?? CompareLayout == FieldCompareLayout.List ? [.. all.Where(row => row.Changed)] : all;
        var lang = ProviderControl.LangOf(this);
        Control next = Rows.Count == 0 ? new TextControl { Text = lang.FieldCompareNone, Tone = TextTone.Muted }
            : CompareLayout == FieldCompareLayout.List ? List(lang) : Table(lang);
        SuspendLayout();
        if (shown is not null)
        {
            Controls.Remove(shown);
            shown.Dispose();
        }
        shown = next;
        shown.Dock = DockStyle.Top;
        Controls.Add(shown);
        ResumeLayout();
        Parent?.PerformLayout();
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => shown?.GetPreferredSize(proposedSize) ?? Size.Empty;

    /// <summary>一份记录里这个字段的显示：Cell 优先，状态是 StatusBadgeControl，其余是按格式的文字。</summary>
    object? Display(FieldCompareRow row, int side, Lang lang)
    {
        var record = side == 0 ? Before : After;
        if (row.Field.Cell is { } cell && record is not null) return cell(record, side);
        var value = side == 0 ? row.Before : row.After;
        if (row.Field.Statuses is { } statuses && value is string key) return new StatusBadgeControl { Statuses = statuses, Value = key };
        return FieldComparison.Text(row.Field, value, lang.Code, lang.Currency);
    }

    /// <summary>列表里只放文字：控件换成它的文字（状态是名字）。</summary>
    string TextOf(FieldCompareRow row, int side, Lang lang) => Display(row, side, lang) switch
    {
        string text => text,
        StatusBadgeControl badge => badge.Statuses!.Label(badge.Value!),
        Control control => control.Text,
        var other => other?.ToString() ?? "",
    };

    TextControl? Change(FieldCompareRow row, Lang lang) => FieldComparison.Change(row, lang.Code, lang.Currency) is { } change
        ? new TextControl { Text = change.Text, Tone = change.Tone, TextSize = TextSize.Sm, Numeric = true }
        : null;

    /// <summary>值放进单元格：文字套一个 TextControl（按要的色调、字重），控件原样。</summary>
    static TableCellControl Cell(object? content, TextTone tone = default, TextWeight weight = default)
    {
        var cell = new TableCellControl();
        switch (content)
        {
            case string text:
                cell.Controls.Add(new TextControl { Text = text, Tone = tone, Weight = weight });
                break;
            case Control control:
                cell.Controls.Add(control);
                break;
            case { } other:
                cell.Text = other.ToString();
                break;
        }
        return cell;
    }

    /// <summary>表格：紧凑的 TableControl，字段、原值（淡显）、新值（改了的中等字重，没改的淡显）、变化（有数值改动时才有这一列）。</summary>
    TableControl Table(Lang lang)
    {
        var numeric = Rows.Any(row => row.Changed && row.Delta is not null);
        var headers = new TableRowControl();
        foreach (var header in lang.FieldCompareHeaders.Take(numeric ? 4 : 3)) headers.Controls.Add(new TableHeaderCellControl { Text = header });
        var head = new TableHeadControl();
        head.Controls.Add(headers);
        var body = new TableBodyControl();
        foreach (var row in Rows)
        {
            var line = new TableRowControl { Tag = row.Changed ? "changed" : null };
            line.Controls.AddRange([
                new TableCellControl { Text = row.Field.Header },
                Cell(Display(row, 0, lang), TextTone.Muted),
                Cell(Display(row, 1, lang), row.Changed ? default : TextTone.Muted, row.Changed ? TextWeight.Medium : default),
            ]);
            if (numeric) line.Controls.Add(Cell(Change(row, lang)));
            body.Controls.Add(line);
        }
        var table = new TableControl { Density = TableDensity.Compact };
        table.Controls.AddRange([head, body]);
        return table;
    }

    /// <summary>列表：字段名一列（muted）、值一列（原值 → 新值，后面跟变化），列间距 md、行间距 xs。</summary>
    ListLayout List(Lang lang)
    {
        var list = new ListLayout();
        foreach (var row in Rows)
        {
            var value = new FlexControl { Align = FlexAlign.Center, Wrap = true, ColumnGap = Space.Xs };
            value.Controls.Add(new ValueChangeControl { Before = Before is null ? null : TextOf(row, 0, lang), After = TextOf(row, 1, lang) });
            if (Change(row, lang) is { } change) value.Controls.Add(change);
            list.Controls.AddRange([new TextControl { Text = row.Field.Header, Tone = TextTone.Muted }, value]);
        }
        return list;
    }

    /// <summary>
    /// 列表的排版（同上游 dl 的 grid：auto 1fr）：子控件两个一组（字段名、值），字段名一列取最宽的，值占剩下的宽度；列间距 md、行间距 xs（em）。
    /// </summary>
    internal sealed class ListLayout : Control
    {
        public ListLayout()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            // 停靠在顶上：高度按内容
            AutoSize = true;
        }

        (int Column, int Row) Gaps
        {
            get
            {
                var em = ElementContext.Of(this).Em;
                return ((int)MathF.Round(Tokens.Space.Md * em), (int)MathF.Round(Tokens.Space.Xs * em));
            }
        }

        IEnumerable<(Control Label, Control Value)> Pairs()
        {
            for (var index = 0; index + 1 < Controls.Count; index += 2) yield return (Controls[index], Controls[index + 1]);
        }

        int LabelWidth => Pairs().Select(pair => pair.Label.GetPreferredSize(Size.Empty).Width).DefaultIfEmpty().Max();

        public override Size GetPreferredSize(Size proposedSize)
        {
            var (column, row) = Gaps;
            var label = LabelWidth;
            var room = proposedSize.Width > 1 ? Math.Max(0, proposedSize.Width - label - column) : 0;
            int width = 0, height = 0, count = 0;
            foreach (var (name, value) in Pairs())
            {
                var size = value.GetPreferredSize(new Size(room, 0));
                width = Math.Max(width, label + column + size.Width);
                height += Math.Max(name.GetPreferredSize(Size.Empty).Height, size.Height) + (count++ > 0 ? row : 0);
            }
            return new Size(proposedSize.Width > 1 ? Math.Min(width, proposedSize.Width) : width, height);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var (column, row) = Gaps;
            var label = LabelWidth;
            var room = Math.Max(0, Width - label - column);
            var y = 0;
            foreach (var (name, value) in Pairs())
            {
                var nameHeight = name.GetPreferredSize(Size.Empty).Height;
                var valueHeight = value.GetPreferredSize(new Size(room, 0)).Height;
                name.SetBounds(RightToLeft == RightToLeft.Yes ? Width - label : 0, y, label, nameHeight);
                value.SetBounds(RightToLeft == RightToLeft.Yes ? 0 : label + column, y, room, valueHeight);
                y += Math.Max(nameHeight, valueHeight) + row;
            }
        }
    }
}
