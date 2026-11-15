using System.Numerics;
using System.Text.Json;

namespace Rune.WinForms.Tests;

/// <summary>JsonViewerControl：第一层展开、值按类型显示（BigInteger、decimal、日期照实）、全部展开 / 收起、选中后路径栏的路径；全部展开到最深时循环引用停下；搜索留下匹配的项与祖先；五千条只建看得见的行。</summary>
public class JsonViewerTests
{
    static Dictionary<string, object?> Sample()
    {
        var order = new Dictionary<string, object?>
        {
            ["id"] = BigInteger.Parse("9007199254740993"),
            ["unit-price"] = 12.50m,
            ["placedAt"] = new DateTimeOffset(2026, 11, 8, 8, 0, 0, TimeSpan.Zero),
            ["paid"] = true,
            ["note"] = null,
            ["items"] = new[] { new { sku = "A-1", qty = 2 } },
        };
        order["self"] = order;
        return order;
    }

    /// <summary>每一行的读屏名称（键名: 值）。WinForms 的行是画出来的，读不到画出来的字：项数另看 JsonViewerNodes.SizeOf。</summary>
    static List<string> Rows(JsonViewerControl viewer) =>
        [.. Enumerable.Range(0, viewer.TreePart.AccessibilityObject.GetChildCount()).Select(index => viewer.TreePart.AccessibilityObject.GetChild(index)!.Name!)];

    static AccessibleStates StateOf(JsonViewerControl viewer, string key) =>
        viewer.TreePart.AccessibilityObject.GetChild(viewer.TreePart.Rows.ToList().FindIndex(row => row.Key == key))!.State;

    static JsonViewerControl Create(object? value) => new() { Value = value, Width = 600, Dock = DockStyle.Top };

    [Fact]
    [Upstream("JsonViewer", "第一层的容器展开；bigint、Decimal、Date 照实显示；循环引用不无限展开")]
    public Task The_first_level_is_open_and_values_show_as_they_are() => WinFormsUi.Run(() =>
    {
        var viewer = Create(Sample());
        viewer.AccessibleName = "订单";
        using var form = WinFormsUi.Show(viewer);

        var rows = Rows(viewer);
        Assert.Contains("id: 9007199254740993", rows);
        Assert.Contains("unit-price: 12.50", rows);
        Assert.Contains("placedAt: 2026-11-08T08:00:00.000Z", rows);
        Assert.Contains("items: […]", rows);
        Assert.Contains("0: {…}", rows);
        Assert.Contains("self: [Circular]", rows);
        Assert.Equal(1, JsonViewerNodes.SizeOf((JsonViewerNode)viewer.TreePart.Rows.Single(row => row.Key == "$.items").Node));
        Assert.Equal("订单", viewer.TreePart.AccessibilityObject.Name);
        WinFormsUi.Paint(viewer.TreePart.View);
    });

    [Fact]
    [Upstream("JsonViewer", "全部收起、全部展开改的是各项的 aria-expanded")]
    public Task Collapse_all_and_expand_all_change_the_items() => WinFormsUi.Run(() =>
    {
        var viewer = Create(Sample());
        using var form = WinFormsUi.Show(viewer);

        viewer.Buttons.CollapseAll.PerformClick();
        Assert.True(StateOf(viewer, "$.items").HasFlag(AccessibleStates.Collapsed));
        viewer.Buttons.ExpandAll.PerformClick();
        Assert.True(StateOf(viewer, "$.items").HasFlag(AccessibleStates.Expanded));
        Assert.True(StateOf(viewer, "$.items[0]").HasFlag(AccessibleStates.Expanded));
    });

    [Fact]
    [Upstream("JsonViewer", "选中一项后路径栏显示它的路径")]
    public Task Selecting_shows_the_path() => WinFormsUi.Run(() =>
    {
        var viewer = Create(Sample());
        using var form = WinFormsUi.Show(viewer);
        Assert.True(viewer.PathBarParts.Hint.Visible);
        Assert.Equal(("搜索键或值", "全部展开"), (viewer.SearchPart.PlaceholderText, viewer.Buttons.ExpandAll.Text));

        viewer.TreePart.ClickItem("$[\"unit-price\"]");
        Assert.Equal("$[\"unit-price\"]", viewer.PathBarParts.Path.Text);
        Assert.Equal(("复制路径", "复制值"), (viewer.PathBarParts.CopyPath.AccessibleName, viewer.PathBarParts.CopyValue.AccessibleName));
        Assert.Equal(("$[\"unit-price\"]", "12.50"), (viewer.PathBarParts.CopyPath.Value, viewer.PathBarParts.CopyValue.Value));
        Assert.False(viewer.PathBarParts.Hint.Visible);
    });

    [Fact]
    [Upstream("JsonViewer", "全部展开到最深；循环引用显示 [Circular]")]
    public Task Expand_all_stops_at_cycles() => WinFormsUi.Run(() =>
    {
        var node = new Dictionary<string, object?> { ["name"] = "根", ["child"] = new Dictionary<string, object?> { ["leaf"] = 1 } };
        node["self"] = node;
        var viewer = Create(node);
        using var form = WinFormsUi.Show(viewer);

        viewer.Buttons.ExpandAll.PerformClick();
        Assert.Contains("self: [Circular]", Rows(viewer));
        Assert.Contains("leaf: 1", Rows(viewer));
    });

    [Fact]
    [Upstream("JsonViewer", "搜索留下匹配的项与祖先")]
    public Task Search_keeps_matches_and_ancestors() => WinFormsUi.Run(() =>
    {
        var response = JsonDocument.Parse("""
            { "code": "ok", "data": { "number": "SO-1", "items": [ { "sku": "MUG-01", "qty": 2 }, { "sku": "TEA-12", "name": "龙井", "qty": 1, "price": 1110.50 } ] } }
            """).RootElement;
        var viewer = Create(response);
        using var form = WinFormsUi.Show(viewer);

        viewer.SearchPart.Value = "TEA-12";
        WinFormsUi.Pump();
        Assert.Equal(["data: {…}", "items: […]", "1: {…}", "sku: \"TEA-12\""], Rows(viewer));
        WinFormsUi.Paint(viewer.TreePart.View);
    });

    [Fact]
    [Upstream("JsonViewer", "五千条只渲染看得见的行")]
    public Task Five_thousand_items_build_no_row_controls() => WinFormsUi.Run(() =>
    {
        var logs = Enumerable.Range(0, 5000).Select(index => new { id = index }).ToArray();
        var viewer = Create(logs);
        viewer.ExpandDepth = 0;
        using var form = WinFormsUi.Show(viewer);

        Assert.Empty(viewer.TreePart.View.Controls);
        Assert.Equal(5000, viewer.TreePart.Rows[0].SetSize);
        Assert.True(viewer.TreePart.Height < viewer.TreePart.View.Height);
    });

    [Fact]
    public void Nodes_follow_the_value_types_and_copy_as_json()
    {
        var roots = JsonViewerNodes.Roots(new Dictionary<string, object?>
        {
            ["tags"] = new HashSet<string> { "首单" },
            ["counts"] = new Dictionary<int, string> { [1] = "a" },
            ["uri"] = new Uri("https://example.com/a"),
            ["when"] = new DateOnly(2026, 11, 8),
            ["text"] = "say \"hi\"\n",
        });
        Assert.Equal(
            [JsonViewerKind.Set, JsonViewerKind.Map, JsonViewerKind.Other, JsonViewerKind.Date, JsonViewerKind.String],
            roots.Select(node => node.Kind));
        Assert.Equal("[...$.tags][0]", JsonViewerNodes.ChildrenOf(roots[0])![0].Path);
        Assert.Equal("$.counts.get(1)", JsonViewerNodes.ChildrenOf(roots[1])![0].Path);
        Assert.Equal(("\"say \\\"hi\\\"\\n\"", "say \"hi\"\n"), (JsonViewerNodes.Display(roots[4]), JsonViewerNodes.CopyText(roots[4])));
        Assert.Equal("2026-11-08", JsonViewerNodes.Display(roots[3]));

        var record = JsonViewerNodes.Roots(new { price = 1288.50m, items = new[] { "杯" } });
        var json = JsonViewerNodes.CopyText(JsonViewerNodes.Roots(new[] { new { price = 1288.50m, items = new[] { "杯" } } })[0]);
        Assert.Equal(JsonViewerKind.Number, record[0].Kind);
        Assert.Equal("{\n  \"price\": 1288.50,\n  \"items\": [\n    \"杯\"\n  ]\n}", json.ReplaceLineEndings("\n"));
    }
}
