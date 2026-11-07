using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 单行文本框（vue-tsx-pro 的 TextField）：输入框外壳（边框、悬停、聚焦时压住边框的焦点环、出错、只读、不可用）里放无边框的平台 TextBox，
/// 前后可以带图标与固定文字，可以一键清空；<see cref="Type"/> 为 password 时字符显示成圆点，带显示密码的按钮。
/// <code>
/// var search = new TextFieldControl { Type = TextFieldType.Search, Icon = Lucide.Search, Clearable = true, AccessibleName = "搜索" };
/// </code>
/// <see cref="Control.Text"/> 是输入的值（TS 的 _value），<see cref="PlaceholderText"/>、<see cref="MaxLength"/>、<see cref="IsReadOnly"/> 转给里面的 TextBox。
/// 外观：<see cref="Variant"/>（surface / soft）、<see cref="TextSize"/>、<see cref="Radius"/>（默认 md）。放进 FieldControl 时以标签为名称，沿用字段的出错、只读。
/// 没做：<c>format</c>（手机号、身份证号的分组显示）。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(TextChanged))]
[Description("单行文本框：外壳里放输入，前后可以带图标与固定文字。")]
public class TextFieldControl : FlexControl, IFieldControl
{
    readonly IconControl icon = new() { Tone = TextTone.Muted, Visible = false };
    readonly TextControl prefix = new() { Tone = TextTone.Muted, Visible = false };
    readonly TextBox input = new() { BorderStyle = BorderStyle.None };
    readonly TextControl suffix = new() { Tone = TextTone.Muted, Visible = false };
    readonly ClearButtonControl clear = new() { Radius = Radius.Full, Visible = false };
    readonly IconButtonControl reveal = new() { Radius = Radius.Full, Visible = false };
    readonly CharacterCountControl count = new() { Visible = false };
    readonly IconControl iconEnd = new() { Tone = TextTone.Muted, Visible = false };
    bool hovered;

    /// <summary>新建：外壳控件高度、左右 space.md，图标与文字隔 space.sm。</summary>
    public TextFieldControl()
    {
        Align = FlexAlign.Center;
        Gap = Space.Sm;
        AutoSize = true;
        Cursor = Cursors.IBeam;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Controls.AddRange([icon, prefix, input, suffix, clear, reveal, count, iconEnd]);
        SetGrow(input, true);
        input.TextChanged += (_, _) =>
        {
            Sync();
            OnTextChanged(EventArgs.Empty);
        };
        input.GotFocus += (_, _) => Invalidate();
        input.LostFocus += (_, _) => Invalidate();
        clear.Click += (_, _) =>
        {
            Text = "";
            input.Focus();
        };
        reveal.Click += (_, _) => RevealPassword = !RevealPassword;
        foreach (Control child in Controls)
        {
            child.MouseEnter += (_, _) => SetHovered(true);
            child.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        }
    }

    /// <summary>输入的值。</summary>
    [AllowNull]
    public override string Text { get => input.Text; set => input.Text = value ?? ""; }

    /// <summary>占位文字：没有内容时显示。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string PlaceholderText { get => input.PlaceholderText; set => input.PlaceholderText = value; }

    /// <summary>最多几个字符；0 是不限。显示字数时它是上限。</summary>
    [Category("Behavior")]
    [DefaultValue(32767)]
    public int MaxLength
    {
        get => input.MaxLength;
        set
        {
            input.MaxLength = value;
            Sync();
        }
    }

    /// <summary>只读：可以选中、复制，不能修改；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsReadOnly
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>校验未通过：红色描边与焦点环；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsInvalid
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>必填；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsRequired { get; set; }

    /// <summary>样式，默认 surface。</summary>
    [Category("Appearance")]
    public ControlVariant Variant
    {
        get => field.Value is null ? ControlVariant.Surface : field;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>字号档位；不写时跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get;
        set
        {
            field = value;
            if (value.Pixels is { } pixels) Font = Fonts.Get(Font.FontFamily.Name, pixels * 72 / 96, Font.Style);
            else ResetFont();
        }
    }

    /// <summary>圆角，默认 md；搜索框常用 full（胶囊形）。</summary>
    [Category("Appearance")]
    public Radius Radius
    {
        get => field.Value is null ? Radius.Md : field;
        set
        {
            field = value;
            Invalidate();
        }
    }

    /// <summary>输入的类型（TS 的 type）：password 时字符显示成圆点，带显示密码的按钮。</summary>
    [Category("Behavior")]
    public TextFieldType Type
    {
        get => field.Value is null ? TextFieldType.Text : field;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>显示着密码的原文。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool RevealPassword
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>前置图标。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public IconData? Icon
    {
        get => icon.Data;
        set
        {
            icon.Data = value;
            icon.Visible = value is not null;
        }
    }

    /// <summary>后置图标。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public IconData? IconEnd
    {
        get => iconEnd.Data;
        set
        {
            iconEnd.Data = value;
            iconEnd.Visible = value is not null;
        }
    }

    /// <summary>输入框前的固定文字（如 <c>https://</c>），不算在值里。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Prefix
    {
        get => prefix.Text is { Length: > 0 } text ? text : null;
        set
        {
            prefix.Text = value ?? "";
            prefix.Visible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>输入框后的固定文字（如 <c>元</c>），不算在值里。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Suffix
    {
        get => suffix.Text is { Length: > 0 } text ? text : null;
        set
        {
            suffix.Text = value ?? "";
            suffix.Visible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>有内容、可编辑时显示清空按钮。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Clearable
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>显示字数计数；上限取 MaxLength（32767 视为不限），超出时用危险色。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool ShowCount
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    bool ShouldSerializeVariant() => Variant != ControlVariant.Surface;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    bool ShouldSerializeRadius() => Radius != Radius.Md;

    bool ShouldSerializeType() => Type != TextFieldType.Text;

    /// <summary>里面的输入框（测试用）。</summary>
    internal TextBox Input => input;

    internal ClearButtonControl ClearButton => clear;

    internal IconButtonControl RevealButton => reveal;

    internal CharacterCountControl Count => count;

    /// <summary>出错：自己写了的优先，其次所在字段的。</summary>
    internal bool Invalid => IsInvalid ?? FieldControl.Of(this)?.Invalid ?? false;

    bool ReadOnly => IsReadOnly ?? FieldControl.Of(this)?.IsReadOnly ?? false;

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Sync();
    }

    /// <summary>内边距、控件高度、只读、密码、清空按钮、字数；输入框的底色跟着外壳（平台的 TextBox 不能透明）。</summary>
    void Sync()
    {
        var em = Em;
        Padding = new Padding((int)MathF.Round(0.75f * em), 0, (int)MathF.Round(0.75f * em), 0);
        MinimumSize = new Size(0, (int)MathF.Ceiling(ControlHeight.Of(ElementContext.Of(this), em, Tokens.Size.Control)));
        input.ReadOnly = ReadOnly;
        var password = Type == TextFieldType.Password;
        input.UseSystemPasswordChar = password && !RevealPassword;
        var colors = ProviderControl.ThemeOf(this).Colors;
        input.BackColor = Fill(colors);
        input.ForeColor = colors.Text;
        clear.Visible = Clearable && Enabled && !ReadOnly && Text.Length > 0;
        reveal.Visible = password;
        reveal.Icon = RevealPassword ? Lucide.EyeOff : Lucide.Eye;
        reveal.AccessibleName = ProviderControl.LangOf(this).PasswordShow;
        count.Visible = ShowCount;
        count.Count = Text.Length;
        count.Max = MaxLength is > 0 and < 32767 ? MaxLength : null;
        Invalidate();
    }

    /// <summary>不小于控件高度：放在 FieldControl 这类不是 FlexControl 的容器里时，它按想要的大小排，不看最小尺寸。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize);
        return new Size(Math.Max(size.Width, MinimumSize.Width), Math.Max(size.Height, MinimumSize.Height));
    }

    /// <summary>外壳的底色：surface；soft、只读时 fill。</summary>
    Color Fill(ThemeColors colors) => Variant == ControlVariant.Soft || ReadOnly ? colors.Fill : colors.Surface;

    void SetHovered(bool value)
    {
        if (hovered == value) return;
        hovered = value;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        SetHovered(true);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
    }

    /// <summary>点在外壳的空白处（内边距、图标、前后缀）：焦点交给输入框。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        input.Focus();
    }

    /// <summary>
    /// 外壳：surface 底（soft、只读时 fill）、xs 宽的描边（border，悬停 borderStrong；soft 时没有；只读 borderMuted；出错 danger）、圆角；
    /// 里面有焦点时 2 像素的焦点环压住边框（出错时 danger）；不可用时整体变淡由文字颜色体现。
    /// </summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        var colors = theme.Colors;
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var radius = Math.Min((Radius.In(theme.Radii) ?? 0) * Em, Height / 2f);
        using (var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), radius))
        using (var fill = new SolidBrush(Fill(colors)))
            graphics.FillPath(fill, path);
        var line = LogicalToDeviceUnits(1);
        Color? border = Invalid ? colors.Danger : ReadOnly ? colors.BorderMuted : Variant == ControlVariant.Soft ? null : hovered ? colors.BorderStrong : colors.Border;
        if (border is { } stroke)
        {
            using var outline = Drawing.RoundedRectangle(new RectangleF(line / 2f, line / 2f, Width - line, Height - line), Math.Max(0, radius - line / 2f));
            using var pen = new Pen(stroke, line);
            graphics.DrawPath(pen, outline);
        }
        if (!ContainsFocus) return;
        var ring = LogicalToDeviceUnits(2);
        using var focus = Drawing.RoundedRectangle(new RectangleF(ring / 2f, ring / 2f, Width - ring, Height - ring), Math.Max(0, radius - ring / 2f));
        using var focusPen = new Pen(Invalid ? colors.Danger : colors.FocusRing, ring);
        graphics.DrawPath(focusPen, focus);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new FieldAccessibleObject(this);

    sealed class FieldAccessibleObject(TextFieldControl owner) : ControlAccessibleObject(owner)
    {
        // 平台默认的名称取 Text，这里的 Text 是输入的值：只认 AccessibleName，没有时是字段的标签
        public override string? Name => owner.AccessibleName is { Length: > 0 } name ? name : FieldControl.Of(owner) is { } scope && scope.Owns(owner) ? scope.Label : null;

        public override AccessibleRole Role => AccessibleRole.Text;

        public override string? Value { get => owner.Text; set => owner.Text = value ?? ""; }
    }
}

/// <summary>
/// 字数计数（vue-tsx-pro 的 CharacterCount）：显示"12 / 100"，超出上限时用危险色；sm、textMuted。TextFieldControl 的 ShowCount 用它，
/// 也可以单独放在 FieldControl 里给自己的输入组件用。读屏念完整的句子（语言包的 characterCount）。
/// </summary>
[ToolboxItem(true)]
[Description("字数计数：超出上限时用危险色。")]
public class CharacterCountControl : TextControl
{
    /// <summary>新建：sm、textMuted，显示 0。</summary>
    public CharacterCountControl()
    {
        TextSize = TextSize.Sm;
        SyncText();
    }

    /// <summary>已输入的字符数。</summary>
    [Category("Data")]
    [DefaultValue(0)]
    public int Count
    {
        get;
        set
        {
            field = value;
            SyncText();
        }
    }

    /// <summary>上限；不给时只显示字数。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public int? Max
    {
        get;
        set
        {
            field = value;
            SyncText();
        }
    }

    /// <summary>超出上限。</summary>
    [Browsable(false)]
    public bool IsOver => Max is { } max && Count > max;

    void SyncText()
    {
        Text = Max is { } max ? $"{Count} / {max}" : Count.ToString();
        Tone = IsOver ? TextTone.Danger : TextTone.Muted;
        AccessibleName = ProviderControl.LangOf(this).CharacterCount(Count, Max);
    }
}
