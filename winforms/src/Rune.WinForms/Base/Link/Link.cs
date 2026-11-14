using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 文字链接（vue-tsx-pro 的 Link）：默认带下划线，正文里的链接不能只靠颜色区分（WCAG 1.4.1）。无窗口元素，放进窗体用 <see cref="LinkControl"/>。
/// 色调默认 accent；下划线 always / hover / none；外部链接末尾一个外链图标（0.1em 的间距）。WinForms 的文字是整段画的，
/// 链接是单独的控件，不跟着一段文字换行。
/// 界面里的链接（卡片页头的操作）一列下来都带下划线太重：所在容器用 <see cref="LinkControl.SetUnderlineScope"/> 给里面的链接默认的下划线
/// （上游的 LinkUnderlineScope）。取值：Link 上写的 > 所在容器给的 > 作用域的默认值（defaults.Link）> always。
/// </summary>
public class Link : Element
{
    readonly Text text = new() { Wrap = TextWrap.NoWrap };
    TextTone tone;
    LinkUnderline underline;
    readonly Icon icon = new() { Data = Lucide.ArrowUpRight };

    /// <summary>链接文字。</summary>
    public string Content { get => text.Content; set { text.Content = value; InvalidateMeasure(); } }

    /// <summary>色调；不写时取作用域的默认值（<see cref="LinkDefaults"/>），再没有时 accent。</summary>
    public TextTone Tone { get => tone.Or(ScopeDefaults?.Tone, TextTone.Accent); set { tone = value; Invalidate(); } }

    /// <summary>下划线；不写时取所在容器给的（<see cref="LinkControl.SetUnderlineScope"/>），再其次作用域的默认值，再没有时 always。</summary>
    public LinkUnderline Underline
    {
        get => underline.Value is not null ? underline : (Host as Control is { } host ? LinkControl.UnderlineScopeOf(host) : null) ?? underline.Or(ScopeDefaults?.Underline, LinkUnderline.Always);
        set { underline = value; Invalidate(); }
    }

    /// <summary>实例上写过的档位（设计器只序列化这些）。</summary>
    internal (bool Tone, bool Underline) Explicit => (tone.Value is not null, underline.Value is not null);

    LinkDefaults? ScopeDefaults => Scope?.ResolveDefaults<LinkDefaults>();

    /// <summary>外观覆盖（上游的 sx）：盖过作用域的 <see cref="LinkSx"/> 与配方。</summary>
    public LinkSx? Sx { get; set { field = value; Invalidate(); } }

    /// <summary>合并好的外观覆盖：作用域的在下，实例的在上。</summary>
    LinkSx? ResolvedSx => Scope?.ResolveStyles(Sx) ?? Sx;

    /// <summary>外部链接：末尾显示外链图标。</summary>
    public bool External { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>指针在上面（宿主设）：hover 档在这时画下划线。</summary>
    public bool IsHovered { get; set { field = value; Invalidate(); } }

    /// <summary>键盘聚焦（宿主设）：画焦点框。</summary>
    public bool IsFocusVisible { get; set { field = value; Invalidate(); } }


    /// <summary>里面的环境：下划线自己画（颜色与文字不同），字体不带下划线。</summary>
    static ElementContext Inner(ElementContext context)
    {
        var style = context.Font.Style & ~FontStyle.Underline;
        return style == context.Font.Style ? context : context with { Font = Fonts.Get(context.Font.FamilyName(), context.Font.SizeInPoints, style) };
    }

    /// <summary>要画下划线：always，或 hover 时指针在上面。</summary>
    internal bool UnderlineShown => Underline == LinkUnderline.Always || (Underline == LinkUnderline.Hover && IsHovered);

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
        // 次要链接（muted：导航、页脚）悬停时换成正文色：不画下划线时也有悬停反馈
        var tone = Tone == TextTone.Muted && IsHovered ? TextTone.Neutral : Tone;
        var inner = Inner(context) with { ForeColor = ResolvedSx?.Foreground ?? tone.ColorIn(context) };
        var size = text.Measure(inner, new Size(int.MaxValue, int.MaxValue));
        text.Bounds = new Rectangle(Bounds.X, Bounds.Y + (Bounds.Height - size.Height) / 2, size.Width, size.Height);
        text.Paint(graphics, inner);
        if (UnderlineShown)
        {
            // 下划线平时是半透明的文字色，悬停时实色（同上游）：一直有下划线的链接也有悬停反馈；位置在基线下 0.12em，粗细约 1/14em
            var fore = inner.ForeColor;
            using var pen = new Pen(IsHovered ? fore : Color.FromArgb(fore.A / 2, fore), MathF.Max(1, MathF.Round(inner.Em / 14)));
            var y = text.Bounds.Y + (text.Baseline(inner) ?? text.Bounds.Height) + MathF.Round(0.12f * inner.Em);
            graphics.DrawLine(pen, text.Bounds.Left, y, text.Bounds.Right, y);
        }
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
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> UnderlineScopes = [];

    /// <summary>
    /// 容器给里面的链接默认的下划线（上游的 LinkUnderlineScope；Link 上写了 Underline 时照写的）：卡片页头的操作链接给 hover。
    /// 容器自己与它里面的 LinkControl 都算；设了之后里面的链接重画。
    /// </summary>
    /// <param name="container">容器（也可以是链接控件自己）。</param>
    /// <param name="value">下划线；null 时去掉。</param>
    public static void SetUnderlineScope(Control container, LinkUnderline? value)
    {
        UnderlineScopes.Remove(container);
        if (value is { } underline) UnderlineScopes.Add(container, underline);
        foreach (var link in Descendants(container).OfType<LinkControl>()) link.Invalidate();

        static IEnumerable<Control> Descendants(Control control) => [control, .. control.Controls.Cast<Control>().SelectMany(Descendants)];
    }

    /// <summary>控件所在的容器给的下划线：从它自己往上找最近的一层；没有时 null。</summary>
    /// <param name="control">链接的宿主。</param>
    internal static LinkUnderline? UnderlineScopeOf(Control control)
    {
        for (Control? node = control; node is not null; node = node.Parent)
            if (UnderlineScopes.TryGetValue(node, out var value)) return (LinkUnderline)value;
        return null;
    }

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

    /// <summary>外观覆盖（代码里设，上游的 sx）：盖过作用域的 <see cref="LinkSx"/> 与配方。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LinkSx? Sx { get => Element.Sx; set => Element.Sx = value; }

    /// <summary>链接文字。</summary>
    [AllowNull]
    public override string Text { get => base.Text; set => base.Text = value; }

    bool ShouldSerializeTone() => Element.Explicit.Tone;

    void ResetTone() => Tone = default;

    bool ShouldSerializeUnderline() => Element.Explicit.Underline;

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
    protected virtual void Navigate(Uri href) => LinkLauncher.Open(href);

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
            get => owner.External ? base.Name + ProviderControl.LangOf(owner).LinkNewWindow : base.Name;
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

/// <summary>Link 在作用域里的默认值（上游的 <c>defaults.Link</c>）：<c>scope.Defaults.Set(new LinkDefaults { Underline = LinkUnderline.Hover });</c></summary>
public sealed record LinkDefaults : IScopeRecord<LinkDefaults>
{
    /// <summary>色调。</summary>
    public TextTone? Tone { get; init; }

    /// <summary>下划线。</summary>
    public LinkUnderline? Underline { get; init; }

    /// <inheritdoc />
    public LinkDefaults Merge(LinkDefaults? over) => over is null ? this : new() { Tone = over.Tone ?? Tone, Underline = over.Underline ?? Underline };
}

/// <summary>Link 的外观覆盖（上游的 <c>sx</c> 与 <c>components.Link.styles</c>）：作用域的在下，实例的 Sx 在上，盖过色调。</summary>
public sealed record LinkSx : IScopeRecord<LinkSx>
{
    /// <summary>文字、下划线、外链图标的颜色。</summary>
    public Color? Foreground { get; init; }

    /// <inheritdoc />
    public LinkSx Merge(LinkSx? over) => over is null ? this : new() { Foreground = over.Foreground ?? Foreground };
}
