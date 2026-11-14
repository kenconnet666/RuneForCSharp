using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>验证码能输入的字符（vue-tsx-pro 的 OtpCharsets）。</summary>
public enum OtpCharset
{
    /// <summary>只有数字（默认）。</summary>
    Numeric,

    /// <summary>字母与数字，字母转成大写。</summary>
    Alphanumeric,
}

/// <summary>
/// 验证码输入框（vue-tsx-pro 的 OtpField）：一格一个字符的短信验证码、动态口令、取件码。
/// <code>
/// &lt;rune:Field Label="短信验证码"&gt;&lt;rune:OtpField Text="{Binding Code}" Completed="OnCompleted" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:OtpField Length="8" Charset="Alphanumeric" Groups="4,4" AutomationProperties.Name="恢复码" /&gt;
/// </code>
/// 同上游（input-otp 的做法）：只有一个真正的输入框（平台的 TextBox，透明、盖在格子上），格子只是显示。粘贴整段、退格、输入法、撤销都是输入框的，
/// 读屏看到的是一个输入框。输入时只留数字（或字母数字，转大写）、截到 <see cref="Length"/> 位——粘贴"123-456"也能填满；填满时触发 <see cref="Completed"/>。
/// 焦点在框里时当前格画焦点环，空格里有闪烁的光标（系统关闭动画时常亮）。格子用输入框的外壳（同 TextField）：样式、圆角、出错、只读、不可用一致。
/// 格子 2.5em 宽、2.75em 高（不小于 minTarget），字 lg、semibold，格间 space.xs；分组之间一道 0.6em 的短横线。
/// <see cref="Mask"/> 只是格子显示圆点：WPF 的 TextBox 没有密码字符（PasswordBox 不是 TextBox）。
/// </summary>
public class OtpField : TextInputBase
{
    /// <summary>几位，默认 6。</summary>
    public static readonly DependencyProperty LengthProperty = DependencyProperty.Register(nameof(Length), typeof(int), typeof(OtpField),
        new FrameworkPropertyMetadata(6, OnShapeChanged), length => (int)length > 0);

    /// <summary>能输入的字符，默认数字。</summary>
    public static readonly DependencyProperty CharsetProperty = DependencyProperty.Register(nameof(Charset), typeof(OtpCharset), typeof(OtpField),
        new FrameworkPropertyMetadata(OtpCharset.Numeric, OnShapeChanged));

    /// <summary>分组显示（如 3,3 在中间加一道短横线）；各组加起来要等于 Length。</summary>
    public static readonly DependencyProperty GroupsProperty = DependencyProperty.Register(nameof(Groups), typeof(IReadOnlyList<int>), typeof(OtpField),
        new FrameworkPropertyMetadata(null, OnShapeChanged));

    /// <summary>格子显示成圆点（动态口令、支付密码）。</summary>
    public static readonly DependencyProperty MaskProperty = DependencyProperty.Register(nameof(Mask), typeof(bool), typeof(OtpField),
        new FrameworkPropertyMetadata(false, OnShapeChanged));

    OtpSlots? slots;
    string completed = "";

    static OtpField() => DefaultStyleKeyProperty.OverrideMetadata(typeof(OtpField), new FrameworkPropertyMetadata(typeof(OtpField)));

    /// <summary>新建：悬停、聚焦、样式变了重画格子。</summary>
    public OtpField()
    {
        MouseEnter += (_, _) => slots?.Refresh();
        MouseLeave += (_, _) => slots?.Refresh();
        IsKeyboardFocusWithinChanged += (_, _) => slots?.Refresh();
    }

    /// <summary>几位。</summary>
    public int Length { get => (int)GetValue(LengthProperty); set => SetValue(LengthProperty, value); }

    /// <summary>能输入的字符。</summary>
    public OtpCharset Charset { get => (OtpCharset)GetValue(CharsetProperty); set => SetValue(CharsetProperty, value); }

    /// <summary>分组。</summary>
    public IReadOnlyList<int>? Groups { get => (IReadOnlyList<int>?)GetValue(GroupsProperty); set => SetValue(GroupsProperty, value); }

    /// <summary>显示成圆点。</summary>
    public bool Mask { get => (bool)GetValue(MaskProperty); set => SetValue(MaskProperty, value); }

    /// <summary>填满时触发（输入、粘贴都算），参数是填好的码。</summary>
    public event EventHandler<string>? Completed;

    /// <summary>格子（测试用）。</summary>
    internal OtpSlots? SlotsPart => slots;

    /// <summary>只留能输入的字符（字母转大写），截到 length 位。</summary>
    internal static string Sanitize(string? text, OtpCharset charset, int length) =>
        new string([.. (text ?? "").Where(c => charset == OtpCharset.Numeric ? char.IsAsciiDigit(c) : char.IsAsciiLetterOrDigit(c))
            .Select(c => char.ToUpperInvariant(c)).Take(length)]);

    static void OnShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((OtpField)d).Sync();

    /// <inheritdoc />
    protected override void OnTemplateParts()
    {
        slots = GetTemplateChild(OtpFieldParts.Slots) as OtpSlots;
        slots?.Attach(this);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == VariantProperty || e.Property == ShowsInvalidProperty || e.Property == ShowsReadOnlyProperty || e.Property == CornerRadiusProperty) slots?.Refresh();
    }

    /// <summary>过滤输入（输入法组合中的字不在 Text 里，组合结束才过滤），变成填满时触发一次 Completed；格子跟着重画。</summary>
    protected override void Sync()
    {
        var text = Text ?? "";
        var next = Sanitize(text, Charset, Length);
        if (next != text)
        {
            // 过滤掉的字符不留在输入框里；光标放到末尾（格子是一格一格填的）。改了 Text 会再进来一次
            SetCurrentValue(TextProperty, next);
            CaretIndex = next.Length;
            return;
        }
        base.Sync();
        if (text.Length == Length && completed != text) Completed?.Invoke(this, text);
        completed = text.Length == Length ? text : "";
        slots?.Refresh();
    }
}

/// <summary>
/// 验证码的格子（OtpField 模板里的 PART_Slots）：按 OtpField 的长度、分组、值与状态画出各格、分隔、当前格的焦点环与光标。
/// 外壳的颜色同 TextField：surface 底、border 描边（悬停 borderStrong）、soft 时 fill 底无描边、只读 fill 底 borderMuted 描边、出错 danger 描边。
/// </summary>
public sealed class OtpSlots : FrameworkElement
{
    readonly DispatcherTimer blink = new() { Interval = TimeSpan.FromMilliseconds(500) };
    OtpField? owner;
    bool caretShown = true;

    /// <summary>新建：光标每半秒闪一次。</summary>
    public OtpSlots()
    {
        IsHitTestVisible = false;
        blink.Tick += (_, _) =>
        {
            caretShown = !caretShown;
            InvalidateVisual();
        };
        Unloaded += (_, _) => blink.Stop();
    }

    /// <summary>各格显示的字（测试用）：空格是 null。</summary>
    internal IReadOnlyList<string?> Shown
    {
        get
        {
            var text = owner?.Text ?? "";
            return [.. Enumerable.Range(0, owner?.Length ?? 0).Select(i => i < text.Length ? owner!.Mask ? "•" : text[i].ToString() : null)];
        }
    }

    /// <summary>在第几格后面加分隔（测试用）。</summary>
    internal IReadOnlySet<int> GroupEnds
    {
        get
        {
            var ends = new HashSet<int>();
            var sum = 0;
            foreach (var size in owner?.Groups ?? [])
            {
                sum += size;
                if (sum < owner!.Length) ends.Add(sum - 1);
            }
            return ends;
        }
    }

    /// <summary>当前格：已填的后面一格（填满时是最后一格）；聚焦、可编辑时才有。</summary>
    internal int? Active => owner is { IsKeyboardFocusWithin: true, ShowsReadOnly: false } otp ? Math.Min((otp.Text ?? "").Length, otp.Length - 1) : null;

    internal void Attach(OtpField field)
    {
        owner = field;
        Refresh();
    }

    internal void Refresh()
    {
        caretShown = true;
        // 当前格是空的时候才闪；系统关闭动画时常亮
        var blinking = Active is { } active && active >= (owner!.Text ?? "").Length && Motion.Enabled;
        if (blinking) blink.Start();
        else blink.Stop();
        InvalidateMeasure();
        InvalidateVisual();
    }

    double Em => owner?.FontSize ?? 14;

    double SlotWidth => 2.5 * Em;

    double SlotHeight => Math.Max(2.75 * Em, this.Resource<double>(ThemeKeys.Size.MinTarget));

    double Gap => this.Resource<double>(ThemeKeys.Space.Xs) * Em;

    double SeparatorWidth => 0.6 * Em;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (owner is null) return default;
        var count = owner.Length;
        var separators = GroupEnds.Count;
        return new Size(count * SlotWidth + separators * SeparatorWidth + (count + separators - 1) * Gap, SlotHeight);
    }

    /// <summary>各格的外壳、字或光标、分隔；当前格外面一圈 2 像素的焦点环（出错时是 danger）。</summary>
    protected override void OnRender(DrawingContext context)
    {
        if (owner is null) return;
        var soft = owner.Variant == ControlVariant.Soft;
        var background = this.Resource<Brush>(owner.ShowsReadOnly || soft ? ThemeKeys.Brush.Fill : ThemeKeys.Brush.Surface);
        var borderKey = owner.ShowsInvalid ? ThemeKeys.Brush.Danger
            : owner.ShowsReadOnly ? ThemeKeys.Brush.BorderMuted
            : soft ? null
            : owner.IsMouseOver ? ThemeKeys.Brush.BorderStrong
            : ThemeKeys.Brush.Border;
        var border = borderKey is null ? null : new Pen(this.Resource<Brush>(borderKey), this.Resource<double>(ThemeKeys.BorderWidth.Xs));
        var ring = new Pen(this.Resource<Brush>(owner.ShowsInvalid ? ThemeKeys.Brush.Danger : ThemeKeys.Brush.FocusRing), 2);
        var radius = owner.CornerRadius.TopLeft;
        var ink = this.Resource<Brush>(ThemeKeys.Brush.Text);
        var typeface = new Typeface(owner.FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var charSize = this.Resource<double>(ThemeKeys.FontSize.Lg) / this.Resource<double>(ThemeKeys.FontSize.Md) * Em;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var ends = GroupEnds;
        var active = Active;
        var shown = Shown;
        var x = 0d;
        for (var index = 0; index < owner.Length; index++)
        {
            var slot = new Rect(x, 0, SlotWidth, SlotHeight);
            var shell = slot;
            shell.Inflate(-0.5, -0.5);
            context.DrawRoundedRectangle(background, border, shell, radius, radius);
            if (index == active)
            {
                var outer = slot;
                outer.Inflate(0.5, 0.5);
                context.DrawRoundedRectangle(null, ring, outer, radius + 1, radius + 1);
            }
            var center = new Point(slot.X + slot.Width / 2, slot.Y + slot.Height / 2);
            if (shown[index] is { } text)
            {
                var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, charSize, ink, dpi);
                context.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
            }
            else if (index == active && caretShown)
            {
                var caret = 1.1 * Em;
                context.DrawRectangle(ink, null, new Rect(center.X - 0.5, center.Y - caret / 2, this.Resource<double>(ThemeKeys.BorderWidth.Xs), caret));
            }
            x += SlotWidth + Gap;
            if (!ends.Contains(index)) continue;
            var line = this.Resource<double>(ThemeKeys.BorderWidth.Sm);
            context.DrawRectangle(this.Resource<Brush>(ThemeKeys.Brush.BorderStrong), null, new Rect(x, SlotHeight / 2 - line / 2, SeparatorWidth, line));
            x += SeparatorWidth + Gap;
        }
    }
}

/// <summary>OtpField 的部件名。</summary>
public static class OtpFieldParts
{
    /// <summary>格子（OtpSlots：外壳、字、分隔都画在它上面；TS 的 propsSlot、propsSeparator）。</summary>
    public const string Slots = "PART_Slots";
}
