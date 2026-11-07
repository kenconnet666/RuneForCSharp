using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 勾选类元素（Checkbox、Radio、Switch）共用的外层（vue-tsx-pro 的 choice-styles）：控件部件（方框、圆、轨道）在起始边，
/// 文字与说明在后面一列，顶端对齐；控件部件垂直居中在第一行（1.5em）里。整块可点，高度不低于 24px（WCAG 2.5.8）；从右到左时整体镜像。
/// 不可用时整体 opacities.disabled（颜色混进宿主往上第一个不透明的背景色）。
/// 部件：<see cref="PropsLabel"/>、说明的 PropsDescription（Checkbox、Radio；<see cref="TextProps"/>）；控件部件是画出来的（DESIGN §7）。
/// 只读、必填、出错、色调、字号、说明由派生类按上游的 props 公开（Radio 的这些来自单选组）。
/// </summary>
public abstract class ChoiceBase : Element
{
    Text label = new();
    Text description = new();

    /// <summary>值变了（点击、空格、读屏、代码里写）：数据绑定认 <c>IsCheckedChanged</c>。</summary>
    public event EventHandler? IsCheckedChanged;

    /// <summary>是否勾选。</summary>
    public bool IsChecked
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnCheckedChanged(Host is Control { IsHandleCreated: true, Visible: true });
            Invalidate();
            IsCheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>文字（TS 的 children），在控件部件后面。</summary>
    public string? Content { get; set { field = value; RebuildLabel(); } }

    /// <summary>文字部件的属性（TS 的 propsLabel）。</summary>
    public TextProps? PropsLabel { get; set { field = value; RebuildLabel(); } }

    /// <summary>可用（宿主的 Enabled）。</summary>
    public bool IsEnabled { get; internal set { if (field == value) return; field = value; Invalidate(); } } = true;

    /// <summary>键盘聚焦（宿主按 ShowFocusCues 设，同 :focus-visible）。</summary>
    public bool IsFocusVisible { get; internal set { if (field == value) return; field = value; Invalidate(); } }

    /// <summary>派生类公开的只读、必填、出错（Radio 的来自单选组）；null 是没写，取所在字段的。</summary>
    private protected bool? readOnly, required, invalid;

    /// <summary>派生类公开的色调、字号档位（Radio 的来自单选组）。</summary>
    private protected StatusTone tone;

    /// <inheritdoc cref="tone" />
    private protected TextSize size;

    /// <summary>说明（Checkbox、Radio 公开；TS 的 description）。</summary>
    private protected string? DescriptionText { get => field; set { field = value; RebuildDescription(); } }

    /// <summary>说明部件的属性（TS 的 propsDescription）。</summary>
    private protected TextProps? DescriptionProps { get => field; set { field = value; RebuildDescription(); } }

    /// <summary>所在的字段：在复选组、单选组里时字段归组（项不接字段）。</summary>
    internal FieldControl? Field => InGroup || Host is not Control host ? null : FieldControl.Of(host);

    /// <summary>在复选组、单选组里。</summary>
    private protected virtual bool InGroup => false;

    /// <summary>只读：能聚焦，不能切换。自己写了的优先，其次所在字段的。</summary>
    internal virtual bool ReadOnly => readOnly ?? Field?.IsReadOnly ?? false;

    /// <summary>校验未通过：自己写了的优先，其次所在字段的。</summary>
    internal virtual bool Invalid => invalid ?? Field?.Invalid ?? false;

    /// <summary>必填：自己写了的优先，其次所在字段的。</summary>
    internal virtual bool Required => required ?? Field?.IsRequired ?? false;

    /// <summary>色调：实例上写的，其次作用域的 <see cref="ChoiceDefaults"/>，最后 accent。</summary>
    internal virtual StatusTone ResolvedTone => tone.Or(ScopeDefaults?.Tone, StatusTone.Accent);

    /// <summary>字号档位：实例上写的，其次作用域的默认值，最后跟随宿主的字体。</summary>
    internal virtual TextSize ResolvedSize => size.Or(ScopeDefaults?.Size, default);

    /// <summary>实例上写过的档位（设计器只序列化这些）。</summary>
    internal (bool Tone, bool Size) Explicit => (tone.Value is not null, size.Value is not null);

    ChoiceDefaults? ScopeDefaults => Scope?.ResolveDefaults<ChoiceDefaults>();

    /// <summary>这次能不能切换（只读、不可用之外的理由，如 Switch 切换中）。</summary>
    internal virtual bool CanToggle => true;

    /// <summary>切换后的值：复选框、开关取反，单选项总是选中。</summary>
    internal virtual bool NextChecked => !IsChecked;

    /// <summary>把勾选写回：复选组、单选组里时交给组。</summary>
    /// <param name="value">新的值。</param>
    internal virtual void ApplyChecked(bool value) => IsChecked = value;

    /// <summary>勾选变了（Switch 让滑块滑过去，Radio 让圆点缩放）。</summary>
    /// <param name="animate">宿主正显示着：要过渡；否则直接到位。</param>
    private protected virtual void OnCheckedChanged(bool animate) { }

    /// <summary>文字部件（测试用）。</summary>
    internal Text LabelPart => label;

    /// <summary>说明部件（测试用）。</summary>
    internal Text DescriptionPart => description;

    bool HasLabel => !string.IsNullOrEmpty(Content);

    bool HasDescription => !string.IsNullOrEmpty(DescriptionText);

    void RebuildLabel()
    {
        label = new Text { Content = Content ?? "" };
        PropsLabel?.ApplyTo(label);
        InvalidateMeasure();
    }

    /// <summary>说明：muted，字号在布局时按 0.875em 给（写了 PropsDescription 的字号时用它）。</summary>
    void RebuildDescription()
    {
        description = new Text { Content = DescriptionText ?? "", Tone = TextTone.Muted };
        DescriptionProps?.ApplyTo(description);
        InvalidateMeasure();
    }

    /// <summary>控件部件（方框、圆、轨道）的大小（设备像素）。</summary>
    /// <param name="inner">按字号档位换算后的环境。</param>
    private protected abstract SizeF ControlSize(ElementContext inner);

    /// <summary>画控件部件。</summary>
    /// <param name="graphics">画布（已开抗锯齿）。</param>
    /// <param name="context">宿主的环境。</param>
    /// <param name="inner">按字号档位换算后的环境。</param>
    /// <param name="box">控件部件在宿主里的位置。</param>
    /// <param name="dim">不可用时把颜色变淡。</param>
    private protected abstract void PaintControl(Graphics graphics, ElementContext context, ElementContext inner, RectangleF box, Func<Color, Color> dim);

    /// <summary>
    /// 各部分的大小（设备像素）：控件部件、第一行（控件部件在里面垂直居中，不低于 1.5em）、与文字的间距 space.sm、文字、说明、文字离上边的距离。
    /// </summary>
    Metrics Layout(ElementContext inner, int availableWidth)
    {
        var control = ControlSize(inner);
        var row = MathF.Max(1.5f * inner.Em, control.Height);
        var gap = Tokens.Space.Sm * inner.Em;
        var limit = availableWidth == int.MaxValue ? int.MaxValue : Math.Max(0, availableWidth - (int)MathF.Ceiling(control.Width + gap));
        var labelSize = HasLabel ? label.Measure(inner, new Size(limit, int.MaxValue)) : default;
        if (HasDescription && DescriptionProps?.Size is null && description.Size != (TextSize)(0.875f * inner.Em * 96 / inner.Dpi))
            description.Size = 0.875f * inner.Em * 96 / inner.Dpi; // 改字号会让宿主重排，只在变了时写
        var descriptionSize = HasDescription ? description.Measure(inner, new Size(limit, int.MaxValue)) : default;
        // 文字的行高按字体算（不是 TS 的 1.5em）：在第一行里居中，多行时上下各留一份
        var labelTop = HasLabel ? MathF.Max(0, (row - Fonts.LineHeight(LabelFont(inner))) / 2) : 0;
        return new Metrics(control, row, gap, labelSize, descriptionSize, labelTop);
    }

    Font LabelFont(ElementContext inner) =>
        label.Size.Pixels is { } pixels ? Fonts.Get(inner.Font.FontFamily.Name, pixels * 72 / 96, inner.Font.Style) : inner.Font;

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var inner = context.Sized(ResolvedSize);
        var metrics = Layout(inner, available.Width);
        var text = Math.Max(metrics.Label.Width, metrics.Description.Width);
        var width = metrics.Control.Width + (text > 0 ? metrics.Gap + text : 0);
        var textHeight = HasLabel || HasDescription ? metrics.LabelTop * 2 + metrics.Label.Height + metrics.Description.Height : 0;
        var height = MathF.Max(context.Scale(Tokens.Size.MinTarget), MathF.Max(metrics.Row, textHeight));
        return new Size((int)MathF.Ceiling(width), (int)MathF.Ceiling(height));
    }

    /// <inheritdoc />
    protected internal override int? Baseline(ElementContext context) =>
        HasLabel ? (int)MathF.Round(Layout(context.Sized(ResolvedSize), Bounds.Width).LabelTop) + label.Baseline(context.Sized(ResolvedSize)) : null;

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var inner = context.Sized(ResolvedSize);
        var metrics = Layout(inner, Bounds.Width);
        var dim = Dimmer(IsEnabled, context.Theme.Colors);

        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            PaintControl(graphics, context, inner, ControlRect(context, metrics), dim);
        }
        finally
        {
            graphics.Restore(state);
        }

        var x = (int)MathF.Round(FromStart(context, metrics.Control.Width + metrics.Gap, Math.Max(metrics.Label.Width, metrics.Description.Width)));
        var y = (int)MathF.Round(Bounds.Y + metrics.LabelTop);
        if (HasLabel)
        {
            label.Bounds = new Rectangle(context.RightToLeft ? x + Math.Max(metrics.Label.Width, metrics.Description.Width) - metrics.Label.Width : x, y, metrics.Label.Width, metrics.Label.Height);
            label.Paint(graphics, inner with { ForeColor = dim(inner.ForeColor) });
        }
        if (HasDescription)
        {
            description.Bounds = new Rectangle(context.RightToLeft ? x + Math.Max(metrics.Label.Width, metrics.Description.Width) - metrics.Description.Width : x, y + metrics.Label.Height, metrics.Description.Width, metrics.Description.Height);
            description.Paint(graphics, inner with { ForeColor = dim(inner.ForeColor) });
        }
    }

    /// <summary>控件部件在宿主里的位置：起始边，垂直居中在第一行里。</summary>
    RectangleF ControlRect(ElementContext context, Metrics metrics) =>
        new(FromStart(context, 0, metrics.Control.Width), Bounds.Y + (metrics.Row - metrics.Control.Height) / 2, metrics.Control.Width, metrics.Control.Height);

    /// <summary>控件部件的位置（测试用）。</summary>
    internal RectangleF ControlBounds(ElementContext context) => ControlRect(context, Layout(context.Sized(ResolvedSize), Bounds.Width));

    /// <summary>从右到左时整体镜像：布局都按起始边算，落到画布上时换算。</summary>
    private protected float FromStart(ElementContext context, float start, float width) => context.RightToLeft ? Bounds.Right - start - width : Bounds.X + start;

    /// <summary>焦点环（focusRingStyles.tight）：贴着控件部件，外扩 4、宽 2（画在宿主留出的 Padding 里）。</summary>
    private protected static void PaintFocusRing(Graphics graphics, ElementContext context, RectangleF box, float radius)
    {
        var (reach, width) = (context.Scale(4), context.Scale(Tokens.BorderWidth.Sm));
        var ring = RectangleF.Inflate(box, reach - width / 2, reach - width / 2);
        using var path = Drawing.RoundedRectangle(ring, radius + reach - width / 2);
        using var pen = new Pen(context.Theme.Colors.FocusRing, width);
        graphics.DrawPath(pen, path);
    }

    /// <summary>
    /// 方框、圆的共同画法（choiceRecipe 的 input 与 choiceInputStyles）：2px 描边、表面底色；勾选时描边与底色都是 <paramref name="fill"/>；
    /// 出错时描边是危险色（排在勾选之后）。
    /// </summary>
    private protected static void PaintInput(Graphics graphics, ElementContext context, RectangleF box, float radius, bool marked, bool invalid, Color fill, Color? border, Func<Color, Color> dim)
    {
        var colors = context.Theme.Colors;
        var width = context.Scale(Tokens.BorderWidth.Sm);
        using (var path = Drawing.RoundedRectangle(box, radius))
        using (var brush = new SolidBrush(dim(marked ? fill : colors.Surface))) graphics.FillPath(brush, path);
        if (marked && !invalid) return;
        var edge = RectangleF.Inflate(box, -width / 2, -width / 2);
        using var outline = Drawing.RoundedRectangle(edge, MathF.Max(0, radius - width / 2));
        using var pen = new Pen(dim(invalid ? colors.Danger : border ?? colors.BorderStrong), width);
        graphics.DrawPath(pen, outline);
    }

    readonly record struct Metrics(SizeF Control, float Row, float Gap, Size Label, Size Description, float LabelTop);
}

/// <summary>勾选类控件要切换时的参数：<see cref="IsChecked"/> 是要切到的值，设 Cancel 不改。</summary>
/// <param name="isChecked">要切到的值。</param>
public sealed class ChoiceTogglingEventArgs(bool isChecked) : CancelEventArgs
{
    /// <summary>要切到的值。</summary>
    public bool IsChecked { get; } = isChecked;
}

/// <summary>勾选类控件（Switch、Checkbox、Radio）在作用域里的默认值（上游的 <c>defaults.Choice</c>）。</summary>
public sealed record ChoiceDefaults : IScopeRecord<ChoiceDefaults>
{
    /// <summary>字号档位。</summary>
    public TextSize? Size { get; init; }

    /// <summary>选中时的色调。</summary>
    public StatusTone? Tone { get; init; }

    /// <inheritdoc />
    public ChoiceDefaults Merge(ChoiceDefaults? over) => over is null ? this : new() { Size = over.Size ?? Size, Tone = over.Tone ?? Tone };
}
