using System.Numerics;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>JsonViewer：第一层展开、值按类型显示（BigInteger、decimal、日期照实）、全部展开 / 收起、选中后路径栏的路径；全部展开到最深时循环引用停下；搜索留下匹配的项与祖先；五千条只建看得见的行。</summary>
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

    /// <summary>每一行的文字（同上游的 textContent：键名、值、项数连在一起）。</summary>
    static List<string> Rows(JsonViewer viewer) =>
        [.. viewer.TreePart.RealizedItems.OrderBy(item => item.Row!.Key == null ? 0 : viewer.TreePart.Rows.ToList().FindIndex(row => row.Key == item.Row.Key))
            .Select(item => string.Concat(item.GetLogicalDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text)))];

    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));

    static ExpandCollapseState ExpandedOf(JsonViewer viewer, string key) =>
        ((IExpandCollapseProvider)viewer.TreePart.RealizedItems.Single(item => item.Row?.Key == key).Peer()).ExpandCollapseState;

    [Fact]
    [Upstream("JsonViewer", "第一层的容器展开；bigint、Decimal、Date 照实显示；循环引用不无限展开")]
    public Task The_first_level_is_open_and_values_show_as_they_are() => HeadlessUi.Run(() =>
    {
        var viewer = new JsonViewer { Value = Sample(), Width = 600 };
        AutomationProperties.SetName(viewer, "订单");
        HeadlessUi.Flush(HeadlessUi.Show(viewer));

        var rows = Rows(viewer);
        Assert.Contains("id:9007199254740993", rows);
        Assert.Contains("unit-price:12.50", rows);
        Assert.Contains("placedAt:2026-11-08T08:00:00.000Z", rows);
        Assert.Contains("items:[…]1 项", rows);
        Assert.Contains("0:{…}2 项", rows);
        Assert.Contains("self:[Circular]", rows);
        Assert.Equal("订单", viewer.TreePart.Peer().GetName());
    });

    [Fact]
    [Upstream("JsonViewer", "全部收起、全部展开改的是各项的 aria-expanded")]
    public Task Collapse_all_and_expand_all_change_the_items() => HeadlessUi.Run(() =>
    {
        var viewer = new JsonViewer { Value = Sample(), Width = 600 };
        var window = HeadlessUi.Show(viewer);
        HeadlessUi.Flush(window);

        Click(viewer.Buttons.CollapseAll);
        HeadlessUi.Flush(window);
        Assert.Equal(ExpandCollapseState.Collapsed, ExpandedOf(viewer, "$.items"));
        Click(viewer.Buttons.ExpandAll);
        HeadlessUi.Flush(window);
        Assert.Equal([ExpandCollapseState.Expanded, ExpandCollapseState.Expanded], [ExpandedOf(viewer, "$.items"), ExpandedOf(viewer, "$.items[0]")]);
    });

    [Fact]
    [Upstream("JsonViewer", "选中一项后路径栏显示它的路径")]
    public Task Selecting_shows_the_path() => HeadlessUi.Run(() =>
    {
        var viewer = new JsonViewer { Value = Sample(), Width = 600 };
        var window = HeadlessUi.Show(viewer);
        HeadlessUi.Flush(window);
        Assert.True(viewer.PathBarParts.Hint.IsEffectivelyVisible);

        viewer.TreePart.Click("$[\"unit-price\"]");
        HeadlessUi.Flush(window);
        Assert.Equal("$[\"unit-price\"]", viewer.PathBarParts.Path.Text);
        Assert.Equal(("复制路径", "复制值"), (AutomationProperties.GetName(viewer.PathBarParts.CopyPath), AutomationProperties.GetName(viewer.PathBarParts.CopyValue)));
        Assert.Equal(("$[\"unit-price\"]", "12.50"), (viewer.PathBarParts.CopyPath.Value, viewer.PathBarParts.CopyValue.Value));
        Assert.False(viewer.PathBarParts.Hint.IsEffectivelyVisible);
    });

    [Fact]
    [Upstream("JsonViewer", "全部展开到最深；循环引用显示 [Circular]")]
    public Task Expand_all_stops_at_cycles() => HeadlessUi.Run(() =>
    {
        var node = new Dictionary<string, object?> { ["name"] = "根", ["child"] = new Dictionary<string, object?> { ["leaf"] = 1 } };
        node["self"] = node;
        var viewer = new JsonViewer { Value = node, Width = 600, TreeMaxHeight = 40 * 16 };
        var window = HeadlessUi.Show(viewer);
        HeadlessUi.Flush(window);

        Click(viewer.Buttons.ExpandAll);
        HeadlessUi.Flush(window);
        Assert.Contains("self:[Circular]", Rows(viewer));
        Assert.Contains("leaf:1", Rows(viewer));
    });

    [Fact]
    [Upstream("JsonViewer", "搜索留下匹配的项与祖先")]
    public Task Search_keeps_matches_and_ancestors() => HeadlessUi.Run(() =>
    {
        var response = JsonDocument.Parse("""
            { "code": "ok", "data": { "number": "SO-1", "items": [ { "sku": "MUG-01", "qty": 2 }, { "sku": "TEA-12", "name": "龙井", "qty": 1, "price": 1110.50 } ] } }
            """).RootElement;
        var viewer = new JsonViewer { Value = response, Width = 600 };
        var window = HeadlessUi.Show(viewer);
        HeadlessUi.Flush(window);

        viewer.SearchPart.Text = "TEA-12";
        HeadlessUi.Flush(window);
        Assert.Equal(["data:{…}2 项", "items:[…]2 项", "1:{…}4 项", "sku:\"TEA-12\""], Rows(viewer));
    });

    [Fact]
    [Upstream("JsonViewer", "五千条只渲染看得见的行")]
    public Task Five_thousand_items_realize_only_visible_rows() => HeadlessUi.Run(() =>
    {
        var logs = Enumerable.Range(0, 5000).Select(index => new { id = index }).ToArray();
        var viewer = new JsonViewer { Value = logs, ExpandDepth = 0, Width = 600 };
        var window = HeadlessUi.Show(viewer);
        HeadlessUi.Flush(window);

        Assert.InRange(viewer.TreePart.RealizedItems.Count(), 6, 59);
        Assert.Equal(5000, viewer.TreePart.Rows[0].SetSize);
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
