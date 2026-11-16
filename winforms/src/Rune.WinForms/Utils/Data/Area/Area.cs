namespace Rune.WinForms;

/// <summary>省市区的树里的一个行政区：6 位代码、名称与下一级（最后一级没有）。</summary>
/// <param name="Code">6 位代码（前两位是省、前四位是市）。</param>
/// <param name="Name">名称。</param>
/// <param name="Children">下一级；最后一级为 null。</param>
public sealed record AreaNode(string Code, string Name, IReadOnlyList<AreaNode>? Children = null);

/// <summary>
/// 省市区（vue-tsx-pro 的 ui/area）：行政区划数据与它组成的树，交给 Cascader、TreeSelect 做联动选择。
/// <code>
/// cascader.ItemsSource = Area.Tree();                    // 省 / 市 / 区
/// cascader.ItemsSource = Area.Tree(depth: 2);            // 只到市
/// cascader.GetKey = (node, _) => Area.KeyOf(node); cascader.GetLabel = Area.LabelOf; cascader.GetChildren = Area.ChildrenOf;
/// Area.Names(["330000", "330100", "330106"]);           // 浙江省、杭州市、西湖区
/// </code>
/// 数据取自 @vant/area-data（MIT，见 licenses/vant-area-data.txt），由 tools/area-gen 生成进 <c>AreaData.g.cs</c>，第一次用到时解析。
/// </summary>
public static class Area
{
    static readonly Lazy<(Dictionary<string, string> Provinces, Dictionary<string, string> Cities, Dictionary<string, string> Counties)> lists = new(() =>
        (Parse(AreaData.Provinces), Parse(AreaData.Cities), Parse(AreaData.Counties)));

    static readonly Dictionary<int, IReadOnlyList<AreaNode>> trees = [];

    /// <summary>"代码=名称;"→ 按代码的字典，保持原来的先后（省市区按代码排好了）。</summary>
    static Dictionary<string, string> Parse(string text) =>
        text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(entry => entry.Split('=', 2)).ToDictionary(pair => pair[0], pair => pair[1]);

    /// <summary>
    /// 省 → 市 → 区县的树（<paramref name="depth"/>：1 省、2 省市、3 省市区）。直辖市这类没有市一级的，市一级用省的名字补上，保证三级都在。
    /// 同样的 depth 只建一次，返回的是同一个列表。
    /// </summary>
    public static IReadOnlyList<AreaNode> Tree(int depth = 3)
    {
        if (depth is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(depth), depth, "depth 只能是 1、2、3");
        lock (trees)
        {
            if (trees.TryGetValue(depth, out var cached)) return cached;
            var (provinces, cities, counties) = lists.Value;
            List<AreaNode> tree = [.. provinces.Select(province =>
            {
                if (depth == 1) return new AreaNode(province.Key, province.Value);
                var prefix = province.Key[..2];
                var own = cities.Where(city => city.Key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
                if (own.Count == 0) own = [new($"{prefix}0100", province.Value)];
                return new AreaNode(province.Key, province.Value, [.. own.Select(city => depth == 2
                    ? new AreaNode(city.Key, city.Value)
                    : new AreaNode(city.Key, city.Value, [.. counties.Where(county => county.Key.StartsWith(city.Key[..4], StringComparison.Ordinal)).Select(county => new AreaNode(county.Key, county.Value))]))]);
            })];
            trees[depth] = tree;
            return tree;
        }
    }

    /// <summary>取值函数（TS 的 areaAccessors.getKey）：节点的代码。</summary>
    public static string KeyOf(object node) => ((AreaNode)node).Code;

    /// <summary>取值函数（TS 的 areaAccessors.getLabel）：节点的名称。</summary>
    public static string LabelOf(object node) => ((AreaNode)node).Name;

    /// <summary>取值函数（TS 的 areaAccessors.getChildren）：下一级。</summary>
    public static IEnumerable<AreaNode>? ChildrenOf(object node) => ((AreaNode)node).Children;

    /// <summary>代码对应的名称（省、市、区县都行）；没有时 null。</summary>
    public static string? Name(string code)
    {
        var (provinces, cities, counties) = lists.Value;
        return counties.GetValueOrDefault(code) ?? cities.GetValueOrDefault(code) ?? provinces.GetValueOrDefault(code);
    }

    /// <summary>一条代码路径的名称（Cascader 的值 → 显示用的文字）；不认识的代码原样留着。</summary>
    public static IReadOnlyList<string> Names(IEnumerable<string> path) => [.. path.Select(code => Name(code) ?? code)];
}
