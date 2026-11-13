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
    public TextTone Tone
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

    /// <summary>
    /// 描线动画（一笔画出）：第一次画出来时为 true 就在那时播，之后每次由 false 变为 true 再播（时长 durations.xl、缓动 enter，
    /// 系统关闭动画时直接画好）。不写（null）的图标不建动画。元素不知道自己什么时候挂上，所以等第一次绘制才开始。
    /// </summary>
    public bool? Draw
    {
        get;
        set
        {
            if (field == value) return;
            if (value == true) drawPending = true;
            field = value;
            Invalidate();
        }
    }

    /// <summary>描线的进度：1 是画完（平常就是 1，只有描线时从 0 走到 1）。</summary>
    readonly Signal<double> drawn = new(1);
    bool drawPending;

    /// <summary>描线的进度（测试用）。</summary>
    internal double Drawn => drawn.Peek();

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
        if (drawPending)
        {
            // 先擦掉再画：这一帧就从 0 开始，不会先闪一下完整的图标
            drawPending = false;
            drawn.Value = 0;
            Animator.Animate(drawn, 1, Tokens.Duration.Xl, Easing.Enter);
        }
        var progress = drawn.Value;
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
            using var pen = new Pen(color, StrokeWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            if (progress < 1)
            {
                // 描线中：每一笔画出的部分连成折线
                foreach (var stroke in data.Strokes.Partial(progress).Where(stroke => stroke.Length > 1))
                    graphics.DrawLines(pen, [.. stroke.Select(point => new PointF((float)point.X, (float)point.Y))]);
                return;
            }
            using var path = data.CreatePath();
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
