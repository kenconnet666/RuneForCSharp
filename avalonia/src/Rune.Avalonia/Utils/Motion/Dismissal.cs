using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 离场后再请使用方移除（vue-tsx-pro 的 useDismissible）：Tag 的移除、Alert 与 Banner 的关闭共用。
/// <see cref="Dismiss"/> 记下焦点在不在组件里与同一容器里的候选，把"出现的程度"（<paramref name="presence"/>，1 完全显示、0 离场的终点）
/// 动画到 0（durations.sm、exit），结束才调 <paramref name="requested"/>；等布局之后组件还在界面上（使用方没移除）就淡入复原，
/// 被移除了且焦点丢了就交给候选里第一个还在界面上的。系统关闭动画时直接到位。
/// </summary>
/// <param name="owner">要离场的组件。</param>
/// <param name="presence">出现的程度：组件按它画（透明度，或再加缩放）。</param>
/// <param name="requested">离场结束：请使用方移除。</param>
sealed class Dismissal(Control owner, StyledProperty<double> presence, Action requested)
{
    CancellationTokenSource? transition;

    /// <summary>正在离场。</summary>
    public bool IsDismissing { get; private set; }

    /// <summary>离场，结束后请使用方移除；使用方没移除时淡入复原。</summary>
    public async void Dismiss()
    {
        if (IsDismissing) return;
        IsDismissing = true;
        var candidates = owner.IsKeyboardFocusWithin ? FocusCandidates() : [];
        await Animate(0, ThemeKeys.Easing.Exit);
        IsDismissing = false;
        requested();
        Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(owner) is not null)
            {
                _ = Animate(1, ThemeKeys.Easing.Enter);
                return;
            }
            if (candidates.Count == 0 || TopLevel.GetTopLevel(candidates[0]) is not { } top) return;
            if (top.FocusManager?.GetFocusedElement() is Visual focused && TopLevel.GetTopLevel(focused) is not null && focused is not TopLevel) return;
            candidates.FirstOrDefault(candidate => TopLevel.GetTopLevel(candidate) is not null)?.Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>同一容器（最近的面板：Flex、ItemsControl 的项面板）里别的能 Tab 聚焦的元素：后面的在前，前面的倒序接在后面。</summary>
    List<InputElement> FocusCandidates()
    {
        if (owner.FindAncestorOfType<Panel>() is not { } group) return [];
        var following = new List<InputElement>();
        var preceding = new List<InputElement>();
        var after = false;
        foreach (var visual in group.GetVisualDescendants())
        {
            if (visual == owner)
            {
                after = true;
                continue;
            }
            if (visual is not InputElement element || owner.IsVisualAncestorOf(visual)) continue;
            if (!element.Focusable || !element.IsEffectivelyEnabled || !element.IsEffectivelyVisible || !KeyboardNavigation.GetIsTabStop(element)) continue;
            (after ? following : preceding).Add(element);
        }
        preceding.Reverse();
        return [.. following, .. preceding];
    }

    async Task Animate(double to, string easingKey)
    {
        transition?.Cancel();
        var from = owner.GetValue(presence);
        var duration = TopLevel.GetTopLevel(owner) is null ? TimeSpan.Zero : Motion.Duration(owner, ThemeKeys.Duration.Sm);
        if (duration <= TimeSpan.Zero || from == to)
        {
            owner.SetValue(presence, to);
            return;
        }
        var cancel = transition = new CancellationTokenSource();
        // 先写终值：动画在动画优先级上盖住它，结束或被打断后留下的就是终值
        owner.SetValue(presence, to);
        _ = new Animation
        {
            Duration = duration,
            Easing = owner.Resource<Easing>(easingKey),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(presence, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(presence, to) } },
            },
        }.RunAsync(owner, cancel.Token);
        // 结束按真实时间算（同库里别的延时；headless 的动画时钟不走），到点停掉动画，留下的就是终值
        await Task.Delay(duration);
        cancel.Cancel();
    }
}
