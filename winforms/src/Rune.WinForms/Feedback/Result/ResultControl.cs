using System.ComponentModel;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 结果页（vue-tsx-pro 的 Result）：操作完成后的整块反馈（提交成功、支付失败、权限不足）。大图标（第一次画出来时一笔画出）+ 标题 + 说明 + 操作，居中排列。
/// <code>
/// var done = new ResultControl { Tone = StatusTone.Success, Title = "提交成功", Description = "审核结果会通过邮件通知你。", Action = new ButtonControl { Text = "返回列表" } };
/// var missing = new ResultControl { Status = ResultStatus.NotFound };   // 常见状态：图标、色调、状态码、标题与说明都有默认值，显式写的优先
/// </code>
/// 图标 4em、细线（strokeWidths.sm）、色调的颜色，下面多空 space.sm；状态码 2.5em 粗体、次要色、行高 1，读屏里没有（标题已经说明了状态）；
/// 标题半粗（GDI 按粗体画）、大一档（xl / md）；说明最宽 36em、次要色、居中换行；直接加的子控件是说明下面的内容（如错误详情）；操作上面多空 space.md。
/// 项之间 space.sm，上下内边距 space.xl、左右 space.lg，按字体算。宽度由父容器给（停靠、锚定），高度跟着内容。
/// 只是展示，读屏里是一组内容，名称是标题、描述是说明；操作后才出现、需要播报时，由使用方发通知或把焦点移过去。
/// </summary>
public class ResultControl : Panel, IElementHost, IScopeAware
{
    readonly ResultEngine engine;
    readonly Effect paint;
    Icon icon = new();
    Text code = new();
    Text title = new();
    Text description = new();
    string titleText = "";
    string descriptionText = "";
    bool iconSet;
    bool iconPainted;
    TextSize textSize;

    /// <summary>新建：高度跟着内容。</summary>
    public ResultControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AccessibleRole = AccessibleRole.Grouping;
        engine = new ResultEngine(this);
        paint = new Effect(Invalidate);
        RebuildIcon();
        RebuildTexts();
    }

    /// <summary>常见状态（403、404、500）：给出图标、色调、状态码与默认的标题、说明。</summary>
    [Category("Appearance")]
    public ResultStatus Status { get; set { field = value; RebuildIcon(); RebuildTexts(); } }

    /// <summary>色调，默认 success（写了 Status 时按状态）。</summary>
    [Category("Appearance")]
    public StatusTone Tone { get; set { field = value; RebuildIcon(); } }

    /// <summary>图标；不写时按状态或色调选，写 null 不显示。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconData? Icon
    {
        get;
        set
        {
            field = value;
            iconSet = true;
            RebuildIcon();
        }
    }

    /// <summary>第一次画出来时描线画出图标，默认 true；系统关闭动画时直接画好。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool Draw
    {
        get;
        set
        {
            var replay = value && !field;
            field = value;
            RebuildIcon(replay);
        }
    } = true;

    /// <summary>标题；不写时取状态的默认标题（读屏里是这一组的名称）。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Title { get => titleText; set { titleText = value ?? ""; RebuildTexts(); } }

    /// <summary>说明；不写时取状态的默认说明。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Description { get => descriptionText; set { descriptionText = value ?? ""; RebuildTexts(); } }

    /// <summary>操作（如"返回""重试"按钮），排在最下面。</summary>
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

    /// <summary>字号档位，内部尺寸随之缩放；不写时跟随字体。</summary>
    [Category("Appearance")]
    public TextSize TextSize { get => textSize; set { textSize = value; ApplyTextSize(); } }

    /// <summary>图标的部件属性（TS 的 propsIcon），逐字段盖在默认值上。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsIcon { get; set { field = value; RebuildIcon(); } }

    /// <summary>状态码的部件属性（TS 的 propsCode）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsCode { get; set { field = value; RebuildTexts(); } }

    /// <summary>标题的部件属性（TS 的 propsTitle）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsTitle { get; set { field = value; RebuildTexts(); } }

    /// <summary>说明的部件属性（TS 的 propsDescription）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsDescription { get; set { field = value; RebuildTexts(); } }

    bool ShouldSerializeStatus() => Status.Value is not null;

    bool ShouldSerializeTone() => Tone.Value is not null;

    bool ShouldSerializeTextSize() => textSize.Value is not null;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    float Em => this.EmPixels();

    ElementContext Context => ElementContext.Of(this);

    /// <summary>显示的色调：写了的 &gt; 状态的 &gt; success。</summary>
    internal StatusTone ShownTone => Tone.Value is not null ? Tone : Status.Look.Tone ?? StatusTone.Success;

    /// <summary>显示的图标（测试用）；不显示时为 null。</summary>
    internal IconData? ShownIcon => icon.Data;

    /// <summary>元素（测试用）。</summary>
    internal (Icon Icon, Text Code, Text Title, Text Description) Elements => (icon, code, title, description);

    /// <summary>
    /// 图标按状态、色调与合并好的部件属性新建（没写的字段回到元素自己的默认）。描线只在第一次画出来之前、
    /// 或 Draw 由 false 变为 true 时播（同 TS：挂载时与 draw 变为 true 时），之后换色调、换图标不重画。
    /// </summary>
    void RebuildIcon(bool replay = false)
    {
        var tone = ShownTone;
        icon = new Icon { Host = this };
        new IconProps
        {
            Data = iconSet ? Icon : Status.Look.Icon ?? AlertControl.IconFor(tone),
            Tone = new TextTone(tone.Value),
            StrokeWidth = Tokens.StrokeWidth.Sm,
            Draw = Draw && (!iconPainted || replay),
        }.Merge(PropsIcon).ApplyTo(icon);
        Relayout();
    }

    /// <summary>文字：显式写的优先，其次是状态的默认值（语言包）。</summary>
    void RebuildTexts()
    {
        var text = Status.Value is { } key && Context.Lang.ResultStatus.TryGetValue(key, out var found) ? found : default;
        code = Built(Status.Value ?? "", new TextProps { Weight = TextWeight.Bold, Tone = TextTone.Muted, Numeric = true, Align = TextAlign.Center }.Merge(PropsCode));
        title = Built(titleText.Length > 0 ? titleText : text.Title ?? "", new TextProps { Weight = TextWeight.Semibold, Align = TextAlign.Center }.Merge(PropsTitle));
        description = Built(descriptionText.Length > 0 ? descriptionText : text.Description ?? "", new TextProps { Tone = TextTone.Muted, Align = TextAlign.Center }.Merge(PropsDescription));
        Relayout();

        Text Built(string content, TextProps props)
        {
            var built = new Text { Host = this, Content = content };
            props.ApplyTo(built);
            return built;
        }
    }

    void IScopeAware.ScopeChanged()
    {
        ApplyTextSize();
        // 语言包可能换了：状态的默认标题、说明重新取
        RebuildTexts();
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ((IScopeAware)this).ScopeChanged();
    }

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
        var context = Context with { ForeColor = Context.Theme.Colors.Text };
        iconPainted |= icon.Data is not null;
        foreach (Element element in new Element[] { icon, code, title, description })
            if (element is Icon { Data: not null } or Text { Content.Length: > 0 }) element.Paint(e.Graphics, context);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ResultAccessibleObject(this);

    void IElementHost.InvalidateElement(Element element) => Invalidate(element.Bounds);

    void IElementHost.InvalidateLayout(Element element) => Relayout();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>读屏：一组内容，名称是标题、描述是说明；状态码是画的，读屏里没有。</summary>
    sealed class ResultAccessibleObject(ResultControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.title.Content : owner.AccessibleName; set => owner.AccessibleName = value; }

        public override string? Description => string.IsNullOrEmpty(owner.AccessibleDescription) ? owner.description.Content : owner.AccessibleDescription;
    }

    /// <summary>排版：图标、状态码、标题、说明（画的）、子控件、操作，各自居中，项之间 space.sm；图标下面、操作上面各多空一点。</summary>
    sealed class ResultEngine(ResultControl owner) : LayoutEngine
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
            int Center(int w) => (width - w) / 2;
            // 放一项：前面空 gap（第一项不空）再加 before，返回这一项的顶边
            int Next(int height, int before = 0)
            {
                var top = y + (first ? 0 : gap) + before;
                first = false;
                y = top + height;
                return top;
            }
            var toDips = 96f / owner.DeviceDpi;
            if (owner.icon.Data is not null)
            {
                var side = (int)MathF.Round(4 * em);
                // 图标按自己的 Size 画（不看 Bounds）：PropsIcon 没写 Size 时设成 4em
                if (owner.PropsIcon?.Size is null) owner.icon.Size = side * toDips;
                var top = Next(side);
                if (arrange) owner.icon.Bounds = new Rectangle(Center(side), top, side, side);
                y += gap; // 图标下面多空 space.sm
            }
            if (owner.code.Content.Length > 0)
            {
                if (owner.PropsCode?.Size is null) owner.code.Size = 2.5f * em * toDips;
                var size = owner.code.Measure(context, new Size(inner, int.MaxValue));
                // 行高 1：只占字号那么高，字形在里面竖直居中（GDI 的行高是字体自己的，比字号高）
                var line = (int)MathF.Round(2.5f * em);
                var top = Next(line);
                if (arrange) owner.code.Bounds = new Rectangle(Center(size.Width), top + (line - size.Height) / 2, size.Width, size.Height);
            }
            if (owner.PropsTitle?.Size is null) owner.title.Size = Tokens.FontSize.Xl / Tokens.FontSize.Md * em * toDips;
            foreach (var (text, limit) in new[] { (owner.title, inner), (owner.description, Math.Min(inner, (int)MathF.Round(36 * em))) })
            {
                if (text.Content.Length == 0) continue;
                var size = text.Measure(context, new Size(limit, int.MaxValue));
                var top = Next(size.Height);
                if (arrange) text.Bounds = new Rectangle(Center(size.Width), top, size.Width, size.Height);
            }
            var children = owner.Controls.Cast<Control>().Where(child => child != owner.Action && (child.Visible || !owner.Visible)).ToList();
            if (owner.Action is { } action) children.Add(action);
            foreach (var child in children)
            {
                var size = SizeOf(child, inner);
                // 操作上面多空 space.md（TS 的 action 的 margin-block-start）
                var before = child == owner.Action && !first ? (int)MathF.Round(Tokens.Space.Md * em) : 0;
                var top = Next(size.Height, before);
                if (arrange) child.SetBounds(Center(size.Width), top, size.Width, size.Height);
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
