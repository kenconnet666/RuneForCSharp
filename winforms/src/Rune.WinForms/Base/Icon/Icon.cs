using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 图标（vue-tsx-pro 的 Icon）：按 Lucide 的画法描边画一个 <see cref="IconData"/>。无窗口元素，放进窗体用 <see cref="IconControl"/>。
/// <code>
/// new IconControl { Data = Lucide.Search };                          // 装饰性
/// new IconControl { Data = Lucide.Trash, AccessibleName = "删除" };  // 有名称：读屏当作图片
/// </code>
/// 大小默认 1em，跟随宿主的字体；<see cref="Size"/> 改成档位或像素。颜色跟随宿主的 ForeColor，<see cref="Tone"/> 按状态着色（取当前主题）。
/// 从右到左排版里默认不翻转，有方向的图标设 <see cref="FlipInRtl"/>。
/// </summary>
public class Icon : Element
{
    /// <summary>图标数据：<c>Lucide.Search</c>（TS 的 icon；C# 的成员不能与类同名）。</summary>
    public IconData? Data
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Invalidate();
        }
    }

    /// <summary>大小：档位（xs–xl，取主题字号）或像素数；不写时 1em，跟随宿主的字体。</summary>
    public TextSize Size
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>色调（状态图标）；不写时跟随宿主的文字颜色。</summary>
    public Tone Tone
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Invalidate();
        }
    }

    /// <summary>线宽（24 格画布上的单位），默认 strokeWidths.md。</summary>
    public float StrokeWidth
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Invalidate();
        }
    } = Tokens.StrokeWidth.Md;

    /// <summary>从右到左排版时水平翻转：箭头、chevron 这类有方向的图标。</summary>
    public bool FlipInRtl
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var size = (int)MathF.Ceiling(ActualSize(context));
        return new Size(size, size);
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        if (Data is not { } data) return;
        var size = ActualSize(context);
        var color = Tone.ColorIn(context);
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            // 整数坐标落在像素边上：24 格上 2 宽的线正好盖满整像素，不发虚
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TranslateTransform(Bounds.X + MathF.Round((Bounds.Width - size) / 2), Bounds.Y + MathF.Round((Bounds.Height - size) / 2));
            graphics.ScaleTransform(size / IconData.Size, size / IconData.Size);
            if (context.RightToLeft && FlipInRtl)
            {
                graphics.TranslateTransform(IconData.Size, 0);
                graphics.ScaleTransform(-1, 1);
            }
            if (data.CreateFillPath() is { } fill)
            {
                using var brush = new SolidBrush(color);
                using (fill) graphics.FillPath(brush, fill);
            }
            using var path = data.CreatePath();
            using var pen = new Pen(color, StrokeWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            graphics.DrawPath(pen, path);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>实际的边长（设备像素）：像素数与档位按 DPI 换算，不写时 1em。</summary>
    float ActualSize(ElementContext context) => Size.Pixels is { } dip ? context.Scale(dip) : context.Em;
}
