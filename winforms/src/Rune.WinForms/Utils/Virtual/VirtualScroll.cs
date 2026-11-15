namespace Rune.WinForms;

/// <summary>可见范围（不含多建的项），两端都包含（vue-tsx-pro 的 VisibleRange）。</summary>
/// <param name="StartIndex">第一个可见项。</param>
/// <param name="EndIndex">最后一个可见项。</param>
public readonly record struct VisibleRange(int StartIndex, int EndIndex);

/// <summary>滚动到某一项时，它停在视口的什么位置（vue-tsx-pro 的 ScrollAligns）。</summary>
public enum ScrollAlign
{
    /// <summary>已经完整可见就不动，否则就近对齐开头或末尾。</summary>
    Auto,

    /// <summary>开头（横向时为起始端）对齐视口开头。</summary>
    Start,

    /// <summary>居中。</summary>
    Center,

    /// <summary>末端对齐视口末尾。</summary>
    End,
}

/// <summary>虚拟滚动的位置计算（vue-tsx-pro virtualizer 里与平台无关的部分）：按每项的起点与大小找可见范围、算滚到某一项的位置。</summary>
public static class VirtualScroll
{
    /// <summary>滚到一项时的滚动位置：按对齐方式，夹在 0 到最大滚动位置之间。</summary>
    /// <param name="start">这一项的起点（相对内容开头）。</param>
    /// <param name="size">这一项的大小。</param>
    /// <param name="offset">现在的滚动位置。</param>
    /// <param name="viewport">视口大小。</param>
    /// <param name="extent">内容的总大小。</param>
    /// <param name="align">对齐方式。</param>
    public static double OffsetFor(double start, double size, double offset, double viewport, double extent, ScrollAlign align)
    {
        var target = align switch
        {
            ScrollAlign.Start => start,
            ScrollAlign.Center => start + size / 2 - viewport / 2,
            ScrollAlign.End => start + size - viewport,
            // 完整可见就不动；比视口还大时对齐开头
            _ => start >= offset && start + size <= offset + viewport ? offset
                : start < offset || size > viewport ? start
                : start + size - viewport,
        };
        return Math.Clamp(target, 0, Math.Max(0, extent - viewport));
    }

    /// <summary>
    /// 可见范围：起点按顺序排好的各项里，与 [offset, offset + viewport) 相交的第一项与最后一项（二分查找）；没有项时为 null。
    /// </summary>
    /// <param name="starts">每一项的起点（递增）。</param>
    /// <param name="ends">每一项的终点。</param>
    /// <param name="offset">滚动位置。</param>
    /// <param name="viewport">视口大小。</param>
    public static VisibleRange? RangeOf(IReadOnlyList<double> starts, IReadOnlyList<double> ends, double offset, double viewport)
    {
        if (starts.Count == 0) return null;
        var first = Search(ends, offset, inclusive: false);
        var last = Search(starts, offset + viewport, inclusive: true) - 1;
        first = Math.Clamp(first, 0, starts.Count - 1);
        return new VisibleRange(first, Math.Clamp(last, first, starts.Count - 1));
    }

    /// <summary>第一个大于（inclusive 时大于等于）value 的位置；都不大于时是个数。</summary>
    static int Search(IReadOnlyList<double> sorted, double value, bool inclusive)
    {
        var (low, high) = (0, sorted.Count);
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (inclusive ? sorted[middle] < value : sorted[middle] <= value) low = middle + 1;
            else high = middle;
        }
        return low;
    }
}
