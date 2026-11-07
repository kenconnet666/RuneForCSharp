namespace Rune.WinForms;

/// <summary>
/// 按钮家族（Button、IconButton、链接按钮、关闭与清空按钮）在作用域里的默认值（上游的 <c>defaults.Button</c>）：
/// <code>scope.Defaults.Set(new ButtonDefaults { Variant = ButtonVariant.Outline, Size = TextSize.Sm });</code>
/// 实例上写了的属性优先；没写的取最近一层作用域写了的字段，哪层都没写时取组件自己的默认（Button 是 solid、accent）。
/// </summary>
public sealed record ButtonDefaults : IScopeRecord<ButtonDefaults>
{
    /// <summary>样式。</summary>
    public ButtonVariant? Variant { get; init; }

    /// <summary>色调。</summary>
    public StatusTone? Tone { get; init; }

    /// <summary>字号档位。</summary>
    public TextSize? Size { get; init; }

    /// <summary>圆角。</summary>
    public Radius? Radius { get; init; }

    /// <inheritdoc />
    public ButtonDefaults Merge(ButtonDefaults? over) => over is null ? this : new()
    {
        Variant = over.Variant ?? Variant,
        Tone = over.Tone ?? Tone,
        Size = over.Size ?? Size,
        Radius = over.Radius ?? Radius,
    };
}

/// <summary>
/// 按钮家族的外观覆盖（上游的 <c>sx</c> 与 <c>components.Button.styles</c>）：作用域的 <see cref="ProviderControl.Styles"/> 给这片区域的按钮，
/// 实例的 <c>Sx</c> 盖在上面，都盖过配方给的颜色与尺寸。
/// <code>new ButtonControl { Text = "保存", Sx = new ButtonSx { Background = Color.SeaGreen, Foreground = Color.White } };</code>
/// 写了 Background 时悬停也用它（同上游：普通值整体替换档位给的颜色，连悬停）。
/// </summary>
public sealed record ButtonSx : IScopeRecord<ButtonSx>
{
    /// <summary>底色。</summary>
    public Color? Background { get; init; }

    /// <summary>文字与图标的颜色。</summary>
    public Color? Foreground { get; init; }

    /// <summary>描边的颜色。</summary>
    public Color? BorderColor { get; init; }

    /// <summary>圆角。</summary>
    public Radius? Radius { get; init; }

    /// <summary>左右内边距（em）。</summary>
    public float? PaddingInline { get; init; }

    /// <summary>上下内边距（em）。</summary>
    public float? PaddingBlock { get; init; }

    /// <inheritdoc />
    public ButtonSx Merge(ButtonSx? over) => over is null ? this : new()
    {
        Background = over.Background ?? Background,
        Foreground = over.Foreground ?? Foreground,
        BorderColor = over.BorderColor ?? BorderColor,
        Radius = over.Radius ?? Radius,
        PaddingInline = over.PaddingInline ?? PaddingInline,
        PaddingBlock = over.PaddingBlock ?? PaddingBlock,
    };
}
