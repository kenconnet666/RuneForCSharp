namespace Rune.Wpf;

/// <summary>
/// 评分的计算（vue-tsx-pro 的 Rating）：每颗星填了多少、能选的分数、方向键换到哪个分数、点了以后的值。三个平台各有一份逐字相同的副本。
/// </summary>
static class RatingScale
{
    /// <summary>每颗星的边长（em）。</summary>
    public const double StarEm = 1.35;

    /// <summary>星与星之间（em）。</summary>
    public const double GapEm = 0.15;

    /// <summary>指针下那颗星放大到多少。</summary>
    public const double HoverScale = 1.12;

    /// <summary>第 <paramref name="index"/> 颗星（从 1 开始）填了多少：0、50、100。</summary>
    public static int Filled(double? shown, int index) =>
        shown is not { } value ? 0 : value >= index ? 100 : value >= index - 0.5 ? 50 : 0;

    /// <summary>能选的分数：整星时 1…count，半星时 0.5、1…count。</summary>
    public static double[] Scores(int count, bool allowHalf) =>
        allowHalf ? [.. Enumerable.Range(1, count * 2).Select(step => step / 2.0)] : [.. Enumerable.Range(1, count).Select(step => (double)step)];

    /// <summary>点了 <paramref name="score"/> 以后的值：可清空时再点已选的分数清空。</summary>
    public static double? Pick(double? value, double score, bool clearable) => clearable && value == score ? null : score;

    /// <summary>
    /// 方向键从 <paramref name="from"/> 换到哪个分数（同原生单选组：到头绕回另一头）。<paramref name="from"/> 不在分数里（还没评分）时，
    /// 往后是第一个、往前是最后一个。
    /// </summary>
    /// <param name="scores">能选的分数。</param>
    /// <param name="from">现在的分数。</param>
    /// <param name="delta">往后 +1、往前 -1。</param>
    public static double Step(IReadOnlyList<double> scores, double? from, int delta)
    {
        var index = from is { } value ? IndexOf(scores, value) : -1;
        if (index < 0) return delta > 0 ? scores[0] : scores[^1];
        return scores[((index + delta) % scores.Count + scores.Count) % scores.Count];
    }

    static int IndexOf(IReadOnlyList<double> scores, double value)
    {
        for (var i = 0; i < scores.Count; i++)
            if (scores[i] == value) return i;
        return -1;
    }
}
