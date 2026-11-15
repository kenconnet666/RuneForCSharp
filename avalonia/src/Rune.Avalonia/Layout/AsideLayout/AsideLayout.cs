using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Metadata;

namespace Rune.Avalonia;

/// <summary>
/// 主栏 + 侧栏（vue-tsx-pro 的 AsideLayout）：详情页常用的两栏，主栏放长内容（概览、明细、流程），侧栏放要一直看得见的（金额、状态、清单）。
/// <code>
/// &lt;rune:AsideLayout AsideLabel="收益与关闭"&gt;
///   &lt;rune:AsideLayout.Aside&gt;&lt;local:BenefitCard /&gt;&lt;local:ClosingCard /&gt;&lt;/rune:AsideLayout.Aside&gt;
///   &lt;local:OverviewCard /&gt;
///   &lt;local:PartsCard /&gt;
/// &lt;/rune:AsideLayout&gt;
/// </code>
/// 宽的时候侧栏定宽（<see cref="AsideWidth"/>，默认 26rem）、贴在滚动区域顶部（Sticky，离顶 md），滚动主栏时它不动；
/// 自己不到 56rem 宽时叠成一列，侧栏在后。按自己的宽度而不是窗口宽度决定叠不叠：放在抽屉、分栏里也对。两栏里的块按 Gap 竖排。
/// 部件：<see cref="AsideLayoutParts"/>。
/// </summary>
public class AsideLayout : Control
{
    /// <summary>侧栏的名称（读屏）。</summary>
    public static readonly StyledProperty<string?> AsideLabelProperty = AvaloniaProperty.Register<AsideLayout, string?>(nameof(AsideLabel));

    /// <summary>侧栏的宽度，默认 26rem。</summary>
    public static readonly StyledProperty<Length> AsideWidthProperty = AvaloniaProperty.Register<AsideLayout, Length>(nameof(AsideWidth), new Length("26rem"));

    /// <summary>侧栏贴在滚动区域顶部，默认 true。</summary>
    public static readonly StyledProperty<bool> StickyProperty = AvaloniaProperty.Register<AsideLayout, bool>(nameof(Sticky), true);

    /// <summary>两栏之间、栏里各块之间的间距，默认 md。</summary>
    public static readonly StyledProperty<Space> GapProperty = AvaloniaProperty.Register<AsideLayout, Space>(nameof(Gap), Space.Md);

    /// <summary>宽于这个时两栏并排（上游的容器查询 56rem）。</summary>
    static readonly Length Breakpoint = new("56rem");

    readonly Flex main = new() { Name = AsideLayoutParts.Main, Direction = FlexDirection.Column, Align = FlexAlign.Stretch };
    readonly Flex aside = new() { Name = AsideLayoutParts.Aside, Direction = FlexDirection.Column, Align = FlexAlign.Stretch };
    // 侧栏的外层：贴住时是 Affix（只在父元素即这个布局的范围里贴住，同 CSS 的 position: sticky），否则是不动的 Decorator
    Control holder;
    bool wide;

    static AsideLayout() => AffectsMeasure<AsideLayout>(AsideWidthProperty, GapProperty, global::Avalonia.Controls.Documents.TextElement.FontSizeProperty);

    /// <summary>新建：主栏与侧栏都是竖排。</summary>
    public AsideLayout()
    {
        holder = new Decorator { Child = aside };
        LogicalChildren.AddRange([main, holder]);
        VisualChildren.AddRange([main, holder]);
        Sync();
    }

    /// <summary>主栏的内容（一块一块竖排）。</summary>
    [Content]
    public global::Avalonia.Controls.Controls Children => main.Children;

    /// <summary>侧栏的内容（一块一块竖排）。</summary>
    public global::Avalonia.Controls.Controls Aside => aside.Children;

    /// <summary>侧栏的名称。</summary>
    public string? AsideLabel { get => GetValue(AsideLabelProperty); set => SetValue(AsideLabelProperty, value); }

    /// <summary>侧栏的宽度。</summary>
    public Length AsideWidth { get => GetValue(AsideWidthProperty); set => SetValue(AsideWidthProperty, value); }

    /// <summary>侧栏贴在滚动区域顶部。</summary>
    public bool Sticky { get => GetValue(StickyProperty); set => SetValue(StickyProperty, value); }

    /// <summary>间距。</summary>
    public Space Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>现在两栏并排（测试用）。</summary>
    internal bool Wide => wide;

    double Em => global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);

    double GapPixels => Gap.ResourceKey is { } key ? this.Resource<double>(key) * Em : 0;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AsideLabelProperty || change.Property == StickyProperty || change.Property == GapProperty) Sync();
    }

    void Sync()
    {
        main.Gap = aside.Gap = Gap;
        AutomationProperties.SetName(aside, AsideLabel);
        // 叠成一列时不贴：侧栏在最后，上游的容器查询只在并排时开 sticky
        var stick = Sticky && wide;
        if (stick == holder is Affix) return;
        if (holder is Affix affix) affix.Content = null;
        else ((Decorator)holder).Child = null;
        LogicalChildren.Remove(holder);
        VisualChildren.Remove(holder);
        holder = stick ? new Affix { Shadow = false, OffsetTop = this.Resource<double>(ThemeKeys.Space.Md) * Em, Content = aside } : new Decorator { Child = aside };
        LogicalChildren.Add(holder);
        VisualChildren.Add(holder);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var gap = GapPixels;
        var next = !double.IsInfinity(availableSize.Width) && availableSize.Width >= Breakpoint.Pixels(Em);
        if (next != wide)
        {
            wide = next;
            Sync();
        }
        if (!wide)
        {
            main.Measure(availableSize.WithHeight(double.PositiveInfinity));
            holder.Measure(availableSize.WithHeight(double.PositiveInfinity));
            var width = Math.Max(main.DesiredSize.Width, holder.DesiredSize.Width);
            return new Size(width, main.DesiredSize.Height + gap + holder.DesiredSize.Height);
        }
        var side = Math.Min(AsideWidth.Pixels(Em), availableSize.Width);
        var rest = Math.Max(0, availableSize.Width - side - gap);
        main.Measure(new Size(rest, double.PositiveInfinity));
        holder.Measure(new Size(side, double.PositiveInfinity));
        return new Size(availableSize.Width, Math.Max(main.DesiredSize.Height, holder.DesiredSize.Height));
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var gap = GapPixels;
        if (!wide)
        {
            main.Arrange(new Rect(0, 0, finalSize.Width, main.DesiredSize.Height));
            holder.Arrange(new Rect(0, main.DesiredSize.Height + gap, finalSize.Width, holder.DesiredSize.Height));
            return finalSize;
        }
        var side = Math.Min(AsideWidth.Pixels(Em), finalSize.Width);
        var rest = Math.Max(0, finalSize.Width - side - gap);
        // 两栏顶端对齐（align-items: start）；从右往左时平台整体镜像，侧栏到左边
        main.Arrange(new Rect(0, 0, rest, main.DesiredSize.Height));
        holder.Arrange(new Rect(rest + gap, 0, side, holder.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>AsideLayout 的部件名（TS 的部件）。外层（TS 的 root）是布局自己。</summary>
public static class AsideLayoutParts
{
    /// <summary>主栏（TS 的 propsMain）：Flex，竖排、隔 Gap。</summary>
    public const string Main = "PART_Main";

    /// <summary>侧栏（TS 的 propsAside）：Flex，竖排、隔 Gap，读屏名称是 AsideLabel。</summary>
    public const string Aside = "PART_Aside";
}
