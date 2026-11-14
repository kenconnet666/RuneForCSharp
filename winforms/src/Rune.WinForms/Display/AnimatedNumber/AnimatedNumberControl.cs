using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 会滚动的数字（vue-tsx-pro 的 AnimatedNumber）：值变化时从旧值过渡到新值，按当前语言格式化，等宽数字不抖动。统计卡片的数值、金额、百分比、计数器。
/// <code>
/// var growth = new AnimatedNumberControl { Value = 0.123m, Format = new NumericFormat { Style = NumericStyle.Percent, MaximumFractionDigits = 1 }, Trend = true };
/// </code>
/// 值是 decimal：过渡的中间帧按浮点数算并按终值的小数位取整，停下时按原值格式化，不丢精度。过渡的时长默认 durations.lg、缓动 standard，
/// 系统关闭动画时直接跳到新值；<see cref="From"/> 给了时第一次画出来就从它滚到 Value。<see cref="Trend"/> 按正负着色（正数成功色、负数危险色）。
/// 前后缀是画的文字（TS 的 prefix、suffix），前面的图标写 <see cref="PrefixIcon"/>。读屏只读终值（连同前后缀）。大小跟着终值的文字。
/// </summary>
public class AnimatedNumberControl : Control, IElementHost, IScopeAware, IBaseline
{
    readonly Effect paint;
    readonly Signal<double> current = new(0);
    readonly Icon icon = new();
    Text prefix = new();
    Text number = new();
    Text suffix = new();
    decimal value;
    bool fromPending;

    /// <summary>新建：数值 0，大小跟着内容。</summary>
    public AnimatedNumberControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        AutoSize = true;
        AccessibleRole = AccessibleRole.StaticText;
        paint = new Effect(Invalidate);
        icon.Host = this;
        RebuildTexts();
    }

    /// <summary>前缀的部件属性（TS 的 propsPrefix）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsPrefix { get; set { field = value; RebuildTexts(); } }

    /// <summary>数字的部件属性（TS 的 propsValue），逐字段盖在默认的等宽数字上。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsValue { get; set { field = value; RebuildTexts(); } }

    /// <summary>后缀的部件属性（TS 的 propsSuffix）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsSuffix { get; set { field = value; RebuildTexts(); } }

    /// <summary>新建文字元素（保留文字），套上合并后的部件属性；都不换行。</summary>
    void RebuildTexts()
    {
        prefix = Built(prefix.Content, new TextProps().Merge(PropsPrefix));
        number = Built(number.Content, new TextProps { Numeric = true }.Merge(PropsValue));
        suffix = Built(suffix.Content, new TextProps().Merge(PropsSuffix));
        Relayout();

        Text Built(string content, TextProps props)
        {
            var text = new Text { Content = content, Wrap = TextWrap.NoWrap };
            props.ApplyTo(text);
            text.Host = this;
            return text;
        }
    }

    /// <summary>数值。</summary>
    [Category("Appearance")]
    [DefaultValue(typeof(decimal), "0")]
    public decimal Value
    {
        get => value;
        set
        {
            if (this.value == value) return;
            var from = current.Peek();
            this.value = value;
            // 还没画出来时直接到位（TS：挂载时不给 from 就直接显示）
            if (IsHandleCreated && !fromPending) Play(from);
            else current.Value = (double)value;
            Relayout();
        }
    }

    /// <summary>格式；不写时是普通数字（0–3 位小数）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NumericFormat? Format { get; set { field = value; Relayout(); } }

    /// <summary>过渡的时长；不写时取 durations.lg。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeSpan? Duration { get; set; }

    /// <summary>第一次画出来时从这个值滚到 Value；不写时直接显示 Value。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal? From
    {
        get;
        set
        {
            field = value;
            fromPending = value is not null;
            if (value is { } start) current.Value = (double)start;
        }
    }

    /// <summary>按正负着色：正数成功色、负数危险色（涨跌）；0 时是 Tone。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool Trend { get; set { field = value; Invalidate(); } }

    /// <summary>文字色调；写了 Trend 时由正负决定。</summary>
    [Category("Appearance")]
    public TextTone Tone { get; set { field = value; Invalidate(); } }

    /// <summary>数字前的文字。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Prefix { get => prefix.Content; set { prefix.Content = value ?? ""; Relayout(); } }

    /// <summary>数字前的图标（在前缀文字前面）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconData? PrefixIcon { get => icon.Data; set { icon.Data = value; Relayout(); } }

    /// <summary>前缀图标的色调。</summary>
    [Category("Appearance")]
    public TextTone PrefixIconTone { get => icon.Tone; set => icon.Tone = value; }

    /// <summary>数字后的文字（如单位"人"）。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Suffix { get => suffix.Content; set { suffix.Content = value ?? ""; Relayout(); } }

    /// <inheritdoc />
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(true)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    bool ShouldSerializeTone() => Tone.Value is not null;

    bool ShouldSerializePrefixIconTone() => PrefixIconTone.Value is not null;

    ElementContext Context => ElementContext.Of(this);

    /// <summary>正负：up、down、flat（TS 的 data-trend）。</summary>
    public string Direction => value > 0 ? "up" : value < 0 ? "down" : "flat";

    /// <summary>终值的文字（读屏念的数字）。</summary>
    public string FinalText => Formatted(value);

    /// <summary>当前显示的数字（测试用）。</summary>
    internal string ShownText => number.Content;

    /// <summary>显示的色调（测试用）：Trend 时按正负。</summary>
    internal TextTone ShownTone => Trend ? Direction switch { "up" => TextTone.Success, "down" => TextTone.Danger, _ => Tone } : Tone;

    string Formatted(decimal number) => (Format ?? NumericFormat.Default).Format(number, Context.Lang.Code, Context.Lang.Currency);

    /// <summary>从 <paramref name="from"/> 过渡到 Value（durations.lg、standard）；系统关闭动画、时长为 0 时直接到位。</summary>
    void Play(double from)
    {
        current.Value = from;
        Animator.Animate(current, (double)value, Duration ?? Tokens.Duration.Lg, Easing.Standard);
    }

    /// <summary>停在终值时按原值格式化（decimal 不经过浮点数）；过渡中按终值的小数位取整，整数不会滚出一串小数。</summary>
    void UpdateText(double shown) =>
        number.Content = shown == (double)value ? Formatted(value) : Formatted(Math.Round((decimal)shown, Math.Min(20, (int)value.Scale)));

    void Relayout()
    {
        UpdateText(current.Peek());
        if (AutoSize) Size = GetPreferredSize(Size.Empty);
        Invalidate();
    }

    void IScopeAware.ScopeChanged() => Relayout();

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    /// <summary>[图标][前缀][数字][后缀] 排成一行、竖直居中；数字按终值的文字量宽度（等宽数字，滚动时不跳）。</summary>
    Size Arrange()
    {
        var context = Context;
        var x = 0;
        var parts = new List<(Element Element, Size Size)>();
        if (icon.Data is not null)
        {
            var side = (int)MathF.Ceiling(context.Em);
            parts.Add((icon, new Size(side, side)));
        }
        if (Prefix.Length > 0) parts.Add((prefix, prefix.Measure(context, new Size(int.MaxValue, int.MaxValue))));
        var shown = number.Content;
        number.Content = FinalText;
        parts.Add((number, number.Measure(context, new Size(int.MaxValue, int.MaxValue))));
        number.Content = shown;
        if (Suffix.Length > 0) parts.Add((suffix, suffix.Measure(context, new Size(int.MaxValue, int.MaxValue))));
        var height = parts.Max(part => part.Size.Height);
        foreach (var (element, size) in parts)
        {
            element.Bounds = new Rectangle(x, (height - size.Height) / 2, size.Width, size.Height);
            x += size.Width;
        }
        return new Size(x, height);
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => Arrange();

    /// <summary>数字的基线（Flex、Stat 按基线对齐单位时用）。</summary>
    int? IBaseline.Baseline
    {
        get
        {
            Arrange();
            return number.Baseline(Context) is { } baseline ? number.Bounds.Y + baseline : null;
        }
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Arrange();
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (fromPending)
        {
            // From：第一次画出来时开始滚
            fromPending = false;
            Play(current.Peek());
        }
        using var _ = paint.Track();
        UpdateText(current.Value);
        var context = Context;
        var color = ShownTone.Value is null ? context.ForeColor : ShownTone.ColorIn(context);
        var tinted = context with { ForeColor = color };
        if (icon.Data is not null) icon.Paint(e.Graphics, tinted);
        if (Prefix.Length > 0) prefix.Paint(e.Graphics, tinted);
        number.Paint(e.Graphics, tinted);
        if (Suffix.Length > 0) suffix.Paint(e.Graphics, tinted);
    }

    void IElementHost.InvalidateElement(Element element) => Invalidate();

    void IElementHost.InvalidateLayout(Element element) => Invalidate();

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new AnimatedNumberAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Animator.Stop(current);
            paint.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>读屏：一段文字，念前缀、终值与后缀（滚动中的数字不另念）。</summary>
    sealed class AnimatedNumberAccessibleObject(AnimatedNumberControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.Prefix + owner.FinalText + owner.Suffix : owner.AccessibleName; set => owner.AccessibleName = value; }
    }
}
