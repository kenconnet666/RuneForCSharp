namespace Rune.Avalonia;

/// <summary>
/// 分页的页码序列（vue-tsx-pro 的 paginationItems，算法同 MUI 的 usePagination）：首尾各留 boundaries 页、当前页左右各留 siblings 页，
/// 中间用省略号；序列长度固定（页数够多时总是 2 × boundaries + 2 × siblings + 3 项），翻页时按钮不跳动、不会让指针下的按钮变成别的页码。
/// <code>
/// PaginationItems.Of(page: 6, pageCount: 20)  // [1, null, 5, 6, 7, null, 20]（null 是省略号）
/// </code>
/// </summary>
public static class PaginationItems
{
    /// <summary>要显示的页码与省略号（null），依次排列。</summary>
    /// <param name="page">当前页，从 1 开始。</param>
    /// <param name="pageCount">总页数。</param>
    /// <param name="siblings">当前页左右各显示几页。</param>
    /// <param name="boundaries">首尾各固定显示几页。</param>
    public static IReadOnlyList<int?> Of(int page, int pageCount, int siblings = 1, int boundaries = 1)
    {
        var startPages = Range(1, Math.Min(boundaries, pageCount));
        var endPages = Range(Math.Max(pageCount - boundaries + 1, boundaries + 1), pageCount);
        // 当前页这一组的起止：靠近两端时向另一侧多借几页，保持总项数不变
        var siblingsStart = Math.Max(Math.Min(page - siblings, pageCount - boundaries - siblings * 2 - 1), boundaries + 2);
        var siblingsEnd = Math.Min(Math.Max(page + siblings, boundaries + siblings * 2 + 2), endPages.Count > 0 ? endPages[0] - 2 : pageCount - 1);
        // 省略号只代替两页以上；只隔一页时直接显示那一页
        int?[] before = siblingsStart > boundaries + 2 ? [null] : boundaries + 1 < pageCount - boundaries ? [boundaries + 1] : [];
        int?[] after = siblingsEnd < pageCount - boundaries - 1 ? [null] : pageCount - boundaries > boundaries ? [pageCount - boundaries] : [];
        return [.. startPages.Select(n => (int?)n), .. before, .. Range(siblingsStart, siblingsEnd).Select(n => (int?)n), .. after, .. endPages.Select(n => (int?)n)];
    }

    /// <summary>总页数：写了页数用它，否则按总条数与每页条数算；至少 1 页。</summary>
    public static int PageCount(int? pageCount, int? total, int pageSize) =>
        Math.Max(1, pageCount ?? (int)Math.Ceiling((total ?? 0) / (double)Math.Max(1, pageSize)));

    static List<int> Range(int from, int to) => [.. Enumerable.Range(from, Math.Max(0, to - from + 1))];
}
