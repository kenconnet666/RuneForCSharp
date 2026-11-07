using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 展开收起的内容区（vue-tsx-pro 的 Collapse），带高度过渡。触发器另外写：
/// <code>
/// &lt;rune:Collapse IsOpen="{Binding ShowAdvanced}"&gt;&lt;rune:Grid MinItemWidth="9em"&gt;…&lt;/rune:Grid&gt;&lt;/rune:Collapse&gt;
/// </code>
/// 高度在 0 与内容高度之间过渡（durations.md、easings.standard，系统关闭动画时直接到位）；内容一直按完整高度排版，露不出来的部分裁掉。
/// 完全收起（过渡结束）后内容隐藏：不能 Tab 聚焦，读屏也读不到；收起的过程中仍可见。样式里按 IsOpen 写触发器（TS 的 data-state）。
/// <see cref="Lazy"/> 时第一次展开才放进内容，之后收起也保留。内容层引用部件样式键 <see cref="CollapseParts.Content"/>。
/// 页内搜索命中收起的内容时自动展开（hidden="until-found"）是浏览器的能力，桌面上没有。
/// </summary>
[ContentProperty(nameof(Content))]
public class Collapse : FrameworkElement
{
    /// <summary>是否展开（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(Collapse), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Collapse)d).Toggle()));

    /// <summary>第一次展开时才放进内容。</summary>
    public static readonly DependencyProperty LazyProperty = DependencyProperty.Register(
        nameof(Lazy), typeof(bool), typeof(Collapse), new FrameworkPropertyMetadata(false, (d, _) => ((Collapse)d).UpdateContent()));

    /// <summary>内容。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(UIElement), typeof(Collapse), new FrameworkPropertyMetadata(null, (d, _) => ((Collapse)d).UpdateContent()));

    /// <summary>露出来的比例：0 收起，1 展开（过渡驱动）。</summary>
    static readonly DependencyProperty OpennessProperty = DependencyProperty.Register(
        "Openness", typeof(double), typeof(Collapse), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    readonly Border holder = new() { Visibility = Visibility.Collapsed };
    DoubleAnimation? transition;
    bool opened;

    static Collapse() => ClipToBoundsProperty.OverrideMetadata(typeof(Collapse), new FrameworkPropertyMetadata(true));

    /// <summary>新建：收起。</summary>
    public Collapse()
    {
        holder.SetResourceReference(StyleProperty, CollapseParts.Content);
        AddVisualChild(holder);
        AddLogicalChild(holder);
    }

    /// <summary>是否展开。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>第一次展开时才放进内容（内容很重、多数人不会展开时用）。</summary>
    public bool Lazy { get => (bool)GetValue(LazyProperty); set => SetValue(LazyProperty, value); }

    /// <summary>内容。</summary>
    public UIElement? Content { get => (UIElement?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => holder;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { holder }.GetEnumerator();

    void UpdateContent() => holder.Child = opened || !Lazy ? Content : null;

    /// <summary>展开时先显示内容再长高；收起时缩到 0、过渡结束才隐藏。没加载时直接到位。</summary>
    void Toggle()
    {
        var open = IsOpen;
        if (open)
        {
            opened = true;
            UpdateContent();
            holder.Visibility = Visibility.Visible;
        }
        var (from, to) = ((double)GetValue(OpennessProperty), open ? 1d : 0d);
        // 先写终值：过渡结束（FillBehavior.Stop）或被打断后留下的就是它
        SetValue(OpennessProperty, to);
        var duration = IsLoaded ? Motion.Duration(this, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200)) : TimeSpan.Zero;
        if (duration <= TimeSpan.Zero)
        {
            transition = null;
            BeginAnimation(OpennessProperty, null);
            Settle();
            return;
        }
        var animation = new DoubleAnimation(from, to, duration) { EasingFunction = TryFindResource("Rune.Easing.Standard") as IEasingFunction, FillBehavior = FillBehavior.Stop };
        animation.Completed += (_, _) =>
        {
            if (transition == animation) Settle();
        };
        transition = animation;
        BeginAnimation(OpennessProperty, animation);
    }

    /// <summary>过渡结束：完全收起时隐藏内容（不能聚焦、读屏读不到）。</summary>
    void Settle()
    {
        if (!IsOpen) holder.Visibility = Visibility.Collapsed;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        holder.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return new Size(holder.DesiredSize.Width, holder.DesiredSize.Height * (double)GetValue(OpennessProperty));
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        holder.Arrange(new Rect(0, 0, finalSize.Width, holder.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>Collapse 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:CollapseParts.Content}" TargetType="Border"&gt;</c>。</summary>
public static class CollapseParts
{
    /// <summary>内容层（TS 的 propsContent）：按完整高度排版，收起后隐藏。</summary>
    public static readonly PartKey Content = new(typeof(Collapse), nameof(Content), typeof(Border));
}
