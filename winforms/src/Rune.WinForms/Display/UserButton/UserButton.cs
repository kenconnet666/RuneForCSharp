using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 用户按钮（vue-tsx-pro 的 UserButton）：头像 + 名字（可带一行说明）+ 展开箭头，做账号菜单、切换账号的触发器；放在 AppShell 顶栏、侧栏底部。
/// 无窗口元素，放进窗体用 <see cref="UserButtonControl"/>。建在按钮内核上（ghost、neutral：正文色，悬停 fillHover 底）：
/// 没有说明时是一颗胶囊（上下与起始边 space.xs，结束边 space.sm，圆角 full）；有说明且不 <see cref="Compact"/> 时占满宿主给的宽度
/// （左右 space.sm、圆角 md），箭头换成上下双箭头、贴着结束边；Compact 时只剩头像（四周 space.xs）。
/// 头像 sm；名字 medium、0.93em，说明 textMuted、0.86em，放不下时省略；箭头 textMuted。
/// </summary>
public class UserButton : ButtonCore
{
    readonly Avatar avatar = new() { Size = TextSize.Sm };
    Text name = new();
    Text description = new();
    readonly Icon indicator = new();

    /// <summary>新建：部件套上默认属性。</summary>
    public UserButton() => RebuildParts();

    /// <summary>名字：显示在头像旁边，是按钮的名称。</summary>
    public string? DisplayName { get; set { field = value; avatar.DisplayName = value; name.Content = value ?? ""; InvalidateMeasure(); } }

    /// <summary>名字下面的一行说明：部门、角色、邮箱。</summary>
    public string? Description { get; set { field = value; description.Content = value ?? ""; RebuildIndicator(); } }

    /// <summary>头像图片；没有时显示名字的首字母。</summary>
    public Image? Source { get => avatar.Source; set => avatar.Source = value; }

    /// <summary>只显示头像（顶栏空间紧、侧栏收起时）。</summary>
    public bool Compact { get; set { field = value; RebuildIndicator(); } }

    /// <summary>结束边的箭头；不写时 ChevronDown，有说明时 ChevronsUpDown。</summary>
    public IconData? Indicator { get; set { field = value; RebuildIndicator(); } }

    /// <summary>显示结束边的箭头，默认是（TS 的 indicator={false} 是 false）。</summary>
    public bool ShowIndicator { get; set { field = value; InvalidateMeasure(); } } = true;

    /// <summary>名字部件的属性（TS 的 propsName）。</summary>
    public TextProps? PropsName { get; set { field = value; RebuildParts(); } }

    /// <summary>说明部件的属性（TS 的 propsDescription）。</summary>
    public TextProps? PropsDescription { get; set { field = value; RebuildParts(); } }

    /// <summary>箭头部件的属性（TS 的 propsIndicator）。</summary>
    public IconProps? PropsIndicator { get; set { field = value; RebuildIndicator(); } }

    /// <summary>有说明：不 compact 时整行显示。</summary>
    public bool IsDescribed => !string.IsNullOrEmpty(Description);

    /// <summary>整行的形态：有说明且不 compact。</summary>
    bool IsRow => IsDescribed && !Compact;

    /// <summary>头像部件（测试用）。</summary>
    internal Avatar AvatarPart => avatar;

    /// <summary>名字部件（测试用）。</summary>
    internal Text NamePart => name;

    /// <summary>箭头部件（测试用）。</summary>
    internal Icon IndicatorPart => indicator;

    /// <summary>显示箭头：不 compact 且没关掉。</summary>
    internal bool IndicatorShown => !Compact && ShowIndicator;

    /// <inheritdoc />
    protected override ButtonVariant DefaultVariant => ButtonVariant.Ghost;

    /// <inheritdoc />
    protected override StatusTone DefaultTone => StatusTone.Neutral;

    /// <inheritdoc />
    protected override Radius DefaultRadius => IsRow ? Radius.Md : Radius.Full;

    void RebuildParts()
    {
        name = Rebuilt(name, new TextProps { Weight = TextWeight.Medium }.Merge(PropsName));
        description = Rebuilt(description, new TextProps { Tone = TextTone.Muted }.Merge(PropsDescription));
        RebuildIndicator();

        static Text Rebuilt(Text old, TextProps props)
        {
            var text = new Text { Content = old.Content, Truncate = true };
            props.ApplyTo(text);
            return text;
        }
    }

    void RebuildIndicator()
    {
        new IconProps { Data = Indicator ?? (IsRow ? Lucide.ChevronsUpDown : Lucide.ChevronDown), Tone = TextTone.Muted }.Merge(PropsIndicator).ApplyTo(indicator);
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>上下与起始边 space.xs、结束边 space.sm；整行左右 space.sm；compact 四周 space.xs。外观覆盖写了左右的两边都用它。</summary>
    private protected override (float Start, float End, float Block) Padding(float em)
    {
        var (xs, sm) = (Tokens.Space.Xs * em, Tokens.Space.Sm * em);
        var sx = ResolvedSx;
        var (start, end) = sx?.PaddingInline is { } inline ? (inline * em, inline * em) : Compact ? (xs, xs) : IsRow ? (sm, sm) : (xs, sm);
        return (start, end, (sx?.PaddingBlock ?? Tokens.Space.Xs) * em);
    }

    /// <summary>
    /// 名字、说明的字号是外面的 0.93、0.86 倍（相对字号的 sm、xs 档；部件属性写了字号时不管）。改字号会让宿主重排，只在变了时写。
    /// </summary>
    void SizeTexts(ElementContext inner)
    {
        var dip = inner.Em * 96 / inner.Dpi;
        if (PropsName?.Size is null && name.Size != (TextSize)(0.93f * dip)) name.Size = 0.93f * dip;
        if (PropsDescription?.Size is null && description.Size != (TextSize)(0.86f * dip)) description.Size = 0.86f * dip;
    }

    /// <summary>各部件的大小：头像、名字与说明（不限宽）、箭头（不显示时为空）与间距 space.sm。</summary>
    (Size Avatar, Size Name, Size Description, Size Indicator, float Gap) Metrics(ElementContext inner, int textWidth)
    {
        SizeTexts(inner);
        var limit = new Size(textWidth, int.MaxValue);
        return (avatar.Measure(inner, new Size(int.MaxValue, int.MaxValue)),
            Compact ? default : name.Measure(inner, limit),
            Compact || !IsDescribed ? default : description.Measure(inner, limit),
            IndicatorShown ? indicator.Measure(inner, new Size(int.MaxValue, int.MaxValue)) : default,
            Tokens.Space.Sm * inner.Em);
    }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var size = base.Measure(context, available);
        return IsRow && available.Width < int.MaxValue ? size with { Width = Math.Max(size.Width, available.Width) } : size;
    }

    /// <inheritdoc />
    protected override SizeF MeasureContent(ElementContext inner)
    {
        var m = Metrics(inner, int.MaxValue);
        var text = new SizeF(Math.Max(m.Name.Width, m.Description.Width), m.Name.Height + m.Description.Height);
        var width = m.Avatar.Width + (Compact ? 0 : m.Gap + text.Width) + (IndicatorShown ? m.Gap + m.Indicator.Width : 0);
        return new SizeF(width, Math.Max(Math.Max(m.Avatar.Height, text.Height), m.Indicator.Height));
    }

    /// <summary>头像、名字与说明、箭头从起始边排开，各自垂直居中；整行时文字撑开、箭头贴着结束边，名字、说明放不下时省略。</summary>
    protected override void PaintContent(Graphics graphics, ElementContext inner, Rectangle bounds)
    {
        var fixedWidth = Metrics(inner, int.MaxValue) is var all ? all.Avatar.Width + (IndicatorShown ? all.Gap + all.Indicator.Width : 0) + (Compact ? 0 : all.Gap) : 0;
        var m = Metrics(inner, Math.Max(0, bounds.Width - (int)MathF.Ceiling(fixedWidth)));
        var rtl = inner.RightToLeft;
        var x = (float)bounds.X;
        void Place(Element part, Size size, int y)
        {
            var left = rtl ? bounds.Right - (x - bounds.X) - size.Width : x;
            part.Bounds = new Rectangle((int)MathF.Round(left), y, size.Width, size.Height);
            part.Paint(graphics, inner);
            x += size.Width + m.Gap;
        }
        int Middle(int height) => bounds.Y + (bounds.Height - height) / 2;

        Place(avatar, m.Avatar, Middle(m.Avatar.Height));
        if (!Compact)
        {
            var top = Middle(m.Name.Height + m.Description.Height);
            var start = x;
            Place(name, m.Name, top);
            if (IsDescribed)
            {
                x = start;
                Place(description, m.Description, top + m.Name.Height);
            }
            x = start + Math.Max(m.Name.Width, m.Description.Width) + m.Gap;
        }
        if (!IndicatorShown) return;
        if (IsRow) x = bounds.Right - m.Indicator.Width;
        Place(indicator, m.Indicator, Middle(m.Indicator.Height));
    }
}

/// <summary>
/// 放进窗体、设计器里用的 <see cref="UserButton"/>：名字是读屏的名称，说明是读屏的说明；有说明且不 compact 时占满宿主给的宽度（设 Dock、Anchor）。
/// 账号菜单：Click 里打开自己的菜单（组件库的 Menu 还没有）。输入、焦点见 <see cref="ButtonControlBase{T}"/>。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[DefaultProperty(nameof(DisplayName))]
[Description("用户按钮：头像 + 名字（可带一行说明）+ 展开箭头，账号菜单的触发器。")]
public class UserButtonControl : ButtonControlBase<UserButton>
{
    /// <summary>名字。</summary>
    [Category("Appearance")]
    [Description("名字：显示在头像旁边，是按钮的名称。")]
    [DefaultValue(null)]
    public string? DisplayName { get => Element.DisplayName; set => Element.DisplayName = value; }

    /// <summary>说明。</summary>
    [Category("Appearance")]
    [Description("名字下面的一行说明：部门、角色、邮箱；写了时整行显示。")]
    [DefaultValue(null)]
    public string? Description { get => Element.Description; set => Element.Description = value; }

    /// <summary>头像图片。</summary>
    [Category("Appearance")]
    [Description("头像图片；没有时显示名字的首字母。")]
    [DefaultValue(null)]
    public Image? Source { get => Element.Source; set => Element.Source = value; }

    /// <summary>只显示头像。</summary>
    [Category("Appearance")]
    [Description("只显示头像（顶栏空间紧、侧栏收起时）。")]
    [DefaultValue(false)]
    public bool Compact { get => Element.Compact; set => Element.Compact = value; }

    /// <summary>结束边的箭头。</summary>
    [Category("Appearance")]
    [Description("结束边的箭头：Lucide 的图标名；空着时 ChevronDown，有说明时 ChevronsUpDown。")]
    [DefaultValue(null)]
    public IconData? Indicator { get => Element.Indicator; set => Element.Indicator = value; }

    /// <summary>显示结束边的箭头。</summary>
    [Category("Appearance")]
    [Description("显示结束边的箭头。")]
    [DefaultValue(true)]
    public bool ShowIndicator { get => Element.ShowIndicator; set => Element.ShowIndicator = value; }

    /// <summary>名字部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsName { get => Element.PropsName; set => Element.PropsName = value; }

    /// <summary>说明部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsDescription { get => Element.PropsDescription; set => Element.PropsDescription = value; }

    /// <summary>箭头部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsIndicator { get => Element.PropsIndicator; set => Element.PropsIndicator = value; }

    /// <inheritdoc />
    protected override string? DefaultAccessibleName => DisplayName;

    /// <inheritdoc />
    protected override string? DefaultAccessibleDescription => Compact ? null : Description;
}
