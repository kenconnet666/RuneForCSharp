using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 页头（vue-tsx-pro 的 PageHeader）：面包屑、返回、标题与标记、说明、结束边的操作，下面还可以放摘要或标签页。列表页、详情页、表单页
/// 开头都是这一块，不用每页自己拿 FlexControl 拼。
/// <code>
/// var header = new PageHeaderControl { Title = "PO-2026-0187", Description = "华辰精密 · 2026-10-20 下单", Meta = statusBadge, Actions = buttons };
/// header.Back += (_, _) => NavigateBack();
/// header.Controls.Add(summary);   // 页头下面的内容
/// </code>
/// <list type="bullet">
/// <item>标题是 H1（<see cref="HeadingLevel"/> 可改成 2、3：嵌在页面里的一块），字号固定 xxl；说明是次要色的段落。</item>
/// <item>操作靠结束边，放不下时整块换到标题下面一行。<see cref="Meta"/> 跟在标题后面（状态、标签）。</item>
/// <item>返回：订阅了 <see cref="Back"/> 就显示返回按钮；给了 <see cref="BackHref"/> 时返回是链接（点击时同样发 Back）。名称取语言包（"返回"）。</item>
/// <item>放进来的子控件排在最下面：摘要（DescriptionsControl）、标签页。</item>
/// </list>
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Back))]
[Description("页头：面包屑、返回、标题、说明、操作。")]
public class PageHeaderControl : FlexControl
{
    readonly BreadcrumbsControl breadcrumbs = new() { Visible = false };
    readonly FlexControl main = new() { Wrap = FlexWrap.Wrap, Align = FlexAlign.Start, Gap = Space.Md, AutoSize = true };
    readonly FlexControl heading = new() { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true };
    readonly FlexControl titleRow = new() { Wrap = FlexWrap.Wrap, Align = FlexAlign.Center, ColumnGap = Space.Md, RowGap = Space.Xs, AutoSize = true };
    readonly ParagraphControl description = new() { Tone = TextTone.Muted, Visible = false };
    readonly FlexControl actions = new() { Wrap = FlexWrap.Wrap, Align = FlexAlign.Center, Justify = FlexJustify.End, Gap = Space.Sm, AutoSize = true, Visible = false };
    Control title = new H1Control { TextSize = TextSize.Xxl };
    Control? back;
    EventHandler? backHandlers;

    /// <summary>新建：竖排，标题是 H1。</summary>
    public PageHeaderControl()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Md;
        Align = FlexAlign.Stretch;
        AutoSize = true;
        titleRow.Controls.Add(title);
        heading.Controls.AddRange([titleRow, description]);
        main.Controls.AddRange([heading, actions]);
        // 标题一侧占满剩下的宽度，操作靠结束边；换到下一行时也靠结束边（TS 的 margin-inline-start: auto）
        main.SetGrow(heading, true);
        main.SetGrow(actions, true);
        Controls.AddRange([breadcrumbs, main]);
    }

    /// <summary>标题。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Title { get; set { field = value; title.Text = value ?? ""; } }

    /// <summary>标题下面的说明。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Description
    {
        get;
        set
        {
            field = value;
            description.Text = value ?? "";
            description.Visible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>标题的级别（1–3），默认 1：H1–H3 是不同的控件（读屏念出级别）。</summary>
    [Category("Behavior")]
    [DefaultValue(1)]
    public int HeadingLevel
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 3);
            field = value;
            PlaceTitle();
        }
    } = 1;

    /// <summary>面包屑：最后一项是当前页。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<BreadcrumbItem>? Breadcrumbs
    {
        get;
        set
        {
            field = value;
            breadcrumbs.Items = value;
            breadcrumbs.Visible = value is { Count: > 0 };
        }
    }

    /// <summary>标题后面的标记：状态、标签（一个控件）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Meta
    {
        get;
        set
        {
            if (field is not null) titleRow.Controls.Remove(field);
            field = value;
            if (value is not null) titleRow.Controls.Add(value);
        }
    }

    /// <summary>结束边的操作：按钮（一个控件，几个时放进一个 FlexControl）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Actions
    {
        get;
        set
        {
            actions.Controls.Clear();
            field = value;
            if (value is not null) actions.Controls.Add(value);
            actions.Visible = value is not null;
        }
    }

    /// <summary>返回的地址：给了时返回是一个链接按钮。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public Uri? BackHref { get; set { field = value; UpdateBack(); } }

    /// <summary>点了返回（TS 的 onBack）；订阅了它（或给了 BackHref）才显示返回按钮。</summary>
    public event EventHandler? Back
    {
        add
        {
            backHandlers += value;
            UpdateBack();
        }
        remove
        {
            backHandlers -= value;
            UpdateBack();
        }
    }

    /// <summary>标题与返回按钮（测试用）。</summary>
    internal (Control Title, Control? Back) Parts => (title, back);

    void PlaceTitle()
    {
        titleRow.Controls.Remove(title);
        title.Dispose();
        title = HeadingLevel switch { 2 => new H2Control { TextSize = TextSize.Xxl }, 3 => new H3Control { TextSize = TextSize.Xxl }, _ => new H1Control { TextSize = TextSize.Xxl } };
        title.Text = Title ?? "";
        titleRow.Controls.Add(title);
        titleRow.Controls.SetChildIndex(title, 0);
    }

    /// <summary>返回按钮：给了 BackHref 是图标链接按钮，否则订阅了 Back 时是图标按钮；ghost、中性。</summary>
    void UpdateBack()
    {
        var wanted = BackHref is not null || backHandlers is not null;
        if (back is not null && (!wanted || (back is IconLinkButtonControl) != (BackHref is not null)))
        {
            main.Controls.Remove(back);
            back.Dispose();
            back = null;
        }
        if (back is IconLinkButtonControl link) link.Href = BackHref;
        if (!wanted || back is not null) return;
        var name = PatternsMessages.For(ProviderControl.LangOf(this)).PageHeaderBack;
        back = BackHref is { } href
            ? new IconLinkButtonControl { Href = href, Icon = Lucide.ArrowLeft, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, AccessibleName = name }
            : new IconButtonControl { Icon = Lucide.ArrowLeft, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, AccessibleName = name };
        back.Click += (_, _) => backHandlers?.Invoke(this, EventArgs.Empty);
        main.Controls.Add(back);
        main.Controls.SetChildIndex(back, 0);
    }
}
