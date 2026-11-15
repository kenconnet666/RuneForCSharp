using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 固钉（vue-tsx-pro 的 Affix）：滚动到某个位置后贴住滚动区域的顶部（或底部）不走，如表单的提交栏、长表格上方的筛选条、文章的目录。
/// <code>
/// &lt;rune:ScrollArea&gt;&lt;StackPanel&gt;…&lt;rune:Affix OffsetTop="8"&gt;&lt;local:FilterBar /&gt;&lt;/rune:Affix&gt;…&lt;/StackPanel&gt;&lt;/rune:ScrollArea&gt;
/// &lt;rune:Affix OffsetBottom="0" StuckChanged="OnStuck"&gt;&lt;local:ActionBar /&gt;&lt;/rune:Affix&gt;
/// </code>
/// 同 CSS 的 position: sticky：不脱离排版（原来的位置照样占着），滚过时用位移钉在滚动区域里，只在父元素的范围里生效（父元素滚出去时跟着走）。
/// 滚动区域是 <see cref="Target"/>，不写时是外层最近的 ScrollViewer。贴住时放到兄弟元素上面、加 md 阴影（<see cref="Shadow"/>），伪类 :stuck，报告 <see cref="StuckChanged"/>。
/// 写了 <see cref="OffsetBottom"/>（没写 <see cref="OffsetTop"/>）时贴在底部。部件：<see cref="AffixParts"/>。
/// </summary>
public class Affix : Control
{
    /// <summary>贴住时离顶部多远，默认 0；与 OffsetBottom 二选一。</summary>
    public static readonly StyledProperty<double?> OffsetTopProperty = AvaloniaProperty.Register<Affix, double?>(nameof(OffsetTop));

    /// <summary>贴住时离底部多远：贴在底部。</summary>
    public static readonly StyledProperty<double?> OffsetBottomProperty = AvaloniaProperty.Register<Affix, double?>(nameof(OffsetBottom));

    /// <summary>滚动的区域；不写时是外层最近的 ScrollViewer。</summary>
    public static readonly StyledProperty<ScrollViewer?> TargetProperty = AvaloniaProperty.Register<Affix, ScrollViewer?>(nameof(Target));

    /// <summary>贴住时加阴影，默认 true。</summary>
    public static readonly StyledProperty<bool> ShadowProperty = AvaloniaProperty.Register<Affix, bool>(nameof(Shadow), true);

    /// <summary>内容。</summary>
    public static readonly StyledProperty<Control?> ContentProperty = AvaloniaProperty.Register<Affix, Control?>(nameof(Content));

    /// <summary>贴住了。</summary>
    public static readonly DirectProperty<Affix, bool> IsStuckProperty = AvaloniaProperty.RegisterDirect<Affix, bool>(nameof(IsStuck), affix => affix.IsStuck);

    /// <summary><see cref="StuckChanged"/> 的路由事件。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StuckChangedEvent = RoutedEvent.Register<Affix, RoutedEventArgs>(nameof(StuckChanged), RoutingStrategies.Direct);

    readonly Border root = new() { Name = AffixParts.Root };
    readonly TranslateTransform shift = new();
    ScrollViewer? scroller;

    /// <summary>新建：内容包在画阴影的那一层里。</summary>
    public Affix()
    {
        RenderTransform = shift;
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>离顶部多远。</summary>
    public double? OffsetTop { get => GetValue(OffsetTopProperty); set => SetValue(OffsetTopProperty, value); }

    /// <summary>离底部多远。</summary>
    public double? OffsetBottom { get => GetValue(OffsetBottomProperty); set => SetValue(OffsetBottomProperty, value); }

    /// <summary>滚动的区域。</summary>
    public ScrollViewer? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <summary>贴住时加阴影。</summary>
    public bool Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    /// <summary>内容。</summary>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>贴住了。</summary>
    public bool IsStuck
    {
        get;
        private set
        {
            if (!SetAndRaise(IsStuckProperty, ref field, value)) return;
            PseudoClasses.Set(":stuck", value);
            UpdateShadow();
            RaiseEvent(new RoutedEventArgs(StuckChangedEvent, this));
        }
    }

    /// <summary>贴住、离开时（看 <see cref="IsStuck"/>）。</summary>
    public event EventHandler<RoutedEventArgs>? StuckChanged
    {
        add => AddHandler(StuckChangedEvent, value);
        remove => RemoveHandler(StuckChangedEvent, value);
    }

    /// <summary>钉住的位移（测试用）。</summary>
    internal double Shift => shift.Y;

    bool AtBottom => OffsetBottom is not null && OffsetTop is null;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty) root.Child = Content;
        else if (change.Property == TargetProperty) Listen(Target ?? this.FindAncestorOfType<ScrollViewer>());
        else if (change.Property == ShadowProperty) UpdateShadow();
        else if (change.Property == OffsetTopProperty || change.Property == OffsetBottomProperty) Update();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Listen(Target ?? this.FindAncestorOfType<ScrollViewer>());
        LayoutUpdated += OnLayoutUpdated;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnLayoutUpdated;
        Listen(null);
    }

    void Listen(ScrollViewer? next)
    {
        if (scroller == next) return;
        if (scroller is not null) scroller.ScrollChanged -= OnScrolled;
        scroller = next;
        if (scroller is not null) scroller.ScrollChanged += OnScrolled;
        Update();
    }

    void OnScrolled(object? sender, ScrollChangedEventArgs e) => Update();

    void OnLayoutUpdated(object? sender, EventArgs e) => Update();

    /// <summary>
    /// 原来的位置（去掉位移）滚到离顶部不到 OffsetTop 时往下钉，但不超出父元素的底边；贴底时同理往上钉，不超出父元素的顶边。
    /// 钉着的时候放到兄弟元素上面（它们排在后面，会盖住它）。
    /// </summary>
    void Update()
    {
        if (scroller is null || this.GetVisualParent() is not Visual parent || this.TranslatePoint(default, scroller) is not { } at || parent.TranslatePoint(default, scroller) is not { } parentAt)
        {
            Pin(0);
            return;
        }
        var natural = at.Y - shift.Y;
        var height = Bounds.Height;
        double next;
        if (AtBottom)
        {
            var limit = scroller.Viewport.Height - (OffsetBottom ?? 0);
            next = Math.Max(Math.Min(0, limit - (natural + height)), parentAt.Y - natural);
            next = Math.Min(0, next);
        }
        else
        {
            next = Math.Min(Math.Max(0, (OffsetTop ?? 0) - natural), parentAt.Y + parent.Bounds.Height - (natural + height));
            next = Math.Max(0, next);
        }
        Pin(next);
    }

    void Pin(double offset)
    {
        if (shift.Y != offset) shift.Y = offset;
        var stuck = Math.Abs(offset) > 0.5;
        ZIndex = stuck ? 5 : 0;
        IsStuck = stuck;
    }

    /// <summary>贴住时 md 阴影（浮在内容上）。</summary>
    void UpdateShadow()
    {
        if (Shadow && IsStuck) root[!Border.BoxShadowProperty] = root.GetResourceObservable(ThemeKeys.Shadow.Md).ToBinding();
        else root.ClearValue(Border.BoxShadowProperty);
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

/// <summary>Affix 的部件名。</summary>
public static class AffixParts
{
    /// <summary>钉住的那一层（画阴影）。</summary>
    public const string Root = "PART_Root";
}
