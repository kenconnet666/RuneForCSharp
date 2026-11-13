using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
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
/// 字号与颜色同标签，悬停的底色是标签文字色的 14%、按下的波纹 22%（彩色标签上协调，不用幽灵按钮的灰色），读屏名称是"移除 + 标签文字"（<see cref="RemoveLabel"/> 能改）。点它时标签先淡出缩小到 0.8（durations.sm、exit），
/// 离场结束才发 RemoveRequested。使用方移除了标签时，原来在移除按钮上的焦点交给同一容器里下一个（没有就上一个）能聚焦的元素；
/// 没移除（如拒绝删除）时淡入复原。部件名：<see cref="TagParts"/>。
/// 可选中（筛选条件的一排标签）：写了 <see cref="IsSelected"/> 时整个标签是切换按钮（读屏念"已按下 / 未按下"），点击、空格、Enter 切换；
/// 没选中时是 surface（有描边），选中时 solid 实底并在前面加一个对勾（不只靠颜色）；不写色调时 accent、不写字号时 sm（点击区域够大）。
/// 悬停时叠一层 8% 的文字色（状态层：各色调、各样式都适用，亮色里变深、暗色里变亮）。
/// 可选中的标签不带移除按钮。一排放在有名称的容器里（AutomationProperties.Name）。
/// <code>
/// &lt;rune:Tag IsSelected="{Binding Urgent}"&gt;加急&lt;/rune:Tag&gt;
/// </code>
/// </summary>
public class Tag : Badge
{
    /// <summary>移除按钮的读屏名称；不写时是"移除"加上标签文字（内容是文字时）。</summary>
    public static readonly StyledProperty<string?> RemoveLabelProperty = AvaloniaProperty.Register<Tag, string?>(nameof(RemoveLabel));

    /// <summary>可选中：null 时是普通标签；true / false 时整个标签是切换按钮（默认双向绑定），点一下切换。</summary>
    public static readonly StyledProperty<bool?> IsSelectedProperty = AvaloniaProperty.Register<Tag, bool?>(nameof(IsSelected), defaultBindingMode: BindingMode.TwoWay);

    static readonly StyledProperty<IBrush?> FocusRingProperty = AvaloniaProperty.Register<Tag, IBrush?>("FocusRing");

    /// <summary>出现的程度：1 是完全显示，0 是离场的终点（透明、缩到 0.8）。</summary>
    static readonly StyledProperty<double> PresenceProperty = AvaloniaProperty.Register<Tag, double>("Presence", 1);

    EventHandler? removeRequested;
    CloseButton? remove;
    readonly List<IDisposable> selectable = [];
    readonly Dismissal dismissal;

    static Tag() => AffectsRender<Tag>(FocusRingProperty, IsFocusedProperty, IsPointerOverProperty);

    /// <summary>新建：焦点环的颜色绑到主题；移除时先离场（Dismissal）。</summary>
    public Tag()
    {
        new TemplateResources(this).Bind(FocusRingProperty, "Rune.Brush.FocusRing");
        dismissal = new Dismissal(this, PresenceProperty, () => removeRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>可选中。</summary>
    public bool? IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
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
    public bool IsDismissing => dismissal.IsDismissing;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty) SyncSelectable();
        else if (change.Property == ContentProperty || change.Property == RemoveLabelProperty) UpdateRemoveLabel();
        else if (change.Property == FontSizeProperty) UpdateRemoveSize();
        else if (change.Property == PresenceProperty)
        {
            var presence = change.GetNewValue<double>();
            Opacity = presence;
            RenderTransform = presence >= 1 ? null : new ScaleTransform(0.8 + 0.2 * presence, 0.8 + 0.2 * presence);
        }
    }

    /// <summary>
    /// 可选中时的外观与交互：没写的色调 accent、字号 sm、样式 surface（模板优先级，写了的照用）；选中时样式 solid、前面一个对勾
    /// （动画优先级：盖过写了的样式与图标，同上游选中时一律实底）。能聚焦、手形指针；不带移除按钮。
    /// </summary>
    void SyncSelectable()
    {
        foreach (var value in selectable) value.Dispose();
        selectable.Clear();
        var selected = IsSelected;
        Focusable = selected is not null;
        Cursor = selected is null ? null : new Cursor(StandardCursorType.Hand);
        SyncRemove();
        if (selected is null) return;
        selectable.AddRange(new[]
        {
            SetValue(ToneProperty, StatusTone.Accent, BindingPriority.Template),
            SetValue(SizeProperty, TextSize.Sm, BindingPriority.Template),
            SetValue(VariantProperty, StatusVariant.Surface, BindingPriority.Template),
            selected == true ? SetValue(VariantProperty, StatusVariant.Solid, BindingPriority.Animation) : null,
            selected == true ? SetValue(IconProperty, Lucide.Check, BindingPriority.Animation) : null,
        }.OfType<IDisposable>());
    }

    /// <summary>切换选中（可选中时）。</summary>
    void ToggleSelected()
    {
        if (IsSelected is { } selected && IsEffectivelyEnabled) SetCurrentValue(IsSelectedProperty, !selected);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsSelected is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (IsSelected is null || e.InitialPressMouseButton != MouseButton.Left || !new Rect(Bounds.Size).Contains(e.GetPosition(this))) return;
        e.Handled = true;
        ToggleSelected();
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

    /// <summary>可选中时：悬停叠一层 8% 的文字色（画在底色上、文字下，同上游的 background-image）；键盘聚焦画焦点环（2px focusRing，压在边上，同按钮）。</summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (IsSelected is null) return;
        if (IsPointerOver && Foreground is ISolidColorBrush fore)
        {
            var box = new Rect(Bounds.Size);
            var corner = CornerRadius(box.Height);
            context.DrawRectangle(new SolidColorBrush(fore.Color, 0.08 * fore.Opacity), null, box, corner, corner);
        }
        if (!Classes.Contains(":focus-visible") || GetValue(FocusRingProperty) is not { } ring) return;
        var bounds = new Rect(Bounds.Size).Deflate(1);
        var radius = CornerRadius(bounds.Height);
        context.DrawRectangle(null, new Pen(ring, 2), bounds, radius, radius);
    }

    /// <summary>可选中时读屏是切换按钮（名称取文字）；否则同 Badge，直接读里面的文字。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new TagAutomationPeer(this);

    sealed class TagAutomationPeer(Tag owner) : ControlAutomationPeer(owner), IToggleProvider, IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.IsSelected is null ? AutomationControlType.Text : AutomationControlType.Button;

        protected override bool IsControlElementCore() => owner.IsSelected is not null;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Content as string;

        public ToggleState ToggleState => owner.IsSelected == true ? ToggleState.On : ToggleState.Off;

        public void Toggle() => owner.ToggleSelected();

        public void Invoke() => owner.ToggleSelected();
    }

    /// <summary>有订阅时放上移除按钮，没有时拿掉（可选中的标签不带）。</summary>
    void SyncRemove()
    {
        if (removeRequested is null || IsSelected is not null)
        {
            Trailing = remove = null;
            return;
        }
        if (remove is not null) return;
        remove = new CloseButton { Name = TagParts.Remove, Radius = Radius.Full, VerticalAlignment = VerticalAlignment.Center, Focusable = RemoveFocusable };
        // 颜色、字号同标签（上游 color: inherit，按钮不按自己的字号档位放大）
        remove.Bind(ForegroundProperty, this.GetObservable(ForegroundProperty));
        remove.PropertyChanged += (_, e) => { if (e.Property == ForegroundProperty) UpdateRemovePalette(); };
        UpdateRemovePalette();
        remove.Click += (_, _) => dismissal.Dismiss();
        Trailing = remove;
        UpdateRemoveSize();
        UpdateRemoveLabel();
    }

    /// <summary>
    /// 移除按钮的调色板换成标签文字色的：幽灵按钮主题的悬停底色（SubtleHover）是它的 14%，不用灰色；按下的波纹本来就是文字色的 22%。
    /// </summary>
    void UpdateRemovePalette()
    {
        if (remove?.Foreground is not ISolidColorBrush fore) return;
        remove.Palette = new TonePalette { Text = fore, SubtleHover = new SolidColorBrush(fore.Color, 0.14 * fore.Opacity) };
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
}

/// <summary>Tag 的部件名（图标、圆点同 <see cref="BadgeParts"/>）：样式用子选择器 <c>rune|Tag &gt; rune|CloseButton#PART_Remove</c>。</summary>
public static class TagParts
{
    /// <summary>移除按钮（TS 的 propsRemove）。</summary>
    public const string Remove = "PART_Remove";
}
