using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 一块颜色（vue-tsx-pro 的 swatchStyles：ColorSwatch、ColorField、ColorPicker 共用）：棋盘格（fill 与 surface 相间）垫底，半透明的颜色看得出来；
/// 上面铺颜色；内描边是正文色 14% 的细线，亮色、暗色主题里都看得见色块的边界。尺寸由使用方给，圆角按 <see cref="RadiusKey"/> 的 em 系数。
/// </summary>
internal sealed class SwatchView : Control
{
    public SwatchView()
    {
        ClipToBounds = true;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>颜色。</summary>
    public IBrush? Fill
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    /// <summary>圆角的 token（em 系数），默认 md。</summary>
    public string RadiusKey { get; init; } = ThemeKeys.Radius.Md;

    /// <summary>棋盘格一格多大（像素）；不写时 0.5em（上游的 backgroundSize: 0.5em）。</summary>
    public double? Cell { get; init; }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var em = TextElement.GetFontSize(this);
        var radius = this.Resource<double>(RadiusKey) * em;
        var shape = new RoundedRect(bounds, radius);
        using (context.PushClip(shape))
        {
            context.FillRectangle(this.Resource<IBrush>(ThemeKeys.Brush.Surface), bounds);
            var checker = this.Resource<IBrush>(ThemeKeys.Brush.Fill);
            // 0.5em 见方的格子里棋盘格是 2×2，一格是它的一半
            var cell = Cell ?? em / 4;
            for (var y = 0; y * cell < bounds.Height; y++)
                for (var x = y % 2; x * cell < bounds.Width; x += 2)
                    context.FillRectangle(checker, new Rect(x * cell, y * cell, cell, cell));
            if (Fill is { } fill) context.FillRectangle(fill, bounds);
        }
        if (this.Resource<IBrush>(ThemeKeys.Brush.Text) is ISolidColorBrush text)
        {
            var ring = new Pen(new SolidColorBrush(text.Color, 0.14), 1);
            context.DrawRectangle(null, ring, new RoundedRect(bounds.Deflate(0.5), Math.Max(0, radius - 0.5)));
        }
    }
}
