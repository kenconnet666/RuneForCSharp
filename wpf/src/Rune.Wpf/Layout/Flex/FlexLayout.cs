namespace Rune.Wpf;

/// <summary>
/// flex 排版的计算（CSS flexbox 里 vue-tsx-pro 的 Flex 用到的部分）：分行、伸展与收缩、主轴分布、交叉轴对齐。
/// 只算数，量与摆由面板做（主轴、交叉轴已经按方向换好）。三个库各有一份，除命名空间外逐字相同（DESIGN §14 Flex）。
/// </summary>
static class FlexLayout
{
    const double Epsilon = 0.01;

    /// <summary>一项：主轴上的基础尺寸（内容想要的）与能否伸展（Grow：分剩下的空间，也能缩到 0）。</summary>
    public readonly record struct Item(double Basis, bool Grow);

    /// <summary>一行：从第几项起、几项，每项分到的主轴尺寸；交叉轴尺寸由面板按分到的尺寸量过之后填。</summary>
    public sealed class Line(int start, double[] main)
    {
        public int Start { get; } = start;

        public double[] Main { get; } = main;

        public int Count => Main.Length;

        public double Cross { get; set; }
    }

    /// <summary>
    /// 分行（wrap 时放不下就换行），再在每行里伸展或收缩（CSS 的 flex-grow: 1 只给 Grow 的项，flex-shrink: 1 按基础尺寸加权）。
    /// </summary>
    /// <param name="items">各项。</param>
    /// <param name="available">主轴上可用的尺寸；无穷大时不伸缩、不换行。</param>
    /// <param name="gap">主轴上项与项之间的间距。</param>
    /// <param name="wrap">换行。</param>
    /// <param name="min">第 i 项最多能缩到多小（要收缩时才问）。</param>
    public static List<Line> Distribute(IReadOnlyList<Item> items, double available, double gap, bool wrap, Func<int, double> min)
    {
        var lines = new List<Line>();
        for (var start = 0; start < items.Count;)
        {
            var count = 1;
            var used = items[start].Basis;
            if (!wrap || double.IsInfinity(available)) count = items.Count - start;
            else
                for (; start + count < items.Count && used + gap + items[start + count].Basis <= available + Epsilon; count++)
                    used += gap + items[start + count].Basis;
            lines.Add(Fit(items, start, count, available, gap, min));
            start += count;
        }
        return lines;
    }

    static Line Fit(IReadOnlyList<Item> items, int start, int count, double available, double gap, Func<int, double> min)
    {
        var main = new double[count];
        for (var i = 0; i < count; i++) main[i] = items[start + i].Basis;
        var line = new Line(start, main);
        if (double.IsInfinity(available)) return line;
        var free = available - main.Sum() - gap * (count - 1);
        if (free > Epsilon)
        {
            var growers = Enumerable.Range(0, count).Where(i => items[start + i].Grow).ToList();
            foreach (var i in growers) main[i] += free / growers.Count;
            return line;
        }
        // 收缩：按基础尺寸分摊不够的部分，缩到最小尺寸的项冻结，其余的再分（CSS 的"解决弹性长度"）
        var mins = new double[count];
        for (var i = 0; i < count; i++) mins[i] = items[start + i].Grow ? 0 : Math.Min(min(start + i), main[i]);
        var frozen = new bool[count];
        while ((free = available - main.Sum() - gap * (count - 1)) < -Epsilon)
        {
            var weight = Enumerable.Range(0, count).Where(i => !frozen[i]).Sum(i => items[start + i].Basis);
            if (weight <= 0) break;
            var violated = false;
            for (var i = 0; i < count; i++)
            {
                if (frozen[i] || main[i] + free * items[start + i].Basis / weight >= mins[i]) continue;
                (main[i], frozen[i], violated) = (mins[i], true, true);
            }
            if (violated) continue;
            for (var i = 0; i < count; i++)
                if (!frozen[i]) main[i] += free * items[start + i].Basis / weight;
            break;
        }
        return line;
    }

    /// <summary>一行里每项在主轴上的起点（justify-content）；reverse 时从主轴的另一端排起。</summary>
    public static double[] Positions(double[] main, double gap, double size, FlexJustify justify, bool reverse)
    {
        var n = main.Length;
        var free = Math.Max(0, size - main.Sum() - gap * Math.Max(0, n - 1));
        var (offset, between) = justify.Value switch
        {
            "center" => (free / 2, 0d),
            "end" => (free, 0d),
            "between" => (0d, n > 1 ? free / (n - 1) : 0),
            "around" => (free / n / 2, free / n),
            "evenly" => (free / (n + 1), free / (n + 1)),
            _ => (0d, 0d),
        };
        var positions = new double[n];
        var x = offset;
        for (var i = 0; i < n; i++)
        {
            positions[i] = reverse ? size - x - main[i] : x;
            x += main[i] + gap + between;
        }
        return positions;
    }

    /// <summary>
    /// 一项在行里的交叉轴位置与尺寸（align-items，项自己的 align-self 盖过它）：stretch 填满行，baseline 让第一行文字的基线对齐（没有基线的按底边）。
    /// </summary>
    public static (double Offset, double Size) Align(double cross, double lineCross, FlexAlign align, double ascent, double lineAscent) => align.Value switch
    {
        "start" => (0, cross),
        "center" => ((lineCross - cross) / 2, cross),
        "end" => (lineCross - cross, cross),
        "baseline" => (lineAscent - ascent, cross),
        _ => (0, lineCross),
    };

    /// <summary>一行的交叉轴尺寸：最高的项；按基线对齐的项基线以上、以下分别取最大，再和其余的项比。</summary>
    public static (double Cross, double Ascent) LineCross(IReadOnlyList<(double Cross, double Ascent, FlexAlign Align)> items)
    {
        var aligned = items.Where(item => item.Align.Value == "baseline").ToList();
        var ascent = aligned.Select(item => item.Ascent).DefaultIfEmpty().Max();
        var descent = aligned.Select(item => item.Cross - item.Ascent).DefaultIfEmpty().Max();
        return (Math.Max(ascent + descent, items.Select(item => item.Cross).DefaultIfEmpty().Max()), ascent);
    }

    /// <summary>
    /// 各行的交叉轴尺寸铺到容器上：单行时这一行就是容器的交叉轴尺寸（CSS 的单行 flex 容器，内容更宽也不跟着撑开，拉伸的项随容器）；
    /// 多行时多出来的空间平分给各行（align-content 的默认值 normal，同 stretch）。
    /// </summary>
    public static void Stretch(List<Line> lines, double size, double gap, bool wrap)
    {
        if (lines.Count == 0) return;
        if (!wrap)
        {
            lines[0].Cross = size;
            return;
        }
        var extra = size - lines.Sum(line => line.Cross) - gap * (lines.Count - 1);
        if (extra > Epsilon)
            foreach (var line in lines) line.Cross += extra / lines.Count;
    }
}
