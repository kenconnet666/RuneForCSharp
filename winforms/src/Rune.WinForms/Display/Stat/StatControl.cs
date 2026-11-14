using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 统计数值（vue-tsx-pro 的 Stat）：指标名、数值与单位、可选的图标与环比变化。仪表盘顶部的一排卡片、详情页的关键指标。外观只是内容，放进 Card、Grid 里组合。
/// <code>
/// var sales = new StatControl { Label = "实际销售额", Value = 910.36m, Unit = "万元", Icon = Lucide.Wallet, Change = 0.123m, ChangeLabel = "较上月" };
/// </code>
/// 排版（参考 Ant Design Pro 的 ChartCard）：第一行是图标框（1.75em、accentSoft 底）与指标名（sm、次要色）、结束边是变化量（xs，放不下时换到下一行）；
/// 第二行是数值（xxl、粗体）与单位（xs、次要色，按基线对齐）；再下面是说明（xs、次要色）、内容（子控件，占满整宽）与底栏（上方一条分隔线）。行与行之间 space.xs。
/// 数值按 <see cref="Format"/> 格式化并在变化时滚动（AnimatedNumberControl 子控件）；写 <see cref="Text"/> 时原样显示（如"—""98.5%"）。
/// 变化量默认按百分比、带符号（+12.3%），上升成功色、下降危险色，<see cref="InvertChange"/> 反过来。<see cref="Tone"/> 给数值着色。
/// 加载中（IsLoading）数值与变化量换成骨架，指标名照常显示；很快完成时不闪。读屏里是一组，名称是指标名、描述是数值与单位，忙时带 Busy 状态。
/// 放进 <see cref="StatGroupControl"/> 时左右各空 space.md，不在一行开头时起始边画一条竖线。宽度由父容器给，高度跟着内容。
/// </summary>
public class StatControl : Panel, IElementHost, IScopeAware
{
    readonly Effect paint;
    readonly Icon icon = new();
    readonly Text label = new() { Tone = TextTone.Muted };
    readonly Text text = new() { Numeric = true, Weight = TextWeight.Bold, Wrap = TextWrap.NoWrap };
    readonly Text unit = new() { Tone = TextTone.Muted, Wrap = TextWrap.NoWrap };
    readonly Icon trendIcon = new();
    readonly Text changeLabel = new() { Tone = TextTone.Muted, Wrap = TextWrap.NoWrap };
    readonly Text description = new() { Tone = TextTone.Muted };
    readonly AnimatedNumberControl number = new();
    readonly AnimatedNumberControl changeNumber = new();
    readonly SkeletonControl numberSkeleton = new() { AutoSize = false, Visible = false };
    readonly SkeletonControl changeSkeleton = new() { AutoSize = false, Visible = false };
    readonly StatEngine engine;
    DelayedLoading loading;
    Rectangle iconBox;
    int? footerLine;

    /// <summary>新建：数值 0，高度跟着内容。</summary>
    public StatControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AccessibleRole = AccessibleRole.Grouping;
        engine = new StatEngine(this);
        paint = new Effect(Invalidate);
        icon.Host = label.Host = text.Host = unit.Host = trendIcon.Host = changeLabel.Host = description.Host = this;
        // 先有加载状态：加子控件时就会排版
        loading = new DelayedLoading(ShowSkeleton);
        Controls.AddRange([number, changeNumber, numberSkeleton, changeSkeleton]);
        ApplyFonts();
        Recompute();
    }

    /// <summary>指标名；读屏里是这一组的名称。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Label { get => label.Content; set { label.Content = value ?? ""; Relayout(); } }

    /// <summary>数值：按 Format 格式化，变化时滚动。</summary>
    [Category("Appearance")]
    [DefaultValue(typeof(decimal), "0")]
    public decimal Value { get => number.Value; set => number.Value = value; }

    /// <summary>原样显示的数值（TS 的字符串 value，如"—""98.5%"）；写了时盖过 Value。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public new string? Text { get; set { field = value; Recompute(); } }

    /// <summary>数值的格式，如两位小数、百分比、按万元（Scale = 10000）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NumericFormat? Format { get => number.Format; set => number.Format = value; }

    /// <summary>单位，跟在数值后面、字号小一档。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Unit { get => unit.Content; set { unit.Content = value ?? ""; Relayout(); } }

    /// <summary>指标名前面的图标（装饰）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconData? Icon { get => icon.Data; set { icon.Data = value; Relayout(); } }

    /// <summary>变化量（环比、同比）：正数向上、负数向下；默认按百分比显示（0.12 → +12%）。不写时没有变化量。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal? Change { get; set { field = value; Recompute(); } }

    /// <summary>变化量的格式，默认百分比、最多 1 位小数、正负都带符号（0 不带）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NumericFormat? ChangeFormat { get; set { field = value; Recompute(); } }

    /// <summary>变化量后面的说明，如"较上月"。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string ChangeLabel { get => changeLabel.Content; set { changeLabel.Content = value ?? ""; Relayout(); } }

    /// <summary>下降是好事（成本、耗时、故障数）：下降显示成功色、上升显示危险色。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool InvertChange { get; set { field = value; Recompute(); } }

    /// <summary>数值下面的补充说明（一行小字）。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Description { get => description.Content; set { description.Content = value ?? ""; Relayout(); } }

    /// <summary>数值下面占满整宽的补充内容：进度条、迷你图。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content { get; set { Swap(field, value); field = value; } }

    /// <summary>底栏：上方一条分隔线。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Footer { get; set { Swap(field, value); field = value; } }

    /// <summary>数值的色调：超时、缺漏这类要引起注意的数用 danger、warning；不写时是正文色。</summary>
    [Category("Appearance")]
    public TextTone Tone { get; set { field = value; Recompute(); } }

    /// <summary>加载中：数值与变化量换成骨架；很快完成时不闪。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsLoading
    {
        get;
        set
        {
            field = value;
            if (value && !IsHandleCreated && !loading.Shown)
            {
                // 还没显示就在加载：立即显示骨架（没有内容可看，空白比骨架更糟）
                loading.Stop();
                loading = new DelayedLoading(ShowSkeleton, loading: true);
                ShowSkeleton(true);
            }
            else loading.Update(value);
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        }
    }

    bool ShouldSerializeTone() => Tone.Value is not null;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    ElementContext Context => ElementContext.Of(this);

    float Em => this.EmPixels();

    /// <summary>变化的方向：up、down、flat（TS 的 data-trend）；没有变化量时为 null。</summary>
    public string? Direction => Change is not { } delta ? null : delta > 0 ? "up" : delta < 0 ? "down" : "flat";

    /// <summary>变化量的色调：持平是次要色，改善是成功色，变差是危险色。</summary>
    internal TextTone ChangeTone => Direction switch
    {
        "up" => InvertChange ? TextTone.Danger : TextTone.Success,
        "down" => InvertChange ? TextTone.Success : TextTone.Danger,
        _ => TextTone.Muted,
    };

    /// <summary>骨架在显示吗（测试用）。</summary>
    internal bool SkeletonShown => loading.Shown;

    /// <summary>数字与变化量的子控件（测试用）。</summary>
    internal (AnimatedNumberControl Number, AnimatedNumberControl Change) Numbers => (number, changeNumber);

    /// <summary>元素（测试用）。</summary>
    internal (Icon Icon, Text Text, Text Unit, Text ChangeLabel, Text Description) Elements => (icon, text, unit, changeLabel, description);

    /// <summary>在 StatGroupControl 里。</summary>
    bool Grouped => Parent is StatGroupControl;

    void Swap(Control? old, Control? value)
    {
        if (old is not null) Controls.Remove(old);
        if (value is not null) Controls.Add(value);
        Relayout();
    }

    void Recompute()
    {
        number.Tone = Tone;
        text.Content = Text ?? "";
        text.Tone = Tone;
        changeNumber.Value = Change ?? 0;
        changeNumber.Format = ChangeFormat ?? DefaultChangeFormat;
        changeNumber.Tone = trendIcon.Tone = ChangeTone;
        trendIcon.Data = Direction == "up" ? Lucide.TrendingUp : Direction == "down" ? Lucide.TrendingDown : null;
        ShowParts(loading.Shown);
    }

    /// <summary>变化量的默认格式（TS 的 CHANGE_FORMAT）。</summary>
    public static NumericFormat DefaultChangeFormat { get; } = new() { Style = NumericStyle.Percent, MaximumFractionDigits = 1, SignDisplay = NumericSign.ExceptZero };

    void ShowParts(bool skeleton)
    {
        number.Visible = !skeleton && Text is null;
        changeNumber.Visible = !skeleton && Change is not null;
        numberSkeleton.Visible = skeleton;
        changeSkeleton.Visible = skeleton && Change is not null;
        Relayout();
    }

    void ShowSkeleton(bool shown)
    {
        ShowParts(shown);
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
    }

    void IScopeAware.ScopeChanged()
    {
        ApplyFonts();
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ((IScopeAware)this).ScopeChanged();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyFonts();
        Relayout();
    }

    /// <summary>字号档位：指标名 sm，数值 xxl 粗体，单位、变化量、说明 xs（固定档位，不随 Stat 的字体缩放，同 TS）。</summary>
    void ApplyFonts()
    {
        var family = Font.FamilyName();
        number.Font = Fonts.Get(family, Tokens.FontSize.Xxl * 72 / 96, FontStyle.Bold);
        changeNumber.Font = Fonts.Get(family, Tokens.FontSize.Xs * 72 / 96, FontStyle.Regular);
        label.Size = Tokens.FontSize.Sm;
        text.Size = Tokens.FontSize.Xxl;
        unit.Size = changeLabel.Size = description.Size = Tokens.FontSize.Xs;
    }

    void Relayout()
    {
        PerformLayout();
        Invalidate();
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => engine.Measure(proposedSize);

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var context = Context with { ForeColor = Context.Theme.Colors.Text };
        var colors = context.Theme.Colors;
        var graphics = e.Graphics;
        if (Grouped && Left > 0)
            using (var line = new SolidBrush(colors.BorderMuted)) graphics.FillRectangle(line, 0, 0, Line, Height);
        if (icon.Data is not null)
        {
            var state = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var shape = Drawing.RoundedRectangle(iconBox, (Radius.Md.In(context.Theme.Radii) ?? 0) * context.Scale(Tokens.FontSize.Sm));
            using (var fill = new SolidBrush(colors.AccentSoft)) graphics.FillPath(fill, shape);
            graphics.Restore(state);
            icon.Paint(graphics, context with { ForeColor = colors.AccentText });
        }
        label.Paint(graphics, context);
        if (!loading.Shown)
        {
            if (Text is not null) text.Paint(graphics, context);
            if (Unit.Length > 0) unit.Paint(graphics, context);
            if (trendIcon.Data is not null) trendIcon.Paint(graphics, context);
            if (Change is not null && ChangeLabel.Length > 0) changeLabel.Paint(graphics, context);
        }
        if (Description.Length > 0) description.Paint(graphics, context);
        if (footerLine is { } y)
            using (var line = new SolidBrush(colors.BorderMuted)) graphics.FillRectangle(line, GroupedPadding, y, Width - 2 * GroupedPadding, Line);
    }

    int Line => Math.Max(1, (int)MathF.Round(Tokens.BorderWidth.Xs * DeviceDpi / 96f));

    int GroupedPadding => Grouped ? (int)MathF.Round(Tokens.Space.Md * Em) : 0;

    void IElementHost.InvalidateElement(Element element) => Invalidate();

    void IElementHost.InvalidateLayout(Element element) => Relayout();

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new StatAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            loading.Stop();
            paint.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>读屏：一组，名称是指标名、描述是数值与单位（还有变化量）；加载中带 Busy 状态。</summary>
    sealed class StatAccessibleObject(StatControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.Label : owner.AccessibleName; set => owner.AccessibleName = value; }

        public override string? Description => string.IsNullOrEmpty(owner.AccessibleDescription)
            ? string.Join(" ", new[] { owner.Text ?? owner.number.FinalText, owner.Unit, owner.Change is null ? "" : owner.changeNumber.FinalText, owner.ChangeLabel }.Where(part => part.Length > 0))
            : owner.AccessibleDescription;

        public override AccessibleStates State => owner.IsLoading ? base.State | AccessibleStates.Busy : base.State;
    }

    /// <summary>
    /// 排版：第一行 [图标框][指标名] …… [箭头 变化量 说明]，放不下时变化量换到下一行；然后数值与单位（按基线对齐）、说明、内容、底栏，行间 space.xs。
    /// </summary>
    sealed class StatEngine(StatControl owner) : LayoutEngine
    {
        static Size SizeOf(Control control, int width) => control.AutoSize ? control.GetPreferredSize(new Size(width, 0)) : control.Size;

        int Run(int width, bool arrange)
        {
            var context = owner.Context;
            var em = owner.Em;
            var pad = owner.GroupedPadding;
            var inner = Math.Max(1, width - 2 * pad);
            var (xs, sm) = (context.Scale(Tokens.FontSize.Xs), context.Scale(Tokens.FontSize.Sm));
            var gap = (int)MathF.Round(Tokens.Space.Xs * em);
            var y = 0;
            void Next(int height) => y += height + gap;

            // 第一行：图标框 1.75em、指标名之间 space.sm（都按 sm 的字号算）
            var labelGap = (int)MathF.Round(Tokens.Space.Sm * sm);
            var box = owner.icon.Data is null ? 0 : (int)MathF.Round(1.75f * sm);
            var labelSize = owner.label.Measure(context, new Size(int.MaxValue, int.MaxValue));
            var labelWidth = (box > 0 ? box + labelGap : 0) + labelSize.Width;
            var labelHeight = Math.Max(box, labelSize.Height);
            // 变化量：箭头 1em、一个空格宽、数字、说明（都按 xs）
            var changeGap = (int)MathF.Round(Tokens.Space.Xs * xs);
            var arrow = owner.trendIcon.Data is null || owner.loading.Shown ? 0 : (int)MathF.Ceiling(xs);
            var changeSize = owner.Change is null ? Size.Empty : owner.loading.Shown ? new Size((int)MathF.Round(6 * xs), (int)MathF.Round(1.5f * xs)) : SizeOf(owner.changeNumber, int.MaxValue);
            var changeLabelSize = owner.Change is null || owner.loading.Shown || owner.ChangeLabel.Length == 0 ? Size.Empty : owner.changeLabel.Measure(context, new Size(int.MaxValue, int.MaxValue));
            var changeWidth = owner.Change is null ? 0 : (arrow > 0 ? arrow + changeGap : 0) + changeSize.Width + (changeLabelSize.Width > 0 ? changeGap + changeLabelSize.Width : 0);
            var changeHeight = Math.Max(changeSize.Height, changeLabelSize.Height);
            var columnGap = (int)MathF.Round(Tokens.Space.Sm * em);
            var wrapped = changeWidth > 0 && labelWidth + columnGap + changeWidth > inner;
            var rowHeight = wrapped ? labelHeight : Math.Max(labelHeight, changeHeight);
            if (arrange)
            {
                owner.iconBox = new Rectangle(pad, y + (rowHeight - box) / 2, box, box);
                var iconSide = (int)MathF.Ceiling(sm);
                owner.icon.Size = Tokens.FontSize.Sm;
                owner.icon.Bounds = new Rectangle(pad + (box - iconSide) / 2, y + (rowHeight - iconSide) / 2, iconSide, iconSide);
                owner.label.Bounds = new Rectangle(pad + (box > 0 ? box + labelGap : 0), y + (rowHeight - labelSize.Height) / 2, labelSize.Width, labelSize.Height);
            }
            if (changeWidth > 0)
            {
                if (wrapped) Next(rowHeight);
                var top = wrapped ? y : y + (rowHeight - changeHeight) / 2;
                var x = wrapped ? pad : pad + inner - changeWidth;
                if (arrange)
                {
                    owner.trendIcon.Size = Tokens.FontSize.Xs;
                    if (arrow > 0) owner.trendIcon.Bounds = new Rectangle(x, top + (changeHeight - arrow) / 2, arrow, arrow);
                    var numberLeft = x + (arrow > 0 ? arrow + changeGap : 0);
                    var target = owner.loading.Shown ? owner.changeSkeleton : (Control)owner.changeNumber;
                    target.SetBounds(numberLeft, top + (changeHeight - changeSize.Height) / 2, changeSize.Width, changeSize.Height);
                    if (changeLabelSize.Width > 0) owner.changeLabel.Bounds = new Rectangle(numberLeft + changeSize.Width + changeGap, top + (changeHeight - changeLabelSize.Height) / 2, changeLabelSize.Width, changeLabelSize.Height);
                }
                if (wrapped) rowHeight = changeHeight;
            }
            Next(rowHeight);

            // 数值与单位：单位按基线对齐，之间 space.xs
            var xxl = context.Scale(Tokens.FontSize.Xxl);
            var valueSize = owner.loading.Shown ? new Size((int)MathF.Round(4 * xxl), (int)MathF.Round(1.25f * xxl))
                : owner.Text is not null ? owner.text.Measure(context, new Size(int.MaxValue, int.MaxValue)) : SizeOf(owner.number, int.MaxValue);
            var unitSize = owner.loading.Shown || owner.Unit.Length == 0 ? Size.Empty : owner.unit.Measure(context, new Size(int.MaxValue, int.MaxValue));
            var valueBaseline = owner.loading.Shown ? valueSize.Height : owner.Text is not null ? owner.text.Baseline(context) ?? valueSize.Height : ((IBaseline)owner.number).Baseline ?? valueSize.Height;
            var unitBaseline = unitSize.Height > 0 ? owner.unit.Baseline(context) ?? unitSize.Height : 0;
            var unitTop = Math.Max(0, valueBaseline - unitBaseline);
            var valueHeight = Math.Max(valueSize.Height, unitTop + unitSize.Height);
            if (arrange)
            {
                Control? valueControl = owner.loading.Shown ? owner.numberSkeleton : owner.Text is null ? owner.number : null;
                valueControl?.SetBounds(pad, y, valueSize.Width, valueSize.Height);
                if (owner.Text is not null) owner.text.Bounds = new Rectangle(pad, y, valueSize.Width, valueSize.Height);
                if (unitSize.Width > 0) owner.unit.Bounds = new Rectangle(pad + valueSize.Width + gap, y + unitTop, unitSize.Width, unitSize.Height);
            }
            Next(valueHeight);

            if (owner.Description.Length > 0)
            {
                var size = owner.description.Measure(context, new Size(inner, int.MaxValue));
                if (arrange) owner.description.Bounds = new Rectangle(pad, y, size.Width, size.Height);
                Next(size.Height);
            }
            if (owner.Content is { Visible: true } content)
            {
                var size = SizeOf(content, inner);
                if (arrange) content.SetBounds(pad, y, inner, size.Height);
                Next(size.Height);
            }
            owner.footerLine = null;
            if (owner.Footer is { Visible: true } footer)
            {
                // 底栏是 xs 的字号：上面多空 space.xs、线下面空 space.sm
                y += (int)MathF.Round(Tokens.Space.Xs * xs);
                owner.footerLine = y;
                y += owner.Line + (int)MathF.Round(Tokens.Space.Sm * xs);
                var size = SizeOf(footer, inner);
                if (arrange) footer.SetBounds(pad, y, inner, size.Height);
                Next(size.Height);
            }
            return Math.Max(0, y - gap);
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

/// <summary>
/// 一排指标（vue-tsx-pro 的 StatGroup）：按宽度换行（每个不窄于 MinItemWidth，默认 9rem），同一行里指标之间一条竖线（borderMuted，由 StatControl 画）。
/// 是一个 GridControl：行间距 space.md、没有栏间距。
/// </summary>
public class StatGroupControl : GridControl
{
    /// <summary>新建：每个指标最窄 9rem。</summary>
    public StatGroupControl()
    {
        MinItemWidth = new Length("9rem");
        RowGap = Space.Md;
        ColumnGap = Space.None;
    }
}
