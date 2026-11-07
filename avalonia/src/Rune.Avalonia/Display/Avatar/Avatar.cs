using System.ComponentModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 头像（vue-tsx-pro 的 Avatar）：显示图片，没有图片时显示后备内容（默认是名字的首字母，没有名字时是人像图标）。
/// <code>
/// &lt;rune:Avatar Source="{Binding Photo}" DisplayName="{Binding Name}" /&gt;
/// &lt;rune:Avatar DisplayName="张三" Size="lg" Radius="md" /&gt;
/// &lt;rune:Avatar DisplayName="李四" Variant="solid" Tone="success" /&gt;
/// </code>
/// 边长是字号的 2.5 倍，默认圆形。后备内容的底色按状态色调（与 Badge 同一张调色板）：soft（默认）是浅底 + 色调文字色，solid 是实底 + 反色；
/// 色调默认 accent。读屏：有名字时是一张图片、名称是名字（图片本身不另念）；没有名字时是装饰。
/// TS 的 name、src 在这里叫 DisplayName、Source（Name 是控件名；图片给 IImage，地址要先解码成 Bitmap）。
/// 部件：<c>rune|Avatar Image#PART_Image</c>、<c>ContentPresenter#PART_Fallback</c>。
/// </summary>
public class Avatar : Control
{
    /// <summary>图片；没有时显示后备内容。</summary>
    public static readonly StyledProperty<IImage?> SourceProperty = AvaloniaProperty.Register<Avatar, IImage?>(nameof(Source));

    /// <summary>名字：读屏的名称，也用来生成首字母；不写时头像是装饰。</summary>
    public static readonly StyledProperty<string?> DisplayNameProperty = AvaloniaProperty.Register<Avatar, string?>(nameof(DisplayName));

    /// <summary>后备内容，默认是名字的首字母，没有名字时是人像图标。</summary>
    public static readonly StyledProperty<object?> FallbackProperty = AvaloniaProperty.Register<Avatar, object?>(nameof(Fallback));

    /// <summary>字号档位，头像边长是它的 2.5 倍；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Avatar, TextSize>(nameof(Size));

    /// <summary>圆角，主题里默认 full（圆形）；团队、组织、应用的图标常用 md。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<Avatar, Radius>(nameof(Radius));

    /// <summary>后备内容的底色样式，主题里默认 soft。</summary>
    public static readonly StyledProperty<AvatarVariant> VariantProperty = AvaloniaProperty.Register<Avatar, AvatarVariant>(nameof(Variant));

    /// <summary>后备内容的色调，主题里默认 accent。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Avatar, StatusTone>(nameof(Tone));

    /// <summary>色调的调色板：主题里的 setter 按 <see cref="Tone"/> 绑定，样式档位取它的字段。</summary>
    public static readonly StyledProperty<TonePalette?> PaletteProperty = AvaloniaProperty.Register<Avatar, TonePalette?>(nameof(Palette));

    /// <summary>字号（继承），边长按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Avatar>();

    static readonly StyledProperty<double> RadiusFactorProperty = AvaloniaProperty.Register<Avatar, double>("RadiusFactor");

    readonly Border root = new() { ClipToBounds = true };
    readonly Panel layers = new();
    readonly Image image = new() { Name = AvatarParts.Image, IsVisible = false };
    readonly ContentPresenter fallback = new() { Name = AvatarParts.Fallback };
    readonly TemplateResources resources;

    static Avatar() => AffectsMeasure<Avatar>(FontSizeProperty);

    /// <summary>新建：后备内容垫在图片下面。</summary>
    public Avatar()
    {
        resources = new TemplateResources(this);
        layers.Children.AddRange([fallback, image]);
        root.Child = layers;
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        UpdateFallback();
    }

    /// <summary>图片。</summary>
    public IImage? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>名字。</summary>
    public string? DisplayName { get => GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }

    /// <summary>后备内容。</summary>
    public object? Fallback { get => GetValue(FallbackProperty); set => SetValue(FallbackProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>后备内容的底色样式。</summary>
    public AvatarVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>后备内容的色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>色调的调色板。</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public TonePalette? Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>后备内容在显示吗。</summary>
    internal bool FallbackShown => fallback.IsVisible;

    /// <summary>名字的首字母（vue-tsx-pro 的 initials）：中文取最后一个字（通常是名），其他语言取前两个词的首字母（第一个与最后一个），大写。</summary>
    /// <param name="name">名字。</param>
    public static string Initials(string name) => AvatarInitials.Of(name);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty) Resolved.Bind(this, Resolved.PaletteProperty, Tone.PaletteKey);
        else if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == RadiusProperty) resources.Bind(RadiusFactorProperty, Radius.ResourceKey);
        else if (change.Property == VariantProperty || change.Property == PaletteProperty) UpdateColors();
        else if (change.Property == SourceProperty || change.Property == DisplayNameProperty || change.Property == FallbackProperty) UpdateFallback();
        else if (change.Property == FontSizeProperty || change.Property == RadiusFactorProperty) InvalidateArrange();
    }

    /// <summary>soft：浅底 + 色调文字色；solid：实底 + 反色（vue-tsx-pro 的 statusToneStyles）。</summary>
    void UpdateColors()
    {
        if (Palette is not { } p) return;
        var (back, fore) = Variant.Value == "solid" ? (p.Solid, p.Contrast) : (p.Soft, p.Text);
        root.Background = back;
        resources.Set(TextElement.ForegroundProperty, fore);
    }

    /// <summary>有图片时显示图片；没有时显示后备内容：给的、名字的首字母，或人像图标。</summary>
    void UpdateFallback()
    {
        image.Source = Source;
        image.IsVisible = Source is not null;
        fallback.IsVisible = Source is null;
        fallback.Content = Fallback ?? (string.IsNullOrWhiteSpace(DisplayName)
            ? new Icon { Data = Lucide.User }
            : new Text { Text = Initials(DisplayName), Weight = TextWeight.Medium, Wrap = TextWrap.NoWrap });
    }

    double Side => 2.5 * FontSize;

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        root.Measure(new LayoutSize(Side, Side));
        return new LayoutSize(Side, Side);
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        root.CornerRadius = new CornerRadius(Radius.Value == "full" ? Side / 2 : Math.Min(GetValue(RadiusFactorProperty) * FontSize, Side / 2));
        root.Arrange(new Rect(0, 0, Side, Side));
        return new LayoutSize(Side, Side);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AvatarAutomationPeer(this);

    /// <summary>有名字时是一张图片、名称是名字（里面的首字母、图片不另念）；没有名字时是装饰（TS 的 aria-hidden）。</summary>
    sealed class AvatarAutomationPeer(Avatar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string? GetNameCore() => owner.DisplayName;

        protected override bool IsControlElementCore() => !string.IsNullOrWhiteSpace(owner.DisplayName);

        protected override bool IsContentElementCore() => IsControlElementCore();

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>Avatar 的部件名。</summary>
public static class AvatarParts
{
    /// <summary>图片（TS 的 propsImage）。</summary>
    public const string Image = "PART_Image";

    /// <summary>后备内容（TS 的 propsFallback）。</summary>
    public const string Fallback = "PART_Fallback";
}

/// <summary>后备内容的底色样式（vue-tsx-pro 的 AvatarVariants）。</summary>
/// <param name="Value">取值（soft、solid）。</param>
[TypeConverter(typeof(VariantConverter<AvatarVariant>))]
public readonly record struct AvatarVariant(string Value) : IVariant<AvatarVariant>
{
    /// <summary>浅色底（默认）。</summary>
    public static readonly AvatarVariant Soft = new("soft");

    /// <summary>实底。</summary>
    public static readonly AvatarVariant Solid = new("solid");

    /// <inheritdoc />
    public static IReadOnlyList<AvatarVariant> Known { get; } = [Soft, Solid];

    /// <inheritdoc />
    public static AvatarVariant Parse(string value)
    {
        var variant = new AvatarVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
