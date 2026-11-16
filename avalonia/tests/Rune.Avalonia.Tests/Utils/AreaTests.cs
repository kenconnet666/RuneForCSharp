namespace Rune.Avalonia.Tests;

/// <summary>省市区：三级的树（直辖市补上市一级）、按层数截断并缓存、代码查名称。</summary>
public class AreaTests
{
    [Fact]
    [Upstream("area", "省 → 市 → 区县；代码前缀对得上")]
    public void Tree_nests_counties_under_cities()
    {
        var zhejiang = Area.Tree().Single(province => province.Code == "330000");
        var hangzhou = zhejiang.Children!.Single(city => city.Code == "330100");
        Assert.Equal("杭州市", hangzhou.Name);
        Assert.Contains("西湖区", hangzhou.Children!.Select(county => county.Name));
        Assert.All(hangzhou.Children!, county => Assert.StartsWith("3301", county.Code));
    }

    [Fact]
    [Upstream("area", "直辖市有市一级，三级都在")]
    public void Municipalities_get_a_city_level()
    {
        var beijing = Area.Tree().Single(province => province.Code == "110000");
        Assert.NotEmpty(beijing.Children!);
        Assert.Contains("东城区", beijing.Children![0].Children!.Select(county => county.Name));
    }

    [Fact]
    [Upstream("area", "depth 截断层数；同样的 depth 返回同一个数组")]
    public void Depth_cuts_levels_and_is_cached()
    {
        Assert.Null(Area.Tree(depth: 1)[0].Children);
        Assert.Null(Area.Tree(depth: 2)[0].Children![0].Children);
        Assert.Same(Area.Tree(depth: 2), Area.Tree(depth: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => Area.Tree(depth: 4));
    }

    [Fact]
    [Upstream("area", "代码查名称；取值函数给 Cascader")]
    public void Names_and_accessors()
    {
        Assert.Equal(["浙江省", "杭州市", "西湖区", "999999"], Area.Names(["330000", "330100", "330106", "999999"]));
        Assert.Equal("北京市", Area.Name("110000"));
        var node = Area.Tree()[0];
        Assert.Equal([node.Code, node.Name], [Area.KeyOf(node), Area.LabelOf(node)]);
        Assert.Same(node.Children, Area.ChildrenOf(node));
    }
}
