using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 文字链接（vue-tsx-pro 的 Link）：默认带下划线，正文里的链接不能只靠颜色区分（WCAG 1.4.1）。是一段行内文字（Hyperlink），
/// 跟着段落换行，能用 Tab 聚焦、读屏念"链接"：
/// <code>
/// &lt;rune:Paragraph&gt;去 &lt;rune:Link Href="https://vuejs.org" External="True"&gt;Vue 文档&lt;/rune:Link&gt; 看看&lt;/rune:Paragraph&gt;
/// </code>
/// 同 <see cref="LinkButton"/>：<see cref="Href"/> 是绝对地址，点击后用系统打开；外部链接末尾一个外链图标、读屏名称后补"在新窗口打开"。
/// 色调（默认 accent）、下划线（always / hover / none）是主题样式的触发器。
/// 界面里的链接（卡片页头的操作）一列下来都带下划线太重：所在容器用 <see cref="UnderlineScopeProperty"/>（继承）给里面的链接默认的下划线
/// （上游的 LinkUnderlineScope）。取值：Link 上写的（本地值、绑定）> 所在容器给的 > 主题与 Provider 的默认（defaults.Link）> always。
/// </summary>
public class Link : Hyperlink
{
    /// <summary>链接地址（绝对地址，如 https://、mailto:）。</summary>
    public static readonly DependencyProperty HrefProperty = DependencyProperty.Register(nameof(Href), typeof(Uri), typeof(Link));

    /// <summary>外部链接：末尾显示外链图标，读屏名称后补上"在新窗口打开"。</summary>
    public static readonly DependencyProperty ExternalProperty = DependencyProperty.Register(
        nameof(External), typeof(bool), typeof(Link), new FrameworkPropertyMetadata(false, (d, _) => ((Link)d).UpdateIcon()));

    /// <summary>色调；主题里默认 accent（品牌色文字）。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(TextTone), typeof(Link));

    /// <summary>下划线，主题里默认 always。</summary>
    public static readonly DependencyProperty UnderlineProperty = DependencyProperty.Register(nameof(Underline), typeof(LinkUnderline), typeof(Link),
        new FrameworkPropertyMetadata(default(LinkUnderline), null, CoerceUnderline));

    /// <summary>所在容器给里面的链接的默认下划线（继承；上游的 LinkUnderlineScope）：Link 上写了 Underline 时照写的。</summary>
    public static readonly DependencyProperty UnderlineScopeProperty = DependencyProperty.RegisterAttached("UnderlineScope", typeof(LinkUnderline?), typeof(Link),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) => (d as Link)?.CoerceValue(UnderlineProperty)));

    /// <summary>容器给里面的链接的默认下划线。</summary>
    /// <param name="element">容器。</param>
    public static LinkUnderline? GetUnderlineScope(DependencyObject element) => (LinkUnderline?)element.GetValue(UnderlineScopeProperty);

    /// <inheritdoc cref="GetUnderlineScope" />
    /// <param name="element">容器。</param>
    /// <param name="value">下划线；null 时不改。</param>
    public static void SetUnderlineScope(DependencyObject element, LinkUnderline? value) => element.SetValue(UnderlineScopeProperty, value);

    /// <summary>Link 上写的（本地值、绑定）照用；否则所在容器给了就用它，再否则是主题与 Provider 的。</summary>
    static object CoerceUnderline(DependencyObject d, object baseValue) =>
        DependencyPropertyHelper.GetValueSource(d, UnderlineProperty).BaseValueSource != BaseValueSource.Local && GetUnderlineScope(d) is { } scoped ? scoped : baseValue;

    InlineUIContainer? icon;

    static Link()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Link), new FrameworkPropertyMetadata(typeof(Link)));
        TextDecorationsProperty.OverrideMetadata(typeof(Link), new FrameworkPropertyMetadata(null, null, CoerceDecorations));
        ForegroundProperty.OverrideMetadata(typeof(Link), new FrameworkPropertyMetadata((d, _) => d.CoerceValue(TextDecorationsProperty)));
    }

    /// <summary>
    /// 主题样式给的下划线换成文字色画的：平时半透明（50%），悬停时实色（同上游）——一直有下划线的链接也有悬停反馈，各色调都适用。
    /// </summary>
    static object? CoerceDecorations(DependencyObject d, object? baseValue)
    {
        if (baseValue is not TextDecorationCollection { Count: > 0 } || d is not Link { Foreground: SolidColorBrush fore } link) return baseValue;
        var pen = new Pen(new SolidColorBrush(fore.Color) { Opacity = (link.IsMouseOver ? 1 : 0.5) * fore.Opacity }, 1);
        pen.Freeze();
        return new TextDecorationCollection { new TextDecoration(TextDecorationLocation.Underline, pen, 0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended) };
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        CoerceValue(TextDecorationsProperty);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        CoerceValue(TextDecorationsProperty);
    }

    /// <summary>新建链接：载入后把外链图标挪到最后（XAML 里 External 先于文字赋值，图标会排在前面）。</summary>
    public Link() => Loaded += (_, _) =>
    {
        if (icon is null || Inlines.LastInline == icon) return;
        Inlines.Remove(icon);
        Inlines.Add(icon);
    };

    /// <summary>链接地址。</summary>
    public Uri? Href { get => (Uri?)GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>外部链接。</summary>
    public bool External { get => (bool)GetValue(ExternalProperty); set => SetValue(ExternalProperty, value); }

    /// <summary>色调。</summary>
    public TextTone Tone { get => (TextTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>下划线。</summary>
    public LinkUnderline Underline { get => (LinkUnderline)GetValue(UnderlineProperty); set => SetValue(UnderlineProperty, value); }

    /// <summary>外链图标（TS 的 propsIcon）：部件样式键 <see cref="LinkParts.Icon"/>。</summary>
    internal Icon? IconPart => icon?.Child as Icon;

    /// <summary>先触发 Click、Command，再打开链接。</summary>
    protected override void OnClick()
    {
        base.OnClick();
        if (Href is { IsAbsoluteUri: true } href) Navigate(href);
    }

    /// <summary>用系统打开链接（浏览器、邮件……）。</summary>
    /// <param name="href">绝对地址。</param>
    protected virtual void Navigate(Uri href) => LinkLauncher.Open(href);

    /// <summary>外部链接末尾 0.1em 外一个外链图标（行内的图标容器，跟着文字）。</summary>
    void UpdateIcon()
    {
        if (External && icon is null)
        {
            var part = new Icon { Data = Lucide.ArrowUpRight };
            part.SetResourceReference(FrameworkElement.StyleProperty, LinkParts.Icon);
            Inlines.Add(icon = new InlineUIContainer(part) { BaselineAlignment = BaselineAlignment.Center });
        }
        else if (!External && icon is not null)
        {
            Inlines.Remove(icon);
            icon = null;
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LinkPeer(this);

    /// <summary>外部链接的名称后补上语言包的"在新窗口打开"。</summary>
    sealed class LinkPeer(Link owner) : HyperlinkAutomationPeer(owner)
    {
        protected override string GetNameCore() => owner.External ? base.GetNameCore() + Provider.GetLang(owner).LinkNewWindow : base.GetNameCore();
    }
}

/// <summary>Link 的部件样式键。</summary>
public static class LinkParts
{
    /// <summary>外链图标（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(Link), nameof(Icon), typeof(Icon));
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
