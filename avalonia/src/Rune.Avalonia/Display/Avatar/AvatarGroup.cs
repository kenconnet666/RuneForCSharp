using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 头像组（vue-tsx-pro 的 AvatarGroup）：一排头像叠放，超过 <see cref="Max"/> 的折叠成"+N"。协作者、参与人、点赞的人。
/// <code>
/// &lt;rune:AvatarGroup Max="3" AutomationProperties.Name="协作者"&gt;
///   &lt;rune:Avatar DisplayName="张三" /&gt; &lt;rune:Avatar DisplayName="李四" /&gt; …
/// &lt;/rune:AvatarGroup&gt;
/// </code>
/// 头像的大小随组的 <see cref="Size"/>（或继承的字号）；组里的头像往前叠 0.5em，各带一圈页面底色的描边（borderWidths.sm）把相邻的分开，后面的盖在前面的上面。
/// "+N"和头像一样大、一样叠放（圆形、fill 底、正文色、xs / md 的字号、中等字重），读屏念"还有 N 人"（语言包的 AvatarMore）；
/// <see cref="Total"/> 给了时按它算 N（列表只拿到了一部分时）。读屏里是一组，名称写在 AutomationProperties.Name 上。
/// 部件：<c>rune|AvatarGroup Border#PART_More</c>、<c>rune|Text#PART_MoreText</c>。
/// </summary>
public class AvatarGroup : Control
{
    /// <summary>最多显示几个头像，其余折叠成"+N"；不写时全部显示。</summary>
    public static readonly StyledProperty<int?> MaxProperty = AvaloniaProperty.Register<AvatarGroup, int?>(nameof(Max));

    /// <summary>总人数：列表只拿到前几个时给出，"+N"按它算。</summary>
    public static readonly StyledProperty<int?> TotalProperty = AvaloniaProperty.Register<AvatarGroup, int?>(nameof(Total));

    /// <summary>字号档位，头像边长是它的 2.5 倍；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<AvatarGroup, TextSize>(nameof(Size));

    /// <summary>字号（继承），头像与"+N"的边长按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<AvatarGroup>();

    readonly Stack stack = new();
    readonly MoreBorder more = new() { Name = AvatarGroupParts.More };
    readonly Text moreText = new() { Name = AvatarGroupParts.MoreText };
    readonly TemplateResources resources;

    static AvatarGroup() => AffectsMeasure<AvatarGroup>(FontSizeProperty);

    /// <summary>新建：没有头像。</summary>
    public AvatarGroup()
    {
        resources = new TemplateResources(this);
        more.Child = moreText;
        AutomationProperties.SetAccessibilityView(moreText, AccessibilityView.Raw);
        Children.CollectionChanged += (_, _) => Sync();
        UpdateMetrics();
        LogicalChildren.Add(stack);
        VisualChildren.Add(stack);
    }

    /// <summary>头像（Avatar）。</summary>
    [Content]
    public AvaloniaList<Control> Children { get; } = [];

    /// <summary>最多显示几个头像。</summary>
    public int? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>总人数。</summary>
    public int? Total { get => GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>"+N"（测试用）；没有折叠时不在组里。</summary>
    internal Border More => more;

    /// <summary>显示出来的（头像与"+N"，测试用）。</summary>
    internal IReadOnlyList<Control> Shown => [.. stack.Children.Cast<Ring>().Select(ring => ring.Child!)];

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == FontSizeProperty) UpdateMetrics();
        else if (change.Property == MaxProperty || change.Property == TotalProperty || change.Property == Provider.LangProperty) Sync();
    }

    /// <summary>显示前 Max 个头像，其余（与 Total 里没拿到的）折叠成"+N"。</summary>
    void Sync()
    {
        var shown = Max is { } max && Children.Count > max ? Children.Take(Math.Max(0, max)).ToList() : [.. Children];
        var hidden = (Total ?? Children.Count) - shown.Count;
        if (hidden > 0)
        {
            moreText.Text = $"+{hidden}";
            AutomationProperties.SetName(more, Provider.GetLang(this).AvatarMore(hidden));
            shown.Add(more);
        }
        // 先把旧的外圈拆开：同一个头像不能同时挂在两个父元素下
        foreach (var ring in stack.Children.Cast<Ring>()) ring.Child = null;
        stack.Children.Clear();
        stack.Children.AddRange(shown.Select(child => new Ring { Child = child }));
    }

    /// <summary>"+N"同组里的头像：边长 2.5em、圆形；文字小一档（xs / md）。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        more.Width = more.Height = 2.5 * em;
        more.CornerRadius = new CornerRadius(1.25 * em);
        moreText.FontSize = this.Resource<double>(ThemeKeys.FontSize.Xs) / this.Resource<double>(ThemeKeys.FontSize.Md) * em;
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        stack.Measure(availableSize);
        return stack.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        stack.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AvatarGroupAutomationPeer(this);

    /// <summary>
    /// 组里每一项的外圈：描边（TS 的 box-shadow: 0 0 0 borderWidths.sm background）画在项外面一圈，不占位置；
    /// 后面的项盖在前面的上面，描边就把相邻的分开。
    /// </summary>
    sealed class Ring : Decorator
    {
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var width = this.Resource<double>(ThemeKeys.BorderWidth.Sm);
            if (this.Resource<IBrush?>(ThemeKeys.Brush.Background) is not { } brush || width <= 0) return;
            var radius = Child switch { Avatar avatar => avatar.CornerRadius, Border border => border.CornerRadius.TopLeft, _ => 0 };
            context.DrawRectangle(null, new Pen(brush, width), new RoundedRect(new Rect(Bounds.Size).Inflate(width / 2), radius + width / 2));
        }
    }

    /// <summary>"+N"：样式按 Border 匹配（部件选择器写 Border#PART_More），读屏里是一张图片，名称是"还有 N 人"。</summary>
    sealed class MoreBorder : Border
    {
        protected override Type StyleKeyOverride => typeof(Border);

        protected override AutomationPeer OnCreateAutomationPeer() => new MorePeer(this);

        sealed class MorePeer(MoreBorder owner) : ControlAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

            protected override bool IsControlElementCore() => true;

            protected override bool IsContentElementCore() => true;

            protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
        }
    }

    /// <summary>读屏：一组，名称是 AutomationProperties.Name；子元素直接是头像与"+N"（中间的排版层与外圈不出现）。</summary>
    sealed class AvatarGroupAutomationPeer(AvatarGroup owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => [.. owner.Shown.Select(CreatePeerForElement)];
    }

    /// <summary>
    /// 叠放的一排：后一个往前叠 0.5em（按它自己的字号），竖直居中。"+N"同头像：边长 2.5em、圆形、带描边。
    /// </summary>
    sealed class Stack : Panel
    {
        // 往前叠的 0.5em 按项自己的字号（头像可以写自己的 Size）
        static double Em(Control ring) => ((Decorator)ring).Child?.GetValue(TextElement.FontSizeProperty) ?? 0;

        protected override LayoutSize MeasureOverride(LayoutSize availableSize)
        {
            var (width, height) = (0.0, 0.0);
            for (var index = 0; index < Children.Count; index++)
            {
                var child = Children[index];
                child.Measure(LayoutSize.Infinity);
                width += child.DesiredSize.Width - (index > 0 ? 0.5 * Em(child) : 0);
                height = Math.Max(height, child.DesiredSize.Height);
            }
            return new LayoutSize(Math.Max(0, width), height);
        }

        protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
        {
            var x = 0.0;
            for (var index = 0; index < Children.Count; index++)
            {
                var child = Children[index];
                if (index > 0) x -= 0.5 * Em(child);
                child.Arrange(new Rect(x, (finalSize.Height - child.DesiredSize.Height) / 2, child.DesiredSize.Width, child.DesiredSize.Height));
                x += child.DesiredSize.Width;
            }
            return finalSize;
        }
    }
}

/// <summary>AvatarGroup 的部件名。</summary>
public static class AvatarGroupParts
{
    /// <summary>"+N"（TS 的 propsMore）。</summary>
    public const string More = "PART_More";

    /// <summary>"+N"里的文字（TS 的 propsMoreText），对读屏隐藏。</summary>
    public const string MoreText = "PART_MoreText";
}
