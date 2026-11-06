using System.ComponentModel;
using global::Avalonia;
using global::Avalonia.Automation.Peers;

namespace Rune.Avalonia;

/// <summary>
/// 文字链接（vue-tsx-pro 的 Link）：默认带下划线，正文里的链接不能只靠颜色区分（WCAG 1.4.1）。读屏念"链接"。
/// <code>
/// &lt;rune:Link Href="https://vuejs.org" External="True"&gt;Vue 文档&lt;/rune:Link&gt;
/// &lt;rune:Link Href="…" Underline="hover" Tone="muted"&gt;导航里的次要链接&lt;/rune:Link&gt;
/// </code>
/// Avalonia 的段落里没有可点击的行内文字：Link 是一个控件，放进一段文字时用 InlineUIContainer，链接文字本身不跨行换行。
/// 同 <see cref="LinkButton"/>：<see cref="Href"/> 是绝对地址，点击后用系统打开；外部链接末尾一个外链图标、读屏名称后补"在新窗口打开"。
/// 键盘聚焦有焦点环，不可用（含可聚焦的不可用）时不打开。
/// </summary>
public class Link : ButtonBase
{
    /// <summary>链接地址（绝对地址，如 https://、mailto:）。</summary>
    public static readonly StyledProperty<Uri?> HrefProperty = LinkButton.HrefProperty.AddOwner<Link>();

    /// <summary>外部链接：末尾显示外链图标，读屏名称后补上"在新窗口打开"。</summary>
    public static readonly StyledProperty<bool> ExternalProperty = LinkButton.ExternalProperty.AddOwner<Link>();

    /// <summary>色调；不写时 accent（品牌色文字）。</summary>
    public static readonly StyledProperty<TextTone> ToneProperty = AvaloniaProperty.Register<Link, TextTone>(nameof(Tone));

    /// <summary>下划线，主题里默认 always。</summary>
    public static readonly StyledProperty<LinkUnderline> UnderlineProperty = AvaloniaProperty.Register<Link, LinkUnderline>(nameof(Underline));

    readonly TemplateResources resources;

    /// <summary>新建链接：先按默认色调（accent）取色。</summary>
    public Link()
    {
        resources = new TemplateResources(this);
        resources.Bind(ForegroundProperty, TextTone.Accent.BrushKey);
    }

    /// <summary>链接地址。</summary>
    public Uri? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>外部链接。</summary>
    public bool External { get => GetValue(ExternalProperty); set => SetValue(ExternalProperty, value); }

    /// <summary>色调。</summary>
    public TextTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>下划线。</summary>
    public LinkUnderline Underline { get => GetValue(UnderlineProperty); set => SetValue(UnderlineProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // 色调的文字色按模板优先级（同 Text），带类或伪类的样式盖得过
        if (change.Property == ToneProperty) resources.Bind(ForegroundProperty, (Tone.Value is null ? TextTone.Accent : Tone).BrushKey);
        else if (change.Property == UnderlineProperty)
            PseudoClasses.Swap(Pseudo(change.GetOldValue<LinkUnderline>()), Pseudo(Underline));
    }

    static string? Pseudo(LinkUnderline underline) => underline.Value is null ? null : $"underline-{underline.Value}";

    /// <summary>先触发 Click、Command，再打开链接；加载中、不可用时都不执行。</summary>
    protected override void OnClick()
    {
        base.OnClick();
        if (!IsLoading && !IsDisabled && Href is { IsAbsoluteUri: true } href) Navigate(href);
    }

    /// <summary>用系统打开链接（浏览器、邮件……）。</summary>
    /// <param name="href">绝对地址。</param>
    protected virtual void Navigate(Uri href) => LinkLauncher.Open(this, href);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LinkAutomationPeer(this, () => External);
}

/// <summary>Link 的部件名。</summary>
public static class LinkParts
{
    /// <summary>外链图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";
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
