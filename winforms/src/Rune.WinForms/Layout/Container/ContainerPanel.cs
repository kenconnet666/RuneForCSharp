using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 限制内容最大宽度并水平居中（vue-tsx-pro 的 Container）：页面主体、表单页、文章。叫 ContainerPanel：WinForms 自己有 ContainerControl。
/// <code>
/// var page = new ContainerPanel { ContainerSize = ContainerSize.Sm, Dock = DockStyle.Fill };
/// page.Controls.Add(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Dock = DockStyle.Top });
/// </code>
/// 子控件的停靠、锚定都在居中的那一栏里（<see cref="DisplayRectangle"/>）：最大宽度是主题 sizes 的容器宽度（xs–xl：480–1280），
/// 左右内边距是 space 档位（默认 lg，按字体算），窄的时候内容不贴边。控件自己有 Size、Padding，TS 的 size、padding
/// 在这里叫 <see cref="ContainerSize"/>、<see cref="InlinePadding"/>。
/// </summary>
public class ContainerPanel : Panel
{
    /// <summary>最大宽度档位；不写时 lg。</summary>
    [Category("Layout")]
    [Description("最大宽度档位：xs、sm、md、lg（默认）、xl。")]
    public ContainerSize ContainerSize { get => field.Value is null ? ContainerSize.Lg : field; set { field = value; Relayout(); } }

    /// <summary>左右内边距档位；不写时 lg。</summary>
    [Category("Layout")]
    [Description("左右内边距档位，默认 lg。")]
    public Space InlinePadding { get => field.Value is null ? Space.Lg : field; set { field = value; Relayout(); } }

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    /// <summary>居中的那一栏：宽度不超过最大宽度，再去掉左右内边距。</summary>
    public override Rectangle DisplayRectangle
    {
        get
        {
            var area = base.DisplayRectangle;
            var width = Math.Min(area.Width, (int)MathF.Round(ContainerSize.Pixels * DeviceDpi / 96f));
            var inline = (int)MathF.Round((InlinePadding.Factor ?? 0) * Em);
            return new Rectangle(area.X + (area.Width - width) / 2 + inline, area.Y, Math.Max(0, width - 2 * inline), area.Height);
        }
    }

    bool ShouldSerializeContainerSize() => ContainerSize != ContainerSize.Lg;

    void ResetContainerSize() => ContainerSize = default;

    bool ShouldSerializeInlinePadding() => InlinePadding != Space.Lg;

    void ResetInlinePadding() => InlinePadding = default;

    void Relayout()
    {
        PerformLayout();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }
}

/// <summary>容器最大宽度档位（vue-tsx-pro 的 ContainerSizes，<see cref="Tokens.Size"/> 的容器宽度）。</summary>
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

    /// <summary>最大宽度（设备无关像素）；使用方扩展的取值按 lg。</summary>
    public float Pixels => Value switch
    {
        "xs" => Tokens.Size.Xs,
        "sm" => Tokens.Size.Sm,
        "md" => Tokens.Size.Md,
        "xl" => Tokens.Size.Xl,
        _ => Tokens.Size.Lg,
    };

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
