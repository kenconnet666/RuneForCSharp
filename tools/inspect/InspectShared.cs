// 三个平台的 inspect 共用的命令行、表格输出与产物路径（各平台的 inspect 项目以链接方式编译本文件，不做成库）
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Rune.Inspect;

/// <summary>
/// 命令行：<c>inspect &lt;命令&gt; [组件/示例] [--theme 主题] [--width 像素] [--scale 倍数] [--json] [--out 路径]</c>。
/// </summary>
/// <param name="Command">list、snapshot、tree、props、uia。</param>
/// <param name="Key">"组件/示例名"，如 Icon/basic；list 不需要。</param>
/// <param name="Theme">主题名，同 vue-tsx-pro 的 builtinThemes（light、dark、paper……）。</param>
/// <param name="Width">窗口的宽度（设备无关像素，含四周的留白）。</param>
/// <param name="Padding">示例四周的留白（设备无关像素）：看效果时留 24；和文档站逐像素对照时写 0，宽度写文档站示例区的内容宽度（756）。</param>
/// <param name="Scale">缩放（DPI / 96）。</param>
/// <param name="Exact">snapshot：布局不按像素取整（同浏览器的小数像素排版），和文档站逐像素对照时用；WPF 默认就不取整，WinForms 只有整像素。</param>
/// <param name="Json">输出 JSON 而不是 Markdown。</param>
/// <param name="Out">截图的路径；不写时放在 artifacts/inspect/&lt;平台&gt;/ 下。</param>
/// <param name="Count">bench：每轮建几个实例。</param>
/// <param name="Runs">bench：跑几轮（第一轮预热，不计）。</param>
sealed record InspectOptions(string Command, string? Key, string Theme, double Width, double Padding, double Scale, bool Exact, bool Json, string? Out, int Count, int Runs)
{
    public const string Usage = """
        用法：inspect <命令> [组件/示例] [选项]
          list                       列出全部示例
          snapshot <组件/示例>        渲染成 PNG（artifacts/inspect/<平台>/）
          tree <组件/示例>            元素树：类型、名字、类、边界
          props <组件/示例>           每个元素解析后的属性与值的来源
          uia <组件/示例>             读屏看到的 UI Automation 树
          styles <组件/示例>          每个部件的外观取值（同 tools/ts-styles 的格式：部件按 scope/part#序号，像素与 #AARRGGBB），parity 拿来比对
          bench [组件]                基准：批量建组件，量创建、首次布局、渲染、换主题的耗时与内存（可只跑名字以它开头的）
        选项：--theme <主题>（默认 light） --width <像素>（默认 800） --padding <像素>（默认 24） --scale <倍数>（默认 1） --exact --json --out <路径>
              --count <个数>（bench，默认 1000） --runs <轮数>（bench，默认 6，第一轮预热）
        """;

    public static InspectOptions Parse(string[] args)
    {
        if (args.Length == 0) throw new UsageException("缺少命令");
        string? key = null, theme = null, outPath = null;
        double width = 800, padding = 24, scale = 1, count = 1000, runs = 6;
        var json = false;
        var exact = false;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--theme": theme = Value(ref i); break;
                case "--width": width = Number(ref i); break;
                case "--padding": padding = double.TryParse(Value(ref i), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : throw new UsageException("--padding 要写不小于 0 的数"); break;
                case "--scale": scale = Number(ref i); break;
                case "--out": outPath = Value(ref i); break;
                case "--count": count = Number(ref i); break;
                case "--runs": runs = Number(ref i); break;
                case "--json": json = true; break;
                case "--exact": exact = true; break;
                case var arg when arg.StartsWith("--", StringComparison.Ordinal): throw new UsageException($"不认识的选项 {arg}");
                case var arg when key is null: key = arg; break;
                case var arg: throw new UsageException($"多余的参数 {arg}");
            }
        }
        var command = args[0];
        if (command is not ("list" or "snapshot" or "tree" or "props" or "uia" or "styles" or "bench")) throw new UsageException($"不认识的命令 {command}");
        if (command is not ("list" or "bench") && key is null) throw new UsageException($"{command} 要写示例（组件/示例名，如 Icon/basic）");
        if (runs < 2) throw new UsageException("--runs 至少 2（第一轮预热，不计）");
        return new InspectOptions(command, key, theme ?? "light", width, padding, scale, exact, json, outPath, (int)count, (int)runs);

        string Value(ref int i) => ++i < args.Length ? args[i] : throw new UsageException($"{args[i - 1]} 后面要写值");

        double Number(ref int i)
        {
            var text = Value(ref i);
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number > 0
                ? number
                : throw new UsageException($"{args[i - 1]} 要写正数，不是“{text}”");
        }
    }
}

/// <summary>命令行写错了：打印原因与用法。</summary>
sealed class UsageException(string message) : Exception(message);

/// <summary>入口：解析命令行、执行、把结果写到标准输出；写错了打印用法，退出码 2。</summary>
static class InspectProgram
{
    public static int Run(string[] args, Func<InspectOptions, string> execute)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            Console.Out.Write(execute(InspectOptions.Parse(args)));
            return 0;
        }
        catch (UsageException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(InspectOptions.Usage);
            return 2;
        }
    }
}

/// <summary>一张表：默认输出 Markdown 表，<c>--json</c> 时输出对象数组（列名作键）。</summary>
/// <param name="columns">列名。</param>
sealed class ReportTable(params string[] columns)
{
    readonly List<string[]> rows = [];

    public void Add(params object?[] cells) => rows.Add([.. cells.Select(Report.Format)]);

    public string Render(bool json) => json ? ToJson() : ToMarkdown();

    string ToMarkdown()
    {
        var text = new StringBuilder();
        text.Append("| ").AppendJoin(" | ", columns).AppendLine(" |");
        text.Append('|').AppendJoin("", columns.Select(_ => "---|")).AppendLine();
        foreach (var row in rows) text.Append("| ").AppendJoin(" | ", row.Select(cell => cell.Replace("|", "\\|", StringComparison.Ordinal))).AppendLine(" |");
        return text.ToString();
    }

    string ToJson() => JsonSerializer.Serialize(
        rows.Select(row => columns.Zip(row).ToDictionary(pair => pair.First, pair => pair.Second)),
        new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}

/// <summary>输出的格式与产物的位置。</summary>
static class Report
{
    /// <summary>单元格的文字：数字用不变文化、去掉多余的零；null 写成"—"。</summary>
    public static string Format(object? value) => value switch
    {
        null => "—",
        double number => number.ToString("0.###", CultureInfo.InvariantCulture),
        float number => number.ToString("0.###", CultureInfo.InvariantCulture),
        bool flag => flag ? "是" : "否",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "—",
    };

    /// <summary>截图的默认路径：artifacts/inspect/&lt;平台&gt;/&lt;组件&gt;-&lt;示例&gt;-&lt;主题&gt;[@缩放].png。</summary>
    public static string SnapshotPath(string platform, InspectOptions options)
    {
        if (options.Out is { } path) return Path.GetFullPath(path);
        var scale = options.Scale == 1 ? "" : $"@{Format(options.Scale)}x";
        var name = $"{options.Key!.Replace('/', '-')}-{options.Theme}{scale}.png";
        return Path.Combine(RepoRoot(), "artifacts", "inspect", platform, name);
    }

    /// <summary>仓库根目录：从程序所在目录往上找 RuneForCSharp.slnx。</summary>
    static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "RuneForCSharp.slnx"))) return dir.FullName;
        throw new InvalidOperationException($"从 {AppContext.BaseDirectory} 往上找不到 RuneForCSharp.slnx；用 --out 指定截图路径");
    }
}

/// <summary>
/// 基准的结果：每个对象跑若干轮，第一轮预热（JIT、样式与资源的首次查找）不计，其余每个阶段取中位数。
/// 耗时是挂钟时间（毫秒）；分配是这一轮里当前线程分配的字节；常驻是建完、布局完、完整回收之后多出来的托管内存，按个数平摊。
/// </summary>
/// <param name="phases">阶段（列名），按先后。</param>
sealed class BenchReport(params string[] phases)
{
    readonly List<(string Subject, Dictionary<string, List<double>> Samples)> subjects = [];

    /// <summary>记一个样本；同一对象的样本按轮累积。</summary>
    public void Add(string subject, string phase, double value)
    {
        var entry = subjects.FirstOrDefault(s => s.Subject == subject);
        if (entry.Subject is null) subjects.Add(entry = (subject, []));
        if (!entry.Samples.TryGetValue(phase, out var list)) entry.Samples[phase] = list = [];
        list.Add(value);
    }

    public string Render(bool json)
    {
        var table = new ReportTable(["组件", .. phases]);
        foreach (var (subject, samples) in subjects)
            table.Add([subject, .. phases.Select(phase => samples.TryGetValue(phase, out var list) ? (object)Math.Round(Median(list), 2) : null)]);
        return table.Render(json);
    }

    static double Median(List<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
    }
}

/// <summary>基准的计量：耗时、分配、常驻。</summary>
static class Bench
{
    /// <summary>执行一次，返回毫秒。</summary>
    public static double Time(Action action)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        action();
        return watch.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// 完整回收后的托管内存（字节）：量常驻用。终结器要等两遍：第二次回收又会排进新的终结（WPF 渲染留下的 MIL 句柄），
    /// 不等完它们就和后面的计时并行，抢 WPF 查主题样式时的锁（实测把按钮的创建拖慢几十倍）。
    /// </summary>
    public static long Settled()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    /// <summary>当前线程到现在分配的字节。</summary>
    public static long Allocated() => GC.GetAllocatedBytesForCurrentThread();
}

/// <summary>
/// 部件的外观取值（<c>inspect styles</c>）：与 tools/ts-styles 取的 vue-tsx-pro 文档站同一种格式，parity 逐项比对。
/// 部件的称呼同 TS 的 <c>data-scope/data-part#序号</c>：组件根是 <c>button/root</c>，模板或代码里组合的部件
/// <c>PART_Icon</c> 是 <c>button/icon</c>；序号按元素树的先后、每种称呼各自数。长度是设备无关像素，颜色是 #AARRGGBB。
/// </summary>
sealed class StyleReport
{
    /// <summary>与 TS 的 data-scope 不同名的组件：按钮家族都用 button-core 的配方，scope 都是 button。</summary>
    static readonly Dictionary<string, string> ScopeAliases = new()
    {
        ["ButtonBase"] = "button",
        ["ButtonCore"] = "button",
        ["IconButton"] = "button",
        ["LinkButton"] = "button",
        ["IconLinkButton"] = "button",
        ["CloseButton"] = "button",
        ["ClearButton"] = "button",
        // 上游的勾选类（Switch，以后的 Checkbox、Radio）共用 choice 的部件标记
        ["Switch"] = "choice",
        ["Checkbox"] = "choice",
        ["Radio"] = "choice",
        ["ChoiceBase"] = "choice",
    };

    /// <summary>
    /// 上游没有部件标记的组件（样式直接写在元素上，不带 data-scope）：它们自己与部件都不按组件记，
    /// 里面的元素按标签配对（Field 的星号是 @svg）。
    /// </summary>
    static readonly HashSet<string> Unscoped = ["Field", "FieldLabel"];

    /// <summary>组件在上游有部件标记（不在 <see cref="Unscoped"/> 里）。</summary>
    public static bool IsScoped(string componentType) => !Unscoped.Contains(componentType);

    /// <summary>组件根在上游是另一个组件的部件：复选组、单选组是 choice 配方的 group。</summary>
    static readonly Dictionary<string, string> RootAliases = new()
    {
        ["CheckboxGroup"] = "choice/group",
        ["RadioGroup"] = "choice/group",
    };

    /// <summary>部件在上游叫别的名字：API 名（propsThumb）与部件标记（data-part="indicator"）不同的。</summary>
    static readonly Dictionary<(string Component, string Part), string> PartAliases = new()
    {
        [("Switch", "PART_Track")] = "input",
        [("Switch", "PART_Thumb")] = "indicator",
        [("Checkbox", "PART_Box")] = "input",
        [("Radio", "PART_Circle")] = "input",
    };

    /// <summary>TS 里没有部件标记的组件（css.create 写的）：按渲染出的标签配对，<c>@p#0</c>，同 tools/ts-styles。</summary>
    static readonly Dictionary<string, string> Tags = new()
    {
        ["Icon"] = "@svg",
        ["Text"] = "@span",
        ["Paragraph"] = "@p",
        ["Strong"] = "@strong",
        ["Em"] = "@em",
        ["Small"] = "@small",
        ["Kbd"] = "@kbd",
        ["H1"] = "@h1",
        ["H2"] = "@h2",
        ["H3"] = "@h3",
        ["H4"] = "@h4",
        ["H5"] = "@h5",
        ["H6"] = "@h6",
    };

    readonly List<(string Key, string Type, Dictionary<string, object> Props)> parts = [];
    readonly Dictionary<string, int> counts = [];

    /// <summary>组件类型名 + 部件名（PART_Icon；组件根为 null）→ <c>button/icon</c>。</summary>
    public static string Name(string componentType, string? partName)
    {
        if (partName is null && Tags.TryGetValue(componentType, out var tag)) return tag;
        if (partName is null && RootAliases.TryGetValue(componentType, out var root)) return root;
        var scope = ScopeAliases.GetValueOrDefault(componentType) ?? Kebab(componentType);
        var part = partName is null ? "root"
            : PartAliases.GetValueOrDefault((componentType, partName)) ?? char.ToLowerInvariant(partName["PART_".Length]) + partName["PART_".Length..][1..];
        return $"{scope}/{part}";
    }

    /// <summary>记一个部件；值为 null 的属性不记。</summary>
    public void Add(string name, string type, IEnumerable<(string Prop, object? Value)> props)
    {
        var index = counts[name] = counts.GetValueOrDefault(name, -1) + 1;
        parts.Add(($"{name}#{index}", type, props.Where(p => p.Value is not null).ToDictionary(p => p.Prop, p => Round(p.Value!))));
    }

    /// <summary>颜色：#AARRGGBB。</summary>
    public static string Color(byte a, byte r, byte g, byte b) => $"#{a:X2}{r:X2}{g:X2}{b:X2}";

    public string Render(bool json)
    {
        if (json)
            return JsonSerializer.Serialize(new { parts = parts.Select(p => new { key = p.Key, type = p.Type, props = p.Props }) },
                new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var table = new ReportTable("部件", "类型", "宽 × 高", "内边距", "字号 / 字重", "前景", "底色", "描边", "圆角");
        foreach (var (key, type, p) in parts)
            table.Add(key, type, $"{Get(p, "Width")} × {Get(p, "Height")}",
                $"{Get(p, "PaddingTop")} {Get(p, "PaddingRight")} {Get(p, "PaddingBottom")} {Get(p, "PaddingLeft")}",
                $"{Get(p, "FontSize")} / {Get(p, "FontWeight")}", Get(p, "Foreground"), Get(p, "Background"),
                p.ContainsKey("BorderTop") ? $"{Get(p, "BorderTop")} {Get(p, "BorderColor")}" : null, Get(p, "RadiusTopLeft"));
        return table.Render(false);
    }

    static string? Get(Dictionary<string, object> props, string name) => props.TryGetValue(name, out var value) ? Report.Format(value) : null;

    static object Round(object value) => value switch
    {
        double number => Math.Round(number, 2),
        float number => Math.Round(number, 2),
        _ => value,
    };

    static string Kebab(string name) => string.Concat(name.Select((c, i) => char.IsUpper(c) ? (i > 0 ? "-" : "") + char.ToLowerInvariant(c) : c.ToString()));
}
