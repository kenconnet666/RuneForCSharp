using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 离场后再请使用方移除（vue-tsx-pro 的 useDismissible）：Tag 的移除、Alert 与 Banner 的关闭共用。
/// <see cref="Dismiss"/> 记下焦点在不在组件里与同一容器里的候选，把"出现的程度"（<paramref name="presence"/>，1 完全显示、0 离场的终点）
/// 过渡到 0（durations.sm、exit），结束才调 <paramref name="requested"/>；布局之后组件还在界面上（使用方没移除）就淡入复原，
/// 被移除了且焦点丢了就交给候选里第一个还在界面上的。系统关闭动画、还没加载时直接到位。
/// </summary>
/// <param name="owner">要离场的组件。</param>
/// <param name="presence">出现的程度：组件按它画（透明度，或再加缩放）。</param>
/// <param name="requested">离场结束：请使用方移除。</param>
sealed class Dismissal(FrameworkElement owner, DependencyProperty presence, Action requested)
{
    /// <summary>正在离场。</summary>
    public bool IsDismissing { get; private set; }

    /// <summary>离场，结束后请使用方移除；使用方没移除时淡入复原。</summary>
    public void Dismiss()
    {
        if (IsDismissing) return;
        IsDismissing = true;
        var candidates = owner.IsKeyboardFocusWithin ? FocusCandidates() : [];
        Animate(0, "Rune.Easing.Exit", () =>
        {
            IsDismissing = false;
            requested();
            owner.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (PresentationSource.FromVisual(owner) is not null)
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
        DependencyObject? group = VisualTreeHelper.GetParent(owner);
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
                if (child == owner)
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
        var from = (double)owner.GetValue(presence);
        // 先写终值：过渡结束（FillBehavior.Stop）或被打断后留下的就是它
        owner.SetValue(presence, to);
        var duration = owner.IsLoaded ? Motion.Duration(owner, "Rune.Duration.Sm", TimeSpan.FromMilliseconds(120)) : TimeSpan.Zero;
        if (duration <= TimeSpan.Zero || from == to)
        {
            owner.BeginAnimation(presence, null);
            completed?.Invoke();
            return;
        }
        var animation = new DoubleAnimation(from, to, duration) { EasingFunction = owner.TryFindResource(easingKey) as IEasingFunction, FillBehavior = FillBehavior.Stop };
        if (completed is not null) animation.Completed += (_, _) => completed();
        owner.BeginAnimation(presence, animation);
    }
}
