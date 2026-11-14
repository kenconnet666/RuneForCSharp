using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

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
/// 这时下面还有被盖住的内容，带 md 阴影、描边变成 border（<c>:stuck</c>），提示还能往下滚；滚到底时与内容平齐。
/// <see cref="Start"/> 是起始边的说明（未保存的改动、已选几项，次要色、小一号），按钮靠结束边排开、放不下时换行。
/// <see cref="Sticky"/> 为 false 时是普通的一行（对话框里已经固定的底栏、列表下面的批量操作）。
/// 外观：surface 底、上边 xs 的 borderMuted 描边，上下 space.md、左右 space.lg。部件名在 <see cref="ActionBarParts"/>。
/// </summary>
[PseudoClasses(":stuck")]
public class ActionBar : Control
{
    /// <summary>起始边的内容：未保存的改动、已选几项、校验汇总。</summary>
    public static readonly StyledProperty<object?> StartProperty = AvaloniaProperty.Register<ActionBar, object?>(nameof(Start));

    /// <summary>贴在滚动区域的底边，默认 true。</summary>
    public static readonly StyledProperty<bool> StickyProperty = AvaloniaProperty.Register<ActionBar, bool>(nameof(Sticky), true);

    /// <summary>贴着底边、盖着下面的内容（只读）。</summary>
    public static readonly DirectProperty<ActionBar, bool> IsStuckProperty = AvaloniaProperty.RegisterDirect<ActionBar, bool>(nameof(IsStuck), bar => bar.IsStuck);

    readonly Border root = new() { Name = ActionBarParts.Root };
    readonly Flex row = new() { Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, Gap = Space.Md };
    readonly ContentControl start = new() { Name = ActionBarParts.Start, IsVisible = false };
    readonly Flex actions = new() { Name = ActionBarParts.Actions, Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, Gap = Space.Sm, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right };
    readonly TranslateTransform shift = new();
    ScrollViewer? scroller;

    static ActionBar() => AffectsMeasure<ActionBar>(TextElement.FontSizeProperty);

    /// <summary>新建：起始边的说明占满剩下的宽度，按钮靠结束边。</summary>
    public ActionBar()
    {
        Flex.SetGrow(start, true);
        row.Children.AddRange([start, actions]);
        root.Child = row;
        root.Bind(Border.BackgroundProperty, root.GetResourceObservable("Rune.Brush.Surface"));
        root.Bind(Border.BorderBrushProperty, root.GetResourceObservable("Rune.Brush.BorderMuted"));
        start.Bind(ContentControl.ForegroundProperty, start.GetResourceObservable("Rune.Brush.TextMuted"));
        RenderTransform = shift;
        // 挪上去时盖在前面的内容上面
        ZIndex = 1;
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>起始边的内容。</summary>
    public object? Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <summary>贴在滚动区域的底边。</summary>
    public bool Sticky { get => GetValue(StickyProperty); set => SetValue(StickyProperty, value); }

    /// <summary>贴着底边、盖着下面的内容。</summary>
    public bool IsStuck
    {
        get;
        private set
        {
            if (!SetAndRaise(IsStuckProperty, ref field, value)) return;
            PseudoClasses.Set(":stuck", value);
            root.Bind(Border.BorderBrushProperty, root.GetResourceObservable(value ? "Rune.Brush.Border" : "Rune.Brush.BorderMuted"));
            root.BoxShadow = value ? this.Resource<BoxShadows>(ThemeKeys.Shadow.Md) : default;
        }
    }

    /// <summary>按钮（靠结束边排开）。</summary>
    [Content]
    public global::Avalonia.Controls.Controls Children => actions.Children;

    /// <summary>往上挪了多少（测试用）。</summary>
    internal double Lift => -shift.Y;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StartProperty)
        {
            start.Content = Start;
            start.IsVisible = Start is not null and not "";
        }
        else if (change.Property == StickyProperty) Stick();
        else if (change.Property == TextElement.FontSizeProperty) UpdateMetrics();
    }

    /// <summary>按 em 的尺寸：上下 space.md、左右 space.lg，说明小一号（0.93em）。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        var (md, lg) = (this.Resource<double>(ThemeKeys.Space.Md) * em, this.Resource<double>(ThemeKeys.Space.Lg) * em);
        root.Padding = new Thickness(lg, md);
        root.BorderThickness = new Thickness(0, Math.Max(1, this.Resource<double>(ThemeKeys.BorderWidth.Xs)), 0, 0);
        start.SetValue(TextElement.FontSizeProperty, 0.93 * em);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateMetrics();
        scroller = this.FindAncestorOfType<ScrollViewer>();
        if (scroller is not null)
        {
            scroller.ScrollChanged += OnScrolled;
            scroller.LayoutUpdated += OnScrolled;
        }
        Stick();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (scroller is not null)
        {
            scroller.ScrollChanged -= OnScrolled;
            scroller.LayoutUpdated -= OnScrolled;
        }
        scroller = null;
    }

    void OnScrolled(object? sender, EventArgs e) => Stick();

    /// <summary>
    /// 网页的 position: sticky（bottom）：原来的位置在滚动区域的底边下面时往上挪到贴着底边，最多挪到父元素的顶上；
    /// 挪了就是盖着下面的内容。
    /// </summary>
    void Stick()
    {
        var lift = 0d;
        if (Sticky && scroller is not null && this.TranslatePoint(default, scroller) is { } at)
        {
            // TranslatePoint 带着现在的平移：减掉它得到原来的位置
            var bottom = at.Y - shift.Y + Bounds.Height;
            lift = Math.Clamp(bottom - scroller.Viewport.Height, 0, Bounds.Y);
        }
        if (Math.Abs(shift.Y + lift) > 0.01) shift.Y = -lift;
        IsStuck = lift > 0.5;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>ActionBar 的部件名：<c>rune|ActionBar Border#PART_Root</c>。</summary>
public static class ActionBarParts
{
    /// <summary>外层（TS 的 Root）：Border，底色、上边描边、内边距、贴底时的阴影。</summary>
    public const string Root = "PART_Root";

    /// <summary>起始边的说明（TS 的 propsStart）：ContentControl。</summary>
    public const string Start = "PART_Start";

    /// <summary>按钮区（TS 的 propsActions）：Flex。</summary>
    public const string Actions = "PART_Actions";
}
