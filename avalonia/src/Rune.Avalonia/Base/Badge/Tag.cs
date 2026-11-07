using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 标签（vue-tsx-pro 的 Tag）：外观同 <see cref="Badge"/>（样式、色调、字号、圆角、图标，作用域里的 Badge 默认值一起改），可以带移除按钮
/// （筛选条件、已选项、输入的关键词）。
/// <code>
/// &lt;rune:Tag Icon="{x:Static rune:Lucide.Code}"&gt;TypeScript&lt;/rune:Tag&gt;
/// &lt;rune:Tag Tone="accent" Radius="full" RemoveRequested="OnRemoveFilter"&gt;品牌：小米&lt;/rune:Tag&gt;
/// </code>
/// 订阅了 <see cref="RemoveRequested"/> 才有移除按钮（上游给了 onRemove 才有）：文字后面一个圆形的 <see cref="CloseButton"/>，宽 1.5em、
/// 字号与颜色同标签，读屏名称是"移除 + 标签文字"（<see cref="RemoveLabel"/> 能改）。点它时标签先淡出缩小到 0.8（durations.sm、exit），
/// 离场结束才发 RemoveRequested。使用方移除了标签时，原来在移除按钮上的焦点交给同一容器里下一个（没有就上一个）能聚焦的元素；
/// 没移除（如拒绝删除）时淡入复原。部件名：<see cref="TagParts"/>。
/// </summary>
public class Tag : Badge
{
    /// <summary>移除按钮的读屏名称；不写时是"移除"加上标签文字（内容是文字时）。</summary>
    public static readonly StyledProperty<string?> RemoveLabelProperty = AvaloniaProperty.Register<Tag, string?>(nameof(RemoveLabel));

    /// <summary>出现的程度：1 是完全显示，0 是离场的终点（透明、缩到 0.8）。</summary>
    static readonly StyledProperty<double> PresenceProperty = AvaloniaProperty.Register<Tag, double>("Presence", 1);

    EventHandler? removeRequested;
    CloseButton? remove;
    CancellationTokenSource? transition;
    bool dismissing;

    /// <summary>移除按钮的读屏名称。</summary>
    public string? RemoveLabel { get => GetValue(RemoveLabelProperty); set => SetValue(RemoveLabelProperty, value); }

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

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == RemoveLabelProperty) UpdateRemoveLabel();
        else if (change.Property == FontSizeProperty) UpdateRemoveSize();
        else if (change.Property == PresenceProperty)
        {
            var presence = change.GetNewValue<double>();
            Opacity = presence;
            RenderTransform = presence >= 1 ? null : new ScaleTransform(0.8 + 0.2 * presence, 0.8 + 0.2 * presence);
        }
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
        remove = new CloseButton { Name = TagParts.Remove, Radius = Radius.Full, VerticalAlignment = VerticalAlignment.Center };
        // 颜色、字号同标签（上游 color: inherit，按钮不按自己的字号档位放大）
        remove.Bind(ForegroundProperty, this.GetObservable(ForegroundProperty));
        remove.Click += (_, _) => Dismiss();
        Trailing = remove;
        UpdateRemoveSize();
        UpdateRemoveLabel();
    }

    /// <summary>宽、最小高 1.5em，上下各让出 0.25em（不把标签撑高）。</summary>
    void UpdateRemoveSize()
    {
        if (remove is null) return;
        var em = FontSize;
        remove.FontSize = em;
        remove.Width = remove.MinHeight = 1.5 * em;
        remove.Margin = new Thickness(0, -0.25 * em);
    }

    void UpdateRemoveLabel()
    {
        if (remove is null) return;
        var lang = Provider.GetLang(this);
        var label = RemoveLabel ?? (Content is string { Length: > 0 } text ? $"{lang.Remove} {text}" : lang.Remove);
        AutomationProperties.SetName(remove, label);
    }

    /// <summary>
    /// 离场：记下焦点在不在移除按钮上与同一容器里的候选，淡出缩小，结束后请使用方移除；等布局之后还在界面上就淡入复原，
    /// 被移除了且焦点丢了就交给候选里第一个还在界面上的。
    /// </summary>
    async void Dismiss()
    {
        if (dismissing) return;
        dismissing = true;
        var candidates = IsKeyboardFocusWithin ? FocusCandidates() : [];
        await Animate(0, "Rune.Easing.Exit", new CubicEaseIn());
        dismissing = false;
        removeRequested?.Invoke(this, EventArgs.Empty);
        Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(this) is not null)
            {
                _ = Animate(1, "Rune.Easing.Enter", new CubicEaseOut());
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
        if (this.FindAncestorOfType<Panel>() is not { } group) return [];
        var following = new List<InputElement>();
        var preceding = new List<InputElement>();
        var after = false;
        foreach (var visual in group.GetVisualDescendants())
        {
            if (visual == this)
            {
                after = true;
                continue;
            }
            if (visual is not InputElement element || this.IsVisualAncestorOf(visual)) continue;
            if (!element.Focusable || !element.IsEffectivelyEnabled || !element.IsEffectivelyVisible || !KeyboardNavigation.GetIsTabStop(element)) continue;
            (after ? following : preceding).Add(element);
        }
        preceding.Reverse();
        return [.. following, .. preceding];
    }

    async Task Animate(double to, string easingKey, Easing fallback)
    {
        transition?.Cancel();
        var from = GetValue(PresenceProperty);
        var duration = VisualRoot is null ? TimeSpan.Zero : Motion.Duration(this, "Rune.Duration.Sm", TimeSpan.FromMilliseconds(120));
        if (duration <= TimeSpan.Zero || from == to)
        {
            SetValue(PresenceProperty, to);
            return;
        }
        var cancel = transition = new CancellationTokenSource();
        // 先写终值：动画在动画优先级上盖住它，结束或被打断后留下的就是终值
        SetValue(PresenceProperty, to);
        _ = new Animation
        {
            Duration = duration,
            Easing = this.Resource(easingKey, fallback),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(PresenceProperty, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(PresenceProperty, to) } },
            },
        }.RunAsync(this, cancel.Token);
        // 结束按真实时间算（同库里别的延时；headless 的动画时钟不走），到点停掉动画，留下的就是终值
        await Task.Delay(duration);
        cancel.Cancel();
    }
}

/// <summary>Tag 的部件名（图标、圆点同 <see cref="BadgeParts"/>）：样式用子选择器 <c>rune|Tag &gt; rune|CloseButton#PART_Remove</c>。</summary>
public static class TagParts
{
    /// <summary>移除按钮（TS 的 propsRemove）。</summary>
    public const string Remove = "PART_Remove";
}
