using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf;

/// <summary>
/// 正文列表的共同部分（vue-tsx-pro 的 listStylesProse）：<see cref="ListItem"/> 竖着排，项之间 <see cref="Gap"/>；
/// 字号档位 <see cref="Size"/> 设在列表上（主题样式的触发器），项继承。列表给直接子元素里的每一项设记号（圆点、编号、图标）。
/// </summary>
public abstract class ListBase : Panel
{
    /// <summary>字号档位；不写时继承外面的字号。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(ListBase), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    /// <summary>项与项之间的间距档位，默认 xs；每项是一段话时用 md。</summary>
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(Space), typeof(ListBase), new FrameworkPropertyMetadata(Space.Xs, (d, _) => ((ListBase)d).BindGap()));

    static readonly DependencyProperty GapFactorProperty = DependencyProperty.Register(
        "GapFactor", typeof(double), typeof(ListBase), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    static ListBase()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ListBase), new FrameworkPropertyMetadata(typeof(ListBase)));
        TextElement.FontSizeProperty.OverrideMetadata(typeof(ListBase), new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));
    }

    /// <summary>新建：间距绑到主题的 space。</summary>
    private protected ListBase() => BindGap();

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>项与项之间的间距档位。</summary>
    public Space Gap { get => (Space)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>直接子元素里的列表项（HTML 里 ol 只给 li 编号）。</summary>
    private protected IEnumerable<ListItem> Items => InternalChildren.OfType<ListItem>();

    double GapPixels => (double)GetValue(GapFactorProperty) * TextElement.GetFontSize(this);

    /// <summary>给每一项设记号。</summary>
    private protected abstract void UpdateMarkers();

    void BindGap() => SetResourceReference(GapFactorProperty, Gap.ResourceKey);

    /// <summary>档位名由主题样式的触发器给字号；像素数直接设。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TextSize)e.NewValue).Pixels is { } pixels) d.SetValue(TextElement.FontSizeProperty, pixels);
        else if (((TextSize)e.OldValue).Pixels is not null) d.ClearValue(TextElement.FontSizeProperty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var (width, height, shown) = (0d, 0d, 0);
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            if (child.Visibility == Visibility.Collapsed) continue;
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
            shown++;
        }
        return new Size(width, height + Math.Max(0, shown - 1) * GapPixels);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0d;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
            y += child.DesiredSize.Height + GapPixels;
        }
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        UpdateMarkers();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListAutomationPeer(this);

    sealed class ListAutomationPeer(ListBase owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => Owner.GetType().Name;
    }
}

/// <summary>
/// 无序列表（vue-tsx-pro 的 List）：圆点，或 <see cref="Icon"/> 代替圆点（功能清单的对勾），<see cref="ListItem.Icon"/> 逐项覆盖。
/// <code>
/// &lt;rune:List Icon="{x:Static rune:Lucide.Check}"&gt;
///   &lt;rune:ListItem&gt;不限项目&lt;/rune:ListItem&gt;
///   &lt;rune:ListItem Icon="{x:Static rune:Lucide.X}"&gt;单点登录&lt;/rune:ListItem&gt;
/// &lt;/rune:List&gt;
/// </code>
/// 各项的图标引用部件样式键 <see cref="ListParts.Icon"/>（TS 的 propsIcon），改颜色在列表的 Resources 或 Provider.Styles 里写这个键的样式。
/// 界面结构上的列表（导航、分页、步骤）不要圆点，用布局组件。
/// </summary>
public class List : ListBase
{
    /// <summary>用图标代替圆点；ListItem 的 Icon 逐项覆盖。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(IconData), typeof(List), new FrameworkPropertyMetadata(null, (d, _) => ((List)d).UpdateMarkers()));

    /// <summary>用图标代替圆点。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    private protected override void UpdateMarkers()
    {
        foreach (var item in Items) item.SetMarker("•", Icon);
    }
}

/// <summary>有序列表（vue-tsx-pro 的 OrderedList）：编号从 <see cref="Start"/> 起；里面的项不沿用外层 List 的图标。</summary>
public class OrderedList : ListBase
{
    /// <summary>第一项的编号，默认 1（HTML 的 start）。</summary>
    public static readonly DependencyProperty StartProperty = DependencyProperty.Register(
        nameof(Start), typeof(int), typeof(OrderedList), new FrameworkPropertyMetadata(1, (d, _) => ((OrderedList)d).UpdateMarkers()));

    /// <summary>第一项的编号。</summary>
    public int Start { get => (int)GetValue(StartProperty); set => SetValue(StartProperty, value); }

    private protected override void UpdateMarkers()
    {
        var number = Start;
        foreach (var item in Items) item.SetMarker($"{number++}.", null);
    }
}

/// <summary>
/// 列表项（vue-tsx-pro 的 ListItem）：记号（圆点、编号）在起始边的 1.5em 里，文字与记号隔 space.xs；
/// 带图标时图标在前、文字在后（隔 space.sm），不缩进。图标引用部件样式键 <see cref="ListItemParts.Icon"/>。
/// 记号与图标是装饰，读屏念项里的文字。
/// </summary>
[TemplatePart(Name = Marker, Type = typeof(TextBlock))]
[TemplatePart(Name = IconPart, Type = typeof(Icon))]
[TemplatePart(Name = ContentPart, Type = typeof(ContentPresenter))]
public class ListItem : ContentControl
{
    const string Marker = "PART_Marker";
    const string IconPart = "PART_Icon";
    const string ContentPart = "PART_ContentPresenter";

    /// <summary>这一项的图标，覆盖 List 的 Icon（有序列表里不用）。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(IconData), typeof(ListItem), new FrameworkPropertyMetadata(null, (d, _) => ((ListItem)d).UpdateParts()));

    string? marker;
    IconData? listIcon;
    TextBlock? markerPart;
    Icon? iconPart;
    ContentPresenter? contentPart;

    static ListItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ListItem), new FrameworkPropertyMetadata(typeof(ListItem)));
        FocusableProperty.OverrideMetadata(typeof(ListItem), new FrameworkPropertyMetadata(false));
    }

    /// <summary>这一项的图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>列表给的记号与默认图标。</summary>
    internal void SetMarker(string text, IconData? icon)
    {
        (marker, listIcon) = (text, icon);
        UpdateParts();
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        markerPart = GetTemplateChild(Marker) as TextBlock;
        iconPart = GetTemplateChild(IconPart) as Icon;
        contentPart = GetTemplateChild(ContentPart) as ContentPresenter;
        UpdateParts();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) UpdateParts();
    }

    /// <summary>记号或图标，与按字号算的 em 尺寸（模板里没有 em 绑定，同 Badge 在代码里算）。</summary>
    void UpdateParts()
    {
        var shown = Icon ?? listIcon;
        var em = FontSize;
        if (markerPart is not null)
        {
            markerPart.Text = marker;
            markerPart.Visibility = shown is null ? Visibility.Visible : Visibility.Collapsed;
            markerPart.Width = 1.5 * em;
            markerPart.Padding = new Thickness(0, 0, 0.3 * em, 0);
        }
        if (iconPart is not null)
        {
            iconPart.Data = shown;
            iconPart.Visibility = shown is null ? Visibility.Collapsed : Visibility.Visible;
            iconPart.Margin = new Thickness(0, 0.2 * em, 0, 0);
        }
        contentPart?.Margin = new Thickness((shown is null ? 0.25 : 0.5) * em, 0, 0, 0);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListItemAutomationPeer(this);

    sealed class ListItemAutomationPeer(ListItem owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(ListItem);

        // 记号是装饰（TS 的 ::marker 读屏不念），只留内容
        protected override List<AutomationPeer>? GetChildrenCore() =>
            base.GetChildrenCore()?.Where(peer => peer is not UIElementAutomationPeer { Owner: var element } || element != owner.markerPart).ToList();
    }
}

/// <summary>List 的部件样式键：各项的图标（TS 的 propsIcon），与 <see cref="ListItemParts.Icon"/> 是同一个键。</summary>
public static class ListParts
{
    /// <summary>各项的图标。</summary>
    public static readonly PartKey Icon = ListItemParts.Icon;
}

/// <summary>ListItem 的部件样式键：Provider.Styles 或列表的 Resources 里写 <c>&lt;Style x:Key="{x:Static rune:ListItemParts.Icon}" TargetType="rune:Icon"&gt;</c>。</summary>
public static class ListItemParts
{
    /// <summary>图标（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(ListItem), nameof(Icon), typeof(Icon));

    /// <summary>内容层（TS 的 propsContent）。</summary>
    public static readonly PartKey Content = new(typeof(ListItem), nameof(Content), typeof(ContentPresenter));
}
