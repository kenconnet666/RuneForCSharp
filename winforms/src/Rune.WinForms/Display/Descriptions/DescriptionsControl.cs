using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 描述列表（vue-tsx-pro 的 Descriptions）：键值对的详情（订单信息、用户资料），可分栏、可跨列，标签在左（横排）、在上（竖排）或各项一行排开（行内）。
/// <code>
/// var order = new DescriptionsControl { Title = "订单信息", Columns = 3, Dock = DockStyle.Top };
/// order.Items.Add(new DescriptionItem { Label = "订单号", Value = "A-1024", Copyable = true });
/// order.Items.Add(new DescriptionItem { Label = "收货地址", Value = address, Span = 3 });
/// </code>
/// 固定栏数时，列表窄于 32em 排成一列（TS 的容器查询）；<see cref="Columns"/> 写 auto 时按列表的宽度定栏数：每栏不窄于 <see cref="MinItemWidth"/>，
/// 最多 <see cref="MaxColumns"/> 栏。跨列（Span）按算出的栏数封顶。栏间距 space.xl、行间距 space.md；横排时标签与值之间 space.md，
/// 竖排时上下 space.xs；行内时各项之间 space.xl（换行 space.xs），项内 space.xs。标签次要色（画的文字），值是文字时画出来、是控件时原样放。
/// <see cref="Bordered"/>：表格样式，外框 borderMuted、圆角 md（碰到圆角的格子按圆角裁），格子之间有分隔线，标签格 fill 底、至少 8em，格子内边距 space.sm / space.md。
/// 项写 Copyable 时值后面有复制按钮（xs，名称"复制 + 标签"），复制的是 CopyText，没写时是值的文字。
/// 读屏里每一项是一组，名称是标签、描述是值的文字。宽度由父容器给（停靠、锚定），高度跟着内容。
/// </summary>
public class DescriptionsControl : Panel, IElementHost, IScopeAware
{
    readonly Effect paint;
    Text title = new();
    readonly ListPanel list;
    readonly GridControl grid = new() { AutoSize = true };
    readonly FlexControl inline = new() { Wrap = true, ColumnGap = Space.Xl, RowGap = Space.Xs, AutoSize = true };
    readonly CornerClip cornerClip = new();
    TextSize textSize;
    int columns = 1;

    /// <summary>新建：高度跟着内容。</summary>
    public DescriptionsControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        // 先有排版引擎：加子控件时 WinForms 就要用它
        LayoutEngine = new DescriptionsEngine(this);
        paint = new Effect(Invalidate);
        list = new ListPanel(this);
        Controls.Add(list);
        RebuildTitle();
        grid.Layout += (_, _) => ClipCorners();
        Items = new ItemList(this);
    }

    /// <summary>各项。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Collection<DescriptionItem> Items { get; }

    /// <summary>标题。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Title { get => title.Content; set { title.Content = value ?? ""; Relayout(); } }

    /// <summary>标题的部件属性（TS 的 propsTitle），逐字段盖在默认的中等字重、md 的字号上。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsTitle { get; set { field = value; RebuildTitle(); } }

    /// <summary>标签的部件属性（TS 的 propsLabel），逐字段盖在默认的次要色上。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsLabel { get; set { field = value; Rebuild(); } }

    void RebuildTitle()
    {
        var text = new Text { Content = title.Content };
        new TextProps { Weight = TextWeight.Medium, Size = Tokens.FontSize.Md }.Merge(PropsTitle).ApplyTo(text);
        text.Host = this;
        title = text;
        Relayout();
    }

    /// <summary>分几栏，默认 1；auto 时按宽度定。</summary>
    [Category("Layout")]
    public DescriptionsColumns Columns { get; set { field = value; Relayout(); } }

    /// <summary>Columns 为 auto 时每栏的最小宽度（像素），默认 240。</summary>
    [Category("Layout")]
    [DefaultValue(240)]
    public int MinItemWidth { get; set { field = value; Relayout(); } } = 240;

    /// <summary>Columns 为 auto 时最多几栏，默认 4。</summary>
    [Category("Layout")]
    [DefaultValue(4)]
    public int MaxColumns { get; set { field = value; Relayout(); } } = 4;

    /// <summary>标签与值的排列，默认 horizontal（TS 的 layout；Control 自己有 Layout 事件）。</summary>
    [Category("Appearance")]
    public DescriptionsLayout ItemLayout { get; set { field = value; Rebuild(); } }

    /// <summary>带边框（表格样式）：标签有底色，各项之间有分隔线。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool Bordered { get; set { field = value; Rebuild(); } }

    /// <summary>字号档位；不写时跟随字体。</summary>
    [Category("Appearance")]
    public TextSize TextSize { get => textSize; set { textSize = value; ApplyTextSize(); } }

    bool ShouldSerializeColumns() => Columns.Value is not null;

    bool ShouldSerializeItemLayout() => ItemLayout.Value is not null;

    bool ShouldSerializeTextSize() => textSize.Value is not null;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine { get; }

    float Em => this.EmPixels();

    ElementContext Context => ElementContext.Of(this);

    DescriptionsLayout ShownLayout => ItemLayout.Value is null ? DescriptionsLayout.Horizontal : ItemLayout;

    int Line => Math.Max(1, (int)MathF.Round(Tokens.BorderWidth.Xs * DeviceDpi / 96f));

    /// <summary>当前的栏数（测试用）。</summary>
    internal int ColumnCount => columns;

    /// <summary>各项（测试用）。</summary>
    internal IReadOnlyList<Control> Cells => [.. (ShownLayout == DescriptionsLayout.Inline ? inline.Controls : grid.Controls).Cast<Control>()];

    /// <summary>某一项跨几列（测试用）。</summary>
    internal int SpanOf(Control cell) => grid.GetSpan(cell).Count ?? columns;

    void ApplyTextSize()
    {
        if (TextSize.Pixels is { } pixels)
        {
            var outer = Parent?.Font ?? Font;
            Font = Fonts.Get(outer.FamilyName(), pixels * 72 / 96, outer.Style);
        }
        else ResetFont();
        Rebuild();
    }

    void IScopeAware.ScopeChanged()
    {
        ApplyTextSize();
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ((IScopeAware)this).ScopeChanged();
    }

    void Relayout()
    {
        PerformLayout();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    /// <summary>按项重建：每一项是一个格子（画标签与文字的值，控件的值与复制按钮是子控件），放进网格（行内时放进换行的一排）。</summary>
    void Rebuild()
    {
        foreach (var old in grid.Controls.Cast<Control>().Concat(inline.Controls.Cast<Control>()).ToList())
        {
            cornerClip.Release(old);
            old.Dispose();
        }
        grid.Controls.Clear();
        inline.Controls.Clear();
        list.Controls.Clear();
        var layout = ShownLayout;
        (grid.ColumnGap, grid.RowGap) = Bordered ? (Space.None, Space.None) : (Space.Xl, Space.Md);
        foreach (var item in Items)
        {
            var cell = new ItemPanel(this, item, layout, Bordered);
            if (layout == DescriptionsLayout.Inline) inline.Controls.Add(cell);
            else grid.Controls.Add(cell);
        }
        list.Controls.Add(layout == DescriptionsLayout.Inline ? inline : grid);
        list.Padding = new Padding(Bordered ? Line : 0);
        Relayout();
    }

    /// <summary>栏数与跨度按列表的宽度：auto 时按每栏最小宽度，固定栏数时窄于 32em 排成一列。</summary>
    void UpdateColumns(int width)
    {
        var shown = Columns.Value is null ? (DescriptionsColumns)1 : Columns;
        var count = ShownLayout == DescriptionsLayout.Inline ? 1 : shown.Resolve(width, Em, MinItemWidth * DeviceDpi / 96.0, MaxColumns);
        columns = count;
        if (grid.Columns != count) grid.Columns = count;
        foreach (ItemPanel cell in grid.Controls)
        {
            GridSpan span = Math.Clamp(cell.Item.Span, 1, count);
            if (grid.GetSpan(cell) != span) grid.SetSpan(cell, span);
        }
    }

    /// <summary>边框样式按外框里面的圆角裁掉碰到圆角的格子：标签格的底色不盖住外框的圆角。</summary>
    void ClipCorners()
    {
        var radius = Bordered ? Math.Max(0, (Radius.Md.In(Context.Theme.Radii) ?? 0) * Em - Line) : 0;
        cornerClip.Apply(grid.Controls.Cast<Control>(), new RectangleF(0, 0, grid.Width, grid.Height), radius);
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width is > 1 and < int.MaxValue ? proposedSize.Width : Width;
        return new Size(width, ((DescriptionsEngine)LayoutEngine).Run(width, arrange: false));
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        if (Title.Length > 0) title.Paint(e.Graphics, Context with { ForeColor = Context.Theme.Colors.Text });
    }

    void IElementHost.InvalidateElement(Element element) => Invalidate(element.Bounds);

    void IElementHost.InvalidateLayout(Element element) => Relayout();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>排版：标题（固定 md 的字号）在上，下面空 space.md 是列表，占满宽度。</summary>
    sealed class DescriptionsEngine(DescriptionsControl owner) : LayoutEngine
    {
        public int Run(int width, bool arrange)
        {
            var context = owner.Context;
            var y = 0;
            if (owner.Title.Length > 0)
            {
                var size = owner.title.Measure(context, new Size(width, int.MaxValue));
                if (arrange) owner.title.Bounds = new Rectangle(0, 0, size.Width, size.Height);
                y = size.Height + (int)MathF.Round(Tokens.Space.Md * owner.Em);
            }
            var padding = owner.list.Padding.Horizontal;
            owner.UpdateColumns(width - padding);
            var listHeight = owner.list.GetPreferredSize(new Size(width, 0)).Height;
            if (arrange) owner.list.SetBounds(0, y, width, listHeight);
            return y + listHeight;
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            Run(owner.ClientSize.Width, arrange: true);
            return owner.AutoSize;
        }
    }

    /// <summary>列表：边框样式时画外框（borderMuted、圆角 md），内边距一条线宽；里面是网格或换行的一排，撑满宽度。</summary>
    sealed class ListPanel : Panel
    {
        readonly DescriptionsControl owner;

        public ListPanel(DescriptionsControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        Control? Content => Controls.Count > 0 ? Controls[0] : null;

        public override Size GetPreferredSize(Size proposedSize)
        {
            if (Content is not { } content) return new Size(proposedSize.Width, Padding.Vertical);
            var inner = Math.Max(1, proposedSize.Width - Padding.Horizontal);
            return new Size(proposedSize.Width, content.GetPreferredSize(new Size(inner, 0)).Height + Padding.Vertical);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (Content is not { } content) return;
            var inner = Math.Max(1, ClientSize.Width - Padding.Horizontal);
            content.SetBounds(Padding.Left, Padding.Top, inner, content.GetPreferredSize(new Size(inner, 0)).Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!owner.Bordered) return;
            var context = ElementContext.Of(this);
            var line = owner.Line;
            var radius = (Radius.Md.In(context.Theme.Radii) ?? 0) * owner.Em;
            var state = e.Graphics.Save();
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var shape = Drawing.RoundedRectangle(new RectangleF(line / 2f, line / 2f, Width - line, Height - line), radius);
            using var pen = new Pen(context.Theme.Colors.BorderMuted, line);
            e.Graphics.DrawPath(pen, shape);
            e.Graphics.Restore(state);
        }
    }

    /// <summary>
    /// 一项：标签是画的文字；值是文字时也画出来，是控件时是子控件；复制按钮是子控件。
    /// 边框样式时画标签格的底色，不在第一行、第一列时画上边、起始边的分隔线（外框由列表画）。读屏里是一组，名称是标签、描述是值的文字。
    /// </summary>
    sealed class ItemPanel : Panel, IElementHost
    {
        readonly DescriptionsControl owner;
        readonly DescriptionsLayout layout;
        readonly bool bordered;
        readonly Text label;
        readonly Text? text;
        readonly Control? value;
        readonly CopyButtonControl? copy;
        Rectangle labelCell;

        public ItemPanel(DescriptionsControl owner, DescriptionItem item, DescriptionsLayout layout, bool bordered)
        {
            this.owner = owner;
            this.layout = layout;
            this.bordered = bordered;
            Item = item;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AutoSize = true;
            AccessibleRole = AccessibleRole.Grouping;
            AccessibleName = item.Label;
            // 元素先建好再挂上：挂着时改内容会触发排版，那时字段还没赋值
            label = new Text { Content = item.Label };
            new TextProps { Tone = TextTone.Muted }.Merge(owner.PropsLabel).ApplyTo(label);
            label.Host = this;
            if (item.Value is Control control) Controls.Add(value = control);
            else
            {
                // 没有值时显示 EmptyValue（—），不留一格空白（上游 hasContent）
                var shown = item.ValueText is { Length: > 0 } valueText ? valueText : ElementContext.Of(owner).Lang.EmptyValue;
                text = new Text { Content = shown };
                text.Host = this;
                AccessibleDescription = shown;
            }
            if (item.Copyable)
            {
                var lang = ElementContext.Of(owner).Lang;
                copy = new CopyButtonControl { Value = item.CopyText ?? item.ValueText, TextSize = TextSize.Xs, AccessibleName = lang.DescriptionsCopy(item.Label.Length > 0 ? item.Label : lang.Copy) };
                Controls.Add(copy);
            }
        }

        public DescriptionItem Item { get; }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            PerformLayout();
        }

        protected override void Dispose(bool disposing)
        {
            // 使用方给的值控件还给使用方：不跟着格子释放
            if (disposing && value is not null) Controls.Remove(value);
            base.Dispose(disposing);
        }

        public override Size GetPreferredSize(Size proposedSize) => Arrange(proposedSize.Width is > 1 and < int.MaxValue ? proposedSize.Width : int.MaxValue, arrange: false);

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Arrange(ClientSize.Width, arrange: true);
        }

        /// <summary>横排：标签在起始边（边框时至少 8em）、值占满剩下的宽度；竖排：标签在上、值在下；行内：标签、值挨着。</summary>
        Size Arrange(int width, bool arrange)
        {
            var context = ElementContext.Of(this) with { ForeColor = ElementContext.Of(this).Theme.Colors.Text };
            var em = context.Em;
            var (xs, sm, md) = ((int)MathF.Round(Tokens.Space.Xs * em), (int)MathF.Round(Tokens.Space.Sm * em), (int)MathF.Round(Tokens.Space.Md * em));
            var pad = bordered ? new Padding(md, sm, md, sm) : Padding.Empty;
            var gap = bordered ? 0 : layout == DescriptionsLayout.Horizontal ? md : xs;
            var unbounded = width == int.MaxValue;
            var labelSize = label.Measure(context, new Size(unbounded ? int.MaxValue : Math.Max(1, width - pad.Horizontal), int.MaxValue));
            var labelWidth = Math.Max(labelSize.Width + pad.Horizontal, bordered ? (int)MathF.Round(8 * em) : 0);
            if (layout == DescriptionsLayout.Vertical)
            {
                var valueWidth = unbounded ? int.MaxValue : Math.Max(1, width - pad.Horizontal);
                var valueSize = MeasureValue(context, valueWidth, xs);
                var labelHeight = labelSize.Height + pad.Vertical;
                if (arrange)
                {
                    labelCell = new Rectangle(0, 0, width, labelHeight);
                    label.Bounds = new Rectangle(pad.Left, pad.Top, labelSize.Width, labelSize.Height);
                    PlaceValue(context, new Rectangle(pad.Left, labelHeight + gap + pad.Top, valueSize.Width, valueSize.Height), xs);
                }
                return new Size(unbounded ? Math.Max(labelWidth, valueSize.Width + pad.Horizontal) : width, labelHeight + gap + valueSize.Height + pad.Vertical);
            }
            var valueLeft = labelWidth + gap;
            var available = unbounded ? int.MaxValue : Math.Max(1, width - valueLeft - pad.Horizontal);
            var measured = MeasureValue(context, available, xs);
            // 按基线对齐（TS 的 align-items: baseline）：值是控件时，标签的基线对准控件里第一行文字的；边框样式拉伸、靠上
            var shift = !bordered && value is not null && FieldControl.FirstBaseline(value) is { } target && label.Baseline(context) is { } own ? target - own : 0;
            var (labelTop, valueTop) = (pad.Top + Math.Max(0, shift), pad.Top + Math.Max(0, -shift));
            var height = Math.Max(labelTop + labelSize.Height, valueTop + measured.Height) + pad.Bottom;
            if (arrange)
            {
                labelCell = new Rectangle(0, 0, labelWidth, Height);
                label.Bounds = new Rectangle(pad.Left, labelTop, labelSize.Width, labelSize.Height);
                PlaceValue(context, new Rectangle(valueLeft + pad.Left, valueTop, measured.Width, measured.Height), xs);
            }
            var natural = valueLeft + measured.Width + pad.Horizontal;
            return new Size(unbounded || layout == DescriptionsLayout.Inline ? natural : width, height);
        }

        Size MeasureValue(ElementContext context, int width, int gap)
        {
            var copySize = copy?.GetPreferredSize(Size.Empty) ?? Size.Empty;
            var room = copy is null || width == int.MaxValue ? width : Math.Max(1, width - copySize.Width - gap);
            var size = text?.Measure(context, new Size(room, int.MaxValue)) ?? (value is null ? Size.Empty : value.AutoSize ? value.GetPreferredSize(new Size(room, 0)) : value.Size);
            if (copy is null) return size;
            // 复制按钮上下各收 0.25em：不把这一行撑高
            var copyHeight = Math.Max(0, copySize.Height - (int)MathF.Round(0.5f * context.Em));
            return new Size(size.Width + gap + copySize.Width, Math.Max(size.Height, copyHeight));
        }

        void PlaceValue(ElementContext context, Rectangle bounds, int gap)
        {
            var copySize = copy?.GetPreferredSize(Size.Empty) ?? Size.Empty;
            var contentWidth = copy is null ? bounds.Width : bounds.Width - gap - copySize.Width;
            if (text is not null)
            {
                var size = text.Measure(context, new Size(Math.Max(1, contentWidth), int.MaxValue));
                text.Bounds = bounds with { Width = size.Width, Height = size.Height };
                contentWidth = size.Width;
            }
            else value?.SetBounds(bounds.X, bounds.Y, Math.Max(1, contentWidth), value.AutoSize ? value.GetPreferredSize(new Size(contentWidth, 0)).Height : value.Height);
            // 按钮紧跟在值后面，竖直对准第一行
            var lineHeight = Fonts.LineHeight(Font);
            copy?.SetBounds(bounds.X + contentWidth + gap, bounds.Y + (lineHeight - copySize.Height) / 2, copySize.Width, copySize.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var context = ElementContext.Of(this);
            var colors = context.Theme.Colors;
            if (bordered)
            {
                if (layout != DescriptionsLayout.Inline)
                    using (var fill = new SolidBrush(colors.Fill)) e.Graphics.FillRectangle(fill, labelCell with { Height = layout == DescriptionsLayout.Vertical ? labelCell.Height : Height });
                var line = owner.Line;
                using var brush = new SolidBrush(colors.BorderMuted);
                if (Top > 0) e.Graphics.FillRectangle(brush, 0, 0, Width, line);
                if (Left > 0) e.Graphics.FillRectangle(brush, 0, 0, line, Height);
            }
            var tinted = context with { ForeColor = colors.Text };
            label.Paint(e.Graphics, tinted);
            text?.Paint(e.Graphics, tinted);
        }

        void IElementHost.InvalidateElement(Element element) => Invalidate();

        void IElementHost.InvalidateLayout(Element element) => PerformLayout();
    }

    /// <summary>项加进来、拿走都重建。</summary>
    sealed class ItemList(DescriptionsControl owner) : Collection<DescriptionItem>
    {
        protected override void InsertItem(int index, DescriptionItem item)
        {
            base.InsertItem(index, item);
            owner.Rebuild();
        }

        protected override void SetItem(int index, DescriptionItem item)
        {
            base.SetItem(index, item);
            owner.Rebuild();
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            owner.Rebuild();
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            owner.Rebuild();
        }
    }
}

/// <summary>描述列表里的一项（vue-tsx-pro 的 DescriptionItem）。</summary>
public class DescriptionItem
{
    /// <summary>名称。</summary>
    public string Label { get; set; } = "";

    /// <summary>值：文字、数字（画出来）或控件（原样放）。</summary>
    public object? Value { get; set; }

    /// <summary>占几列，默认 1；超过栏数时按栏数。</summary>
    public int Span { get; set; } = 1;

    /// <summary>值后面放复制按钮。</summary>
    public bool Copyable { get; set; }

    /// <summary>复制的文字；不写时是值的文字（值是文字或数字时）。</summary>
    public string? CopyText { get; set; }

    /// <summary>值的文字：控件时为空。</summary>
    internal string ValueText => Value switch
    {
        null or Control or bool => "",
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.CurrentCulture),
        _ => Value.ToString() ?? "",
    };
}
