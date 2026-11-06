using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 按钮家族（<see cref="Button"/>、<see cref="IconButton"/>）的外观内核（vue-tsx-pro 的 button-core）：
/// 外观 = 样式 × 色调 × 字号档位 × 圆角，内部尺寸都是 em，随字号缩放；加载指示防闪烁（<see cref="DelayedLoading"/>，<see cref="ShowsSpinner"/>）。
/// 样式 × 色调的颜色是主题样式的 MultiTrigger（tools/recipe-gen 按调色板表生成 ButtonRecipe.g.xaml，DESIGN §4），
/// 各组件的主题样式 BasedOn 它，使用方任何 setter 都能盖过。
/// </summary>
[TemplatePart(Name = "PART_ContentPresenter", Type = typeof(ContentPresenter))]
public abstract class ButtonCore : ButtonBase
{
    /// <summary>样式（Button 默认 solid，IconButton 默认 ghost，写在各自的主题样式里）。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(ButtonVariant), typeof(ButtonCore), new FrameworkPropertyMetadata(default(ButtonVariant), (d, _) => ((ButtonCore)d).UpdateUnderline()));

    /// <summary>色调（Button 默认 accent，IconButton 默认 neutral）。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(ButtonCore));

    /// <summary>字号档位，内部尺寸随之缩放；不写时继承外面的字号。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(ButtonCore), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    /// <summary>圆角，主题里默认 md。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(Radius), typeof(ButtonCore), new FrameworkPropertyMetadata(default(Radius), OnRadiusChanged));

    /// <summary>图标（Button 里在文字前）。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(ButtonCore));

    static readonly DependencyPropertyKey ShowsSpinnerKey = DependencyProperty.RegisterReadOnly(nameof(ShowsSpinner), typeof(bool), typeof(ButtonCore), new PropertyMetadata(false));

    /// <summary>加载指示正在显示（加载开始 200 毫秒后，至少 500 毫秒）：主题据此让内容透明、显示旋转指示。</summary>
    public static readonly DependencyProperty ShowsSpinnerProperty = ShowsSpinnerKey.DependencyProperty;

    readonly DelayedLoading spinner;
    ContentPresenter? presenter;

    /// <summary>新建按钮。</summary>
    protected ButtonCore()
    {
        spinner = new DelayedLoading(shown => SetValue(ShowsSpinnerKey, shown));
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

    /// <summary>图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>加载指示正在显示。</summary>
    [Browsable(false)]
    public bool ShowsSpinner => (bool)GetValue(ShowsSpinnerProperty);

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        presenter = GetTemplateChild("PART_ContentPresenter") as ContentPresenter;
    }

    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (ButtonCore)d;
        if (button.Size.ResourceKey is { } key) button.SetResourceReference(FontSizeProperty, key);
        else button.ClearValue(FontSizeProperty);
        if (button.Size.Pixels is { } pixels) button.SetValue(FontSizeProperty, pixels);
    }

    static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (ButtonCore)d;
        if (button.Radius.ResourceKey is { } key) button.SetResourceReference(EmSize.CornerRadiusProperty, key);
        else button.ClearValue(EmSize.CornerRadiusProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsLoadingProperty) spinner.Update((bool)e.NewValue);
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
        UpdateUnderline();
    }

    /// <summary>链接样式悬停时文字加下划线（ContentPresenter 生成的 TextBlock 上）。</summary>
    void UpdateUnderline()
    {
        if (presenter is null || VisualTreeHelper.GetChildrenCount(presenter) == 0 || VisualTreeHelper.GetChild(presenter, 0) is not TextBlock text) return;
        text.TextDecorations = Variant == ButtonVariant.Link && IsMouseOver ? TextDecorations.Underline : null;
    }
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

    /// <summary>看起来像链接（无底、无左右内边距、悬停下划线）：文字中间的操作。</summary>
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
