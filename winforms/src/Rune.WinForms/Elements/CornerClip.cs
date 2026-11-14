namespace Rune.WinForms;

/// <summary>
/// 碰到圆角的子控件按圆角裁（TS 的 overflow: hidden）：卡片里贴边的横幅、带边框的描述列表的标签格不会盖住外框的圆角。
/// 子控件画在自己的窗口上，容器盖不住它们，只能给它们设窗口区域；只动自己裁过的，使用方自己设的区域不碰。
/// 一个容器一份（记着裁过哪些、按什么裁的：每次排版都会调用，裁法没变就不重设，设窗口区域要重画整个子窗口）。
/// </summary>
sealed class CornerClip
{
    readonly Dictionary<Control, (RectangleF Local, float Radius)> clipped = [];

    /// <summary>按圆角裁一组子控件。</summary>
    /// <param name="children">要看的子控件（同一个父控件下）。</param>
    /// <param name="inner">圆角矩形（父控件的坐标）。</param>
    /// <param name="radius">圆角半径；0 时去掉所有裁过的区域。</param>
    public void Apply(IEnumerable<Control> children, RectangleF inner, float radius)
    {
        var corner = (int)MathF.Ceiling(radius);
        foreach (var child in children)
        {
            var bounds = child.Bounds;
            var touches = radius > 0 && (bounds.Left < inner.Left + corner || bounds.Right > inner.Right - corner)
                && (bounds.Top < inner.Top + corner || bounds.Bottom > inner.Bottom - corner);
            if (!touches)
            {
                Release(child);
                continue;
            }
            var local = inner with { X = inner.X - bounds.X, Y = inner.Y - bounds.Y };
            if (clipped.TryGetValue(child, out var last) ? last == (local, radius) : child.Region is not null) continue;
            using var path = Drawing.RoundedRectangle(local, radius);
            clipped[child] = (local, radius);
            Swap(child, new Region(path));
        }
    }

    /// <summary>子控件移走时去掉裁过的区域。</summary>
    public void Release(Control child)
    {
        if (clipped.Remove(child)) Swap(child, null);
    }

    static void Swap(Control child, Region? region)
    {
        var old = child.Region;
        child.Region = region;
        old?.Dispose();
    }
}
