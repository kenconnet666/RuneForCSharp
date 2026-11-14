using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 没有外观的按钮（vue-tsx-pro 的 ButtonBase）：焦点环、按下的波纹、颜色过渡、加载中拦下点击，外观由使用方的样式给出。
/// 菜单项、标签页、折叠标题这类"长得不像按钮的按钮"都建在它上面；要按钮外观时用 <see cref="Button"/>、<see cref="IconButton"/>。
/// <code>
/// &lt;rune:ButtonBase Padding="12" CornerRadius="6" Background="{DynamicResource Rune.Brush.Surface}"&gt;……&lt;/rune:ButtonBase&gt;
/// </code>
/// 从平台的 Button 派生：Click、Command、键盘、读屏的 Invoke 都现成。效果同 TS：按下有波纹（<see cref="Wpf.Ripple"/>，文字颜色的 22%），
/// 背景色过渡（模板里的 <see cref="BrushTransition"/>），键盘聚焦时外面一圈焦点环（<see cref="IsFocusVisible"/>）。
/// 模板里的 PART_Ripples（Canvas）与 PART_FocusRing（Border）有就生效，换模板时照着放。
/// </summary>
[TemplatePart(Name = "PART_Ripples", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_FocusRing", Type = typeof(Border))]
public class ButtonBase : System.Windows.Controls.Button
{
    /// <summary>加载中：点击无效（Command 也不执行），焦点保留。加载指示由使用方放进内容。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(ButtonBase));

    /// <summary>
    /// 不可用（IsEnabled=False）时仍可聚焦：留在 Tab 顺序里、读屏念"不可用"，点击、命令、波纹都无效，外观同不可用
    /// （TS 的 focusableWhenDisabled，aria-disabled）。一组键盘导航里不希望焦点跳过它时用。
    /// </summary>
    public static readonly DependencyProperty FocusableWhenDisabledProperty = DependencyProperty.Register(
        nameof(FocusableWhenDisabled), typeof(bool), typeof(ButtonBase), new FrameworkPropertyMetadata(false, (d, _) => d.CoerceValue(IsEnabledProperty)));

    static readonly DependencyPropertyKey IsDisabledKey = DependencyProperty.RegisterReadOnly(nameof(IsDisabled), typeof(bool), typeof(ButtonBase), new PropertyMetadata(false));

    /// <summary>不可用：写了 IsEnabled=False（含可聚焦的不可用），或外面不可用、命令不能执行。主题的不可用外观、悬停都看它。</summary>
    public static readonly DependencyProperty IsDisabledProperty = IsDisabledKey.DependencyProperty;

    /// <summary>按下时的波纹；不写时沿用 <see cref="Wpf.Ripple.Enabled"/>（默认开启）。</summary>
    public static readonly DependencyProperty RippleProperty = DependencyProperty.Register(nameof(Ripple), typeof(bool?), typeof(ButtonBase));

    /// <summary>圆角（按钮家族的主题按 Radius 设 em 系数，EmSize 算出实际值、按大小收回过大的圆角）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(ButtonBase),
        new FrameworkPropertyMetadata(default(CornerRadius), (d, _) => ((ButtonBase)d).UpdateClip(), EmSize.CoerceCornerRadius));

    static readonly DependencyPropertyKey IsFocusVisibleKey = DependencyProperty.RegisterReadOnly(nameof(IsFocusVisible), typeof(bool), typeof(ButtonBase), new PropertyMetadata(false));

    /// <summary>键盘聚焦（同 CSS 的 :focus-visible）：主题据此显示焦点环。指针点出来的焦点不显示。</summary>
    public static readonly DependencyProperty IsFocusVisibleProperty = IsFocusVisibleKey.DependencyProperty;

    /// <summary>焦点环比外框大出的距离：outline 宽 2、偏移 2（borderWidths.sm）。</summary>
    private protected virtual double FocusRingOutset => 4;

    Canvas? ripples;
    Border? focusRing;
    Action? releasePointerRipple;
    Action? releaseKeyRipple;

    static ButtonBase()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonBase), new FrameworkPropertyMetadata(typeof(ButtonBase)));
        EmSize.Register(typeof(ButtonBase), CornerRadiusProperty);
        IsEnabledProperty.OverrideMetadata(typeof(ButtonBase), new UIPropertyMetadata(true, null, CoerceIsEnabled));
    }

    /// <summary>新建按钮。</summary>
    public ButtonBase() => SizeChanged += (_, _) => UpdateClip();

    /// <summary>加载中。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>按下时的波纹。</summary>
    public bool? Ripple { get => (bool?)GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    /// <summary>圆角。</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>键盘聚焦。</summary>
    [Browsable(false)]
    public bool IsFocusVisible => (bool)GetValue(IsFocusVisibleProperty);

    /// <summary>不可用时仍可聚焦。</summary>
    public bool FocusableWhenDisabled { get => (bool)GetValue(FocusableWhenDisabledProperty); set => SetValue(FocusableWhenDisabledProperty, value); }

    /// <summary>不可用（含可聚焦的不可用）。</summary>
    [Browsable(false)]
    public bool IsDisabled => (bool)GetValue(IsDisabledProperty);

    /// <summary>
    /// 代替 UIElement 的 IsEnabled 强制：外面不可用、命令不能执行时不可用（同平台）；自己写了 False 时，可聚焦的不可用仍保持可用
    /// （能聚焦、收键盘），另记下 IsDisabled，点击在 OnClick 里拦下。
    /// </summary>
    static object CoerceIsEnabled(DependencyObject d, object baseValue)
    {
        var button = (ButtonBase)d;
        var parent = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d);
        var inherited = parent is not UIElement { IsEnabled: false } && parent is not ContentElement { IsEnabled: false } && button.IsEnabledCore;
        var requested = (bool)baseValue;
        button.SetValue(IsDisabledKey, !(requested && inherited));
        return inherited && (requested || button.FocusableWhenDisabled);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ripples = GetTemplateChild("PART_Ripples") as Canvas;
        focusRing = GetTemplateChild("PART_FocusRing") as Border;
        UpdateClip();
    }

    /// <summary>加载中点击无效（焦点保留）；Command 也不执行。</summary>
    protected override void OnClick()
    {
        if (!IsLoading && !IsDisabled) base.OnClick();
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (ripples is not null && RippleOn)
        {
            releasePointerRipple?.Invoke();
            releasePointerRipple = Wpf.Ripple.Start(ripples, e.GetPosition(ripples), Foreground);
        }
        base.OnMouseLeftButtonDown(e);
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleasePointerRipple();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ReleasePointerRipple();
    }

    /// <inheritdoc />
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        ReleasePointerRipple();
    }

    /// <summary>Enter、空格从中心扩散；按住不放的重复按键不再出新的波纹。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (ripples is not null && RippleOn && !e.IsRepeat && releaseKeyRipple is null && e.Key is Key.Enter or Key.Space)
            releaseKeyRipple = Wpf.Ripple.Start(ripples, new Point(ripples.ActualWidth / 2, ripples.ActualHeight / 2), Foreground);
        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        ReleaseKeyRipple();
    }

    /// <summary>焦点环只在键盘聚焦时显示（指针按下得到的焦点不显示，同 :focus-visible）。</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        SetValue(IsFocusVisibleKey, InputManager.Current.MostRecentInputDevice is KeyboardDevice);
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        SetValue(IsFocusVisibleKey, false);
        ReleaseKeyRipple();
    }

    /// <summary>不可用、加载中不出波纹。</summary>
    bool RippleOn => (Ripple ?? Wpf.Ripple.Enabled) && !IsDisabled && !IsLoading;

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

    /// <summary>波纹层裁成按钮的圆角；焦点环的圆角 = 外框的圆角 + 外扩的距离（同 CSS outline 跟着 border-radius 外扩）。</summary>
    void UpdateClip()
    {
        var r = CornerRadius.TopLeft;
        if (ripples is not null) ripples.Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), r, r);
        if (focusRing is not null)
        {
            focusRing.CornerRadius = new CornerRadius(r + FocusRingOutset);
            focusRing.Margin = new Thickness(-FocusRingOutset);
        }
    }
}

/// <summary>
/// 按钮家族的读屏对等体：可聚焦的不可用时报"不可用"（Invoke 也就无效）；加载中时状态（ItemStatus）是语言包的"加载中"
/// （TS 的 aria-busy：UIA 的按钮没有忙碌状态，读屏念 ItemStatus）。
/// </summary>
/// <param name="owner">按钮。</param>
public class ButtonBaseAutomationPeer(ButtonBase owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
{
    /// <inheritdoc />
    protected override bool IsEnabledCore() => !owner.IsDisabled;

    bool? Expanded => (bool?)owner.GetValue(Popover.TriggerExpandedProperty);

    /// <summary>弹出框、菜单的触发器（TS 的 aria-expanded、aria-haspopup）：能展开、收起；别的按钮没有这个模式。</summary>
    /// <param name="patternInterface">模式。</param>
    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.ExpandCollapse && Expanded is not null ? this : base.GetPattern(patternInterface);

    ExpandCollapseState IExpandCollapseProvider.ExpandCollapseState => Expanded == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    void IExpandCollapseProvider.Expand()
    {
        if (Expanded == false) ((System.Windows.Automation.Provider.IInvokeProvider)this).Invoke();
    }

    void IExpandCollapseProvider.Collapse()
    {
        if (Expanded == true) ((System.Windows.Automation.Provider.IInvokeProvider)this).Invoke();
    }

    /// <inheritdoc />
    protected override string GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();
}
