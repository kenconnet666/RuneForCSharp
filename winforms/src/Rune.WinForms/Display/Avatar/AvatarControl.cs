using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 头像（vue-tsx-pro 的 Avatar）：显示图片，没有图片时显示后备内容（默认是名字的首字母，没有名字时是人像图标）。
/// 无窗口元素，放进窗体用 <see cref="AvatarControl"/>。边长是字号的 2.5 倍，默认圆形；后备内容的底色按状态色调
/// （与 Badge 同一张调色板）：soft（默认）是浅底 + 色调文字色，solid 是实底 + 反色；色调默认 accent。图片按圆角裁剪、铺满（cover）。
/// </summary>
public class Avatar : Element
{
    readonly Text initials = new() { Weight = TextWeight.Medium, Wrap = TextWrap.NoWrap };
    readonly Icon person = new() { Data = Lucide.User };

    /// <summary>图片；没有时显示后备内容。</summary>
    public Image? Source { get; set { field = value; Invalidate(); } }

    /// <summary>名字：读屏的名称，也用来生成首字母。</summary>
    public string? DisplayName { get; set { field = value; UpdateText(); } }

    /// <summary>后备文字；不写时是名字的首字母，没有名字时是人像图标。</summary>
    public string? Fallback { get; set { field = value; UpdateText(); } }

    /// <summary>字号档位，头像边长是它的 2.5 倍；不写时取作用域的默认值（<see cref="AvatarDefaults"/>），再没有时跟随宿主。</summary>
    public TextSize Size { get => size.Or(ScopeDefaults?.Size, default); set { size = value; InvalidateMeasure(); } }

    /// <summary>圆角；不写时取作用域的默认值，再没有时 full（圆形）。</summary>
    public Radius Radius { get => radius.Or(ScopeDefaults?.Radius, Radius.Full); set { radius = value; Invalidate(); } }

    /// <summary>后备内容的底色样式；不写时取作用域的默认值，再没有时 soft。</summary>
    public AvatarVariant Variant { get => variant.Or(ScopeDefaults?.Variant, AvatarVariant.Soft); set { variant = value; Invalidate(); } }

    /// <summary>后备内容的色调；不写时取作用域的默认值，再没有时 accent。</summary>
    public StatusTone Tone { get => tone.Or(ScopeDefaults?.Tone, StatusTone.Accent); set { tone = value; Invalidate(); } }

    /// <summary>实例上写过的档位（设计器只序列化这些）。</summary>
    internal (bool Size, bool Radius, bool Variant, bool Tone) Explicit => (size.Value is not null, radius.Value is not null, variant.Value is not null, tone.Value is not null);

    AvatarDefaults? ScopeDefaults => Scope?.ResolveDefaults<AvatarDefaults>();

    TextSize size;
    Radius radius;
    AvatarVariant variant;
    StatusTone tone;

    /// <summary>画出来的后备文字（没有时画人像图标）。</summary>
    internal string FallbackText => initials.Content;

    void UpdateText()
    {
        initials.Content = Fallback ?? (string.IsNullOrWhiteSpace(DisplayName) ? "" : AvatarInitials.Of(DisplayName));
        Invalidate();
    }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var side = (int)MathF.Round(2.5f * context.Sized(Size).Em);
        return new Size(side, side);
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var inner = context.Sized(Size);
        var side = Math.Min(Bounds.Width, Bounds.Height);
        var box = new RectangleF(Bounds.X, Bounds.Y, side, side);
        var radius = Radius.Value == "full" ? side / 2f : (Radius.In(context.Theme.Radii) ?? 0) * inner.Em;
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var shape = Drawing.RoundedRectangle(box, radius);
            if (Source is { } image)
            {
                // cover：按短边铺满，居中裁掉多出来的
                graphics.SetClip(shape);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                var scale = Math.Max(box.Width / image.Width, box.Height / image.Height);
                var (w, h) = (image.Width * scale, image.Height * scale);
                graphics.DrawImage(image, box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
                return;
            }
            var (background, foreground, _) = BadgeRecipe.Colors(Variant.Value, Tone.Value, false, context.Theme.Colors);
            using (var brush = new SolidBrush(background ?? context.Theme.Colors.Fill)) graphics.FillPath(brush, shape);
            var fallback = inner with { ForeColor = foreground };
            Element part = initials.Content.Length > 0 ? initials : person;
            var size = part.Measure(fallback, Bounds.Size);
            part.Bounds = new Rectangle((int)(box.X + (side - size.Width) / 2f), (int)(box.Y + (side - size.Height) / 2f), size.Width, size.Height);
            part.Paint(graphics, fallback);
        }
        finally
        {
            graphics.Restore(state);
        }
    }
}

/// <summary>
/// 放进窗体的头像（单元素宿主）。读屏：有名字时是一张图片、名称是名字；没有名字时没有角色（装饰）。
/// <code>
/// var avatar = new AvatarControl { DisplayName = user.Name, Source = user.Photo };
/// </code>
/// TS 的 name、src、size 在这里叫 DisplayName、Source、TextSize（控件自己有 Name、Size）。
/// </summary>
public class AvatarControl : ElementControl<Avatar>
{
    /// <summary>新建：不能获得焦点。</summary>
    public AvatarControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        UpdateRole();
    }

    /// <summary>图片；没有时显示后备内容。</summary>
    [Category("Appearance")]
    [Description("图片；没有时显示后备内容（名字的首字母或人像图标）。")]
    [DefaultValue(null)]
    public Image? Source { get => Element.Source; set => Element.Source = value; }

    /// <summary>名字：读屏的名称，也用来生成首字母；不写时头像是装饰。</summary>
    [Category("Accessibility")]
    [Description("名字：读屏的名称，也用来生成首字母；不写时头像是装饰。")]
    [DefaultValue(null)]
    public string? DisplayName
    {
        get => Element.DisplayName;
        set
        {
            Element.DisplayName = value;
            UpdateRole();
        }
    }

    /// <summary>后备文字。</summary>
    [Category("Appearance")]
    [Description("后备文字；不写时是名字的首字母，没有名字时是人像图标。")]
    [DefaultValue(null)]
    public string? Fallback { get => Element.Fallback; set => Element.Fallback = value; }

    /// <summary>字号档位，头像边长是它的 2.5 倍。</summary>
    [Category("Appearance")]
    [Description("字号档位，头像边长是它的 2.5 倍；不写时跟随字体。")]
    public TextSize TextSize { get => Element.Size; set => Element.Size = value; }

    /// <summary>圆角。</summary>
    [Category("Appearance")]
    [Description("圆角，默认 full（圆形）；团队、组织、应用的图标常用 md。")]
    public Radius Radius { get => Element.Radius; set => Element.Radius = value; }

    /// <summary>后备内容的底色样式。</summary>
    [Category("Appearance")]
    [Description("后备内容的底色样式：soft（默认）、solid。")]
    public AvatarVariant Variant { get => Element.Variant; set => Element.Variant = value; }

    /// <summary>后备内容的色调。</summary>
    [Category("Appearance")]
    [Description("后备内容的色调，默认 accent。")]
    public StatusTone Tone { get => Element.Tone; set => Element.Tone = value; }

    bool ShouldSerializeTextSize() => Element.Explicit.Size;

    void ResetTextSize() => TextSize = default;

    bool ShouldSerializeRadius() => Element.Explicit.Radius;

    void ResetRadius() => Radius = default;

    bool ShouldSerializeVariant() => Element.Explicit.Variant;

    void ResetVariant() => Variant = default;

    bool ShouldSerializeTone() => Element.Explicit.Tone;

    void ResetTone() => Tone = default;

    void UpdateRole()
    {
        var named = !string.IsNullOrWhiteSpace(DisplayName);
        AccessibleRole = named ? AccessibleRole.Graphic : AccessibleRole.None;
        AccessibleName = named ? DisplayName : null;
    }
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

/// <summary>Avatar 在作用域里的默认值（上游的 <c>defaults.Avatar</c>）。</summary>
public sealed record AvatarDefaults : IScopeRecord<AvatarDefaults>
{
    /// <summary>字号档位。</summary>
    public TextSize? Size { get; init; }

    /// <summary>圆角。</summary>
    public Radius? Radius { get; init; }

    /// <summary>后备内容的底色样式。</summary>
    public AvatarVariant? Variant { get; init; }

    /// <summary>后备内容的色调。</summary>
    public StatusTone? Tone { get; init; }

    /// <inheritdoc />
    public AvatarDefaults Merge(AvatarDefaults? over) => over is null ? this : new() { Size = over.Size ?? Size, Radius = over.Radius ?? Radius, Variant = over.Variant ?? Variant, Tone = over.Tone ?? Tone };
}
