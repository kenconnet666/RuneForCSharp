using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>倒计时的外观。</summary>
public enum CountdownVariant
{
    /// <summary>一串"时:分:秒"（默认）。</summary>
    Text,

    /// <summary>一格一格的数字块（限时抢购），天数单独一格；字号随控件的字体。</summary>
    Blocks,
}

/// <summary>
/// 倒计时画出来的样子（<see cref="CountdownControl"/> 用）：一串文字，或一格一格的数字块（天数一格、时分秒各一格，中间是冒号）。
/// 格子是 accent 底、accentContrast 字、semibold，最小宽度 2em，内边距 xxs / xs、圆角 sm 都按字号算（TS 的 countdownRecipe）。
/// </summary>
public sealed class CountdownFace : Element
{
    readonly Text text = new() { Numeric = true, Wrap = TextWrap.NoWrap };
    readonly List<Text> segments = [];
    readonly List<Text> separators = [];
    bool hasDays;
    bool blocks;

    /// <summary>显示一串文字。</summary>
    internal void ShowText(string value)
    {
        blocks = false;
        text.Content = value;
        InvalidateMeasure();
    }

    /// <summary>显示数字块：天数（可以没有）一格、时分秒各一格；格子的排法没变时只换文字。</summary>
    internal void ShowBlocks(string? days, string[] clock)
    {
        string[] values = days is null ? clock : [days, .. clock];
        if (!blocks || segments.Count != values.Length || hasDays != days is not null)
        {
            segments.Clear();
            separators.Clear();
            segments.AddRange(values.Select(_ => new Text { Weight = TextWeight.Semibold, Numeric = true, Wrap = TextWrap.NoWrap, Align = TextAlign.Center }));
            separators.AddRange(clock.Skip(1).Select(_ => new Text { Content = ":", Weight = TextWeight.Semibold, Wrap = TextWrap.NoWrap }));
        }
        for (var i = 0; i < values.Length; i++) segments[i].Content = values[i];
        blocks = true;
        hasDays = days is not null;
        InvalidateMeasure();
    }

    /// <summary>格子里的文字（测试用）。</summary>
    internal IReadOnlyList<string> Segments => blocks ? segments.Select(segment => segment.Content).ToList() : [];

    /// <summary>格之间的冒号有几个（测试用）。</summary>
    internal int SeparatorCount => blocks ? separators.Count : 0;

    /// <summary>格子里面的环境：accentContrast 的字。</summary>
    static ElementContext Inner(ElementContext context) => context with { ForeColor = context.Theme.Colors.AccentContrast };

    /// <summary>依次排开的各项：格子与冒号，各自的大小。</summary>
    IEnumerable<(Text Item, bool Cell, SizeF Size)> Items(ElementContext context)
    {
        var em = context.Em;
        var inner = Inner(context);
        var unbounded = new Size(int.MaxValue, int.MaxValue);
        for (var i = 0; i < segments.Count; i++)
        {
            // 天数那格之后是第一个时钟格，不加冒号；时钟格之间才加
            var clockIndex = hasDays ? i - 1 : i;
            if (clockIndex > 0)
            {
                var separator = separators[clockIndex - 1];
                yield return (separator, false, separator.Measure(context, unbounded));
            }
            var content = segments[i].Measure(inner, unbounded);
            yield return (segments[i], true, new SizeF(Math.Max(2 * em, content.Width + 2 * Tokens.Space.Xs * em), content.Height + 2 * Tokens.Space.Xxs * em));
        }
    }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        if (!blocks) return text.Measure(context, available);
        var items = Items(context).ToList();
        var gap = Tokens.Space.Xs * context.Em;
        var width = items.Sum(item => item.Size.Width) + gap * Math.Max(0, items.Count - 1);
        var height = items.Count == 0 ? 0 : items.Max(item => item.Size.Height);
        return new Size((int)MathF.Ceiling(width), (int)MathF.Ceiling(height));
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        if (!blocks)
        {
            text.Bounds = Bounds;
            text.Paint(graphics, context);
            return;
        }
        var em = context.Em;
        var inner = Inner(context);
        var gap = Tokens.Space.Xs * em;
        var radius = (Radius.Sm.In(context.Theme.Radii) ?? 0) * em;
        var x = (float)Bounds.X;
        foreach (var (item, cell, size) in Items(context).ToList())
        {
            var top = Bounds.Y + (Bounds.Height - size.Height) / 2;
            if (cell)
            {
                var box = new RectangleF(x, top, size.Width, size.Height);
                var state = graphics.Save();
                try
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = Drawing.RoundedRectangle(box, radius);
                    using var brush = new SolidBrush(context.Theme.Colors.Accent);
                    graphics.FillPath(brush, path);
                }
                finally
                {
                    graphics.Restore(state);
                }
                var content = item.Measure(inner, new Size(int.MaxValue, int.MaxValue));
                item.Bounds = new Rectangle((int)MathF.Round(x), (int)MathF.Round(top + Tokens.Space.Xxs * em), (int)MathF.Round(size.Width), content.Height);
                item.Paint(graphics, inner);
            }
            else
            {
                item.Bounds = new Rectangle((int)MathF.Round(x), (int)MathF.Round(top), (int)MathF.Ceiling(size.Width), (int)MathF.Ceiling(size.Height));
                item.Paint(graphics, context);
            }
            x += size.Width + gap;
        }
    }

    /// <inheritdoc />
    protected internal override int? Baseline(ElementContext context)
    {
        if (!blocks) return text.Baseline(context);
        if (segments.Count == 0) return null;
        var inner = Inner(context);
        var height = Measure(context, new Size(int.MaxValue, int.MaxValue)).Height;
        var cell = segments[0].Measure(inner, new Size(int.MaxValue, int.MaxValue)).Height + 2 * Tokens.Space.Xxs * context.Em;
        return (int)MathF.Round((height - cell) / 2 + Tokens.Space.Xxs * context.Em) + segments[0].Baseline(inner);
    }
}

/// <summary>
/// 倒计时（vue-tsx-pro 的 Countdown）：到某个时刻还剩多久，按秒（或分）更新；到点时触发 <see cref="Ended"/>，停在 0。
/// <code>
/// var sale = new CountdownControl { To = startsAt, Variant = CountdownVariant.Blocks };     // [2天] [03]:[15]:[42]
/// sale.Ended += (_, _) =&gt; Refresh();
/// new CountdownControl { To = deadline, Precision = CountdownPrecision.Minute };          // 2天 03:15
/// new CountdownControl { To = resendAt, Formatter = left =&gt; $"{Math.Ceiling(left.Total.TotalSeconds)}" };
/// </code>
/// 目标时刻同 <see cref="DateTimeTextControl"/> 的值。默认显示 时:分:秒，超过一天时前面加天数。几个计时器对齐到整秒一起跳；
/// 没有窗口句柄（不在界面上）时不更新。字号随控件的字体（数字块也按它算）。
/// 读屏是静态文字（TS 的 role="timer" 默认不播报），名称是显示的文字，写了 AccessibleName 时用它；到点的提示放在 Ended 里（Toast 等）。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(To))]
[DefaultEvent(nameof(Ended))]
[Description("倒计时：到某个时刻还剩多久，到点时触发 Ended。")]
public class CountdownControl : ElementControl<CountdownFace>
{
    readonly NowClock clock;
    object? to;
    CountdownPrecision precision;
    CountdownVariant variant;
    Func<CountdownRemaining, string>? formatter;
    bool? ended;

    /// <summary>新建：不能获得焦点。</summary>
    public CountdownControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        clock = new NowClock(Recompute);
        Recompute();
    }

    /// <summary>目标时刻：DateTimeOffset、DateTime、DateOnly 或 ISO 字符串。</summary>
    [Category("Data")]
    [Description("目标时刻：DateTimeOffset、DateTime、DateOnly 或 ISO 字符串。")]
    [DefaultValue(null)]
    [TypeConverter(typeof(StringConverter))]
    public object? To { get => to; set { to = value; Recompute(); } }

    /// <summary>精度，默认到秒。</summary>
    [Category("Behavior")]
    [Description("精度：Second（默认，每秒更新）、Minute（每分钟更新，不显示秒）。")]
    [DefaultValue(CountdownPrecision.Second)]
    public CountdownPrecision Precision { get => precision; set { precision = value; Recompute(); } }

    /// <summary>外观，默认一串文字。</summary>
    [Category("Appearance")]
    [Description("外观：Text（默认）一串时:分:秒，Blocks 一格一格的数字块。")]
    [DefaultValue(CountdownVariant.Text)]
    public CountdownVariant Variant { get => variant; set { variant = value; Recompute(); } }

    /// <summary>自定义显示（TS 的渲染函数）：拿到拆好的天、时、分、秒，返回要显示的文字；写了时忽略 Variant。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<CountdownRemaining, string>? Formatter { get => formatter; set { formatter = value; Recompute(); } }

    /// <summary>还剩多久；目标认不出时为 null。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CountdownRemaining? Remaining { get; private set; }

    /// <summary>显示的文字（也是读屏的名称）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string DisplayText { get; private set; } = "";

    /// <summary>时钟（测试里换成假的）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeProvider TimeProvider
    {
        get => clock.Time;
        set
        {
            clock.Time = value;
            Recompute();
        }
    }

    /// <summary>到点时触发一次（一开始就已经过了的不触发）。</summary>
    [Category("Action")]
    [Description("到点时触发一次。")]
    public event EventHandler? Ended;

    /// <inheritdoc />
    protected override void OnScopeChanged()
    {
        Recompute();
        base.OnScopeChanged();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Recompute();
    }

    /// <inheritdoc />
    protected override void OnHandleDestroyed(EventArgs e)
    {
        clock.Stop();
        base.OnHandleDestroyed(e);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) clock.Stop();
        base.Dispose(disposing);
    }

    /// <summary>按现在重算剩余、重画，排下一跳；从没到点变成到点时触发 Ended。</summary>
    void Recompute()
    {
        var lang = ProviderControl.LangOf(this);
        var remaining = TimeText.Remaining(to, clock.Now, lang, precision);
        Remaining = remaining?.Remaining;
        var left = remaining?.Remaining ?? default;
        DisplayText = remaining is null ? "" : formatter?.Invoke(left) ?? TimeText.CountdownText(left, precision, lang.Code);
        if (remaining is not null && formatter is null && variant == CountdownVariant.Blocks)
        {
            var (days, clockParts) = TimeText.Segments(left, precision, lang.Code);
            Element.ShowBlocks(days, clockParts);
        }
        else Element.ShowText(DisplayText);
        if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        clock.Schedule(IsHandleCreated ? remaining?.Every : null);

        var was = ended;
        ended = remaining?.Remaining.Ended;
        if (ended == true && was == false) Ended?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new CountdownAccessibleObject(this);

    /// <summary>读屏：静态文字，名称是显示的文字（写了 AccessibleName 时用它）。</summary>
    sealed class CountdownAccessibleObject(CountdownControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => owner.AccessibleRole is AccessibleRole.Default ? AccessibleRole.StaticText : owner.AccessibleRole;

        public override string? Name => owner.AccessibleName ?? owner.DisplayText;
    }
}
