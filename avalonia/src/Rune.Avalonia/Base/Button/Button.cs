using System.ComponentModel;
using global::Avalonia;
using global::Avalonia.Animation;
using global::Avalonia.Animation.Easings;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Data;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 按钮（vue-tsx-pro 的 Button）：外观 = 样式 × 色调 × 字号档位 × 圆角，内部尺寸都是 em，随字号缩放。
/// <code>
/// &lt;rune:Button Icon="{x:Static rune:Lucide.Plus}"&gt;新建项目&lt;/rune:Button&gt;
/// &lt;rune:Button Variant="outline" Tone="neutral"&gt;取消&lt;/rune:Button&gt;
/// &lt;rune:Button IsLoading="True"&gt;保存&lt;/rune:Button&gt;   &lt;!-- 显示旋转指示，宽度不变，点击无效，焦点保留 --&gt;
/// </code>
/// 从平台的 Button 派生：Click、Command、键盘、读屏的 Invoke 都现成。色调样式设 <see cref="Palette"/>，样式档位绑定调色板的字段
/// （DESIGN §4 决定 11），档位与色调切伪类（<c>:solid</c>、<c>:danger</c>）；使用方要改单个按钮的颜色，在带伪类的选择器里设 Background。
/// 部件（模板里的名字，见 <see cref="ButtonParts"/>）：内容层、前置图标、后置图标、加载指示，用 <c>/template/</c> 样式改。
/// 效果同 TS：按下有波纹（<see cref="Avalonia.Ripple"/>），颜色在 durations.sm 内过渡，键盘聚焦时外面一圈焦点环，
/// 加载指示防闪烁（<see cref="DelayedLoading"/>：很快完成的加载不出现，出现了至少显示 0.5 秒）。
/// </summary>
public class Button : global::Avalonia.Controls.Button
{
    /// <summary>样式，主题里默认 solid。</summary>
    public static readonly StyledProperty<ButtonVariant> VariantProperty = AvaloniaProperty.Register<Button, ButtonVariant>(nameof(Variant));

    /// <summary>色调，主题里默认 accent。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Button, StatusTone>(nameof(Tone));

    /// <summary>字号档位，内部尺寸随之缩放；不写时继承外面的字号。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Button, TextSize>(nameof(Size));

    /// <summary>圆角，主题里默认 md。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<Button, Radius>(nameof(Radius));

    /// <summary>加载中：显示旋转指示，宽度不变，点击无效，焦点保留。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Button, bool>(nameof(IsLoading));

    /// <summary>按下时的波纹；不写时沿用 <see cref="Avalonia.Ripple.Enabled"/>（默认开启）。</summary>
    public static readonly StyledProperty<bool?> RippleProperty = AvaloniaProperty.Register<Button, bool?>(nameof(Ripple));

    /// <summary>文字前的图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<Button, IconData?>(nameof(Icon));

    /// <summary>文字后的图标。</summary>
    public static readonly StyledProperty<IconData?> IconEndProperty = AvaloniaProperty.Register<Button, IconData?>(nameof(IconEnd));

    /// <summary>占满容器宽度。</summary>
    public static readonly StyledProperty<bool> FullWidthProperty = AvaloniaProperty.Register<Button, bool>(nameof(FullWidth));

    /// <summary>色调的调色板：主题的色调样式按 <see cref="Tone"/> 设（<c>Rune.Palette.Accent</c>……），样式档位绑定它的字段。</summary>
    public static readonly StyledProperty<TonePalette?> PaletteProperty = AvaloniaProperty.Register<Button, TonePalette?>(nameof(Palette));

    /// <summary>焦点环比外框大出的距离：outline 宽 2、偏移 2（borderWidths.sm）。</summary>
    const double FocusRingOutset = 4;

    readonly DelayedLoading spinner;
    readonly Dictionary<AvaloniaProperty, IDisposable> bindings = [];
    Canvas? ripples;
    Border? focusRing;
    Action? releasePointerRipple;
    Action? releaseKeyRipple;

    /// <summary>新建按钮：颜色在 durations.sm 内过渡。</summary>
    public Button()
    {
        spinner = new DelayedLoading(shown => PseudoClasses.Set(":spinner", shown));
        AttachedToVisualTree += (_, _) => Transitions = ColorTransitions();
        DetachedFromVisualTree += (_, _) => spinner.Stop();
    }

    /// <summary>样式。</summary>
    public ButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>加载中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>按下时的波纹。</summary>
    public bool? Ripple { get => GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>文字后的图标。</summary>
    public IconData? IconEnd { get => GetValue(IconEndProperty); set => SetValue(IconEndProperty, value); }

    /// <summary>占满容器宽度。</summary>
    public bool FullWidth { get => GetValue(FullWidthProperty); set => SetValue(FullWidthProperty, value); }

    /// <summary>色调的调色板。</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public TonePalette? Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ripples = e.NameScope.Find<Canvas>("PART_Ripples");
        focusRing = e.NameScope.Find<Border>(ButtonParts.FocusRing);
        UpdateFocusRing();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty) PseudoClasses.Swap(change.GetOldValue<ButtonVariant>().Value, Variant.Value);
        else if (change.Property == ToneProperty) PseudoClasses.Swap(change.GetOldValue<StatusTone>().Value, Tone.Value);
        else if (change.Property == SizeProperty)
        {
            if (change.GetOldValue<TextSize>().Pixels is not null) ClearValue(FontSizeProperty);
            BindTo(FontSizeProperty, Size.ResourceKey is { } key ? this.GetResourceObservable(key).ToBinding() : null);
            if (Size.Pixels is { } pixels) SetValue(FontSizeProperty, pixels);
        }
        else if (change.Property == RadiusProperty)
            BindTo(CornerRadiusProperty, Radius.ResourceKey is { } key ? (BindingBase)new EmExtension { Resource = key }.ProvideValue(null!) : null);
        else if (change.Property == CornerRadiusProperty) UpdateFocusRing();
        else if (change.Property == FullWidthProperty)
        {
            if (FullWidth) HorizontalAlignment = HorizontalAlignment.Stretch;
            else ClearValue(HorizontalAlignmentProperty);
        }
        else if (change.Property == IsLoadingProperty)
        {
            PseudoClasses.Set(":loading", IsLoading);
            spinner.Update(IsLoading);
        }
    }

    /// <summary>加载中点击无效（焦点保留）；Command 也不执行。</summary>
    protected override void OnClick()
    {
        if (!IsLoading) base.OnClick();
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
    bool RippleOn => (Ripple ?? Avalonia.Ripple.Enabled) && IsEffectivelyEnabled && !IsLoading;

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

    /// <summary>背景、描边、文字颜色在 durations.sm 内过渡（vue-tsx-pro 的 interactionTransitions.colors）；系统关闭动画时不过渡。</summary>
    Transitions ColorTransitions()
    {
        var duration = Motion.Duration(this.TryFindResource("Rune.Duration.Sm", ActualThemeVariant, out var d) && d is TimeSpan t ? t : TimeSpan.FromMilliseconds(120));
        var easing = this.TryFindResource("Rune.Easing.Standard", ActualThemeVariant, out var s) && s is Easing found ? found : new LinearEasing();
        return
        [
            new BrushTransition { Property = BackgroundProperty, Duration = duration, Easing = easing },
            new BrushTransition { Property = BorderBrushProperty, Duration = duration, Easing = easing },
            new BrushTransition { Property = ForegroundProperty, Duration = duration, Easing = easing },
        ];
    }

    void BindTo(AvaloniaProperty property, BindingBase? binding)
    {
        if (bindings.Remove(property, out var old))
        {
            old.Dispose();
            ClearValue(property);
        }
        if (binding is not null) bindings[property] = Bind(property, binding);
    }
}

/// <summary>按钮的部件名：样式里用 <c>rune|Button /template/ rune|Icon#PART_Icon</c> 选中。</summary>
public static class ButtonParts
{
    /// <summary>内容层（包着图标与文字，TS 的 propsContent）。</summary>
    public const string Content = "PART_Content";

    /// <summary>前置图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>后置图标（TS 的 propsIconEnd）。</summary>
    public const string IconEnd = "PART_IconEnd";

    /// <summary>加载指示（TS 的 propsSpinner）。</summary>
    public const string Spinner = "PART_Spinner";

    /// <summary>键盘聚焦时的焦点环。</summary>
    public const string FocusRing = "PART_FocusRing";
}

/// <summary>按钮的样式（vue-tsx-pro 的 ButtonVariants）。</summary>
/// <param name="Value">取值（solid、soft、surface、outline、ghost、link）。</param>
[TypeConverter(typeof(VariantConverter<ButtonVariant>))]
public readonly record struct ButtonVariant(string Value) : IVariant<ButtonVariant>
{
    /// <summary>实底：一组操作里的主要操作，每组最多一个。</summary>
    public static readonly ButtonVariant Solid = new("solid");

    /// <summary>浅色底：次要但仍需突出的操作。</summary>
    public static readonly ButtonVariant Soft = new("soft");

    /// <summary>浅色底加描边：表单、设置页里常用的次要操作。</summary>
    public static readonly ButtonVariant Surface = new("surface");

    /// <summary>只有描边：次要操作。</summary>
    public static readonly ButtonVariant Outline = new("outline");

    /// <summary>无底无边，悬停时才有底色：工具栏、行内操作、图标按钮。</summary>
    public static readonly ButtonVariant Ghost = new("ghost");

    /// <summary>看起来像链接（无底、无内边距、悬停下划线）：文字中间的操作。</summary>
    public static readonly ButtonVariant Link = new("link");

    /// <inheritdoc />
    public static IReadOnlyList<ButtonVariant> Known { get; } = [Solid, Soft, Surface, Outline, Ghost, Link];

    /// <inheritdoc />
    public static ButtonVariant Parse(string value)
    {
        var variant = new ButtonVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
