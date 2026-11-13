using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

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
/// 部件样式键：<see cref="AvatarGroupParts"/>。
/// </summary>
[ContentProperty(nameof(Children))]
public class AvatarGroup : Control
{
    /// <summary>最多显示几个头像，其余折叠成"+N"；不写时全部显示。</summary>
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(
        nameof(Max), typeof(int?), typeof(AvatarGroup), new FrameworkPropertyMetadata(null, (d, _) => ((AvatarGroup)d).Sync()));

    /// <summary>总人数：列表只拿到前几个时给出，"+N"按它算。</summary>
    public static readonly DependencyProperty TotalProperty = DependencyProperty.Register(
        nameof(Total), typeof(int?), typeof(AvatarGroup), new FrameworkPropertyMetadata(null, (d, _) => ((AvatarGroup)d).Sync()));

    /// <summary>字号档位，头像边长是它的 2.5 倍；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(AvatarGroup), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    readonly Stack stack = new();
    readonly MoreBorder more = new() { Name = "PART_More" };
    readonly Text moreText = new() { Name = "PART_MoreText" };

    static AvatarGroup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AvatarGroup), new FrameworkPropertyMetadata(typeof(AvatarGroup)));
        FocusableProperty.OverrideMetadata(typeof(AvatarGroup), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(AvatarGroup), new FrameworkPropertyMetadata(false));
        FontSizeProperty.OverrideMetadata(typeof(AvatarGroup), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((AvatarGroup)d).UpdateMetrics()));
    }

    /// <summary>新建：没有头像。</summary>
    public AvatarGroup()
    {
        more.SetResourceReference(StyleProperty, AvatarGroupParts.More);
        moreText.SetResourceReference(StyleProperty, AvatarGroupParts.MoreText);
        more.Child = moreText;
        Children.CollectionChanged += (_, _) => Sync();
        AddVisualChild(stack);
        AddLogicalChild(stack);
        UpdateMetrics();
    }

    /// <summary>头像（Avatar）。</summary>
    public ObservableCollection<UIElement> Children { get; } = [];

    /// <summary>最多显示几个头像。</summary>
    public int? Max { get => (int?)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>总人数。</summary>
    public int? Total { get => (int?)GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>"+N"（测试用）；没有折叠时不在组里。</summary>
    internal Border More => more;

    /// <summary>显示出来的（头像与"+N"，测试用）。</summary>
    internal IReadOnlyList<UIElement> Shown => [.. stack.Children.Cast<Ring>().Select(ring => ring.Child)];

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => stack;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { stack }.GetEnumerator();

    /// <summary>档位的字号在主题样式的触发器里；写成像素数时是本地值。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TextSize)e.NewValue).Pixels is { } pixels) d.SetValue(FontSizeProperty, pixels);
        else if (((TextSize)e.OldValue).Pixels is not null) d.ClearValue(FontSizeProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == Provider.LangProperty) Sync();
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
        foreach (var child in shown) stack.Children.Add(new Ring { Child = child });
    }

    /// <summary>"+N"同组里的头像：边长 2.5em、圆形；文字小一档（xs / md）。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        more.Width = more.Height = 2.5 * em;
        more.CornerRadius = new CornerRadius(1.25 * em);
        moreText.FontSize = Resource("Rune.FontSize.Xs", 12.0) / Resource("Rune.FontSize.Md", 14.0) * em;
    }

    double Resource(string key, double fallback) => TryFindResource(key) is double value ? value : fallback;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        stack.Measure(constraint);
        return stack.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        stack.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AvatarGroupAutomationPeer(this);

    /// <summary>读屏：一组，名称是 AutomationProperties.Name；子元素直接是头像与"+N"（中间的排版层与外圈不出现）。</summary>
    sealed class AvatarGroupAutomationPeer(AvatarGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(AvatarGroup);

        protected override List<AutomationPeer>? GetChildrenCore() => [.. owner.Shown.Select(UIElementAutomationPeer.CreatePeerForElement).OfType<AutomationPeer>()];
    }

    /// <summary>
    /// 组里每一项的外圈：描边（TS 的 box-shadow: 0 0 0 borderWidths.sm background）画在项外面一圈，不占位置；
    /// 后面的项盖在前面的上面，描边就把相邻的分开。
    /// </summary>
    sealed class Ring : Decorator
    {
        protected override void OnRender(DrawingContext drawingContext)
        {
            var width = TryFindResource("Rune.BorderWidth.Sm") is double value ? value : 2;
            if (TryFindResource("Rune.Brush.Background") is not Brush brush || width <= 0) return;
            var radius = Child switch { Avatar avatar => avatar.CornerRadius, Border border => border.CornerRadius.TopLeft, _ => 0 };
            var rect = new Rect(RenderSize);
            rect.Inflate(width / 2, width / 2);
            drawingContext.DrawRoundedRectangle(null, new Pen(brush, width), rect, radius + width / 2, radius + width / 2);
        }
    }

    /// <summary>"+N"：读屏里是一张图片，名称是"还有 N 人"，里面的文字不另念。</summary>
    sealed class MoreBorder : Border
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new MorePeer(this);

        sealed class MorePeer(MoreBorder owner) : FrameworkElementAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

            protected override string GetClassNameCore() => "AvatarGroupMore";

            protected override List<AutomationPeer>? GetChildrenCore() => null;
        }
    }

    /// <summary>叠放的一排：后一个往前叠 0.5em（按项自己的字号，头像可以写自己的 Size），竖直居中。</summary>
    sealed class Stack : Panel
    {
        static double Em(UIElement ring) => ((Decorator)ring).Child is Control child ? child.FontSize : 0;

        protected override Size MeasureOverride(Size availableSize)
        {
            var (width, height) = (0.0, 0.0);
            for (var index = 0; index < InternalChildren.Count; index++)
            {
                var child = InternalChildren[index];
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width += child.DesiredSize.Width - (index > 0 ? 0.5 * Em(child) : 0);
                height = Math.Max(height, child.DesiredSize.Height);
            }
            return new Size(Math.Max(0, width), height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var x = 0.0;
            for (var index = 0; index < InternalChildren.Count; index++)
            {
                var child = InternalChildren[index];
                if (index > 0) x -= 0.5 * Em(child);
                child.Arrange(new Rect(x, (finalSize.Height - child.DesiredSize.Height) / 2, child.DesiredSize.Width, child.DesiredSize.Height));
                x += child.DesiredSize.Width;
            }
            return finalSize;
        }
    }
}

/// <summary>AvatarGroup 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:AvatarGroupParts.More}" TargetType="Border"&gt;</c>。</summary>
public static class AvatarGroupParts
{
    /// <summary>"+N"（TS 的 propsMore）。</summary>
    public static readonly PartKey More = new(typeof(AvatarGroup), nameof(More), typeof(Border));

    /// <summary>"+N"里的文字（TS 的 propsMoreText），对读屏隐藏。</summary>
    public static readonly PartKey MoreText = new(typeof(AvatarGroup), nameof(MoreText), typeof(Text));
}
