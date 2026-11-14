using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
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
/// 外层、文字与切换见 <see cref="ChoiceBase"/>；只读时能聚焦、不能切换；切换中（IsLoading）滑块里转圈（防闪烁）、点击无效。
/// 读屏：Toggle 模式（开 / 关），名称取文字；切换中时项状态是"加载中"。部件见 <see cref="SwitchParts"/>，默认外观在 SwitchParts.axaml。
/// </summary>
[PseudoClasses(":loading", ":wide")]
public class Switch : ChoiceBase
{
    /// <summary>只读：能聚焦，不能切换（查看详情、审批时）；外观照常。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = IsReadOnlyCoreProperty.AddOwner<Switch>();

    /// <summary>必填。</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = IsRequiredCoreProperty.AddOwner<Switch>();

    /// <summary>校验未通过：轨道的描边是危险色。</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = IsInvalidCoreProperty.AddOwner<Switch>();

    /// <summary>打开时轨道的色调；主题默认 accent。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = ToneCoreProperty.AddOwner<Switch>();

    /// <summary>字号档位：轨道、滑块随之缩放；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = SizeCoreProperty.AddOwner<Switch>();

    /// <summary>切换中（如等接口返回）：滑块里转圈，点击无效。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Switch, bool>(nameof(IsLoading));

    /// <summary>文字下方的说明（次要颜色、小一号）：设置项里解释这个开关管什么；读屏作为帮助文字念出来（同 Checkbox、Radio）。</summary>
    public static readonly StyledProperty<object?> DescriptionProperty = DescriptionCoreProperty.AddOwner<Switch>();

    /// <summary>打开时轨道里的文字或图标（如"开"、对勾）。</summary>
    public static readonly StyledProperty<object?> CheckedContentProperty = AvaloniaProperty.Register<Switch, object?>(nameof(CheckedContent));

    /// <summary>关闭时轨道里的文字或图标。</summary>
    public static readonly StyledProperty<object?> UncheckedContentProperty = AvaloniaProperty.Register<Switch, object?>(nameof(UncheckedContent));

    /// <summary>
    /// 悬停时的轨道色（上游 switchStyles 的 _hover）：关时 borderStrong 混进 15% 的正文色，开时色调实底混进 12%——亮色里变深、暗色里变亮。
    /// 按主题与开关状态算好，主题样式绑定它。
    /// </summary>
    public static readonly StyledProperty<IBrush?> TrackHoverProperty = AvaloniaProperty.Register<Switch, IBrush?>(nameof(TrackHover));

    /// <summary>滑块的位置：0 在起始边，1 在结束边（过渡驱动）。</summary>
    static readonly StyledProperty<double> PositionProperty = AvaloniaProperty.Register<Switch, double>("Position");

    static readonly StyledProperty<IBrush?> TrackOffProperty = AvaloniaProperty.Register<Switch, IBrush?>("TrackOff");
    static readonly StyledProperty<IBrush?> TextBrushProperty = AvaloniaProperty.Register<Switch, IBrush?>("TextBrush");

    readonly SwitchStack control;
    readonly Border track = new() { Name = SwitchParts.Track };
    readonly Border focusRing = new() { Name = "PART_FocusRing", IsHitTestVisible = false };
    readonly Border onContent = new() { Name = SwitchParts.Content, Classes = { "on" } };
    readonly Border offContent = new() { Name = SwitchParts.Content, Classes = { "off" } };
    readonly ContentPresenter onText = new() { Name = SwitchParts.ContentText };
    readonly ContentPresenter offText = new() { Name = SwitchParts.ContentText };
    readonly Border thumb = new() { Name = SwitchParts.Thumb };
    readonly Spinner spinner = new() { Name = SwitchParts.Spinner, IsVisible = false };
    readonly DelayedLoading loading;
    CancellationTokenSource? slide;

    static Switch()
    {
        AffectsMeasure<Switch>(CheckedContentProperty, UncheckedContentProperty);
        AffectsArrange<Switch>(PositionProperty);
    }

    /// <summary>新建：关闭。</summary>
    public Switch()
    {
        var themed = new TemplateResources(this);
        themed.Bind(TrackOffProperty, ThemeKeys.Brush.BorderStrong);
        themed.Bind(TextBrushProperty, ThemeKeys.Brush.Text);
        loading = new DelayedLoading(shown => spinner.IsVisible = shown);
        onContent.Child = onText;
        offContent.Child = offText;
        thumb.Child = spinner;
        control = new SwitchStack(this) { Name = SwitchParts.Control };
        // 轨道里的文字只是装饰，读屏从开关的状态得知
        foreach (var part in new Control[] { onContent, offContent, spinner }) AutomationProperties.SetAccessibilityView(part, AccessibilityView.Raw);
        control.Children.AddRange([track, onContent, offContent, thumb, focusRing]);
        Compose(control);
    }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>切换中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>打开时轨道里的内容。</summary>
    public object? CheckedContent { get => GetValue(CheckedContentProperty); set => SetValue(CheckedContentProperty, value); }

    /// <summary>文字下方的说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>关闭时轨道里的内容。</summary>
    public object? UncheckedContent { get => GetValue(UncheckedContentProperty); set => SetValue(UncheckedContentProperty, value); }

    /// <summary>悬停时的轨道色。</summary>
    public IBrush? TrackHover { get => GetValue(TrackHoverProperty); set => SetValue(TrackHoverProperty, value); }

    bool Wide => CheckedContent is not null || UncheckedContent is not null;

    /// <summary>按当前的轨道色混进正文色（当前值，不占优先级：使用方写的 TrackHover 照用）。</summary>
    void UpdateTrackHover()
    {
        var (track, amount) = IsChecked ? (ToneFill, 0.88) : (GetValue(TrackOffProperty), 0.85);
        SetCurrentValue(TrackHoverProperty, track is ISolidColorBrush fill && GetValue(TextBrushProperty) is ISolidColorBrush text
            ? new SolidColorBrush(ChoiceBase.Mix(fill.Color, text.Color, amount))
            : track);
    }

    /// <inheritdoc />
    protected override bool CanToggle => !IsLoading;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty || change.Property == ToneFillProperty || change.Property == TrackOffProperty || change.Property == TextBrushProperty) UpdateTrackHover();
        // 轨道、滑块由叠放层排：尺寸与位置变了让它重排
        if (change.Property == PositionProperty) control.InvalidateArrange();
        else if (change.Property == CheckedContentProperty || change.Property == UncheckedContentProperty)
        {
            control.InvalidateMeasure();
            Update();
        }
        else if (change.Property == IsLoadingProperty)
        {
            loading.Update(IsLoading);
            Update();
        }
    }

    /// <summary>加上切换中、轨道里有文字两个伪类；轨道里只显示当前状态那份（另一份占位，宽度不跳）。</summary>
    protected override void Update()
    {
        base.Update();
        PseudoClasses.Set(":loading", IsLoading);
        PseudoClasses.Set(":wide", Wide);
        onText.Content = CheckedContent;
        offText.Content = UncheckedContent;
        onContent.IsVisible = offContent.IsVisible = Wide;
        onContent.Opacity = IsChecked ? 1 : 0;
        offContent.Opacity = IsChecked ? 0 : 1;
    }

    /// <inheritdoc />
    protected override void OnCheckedChanged() => Slide();

    /// <summary>滑块在 durations.sm 内滑到另一端；没挂上时直接到位。</summary>
    void Slide()
    {
        var (from, to) = (GetValue(PositionProperty), IsChecked ? 1d : 0d);
        slide?.Cancel();
        slide = null;
        SetValue(PositionProperty, to);
        var duration = VisualRoot is null ? TimeSpan.Zero
            : Motion.Duration(this, ThemeKeys.Duration.Sm);
        if (duration <= TimeSpan.Zero) return;
        slide = new CancellationTokenSource();
        _ = new Animation
        {
            Duration = duration,
            Easing = this.Resource<Easing>(ThemeKeys.Easing.Standard),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(PositionProperty, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(PositionProperty, to) } },
            },
        }.RunAsync(this, slide.Token);
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

    /// <summary>读屏：Toggle 模式（开 / 关）；切换中时项状态是"加载中"。</summary>
    sealed class SwitchAutomationPeer : ChoiceAutomationPeer, IToggleProvider
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

        protected override string? GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();

        public ToggleState ToggleState => ToState(owner.IsChecked);

        public void Toggle() => owner.Toggle();
    }
}

/// <summary>Switch 的部件名（代码组合的子元素）：<c>rune|Switch #PART_Thumb</c>。外层（TS 的 propsRoot）就是 Switch 自己，文字见 <see cref="ChoiceBaseParts"/>。</summary>
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
}
