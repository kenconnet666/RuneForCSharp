using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 限制内容最大宽度并水平居中（vue-tsx-pro 的 Container）：页面主体、表单页、文章。
/// <code>
/// &lt;rune:Container Size="sm"&gt;&lt;rune:Flex Direction="column" Gap="md"&gt;…&lt;/rune:Flex&gt;&lt;/rune:Container&gt;
/// </code>
/// 最大宽度是主题 sizes 的容器宽度（xs–xl：480–1280），左右内边距是 space 档位（默认 lg，按字号算）：窄的时候内容不贴边。
/// 宽度撑满父元素，超过最大宽度时居中。TS 的 padding 档位叫 <see cref="Padding"/>（Decorator 的 Padding 是 Thickness，所以不从它派生）。
/// </summary>
public class Container : Control
{
    /// <summary>最大宽度档位，默认 lg。</summary>
    public static readonly StyledProperty<ContainerSize> SizeProperty = AvaloniaProperty.Register<Container, ContainerSize>(nameof(Size));

    /// <summary>左右内边距档位，默认 lg。</summary>
    public static readonly StyledProperty<Space> PaddingProperty = AvaloniaProperty.Register<Container, Space>(nameof(Padding), Space.Lg);

    /// <summary>内容。</summary>
    public static readonly StyledProperty<Control?> ContentProperty = AvaloniaProperty.Register<Container, Control?>(nameof(Content));

    static readonly StyledProperty<double> LimitProperty = AvaloniaProperty.Register<Container, double>("Limit", double.PositiveInfinity);
    static readonly StyledProperty<double> PaddingFactorProperty = AvaloniaProperty.Register<Container, double>("PaddingFactor");

    readonly TemplateResources resources;

    static Container() => AffectsMeasure<Container>(LimitProperty, PaddingFactorProperty, TextElement.FontSizeProperty);

    /// <summary>新建：最大宽度、内边距绑到主题。</summary>
    public Container()
    {
        resources = new TemplateResources(this);
        resources.Bind(LimitProperty, ContainerSize.Lg.ResourceKey);
        resources.Bind(PaddingFactorProperty, Padding.ResourceKey);
    }

    /// <summary>最大宽度档位。</summary>
    public ContainerSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>左右内边距档位。</summary>
    public Space Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>内容。</summary>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    double Limit => GetValue(LimitProperty);

    double Inline => GetValue(PaddingFactorProperty) * GetValue(TextElement.FontSizeProperty);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty) resources.Bind(LimitProperty, (Size.Value is null ? ContainerSize.Lg : Size).ResourceKey);
        else if (change.Property == PaddingProperty) resources.Bind(PaddingFactorProperty, Padding.ResourceKey);
        else if (change.Property == ContentProperty)
        {
            if (change.OldValue is Control old)
            {
                LogicalChildren.Remove(old);
                VisualChildren.Remove(old);
            }
            if (change.NewValue is Control content)
            {
                LogicalChildren.Add(content);
                VisualChildren.Add(content);
            }
        }
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var inline = Inline;
        var width = Math.Min(availableSize.Width, Limit);
        if (Content is not { } content) return new LayoutSize(2 * inline, 0);
        content.Measure(new LayoutSize(Math.Max(0, width - 2 * inline), availableSize.Height));
        return new LayoutSize(Math.Min(content.DesiredSize.Width + 2 * inline, Limit), content.DesiredSize.Height);
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        // 宽度撑满，超过最大宽度时居中（CSS 的 width: 100%、max-width、margin-inline: auto）
        var width = Math.Min(finalSize.Width, Limit);
        var inline = Inline;
        Content?.Arrange(new Rect((finalSize.Width - width) / 2 + inline, 0, Math.Max(0, width - 2 * inline), finalSize.Height));
        return finalSize;
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
