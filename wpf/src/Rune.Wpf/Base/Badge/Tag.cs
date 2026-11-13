using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 标签（vue-tsx-pro 的 Tag）：外观同 <see cref="Badge"/>（样式、色调、字号、圆角、图标），可以带移除按钮（筛选条件、已选项、输入的关键词）。
/// <code>
/// &lt;rune:Tag Icon="{x:Static rune:Lucide.Code}"&gt;TypeScript&lt;/rune:Tag&gt;
/// &lt;rune:Tag Tone="accent" Radius="full" RemoveRequested="OnRemoveFilter"&gt;品牌：小米&lt;/rune:Tag&gt;
/// </code>
/// 订阅了 <see cref="RemoveRequested"/> 才有移除按钮（上游给了 onRemove 才有）：文字后面一个圆形的 <see cref="CloseButton"/>，宽 1.5em、
/// 字号与颜色同标签，悬停的底色是标签文字色的 14%、按下的波纹 22%（彩色标签上协调，不用幽灵按钮的灰色），读屏名称是"移除 + 标签文字"（<see cref="RemoveLabel"/> 能改）。点它时标签先淡出缩小到 0.8（durations.sm、exit），
/// 离场结束才发 RemoveRequested。使用方移除了标签时，原来在移除按钮上的焦点交给同一容器里下一个（没有就上一个）能聚焦的元素；
/// 没移除（如拒绝删除）时淡入复原。部件样式键：<see cref="TagParts"/>。
/// 可选中（筛选条件的一排标签）：写了 <see cref="IsSelected"/> 时整个标签是切换按钮（读屏念"已按下 / 未按下"），点击、空格、Enter 切换；
/// 没选中时是 surface（有描边），选中时 solid 实底并在前面加一个对勾（不只靠颜色）；没写的色调、字号用 accent、sm（主题样式的触发器，写了的照用）。
/// 悬停时叠一层 8% 的文字色（状态层：各色调、各样式都适用，亮色里变深、暗色里变亮）。
/// 可选中的标签不带移除按钮。一排放在有名称的容器里（AutomationProperties.Name）。
/// <code>
/// &lt;rune:Tag IsSelected="{Binding Urgent}"&gt;加急&lt;/rune:Tag&gt;
/// </code>
/// </summary>
public class Tag : Badge
{
    /// <summary>移除按钮的读屏名称；不写时是"移除"加上标签文字（内容是文字时）。</summary>
    public static readonly DependencyProperty RemoveLabelProperty = DependencyProperty.Register(nameof(RemoveLabel), typeof(string), typeof(Tag),
        new FrameworkPropertyMetadata(null, (d, _) => ((Tag)d).UpdateRemoveLabel()));

    /// <summary>可选中：null 时是普通标签；true / false 时整个标签是切换按钮（默认双向绑定），点一下切换。</summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(nameof(IsSelected), typeof(bool?), typeof(Tag),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Tag)d).SyncSelectable()));

    static readonly DependencyProperty FocusRingProperty = DependencyProperty.Register("FocusRing", typeof(Brush), typeof(Tag),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>出现的程度：1 是完全显示，0 是离场的终点（透明、缩到 0.8）。</summary>
    static readonly DependencyProperty PresenceProperty = DependencyProperty.Register("Presence", typeof(double), typeof(Tag),
        new FrameworkPropertyMetadata(1d, (d, e) => ((Tag)d).ApplyPresence((double)e.NewValue)));

    EventHandler? removeRequested;
    CloseButton? remove;
    bool dismissing;
    bool focusVisible;
    bool pressed;

    static Tag()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Tag), new FrameworkPropertyMetadata(typeof(Tag)));
        ContentProperty.OverrideMetadata(typeof(Tag), new FrameworkPropertyMetadata((d, _) => ((Tag)d).UpdateRemoveLabel()));
        FontSizeProperty.OverrideMetadata(typeof(Tag), new FrameworkPropertyMetadata((d, _) => ((Tag)d).UpdateRemoveSize()));
        // 文字色变了（主题、色调），悬停中的移除按钮底色跟着换
        ForegroundProperty.OverrideMetadata(typeof(Tag), new FrameworkPropertyMetadata((d, _) => ((Tag)d).UpdateRemoveBackground()));
    }

    /// <summary>新建：变形以中心为原点（缩小时向中间收）。</summary>
    public Tag()
    {
        RenderTransformOrigin = new Point(0.5, 0.5);
        SetResourceReference(FocusRingProperty, "Rune.Brush.FocusRing");
    }

    /// <summary>可选中。</summary>
    public bool? IsSelected { get => (bool?)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>可选中时能聚焦；不带移除按钮。</summary>
    void SyncSelectable()
    {
        Focusable = IsSelected is not null;
        SyncRemove();
    }

    /// <summary>切换选中（可选中时）。</summary>
    void ToggleSelected()
    {
        if (IsSelected is { } selected && IsEnabled) SetCurrentValue(IsSelectedProperty, !selected);
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (IsSelected is null) return;
        pressed = true;
        Focus();
        e.Handled = true;
    }

    /// <summary>在上面按下、没离开过就松开才切换。</summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!pressed) return;
        pressed = false;
        e.Handled = true;
        ToggleSelected();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        pressed = false;
        if (IsSelected is not null) InvalidateVisual();
    }

    /// <summary>Enter 按下时、空格松开时切换（同原生按钮）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsSelected is null || e.Key is not (Key.Enter or Key.Space)) return;
        e.Handled = true;
        if (e.Key == Key.Enter) ToggleSelected();
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (IsSelected is null || e.Key != Key.Space) return;
        e.Handled = true;
        ToggleSelected();
    }

    /// <summary>焦点环只在键盘聚焦时显示（同 :focus-visible）。</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        focusVisible = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        focusVisible = false;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (IsSelected is not null) InvalidateVisual();
    }

    /// <summary>可选中时：悬停叠一层 8% 的文字色（画在底色上、文字下，同上游的 background-image）；键盘聚焦画焦点环（2px focusRing，压在边上，同按钮）。</summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (IsSelected is null) return;
        if (IsMouseOver && Foreground is SolidColorBrush fore)
        {
            var box = new Rect(RenderSize);
            var corner = CornerRadius(box.Height);
            drawingContext.DrawRoundedRectangle(new SolidColorBrush(fore.Color) { Opacity = 0.08 * fore.Opacity }, null, box, corner, corner);
        }
        if (!focusVisible || GetValue(FocusRingProperty) is not Brush ring) return;
        var bounds = new Rect(RenderSize);
        bounds.Inflate(-1, -1);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var radius = CornerRadius(bounds.Height);
        drawingContext.DrawRoundedRectangle(null, new Pen(ring, 2), bounds, radius, radius);
    }

    /// <summary>可选中时读屏是切换按钮（名称取文字）；否则同 Badge，直接读里面的文字。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new TagAutomationPeer(this);

    sealed class TagAutomationPeer(Tag owner) : FrameworkElementAutomationPeer(owner), IToggleProvider
    {
        protected override string GetClassNameCore() => nameof(Tag);

        protected override AutomationControlType GetAutomationControlTypeCore() => owner.IsSelected is null ? AutomationControlType.Text : AutomationControlType.Button;

        protected override bool IsControlElementCore() => owner.IsSelected is not null;

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Content as string ?? "";

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Toggle && owner.IsSelected is not null ? this : base.GetPattern(patternInterface);

        public ToggleState ToggleState => owner.IsSelected == true ? ToggleState.On : ToggleState.Off;

        public void Toggle() => owner.ToggleSelected();
    }

    /// <summary>移除按钮的读屏名称。</summary>
    public string? RemoveLabel { get => (string?)GetValue(RemoveLabelProperty); set => SetValue(RemoveLabelProperty, value); }

    /// <summary>请使用方移除这个标签（离场结束时发）；订阅了才有移除按钮。</summary>
    public event EventHandler? RemoveRequested
    {
        add
        {
            removeRequested += value;
            SyncRemove();
        }
        remove
        {
            removeRequested -= value;
            SyncRemove();
        }
    }

    /// <summary>移除按钮能不能聚焦（SelectedTags 里不能：焦点留在下拉的触发框上，按下也不抢）。</summary>
    internal bool RemoveFocusable
    {
        get;
        set
        {
            field = value;
            if (remove is not null) remove.Focusable = value;
        }
    } = true;

    /// <summary>正在离场（测试、样式用；上游的 data-state="closed"）。</summary>
    public bool IsDismissing => dismissing;

    void ApplyPresence(double presence)
    {
        Opacity = presence;
        RenderTransform = presence >= 1 ? Transform.Identity : new ScaleTransform(0.8 + 0.2 * presence, 0.8 + 0.2 * presence);
    }

    /// <summary>有订阅时放上移除按钮，没有时拿掉。</summary>
    void SyncRemove()
    {
        if (removeRequested is null || IsSelected is not null)
        {
            Trailing = remove = null;
            return;
        }
        if (remove is not null) return;
        remove = new CloseButton { Radius = Radius.Full, VerticalAlignment = VerticalAlignment.Center, Focusable = RemoveFocusable };
        remove.SetResourceReference(StyleProperty, TagParts.Remove);
        // 颜色同标签（上游 color: inherit）
        remove.SetBinding(ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
        // 用按钮自己的事件：DependencyPropertyDescriptor.AddValueChanged 不退订会让按钮与标签一直留在静态表里
        remove.MouseEnter += (_, _) => UpdateRemoveBackground();
        remove.MouseLeave += (_, _) => UpdateRemoveBackground();
        remove.Click += (_, _) => Dismiss();
        Trailing = remove;
        UpdateRemoveSize();
        UpdateRemoveLabel();
    }

    /// <summary>移除按钮悬停的底色：标签文字色的 14%（本地值盖过幽灵按钮主题的灰色）；平时交还主题。按下的波纹本来就是文字色的 22%。</summary>
    void UpdateRemoveBackground()
    {
        if (remove is null) return;
        if (remove.IsMouseOver && Foreground is SolidColorBrush fore) remove.Background = new SolidColorBrush(fore.Color) { Opacity = 0.14 * fore.Opacity };
        else remove.ClearValue(BackgroundProperty);
    }

    /// <summary>字号同标签（不按按钮自己的档位放大），宽、最小高 1.5em，上下各让出 0.25em（不把标签撑高）。</summary>
    void UpdateRemoveSize()
    {
        if (remove is null) return;
        var em = FontSize;
        remove.FontSize = em;
        remove.Width = remove.MinHeight = 1.5 * em;
        remove.Margin = new Thickness(0, -0.25 * em, 0, -0.25 * em);
    }

    void UpdateRemoveLabel()
    {
        if (remove is null) return;
        var lang = Provider.GetLang(this);
        AutomationProperties.SetName(remove, RemoveLabel ?? (Content is string { Length: > 0 } text ? $"{lang.Remove} {text}" : lang.Remove));
    }

    /// <summary>
    /// 离场：记下焦点在不在移除按钮上与同一容器里的候选，淡出缩小，结束后请使用方移除；布局之后还在界面上就淡入复原，
    /// 被移除了且焦点丢了就交给候选里第一个还在界面上的。
    /// </summary>
    void Dismiss()
    {
        if (dismissing) return;
        dismissing = true;
        var candidates = IsKeyboardFocusWithin ? FocusCandidates() : [];
        Animate(0, "Rune.Easing.Exit", () =>
        {
            dismissing = false;
            removeRequested?.Invoke(this, EventArgs.Empty);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (PresentationSource.FromVisual(this) is not null)
                {
                    Animate(1, "Rune.Easing.Enter", null);
                    return;
                }
                if (Keyboard.FocusedElement is UIElement { IsVisible: true }) return;
                candidates.FirstOrDefault(candidate => PresentationSource.FromVisual(candidate) is not null)?.Focus();
            });
        });
    }

    /// <summary>同一容器（最近的面板：Flex、ItemsControl 的项面板）里别的能 Tab 聚焦的元素：后面的在前，前面的倒序接在后面。</summary>
    List<UIElement> FocusCandidates()
    {
        DependencyObject? group = VisualTreeHelper.GetParent(this);
        while (group is not null and not Panel) group = VisualTreeHelper.GetParent(group);
        if (group is null) return [];
        var following = new List<UIElement>();
        var preceding = new List<UIElement>();
        var after = false;
        void Walk(DependencyObject parent)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child == this)
                {
                    after = true;
                    continue;
                }
                if (child is UIElement { Focusable: true, IsEnabled: true, IsVisible: true } element && KeyboardNavigation.GetIsTabStop(element))
                    (after ? following : preceding).Add(element);
                Walk(child);
            }
        }
        Walk(group);
        preceding.Reverse();
        return [.. following, .. preceding];
    }

    /// <summary>出现程度过渡到 <paramref name="to"/>（durations.sm）；系统关闭动画、还没加载时立即到位。</summary>
    void Animate(double to, string easingKey, Action? completed)
    {
        var from = (double)GetValue(PresenceProperty);
        // 先写终值：过渡结束（FillBehavior.Stop）或被打断后留下的就是它
        SetValue(PresenceProperty, to);
        var duration = IsLoaded ? Motion.Duration(this, "Rune.Duration.Sm", TimeSpan.FromMilliseconds(120)) : TimeSpan.Zero;
        if (duration <= TimeSpan.Zero || from == to)
        {
            BeginAnimation(PresenceProperty, null);
            completed?.Invoke();
            return;
        }
        var animation = new DoubleAnimation(from, to, duration) { EasingFunction = TryFindResource(easingKey) as IEasingFunction, FillBehavior = FillBehavior.Stop };
        if (completed is not null) animation.Completed += (_, _) => completed();
        BeginAnimation(PresenceProperty, animation);
    }
}

/// <summary>Tag 的部件样式键（图标、圆点同 <see cref="BadgeParts"/>）。</summary>
public static class TagParts
{
    /// <summary>移除按钮（TS 的 propsRemove）。</summary>
    public static readonly PartKey Remove = new(typeof(Tag), nameof(Remove), typeof(CloseButton));
}
