using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Styling;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 开关（vue-tsx-pro 的 Switch）：立即生效的开 / 关设置，如"接收通知"；要提交才生效的选项用 Checkbox。
/// <code>
/// &lt;rune:Switch IsChecked="{Binding Notify}"&gt;接收通知&lt;/rune:Switch&gt;
/// &lt;rune:Switch IsChecked="{Binding AutoSave}" IsLoading="{Binding Saving}" CheckedContent="开" UncheckedContent="关" /&gt;
/// </code>
/// 轨道 2.25em × 1.25em，滑块 0.875em，在 durations.sm 内滑到另一端（从右到左时 Avalonia 整体镜像）；轨道里的文字（CheckedContent、
/// UncheckedContent）让轨道随文字加宽，两份取较宽的，切换时宽度不跳。打开时轨道是色调的实底（<see cref="ToneFill"/>），滑块是其上的颜色。
/// 文字在右边，整块可点，高度不低于 24px（WCAG 2.5.8）。空格切换；只读时能聚焦、不能切换；切换中（IsLoading）滑块里转圈（防闪烁）、点击无效。
/// 读屏：Toggle 模式（开 / 关），名称取文字；切换中时项状态是"加载中"。部件见 <see cref="SwitchParts"/>，默认外观在 SwitchParts.axaml。
/// </summary>
[PseudoClasses(":checked", ":readonly", ":invalid", ":loading", ":wide")]
public class Switch : Control
{
    /// <summary>是否打开（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty = AvaloniaProperty.Register<Switch, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>只读：能聚焦，不能切换（查看详情、审批时）；外观照常。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<Switch, bool>(nameof(IsReadOnly));

    /// <summary>必填。</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = AvaloniaProperty.Register<Switch, bool>(nameof(IsRequired));

    /// <summary>校验未通过：轨道的描边是危险色。</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = AvaloniaProperty.Register<Switch, bool>(nameof(IsInvalid));

    /// <summary>切换中（如等接口返回）：滑块里转圈，点击无效。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Switch, bool>(nameof(IsLoading));

    /// <summary>打开时轨道里的文字或图标（如"开"、对勾）。</summary>
    public static readonly StyledProperty<object?> CheckedContentProperty = AvaloniaProperty.Register<Switch, object?>(nameof(CheckedContent));

    /// <summary>关闭时轨道里的文字或图标。</summary>
    public static readonly StyledProperty<object?> UncheckedContentProperty = AvaloniaProperty.Register<Switch, object?>(nameof(UncheckedContent));

    /// <summary>文字（读屏名称）。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Switch, object?>(nameof(Content));

    /// <summary>打开时轨道的色调；主题默认 accent。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Switch, StatusTone>(nameof(Tone));

    /// <summary>字号档位：轨道、滑块随之缩放；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Switch, TextSize>(nameof(Size));

    /// <summary>色调的实底（TS 的 --tone-fill），按 Tone 取主题资源；部件样式绑它。</summary>
    public static readonly StyledProperty<IBrush?> ToneFillProperty = AvaloniaProperty.Register<Switch, IBrush?>(nameof(ToneFill));

    /// <summary>实底上的滑块、文字色（TS 的 --tone-on-fill）。</summary>
    public static readonly StyledProperty<IBrush?> ToneOnFillProperty = AvaloniaProperty.Register<Switch, IBrush?>(nameof(ToneOnFill));

    /// <summary>字号（继承），em 尺寸按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Switch>();

    /// <summary>滑块的位置：0 在起始边，1 在结束边（过渡驱动）。</summary>
    static readonly StyledProperty<double> PositionProperty = AvaloniaProperty.Register<Switch, double>("Position");

    static readonly StyledProperty<double> GapFactorProperty = AvaloniaProperty.Register<Switch, double>("GapFactor", 0.5);
    static readonly StyledProperty<double> MinTargetProperty = AvaloniaProperty.Register<Switch, double>("MinTarget", 24);

    readonly SwitchStack control;
    readonly Border track = new() { Name = SwitchParts.Track };
    readonly Border focusRing = new() { Name = "PART_FocusRing", IsHitTestVisible = false };
    readonly Border onContent = new() { Name = SwitchParts.Content, Classes = { "on" } };
    readonly Border offContent = new() { Name = SwitchParts.Content, Classes = { "off" } };
    readonly ContentPresenter onText = new() { Name = SwitchParts.ContentText };
    readonly ContentPresenter offText = new() { Name = SwitchParts.ContentText };
    readonly Border thumb = new() { Name = SwitchParts.Thumb };
    readonly Spinner spinner = new() { Name = SwitchParts.Spinner, IsVisible = false };
    readonly ContentPresenter label = new() { Name = SwitchParts.Label };
    readonly TemplateResources resources;
    readonly DelayedLoading loading;
    CancellationTokenSource? slide;
    bool pressed;

    static Switch()
    {
        FocusableProperty.OverrideDefaultValue<Switch>(true);
        AffectsMeasure<Switch>(FontSizeProperty, GapFactorProperty, MinTargetProperty, CheckedContentProperty, UncheckedContentProperty, ContentProperty);
        AffectsArrange<Switch>(PositionProperty);
    }

    /// <summary>新建：关闭；间距、色调绑到主题。</summary>
    public Switch()
    {
        resources = new TemplateResources(this);
        resources.Bind(GapFactorProperty, "Rune.Space.Sm");
        resources.Bind(MinTargetProperty, "Rune.Size.MinTarget");
        BindTone();
        loading = new DelayedLoading(shown => spinner.IsVisible = shown);
        onContent.Child = onText;
        offContent.Child = offText;
        thumb.Child = spinner;
        control = new SwitchStack(this) { Name = SwitchParts.Control };
        // 轨道里的文字只是装饰，读屏从开关的状态得知
        foreach (var part in new Control[] { onContent, offContent, spinner }) AutomationProperties.SetAccessibilityView(part, AccessibilityView.Raw);
        control.Children.AddRange([track, onContent, offContent, thumb, focusRing]);
        LogicalChildren.AddRange([control, label]);
        VisualChildren.AddRange([control, label]);
        Update();
    }

    /// <summary>
    /// 要切换时（点击、空格、读屏）先发：设 Cancel 不改值——切换要等接口时拦下，接口回来再写 IsChecked（TS 的受控 onUpdate:_checked）。
    /// </summary>
    public event EventHandler<SwitchTogglingEventArgs>? Toggling;

    /// <summary>是否打开。</summary>
    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>切换中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>打开时轨道里的内容。</summary>
    public object? CheckedContent { get => GetValue(CheckedContentProperty); set => SetValue(CheckedContentProperty, value); }

    /// <summary>关闭时轨道里的内容。</summary>
    public object? UncheckedContent { get => GetValue(UncheckedContentProperty); set => SetValue(UncheckedContentProperty, value); }

    /// <summary>文字。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>色调的实底。</summary>
    public IBrush? ToneFill { get => GetValue(ToneFillProperty); set => SetValue(ToneFillProperty, value); }

    /// <summary>实底上的颜色。</summary>
    public IBrush? ToneOnFill { get => GetValue(ToneOnFillProperty); set => SetValue(ToneOnFillProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    double Em => FontSize;

    bool Wide => CheckedContent is not null || UncheckedContent is not null;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // 轨道、滑块由叠放层排：尺寸与位置变了让它重排
        if (change.Property == PositionProperty) control.InvalidateArrange();
        else if (change.Property == FontSizeProperty || change.Property == CheckedContentProperty || change.Property == UncheckedContentProperty) control.InvalidateMeasure();
        if (change.Property == IsCheckedProperty)
        {
            Update();
            Slide();
        }
        else if (change.Property == ToneProperty) BindTone();
        else if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == IsLoadingProperty)
        {
            loading.Update(IsLoading);
            Update();
        }
        else if (change.Property == IsReadOnlyProperty || change.Property == IsInvalidProperty || change.Property == ContentProperty
                 || change.Property == CheckedContentProperty || change.Property == UncheckedContentProperty) Update();
    }

    void BindTone()
    {
        var (fill, onFill) = Rune.Avalonia.ToneFill.Keys(Tone);
        resources.Bind(ToneFillProperty, fill);
        resources.Bind(ToneOnFillProperty, onFill);
    }

    /// <summary>状态伪类、文字、轨道里只显示当前状态那份（另一份占位，宽度不跳）。</summary>
    void Update()
    {
        PseudoClasses.Set(":checked", IsChecked);
        PseudoClasses.Set(":readonly", IsReadOnly);
        PseudoClasses.Set(":invalid", IsInvalid);
        PseudoClasses.Set(":loading", IsLoading);
        PseudoClasses.Set(":wide", Wide);
        label.Content = Content;
        label.IsVisible = Content is not null;
        onText.Content = CheckedContent;
        offText.Content = UncheckedContent;
        onContent.IsVisible = offContent.IsVisible = Wide;
        onContent.Opacity = IsChecked ? 1 : 0;
        offContent.Opacity = IsChecked ? 0 : 1;
    }

    /// <summary>滑块在 durations.sm 内滑到另一端；没挂上时直接到位。</summary>
    void Slide()
    {
        var (from, to) = (GetValue(PositionProperty), IsChecked ? 1d : 0d);
        slide?.Cancel();
        slide = null;
        SetValue(PositionProperty, to);
        var duration = VisualRoot is null ? TimeSpan.Zero
            : Motion.Duration(this.TryFindResource("Rune.Duration.Sm", ActualThemeVariant, out var value) && value is TimeSpan sm ? sm : TimeSpan.FromMilliseconds(120));
        if (duration <= TimeSpan.Zero) return;
        slide = new CancellationTokenSource();
        _ = new Animation
        {
            Duration = duration,
            Easing = this.TryFindResource("Rune.Easing.Standard", ActualThemeVariant, out var found) && found is Easing standard ? standard : new CubicEaseInOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(PositionProperty, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(PositionProperty, to) } },
            },
        }.RunAsync(this, slide.Token);
    }

    /// <summary>切换（点击、空格、读屏的 Toggle）：只读、切换中时不变。</summary>
    internal void Toggle()
    {
        if (IsReadOnly || IsLoading || !IsEffectivelyEnabled) return;
        var args = new SwitchTogglingEventArgs(!IsChecked);
        Toggling?.Invoke(this, args);
        if (!args.Cancel) SetCurrentValue(IsCheckedProperty, args.IsChecked);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        pressed = true;
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!pressed) return;
        pressed = false;
        e.Handled = true;
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) Toggle();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space) e.Handled = true;
    }

    /// <summary>空格松开时切换（同原生 checkbox）。</summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key != Key.Space) return;
        e.Handled = true;
        Toggle();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        loading.Stop();
    }

    /// <summary>轨道的大小：2.25em × 1.25em；轨道里有文字时随较宽的那份加宽。</summary>
    LayoutSize TrackSize()
    {
        var width = 2.25 * Em;
        if (Wide)
        {
            onContent.Padding = new Thickness(0.5 * Em, 0, 1.375 * Em, 0);
            offContent.Padding = new Thickness(1.375 * Em, 0, 0.5 * Em, 0);
            onContent.Measure(LayoutSize.Infinity);
            offContent.Measure(LayoutSize.Infinity);
            width = Math.Max(width, Math.Max(onContent.DesiredSize.Width, offContent.DesiredSize.Width));
        }
        return new LayoutSize(width, 1.25 * Em);
    }

    double Gap => GetValue(GapFactorProperty) * Em;

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        control.Measure(LayoutSize.Infinity);
        var controlSize = control.DesiredSize;
        var labelSize = default(LayoutSize);
        if (label.IsVisible)
        {
            label.Measure(availableSize.Deflate(new Thickness(controlSize.Width + Gap, 0, 0, 0)));
            labelSize = label.DesiredSize;
        }
        var width = controlSize.Width + (label.IsVisible ? Gap + labelSize.Width : 0);
        return new LayoutSize(width, Math.Max(GetValue(MinTargetProperty), Math.Max(controlSize.Height, labelSize.Height)));
    }

    /// <summary>控件与文字一行、顶端对齐：轨道居中在第一行文字的行高（1.5em）里。</summary>
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        control.Arrange(new Rect(control.DesiredSize));
        if (label.IsVisible) label.Arrange(new Rect(control.DesiredSize.Width + Gap, 0, Math.Max(0, finalSize.Width - control.DesiredSize.Width - Gap), label.DesiredSize.Height));
        return finalSize;
    }

    /// <summary>画一层透明底：控件与文字之间的空隙也能点（TS 的 label 整块可点）。</summary>
    public override void Render(DrawingContext context) => context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SwitchAutomationPeer(this);

    /// <summary>
    /// 叠放层（TS 的 control）：轨道、轨道里的两份文字、滑块、焦点环叠在一起，高度不低于 1.5em（与第一行文字居中）。
    /// 焦点环贴着轨道（focusRingStyles.tight：外扩 borderWidths.sm，宽 2）。
    /// </summary>
    sealed class SwitchStack(Switch owner) : Panel
    {
        protected override LayoutSize MeasureOverride(LayoutSize availableSize)
        {
            var track = owner.TrackSize();
            owner.thumb.Measure(LayoutSize.Infinity);
            owner.track.Measure(LayoutSize.Infinity);
            owner.focusRing.Measure(LayoutSize.Infinity);
            return new LayoutSize(track.Width, Math.Max(1.5 * owner.Em, track.Height));
        }

        protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
        {
            var em = owner.Em;
            var track = owner.TrackSize();
            var trackRect = new Rect(0, (finalSize.Height - track.Height) / 2, track.Width, track.Height);
            owner.track.CornerRadius = new CornerRadius(track.Height / 2);
            owner.track.Arrange(trackRect);
            const double Ring = 4;
            owner.focusRing.CornerRadius = new CornerRadius(track.Height / 2 + Ring);
            owner.focusRing.Arrange(trackRect.Inflate(Ring));
            foreach (var content in new[] { owner.onContent, owner.offContent })
                if (content.IsVisible) content.Arrange(trackRect);
            var side = 0.875 * em;
            var (start, end) = (0.1875 * em, track.Width - 1.0625 * em);
            var x = start + (end - start) * owner.GetValue(PositionProperty);
            owner.thumb.CornerRadius = new CornerRadius(side / 2);
            owner.spinner.Size = 0.75 * em;
            owner.thumb.Arrange(new Rect(x, trackRect.Y + (track.Height - side) / 2, side, side));
            return finalSize;
        }
    }

    /// <summary>读屏：Toggle 模式（开 / 关），名称取文字；切换中时项状态是"加载中"。</summary>
    sealed class SwitchAutomationPeer : ControlAutomationPeer, IToggleProvider
    {
        readonly Switch owner;

        public SwitchAutomationPeer(Switch owner) : base(owner)
        {
            this.owner = owner;
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == IsCheckedProperty)
                    RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, ToState((bool)e.OldValue!), ToState((bool)e.NewValue!));
            };
        }

        static ToggleState ToState(bool on) => on ? ToggleState.On : ToggleState.Off;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Content as string;

        protected override string? GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();

        public ToggleState ToggleState => ToState(owner.IsChecked);

        public void Toggle() => owner.Toggle();
    }
}

/// <summary>Switch 要切换时的参数：<see cref="IsChecked"/> 是要切到的值，设 Cancel 不改。</summary>
/// <param name="isChecked">要切到的值。</param>
public sealed class SwitchTogglingEventArgs(bool isChecked) : System.ComponentModel.CancelEventArgs
{
    /// <summary>要切到的值。</summary>
    public bool IsChecked { get; } = isChecked;
}

/// <summary>Switch 的部件名（代码组合的子元素）：<c>rune|Switch #PART_Thumb</c>。外层（TS 的 propsRoot）就是 Switch 自己。</summary>
public static class SwitchParts
{
    /// <summary>叠放层（TS 的 propsControl）：轨道、文字、滑块叠在一起。</summary>
    public const string Control = "PART_Control";

    /// <summary>轨道（TS 的 input）：Border，关时 borderStrong、开时色调实底、出错时描边是危险色。</summary>
    public const string Track = "PART_Track";

    /// <summary>滑块（TS 的 propsThumb）：Border。</summary>
    public const string Thumb = "PART_Thumb";

    /// <summary>轨道里的文字外层（TS 的 propsContent，开、关两份，类 on、off）：Border，对读屏隐藏。</summary>
    public const string Content = "PART_Content";

    /// <summary>轨道里文字的字号层（TS 的 propsContentText）：ContentPresenter。</summary>
    public const string ContentText = "PART_ContentText";

    /// <summary>滑块里的加载指示（TS 的 propsSpinner）。</summary>
    public const string Spinner = "PART_Spinner";

    /// <summary>文字（TS 的 propsLabel）：ContentPresenter。</summary>
    public const string Label = "PART_Label";
}
