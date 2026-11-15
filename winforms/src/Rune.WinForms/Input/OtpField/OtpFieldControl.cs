using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

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
/// var code = new OtpFieldControl();
/// code.Completed += (_, value) =&gt; Verify(value);
/// new FieldControl { Label = "短信验证码", Content = code };
/// </code>
/// 同上游（input-otp 的做法）：只有一个真正的输入框（平台的 TextBox，放在看不见的地方），格子是这个控件画的。粘贴整段、退格、输入法、撤销都是输入框的，
/// 读屏看到的是那个输入框（名称是字段的标签）。输入时只留数字（或字母数字，转大写）、截到 <see cref="Length"/> 位——粘贴"123-456"也能填满；
/// 填满时触发 <see cref="Completed"/>。焦点在框里时当前格画焦点环，空格里有闪烁的光标（系统关闭动画时常亮）。
/// 格子用输入框的外壳（同 TextFieldControl 的颜色）：样式、圆角、出错、只读一致；不可用时整体变淡。格子 2.5em 宽、2.75em 高，字 lg、semibold，格间 space.xs。
/// </summary>
[DefaultProperty(nameof(Text))]
[DefaultEvent(nameof(Completed))]
[Description("验证码输入框：一格一个字符，填满时触发 Completed。")]
public class OtpFieldControl : Control, IFieldControl, IScopeAware
{
    // 真正的输入框：放在控件外面（看不见），焦点、键盘、输入法都在它上面
    readonly TextBox input = new() { BorderStyle = BorderStyle.None, Location = new Point(-1000, -1000), Size = new Size(10, 10), ShortcutsEnabled = true };
    readonly System.Windows.Forms.Timer blink = new() { Interval = 500 };
    bool caretShown = true;
    bool hovered;
    string completed = "";
    string reported = "";

    /// <summary>新建：6 位数字。</summary>
    public OtpFieldControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        // 控件自己不占 Tab 位置：Tab 落在里面的输入框上；Focus() 转给输入框
        TabStop = false;
        AutoSize = true;
        Cursor = Cursors.IBeam;
        Controls.Add(input);
        input.TextChanged += (_, _) => Sync();
        input.GotFocus += (_, _) => Refresh(caret: true);
        input.LostFocus += (_, _) => Refresh(caret: true);
        blink.Tick += (_, _) =>
        {
            caretShown = !caretShown;
            Invalidate();
        };
    }

    /// <summary>输入的码。</summary>
    [AllowNull]
    public override string Text { get => input.Text; set => input.Text = value ?? ""; }

    /// <summary>几位，默认 6。</summary>
    [Category("Behavior")]
    [DefaultValue(6)]
    public int Length
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
            Relayout();
            Sync();
        }
    } = 6;

    /// <summary>能输入的字符，默认数字。</summary>
    [Category("Behavior")]
    [DefaultValue(OtpCharset.Numeric)]
    public OtpCharset Charset { get; set { field = value; Sync(); } }

    /// <summary>分组显示（如 3,3 在中间加一道短横线）；各组加起来要等于 Length。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<int>? Groups { get; set { field = value; Relayout(); } }

    /// <summary>显示成圆点（动态口令、支付密码）：输入框也是密码框。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Mask
    {
        get;
        set
        {
            field = value;
            input.UseSystemPasswordChar = value;
            Invalidate();
        }
    }

    /// <summary>样式，默认 surface。</summary>
    [Category("Appearance")]
    public ControlVariant Variant { get => field.Value is null ? ControlVariant.Surface : field; set { field = value; Invalidate(); } }

    /// <summary>圆角，默认 md。</summary>
    [Category("Appearance")]
    public Radius Radius { get => field.Value is null ? Radius.Md : field; set { field = value; Invalidate(); } }

    /// <summary>字号档位：格子随之缩放；不写时跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get;
        set
        {
            field = value;
            if (value.Pixels is { } pixels) Font = Fonts.Get(Font.FamilyName(), pixels * 72 / 96, Font.Style);
            else ResetFont();
        }
    }

    /// <summary>校验未通过；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsInvalid { get; set { field = value; Sync(); } }

    /// <summary>必填；不写时跟着所在的字段（字段的标签画星号）。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsRequired { get; set; }

    /// <summary>只读；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsReadOnly { get; set { field = value; Sync(); } }

    bool ShouldSerializeVariant() => Variant != ControlVariant.Surface;

    bool ShouldSerializeRadius() => Radius != Radius.Md;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    /// <summary>填满时触发（输入、粘贴都算），参数是填好的码。</summary>
    [Category("Action")]
    public event EventHandler<string>? Completed;

    internal bool Invalid => IsInvalid ?? FieldControl.Of(this)?.Invalid ?? false;

    internal bool ReadOnly => IsReadOnly ?? FieldControl.ReadOnlyOf(this);

    /// <summary>真正的输入框（测试用）。</summary>
    internal TextBox Input => input;

    /// <summary>各格显示的字（测试用）：空格是 null。</summary>
    internal IReadOnlyList<string?> Shown => [.. Enumerable.Range(0, Length).Select(i => i < Text.Length ? Mask ? "•" : Text[i].ToString() : null)];

    /// <summary>在第几格后面加分隔（测试用）。</summary>
    internal IReadOnlySet<int> GroupEnds
    {
        get
        {
            var ends = new HashSet<int>();
            var sum = 0;
            foreach (var size in Groups ?? [])
            {
                sum += size;
                if (sum < Length) ends.Add(sum - 1);
            }
            return ends;
        }
    }

    /// <summary>当前格：已填的后面一格（填满时是最后一格）；聚焦、可编辑时才有。</summary>
    internal int? Active => input.Focused && !ReadOnly ? Math.Min(Text.Length, Length - 1) : null;

    float Em => this.EmPixels();

    float SlotWidth => 2.5f * Em;

    float SlotHeight => MathF.Max(2.75f * Em, Tokens.Size.MinTarget * DeviceDpi / 96f);

    float Gap => Tokens.Space.Xs * Em;

    float SeparatorWidth => 0.6f * Em;

    void IFieldControl.FieldChanged() => Sync();

    void IScopeAware.ScopeChanged() => Invalidate();

    /// <summary>只留能输入的字符（字母转大写），截到 length 位。</summary>
    internal static string Sanitize(string? text, OtpCharset charset, int length) =>
        new string([.. (text ?? "").Where(c => charset == OtpCharset.Numeric ? char.IsAsciiDigit(c) : char.IsAsciiLetterOrDigit(c))
            .Select(c => char.ToUpperInvariant(c)).Take(length)]);

    /// <summary>过滤输入（输入法组合中的字不在 Text 里，组合结束才过滤），变成填满时触发一次 Completed；读屏名称、只读跟着字段。</summary>
    void Sync()
    {
        var text = input.Text;
        var next = Sanitize(text, Charset, Length);
        if (next != text)
        {
            // 过滤掉的字符不留在输入框里；光标放到末尾（格子是一格一格填的）。改了 Text 会再进来一次
            input.Text = next;
            input.SelectionStart = next.Length;
            return;
        }
        input.ReadOnly = ReadOnly;
        input.AccessibleName = AccessibleName is { Length: > 0 } name ? name : FieldControl.Of(this) is { } scope && scope.Owns(this) ? scope.Label : null;
        input.AccessibleDescription = FieldControl.DescribedBy(this, AccessibleDescription);
        // 只在值真的变了时报告：字段的状态变了也会走到这里，处理 TextChanged 时改字段（说明跟着输入变）不能又绕回来
        if (text != reported)
        {
            reported = text;
            OnTextChanged(EventArgs.Empty);
        }
        if (text.Length == Length && completed != text) Completed?.Invoke(this, text);
        completed = text.Length == Length ? text : "";
        Refresh(caret: true);
    }

    void Refresh(bool caret)
    {
        if (caret) caretShown = true;
        // 当前格是空的时候才闪；系统关闭动画时常亮
        var blinking = Active is { } active && active >= Text.Length && Motion.Enabled;
        if (blinking) blink.Start();
        else blink.Stop();
        Invalidate();
    }

    void Relayout()
    {
        if (AutoSize) Size = GetPreferredSize(Size.Empty);
        Invalidate();
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        var separators = GroupEnds.Count;
        return new Size((int)MathF.Ceiling(Length * SlotWidth + separators * SeparatorWidth + (Length + separators - 1) * Gap), (int)MathF.Ceiling(SlotHeight));
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Sync();
    }

    /// <summary>焦点交给输入框（Focus()、标签的助记键都落到这里）。</summary>
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        input.Focus();
    }

    /// <summary>点哪一格都是点输入框。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (Enabled) input.Focus();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovered = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = false;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    /// <summary>各格的外壳（同输入框的颜色）、字或光标、分隔；当前格外面一圈 2 像素的焦点环（出错时是 danger）。不可用时整体变淡。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        var colors = theme.Colors;
        var alpha = Enabled ? 255 : (int)(255 * Tokens.Opacity.Disabled);
        Color Dim(Color color) => Color.FromArgb(color.A * alpha / 255, color);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var soft = Variant == ControlVariant.Soft;
        Color? border = Invalid ? colors.Danger : ReadOnly ? colors.BorderMuted : soft ? null : hovered ? colors.BorderStrong : colors.Border;
        var radius = (Radius.In(theme.Radii) ?? 0) * Em;
        var px = DeviceDpi / 96f;
        using var font = Fonts.Get(Font.FamilyName(), Font.SizeInPoints * Tokens.FontSize.Lg / Tokens.FontSize.Md, FontStyle.Bold);
        var ends = GroupEnds;
        var active = Active;
        var shown = Shown;
        var x = 0f;
        for (var index = 0; index < Length; index++)
        {
            var slot = new RectangleF(x, 0, SlotWidth, SlotHeight);
            using (var path = Drawing.RoundedRectangle(RectangleF.Inflate(slot, -px / 2, -px / 2), radius))
            {
                using (var fill = new SolidBrush(Dim(ControlShell.Fill(colors, Variant, ReadOnly, hovered))))
                    graphics.FillPath(fill, path);
                if (border is { } line)
                {
                    using var pen = new Pen(Dim(line), Tokens.BorderWidth.Xs * px);
                    graphics.DrawPath(pen, path);
                }
            }
            if (index == active)
            {
                using var ring = new Pen(Invalid ? colors.Danger : colors.FocusRing, 2 * px);
                using var path = Drawing.RoundedRectangle(RectangleF.Inflate(slot, -px, -px), radius);
                graphics.DrawPath(ring, path);
            }
            if (shown[index] is { } text)
                TextRenderer.DrawText(graphics, text, font, Rectangle.Round(slot), Dim(colors.Text), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            else if (index == active && caretShown)
            {
                var caret = 1.1f * Em;
                using var brush = new SolidBrush(colors.Text);
                graphics.FillRectangle(brush, slot.X + slot.Width / 2 - px / 2, slot.Y + slot.Height / 2 - caret / 2, Tokens.BorderWidth.Xs * px, caret);
            }
            x += SlotWidth + Gap;
            if (!ends.Contains(index)) continue;
            var thickness = Tokens.BorderWidth.Sm * px;
            using (var brush = new SolidBrush(Dim(colors.BorderStrong)))
                graphics.FillRectangle(brush, x, SlotHeight / 2 - thickness / 2, SeparatorWidth, thickness);
            x += SeparatorWidth + Gap;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) blink.Dispose();
        base.Dispose(disposing);
    }
}
