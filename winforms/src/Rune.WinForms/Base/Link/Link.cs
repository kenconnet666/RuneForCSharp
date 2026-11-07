using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 文字链接（vue-tsx-pro 的 Link）：默认带下划线，正文里的链接不能只靠颜色区分（WCAG 1.4.1）。无窗口元素，放进窗体用 <see cref="LinkControl"/>。
/// 色调默认 accent；下划线 always / hover / none；外部链接末尾一个外链图标（0.1em 的间距）。WinForms 的文字是整段画的，
/// 链接是单独的控件，不跟着一段文字换行。
/// </summary>
public class Link : Element
{
    readonly Text text = new() { Wrap = TextWrap.NoWrap };
    readonly Icon icon = new() { Data = Lucide.ArrowUpRight };

    /// <summary>链接文字。</summary>
    public string Content { get => text.Content; set { text.Content = value; InvalidateMeasure(); } }

    /// <summary>色调；不写时 accent。</summary>
    public TextTone Tone { get => field.Value is null ? TextTone.Accent : field; set { field = value; text.Tone = value.Value is null ? TextTone.Accent : value; Invalidate(); } }

    /// <summary>下划线；不写时 always。</summary>
    public LinkUnderline Underline { get => field.Value is null ? LinkUnderline.Always : field; set { field = value; Invalidate(); } }

    /// <summary>外部链接：末尾显示外链图标。</summary>
    public bool External { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>指针在上面（宿主设）：hover 档在这时画下划线。</summary>
    public bool IsHovered { get; set { field = value; Invalidate(); } }

    /// <summary>键盘聚焦（宿主设）：画焦点框。</summary>
    public bool IsFocusVisible { get; set { field = value; Invalidate(); } }

    /// <summary>新建：文字默认品牌色。</summary>
    public Link() => text.Tone = TextTone.Accent;

    /// <summary>里面的环境：要下划线时字体加上下划线。</summary>
    ElementContext Inner(ElementContext context)
    {
        var underline = Underline == LinkUnderline.Always || (Underline == LinkUnderline.Hover && IsHovered);
        var style = underline ? context.Font.Style | FontStyle.Underline : context.Font.Style & ~FontStyle.Underline;
        return style == context.Font.Style ? context : context with { Font = Fonts.Get(context.Font.FontFamily.Name, context.Font.SizeInPoints, style) };
    }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var inner = Inner(context);
        var size = text.Measure(inner, new Size(int.MaxValue, int.MaxValue));
        if (!External) return size;
        var glyph = icon.Measure(inner, available);
        return new Size(size.Width + (int)MathF.Ceiling(0.1f * inner.Em) + glyph.Width, Math.Max(size.Height, glyph.Height));
    }

    /// <summary>文字的基线：文字在元素里竖直居中（外链图标比文字高时下移），再加文字自己的上伸部。Flex 按基线对齐时用。</summary>
    protected internal override int? Baseline(ElementContext context)
    {
        var inner = Inner(context);
        var size = text.Measure(inner, new Size(int.MaxValue, int.MaxValue));
        var height = Bounds.Height > 0 ? Bounds.Height : Measure(context, new Size(int.MaxValue, int.MaxValue)).Height;
        return (height - size.Height) / 2 + text.Baseline(inner);
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var inner = Inner(context) with { ForeColor = Tone.ColorIn(context) };
        var size = text.Measure(inner, new Size(int.MaxValue, int.MaxValue));
        text.Bounds = new Rectangle(Bounds.X, Bounds.Y + (Bounds.Height - size.Height) / 2, size.Width, size.Height);
        text.Paint(graphics, inner);
        if (External)
        {
            var glyph = icon.Measure(inner, Bounds.Size);
            icon.Bounds = new Rectangle(text.Bounds.Right + (int)MathF.Ceiling(0.1f * inner.Em), Bounds.Y + (Bounds.Height - glyph.Height) / 2, glyph.Width, glyph.Height);
            icon.Paint(graphics, inner);
        }
        if (!IsFocusVisible) return;
        // 焦点环：focusRing 色、2px，圆角 radii.xs
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var width = context.Scale(2);
            using var pen = new Pen(context.Theme.Colors.FocusRing, width);
            using var path = Drawing.RoundedRectangle(new RectangleF(Bounds.X + width / 2, Bounds.Y + width / 2, Bounds.Width - width, Bounds.Height - width), (Radius.Xs.In(context.Theme.Radii) ?? 0) * inner.Em);
            graphics.DrawPath(pen, path);
        }
        finally
        {
            graphics.Restore(state);
        }
    }
}

/// <summary>
/// 放进窗体的文字链接：Tab 能聚焦，Enter 或点击打开（先触发 Click）；读屏里是链接，外部链接的名称后补"在新窗口打开"。
/// <see cref="Href"/> 是绝对地址，用系统打开（浏览器、邮件……）。
/// </summary>
public class LinkControl : ElementControl<Link>
{
    /// <summary>新建：能聚焦，指针是手形。</summary>
    public LinkControl()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    /// <summary>链接地址（绝对地址）。</summary>
    [Category("Behavior")]
    [Description("链接地址（绝对地址，如 https://、mailto:），点击后用系统打开。")]
    [DefaultValue(null)]
    public Uri? Href { get; set; }

    /// <summary>外部链接。</summary>
    [Category("Behavior")]
    [Description("外部链接：末尾显示外链图标，读屏名称后补上\"在新窗口打开\"。")]
    [DefaultValue(false)]
    public bool External { get => Element.External; set => Element.External = value; }

    /// <summary>色调。</summary>
    [Category("Appearance")]
    [Description("色调，默认 accent。")]
    public TextTone Tone { get => Element.Tone; set => Element.Tone = value; }

    /// <summary>下划线。</summary>
    [Category("Appearance")]
    [Description("下划线：always（默认）、hover、none。")]
    public LinkUnderline Underline { get => Element.Underline; set => Element.Underline = value; }

    /// <summary>链接文字。</summary>
    [AllowNull]
    public override string Text { get => base.Text; set => base.Text = value; }

    bool ShouldSerializeTone() => Tone != TextTone.Accent;

    void ResetTone() => Tone = default;

    bool ShouldSerializeUnderline() => Underline != LinkUnderline.Always;

    void ResetUnderline() => Underline = default;

    /// <summary>代码里点击。</summary>
    public void PerformClick() => OnClick(EventArgs.Empty);

    /// <summary>先触发 Click，再打开链接；不可用时都不执行。</summary>
    protected override void OnClick(EventArgs e)
    {
        if (!Enabled) return;
        base.OnClick(e);
        if (Href is { IsAbsoluteUri: true } href) Navigate(href);
    }

    /// <summary>用系统打开链接。</summary>
    /// <param name="href">绝对地址。</param>
    protected virtual void Navigate(Uri href) => Process.Start(new ProcessStartInfo(href.AbsoluteUri) { UseShellExecute = true })?.Dispose();

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Element.Content = Text;
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Element.IsHovered = true;
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Element.IsHovered = false;
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Enter || base.IsInputKey(keyData);

    /// <summary>Enter 打开（同浏览器里的链接，空格不打开）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Enter) return;
        PerformClick();
        e.Handled = true;
    }

    /// <summary>焦点环只在键盘提示开着时显示（同 :focus-visible）。</summary>
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Element.IsFocusVisible = ShowFocusCues;
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Element.IsFocusVisible = false;
    }

    /// <inheritdoc />
    protected override void OnChangeUICues(UICuesEventArgs e)
    {
        base.OnChangeUICues(e);
        Element.IsFocusVisible = Focused && ShowFocusCues;
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new LinkAccessibleObject(this);

    /// <summary>读屏里是链接，默认动作打开；外部链接的名称后补上语言包的"在新窗口打开"。</summary>
    sealed class LinkAccessibleObject(LinkControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name
        {
            get => owner.External ? base.Name + RuneLang.Current.Value.LinkNewWindow : base.Name;
            set => base.Name = value;
        }

        public override AccessibleRole Role => AccessibleRole.Link;

        public override void DoDefaultAction() => owner.PerformClick();
    }
}

/// <summary>链接的下划线（vue-tsx-pro 的 LinkUnderlines）。</summary>
/// <param name="Value">取值（always、hover、none）。</param>
[TypeConverter(typeof(VariantConverter<LinkUnderline>))]
public readonly record struct LinkUnderline(string Value) : IVariant<LinkUnderline>
{
    /// <summary>总是有：正文里的链接（默认）。</summary>
    public static readonly LinkUnderline Always = new("always");

    /// <summary>悬停时才有：卡片、列表里一眼看得出是链接的地方。</summary>
    public static readonly LinkUnderline Hover = new("hover");

    /// <summary>没有：导航里的次要链接。</summary>
    public static readonly LinkUnderline None = new("none");

    /// <inheritdoc />
    public static IReadOnlyList<LinkUnderline> Known { get; } = [Always, Hover, None];

    /// <inheritdoc />
    public static LinkUnderline Parse(string value)
    {
        var underline = new LinkUnderline(value);
        Variants.WarnIfUnknown(underline);
        return underline;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
