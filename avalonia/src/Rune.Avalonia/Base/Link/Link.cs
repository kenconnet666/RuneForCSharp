using System.ComponentModel;
using global::Avalonia;
using global::Avalonia.Automation.Peers;
using global::Avalonia.Controls;
using global::Avalonia.Media;

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
/// 界面里的链接（卡片页头的操作）一列下来都带下划线太重：所在容器用 <see cref="UnderlineScopeProperty"/>（继承）给里面的链接默认的下划线
/// （上游的 LinkUnderlineScope）。取值：Link 上写的（本地值、绑定）> 所在容器给的 > 主题与 Provider 的默认（defaults.Link）> always。
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

    /// <summary>所在容器给里面的链接的默认下划线（继承；上游的 LinkUnderlineScope）：Link 上写了 Underline 时照写的。</summary>
    public static readonly AttachedProperty<LinkUnderline?> UnderlineScopeProperty =
        AvaloniaProperty.RegisterAttached<Link, Control, LinkUnderline?>("UnderlineScope", inherits: true);

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

    /// <summary>容器给里面的链接的默认下划线。</summary>
    /// <param name="control">容器。</param>
    public static LinkUnderline? GetUnderlineScope(Control control) => control.GetValue(UnderlineScopeProperty);

    /// <inheritdoc cref="GetUnderlineScope" />
    /// <param name="control">容器。</param>
    /// <param name="value">下划线；null 时不改。</param>
    public static void SetUnderlineScope(Control control, LinkUnderline? value) => control.SetValue(UnderlineScopeProperty, value);

    /// <summary>实际的下划线：Link 上写的（本地值、绑定）优先，其次所在容器给的，再其次主题与 Provider 的。</summary>
    LinkUnderline EffectiveUnderline =>
        global::Avalonia.Diagnostics.AvaloniaObjectExtensions.GetDiagnostic(this, UnderlineProperty).Priority > global::Avalonia.Data.BindingPriority.LocalValue && GetValue(UnderlineScopeProperty) is { } scoped ? scoped : Underline;

    LinkUnderline shownUnderline;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty) BindTone();
        else if (change.Property == UnderlineProperty || change.Property == UnderlineScopeProperty)
        {
            var next = EffectiveUnderline;
            PseudoClasses.Swap(Pseudo(shownUnderline), Pseudo(next));
            shownUnderline = next;
            UpdateUnderline();
        }
        else if (change.Property == ForegroundProperty) UpdateUnderline();
        else if (change.Property == IsPointerOverProperty || change.Property == IsEnabledProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            UpdateUnderline();
            if (Tone == TextTone.Muted) BindTone();
        }
    }

    /// <summary>
    /// 色调的文字色按模板优先级（同 Text），带类或伪类的样式盖得过。次要链接（muted：导航、页脚）悬停时换成正文色：不画下划线时也有悬停反馈。
    /// </summary>
    void BindTone()
    {
        var tone = Tone.Value is null ? TextTone.Accent : Tone == TextTone.Muted && IsPointerOver && IsEffectivelyEnabled ? TextTone.Neutral : Tone;
        resources.Bind(ForegroundProperty, tone.BrushKey);
    }

    global::Avalonia.Controls.Presenters.ContentPresenter? presenter;
    IDisposable? underline;

    /// <inheritdoc />
    protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        presenter = e.NameScope.Find<global::Avalonia.Controls.Presenters.ContentPresenter>("PART_ContentPresenter");
        UpdateUnderline();
    }

    /// <summary>
    /// 下划线：always 总有，hover 在指针在上面（可聚焦的不可用时除外）时有。按模板优先级设在内容呈现器上，
    /// TextDecorations 是继承的：纯文字、文字加图标（图标不画下划线）里的文字都带上（控件主题里写不了子选择器）。
    /// </summary>
    void UpdateUnderline()
    {
        underline?.Dispose();
        underline = null;
        if (presenter is null) return;
        var effective = EffectiveUnderline;
        var shown = effective.Value is null or "always" || (effective.Value == "hover" && IsPointerOver && !IsDisabled);
        if (!shown) return;
        // 下划线平时是半透明的文字色，悬停时实色（同上游）：一直有下划线的链接也有悬停反馈，各色调都适用
        var stroke = Foreground is ISolidColorBrush fore ? new SolidColorBrush(fore.Color, (IsPointerOver && !IsDisabled ? 1 : 0.5) * fore.Opacity) : null;
        var decorations = new TextDecorationCollection { new TextDecoration { Location = TextDecorationLocation.Underline, Stroke = stroke } };
        underline = presenter.SetValue(global::Avalonia.Controls.Documents.Inline.TextDecorationsProperty, decorations, global::Avalonia.Data.BindingPriority.Template);
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
