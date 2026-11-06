using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 按钮（vue-tsx-pro 的 Button）：外观 = 样式 × 色调 × 字号档位 × 圆角，内部尺寸都是 em，随字号缩放。
/// <code>
/// &lt;rune:Button Icon="{x:Static rune:Lucide.Plus}"&gt;新建项目&lt;/rune:Button&gt;
/// &lt;rune:Button Variant="outline" Tone="neutral"&gt;取消&lt;/rune:Button&gt;
/// &lt;rune:Button IsLoading="True"&gt;保存&lt;/rune:Button&gt;   &lt;!-- 显示旋转指示，宽度不变，点击无效，焦点保留 --&gt;
/// </code>
/// 从平台的 Button 派生：Click、Command、键盘、读屏的 Invoke 都现成。样式 × 色调的颜色是主题样式的 MultiTrigger
/// （tools/recipe-gen 按调色板表生成 ButtonRecipe.g.xaml，DESIGN §4），使用方任何 setter 都能盖过。
/// 部件（<see cref="ButtonParts"/>）引用部件样式键，Provider 的部件样式能改。
/// 效果同 TS：按下有波纹（<see cref="Wpf.Ripple"/>），背景色过渡，键盘聚焦时外面一圈焦点环，加载指示防闪烁（<see cref="DelayedLoading"/>）。
/// </summary>
[TemplatePart(Name = "PART_Ripples", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_Icon", Type = typeof(Icon))]
[TemplatePart(Name = "PART_IconEnd", Type = typeof(Icon))]
[TemplatePart(Name = "PART_ContentPresenter", Type = typeof(ContentPresenter))]
public class Button : System.Windows.Controls.Button
{
    /// <summary>样式，主题里默认 solid。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(ButtonVariant), typeof(Button), new FrameworkPropertyMetadata(default(ButtonVariant), (d, _) => ((Button)d).UpdateUnderline()));

    /// <summary>色调，主题里默认 accent。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(Button));

    /// <summary>字号档位，内部尺寸随之缩放；不写时继承外面的字号。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(Button), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    /// <summary>圆角，主题里默认 md。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(Radius), typeof(Button), new FrameworkPropertyMetadata(default(Radius), OnRadiusChanged));

    /// <summary>加载中：显示旋转指示，宽度不变，点击无效，焦点保留。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(Button), new FrameworkPropertyMetadata(false, (d, e) => ((Button)d).spinner.Update((bool)e.NewValue)));

    /// <summary>按下时的波纹；不写时沿用 <see cref="Wpf.Ripple.Enabled"/>（默认开启）。</summary>
    public static readonly DependencyProperty RippleProperty = DependencyProperty.Register(nameof(Ripple), typeof(bool?), typeof(Button));

    /// <summary>文字前的图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(IconData), typeof(Button), new FrameworkPropertyMetadata(null, (d, _) => ((Button)d).UpdateParts()));

    /// <summary>文字后的图标。</summary>
    public static readonly DependencyProperty IconEndProperty = DependencyProperty.Register(
        nameof(IconEnd), typeof(IconData), typeof(Button), new FrameworkPropertyMetadata(null, (d, _) => ((Button)d).UpdateParts()));

    /// <summary>占满容器宽度。</summary>
    public static readonly DependencyProperty FullWidthProperty = DependencyProperty.Register(
        nameof(FullWidth), typeof(bool), typeof(Button), new FrameworkPropertyMetadata(false, OnFullWidthChanged));

    /// <summary>圆角（主题按 <see cref="Radius"/> 设 em 系数，EmSize 算出实际值）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(Button),
        new FrameworkPropertyMetadata(default(CornerRadius), (d, _) => ((Button)d).UpdateClip(), CoerceCornerRadius));

    static readonly DependencyPropertyKey ShowsSpinnerKey = DependencyProperty.RegisterReadOnly(nameof(ShowsSpinner), typeof(bool), typeof(Button), new PropertyMetadata(false));

    /// <summary>加载指示正在显示（加载开始 200 毫秒后，至少 500 毫秒）：主题据此让内容透明、显示旋转指示。</summary>
    public static readonly DependencyProperty ShowsSpinnerProperty = ShowsSpinnerKey.DependencyProperty;

    static readonly DependencyPropertyKey IsFocusVisibleKey = DependencyProperty.RegisterReadOnly(nameof(IsFocusVisible), typeof(bool), typeof(Button), new PropertyMetadata(false));

    /// <summary>键盘聚焦（同 CSS 的 :focus-visible）：主题据此显示焦点环。指针点出来的焦点不显示。</summary>
    public static readonly DependencyProperty IsFocusVisibleProperty = IsFocusVisibleKey.DependencyProperty;

    /// <summary>焦点环比外框大出的距离：outline 宽 2、偏移 2（borderWidths.sm）。</summary>
    const double FocusRingOutset = 4;

    readonly DelayedLoading spinner;
    Canvas? ripples;
    Icon? icon;
    Icon? iconEnd;
    ContentPresenter? presenter;
    Border? focusRing;
    Action? releasePointerRipple;
    Action? releaseKeyRipple;

    static Button()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Button), new FrameworkPropertyMetadata(typeof(Button)));
        EmSize.Register(typeof(Button), CornerRadiusProperty);
    }

    /// <summary>新建按钮。</summary>
    public Button()
    {
        spinner = new DelayedLoading(shown => SetValue(ShowsSpinnerKey, shown));
        SizeChanged += (_, _) =>
        {
            CoerceValue(CornerRadiusProperty);
            UpdateClip();
        };
        Unloaded += (_, _) => spinner.Stop();
    }

    /// <summary>样式。</summary>
    public ButtonVariant Variant { get => (ButtonVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>加载中。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>按下时的波纹。</summary>
    public bool? Ripple { get => (bool?)GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>文字后的图标。</summary>
    public IconData? IconEnd { get => (IconData?)GetValue(IconEndProperty); set => SetValue(IconEndProperty, value); }

    /// <summary>占满容器宽度。</summary>
    public bool FullWidth { get => (bool)GetValue(FullWidthProperty); set => SetValue(FullWidthProperty, value); }

    /// <summary>圆角。</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>加载指示正在显示。</summary>
    [Browsable(false)]
    public bool ShowsSpinner => (bool)GetValue(ShowsSpinnerProperty);

    /// <summary>键盘聚焦。</summary>
    [Browsable(false)]
    public bool IsFocusVisible => (bool)GetValue(IsFocusVisibleProperty);

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ripples = GetTemplateChild("PART_Ripples") as Canvas;
        icon = GetTemplateChild("PART_Icon") as Icon;
        iconEnd = GetTemplateChild("PART_IconEnd") as Icon;
        presenter = GetTemplateChild("PART_ContentPresenter") as ContentPresenter;
        focusRing = GetTemplateChild("PART_FocusRing") as Border;
        UpdateParts();
        UpdateClip();
    }

    /// <summary>em 算出的圆角不超过高度的一半：WPF 的 Border 不像 CSS 那样把过大的圆角缩回来，radii.full 会画成椭圆而不是胶囊。</summary>
    static object CoerceCornerRadius(DependencyObject d, object baseValue)
    {
        var radius = (CornerRadius)EmSize.CoerceCornerRadius(d, baseValue);
        var max = ((Button)d).ActualHeight / 2;
        return max > 0 && radius.TopLeft > max ? new CornerRadius(max) : radius;
    }

    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (Button)d;
        if (button.Size.ResourceKey is { } key) button.SetResourceReference(FontSizeProperty, key);
        else button.ClearValue(FontSizeProperty);
        if (button.Size.Pixels is { } pixels) button.SetValue(FontSizeProperty, pixels);
    }

    static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (Button)d;
        if (button.Radius.ResourceKey is { } key) button.SetResourceReference(EmSize.CornerRadiusProperty, key);
        else button.ClearValue(EmSize.CornerRadiusProperty);
    }

    static void OnFullWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue) d.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        else d.ClearValue(HorizontalAlignmentProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) UpdateParts();
    }

    /// <summary>加载中点击无效（焦点保留）；Command 也不执行。</summary>
    protected override void OnClick()
    {
        if (!IsLoading) base.OnClick();
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
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateUnderline();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ReleasePointerRipple();
        UpdateUnderline();
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
    bool RippleOn => (Ripple ?? Wpf.Ripple.Enabled) && IsEnabled && !IsLoading;

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

    /// <summary>有图标才显示图标部件；图标与文字间距 0.5em（gap: sm）。</summary>
    void UpdateParts()
    {
        var gap = 0.5 * FontSize;
        if (icon is not null)
        {
            icon.Visibility = Icon is null ? Visibility.Collapsed : Visibility.Visible;
            icon.Margin = new Thickness(0, 0, Content is null ? 0 : gap, 0);
        }
        if (iconEnd is not null)
        {
            iconEnd.Visibility = IconEnd is null ? Visibility.Collapsed : Visibility.Visible;
            iconEnd.Margin = new Thickness(Content is null ? 0 : gap, 0, 0, 0);
        }
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

    /// <summary>链接样式悬停时文字加下划线（ContentPresenter 生成的 TextBlock 上）。</summary>
    void UpdateUnderline()
    {
        if (presenter is null || VisualTreeHelper.GetChildrenCount(presenter) == 0 || VisualTreeHelper.GetChild(presenter, 0) is not TextBlock text) return;
        text.TextDecorations = Variant == ButtonVariant.Link && IsMouseOver ? TextDecorations.Underline : null;
    }
}

/// <summary>按钮的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:ButtonParts.Icon}" TargetType="rune:Icon"&gt;</c>。</summary>
public static class ButtonParts
{
    /// <summary>内容层（包着图标与文字，TS 的 propsContent）。</summary>
    public static readonly PartKey Content = new(typeof(Button), nameof(Content), typeof(StackPanel));

    /// <summary>前置图标（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(Button), nameof(Icon), typeof(Icon));

    /// <summary>后置图标（TS 的 propsIconEnd）。</summary>
    public static readonly PartKey IconEnd = new(typeof(Button), nameof(IconEnd), typeof(Icon));

    /// <summary>加载指示（TS 的 propsSpinner）。</summary>
    public static readonly PartKey Spinner = new(typeof(Button), nameof(Spinner), typeof(Spinner));
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
