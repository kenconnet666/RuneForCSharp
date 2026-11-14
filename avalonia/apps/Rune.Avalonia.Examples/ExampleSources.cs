using System.Text.RegularExpressions;

namespace Rune.Avalonia.Examples;

/// <summary>
/// 示例的源码（文档站的"显示代码"）：示例项目的源文件作为嵌入资源打进程序集（LogicalName 是 <c>Source/</c> 加相对路径），按示例切出来。
/// 方法组的示例（<c>new("Stat", "basic", …, Basic)</c>）取那个方法，连同紧贴在上面的注释与特性；
/// XAML 示例（<c>() =&gt; new ListBasic()</c>）取那个控件的 XAML 文件。三个库各有一份，除命名空间外逐字相同（tools/twins）。
/// </summary>
public static partial class ExampleSources
{
    const string Prefix = "Source/";
    static readonly Lazy<IReadOnlyDictionary<string, string>> files = new(Load);

    /// <summary>示例的源码；找不到时为 null。</summary>
    /// <param name="create">示例的新建委托（Example.Create）。</param>
    /// <param name="created">新建出来的示例：XAML 示例按它的类型找 XAML 文件。</param>
    public static string? Of(Delegate create, object? created = null)
    {
        var method = create.Method;
        // 编译器生成的名字（lambda）以 < 开头：不是写在源码里的方法
        if (!method.Name.StartsWith('<') && method.DeclaringType is { } type) return MethodSource(type.Name, method.Name);
        if (created is null) return null;
        var name = created.GetType().Name;
        return files.Value.FirstOrDefault(file => file.Key.EndsWith("xaml", StringComparison.Ordinal) && System.IO.Path.GetFileNameWithoutExtension(file.Key) == name).Value;
    }

    /// <summary>某个类里某个方法的源码（去掉公共的缩进）；找不到时为 null。</summary>
    /// <param name="typeName">声明它的类型名。</param>
    /// <param name="methodName">方法名。</param>
    public static string? MethodSource(string typeName, string methodName)
    {
        // 行首是类型加方法名的才是声明；return Basic(…) 这类调用排除掉
        var declaration = new Regex($@"^[ \t]*(?!(?:return|await|new|throw|yield|else)\b)(?:(?:public|internal|private|protected|static|async|override|unsafe)\s+)*[\w<>\[\],.?():]+?\s+{Regex.Escape(methodName)}\s*\(", RegexOptions.Multiline);
        foreach (var text in files.Value.Values)
        {
            if (!Regex.IsMatch(text, $@"\b(class|record|struct)\s+{Regex.Escape(typeName)}\b")) continue;
            var match = declaration.Match(text);
            if (!match.Success) continue;
            var start = IncludeLeadingComments(text, match.Index);
            var end = BodyEnd(text, match.Index + match.Length - 1);
            return end < 0 ? null : Dedent(text[start..end]);
        }
        return null;
    }

    static IReadOnlyDictionary<string, string> Load()
    {
        var assembly = typeof(ExampleSources).Assembly;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var path = name.Replace('\\', '/');
            if (!path.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new System.IO.StreamReader(stream);
            result[path[Prefix.Length..]] = reader.ReadToEnd().Replace("\r\n", "\n");
        }
        return result;
    }

    /// <summary>往上包进紧贴着的注释（// 与 ///）与特性行。</summary>
    static int IncludeLeadingComments(string text, int start)
    {
        while (start > 0)
        {
            var previousEnd = start - 1;
            var previousStart = text.LastIndexOf('\n', Math.Max(0, previousEnd - 1)) + 1;
            var line = text[previousStart..previousEnd].Trim();
            if (!line.StartsWith("//", StringComparison.Ordinal) && !line.StartsWith('[')) break;
            start = previousStart;
        }
        return start;
    }

    /// <summary>
    /// 从参数表的左括号起往后找方法体的结尾：块体到配对的右花括号，表达式体（=>）到最外层的分号。
    /// 跳过字符串、字符与注释里的括号；返回结尾之后的位置，找不到时为 -1。
    /// </summary>
    static int BodyEnd(string text, int open)
    {
        var depth = 0;
        var expression = false;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/') i = SkipTo(text, i, "\n") - 1;
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '*') i = SkipTo(text, i + 2, "*/") + 1;
            else if (c == '"' || c == '\'') i = SkipString(text, i);
            else if (c is '(' or '{' or '[') depth++;
            else if (c is ')' or '}' or ']')
            {
                depth--;
                if (depth == 0 && c == '}' && !expression) return i + 1;
            }
            else if (depth == 0 && c == '=' && i + 1 < text.Length && text[i + 1] == '>') expression = true;
            else if (depth == 0 && c == ';' && expression) return i + 1;
        }
        return -1;
    }

    /// <summary>跳过一个字符串或字符字面量（普通、逐字 @、插值 $、原始 """）；返回它最后一个字符的位置。</summary>
    static int SkipString(string text, int quote)
    {
        if (text.AsSpan(quote).StartsWith("\"\"\"")) return SkipTo(text, quote + 3, "\"\"\"") + 2;
        var verbatim = quote > 0 && (text[quote - 1] == '@' || (quote > 1 && text[quote - 2] == '@'));
        var delimiter = text[quote];
        for (var i = quote + 1; i < text.Length; i++)
        {
            if (!verbatim && text[i] == '\\') i++;
            else if (text[i] == delimiter)
            {
                if (verbatim && i + 1 < text.Length && text[i + 1] == delimiter) i++;
                else return i;
            }
        }
        return text.Length - 1;
    }

    static int SkipTo(string text, int from, string end)
    {
        var at = text.IndexOf(end, from, StringComparison.Ordinal);
        return at < 0 ? text.Length : at;
    }

    /// <summary>去掉各行公共的缩进与首尾空行。</summary>
    static string Dedent(string block)
    {
        var lines = block.Split('\n');
        var indent = lines.Where(line => line.Trim().Length > 0).Select(line => line.Length - line.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join('\n', lines.Select(line => line.Length >= indent ? line[indent..] : line.TrimStart())).Trim('\n');
    }
}
