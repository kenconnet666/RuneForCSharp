using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 面包屑（vue-tsx-pro 的 Breadcrumbs）：当前页在站点层级里的位置。最后一项是当前页（正文色、中等字重，读屏的说明是"当前页"，不是链接），
/// 其余有地址的是链接（次要色，悬停时下划线），分隔符默认是右向的 chevron，从右到左排版时翻转。整条 sm 字号，放不下时换行。
/// <code>
/// var breadcrumbs = new BreadcrumbsControl { Items = [new("首页", "#home", Lucide.House), new("订单", "#orders"), new("#10234")], MaxItems = 4 };
/// </code>
/// 路径太长时用 <see cref="MaxItems"/> 折叠中间部分（保留第一项与最后两项）：折叠处是一个菜单按钮，被折叠的各级在菜单里（同 Radix Themes）。
/// 应用内的层级（文件夹、组织架构的下钻）没有地址：订阅了 <see cref="Navigate"/> 时，没有地址的各级是链接样式的按钮，点了交给它，
/// 当前一级的说明是"当前位置"。名称默认取语言包（面包屑），写 AccessibleName 可以改。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Navigate))]
[Description("面包屑：当前页在站点层级里的位置。")]
public class BreadcrumbsControl : FlexControl
{
    EventHandler<BreadcrumbNavigateEventArgs>? navigate;

    /// <summary>新建：横排、放不下时换行，项与项之间 space.xs。</summary>
    public BreadcrumbsControl()
    {
        Wrap = FlexWrap.Wrap;
        Align = FlexAlign.Center;
        Gap = Space.Xs;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
    }

    /// <summary>路径，从根到当前页；最后一项是当前页。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<BreadcrumbItem>? Items { get; set { field = value; Rebuild(); } }

    /// <summary>分隔符的图标；不写时是右向的 chevron。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconData? Separator { get; set { field = value; Rebuild(); } }

    /// <summary>超过这么多项时折叠中间部分；不写时不折叠。</summary>
    [Category("Behavior")]
    [Description("超过这么多项时折叠中间部分（保留第一项与最后两项）。")]
    [DefaultValue(null)]
    public int? MaxItems { get; set { field = value; Rebuild(); } }

    /// <summary>点了没有地址的一级（应用内的层级导航）；订阅了它，没有地址的各级才是按钮。</summary>
    public event EventHandler<BreadcrumbNavigateEventArgs>? Navigate
    {
        add
        {
            navigate += value;
            Rebuild();
        }
        remove
        {
            navigate -= value;
            Rebuild();
        }
    }

    bool Navigable => navigate is not null;

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ApplyTextSize();
    }

    /// <inheritdoc />
    protected override void OnParentFontChanged(EventArgs e)
    {
        base.OnParentFontChanged(e);
        ApplyTextSize();
    }

    /// <summary>整条 sm 字号（字体族、样式跟着外面）。</summary>
    void ApplyTextSize()
    {
        var outer = Parent?.Font ?? Font;
        Font = Fonts.Get(outer.FamilyName(), Tokens.FontSize.Sm * 72 / 96, outer.Style);
    }

    /// <summary>按路径重建各项：折叠时留第一项与最后两项，中间换成菜单按钮（maxItems 写得比 3 小也一样，再多出来的才有东西可折）。</summary>
    void Rebuild()
    {
        SuspendLayout();
        Controls.Cast<Control>().DisposeAll();
        var lang = ProviderControl.LangOf(this);
        var items = Items ?? [];
        var collapse = MaxItems is { } max && items.Count > Math.Max(max, 3);
        IReadOnlyList<int> shown = collapse ? [0, -1, items.Count - 2, items.Count - 1] : [.. Enumerable.Range(0, items.Count)];
        for (var position = 0; position < shown.Count; position++)
        {
            var row = new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs, AutoSize = true };
            row.Controls.Add(shown[position] < 0 ? Collapsed(items, lang) : Entry(items, shown[position]));
            if (position < shown.Count - 1) row.Controls.Add(SeparatorIcon());
            Controls.Add(row);
        }
        ResumeLayout();
    }

    /// <summary>一级：当前页、链接、按钮（应用内导航）或纯文字。</summary>
    Control Entry(IReadOnlyList<BreadcrumbItem> items, int index)
    {
        var item = items[index];
        if (index == items.Count - 1)
        {
            var current = Label(item, default, new TextControl { Text = item.Label, Weight = TextWeight.Medium });
            current.AccessibleDescription = Navigable ? "当前位置" : "当前页";
            return current;
        }
        if (item.Href is { } href)
            return Label(item, TextTone.Muted, new LinkControl { Text = item.Label, Href = new Uri(href, UriKind.RelativeOrAbsolute), Tone = TextTone.Muted, Underline = LinkUnderline.Hover });
        if (!Navigable) return Label(item, default, new TextControl { Text = item.Label });
        var button = new ButtonControl { Variant = ButtonVariant.Link, Text = item.Label, Icon = item.Icon, TextSize = TextSize.Sm };
        button.Click += (_, _) => navigate?.Invoke(this, new BreadcrumbNavigateEventArgs(item, index));
        return button;
    }

    /// <summary>折叠处：菜单按钮，被折叠的各级在菜单里（有地址的是链接项，没有的交给 Navigate）。</summary>
    MenuControl Collapsed(IReadOnlyList<BreadcrumbItem> items, Lang lang)
    {
        var menu = new MenuControl { Trigger = new IconButtonControl { Icon = Lucide.Ellipsis, TextSize = TextSize.Sm, AccessibleName = lang.BreadcrumbsExpand } };
        for (var index = 1; index < items.Count - 2; index++)
        {
            var hidden = items[index];
            if (hidden.Href is { } href)
            {
                menu.Items.Add(new MenuLinkItemControl { Href = new Uri(href, UriKind.RelativeOrAbsolute), Icon = hidden.Icon, Text = hidden.Label });
                continue;
            }
            var entry = new MenuItemControl { Icon = hidden.Icon, Text = hidden.Label, Enabled = Navigable };
            var at = index;
            entry.Click += (_, _) => navigate?.Invoke(this, new BreadcrumbNavigateEventArgs(hidden, at));
            menu.Items.Add(entry);
        }
        return menu;
    }

    /// <summary>文字前有图标时是一行（图标、文字，间距 space.xs；链接的图标同链接是次要色）；没有图标时就是文字本身。</summary>
    static Control Label(BreadcrumbItem item, TextTone iconTone, Control text)
    {
        if (item.Icon is not { } icon) return text;
        var label = new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs, AutoSize = true };
        label.Controls.AddRange([new IconControl { Data = icon, Tone = iconTone }, text]);
        return label;
    }

    /// <summary>分隔符：次要色，读屏不念（TS 的 aria-hidden）。</summary>
    IconControl SeparatorIcon() =>
        new() { Data = Separator ?? Lucide.ChevronRight, FlipInRtl = Separator is null, Tone = TextTone.Muted, AccessibleRole = AccessibleRole.None };

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new BreadcrumbsAccessibleObject(this);

    /// <summary>名称没写时取语言包（面包屑），换语言后跟着换。</summary>
    sealed class BreadcrumbsAccessibleObject(BreadcrumbsControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name : ProviderControl.LangOf(owner).Breadcrumbs;
    }
}

/// <summary>面包屑的一级。</summary>
/// <param name="Label">显示的文字。</param>
/// <param name="Href">地址；最后一项（当前页）、应用内的层级不需要。</param>
/// <param name="Icon">文字前的图标。</param>
public sealed record BreadcrumbItem(string Label, string? Href = null, IconData? Icon = null);

/// <summary>点了面包屑里没有地址的一级。</summary>
public sealed class BreadcrumbNavigateEventArgs(BreadcrumbItem item, int index) : EventArgs
{
    /// <summary>点的那一级。</summary>
    public BreadcrumbItem Item { get; } = item;

    /// <summary>那一级在 Items 里的位置。</summary>
    public int Index { get; } = index;
}
