using System.Globalization;

namespace Rune.Wpf.Tests;

/// <summary>生成的语言包（tools/lang-gen）：文案、翻译成 C# 的插值函数、表与元组、with 改写。</summary>
public class LangTests
{
    [Fact]
    public void Plain_texts_come_from_both_packs()
    {
        Assert.Equal("确定", Lang.ZhCn.Confirm);
        Assert.Equal("OK", Lang.EnUs.Confirm);
        Assert.Equal("zh-CN", Lang.ZhCn.Code);
        Assert.Equal("全部满足", Lang.ZhCn.ConditionModes["all"]);
        Assert.Contains("{quarter}", Lang.ZhCn.DateFieldPeriodPatterns.Quarter);
        Assert.Equal(4, Lang.EnUs.FieldCompareHeaders.Count);
    }

    [Fact]
    public void Interpolations_follow_the_typescript_functions()
    {
        Assert.Equal("共 1,286 条", Lang.ZhCn.PaginationTotal(1286));
        Assert.Equal("1,286 items", Lang.EnUs.PaginationTotal(1286));
        Assert.Equal("已输入 3 个字符", Lang.ZhCn.CharacterCount(3, null));
        Assert.Equal("已输入 3 个字符，最多 10 个", Lang.ZhCn.CharacterCount(3, 10));
        Assert.Equal("1 day", Lang.EnUs.ChartDays(1));
        Assert.Equal("2 days", Lang.EnUs.ChartDays(2));
        Assert.Equal("（已完成）", Lang.ZhCn.StepState("complete"));
        Assert.Equal("四季度", Lang.ZhCn.CalendarQuarter(4));
        Assert.Equal("1 至 9", Lang.ZhCn.ConditionRange("1", "9"));
        Assert.Equal("1 以上", Lang.ZhCn.ConditionRange("1", ""));
        Assert.Equal("2 / 10", Lang.EnUs.TransferCount(2, 10));
    }

    [Fact]
    public void Numbers_are_not_affected_by_the_current_culture()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            // 德语的小数点是逗号：模板字符串里的数字仍按 JS 的写法输出
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("1.5 days", Lang.EnUs.ChartDays(1.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void A_few_texts_can_be_rewritten_with_with()
    {
        var custom = Lang.ZhCn with { Confirm = "好" };
        Assert.Equal("好", custom.Confirm);
        Assert.Equal(Lang.ZhCn.Cancel, custom.Cancel);
        Assert.Equal("确定", Lang.ZhCn.Confirm);
    }
}
