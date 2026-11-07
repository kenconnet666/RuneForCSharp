namespace Rune.WinForms;

/// <summary>
/// 按钮（vue-tsx-pro 的 Button）：外观 = 样式 × 色调 × 字号档位 × 圆角（<see cref="ButtonCore"/>），内部尺寸都是 em。
/// 无窗口元素，放进窗体用 <see cref="ButtonControl"/>。内容是前置图标、文字、后置图标，间距 0.5em；链接样式悬停时文字加下划线。
/// </summary>
public class Button : ButtonCore
{
    readonly Text text = new() { Wrap = TextWrap.NoWrap };
    Icon? iconEnd;

    /// <summary>文字内容（TS 的 children）；加载中仍保留宽度。</summary>
    public string Content { get => text.Content; set { text.Content = value; InvalidateMeasure(); } }

    /// <summary>文字后的图标。</summary>
    public IconData? IconEnd { get; set { field = value; RebuildIconEnd(); } }

    /// <summary>占满宿主给的宽度。</summary>
    public bool FullWidth { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>贴着结束边的部件（命令面板默认触发按钮里快捷键的 Kbd）：有它时内容靠开始边，多出的宽度留在文字之后。</summary>
    internal Element? Trailing { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>后置图标部件的属性。</summary>
    public IconProps? PropsIconEnd { get; set { field = value; RebuildIconEnd(); } }

    /// <inheritdoc />
    protected override ButtonVariant DefaultVariant => ButtonVariant.Solid;

    /// <inheritdoc />
    protected override StatusTone DefaultTone => StatusTone.Accent;

    /// <summary>没写 <see cref="IconEnd"/> 时的后置图标（外部链接的外链图标）。</summary>
    protected virtual IconData? IconEndFallback => null;

    /// <summary>重新合成后置图标部件（<see cref="IconEndFallback"/> 变了时子类调用）。</summary>
    protected void RebuildIconEnd()
    {
        var data = IconEnd ?? IconEndFallback;
        iconEnd = data is null ? null : new Icon();
        if (iconEnd is not null) new IconProps { Data = data }.Merge(PropsIconEnd).ApplyTo(iconEnd);
        InvalidateMeasure();
    }

    /// <summary>显示的部件：没有文字时文字部件不占位。</summary>
    IEnumerable<Element> Parts()
    {
        if (IconPart is not null) yield return IconPart;
        if (text.Content.Length > 0) yield return text;
        if (iconEnd is not null) yield return iconEnd;
        if (Trailing is not null) yield return Trailing;
    }

    /// <summary>部件的大小（不限宽，不换行）。</summary>
    List<(Element Part, Size Size)> Measured(ElementContext inner) =>
        [.. Parts().Select(part => (part, part.Measure(inner, new Size(int.MaxValue, int.MaxValue))))];

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var size = base.Measure(context, available);
        return FullWidth && available.Width < int.MaxValue ? size with { Width = Math.Max(size.Width, available.Width) } : size;
    }

    /// <inheritdoc />
    protected override SizeF MeasureContent(ElementContext inner)
    {
        var parts = Measured(inner);
        var width = parts.Sum(p => p.Size.Width) + Math.Max(0, parts.Count - 1) * 0.5f * inner.Em;
        // 没有文字时仍给出一行的高度
        var height = Math.Max(parts.Select(p => p.Size.Height).DefaultIfEmpty(0).Max(), text.Measure(inner, new Size(int.MaxValue, int.MaxValue)).Height);
        return new SizeF(width, height);
    }

    /// <inheritdoc />
    protected override void PaintContent(Graphics graphics, ElementContext inner, Rectangle bounds)
    {
        var em = inner.Em;
        var parts = Measured(inner);
        var width = parts.Sum(p => p.Size.Width) + Math.Max(0, parts.Count - 1) * 0.5f * em;
        var x = Trailing is null ? bounds.X + (bounds.Width - width) / 2 : bounds.X;
        foreach (var (part, size) in parts)
        {
            if (part == Trailing) x = bounds.Right - size.Width;
            part.Bounds = new Rectangle((int)MathF.Round(x), bounds.Y + (bounds.Height - size.Height) / 2, size.Width, size.Height);
            part.Paint(graphics, inner);
            x += size.Width + 0.5f * em;
        }
        if (Variant != ButtonVariant.Link || !IsHovered || text.Content.Length == 0) return;
        // 链接样式悬停下划线（位置取文字底部往上一点，同 CSS 的 text-underline-offset 0.2em 量级）
        var underline = text.Bounds.Bottom - (int)(0.1f * em);
        using var pen = new Pen(inner.ForeColor, MathF.Max(1, 0.06f * em));
        graphics.DrawLine(pen, text.Bounds.Left, underline, text.Bounds.Right, underline);
    }
}
