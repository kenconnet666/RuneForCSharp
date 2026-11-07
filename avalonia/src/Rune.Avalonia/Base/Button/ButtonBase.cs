using global::Avalonia;
using global::Avalonia.Automation.Peers;
using global::Avalonia.Animation;
using global::Avalonia.Animation.Easings;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;

namespace Rune.Avalonia;

/// <summary>
/// 没有外观的按钮（vue-tsx-pro 的 ButtonBase）：焦点环、按下的波纹、颜色过渡、加载中拦下点击，外观由使用方的样式给出。
/// 菜单项、标签页、折叠标题这类"长得不像按钮的按钮"都建在它上面；要按钮外观时用 <see cref="Button"/>、<see cref="IconButton"/>。
/// <code>
/// &lt;rune:ButtonBase Padding="12" CornerRadius="6" Background="{DynamicResource Rune.Brush.Surface}"&gt;……&lt;/rune:ButtonBase&gt;
/// </code>
/// 从平台的 Button 派生：Click、Command、键盘、读屏的 Invoke 都现成。效果同 TS：按下有波纹（<see cref="Avalonia.Ripple"/>，
/// 文字颜色的 22%），背景、描边、文字颜色在 durations.sm 内过渡，键盘聚焦时外面一圈焦点环（<c>:focus-visible</c>）。
/// 模板里的 <see cref="ButtonBaseParts.Ripples"/>（Canvas）与 <see cref="ButtonBaseParts.FocusRing"/>（Border）有就生效，换模板时照着放。
/// </summary>
public class ButtonBase : global::Avalonia.Controls.Button
{
    /// <summary>加载中：点击无效（Command 也不执行），焦点保留；伪类 <c>:loading</c>。加载指示由使用方放进内容。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<ButtonBase, bool>(nameof(IsLoading));

    /// <summary>
    /// 不可用（IsEnabled=False）时仍可聚焦：留在 Tab 顺序里、读屏念"不可用"，点击、命令、波纹都无效，外观同不可用
    /// （TS 的 focusableWhenDisabled，aria-disabled）。一组键盘导航里不希望焦点跳过它时用，比如旁边要用 Tooltip 说明为什么不能点。
    /// </summary>
    public static readonly StyledProperty<bool> FocusableWhenDisabledProperty = AvaloniaProperty.Register<ButtonBase, bool>(nameof(FocusableWhenDisabled));

    /// <summary>按下时的波纹；不写时沿用 <see cref="Avalonia.Ripple.Enabled"/>（默认开启）。</summary>
    public static readonly StyledProperty<bool?> RippleProperty = AvaloniaProperty.Register<ButtonBase, bool?>(nameof(Ripple));

    /// <summary>焦点环比外框大出的距离：outline 宽 2、偏移 2（borderWidths.sm）。</summary>
    const double FocusRingOutset = 4;

    static readonly Easing Linear = new LinearEasing();

    /// <summary>按（时长、缓动）共用的颜色过渡：主题与系统动画设置的组合只有几种。</summary>
    static readonly Dictionary<(TimeSpan Duration, Easing Easing), Transitions> SharedTransitions = [];

    Canvas? ripples;
    Border? focusRing;
    Action? releasePointerRipple;
    Action? releaseKeyRipple;

    /// <summary>新建按钮：颜色在 durations.sm 内过渡。</summary>
    public ButtonBase() => AttachedToVisualTree += (_, _) => Transitions = ColorTransitions();

    /// <summary>加载中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>按下时的波纹。</summary>
    public bool? Ripple { get => GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    /// <summary>不可用时仍可聚焦。</summary>
    public bool FocusableWhenDisabled { get => GetValue(FocusableWhenDisabledProperty); set => SetValue(FocusableWhenDisabledProperty, value); }

    /// <summary>不可用：自己写了 IsEnabled=False，或外面不可用。可聚焦的不可用时 IsEffectivelyEnabled 仍为 true，以它为准判断能不能点。</summary>
    public bool IsDisabled => !IsEnabled || !IsEffectivelyEnabled;

    /// <summary>可聚焦的不可用时照样算"可用"（能聚焦、收键盘），点击另外拦下。</summary>
    protected override bool IsEnabledCore => IsEnabled || FocusableWhenDisabled;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ripples = e.NameScope.Find<Canvas>(ButtonBaseParts.Ripples);
        focusRing = e.NameScope.Find<Border>(ButtonBaseParts.FocusRing);
        UpdateFocusRing();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CornerRadiusProperty) UpdateFocusRing();
        else if (change.Property == IsLoadingProperty) PseudoClasses.Set(":loading", IsLoading);
        else if (change.Property == FocusableWhenDisabledProperty) UpdateIsEffectivelyEnabled();
        // 可聚焦的不可用另用伪类 :soft-disabled（TS 的 aria-disabled）：平台每次更新 IsEffectivelyEnabled 都会按它重设 :disabled
        if (change.Property == IsEnabledProperty || change.Property == FocusableWhenDisabledProperty)
            PseudoClasses.Set(":soft-disabled", FocusableWhenDisabled && !IsEnabled);
    }

    /// <summary>加载中点击无效（焦点保留）；Command 也不执行。</summary>
    protected override void OnClick()
    {
        if (!IsLoading && !IsDisabled) base.OnClick();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (ripples is null || !RippleOn || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        releasePointerRipple?.Invoke();
        releasePointerRipple = Avalonia.Ripple.Start(ripples, e.GetPosition(ripples), Foreground);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        ReleasePointerRipple();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        ReleasePointerRipple();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ReleasePointerRipple();
    }

    /// <summary>Enter、空格从中心扩散；按住不放的重复按键不再出新的波纹。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (ripples is not null && RippleOn && releaseKeyRipple is null && e.Key is Key.Enter or Key.Space)
            releaseKeyRipple = Avalonia.Ripple.Start(ripples, new Point(ripples.Bounds.Width / 2, ripples.Bounds.Height / 2), Foreground);
        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        ReleaseKeyRipple();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        ReleaseKeyRipple();
    }

    /// <summary>不可用、加载中不出波纹。</summary>
    bool RippleOn => (Ripple ?? Avalonia.Ripple.Enabled) && !IsDisabled && !IsLoading;

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonBaseAutomationPeer(this);

    void ReleasePointerRipple()
    {
        releasePointerRipple?.Invoke();
        releasePointerRipple = null;
    }

    void ReleaseKeyRipple()
    {
        releaseKeyRipple?.Invoke();
        releaseKeyRipple = null;
    }

    /// <summary>焦点环的圆角 = 外框的圆角 + 外扩的距离（同 CSS outline 跟着 border-radius 外扩）。</summary>
    void UpdateFocusRing()
    {
        if (focusRing is null) return;
        var r = CornerRadius;
        focusRing.CornerRadius = new CornerRadius(r.TopLeft + FocusRingOutset, r.TopRight + FocusRingOutset, r.BottomRight + FocusRingOutset, r.BottomLeft + FocusRingOutset);
        focusRing.Margin = new Thickness(-FocusRingOutset);
    }

    /// <summary>
    /// 背景、描边、文字颜色在 durations.sm 内过渡（vue-tsx-pro 的 interactionTransitions.colors）；系统关闭动画时不过渡。
    /// 同样的时长与缓动共用一份（过渡不存每个控件的状态，Fluent 主题也是一份 Transitions 给所有按钮），不每个按钮各建三个。
    /// </summary>
    Transitions ColorTransitions()
    {
        var duration = Motion.Duration(this, "Rune.Duration.Sm", TimeSpan.FromMilliseconds(120));
        var easing = this.Resource<Easing>("Rune.Easing.Standard", Linear);
        lock (SharedTransitions)
        {
            if (SharedTransitions.TryGetValue((duration, easing), out var shared)) return shared;
            return SharedTransitions[(duration, easing)] =
            [
                new BrushTransition { Property = BackgroundProperty, Duration = duration, Easing = easing },
                new BrushTransition { Property = BorderBrushProperty, Duration = duration, Easing = easing },
                new BrushTransition { Property = ForegroundProperty, Duration = duration, Easing = easing },
            ];
        }
    }
}

/// <summary>按钮家族共有的模板部件名：<see cref="ButtonBase"/> 按名字找到它们，换模板时照着放。</summary>
public static class ButtonBaseParts
{
    /// <summary>波纹层（Canvas，叠在内容下面、不接受命中测试）。</summary>
    public const string Ripples = "PART_Ripples";

    /// <summary>键盘聚焦时的焦点环（Border，主题在 <c>:focus-visible</c> 时显示）。</summary>
    public const string FocusRing = "PART_FocusRing";
}

/// <summary>
/// 按钮家族的读屏对等体：可聚焦的不可用时报"不可用"（Invoke 也就无效）；加载中时状态（ItemStatus）是语言包的"加载中"
/// （TS 的 aria-busy：UIA 的按钮没有忙碌状态，读屏念 ItemStatus）。
/// </summary>
/// <param name="owner">按钮。</param>
public class ButtonBaseAutomationPeer(ButtonBase owner) : ButtonAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override bool IsEnabledCore() => !owner.IsDisabled;

    /// <inheritdoc />
    protected override string? GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();
}
