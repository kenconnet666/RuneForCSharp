using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 只有图标的按钮（vue-tsx-pro 的 IconButton）：宽高相等，默认 ghost 样式、中性色（一排图标不会满屏品牌色），其余外观同 <see cref="Button"/>。
/// 无窗口元素，放进窗体用 <see cref="IconButtonControl"/>；没有文字，宿主必须写 AccessibleName（TS 的 aria-label）。
/// </summary>
public class IconButton : ButtonCore
{
    /// <inheritdoc />
    protected override ButtonVariant DefaultVariant => ButtonVariant.Ghost;

    /// <inheritdoc />
    protected override StatusTone DefaultTone => StatusTone.Neutral;

    /// <inheritdoc />
    protected override bool Square => true;

    /// <summary>没有图标时也占一个图标（1em）的位置。</summary>
    protected override SizeF MeasureContent(ElementContext inner) =>
        IconPart?.Measure(inner, new Size(int.MaxValue, int.MaxValue)) ?? new SizeF(inner.Em, inner.Em);

    /// <inheritdoc />
    protected override void PaintContent(Graphics graphics, ElementContext inner, Rectangle bounds)
    {
        if (IconPart is null) return;
        var size = IconPart.Measure(inner, bounds.Size);
        IconPart.Bounds = new Rectangle(bounds.X + (bounds.Width - size.Width) / 2, bounds.Y + (bounds.Height - size.Height) / 2, size.Width, size.Height);
        IconPart.Paint(graphics, inner);
    }
}

/// <summary>
/// 放进窗体、设计器里用的 <see cref="IconButton"/>：没有文字，读屏名称写 <see cref="Control.AccessibleName"/>（TS 的 aria-label，必须写）。
/// 输入、焦点、无障碍见 <see cref="ButtonControlBase{T}"/>。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[DefaultProperty(nameof(Icon))]
[Description("图标按钮：宽高相等，默认 ghost、中性色，按下有波纹。")]
public class IconButtonControl : ButtonControlBase<IconButton>;
