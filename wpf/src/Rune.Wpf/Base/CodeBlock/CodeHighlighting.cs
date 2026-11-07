using System.Text.RegularExpressions;

namespace Rune.Wpf;

/// <summary>代码高亮的一段：从 <see cref="Start"/> 起 <see cref="Length"/> 个字符是 <see cref="Kind"/>。</summary>
/// <param name="Start">起点（字符下标）。</param>
/// <param name="Length">长度。</param>
/// <param name="Kind">种类，决定颜色（主题的 code* token）。</param>
public readonly record struct CodeToken(int Start, int Length, CodeTokenKind Kind);

/// <summary>高亮的种类：颜色取主题的 code* token（同上游 Shiki 主题的映射：标点用注释色，运算符用关键字色）。</summary>
public enum CodeTokenKind
{
    /// <summary>注释（codeComment）。</summary>
    Comment,

    /// <summary>标点（codeComment）。</summary>
    Punctuation,

    /// <summary>关键字与运算符（codeKeyword）。</summary>
    Keyword,

    /// <summary>字符串（codeString）。</summary>
    String,

    /// <summary>数字、常量（codeNumber）。</summary>
    Number,

    /// <summary>函数名（codeFunction）。</summary>
    Function,

    /// <summary>类型名（codeType）。</summary>
    Type,

    /// <summary>标签名（codeTag）。</summary>
    Tag,

    /// <summary>属性名（codeAttribute）。</summary>
    Attribute,
}

/// <summary>
/// 高亮器：代码与语言 → 高亮的段（没有覆盖到的字符按正文颜色）；不认识的语言返回 null，按纯文本显示。异步：可以接 Shiki 这类外部高亮器。
/// </summary>
/// <param name="code">代码。</param>
/// <param name="language">语言（ts、cs、xml……）。</param>
public delegate Task<IReadOnlyList<CodeToken>?> CodeHighlighter(string code, string language);

/// <summary>
/// CodeBlock 用的纯计算：分行、行号写法、内置的高亮器。内置高亮器按正则逐段扫描，够文档与示例用（TS / JS、C#、XML / XAML / HTML、JSON、CSS、SQL、bash），
/// 不追求编辑器级别的准确；要更准时换成自己的 <see cref="CodeHighlighter"/>。三个库各一份、逐字相同（孪生文件）。
/// </summary>
public static class CodeHighlighting
{
    /// <summary>按行拆开；结尾的换行不算新的一行（文件末尾通常有一个），不然行号会多出一行空的。</summary>
    /// <param name="text">代码。</param>
    public static string[] SplitLines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return lines.Length > 1 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    /// <summary>行号的写法 <c>"1,3-5"</c> → {1, 3, 4, 5}（从 1 开始）；写错的段跳过。</summary>
    /// <param name="spec">逗号分隔的行号或区间；null 时为空。</param>
    public static HashSet<int> ParseLines(string? spec)
    {
        var lines = new HashSet<int>();
        if (spec is null) return lines;
        foreach (var part in spec.Split(','))
        {
            var bounds = part.Split('-');
            if (!int.TryParse(bounds[0].Trim(), out var from)) continue;
            var to = from;
            if (bounds.Length > 1 && !int.TryParse(bounds[1].Trim(), out to)) continue;
            for (var line = from; line <= to; line++) lines.Add(line);
        }
        return lines;
    }

    /// <summary>内置的高亮器（同步算完，包成任务）。</summary>
    public static CodeHighlighter Builtin { get; } = (code, language) => Task.FromResult(Tokenize(code, language));

    /// <summary>按语言扫描出高亮的段；不认识的语言返回 null。</summary>
    /// <param name="code">代码。</param>
    /// <param name="language">语言名或别名（不分大小写）。</param>
    public static IReadOnlyList<CodeToken>? Tokenize(string code, string language)
    {
        if (!Grammars.TryGetValue(language.ToLowerInvariant(), out var grammar)) return null;
        var tokens = new List<CodeToken>();
        var position = 0;
        while (position < code.Length)
        {
            var match = grammar.Match(code, position);
            if (!match.Success) break;
            var kind = Kinds.FirstOrDefault(kind => match.Groups[kind.ToString()].Success);
            var group = match.Groups[kind.ToString()];
            if (group.Success && group.Length > 0) tokens.Add(new CodeToken(group.Index, group.Length, kind));
            position = match.Index + Math.Max(1, match.Length);
        }
        return tokens;
    }

    static readonly CodeTokenKind[] Kinds = Enum.GetValues<CodeTokenKind>();

    const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline;

    const string Numbers = @"(?<Number>\b(?:0[xX][0-9a-fA-F_]+|\d[\d_]*(?:\.\d+)?(?:[eE][+-]?\d+)?)[a-zA-Z]*\b)";

    const string Punctuation = @"(?<Punctuation>[{}()\[\];,.])";

    /// <summary>类 C 语言的规则：注释、字符串、关键字、类型（大写开头）、函数（后面跟括号）、数字、运算符、标点。</summary>
    static Regex CLike(string keywords, string strings) => new(string.Join('|',
        @"(?<Comment>//[^\n]*|/\*[\s\S]*?\*/)",
        strings,
        $@"(?<Keyword>\b(?:{keywords})\b|=>|[=!<>]=?=?|[-+*/%&|^?:]=?|&&|\|\||\?\?)",
        Numbers,
        @"(?<Type>\b[A-Z][A-Za-z0-9_]*\b)",
        @"(?<Function>\b[a-zA-Z_$][\w$]*(?=\s*\())",
        Punctuation), Options);

    const string ScriptKeywords =
        "import|export|from|as|default|const|let|var|function|return|if|else|for|while|do|switch|case|break|continue|new|class|extends|implements|interface|type|enum|" +
        "async|await|yield|try|catch|finally|throw|typeof|instanceof|in|of|this|super|null|undefined|true|false|void|delete|readonly|public|private|protected|static|keyof";

    const string CSharpKeywords =
        "using|namespace|class|record|struct|interface|enum|public|private|protected|internal|static|readonly|const|sealed|abstract|virtual|override|new|return|if|else|" +
        "for|foreach|while|do|switch|case|break|continue|var|void|null|true|false|this|base|async|await|try|catch|finally|throw|is|as|in|out|ref|get|set|init|" +
        "string|int|long|double|float|bool|object|decimal|char|byte|required|partial|where|select|from";

    const string SqlKeywords =
        "select|from|where|and|or|not|insert|into|values|update|set|delete|join|left|right|inner|outer|on|group|by|order|having|limit|offset|as|create|table|" +
        "SELECT|FROM|WHERE|AND|OR|NOT|INSERT|INTO|VALUES|UPDATE|SET|DELETE|JOIN|LEFT|RIGHT|INNER|OUTER|ON|GROUP|BY|ORDER|HAVING|LIMIT|OFFSET|AS|CREATE|TABLE";

    static readonly Regex Script = CLike(ScriptKeywords, @"(?<String>`(?:[^`\\]|\\.)*`|'(?:[^'\\\n]|\\.)*'|""(?:[^""\\\n]|\\.)*"")");

    static readonly Regex CSharp = CLike(CSharpKeywords, @"(?<String>@""(?:[^""]|"""")*""|\$?""(?:[^""\\\n]|\\.)*""|'(?:[^'\\\n]|\\.)')");

    /// <summary>标记语言：注释、标签名、属性名、属性值、标点（尖括号、等号）。</summary>
    static readonly Regex Markup = new(string.Join('|',
        @"(?<Comment><!--[\s\S]*?-->)",
        @"(?<=</?)(?<Tag>[\w:.-]+)",
        @"(?<Attribute>\b[\w:.-]+(?=\s*=))",
        @"(?<String>""[^""]*""|'[^']*')",
        @"(?<Punctuation></?|/?>|=)"), Options);

    static readonly Regex Json = new(string.Join('|',
        @"(?<Attribute>""(?:[^""\\]|\\.)*""(?=\s*:))",
        @"(?<String>""(?:[^""\\]|\\.)*"")",
        @"(?<Keyword>\b(?:true|false|null)\b)",
        Numbers,
        @"(?<Punctuation>[{}\[\],:])"), Options);

    static readonly Regex Css = new(string.Join('|',
        @"(?<Comment>/\*[\s\S]*?\*/)",
        @"(?<String>""[^""]*""|'[^']*')",
        @"(?<Attribute>[\w-]+(?=\s*:))",
        @"(?<Number>-?\d+(?:\.\d+)?(?:px|em|rem|%|s|ms|deg)?\b|#[0-9a-fA-F]{3,8}\b)",
        @"(?<Tag>[.#]?[a-zA-Z][\w-]*(?=[^{}]*\{))",
        @"(?<Punctuation>[{}();:,])"), Options);

    static readonly Regex Sql = CLike(SqlKeywords, @"(?<String>'(?:[^']|'')*')");

    static readonly Regex Shell = new(string.Join('|',
        @"(?<Comment>(?<![\w$])#[^\n]*)",
        @"(?<String>'[^']*'|""(?:[^""\\]|\\.)*"")",
        @"(?<Number>\$\{?\w+\}?)",
        @"(?<Keyword>\b(?:if|then|else|fi|for|in|do|done|case|esac|while|export|function|return)\b|\|\||&&|[|>&])",
        @"(?<Function>^\s*[\w./-]+|(?<=[|;&]\s*)[\w./-]+)",
        @"(?<Attribute>(?<=\s)--?[\w-]+)"), Options);

    static readonly Dictionary<string, Regex> Grammars = new()
    {
        ["ts"] = Script, ["tsx"] = Script, ["typescript"] = Script, ["js"] = Script, ["jsx"] = Script, ["javascript"] = Script,
        ["cs"] = CSharp, ["csharp"] = CSharp, ["c#"] = CSharp,
        ["xml"] = Markup, ["xaml"] = Markup, ["axaml"] = Markup, ["html"] = Markup, ["vue"] = Markup,
        ["json"] = Json, ["css"] = Css, ["sql"] = Sql,
        ["bash"] = Shell, ["sh"] = Shell, ["shell"] = Shell, ["shellscript"] = Shell, ["powershell"] = Shell, ["ps1"] = Shell,
    };
}
