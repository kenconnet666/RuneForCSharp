using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 固钉（vue-tsx-pro 的 Affix）：滚动到某个位置后贴住滚动区域的顶部（或底部）不走，如表单的提交栏、长表格上方的筛选条、文章的目录。
/// <code>
/// &lt;rune:ScrollArea&gt;&lt;StackPanel&gt;…&lt;rune:Affix OffsetTop="8"&gt;&lt;local:FilterBar /&gt;&lt;/rune:Affix&gt;…&lt;/StackPanel&gt;&lt;/rune:ScrollArea&gt;
/// &lt;rune:Affix OffsetBottom="0" StuckChanged="OnStuck"&gt;&lt;local:ActionBar /&gt;&lt;/rune:Affix&gt;
/// </code>
/// 同 CSS 的 position: sticky：不脱离排版（原来的位置照样占着），滚过时用位移钉在滚动区域里，只在父元素的范围里生效（父元素滚出去时跟着走）。
/// 滚动区域是 <see cref="Target"/>，不写时是外层最近的 ScrollViewer。贴住时放到兄弟元素上面（Panel.ZIndex）、加 md 阴影（<see cref="Shadow"/>），
/// <see cref="IsStuck"/> 变了报告 <see cref="StuckChanged"/>。写了 <see cref="OffsetBottom"/>（没写 <see cref="OffsetTop"/>）时贴在底部。
/// </summary>
[ContentProperty(nameof(Content))]
public class Affix : CompositeControl
{
    /// <summary>贴住时离顶部多远，默认 0；与 OffsetBottom 二选一。</summary>
    public static readonly DependencyProperty OffsetTopProperty = DependencyProperty.Register(nameof(OffsetTop), typeof(double?), typeof(Affix),
        new FrameworkPropertyMetadata(null, (d, _) => ((Affix)d).Update()));

    /// <summary>贴住时离底部多远：贴在底部。</summary>
    public static readonly DependencyProperty OffsetBottomProperty = DependencyProperty.Register(nameof(OffsetBottom), typeof(double?), typeof(Affix),
        new FrameworkPropertyMetadata(null, (d, _) => ((Affix)d).Update()));

    /// <summary>滚动的区域；不写时是外层最近的 ScrollViewer。</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(nameof(Target), typeof(ScrollViewer), typeof(Affix),
        new FrameworkPropertyMetadata(null, (d, _) => ((Affix)d).Attach()));

    /// <summary>贴住时加阴影，默认 true。</summary>
    public static readonly DependencyProperty ShadowProperty = DependencyProperty.Register(nameof(Shadow), typeof(bool), typeof(Affix),
        new FrameworkPropertyMetadata(true, (d, _) => ((Affix)d).UpdateShadow()));

    /// <summary>内容。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(UIElement), typeof(Affix),
        new FrameworkPropertyMetadata(null, (d, e) => ((Affix)d).root.Child = (UIElement?)e.NewValue));

    static readonly DependencyPropertyKey IsStuckKey = DependencyProperty.RegisterReadOnly(nameof(IsStuck), typeof(bool), typeof(Affix), new PropertyMetadata(false));

    /// <summary>贴住了。</summary>
    public static readonly DependencyProperty IsStuckProperty = IsStuckKey.DependencyProperty;

    /// <summary><see cref="StuckChanged"/> 的路由事件。</summary>
    public static readonly RoutedEvent StuckChangedEvent = EventManager.RegisterRoutedEvent(nameof(StuckChanged), RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(Affix));

    readonly Border root = new();
    readonly TranslateTransform shift = new();
    ScrollViewer? scroller;

    static Affix()
    {
        FocusableProperty.OverrideMetadata(typeof(Affix), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Affix), new FrameworkPropertyMetadata(false));
    }

    /// <summary>新建：内容包在画阴影的那一层里。</summary>
    public Affix()
    {
        RenderTransform = shift;
        Root = root;
        Loaded += (_, _) =>
        {
            Attach();
            LayoutUpdated += OnLayoutUpdated;
        };
        Unloaded += (_, _) =>
        {
            LayoutUpdated -= OnLayoutUpdated;
            Listen(null);
        };
    }

    /// <summary>离顶部多远。</summary>
    public double? OffsetTop { get => (double?)GetValue(OffsetTopProperty); set => SetValue(OffsetTopProperty, value); }

    /// <summary>离底部多远。</summary>
    public double? OffsetBottom { get => (double?)GetValue(OffsetBottomProperty); set => SetValue(OffsetBottomProperty, value); }

    /// <summary>滚动的区域。</summary>
    public ScrollViewer? Target { get => (ScrollViewer?)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <summary>贴住时加阴影。</summary>
    public bool Shadow { get => (bool)GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    /// <summary>内容。</summary>
    public UIElement? Content { get => (UIElement?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>贴住了。</summary>
    public bool IsStuck { get => (bool)GetValue(IsStuckProperty); private set => SetValue(IsStuckKey, value); }

    /// <summary>贴住、离开时（看 <see cref="IsStuck"/>）。</summary>
    public event RoutedEventHandler StuckChanged
    {
        add => AddHandler(StuckChangedEvent, value);
        remove => RemoveHandler(StuckChangedEvent, value);
    }

    /// <summary>钉住的位移（测试用）。</summary>
    internal double Shift => shift.Y;

    /// <summary>画阴影的那一层（测试用）。</summary>
    internal Border Layer => root;

    bool AtBottom => OffsetBottom is not null && OffsetTop is null;

    void Attach()
    {
        if (!IsLoaded) return;
        ScrollViewer? found = null;
        for (var node = VisualTreeHelper.GetParent(this); node is not null && found is null; node = VisualTreeHelper.GetParent(node)) found = node as ScrollViewer;
        Listen(Target ?? found);
    }

    void Listen(ScrollViewer? next)
    {
        if (scroller == next) return;
        if (scroller is not null) scroller.ScrollChanged -= OnScrolled;
        scroller = next;
        if (scroller is not null) scroller.ScrollChanged += OnScrolled;
        Update();
    }

    void OnScrolled(object sender, ScrollChangedEventArgs e) => Update();

    void OnLayoutUpdated(object? sender, EventArgs e) => Update();

    /// <summary>
    /// 原来的位置（去掉位移）滚到离顶部不到 OffsetTop 时往下钉，但不超出父元素的底边；贴底时同理往上钉，不超出父元素的顶边。
    /// 钉着的时候放到兄弟元素上面（它们排在后面，会盖住它）。
    /// </summary>
    void Update()
    {
        if (scroller is null || VisualTreeHelper.GetParent(this) is not FrameworkElement parent || !scroller.IsAncestorOf(this))
        {
            Pin(0);
            return;
        }
        var natural = TranslatePoint(default, scroller).Y - shift.Y;
        var parentTop = parent.TranslatePoint(default, scroller).Y;
        var height = ActualHeight;
        double next;
        if (AtBottom)
            next = Math.Min(0, Math.Max(Math.Min(0, scroller.ViewportHeight - (OffsetBottom ?? 0) - (natural + height)), parentTop - natural));
        else
            next = Math.Max(0, Math.Min(Math.Max(0, (OffsetTop ?? 0) - natural), parentTop + parent.ActualHeight - (natural + height)));
        Pin(next);
    }

    void Pin(double offset)
    {
        if (shift.Y != offset) shift.Y = offset;
        var stuck = Math.Abs(offset) > 0.5;
        Panel.SetZIndex(this, stuck ? 5 : 0);
        if (stuck == IsStuck) return;
        IsStuck = stuck;
        UpdateShadow();
        RaiseEvent(new RoutedEventArgs(StuckChangedEvent, this));
    }

    /// <summary>贴住时 md 阴影（浮在内容上）。</summary>
    void UpdateShadow()
    {
        if (Shadow && IsStuck) root.SetResourceReference(EffectProperty, ThemeKeys.Shadow.Md);
        else root.ClearValue(EffectProperty);
    }
}
