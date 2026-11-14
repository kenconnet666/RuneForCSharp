using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

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
/// 格子 2.5em 宽、2.75em 高（不小于 minTarget），字 lg、semibold、等宽数字，格间 space.xs；分组之间一道 0.6em 的短横线。
/// </summary>
public class OtpField : TextInputBase
{
    /// <summary>几位，默认 6。</summary>
    public static readonly StyledProperty<int> LengthProperty = AvaloniaProperty.Register<OtpField, int>(nameof(Length), 6, validate: length => length > 0);

    /// <summary>能输入的字符，默认数字。</summary>
    public static readonly StyledProperty<OtpCharset> CharsetProperty = AvaloniaProperty.Register<OtpField, OtpCharset>(nameof(Charset));

    /// <summary>分组显示（如 3,3 在中间加一道短横线）；各组加起来要等于 Length。</summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> GroupsProperty = AvaloniaProperty.Register<OtpField, IReadOnlyList<int>?>(nameof(Groups));

    /// <summary>显示成圆点（动态口令、支付密码）。</summary>
    public static readonly StyledProperty<bool> MaskProperty = AvaloniaProperty.Register<OtpField, bool>(nameof(Mask));

    OtpSlots? slots;

    /// <summary>几位。</summary>
    public int Length { get => GetValue(LengthProperty); set => SetValue(LengthProperty, value); }

    /// <summary>能输入的字符。</summary>
    public OtpCharset Charset { get => GetValue(CharsetProperty); set => SetValue(CharsetProperty, value); }

    /// <summary>分组。</summary>
    public IReadOnlyList<int>? Groups { get => GetValue(GroupsProperty); set => SetValue(GroupsProperty, value); }

    /// <summary>显示成圆点。</summary>
    public bool Mask { get => GetValue(MaskProperty); set => SetValue(MaskProperty, value); }

    /// <summary>填满时触发（输入、粘贴都算），参数是填好的码。</summary>
    public event EventHandler<string>? Completed;

    /// <summary>格子（测试用）。</summary>
    internal OtpSlots? SlotsPart => slots;

    /// <summary>只留能输入的字符（字母转大写），截到 length 位。</summary>
    internal static string Sanitize(string? text, OtpCharset charset, int length) =>
        new string([.. (text ?? "").Where(c => charset == OtpCharset.Numeric ? char.IsAsciiDigit(c) : char.IsAsciiLetterOrDigit(c))
            .Select(c => char.ToUpperInvariant(c)).Take(length)]);

    /// <inheritdoc />
    protected override void OnTemplateParts(INameScope scope)
    {
        slots = scope.Find<OtpSlots>(OtpFieldParts.Slots);
        slots?.Attach(this);
    }

    /// <inheritdoc />
    protected override bool SyncsOn(AvaloniaProperty property) =>
        property == LengthProperty || property == CharsetProperty || property == GroupsProperty || property == MaskProperty || property == VariantProperty
        || property == IsPointerOverProperty || property == IsFocusedProperty || property == IsKeyboardFocusWithinProperty;

    /// <summary>过滤输入（输入法组合中的字不在 Text 里，组合结束才过滤），填满时触发 Completed；格子跟着重画。</summary>
    protected override void Sync()
    {
        var text = Text ?? "";
        var next = Sanitize(text, Charset, Length);
        if (next != text)
        {
            // 过滤掉的字符不留在输入框里；光标放到末尾（格子是一格一格填的）
            SetCurrentValue(TextProperty, next);
            CaretIndex = next.Length;
            return;
        }
        base.Sync();
        slots?.Refresh();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        var before = change.Property == TextProperty ? change.OldValue as string : null;
        base.OnPropertyChanged(change);
        // 遮罩时输入框也是密码框（同 TS 的 type=password：不进剪贴板、读屏不念字）
        if (change.Property == MaskProperty) PasswordChar = Mask ? '•' : default;
        if (change.Property != TextProperty) return;
        // 看这次变更的新值（不是 Text：过滤时在回调里改写过一次，外层回调读到的已经是改写后的）；只在变成填满时触发一次
        var text = change.NewValue as string ?? "";
        if (text.Length == Length && Sanitize(text, Charset, Length) == text && before != text) Completed?.Invoke(this, text);
    }
}

/// <summary>
/// 验证码的格子（OtpField 模板里的 PART_Slots）：按 OtpField 的长度、分组、值与状态画出各格、分隔、当前格的焦点环与光标。
/// 外壳的颜色同 TextField：surface 底、border 描边（悬停 borderStrong）、soft 时 fill 底无描边、只读 fill 底 borderMuted 描边、出错 danger 描边。
/// </summary>
public sealed class OtpSlots : Control
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
    internal int? Active => owner is { IsKeyboardFocusWithin: true } otp && !otp.ReadOnly ? Math.Min((otp.Text ?? "").Length, otp.Length - 1) : null;

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
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        if (owner is null) return default;
        var count = owner.Length;
        var separators = GroupEnds.Count;
        return new LayoutSize(count * SlotWidth + separators * SeparatorWidth + (count + separators - 1) * Gap, SlotHeight);
    }

    /// <summary>各格的外壳、字或光标、分隔；当前格外面一圈 2 像素的焦点环（出错时是 danger）。</summary>
    public override void Render(DrawingContext context)
    {
        if (owner is null) return;
        var soft = owner.Variant == ControlVariant.Soft;
        var background = this.Resource<IBrush>(owner.ReadOnly || soft ? ThemeKeys.Brush.Fill : ThemeKeys.Brush.Surface);
        var borderKey = owner.Invalid ? ThemeKeys.Brush.Danger
            : owner.ReadOnly ? ThemeKeys.Brush.BorderMuted
            : soft ? null
            : owner.IsPointerOver ? ThemeKeys.Brush.BorderStrong
            : ThemeKeys.Brush.Border;
        var border = borderKey is null ? null : new Pen(this.Resource<IBrush>(borderKey), this.Resource<double>(ThemeKeys.BorderWidth.Xs));
        var ring = new Pen(this.Resource<IBrush>(owner.Invalid ? ThemeKeys.Brush.Danger : ThemeKeys.Brush.FocusRing), 2);
        var radius = (Resolved.GetRadius(owner) ?? 0) * Em;
        var ink = this.Resource<IBrush>(ThemeKeys.Brush.Text);
        var typeface = new Typeface(owner.FontFamily, FontStyle.Normal, FontWeight.SemiBold);
        var charSize = this.Resource<double>(ThemeKeys.FontSize.Lg) / this.Resource<double>(ThemeKeys.FontSize.Md) * Em;
        var ends = GroupEnds;
        var active = Active;
        var x = 0d;
        var shown = Shown;
        for (var index = 0; index < owner.Length; index++)
        {
            var slot = new Rect(x, 0, SlotWidth, SlotHeight);
            context.DrawRectangle(background, border, new RoundedRect(slot.Deflate(0.5), radius));
            if (index == active) context.DrawRectangle(null, ring, new RoundedRect(slot.Inflate(0.5), radius + 1));
            if (shown[index] is { } text)
            {
                var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, charSize, ink);
                context.DrawText(formatted, new Point(slot.Center.X - formatted.Width / 2, slot.Center.Y - formatted.Height / 2));
            }
            else if (index == active && caretShown)
            {
                var caret = 1.1 * Em;
                context.FillRectangle(ink, new Rect(slot.Center.X - 0.5, slot.Center.Y - caret / 2, this.Resource<double>(ThemeKeys.BorderWidth.Xs), caret));
            }
            x += SlotWidth + Gap;
            if (!ends.Contains(index)) continue;
            var line = this.Resource<double>(ThemeKeys.BorderWidth.Sm);
            context.FillRectangle(this.Resource<IBrush>(ThemeKeys.Brush.BorderStrong), new Rect(x, SlotHeight / 2 - line / 2, SeparatorWidth, line));
            x += SeparatorWidth + Gap;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        blink.Stop();
    }
}

/// <summary>OtpField 的部件名。</summary>
public static class OtpFieldParts
{
    /// <summary>格子（OtpSlots：外壳、字、分隔都画在它上面；TS 的 propsSlot、propsSeparator）。</summary>
    public const string Slots = "PART_Slots";
}
