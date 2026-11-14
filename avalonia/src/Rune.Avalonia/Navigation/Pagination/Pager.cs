using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;

namespace Rune.Avalonia;

/// <summary>
/// 翻页导航（vue-tsx-pro 的 Pager）：文章末尾的"上一篇 / 下一篇"，两张链接卡片（提示一行带箭头、次要色 sm，标题 accentText、中等字重；
/// border 边框、md 圆角，悬停时边框换品牌色）。够宽时并排两列，窄时上下排；只有下一篇时它仍在结束边。页码式的分页用 <see cref="Pagination"/>。
/// <code>
/// &lt;rune:Pager Previous="{Binding Previous}" Next="{Binding Next}" /&gt;
/// </code>
/// 名称默认取语言包（翻页）。
/// </summary>
public class Pager : Control
{
    /// <summary>上一篇；第一篇没有。</summary>
    public static readonly StyledProperty<PagerLink?> PreviousProperty = AvaloniaProperty.Register<Pager, PagerLink?>(nameof(Previous));

    /// <summary>下一篇；最后一篇没有。</summary>
    public static readonly StyledProperty<PagerLink?> NextProperty = AvaloniaProperty.Register<Pager, PagerLink?>(nameof(Next));

    // 够宽时两列（TS 的 repeat(auto-fit, minmax(min(100%, 14rem), 1fr))）：每张卡片至少 14rem、平分；放不下时换行
    readonly Flex root = new() { Wrap = FlexWrap.Wrap, Gap = Space.Md };

    /// <summary>新建：没有链接。读屏里是导航地标。</summary>
    public Pager()
    {
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Navigation);
    }

    /// <summary>上一篇。</summary>
    public PagerLink? Previous { get => GetValue(PreviousProperty); set => SetValue(PreviousProperty, value); }

    /// <summary>下一篇。</summary>
    public PagerLink? Next { get => GetValue(NextProperty); set => SetValue(NextProperty, value); }

    /// <summary>两张卡片（测试用）。</summary>
    internal IEnumerable<PagerLinkCard> Cards => root.Children.OfType<PagerLinkCard>();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PreviousProperty || change.Property == NextProperty) Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Rebuild();
    }

    void Rebuild()
    {
        root.Children.Clear();
        var lang = Provider.GetLang(this);
        // 只有下一篇时前面空一格：下一篇仍在结束边那一列
        Control first = Previous is { } previous ? new PagerLinkCard(previous, next: false, lang) : new Panel();
        if (Previous is not null || Next is not null) Add(first);
        if (Next is { } next) Add(new PagerLinkCard(next, next: true, lang));
    }

    void Add(Control card)
    {
        card.MinWidth = 14 * 16;
        Flex.SetGrow(card, true);
        root.Children.Add(card);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new PagerPeer(this);

    /// <summary>名称没写时取语言包（翻页）。</summary>
    sealed class PagerPeer(Pager owner) : ControlAutomationPeer(owner)
    {
        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).PagerLabel;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

/// <summary>翻页导航的一侧。</summary>
/// <param name="Href">地址。</param>
/// <param name="Label">文章标题。</param>
public sealed record PagerLink(string Href, string Label);

/// <summary>翻页导航的一张链接卡片：提示（上一篇、下一篇，带箭头）与标题；点了打开地址。下一篇靠结束边对齐。</summary>
public class PagerLinkCard : ButtonBase
{
    internal PagerLinkCard(PagerLink link, bool next, Lang lang)
    {
        Link = link;
        Name = PagerParts.Link;
        var arrow = new Icon { Data = next ? Lucide.ArrowRight : Lucide.ArrowLeft, FlipInRtl = true };
        var hintText = new TextBlock { Text = next ? lang.PagerNext : lang.PagerPrevious };
        var hint = new Flex { Name = PagerParts.Hint, Align = FlexAlign.Center, Gap = Space.Xs };
        hint.Children.AddRange(next ? [hintText, arrow] : [arrow, hintText]);
        var align = next ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        hint.HorizontalAlignment = align;
        var label = new TextBlock { Name = PagerParts.Label, Text = link.Label, HorizontalAlignment = align, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        Content = new StackPanel { Children = { hint, label } };
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Click += (_, _) => LinkLauncher.Open(this, new Uri(link.Href, UriKind.RelativeOrAbsolute));
    }

    /// <summary>这一侧的链接。</summary>
    public PagerLink Link { get; }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ButtonBase);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);

    sealed class CardPeer(PagerLinkCard owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Link.Label;
    }
}

/// <summary>Pager 的部件名（样式选择器用）。</summary>
public static class PagerParts
{
    /// <summary>链接卡片（rune:PagerLinkCard；样式键是 rune:ButtonBase，选择器写 rune|ButtonBase#PART_Link）。</summary>
    public const string Link = "PART_Link";

    /// <summary>提示一行（rune:Flex：箭头与"上一篇 / 下一篇"）。</summary>
    public const string Hint = "PART_Hint";

    /// <summary>文章标题（TextBlock）。</summary>
    public const string Label = "PART_Label";
}
