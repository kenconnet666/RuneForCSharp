using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Rune.Wpf;

/// <summary>
/// 吸底的操作栏（vue-tsx-pro 的 ActionBar）：长表单、详情页底部的"取消 / 保存草稿 / 提交"，滚动时一直贴在滚动区域的底边，不用滚到底才能提交。
/// <code>
/// &lt;rune:ScrollArea&gt;
///   &lt;StackPanel&gt;
///     …长表单…
///     &lt;rune:ActionBar Start="有 3 处改动还没保存"&gt;
///       &lt;rune:Button Variant="ghost" Tone="neutral"&gt;取消&lt;/rune:Button&gt;
///       &lt;rune:Button&gt;提交&lt;/rune:Button&gt;
///     &lt;/rune:ActionBar&gt;
///   &lt;/StackPanel&gt;
/// &lt;/rune:ScrollArea&gt;
/// </code>
/// 放在要跟着滚动的内容的最后：自己还没滚进视口时往上挪，贴着最近的滚动区域的底边（网页的 position: sticky，只在所在的父元素里挪）；
/// 这时下面还有被盖住的内容，带 md 阴影、描边变成 border（<see cref="IsStuck"/>），提示还能往下滚；滚到底时与内容平齐。
/// <see cref="Start"/> 是起始边的说明（未保存的改动、已选几项，次要色、小一号），按钮靠结束边排开、放不下时换行。
/// <see cref="Sticky"/> 为 false 时是普通的一行（对话框里已经固定的底栏、列表下面的批量操作）。
/// 外观：surface 底、上边 xs 的 borderMuted 描边，上下 space.md、左右 space.lg。部件名在 <see cref="ActionBarParts"/>。
/// </summary>
[ContentProperty(nameof(Children))]
public class ActionBar : FrameworkElement
{
    /// <summary>起始边的内容：未保存的改动、已选几项、校验汇总。</summary>
    public static readonly DependencyProperty StartProperty = DependencyProperty.Register(
        nameof(Start), typeof(object), typeof(ActionBar), new PropertyMetadata(null, (d, _) => ((ActionBar)d).UpdateStart()));

    /// <summary>贴在滚动区域的底边，默认 true。</summary>
    public static readonly DependencyProperty StickyProperty = DependencyProperty.Register(
        nameof(Sticky), typeof(bool), typeof(ActionBar), new PropertyMetadata(true, (d, _) => ((ActionBar)d).Stick()));

    static readonly DependencyPropertyKey IsStuckKey = DependencyProperty.RegisterReadOnly(
        nameof(IsStuck), typeof(bool), typeof(ActionBar), new PropertyMetadata(false, (d, _) => ((ActionBar)d).UpdateStuck()));

    /// <summary>贴着底边、盖着下面的内容（只读）。</summary>
    public static readonly DependencyProperty IsStuckProperty = IsStuckKey.DependencyProperty;

    readonly Grid host = new();
    readonly Border shadow = new();
    readonly Border root = new() { Name = ActionBarParts.Root };
    readonly Flex row = new() { Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, Gap = Space.Md };
    readonly ContentPresenter start = new() { Name = ActionBarParts.Start, Visibility = Visibility.Collapsed };
    readonly Flex actions = new() { Name = ActionBarParts.Actions, Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, Gap = Space.Sm, HorizontalAlignment = HorizontalAlignment.Right };
    readonly TranslateTransform shift = new();
    ScrollViewer? scroller;

    static ActionBar() => TextElement.FontSizeProperty.OverrideMetadata(typeof(ActionBar), new FrameworkPropertyMetadata(
        SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((ActionBar)d).UpdateMetrics()));

    /// <summary>新建：起始边的说明占满剩下的宽度，按钮靠结束边。</summary>
    public ActionBar()
    {
        Flex.SetGrow(start, true);
        row.Children.Add(start);
        row.Children.Add(actions);
        root.Child = row;
        // 阴影画在单独的一层上：DropShadowEffect 套在栏上会连文字一起投影
        host.Children.Add(shadow);
        host.Children.Add(root);
        root.SetResourceReference(Border.BackgroundProperty, "Rune.Brush.Surface");
        shadow.SetResourceReference(Border.BackgroundProperty, "Rune.Brush.Surface");
        root.SetResourceReference(Border.BorderBrushProperty, "Rune.Brush.BorderMuted");
        start.SetResourceReference(TextElement.ForegroundProperty, "Rune.Brush.TextMuted");
        RenderTransform = shift;
        // 挪上去时盖在前面的内容上面
        Panel.SetZIndex(this, 1);
        AddLogicalChild(host);
        AddVisualChild(host);
        Loaded += (_, _) =>
        {
            UpdateMetrics();
            scroller = FindScroller();
            if (scroller is not null)
            {
                scroller.ScrollChanged += OnScrolled;
                scroller.LayoutUpdated += OnScrolled;
            }
            Stick();
        };
        Unloaded += (_, _) =>
        {
            if (scroller is not null)
            {
                scroller.ScrollChanged -= OnScrolled;
                scroller.LayoutUpdated -= OnScrolled;
            }
            scroller = null;
        };
    }

    /// <summary>起始边的内容。</summary>
    public object? Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <summary>贴在滚动区域的底边。</summary>
    public bool Sticky { get => (bool)GetValue(StickyProperty); set => SetValue(StickyProperty, value); }

    /// <summary>贴着底边、盖着下面的内容。</summary>
    public bool IsStuck => (bool)GetValue(IsStuckProperty);

    /// <summary>按钮（靠结束边排开）。</summary>
    public UIElementCollection Children => actions.Children;

    /// <summary>往上挪了多少（测试用）。</summary>
    internal double Lift => -shift.Y;

    /// <summary>起始边的说明（测试用）。</summary>
    internal FrameworkElement StartPart => start;

    void UpdateStart()
    {
        start.Content = Start;
        start.Visibility = Start is null or "" ? Visibility.Collapsed : Visibility.Visible;
    }

    void UpdateStuck()
    {
        root.SetResourceReference(Border.BorderBrushProperty, IsStuck ? "Rune.Brush.Border" : "Rune.Brush.BorderMuted");
        if (IsStuck) shadow.SetResourceReference(EffectProperty, "Rune.Shadow.Md");
        else shadow.ClearValue(EffectProperty);
    }

    /// <summary>按 em 的尺寸：上下 space.md、左右 space.lg，说明小一号（0.93em）。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        var (md, lg) = (this.Resource<double>(ThemeKeys.Space.Md) * em, this.Resource<double>(ThemeKeys.Space.Lg) * em);
        root.Padding = new Thickness(lg, md, lg, md);
        root.BorderThickness = new Thickness(0, Math.Max(1, this.Resource<double>(ThemeKeys.BorderWidth.Xs)), 0, 0);
        start.SetValue(TextElement.FontSizeProperty, 0.93 * em);
    }

    ScrollViewer? FindScroller()
    {
        for (var parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is ScrollViewer found) return found;
        return null;
    }

    void OnScrolled(object? sender, EventArgs e) => Stick();

    /// <summary>
    /// 网页的 position: sticky（bottom）：原来的位置在滚动区域的底边下面时往上挪到贴着底边，最多挪到父元素的顶上；
    /// 挪了就是盖着下面的内容。
    /// </summary>
    void Stick()
    {
        var lift = 0d;
        if (Sticky && scroller is not null && IsLoaded && scroller.IsAncestorOf(this) && VisualTreeHelper.GetParent(this) is UIElement parent)
        {
            // 变换带着现在的平移：减掉它得到原来的位置
            var bottom = TransformToAncestor(scroller).Transform(new Point(0, ActualHeight)).Y - shift.Y;
            var top = TranslatePoint(default, parent).Y - shift.Y;
            lift = Math.Clamp(bottom - scroller.ViewportHeight, 0, Math.Max(0, top));
        }
        if (Math.Abs(shift.Y + lift) > 0.01) shift.Y = -lift;
        SetValue(IsStuckKey, lift > 0.5);
    }

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { host }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => host;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        host.Measure(availableSize);
        return host.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        host.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>ActionBar 里子元素的名字（代码组合的子元素）。</summary>
public static class ActionBarParts
{
    /// <summary>外层（TS 的 Root）：Border，底色、上边描边、内边距。</summary>
    public const string Root = "PART_Root";

    /// <summary>起始边的说明（TS 的 propsStart）：ContentPresenter。</summary>
    public const string Start = "PART_Start";

    /// <summary>按钮区（TS 的 propsActions）：Flex。</summary>
    public const string Actions = "PART_Actions";
}
