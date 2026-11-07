using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 开关（vue-tsx-pro 的 Switch）：立即生效的开 / 关设置，如"接收通知"；要提交才生效的选项用 Checkbox。
/// <code>
/// &lt;rune:Switch IsChecked="{Binding Notify}"&gt;接收通知&lt;/rune:Switch&gt;
/// &lt;rune:Switch IsChecked="{Binding AutoSave}" IsLoading="{Binding Saving}" CheckedContent="开" UncheckedContent="关" /&gt;
/// </code>
/// 轨道 2.25em × 1.25em，滑块 0.875em，在 durations.sm 内滑到另一端（从右到左时 WPF 整体镜像）；轨道里的文字（CheckedContent、
/// UncheckedContent）让轨道随文字加宽，两份取较宽的，切换时宽度不跳。打开时轨道是色调的实底（<see cref="ToneFill"/>），滑块是其上的颜色。
/// 文字在右边，整块可点，高度不低于 24px（WCAG 2.5.8）。空格切换；只读时能聚焦、不能切换；切换中（IsLoading）滑块里转圈（防闪烁）、点击无效。
/// 读屏：Toggle 模式（开 / 关），名称取文字；切换中时项状态是"加载中"。部件样式键见 <see cref="SwitchParts"/>，默认外观在 Switch.xaml。
/// </summary>
[ContentProperty(nameof(Content))]
public class Switch : Control
{
    /// <summary>是否打开（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked), typeof(bool), typeof(Switch), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsCheckedChanged));

    /// <summary>只读：能聚焦，不能切换（查看详情、审批时）；外观照常。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(Switch), new PropertyMetadata(false));

    /// <summary>必填（读屏念"必填"）。</summary>
    public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.Register(
        nameof(IsRequired), typeof(bool), typeof(Switch), new PropertyMetadata(false));

    /// <summary>校验未通过：轨道的描边是危险色。</summary>
    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid), typeof(bool), typeof(Switch), new PropertyMetadata(false));

    /// <summary>切换中（如等接口返回）：滑块里转圈，点击无效。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(Switch), new PropertyMetadata(false, (d, e) => ((Switch)d).OnIsLoadingChanged((bool)e.NewValue)));

    /// <summary>打开时轨道里的文字或图标（如"开"、对勾）。</summary>
    public static readonly DependencyProperty CheckedContentProperty = DependencyProperty.Register(
        nameof(CheckedContent), typeof(object), typeof(Switch), new FrameworkPropertyMetadata(null, OnContentChanged));

    /// <summary>关闭时轨道里的文字或图标。</summary>
    public static readonly DependencyProperty UncheckedContentProperty = DependencyProperty.Register(
        nameof(UncheckedContent), typeof(object), typeof(Switch), new FrameworkPropertyMetadata(null, OnContentChanged));

    /// <summary>文字（读屏名称）。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(Switch), new FrameworkPropertyMetadata(null, OnContentChanged));

    /// <summary>打开时轨道的色调；主题默认 accent。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(Switch), new FrameworkPropertyMetadata(default(StatusTone), (d, _) => ((Switch)d).BindTone()));

    /// <summary>字号档位：轨道、滑块随之缩放；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(Switch), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    /// <summary>色调的实底（TS 的 --tone-fill），按 Tone 取主题资源；部件样式绑它。</summary>
    public static readonly DependencyProperty ToneFillProperty = DependencyProperty.Register(
        nameof(ToneFill), typeof(Brush), typeof(Switch), new PropertyMetadata(null));

    /// <summary>实底上的滑块、文字色（TS 的 --tone-on-fill）。</summary>
    public static readonly DependencyProperty ToneOnFillProperty = DependencyProperty.Register(
        nameof(ToneOnFill), typeof(Brush), typeof(Switch), new PropertyMetadata(null));

    static readonly DependencyPropertyKey IsFocusVisiblePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsFocusVisible), typeof(bool), typeof(Switch), new PropertyMetadata(false));

    /// <summary>焦点环是否显示：只在键盘聚焦时（指针点击得到的焦点不显示，同 :focus-visible）。</summary>
    public static readonly DependencyProperty IsFocusVisibleProperty = IsFocusVisiblePropertyKey.DependencyProperty;

    /// <summary>滑块的位置：0 在起始边，1 在结束边（过渡驱动）。</summary>
    static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        "Position", typeof(double), typeof(Switch), new FrameworkPropertyMetadata(0d, (d, _) => ((Switch)d).control.InvalidateArrange()));

    static readonly DependencyProperty GapFactorProperty = DependencyProperty.Register(
        "GapFactor", typeof(double), typeof(Switch), new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.AffectsMeasure));

    static readonly DependencyProperty MinTargetProperty = DependencyProperty.Register(
        "MinTarget", typeof(double), typeof(Switch), new FrameworkPropertyMetadata(24d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    readonly SwitchStack control;
    readonly Border track = new() { Name = "PART_Track" };
    readonly Border focusRing = new() { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    readonly Border onContent = new();
    readonly Border offContent = new();
    readonly ContentPresenter onText = new();
    readonly ContentPresenter offText = new();
    readonly Border thumb = new() { Name = "PART_Thumb" };
    readonly Spinner spinner = new() { Visibility = Visibility.Collapsed };
    readonly ContentPresenter label = new();
    readonly DelayedLoading loading;
    bool pressed;
    bool spacePressed;

    static Switch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Switch), new FrameworkPropertyMetadata(typeof(Switch)));
        FocusableProperty.OverrideMetadata(typeof(Switch), new FrameworkPropertyMetadata(true));
        FontSizeProperty.OverrideMetadata(typeof(Switch), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((Switch)d).UpdateMetrics()));
        Leading.FactorProperty.OverrideMetadata(typeof(Switch), new FrameworkPropertyMetadata(Leading.Normal, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((Switch)d).UpdateMetrics()));
    }

    /// <summary>新建：关闭；部件的外观引用主题的部件样式，间距、色调绑到主题。</summary>
    public Switch()
    {
        control = new SwitchStack(this) { Name = "PART_Control" };
        control.SetResourceReference(StyleProperty, SwitchParts.Control);
        track.SetResourceReference(StyleProperty, SwitchParts.Track);
        focusRing.SetResourceReference(StyleProperty, SwitchParts.FocusRing);
        foreach (var content in new[] { onContent, offContent }) content.SetResourceReference(StyleProperty, SwitchParts.Content);
        foreach (var text in new[] { onText, offText }) text.SetResourceReference(StyleProperty, SwitchParts.ContentText);
        thumb.SetResourceReference(StyleProperty, SwitchParts.Thumb);
        spinner.SetResourceReference(StyleProperty, SwitchParts.Spinner);
        label.SetResourceReference(StyleProperty, SwitchParts.Label);
        SetResourceReference(GapFactorProperty, "Rune.Space.Sm");
        SetResourceReference(MinTargetProperty, "Rune.Size.MinTarget");
        BindTone();
        loading = new DelayedLoading(shown => spinner.Visibility = shown ? Visibility.Visible : Visibility.Collapsed);
        onContent.Child = onText;
        offContent.Child = offText;
        thumb.Child = spinner;
        foreach (var part in new UIElement[] { track, onContent, offContent, thumb, focusRing }) control.Children.Add(part);
        AddVisualChild(control);
        AddLogicalChild(control);
        AddVisualChild(label);
        AddLogicalChild(label);
        UpdateMetrics();
        Update();
        Unloaded += (_, _) => loading.Stop();
    }

    /// <summary>
    /// 要切换时（点击、空格、读屏）先发：设 Cancel 不改值——切换要等接口时拦下，接口回来再写 IsChecked（TS 的受控 onUpdate:_checked）。
    /// </summary>
    public event EventHandler<SwitchTogglingEventArgs>? Toggling;

    /// <summary>是否打开。</summary>
    public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool IsInvalid { get => (bool)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>切换中。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>打开时轨道里的内容。</summary>
    public object? CheckedContent { get => GetValue(CheckedContentProperty); set => SetValue(CheckedContentProperty, value); }

    /// <summary>关闭时轨道里的内容。</summary>
    public object? UncheckedContent { get => GetValue(UncheckedContentProperty); set => SetValue(UncheckedContentProperty, value); }

    /// <summary>文字。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>色调的实底。</summary>
    public Brush? ToneFill { get => (Brush?)GetValue(ToneFillProperty); set => SetValue(ToneFillProperty, value); }

    /// <summary>实底上的颜色。</summary>
    public Brush? ToneOnFill { get => (Brush?)GetValue(ToneOnFillProperty); set => SetValue(ToneOnFillProperty, value); }

    /// <summary>焦点环是否显示。</summary>
    public bool IsFocusVisible => (bool)GetValue(IsFocusVisibleProperty);

    double Em => FontSize;

    double Gap => (double)GetValue(GapFactorProperty) * Em;

    bool Wide => CheckedContent is not null || UncheckedContent is not null;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 2;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index == 0 ? control : label;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new UIElement[] { control, label }.GetEnumerator();

    static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var toggle = (Switch)d;
        toggle.Update();
        toggle.Slide();
        (UIElementAutomationPeer.FromElement(toggle) as SwitchAutomationPeer)?.RaiseToggleStateChanged((bool)e.OldValue, (bool)e.NewValue);
    }

    static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Switch)d).Update();

    /// <summary>档位的字号在主题样式的触发器里；写成像素数时是本地值。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TextSize)e.NewValue).Pixels is { } pixels) d.SetValue(FontSizeProperty, pixels);
        else if (((TextSize)e.OldValue).Pixels is not null) d.ClearValue(FontSizeProperty);
    }

    void OnIsLoadingChanged(bool isLoading)
    {
        loading.Update(isLoading);
        (UIElementAutomationPeer.FromElement(this) as SwitchAutomationPeer)?.RaiseLoadingChanged(isLoading);
    }

    void BindTone()
    {
        var (fill, onFill) = Rune.Wpf.ToneFill.Keys(Tone);
        SetResourceReference(ToneFillProperty, fill);
        SetResourceReference(ToneOnFillProperty, onFill);
    }

    /// <summary>文字、轨道里只显示当前状态那份（另一份占位，宽度不跳）。</summary>
    void Update()
    {
        label.Content = Content;
        label.Visibility = Content is null ? Visibility.Collapsed : Visibility.Visible;
        onText.Content = CheckedContent;
        offText.Content = UncheckedContent;
        onContent.Visibility = Wide ? IsChecked ? Visibility.Visible : Visibility.Hidden : Visibility.Collapsed;
        offContent.Visibility = Wide ? IsChecked ? Visibility.Hidden : Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>em 尺寸：圆角、加载指示、轨道文字给滑块留的位置、第一行文字的行高（1.5em，轨道在它里面居中）。</summary>
    void UpdateMetrics()
    {
        var em = Em;
        track.CornerRadius = new CornerRadius(0.625 * em);
        focusRing.CornerRadius = new CornerRadius(0.625 * em + SwitchStack.Ring);
        thumb.CornerRadius = new CornerRadius(0.4375 * em);
        spinner.Size = 0.75 * em;
        onContent.Padding = new Thickness(0.5 * em, 0, 1.375 * em, 0);
        offContent.Padding = new Thickness(1.375 * em, 0, 0.5 * em, 0);
        // TextBlock 的行高是像素数：文字是 ContentPresenter 里生成的 TextBlock，从它继承
        label.SetValue(TextBlock.LineHeightProperty, Leading.GetFactor(this) * em);
        control.InvalidateMeasure();
    }

    /// <summary>滑块在 durations.sm 内滑到另一端；没加载时直接到位。</summary>
    void Slide()
    {
        var (from, to) = ((double)GetValue(PositionProperty), IsChecked ? 1d : 0d);
        // 先写终值：过渡结束（FillBehavior.Stop）或被打断后留下的就是它
        SetValue(PositionProperty, to);
        var duration = IsLoaded ? Motion.Duration(this, "Rune.Duration.Sm", TimeSpan.FromMilliseconds(120)) : TimeSpan.Zero;
        if (duration <= TimeSpan.Zero || from == to)
        {
            BeginAnimation(PositionProperty, null);
            return;
        }
        BeginAnimation(PositionProperty, new DoubleAnimation(from, to, duration) { EasingFunction = TryFindResource("Rune.Easing.Standard") as IEasingFunction, FillBehavior = FillBehavior.Stop });
    }

    /// <summary>切换（点击、空格、读屏的 Toggle）：只读、切换中、不可用时不变。</summary>
    internal void Toggle()
    {
        if (IsReadOnly || IsLoading || !IsEnabled) return;
        var args = new SwitchTogglingEventArgs(!IsChecked);
        Toggling?.Invoke(this, args);
        if (!args.Cancel) SetCurrentValue(IsCheckedProperty, args.IsChecked);
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        pressed = true;
        Focus();
        e.Handled = true;
    }

    /// <summary>在开关上按下、没离开过就松开才切换（离开再回来不算）。</summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!pressed) return;
        pressed = false;
        e.Handled = true;
        Toggle();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        pressed = false;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Space) return;
        spacePressed = true;
        e.Handled = true;
    }

    /// <summary>空格松开时切换（同原生 checkbox）。</summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key != Key.Space) return;
        e.Handled = true;
        if (!spacePressed) return;
        spacePressed = false;
        Toggle();
    }

    /// <summary>焦点环只在键盘聚焦时显示。</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        SetValue(IsFocusVisiblePropertyKey, InputManager.Current.MostRecentInputDevice is KeyboardDevice);
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        SetValue(IsFocusVisiblePropertyKey, false);
        spacePressed = false;
    }

    /// <summary>画一层透明底：控件与文字之间的空隙也能点（TS 的 label 整块可点）。</summary>
    protected override void OnRender(DrawingContext drawingContext) => drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var controlSize = control.DesiredSize;
        var labelSize = default(Size);
        if (label.Visibility != Visibility.Collapsed)
        {
            label.Measure(new Size(Math.Max(0, constraint.Width - controlSize.Width - Gap), constraint.Height));
            labelSize = label.DesiredSize;
        }
        var width = controlSize.Width + (label.Visibility != Visibility.Collapsed ? Gap + labelSize.Width : 0);
        return new Size(width, Math.Max((double)GetValue(MinTargetProperty), Math.Max(controlSize.Height, labelSize.Height)));
    }

    /// <summary>控件与文字一行、顶端对齐：轨道居中在第一行文字的行高（1.5em）里。</summary>
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        control.Arrange(new Rect(control.DesiredSize));
        if (label.Visibility != Visibility.Collapsed)
            label.Arrange(new Rect(control.DesiredSize.Width + Gap, 0, Math.Max(0, arrangeBounds.Width - control.DesiredSize.Width - Gap), label.DesiredSize.Height));
        return arrangeBounds;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SwitchAutomationPeer(this);

    /// <summary>
    /// 叠放层（TS 的 control）：轨道、轨道里的两份文字、滑块、焦点环叠在一起，高度不低于 1.5em（与第一行文字居中）。
    /// 焦点环贴着轨道（focusRingStyles.tight：外扩 borderWidths.sm，宽 2）。
    /// </summary>
    sealed class SwitchStack(Switch owner) : Panel
    {
        public const double Ring = 4;

        /// <summary>轨道的大小：2.25em × 1.25em；轨道里有文字时随较宽的那份加宽。</summary>
        Size TrackSize()
        {
            var width = 2.25 * owner.Em;
            if (owner.Wide)
            {
                owner.onContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                owner.offContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = Math.Max(width, Math.Max(owner.onContent.DesiredSize.Width, owner.offContent.DesiredSize.Width));
            }
            return new Size(width, 1.25 * owner.Em);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var track = TrackSize();
            foreach (var part in new[] { owner.track, owner.thumb, owner.focusRing }) part.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return new Size(track.Width, Math.Max(1.5 * owner.Em, track.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var em = owner.Em;
            var track = TrackSize();
            var trackRect = new Rect(0, (finalSize.Height - track.Height) / 2, track.Width, track.Height);
            owner.track.Arrange(trackRect);
            owner.focusRing.Arrange(new Rect(trackRect.X - Ring, trackRect.Y - Ring, trackRect.Width + 2 * Ring, trackRect.Height + 2 * Ring));
            foreach (var content in new[] { owner.onContent, owner.offContent }) content.Arrange(trackRect);
            var side = 0.875 * em;
            var (start, end) = (0.1875 * em, track.Width - 1.0625 * em);
            var x = start + (end - start) * (double)owner.GetValue(PositionProperty);
            owner.thumb.Arrange(new Rect(x, trackRect.Y + (track.Height - side) / 2, side, side));
            return finalSize;
        }
    }

    /// <summary>读屏：Toggle 模式（开 / 关），名称取文字；切换中时项状态是"加载中"；轨道里的文字对读屏隐藏（不进子节点）。</summary>
    sealed class SwitchAutomationPeer(Switch owner) : FrameworkElementAutomationPeer(owner), IToggleProvider
    {
        static ToggleState ToState(bool on) => on ? ToggleState.On : ToggleState.Off;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string GetClassNameCore() => nameof(Switch);

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Content as string ?? "";

        protected override string GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();

        protected override bool IsRequiredForFormCore() => owner.IsRequired;

        protected override List<AutomationPeer>? GetChildrenCore() => null;

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Toggle ? this : base.GetPattern(patternInterface);

        public ToggleState ToggleState => ToState(owner.IsChecked);

        public void Toggle()
        {
            if (!owner.IsEnabled) throw new ElementNotEnabledException();
            owner.Toggle();
        }

        public void RaiseToggleStateChanged(bool oldValue, bool newValue) =>
            RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, ToState(oldValue), ToState(newValue));

        public void RaiseLoadingChanged(bool isLoading)
        {
            var status = Provider.GetLang(owner).Loading;
            RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, isLoading ? "" : status, isLoading ? status : "");
        }
    }
}

/// <summary>Switch 要切换时的参数：<see cref="IsChecked"/> 是要切到的值，设 Cancel 不改。</summary>
/// <param name="isChecked">要切到的值。</param>
public sealed class SwitchTogglingEventArgs(bool isChecked) : CancelEventArgs
{
    /// <summary>要切到的值。</summary>
    public bool IsChecked { get; } = isChecked;
}

/// <summary>
/// Switch 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:SwitchParts.Thumb}" TargetType="Border"&gt;</c>。
/// 外层（TS 的 propsRoot）就是 Switch 自己。Track、FocusRing 是 TS 里的 input（轨道本身与它的焦点环，未声明的属性落在它上面）。
/// </summary>
public static class SwitchParts
{
    /// <summary>叠放层（TS 的 propsControl）：轨道、文字、滑块叠在一起。</summary>
    public static readonly PartKey Control = new(typeof(Switch), nameof(Control), typeof(Panel));

    /// <summary>轨道（TS 的 input）：Border，关时 borderStrong、开时色调实底、出错时描边是危险色。</summary>
    public static readonly PartKey Track = new(typeof(Switch), nameof(Track), typeof(Border));

    /// <summary>焦点环：Border，键盘聚焦时贴着轨道显示。</summary>
    public static readonly PartKey FocusRing = new(typeof(Switch), nameof(FocusRing), typeof(Border));

    /// <summary>滑块（TS 的 propsThumb）：Border。</summary>
    public static readonly PartKey Thumb = new(typeof(Switch), nameof(Thumb), typeof(Border));

    /// <summary>轨道里的文字外层（TS 的 propsContent，开、关两份）：Border，对读屏隐藏。</summary>
    public static readonly PartKey Content = new(typeof(Switch), nameof(Content), typeof(Border));

    /// <summary>轨道里文字的字号层（TS 的 propsContentText）：ContentPresenter。</summary>
    public static readonly PartKey ContentText = new(typeof(Switch), nameof(ContentText), typeof(ContentPresenter));

    /// <summary>滑块里的加载指示（TS 的 propsSpinner）。</summary>
    public static readonly PartKey Spinner = new(typeof(Switch), nameof(Spinner), typeof(Spinner));

    /// <summary>文字（TS 的 propsLabel）：ContentPresenter。</summary>
    public static readonly PartKey Label = new(typeof(Switch), nameof(Label), typeof(ContentPresenter));
}
