using System.Windows;
using System.Windows.Automation;
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
/// 字号与颜色同标签，读屏名称是"移除 + 标签文字"（<see cref="RemoveLabel"/> 能改）。点它时标签先淡出缩小到 0.8（durations.sm、exit），
/// 离场结束才发 RemoveRequested。使用方移除了标签时，原来在移除按钮上的焦点交给同一容器里下一个（没有就上一个）能聚焦的元素；
/// 没移除（如拒绝删除）时淡入复原。部件样式键：<see cref="TagParts"/>。
/// </summary>
public class Tag : Badge
{
    /// <summary>移除按钮的读屏名称；不写时是"移除"加上标签文字（内容是文字时）。</summary>
    public static readonly DependencyProperty RemoveLabelProperty = DependencyProperty.Register(nameof(RemoveLabel), typeof(string), typeof(Tag),
        new FrameworkPropertyMetadata(null, (d, _) => ((Tag)d).UpdateRemoveLabel()));

    /// <summary>出现的程度：1 是完全显示，0 是离场的终点（透明、缩到 0.8）。</summary>
    static readonly DependencyProperty PresenceProperty = DependencyProperty.Register("Presence", typeof(double), typeof(Tag),
        new FrameworkPropertyMetadata(1d, (d, e) => ((Tag)d).ApplyPresence((double)e.NewValue)));

    EventHandler? removeRequested;
    CloseButton? remove;
    bool dismissing;

    static Tag()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Tag), new FrameworkPropertyMetadata(typeof(Tag)));
        ContentProperty.OverrideMetadata(typeof(Tag), new FrameworkPropertyMetadata((d, _) => ((Tag)d).UpdateRemoveLabel()));
        FontSizeProperty.OverrideMetadata(typeof(Tag), new FrameworkPropertyMetadata((d, _) => ((Tag)d).UpdateRemoveSize()));
    }

    /// <summary>新建：变形以中心为原点（缩小时向中间收）。</summary>
    public Tag() => RenderTransformOrigin = new Point(0.5, 0.5);

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
        if (removeRequested is null)
        {
            Trailing = remove = null;
            return;
        }
        if (remove is not null) return;
        remove = new CloseButton { Radius = Radius.Full, VerticalAlignment = VerticalAlignment.Center };
        remove.SetResourceReference(StyleProperty, TagParts.Remove);
        // 颜色同标签（上游 color: inherit）
        remove.SetBinding(ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
        remove.Click += (_, _) => Dismiss();
        Trailing = remove;
        UpdateRemoveSize();
        UpdateRemoveLabel();
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
