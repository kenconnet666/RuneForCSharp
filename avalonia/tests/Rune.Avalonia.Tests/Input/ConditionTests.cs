using System.Numerics;

namespace Rune.Avalonia.Tests;

/// <summary>条件树：各种比较方式的求值（数字文字与数字相等、精确的十进制、编码顺序、数组字段、介于的开放端、文字不分大小写、空值），组合与空组；写成一句话；JSON 往返。</summary>
public class ConditionTests
{
    static readonly Dictionary<string, object?> Order = new()
    {
        ["amount"] = 1200, ["code"] = "PO-17", ["category"] = "steel", ["note"] = "", ["tags"] = Array.Empty<string>(), ["due"] = "2026-11-03",
    };

    static ConditionRule Rule(string field, ConditionOp op, object? value = null) => new(field, op, value);

    static bool Yes(Condition condition, object? record = null) => condition.Matches(record ?? Order);

    [Fact]
    [Upstream("condition", "比较：大小按数值（数字文字也算），日期按文字先后，介于含两端、空的一端不限")]
    public void Compares_numbers_dates_and_ranges()
    {
        Assert.True(Yes(Rule("amount", ConditionOp.Eq, "1200")));
        Assert.True(Yes(Rule("amount", ConditionOp.Gt, 999)));
        Assert.False(Yes(Rule("amount", ConditionOp.Lte, 1000)));
        Assert.True(Yes(Rule("due", ConditionOp.Lt, "2026-12-01")));
        Assert.True(Yes(Rule("amount", ConditionOp.Between, new object?[] { 1000, 1200 })));
        Assert.False(Yes(Rule("amount", ConditionOp.Between, new object?[] { null, 1000 })));
        Assert.True(Yes(Rule("amount", ConditionOp.Between, new object?[] { 1000, null })));
        // 没有这个字段：大小比较都不成立
        Assert.False(Yes(Rule("missing", ConditionOp.Gt, 0)));
    }

    [Fact]
    [Upstream("condition", "数值按精确的十进制比（与流程引擎的模型包同一套规则）：长小数、大整数、Decimal 不丢精度；文字按编码顺序")]
    public void Compares_exact_decimals_and_ordinal_text()
    {
        var row = new Dictionary<string, object?>
        {
            ["big"] = "123456789012345678901234567890.01", ["id"] = BigInteger.Parse("12345678901234567890"), ["price"] = 19.90m, ["code"] = "b",
        };
        Assert.True(Yes(Rule("big", ConditionOp.Gt, "123456789012345678901234567890"), row));
        Assert.True(Yes(Rule("big", ConditionOp.Lt, "123456789012345678901234567890.02"), row));
        Assert.True(Yes(Rule("id", ConditionOp.Eq, "12345678901234567890"), row));
        Assert.True(Yes(Rule("id", ConditionOp.Gt, "12345678901234567889"), row));
        Assert.True(Yes(Rule("price", ConditionOp.Eq, "19.9"), row));
        Assert.True(Yes(Rule("price", ConditionOp.Between, new object?[] { "19.89", "1.99e1" }), row));
        // 编码顺序：大写字母排在小写前面，不随语言环境变
        Assert.True(Yes(Rule("code", ConditionOp.Gt, "B"), row));
    }

    [Fact]
    [Upstream("condition", "字段的值是数组：等于、是其中之一是\"有一项满足\"，不等于、不在其中是\"没有一项满足\"")]
    public void Array_fields_match_any_or_none()
    {
        var row = new Dictionary<string, object?> { ["reviewers"] = new[] { "u1", "u2" } };
        Assert.True(Yes(Rule("reviewers", ConditionOp.Eq, "u2"), row));
        Assert.False(Yes(Rule("reviewers", ConditionOp.Ne, "u2"), row));
        Assert.True(Yes(Rule("reviewers", ConditionOp.In, new[] { "u3", "u1" }), row));
        Assert.True(Yes(Rule("reviewers", ConditionOp.NotIn, new[] { "u3" }), row));
        Assert.True(Yes(Rule("reviewers", ConditionOp.Exists), row));
    }

    [Fact]
    [Upstream("condition", "选项与文字：in / notIn，包含与开头不分大小写；exists / empty 把空字符串、空数组算作空")]
    public void Options_text_and_blanks()
    {
        Assert.True(Yes(Rule("category", ConditionOp.In, new[] { "aluminum", "steel" })));
        Assert.False(Yes(Rule("category", ConditionOp.NotIn, new[] { "steel" })));
        Assert.True(Yes(Rule("code", ConditionOp.Contains, "po")));
        Assert.True(Yes(Rule("code", ConditionOp.StartsWith, "PO-1")));
        Assert.False(Yes(Rule("code", ConditionOp.NotContains, "17")));
        Assert.True(Yes(Rule("note", ConditionOp.Empty)));
        Assert.False(Yes(Rule("tags", ConditionOp.Exists)));
    }

    record Head(int Amount);

    record Nested(Head Head);

    [Fact]
    [Upstream("condition", "组合：all / any / not 可以嵌套；没填完整的比较与空的组不加条件（同 query 的 conditionBy）；read 改取值的方式")]
    public void Groups_nest_and_skip_incomplete()
    {
        var condition = Condition.All(Rule("amount", ConditionOp.Gte, 1000), Condition.Not(Condition.Any(Rule("category", ConditionOp.Eq, "service"))));
        Assert.True(Yes(condition));
        Assert.True(Yes(Condition.All()));
        Assert.True(Yes(Condition.Any()));
        Assert.True(Yes(Condition.Not(Condition.All())));
        // 没填完整的比较不算：any 只看填完整的那一条
        Assert.False(Yes(Condition.Any(Rule("amount", ConditionOp.Gt, ""), Rule("amount", ConditionOp.Lt, 0))));
        Assert.True(Yes(Rule("category", ConditionOp.In, Array.Empty<string>())));
        Assert.True(Rule("amount", ConditionOp.Eq, 5).Matches(new Nested(new Head(5)), (row, field) => field == "amount" ? ((Nested)row!).Head.Amount : null));
        // 默认按属性取值，不分大小写
        Assert.True(Rule("amount", ConditionOp.Eq, 5).Matches(new Head(5)));
    }

    [Fact]
    [Upstream("condition", "值的形状与“填完整”：介于至少一端有值，exists 不要值")]
    public void Value_shapes_and_completeness()
    {
        Assert.Equal(ConditionValueShape.Range, Condition.ShapeOf(ConditionOp.Between));
        Assert.Equal(ConditionValueShape.List, Condition.ShapeOf(ConditionOp.NotIn));
        Assert.Equal(ConditionValueShape.None, Condition.ShapeOf(ConditionOp.Empty));
        Assert.True(Rule("a", ConditionOp.Between, new object?[] { null, 3 }).IsComplete);
        Assert.False(Rule("a", ConditionOp.Between, new object?[] { null, null }).IsComplete);
        Assert.False(Rule("a", ConditionOp.In, Array.Empty<object>()).IsComplete);
        Assert.True(Rule("a", ConditionOp.Exists).IsComplete);
    }

    static readonly ConditionField[] Fields =
    [
        new("amount", "金额", ConditionFieldType.Number),
        new("due", "交货日期", ConditionFieldType.Date),
        new("category", "品类", ConditionFieldType.Select) { Options = [new("steel", "钢材"), new("aluminum", "铝材")] },
        new("urgent", "加急", ConditionFieldType.Boolean),
    ];

    [Fact]
    [Upstream("condition", "嵌套的组加括号，取反写“不满足：”；选项写文字、是否写“是 / 否”、数字按语言格式化；不认识的字段写字段名")]
    public void Describes_nested_conditions()
    {
        var condition = Condition.All(
            Rule("amount", ConditionOp.Gte, 100000),
            Condition.Any(Rule("category", ConditionOp.In, new[] { "steel", "aluminum" }), Rule("urgent", ConditionOp.Eq, true)),
            Condition.Not(Rule("due", ConditionOp.Lt, "2026-11-01")),
            Rule("owner", ConditionOp.Exists));
        Assert.Equal("金额 大于等于 100,000，且 （品类 是其中之一 钢材、铝材，或 加急 为 是），且 不满足：交货日期 早于 2026-11-01，且 owner 不为空", condition.Describe(Fields, Lang.ZhCn));
    }

    [Fact]
    [Upstream("condition", "只有一端的介于写成“以上”“以下”")]
    public void Describes_open_ranges()
    {
        Assert.Equal("金额 介于 500 以上", Rule("amount", ConditionOp.Between, new object?[] { 500, null }).Describe(Fields, Lang.ZhCn));
        Assert.Equal("交货日期 介于 2026-11-01 至 2026-11-30", Rule("due", ConditionOp.Between, new[] { "2026-11-01", "2026-11-30" }).Describe(Fields, Lang.ZhCn));
    }

    [Fact]
    public void Json_round_trips_in_the_upstream_shape()
    {
        var condition = Condition.All(Rule("amount", ConditionOp.Gte, 100000), Condition.Not(Condition.Any(Rule("category", ConditionOp.NotIn, new[] { "steel" }), Rule("owner", ConditionOp.Exists))));
        var json = condition.ToJson();
        Assert.Equal("""{"all":[{"field":"amount","op":"gte","value":100000},{"not":{"any":[{"field":"category","op":"notIn","value":["steel"]},{"field":"owner","op":"exists"}]}}]}""", json);
        Assert.Equal(json, Condition.Parse(json).ToJson());
        Assert.False(Condition.Parse(json).Matches(Order));
        Assert.True(Condition.Parse("""{"any":[{"field":"amount","op":"between","value":[1000,null]}]}""").Matches(Order));
    }
}
