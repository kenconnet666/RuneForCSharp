using System.ComponentModel;
using global::Avalonia;
using global::Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 按钮家族（<see cref="Button"/>、<see cref="IconButton"/>）的外观内核（vue-tsx-pro 的 button-core）：
/// 外观 = 样式 × 色调 × 字号档位 × 圆角，内部尺寸都是 em，随字号缩放；加载指示防闪烁（<see cref="DelayedLoading"/>：
/// 很快完成的加载不出现，出现了至少显示 0.5 秒，伪类 <c>:spinner</c>）。
/// 色调、圆角在代码里解析成主题资源（<see cref="Resolved"/>：<c>Rune.Palette.*</c>、圆角系数），共用主题 <c>Rune.ButtonCore</c>
/// 里各一条 setter 绑到 <see cref="Palette"/>、CornerRadius，使用方的样式盖得过；字号档位按模板优先级绑定（字号是继承属性，<see cref="TemplateResources"/>）；
/// 样式档位绑定调色板的字段（DESIGN §4 决定 11），档位与色调切伪类（<c>:solid</c>、<c>:danger</c>）。各组件的主题 BasedOn
/// <c>Rune.ButtonCore</c>，只写自己的默认值与模板。使用方或 Provider 区域换某个色调的调色板，在样式里设 Palette（<c>rune|Button:danger</c>）。
/// </summary>
public abstract class ButtonCore : ButtonBase
{
    /// <summary>样式（Button 默认 solid，IconButton 默认 ghost，写在各自的主题里）。</summary>
    public static readonly StyledProperty<ButtonVariant> VariantProperty = AvaloniaProperty.Register<ButtonCore, ButtonVariant>(nameof(Variant));

    /// <summary>色调（Button 默认 accent，IconButton 默认 neutral）。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<ButtonCore, StatusTone>(nameof(Tone));

    /// <summary>字号档位，内部尺寸随之缩放；不写时继承外面的字号。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<ButtonCore, TextSize>(nameof(Size));

    /// <summary>圆角，主题里默认 md。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<ButtonCore, Radius>(nameof(Radius));

    /// <summary>图标（Button 里在文字前）。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<ButtonCore, IconData?>(nameof(Icon));

    /// <summary>色调的调色板：主题的色调样式按 <see cref="Tone"/> 设（<c>Rune.Palette.Accent</c>……），样式档位绑定它的字段。</summary>
    public static readonly StyledProperty<TonePalette?> PaletteProperty = AvaloniaProperty.Register<ButtonCore, TonePalette?>(nameof(Palette));

    readonly DelayedLoading spinner;
    readonly TemplateResources resources;

    /// <summary>新建按钮。</summary>
    protected ButtonCore()
    {
        spinner = new DelayedLoading(shown => PseudoClasses.Set(":spinner", shown));
        resources = new TemplateResources(this);
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

    /// <summary>图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>色调的调色板。</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public TonePalette? Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty) PseudoClasses.Swap(change.GetOldValue<ButtonVariant>().Value, Variant.Value);
        else if (change.Property == ToneProperty)
        {
            PseudoClasses.Swap(change.GetOldValue<StatusTone>().Value, Tone.Value);
            // 不写成 6 条色调样式：每个按钮省 6 个激活器（Avalonia 的创建开销随嵌套样式的条数涨，DESIGN §9）
            Resolved.Bind(this, Resolved.PaletteProperty, Tone.PaletteKey);
        }
        else if (change.Property == SizeProperty)
        {
            ControlHeight.Apply(this, Size);
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == RadiusProperty) Resolved.Bind(this, Resolved.RadiusProperty, Radius.ResourceKey);
        else if (change.Property == IsLoadingProperty) spinner.Update(IsLoading);
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
