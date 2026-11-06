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
/// <param name="Width">示例区域的宽度（设备无关像素）。</param>
/// <param name="Scale">缩放（DPI / 96）。</param>
/// <param name="Json">输出 JSON 而不是 Markdown。</param>
/// <param name="Out">截图的路径；不写时放在 artifacts/inspect/&lt;平台&gt;/ 下。</param>
sealed record InspectOptions(string Command, string? Key, string Theme, double Width, double Scale, bool Json, string? Out)
{
    public const string Usage = """
        用法：inspect <命令> [组件/示例] [选项]
          list                       列出全部示例
          snapshot <组件/示例>        渲染成 PNG（artifacts/inspect/<平台>/）
          tree <组件/示例>            元素树：类型、名字、类、边界
          props <组件/示例>           每个元素解析后的属性与值的来源
          uia <组件/示例>             读屏看到的 UI Automation 树
        选项：--theme <主题>（默认 light） --width <像素>（默认 800） --scale <倍数>（默认 1） --json --out <路径>
        """;

    public static InspectOptions Parse(string[] args)
    {
        if (args.Length == 0) throw new UsageException("缺少命令");
        string? key = null, theme = null, outPath = null;
        double width = 800, scale = 1;
        var json = false;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--theme": theme = Value(ref i); break;
                case "--width": width = Number(ref i); break;
                case "--scale": scale = Number(ref i); break;
                case "--out": outPath = Value(ref i); break;
                case "--json": json = true; break;
                case var arg when arg.StartsWith("--", StringComparison.Ordinal): throw new UsageException($"不认识的选项 {arg}");
                case var arg when key is null: key = arg; break;
                case var arg: throw new UsageException($"多余的参数 {arg}");
            }
        }
        var command = args[0];
        if (command is not ("list" or "snapshot" or "tree" or "props" or "uia")) throw new UsageException($"不认识的命令 {command}");
        if (command != "list" && key is null) throw new UsageException($"{command} 要写示例（组件/示例名，如 Icon/basic）");
        return new InspectOptions(command, key, theme ?? "light", width, scale, json, outPath);

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
