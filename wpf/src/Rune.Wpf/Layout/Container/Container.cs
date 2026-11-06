using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf;

/// <summary>
/// 限制内容最大宽度并水平居中（vue-tsx-pro 的 Container）：页面主体、表单页、文章。
/// <code>
/// &lt;rune:Container Size="sm"&gt;&lt;rune:Flex Direction="column" Gap="md"&gt;…&lt;/rune:Flex&gt;&lt;/rune:Container&gt;
/// </code>
/// 最大宽度是主题 sizes 的容器宽度（xs–xl：480–1280），左右内边距是 space 档位（默认 lg，按字号算）：窄的时候内容不贴边。
/// 宽度撑满父元素，超过最大宽度时居中。
/// </summary>
public class Container : Decorator
{
    /// <summary>最大宽度档位，默认 lg。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(ContainerSize), typeof(Container), new FrameworkPropertyMetadata(default(ContainerSize), (d, _) => ((Container)d).BindLimit()));

    /// <summary>左右内边距档位，默认 lg。</summary>
    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding), typeof(Space), typeof(Container), new FrameworkPropertyMetadata(Space.Lg, (d, _) => ((Container)d).BindPadding()));

    static readonly DependencyProperty LimitProperty = DependencyProperty.Register(
        "Limit", typeof(double), typeof(Container), new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure));

    static readonly DependencyProperty PaddingFactorProperty = DependencyProperty.Register(
        "PaddingFactor", typeof(double), typeof(Container), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    static Container() => TextElement.FontSizeProperty.OverrideMetadata(typeof(Container), new FrameworkPropertyMetadata(
        SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>新建：最大宽度、内边距绑到主题。</summary>
    public Container()
    {
        BindLimit();
        BindPadding();
    }

    /// <summary>最大宽度档位。</summary>
    public ContainerSize Size { get => (ContainerSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>左右内边距档位。</summary>
    public Space Padding { get => (Space)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    double Limit => (double)GetValue(LimitProperty);

    double Inline => (double)GetValue(PaddingFactorProperty) * TextElement.GetFontSize(this);

    void BindLimit() => SetResourceReference(LimitProperty, (Size.Value is null ? ContainerSize.Lg : Size).ResourceKey);

    void BindPadding()
    {
        if (Padding.ResourceKey is { } key) SetResourceReference(PaddingFactorProperty, key);
        else ClearValue(PaddingFactorProperty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        var inline = Inline;
        if (Child is not { } child) return new Size(2 * inline, 0);
        child.Measure(new Size(Math.Max(0, Math.Min(constraint.Width, Limit) - 2 * inline), constraint.Height));
        return new Size(Math.Min(child.DesiredSize.Width + 2 * inline, Limit), child.DesiredSize.Height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        // 宽度撑满，超过最大宽度时居中（CSS 的 width: 100%、max-width、margin-inline: auto）
        var width = Math.Min(arrangeSize.Width, Limit);
        var inline = Inline;
        Child?.Arrange(new Rect((arrangeSize.Width - width) / 2 + inline, 0, Math.Max(0, width - 2 * inline), arrangeSize.Height));
        return arrangeSize;
    }
}

/// <summary>容器最大宽度档位（vue-tsx-pro 的 ContainerSizes，主题 sizes 的容器宽度）。</summary>
/// <param name="Value">取值（xs、sm、md、lg、xl）。</param>
[TypeConverter(typeof(VariantConverter<ContainerSize>))]
public readonly record struct ContainerSize(string Value) : IVariant<ContainerSize>
{
    /// <summary>480：窄内容，如登录表单。</summary>
    public static readonly ContainerSize Xs = new("xs");

    /// <summary>640：表单页、设置页。</summary>
    public static readonly ContainerSize Sm = new("sm");

    /// <summary>768：文章、详情页。</summary>
    public static readonly ContainerSize Md = new("md");

    /// <summary>1024：常规页面（默认）。</summary>
    public static readonly ContainerSize Lg = new("lg");

    /// <summary>1280：宽表格、仪表盘。</summary>
    public static readonly ContainerSize Xl = new("xl");

    /// <inheritdoc />
    public static IReadOnlyList<ContainerSize> Known { get; } = [Xs, Sm, Md, Lg, Xl];

    /// <summary>最大宽度的资源键（<c>Rune.Size.Lg</c>）；default 时为 null。</summary>
    public string? ResourceKey => Value is null ? null : $"Rune.Size.{char.ToUpperInvariant(Value[0])}{Value[1..]}";

    /// <inheritdoc />
    public static ContainerSize Parse(string value)
    {
        var size = new ContainerSize(value);
        Variants.WarnIfUnknown(size);
        return size;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
