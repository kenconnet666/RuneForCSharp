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
public class Switch : ChoiceBase
{
    /// <summary>只读：能聚焦，不能切换（查看详情、审批时）；外观照常。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = IsReadOnlyCoreProperty.AddOwner(typeof(Switch));

    /// <summary>必填（读屏念"必填"）。</summary>
    public static readonly DependencyProperty IsRequiredProperty = IsRequiredCoreProperty.AddOwner(typeof(Switch));

    /// <summary>校验未通过：轨道的描边是危险色。</summary>
    public static readonly DependencyProperty IsInvalidProperty = IsInvalidCoreProperty.AddOwner(typeof(Switch));

    /// <summary>打开时轨道的色调；主题默认 accent。</summary>
    public static readonly DependencyProperty ToneProperty = ToneCoreProperty.AddOwner(typeof(Switch));

    /// <summary>字号档位：轨道、滑块随之缩放；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = SizeCoreProperty.AddOwner(typeof(Switch));

    /// <summary>切换中（如等接口返回）：滑块里转圈，点击无效。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(Switch), new PropertyMetadata(false, (d, e) => ((Switch)d).OnIsLoadingChanged((bool)e.NewValue)));

    /// <summary>打开时轨道里的文字或图标（如"开"、对勾）。</summary>
    public static readonly DependencyProperty CheckedContentProperty = DependencyProperty.Register(
        nameof(CheckedContent), typeof(object), typeof(Switch), new FrameworkPropertyMetadata(null, (d, _) => ((Switch)d).Update()));

    /// <summary>关闭时轨道里的文字或图标。</summary>
    public static readonly DependencyProperty UncheckedContentProperty = DependencyProperty.Register(
        nameof(UncheckedContent), typeof(object), typeof(Switch), new FrameworkPropertyMetadata(null, (d, _) => ((Switch)d).Update()));

    /// <summary>滑块的位置：0 在起始边，1 在结束边（过渡驱动）。</summary>
    static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        "Position", typeof(double), typeof(Switch), new FrameworkPropertyMetadata(0d, (d, _) => ((Switch)d).control.InvalidateArrange()));

    readonly SwitchStack control;
    readonly Border track = new() { Name = "PART_Track" };
    readonly Border focusRing = new() { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    readonly Border onContent = new();
    readonly Border offContent = new();
    readonly ContentPresenter onText = new();
    readonly ContentPresenter offText = new();
    readonly Border thumb = new() { Name = "PART_Thumb" };
    readonly Spinner spinner = new() { Visibility = Visibility.Collapsed };
    readonly DelayedLoading loading;

    static Switch() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Switch), new FrameworkPropertyMetadata(typeof(Switch)));

    /// <summary>新建：关闭；部件的外观引用主题的部件样式。</summary>
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
        loading = new DelayedLoading(shown => spinner.Visibility = shown ? Visibility.Visible : Visibility.Collapsed);
        onContent.Child = onText;
        offContent.Child = offText;
        thumb.Child = spinner;
        foreach (var part in new UIElement[] { track, onContent, offContent, thumb, focusRing }) control.Children.Add(part);
        Compose(control);
        Unloaded += (_, _) => loading.Stop();
    }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool IsInvalid { get => (bool)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>切换中。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>打开时轨道里的内容。</summary>
    public object? CheckedContent { get => GetValue(CheckedContentProperty); set => SetValue(CheckedContentProperty, value); }

    /// <summary>关闭时轨道里的内容。</summary>
    public object? UncheckedContent { get => GetValue(UncheckedContentProperty); set => SetValue(UncheckedContentProperty, value); }

    bool Wide => CheckedContent is not null || UncheckedContent is not null;

    /// <inheritdoc />
    protected override bool CanToggle => !IsLoading;

    /// <inheritdoc />
    protected override void OnCheckedChanged(bool oldValue, bool newValue)
    {
        Slide();
        (UIElementAutomationPeer.FromElement(this) as SwitchAutomationPeer)?.RaiseToggleStateChanged(oldValue, newValue);
    }

    void OnIsLoadingChanged(bool isLoading)
    {
        loading.Update(isLoading);
        (UIElementAutomationPeer.FromElement(this) as SwitchAutomationPeer)?.RaiseLoadingChanged(isLoading);
    }

    /// <summary>轨道里只显示当前状态那份（另一份占位，宽度不跳）。</summary>
    protected internal override void Update()
    {
        base.Update();
        onText.Content = CheckedContent;
        offText.Content = UncheckedContent;
        onContent.Visibility = Wide ? IsChecked ? Visibility.Visible : Visibility.Hidden : Visibility.Collapsed;
        offContent.Visibility = Wide ? IsChecked ? Visibility.Hidden : Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>em 尺寸：圆角、加载指示、轨道文字给滑块留的位置。</summary>
    protected override void UpdateMetrics()
    {
        var em = Em;
        track.CornerRadius = new CornerRadius(0.625 * em);
        focusRing.CornerRadius = new CornerRadius(0.625 * em + SwitchStack.Ring);
        thumb.CornerRadius = new CornerRadius(0.4375 * em);
        spinner.Size = 0.75 * em;
        onContent.Padding = new Thickness(0.5 * em, 0, 1.375 * em, 0);
        offContent.Padding = new Thickness(1.375 * em, 0, 0.5 * em, 0);
        base.UpdateMetrics();
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
    sealed class SwitchAutomationPeer(Switch owner) : ChoiceAutomationPeer(owner), IToggleProvider
    {
        static ToggleState ToState(bool on) => on ? ToggleState.On : ToggleState.Off;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string GetClassNameCore() => nameof(Switch);

        protected override string GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();

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

/// <summary>
/// Switch 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:SwitchParts.Thumb}" TargetType="Border"&gt;</c>。
/// 外层（TS 的 propsRoot）就是 Switch 自己，文字见 <see cref="ChoiceBaseParts"/>。Track、FocusRing 是 TS 里的 input（轨道本身与它的焦点环，未声明的属性落在它上面）。
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
}
