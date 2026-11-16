using System.Numerics;

namespace Rune.Avalonia.Tests;

/// <summary>
/// TableQuery：翻页立即请求；换排序、条数、表头筛选、Params 回到第一页且只请求一次；查询总是重新请求；当前页空了退到最后一页；
/// 只翻页时带上已知的总数。ToParams：写成 query 库认的接口参数。
/// </summary>
public class TableQueryTests
{
    sealed record Filters(string Keyword);

    /// <summary>假接口：记下每次的查询条件；行按总数与页码算（同步完成，测试里不用等）。</summary>
    sealed class FakeApi
    {
        public readonly List<TableQueryRequest<Filters>> Queries = [];
        public int Total = 45;

        public Task<TablePage<int>> Fetch(TableQueryRequest<Filters> query, CancellationToken cancellation)
        {
            Queries.Add(query);
            var start = (query.Page - 1) * query.PageSize;
            var count = Math.Max(0, Math.Min(query.PageSize, Total - start));
            return Task.FromResult(new TablePage<int>([.. Enumerable.Range(start + 1, count)], Total));
        }
    }

    static (TableQuery<int, Filters> Table, FakeApi Api) Setup(IReadOnlyList<SortState>? sorts = null)
    {
        var api = new FakeApi();
        var table = new TableQuery<int, Filters>(api.Fetch, new Filters(""), sorts: sorts);
        _ = table.Refresh();
        return (table, api);
    }

    [Fact]
    [Upstream("use-table-query", "一开始按第一页、默认条数请求；结果给出 rows 与 total")]
    public void First_request_uses_page_one_and_default_size()
    {
        var (table, api) = Setup();
        var first = api.Queries[0];
        Assert.Equal((1, 10, 0, 0, (int?)null, new Filters("")), (first.Page, first.PageSize, first.Sorts.Count, first.ColumnFilters.Count, first.Total, first.Filters));
        Assert.Equal((10, 45, false), (table.Rows.Count, table.Total, table.IsLoading));
    }

    [Fact]
    [Upstream("use-table-query", "翻页立即请求；换排序、每页条数回到第一页")]
    public void Paging_requests_and_sort_or_size_go_back_to_page_one()
    {
        var (table, api) = Setup();
        table.Page = 3;
        Assert.Equal(3, api.Queries[^1].Page);
        Assert.Equal(21, table.Rows[0]);
        table.Sort = new SortState("Due", SortDirection.Descending);
        Assert.Equal((1, "Due"), (api.Queries[^1].Page, api.Queries[^1].Sorts[0].Key));
        table.Page = 2;
        var before = api.Queries.Count;
        table.PageSize = 20;
        // 回到第一页只请求一次
        Assert.Equal(before + 1, api.Queries.Count);
        Assert.Equal((1, 20), (api.Queries[^1].Page, api.Queries[^1].PageSize));
    }

    [Fact]
    [Upstream("use-table-query", "多列排序：sorts 前面的优先，sort 是第一列，设置 sort 换掉全部")]
    public void Sorts_keep_order_and_sort_replaces_them()
    {
        var (table, api) = Setup([new SortState("Due", SortDirection.Descending)]);
        table.Page = 2;
        table.Sorts = [.. table.Sorts, new SortState("Name", SortDirection.Ascending)];
        Assert.Equal((1, "Due,Name"), (api.Queries[^1].Page, string.Join(',', api.Queries[^1].Sorts.Select(sort => sort.Key))));
        Assert.Equal(new SortState("Due", SortDirection.Descending), table.Sort);
        table.Sort = null;
        Assert.Empty(api.Queries[^1].Sorts);
    }

    [Fact]
    [Upstream("use-table-query", "“查询”应用新条件并回到第一页；条件没变也重新请求")]
    public void Search_applies_filters_and_always_requests()
    {
        var (table, api) = Setup();
        table.Page = 2;
        var before = api.Queries.Count;
        table.Search(new Filters("研发"));
        Assert.Equal((1, new Filters("研发")), (api.Queries[^1].Page, api.Queries[^1].Filters));
        Assert.Equal(before + 1, api.Queries.Count);
        table.Search(new Filters("研发"));
        Assert.Equal(before + 2, api.Queries.Count);
    }

    [Fact]
    [Upstream("use-table-query", "params 变了回到第一页，只按新条件请求一次")]
    public void Params_change_goes_back_to_page_one_once()
    {
        var (table, api) = Setup();
        table.Params = "all";
        table.Page = 3;
        var before = api.Queries.Count;
        table.Params = "overdue";
        var after = api.Queries.Skip(before).ToList();
        Assert.Single(after);
        Assert.Equal((1, "overdue"), (after[0].Page, after[0].Params));
    }

    [Fact]
    [Upstream("use-table-query", "只翻页、换排序时带上已知的总数；表头筛选、“查询”、refresh 之后重新数")]
    public void Known_total_is_passed_while_only_paging_or_sorting()
    {
        var (table, api) = Setup();
        table.Page = 2;
        Assert.Equal((2, 45), (api.Queries[^1].Page, api.Queries[^1].Total));
        table.Sort = new SortState("Due", SortDirection.Ascending);
        Assert.Equal((1, 45), (api.Queries[^1].Page, api.Queries[^1].Total));
        table.ColumnFilters = new Dictionary<string, IReadOnlyList<string>> { ["Status"] = ["draft"] };
        Assert.Equal((1, (int?)null), (api.Queries[^1].Page, api.Queries[^1].Total));
        table.Page = 2;
        Assert.Equal(45, api.Queries[^1].Total);
        _ = table.Refresh();
        Assert.Null(api.Queries[^1].Total);
        table.Search(new Filters(""));
        Assert.Null(api.Queries[^1].Total);
    }

    [Fact]
    [Upstream("use-table-query", "删掉数据后当前页空了：退到最后一页")]
    public void Empty_page_after_deletion_moves_to_the_last_page()
    {
        var (table, api) = Setup();
        table.Page = 5;
        Assert.Equal([41, 42, 43, 44, 45], table.Rows);
        api.Total = 38;
        _ = table.Refresh();
        Assert.Equal((4, 38), (table.Page, table.Rows[^1]));
    }

    [Fact]
    public async Task Failure_keeps_rows_and_only_the_latest_result_counts()
    {
        var calls = 0;
        var slow = new TaskCompletionSource<TablePage<int>>();
        var table = new TableQuery<int, Filters>((query, _) => ++calls switch
        {
            1 => Task.FromResult(new TablePage<int>([1, 2], 2)),
            2 => Task.FromException<TablePage<int>>(new InvalidOperationException("网关超时")),
            3 => slow.Task,
            _ => Task.FromResult(new TablePage<int>([9], 1)),
        }, new Filters(""));
        await table.Refresh();
        await table.Refresh();
        // 失败时保留上一次的行
        Assert.Equal(("网关超时", 2), (table.Error?.Message, table.Rows.Count));
        var stale = table.Refresh();
        Assert.True(table.IsLoading);
        await table.Refresh();
        slow.SetResult(new TablePage<int>([7, 7, 7], 3));
        await stale;
        // 过期的结果丢掉
        Assert.Equal([9], table.Rows);
        Assert.Equal(((Exception?)null, false), (table.Error, table.IsLoading));
    }

    static readonly DataTableColumn[] Columns = [new("status", "状态") { Filter = ColumnFilter.Values }, new("supplier", "供应商") { Filter = ColumnFilter.Keyword }];

    static string Pairs(IEnumerable<KeyValuePair<string, string>> pairs) => string.Join(" ", pairs.Select(pair => $"{pair.Key}={pair.Value}"));

    [Fact]
    [Upstream("use-table-query", "写成 query 库 parseTableRequest 认的参数：sort、total、filter.、search.，查询栏的条件按线上的文字平铺，条件树写成 JSON")]
    public void Params_follow_the_query_library()
    {
        var filters = new Dictionary<string, object?>
        {
            ["keyword"] = "泵",
            ["min"] = 0.0000001m,
            ["id"] = BigInteger.Parse("9007199254740993"),
            ["created"] = new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)),
            ["amount"] = new int?[] { null, 500 },
            ["none"] = new int?[] { null, null },
            ["blank"] = "",
            ["missing"] = null,
            ["categories"] = new[] { "生产", "研发" },
            ["condition"] = Condition.Any(new ConditionRule("amount", ConditionOp.Gte, 1000)),
        };
        var query = new TableQueryRequest<Dictionary<string, object?>>(2, 20, [new("amount", SortDirection.Descending), new("id", SortDirection.Ascending)],
            new Dictionary<string, IReadOnlyList<string>> { ["status"] = ["draft", "paid"], ["supplier"] = [" 启航 "], ["empty"] = [" "] }, 368, filters, null);
        Assert.Equal(
            "page=2 pageSize=20 sort=-amount,id total=368 filter.status=draft filter.status=paid search.supplier= 启航  keyword=泵 min=0.0000001 id=9007199254740993 "
            + "created=2026-01-01 created=2026-01-31 amount= amount=500 categories=生产 categories=研发 "
            + """condition={"any":[{"field":"amount","op":"gte","value":1000}]}""",
            Pairs(query.ToParams(Columns)));
    }

    sealed record OrderFilters([property: System.Text.Json.Serialization.JsonPropertyName("keyword")] string Keyword, DateOnly? Since);

    [Fact]
    [Upstream("use-table-query", "没有排序、总数、表头筛选时只有页码与条数；不给 columns 时表头筛选都按多选")]
    public void Minimal_params_and_columns_default_to_multiple_choice()
    {
        var query = new TableQueryRequest<object?>(1, 10, [], new Dictionary<string, IReadOnlyList<string>> { ["supplier"] = ["启航"] }, null, null, null);
        Assert.Equal("page=1 pageSize=10 filter.supplier=启航", Pairs(query.ToParams()));
        // 对象的字段按属性名（JsonPropertyName 优先），空值不写
        var typed = new TableQueryRequest<OrderFilters>(1, 10, [], new Dictionary<string, IReadOnlyList<string>>(), null, new OrderFilters("泵", null), null);
        Assert.Equal("page=1 pageSize=10 keyword=泵", Pairs(typed.ToParams()));
        Assert.Equal("page=1&pageSize=10&keyword=%E6%B3%B5", TableQuery.QueryString(typed.ToParams()));
    }
}
