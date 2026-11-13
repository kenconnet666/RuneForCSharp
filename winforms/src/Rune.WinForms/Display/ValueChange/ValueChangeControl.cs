using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 一个值的变化（vue-tsx-pro 的 ValueChange）：原值划掉、箭头、新值。放在表格单元格、操作记录、审批意见里。
/// <code>
/// var price = new ValueChangeControl { Before = "¥1,200", After = "¥1,350" };
/// var note = new ValueChangeControl { After = "新建时只有新值" };
/// </code>
/// 原值次要色、删除线；箭头（ArrowRight）次要色；新值正文色、中等字重（GDI 按常规画）。三者之间 space.xs，放不下时换行，按行竖直居中。
/// 没有原值（null）时只显示新值。读屏念"原值 改为 新值"（TS 是 del、ins 与给读屏的"改为"，语言包的 FieldCompareTo）。大小跟着内容。
/// </summary>
public class ValueChangeControl : Control, IElementHost, IScopeAware
{
    readonly Effect paint;
    Text before = new();
    Icon arrow = new();
    Text after = new();

    /// <summary>新建：只有新值，大小跟着内容。</summary>
    public ValueChangeControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        AutoSize = true;
        AccessibleRole = AccessibleRole.StaticText;
        paint = new Effect(Invalidate);
        Rebuild();
    }

    /// <summary>原值的部件属性（TS 的 propsBefore），逐字段盖在默认的次要色、删除线上。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsBefore { get; set { field = value; Rebuild(); } }

    /// <summary>箭头的部件属性（TS 的 propsArrow）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsArrow { get; set { field = value; Rebuild(); } }

    /// <summary>新值的部件属性（TS 的 propsAfter），逐字段盖在默认的中等字重上。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsAfter { get; set { field = value; Rebuild(); } }

    /// <summary>新建元素（保留文字），套上合并后的部件属性（没写的字段回到元素自己的默认）。</summary>
    void Rebuild()
    {
        before = Built(before.Content, new TextProps { Tone = TextTone.Muted }.Merge(PropsBefore));
        before.Strikethrough = true;
        arrow = new Icon();
        new IconProps { Data = Lucide.ArrowRight, Tone = TextTone.Muted }.Merge(PropsArrow).ApplyTo(arrow);
        arrow.Host = this;
        after = Built(after.Content, new TextProps { Weight = TextWeight.Medium }.Merge(PropsAfter));
        Relayout();

        Text Built(string content, TextProps props)
        {
            var text = new Text { Content = content, Wrap = TextWrap.NoWrap };
            props.ApplyTo(text);
            text.Host = this;
            return text;
        }
    }

    /// <summary>原值；没有（null）时只显示新值。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Before { get; set { field = value; before.Content = value ?? ""; Relayout(); } }

    /// <summary>新值。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string After { get => after.Content; set { after.Content = value ?? ""; Relayout(); } }

    /// <inheritdoc />
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(true)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    ElementContext Context => ElementContext.Of(this);

    /// <summary>读屏念的一句："原值 改为 新值"；没有原值时是新值。</summary>
    internal string Spoken => Before is null ? After : $"{Before} {Context.Lang.FieldCompareTo} {After}";

    /// <summary>元素（测试用）。</summary>
    internal (Text Before, Icon Arrow, Text After) Elements => (before, arrow, after);

    void Relayout()
    {
        if (AutoSize) Size = GetPreferredSize(new Size(Parent?.ClientSize.Width ?? 0, 0));
        Arrange(Width);
        Invalidate();
    }

    void IScopeAware.ScopeChanged() => Relayout();

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    /// <summary>原值、箭头、新值依次排开，间距 space.xs；放不下就换行；每行竖直居中。</summary>
    Size Arrange(int width)
    {
        var context = Context;
        var gap = (int)MathF.Round(Tokens.Space.Xs * context.Em);
        var side = (int)MathF.Ceiling(context.Em);
        var items = new List<(Element Element, Size Size)>();
        if (Before is not null)
        {
            items.Add((before, before.Measure(context, new Size(int.MaxValue, int.MaxValue))));
            items.Add((arrow, new Size(side, side)));
        }
        items.Add((after, after.Measure(context, new Size(int.MaxValue, int.MaxValue))));
        var limit = width > 0 ? width : int.MaxValue;
        var (x, y, lineHeight, widest) = (0, 0, 0, 0);
        var line = new List<(Element Element, Size Size, int X)>();
        void Flush()
        {
            foreach (var (element, size, left) in line) element.Bounds = new Rectangle(left, y + (lineHeight - size.Height) / 2, size.Width, size.Height);
            y += lineHeight;
            line.Clear();
        }
        foreach (var (element, size) in items)
        {
            if (line.Count > 0 && x + gap + size.Width > limit)
            {
                Flush();
                y += gap;
                x = 0;
                lineHeight = 0;
            }
            var left = line.Count == 0 ? 0 : x + gap;
            line.Add((element, size, left));
            x = left + size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
            widest = Math.Max(widest, x);
        }
        Flush();
        return new Size(widest, y);
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => Arrange(proposedSize.Width is > 1 and < int.MaxValue ? proposedSize.Width : 0);

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Arrange(Width);
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var context = Context with { ForeColor = Context.Theme.Colors.Text };
        if (Before is not null)
        {
            before.Paint(e.Graphics, context);
            arrow.Paint(e.Graphics, context);
        }
        after.Paint(e.Graphics, context);
    }

    void IElementHost.InvalidateElement(Element element) => Invalidate();

    void IElementHost.InvalidateLayout(Element element) => Relayout();

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ValueChangeAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>读屏：一段文字，念"原值 改为 新值"。</summary>
    sealed class ValueChangeAccessibleObject(ValueChangeControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.Spoken : owner.AccessibleName; set => owner.AccessibleName = value; }
    }
}
