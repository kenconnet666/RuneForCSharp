using System.ComponentModel;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 空状态（vue-tsx-pro 的 Empty）：列表、表格、搜索结果没有内容时的说明。图标 + 标题 + 说明 + 操作，居中排列。
/// <code>
/// var empty = new EmptyControl { Title = "还没有订单", Description = "下单后会显示在这里。", Action = new ButtonControl { Text = "去逛逛" }, Dock = DockStyle.Fill };
/// </code>
/// 图标默认 Inbox（2.5em、次要文字色、strokeWidths.sm），设成 null 不显示；标题中等字重（GDI 按常规画）；说明最宽 32em、次要文字色、居中换行；
/// 直接加的子控件是标题与说明之外的内容；项之间 space.sm，操作上面再空 space.sm；上下内边距 space.xl、左右 space.lg，按字体算。
/// 宽度由父容器给（停靠、锚定），高度跟着内容。只是展示，读屏里是一组内容，名称是标题。
/// </summary>
public class EmptyControl : Panel, IElementHost
{
    readonly EmptyEngine engine;
    readonly Effect paint;
    Icon icon = new();
    Text title = new();
    Text description = new();

    /// <summary>新建：高度跟着内容。</summary>
    public EmptyControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AccessibleRole = AccessibleRole.Grouping;
        engine = new EmptyEngine(this);
        paint = new Effect(Invalidate);
        RebuildIcon();
        RebuildTexts();
    }

    /// <summary>图标，默认 Inbox；null 不显示。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconData? Icon { get; set { field = value; RebuildIcon(); } } = Lucide.Inbox;

    /// <summary>图标的部件属性（TS 的 propsIcon），逐字段盖在默认的次要文字色上。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsIcon { get; set { field = value; RebuildIcon(); } }

    /// <summary>新建图标元素，套上合并后的部件属性（没写的字段回到 Icon 自己的默认）。</summary>
    void RebuildIcon()
    {
        icon = new Icon { Host = this };
        new IconProps { Data = Icon, Tone = TextTone.Muted, StrokeWidth = Tokens.StrokeWidth.Sm }.Merge(PropsIcon).ApplyTo(icon);
        Relayout();
    }

    /// <summary>标题的部件属性（TS 的 propsTitle），逐字段盖在默认的中等字重、居中上。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsTitle { get; set { field = value; RebuildTexts(); } }

    /// <summary>说明的部件属性（TS 的 propsDescription），逐字段盖在默认的次要文字色、居中上。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsDescription { get; set { field = value; RebuildTexts(); } }

    /// <summary>新建标题、说明元素（保留文字），套上合并后的部件属性。</summary>
    void RebuildTexts()
    {
        title = Rebuilt(title, new TextProps { Weight = TextWeight.Medium, Align = TextAlign.Center }.Merge(PropsTitle));
        description = Rebuilt(description, new TextProps { Tone = TextTone.Muted, Align = TextAlign.Center }.Merge(PropsDescription));
        Relayout();

        Text Rebuilt(Text old, TextProps props)
        {
            var text = new Text { Host = this, Content = old.Content };
            props.ApplyTo(text);
            return text;
        }
    }

    /// <summary>标题；读屏里是这一组的名称。</summary>
    [Category("Appearance")]
    [Description("标题。")]
    [DefaultValue("")]
    public string Title { get => title.Content; set { title.Content = value; Relayout(); } }

    /// <summary>说明。</summary>
    [Category("Appearance")]
    [Description("说明（最宽 32em，居中换行）。")]
    [DefaultValue("")]
    public string Description { get => description.Content; set { description.Content = value; Relayout(); } }

    /// <summary>操作（如"新建"按钮），排在最下面。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Action
    {
        get;
        set
        {
            if (field is not null) Controls.Remove(field);
            field = value;
            if (value is not null) Controls.Add(value);
            Relayout();
        }
    }

    /// <summary>字号档位，内部尺寸随之缩放；放在下拉框、小卡片里用 sm。不写时跟随字体。</summary>
    [Category("Appearance")]
    [Description("字号档位，内部尺寸随之缩放；不写时跟随字体。")]
    public TextSize TextSize { get; set { field = value; ApplyTextSize(); } }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    void ResetTextSize() => TextSize = default;

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    /// <summary>图标元素（测试用）。</summary>
    internal Icon IconElement => icon;

    /// <summary>标题与说明元素（测试用）。</summary>
    internal (Text Title, Text Description) TextElements => (title, description);

    ElementContext Context => new(DeviceDpi, Font, ForeColor, RightToLeft == RightToLeft.Yes);

    void ApplyTextSize()
    {
        if (TextSize.Pixels is { } pixels)
        {
            var outer = Parent?.Font ?? Font;
            Font = Fonts.Get(outer.FontFamily.Name, pixels * 72 / 96, outer.Style);
        }
        else ResetFont();
        Relayout();
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

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => engine.Measure(proposedSize);

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var context = Context with { ForeColor = RuneTheme.Current.Value.Colors.Text };
        if (icon.Data is not null) icon.Paint(e.Graphics, context);
        if (Title.Length > 0) title.Paint(e.Graphics, context);
        if (Description.Length > 0) description.Paint(e.Graphics, context);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new EmptyAccessibleObject(this);

    void IElementHost.InvalidateElement(Element element) => Invalidate(element.Bounds);

    void IElementHost.InvalidateLayout(Element element) => Relayout();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>读屏：一组内容，名称是标题（没写 AccessibleName 时）。</summary>
    sealed class EmptyAccessibleObject(EmptyControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.Title : owner.AccessibleName; set => owner.AccessibleName = value; }
    }

    /// <summary>排版：图标、标题、说明（画的）、子控件、操作，各自居中，项之间 space.sm。</summary>
    sealed class EmptyEngine(EmptyControl owner) : LayoutEngine
    {
        static Size SizeOf(Control control, int width) => control.AutoSize ? control.GetPreferredSize(new Size(width, 0)) : control.Size;

        int Run(int width, bool arrange)
        {
            var em = owner.Em;
            var (block, inline, gap) = ((int)MathF.Round(Tokens.Space.Xl * em), (int)MathF.Round(Tokens.Space.Lg * em), (int)MathF.Round(Tokens.Space.Sm * em));
            var inner = Math.Max(0, width - 2 * inline);
            var context = owner.Context;
            var y = block;
            var first = true;
            void Next(int height)
            {
                if (!first) y += gap;
                first = false;
                y += height;
            }
            int Center(int w) => (width - w) / 2;
            if (owner.icon.Data is not null)
            {
                var side = (int)MathF.Round(2.5f * em);
                // 图标按自己的 Size 画（不看 Bounds）：PropsIcon 没写 Size 时设成 2.5em
                if (owner.PropsIcon?.Size is null) owner.icon.Size = side * 96f / owner.DeviceDpi;
                if (arrange) owner.icon.Bounds = new Rectangle(Center(side), y + (first ? 0 : gap), side, side);
                Next(side);
            }
            foreach (var (text, limit) in new[] { (owner.title, inner), (owner.description, Math.Min(inner, (int)MathF.Round(32 * em))) })
            {
                if (text.Content.Length == 0) continue;
                var size = text.Measure(context, new Size(limit, int.MaxValue));
                if (arrange) text.Bounds = new Rectangle(Center(size.Width), y + (first ? 0 : gap), size.Width, size.Height);
                Next(size.Height);
            }
            var children = owner.Controls.Cast<Control>().Where(child => child != owner.Action && (child.Visible || !owner.Visible)).ToList();
            if (owner.Action is { } action) children.Add(action);
            foreach (var child in children)
            {
                var size = SizeOf(child, inner);
                // 操作上面再空 space.sm（TS 的 action 的 margin-block-start）
                var extra = child == owner.Action && !first ? gap : 0;
                if (arrange) child.SetBounds(Center(size.Width), y + (first ? 0 : gap) + extra, size.Width, size.Height);
                Next(size.Height + extra);
            }
            return y + block;
        }

        public Size Measure(Size proposed)
        {
            var width = proposed.Width is > 1 and < int.MaxValue ? proposed.Width : owner.Width;
            return new Size(width, Run(width, arrange: false));
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            Run(owner.ClientSize.Width, arrange: true);
            owner.Invalidate();
            return owner.AutoSize;
        }
    }
}
