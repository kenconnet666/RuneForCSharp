using System.Text;
using System.Text.Json;

namespace Rune.Wpf.Examples;

/// <summary>
/// 文档站的页面与示例清单（vue-tsx-pro 文档站，tools/docs-gen 生成的 docs/site/pages.g.json，作为嵌入资源打进示例程序集）：
/// 分类按上游侧栏的顺序，页面按 order，示例按列与先后。文档应用按它排页面，示例的内容由 <see cref="ExampleCatalog"/> 按"页面名/示例 id"对上。
/// 三个库各有一份，除命名空间外逐字相同（tools/twins）。
/// </summary>
/// <param name="Categories">分类（侧栏的组）。</param>
public sealed record DocSite(IReadOnlyList<DocCategory> Categories)
{
    /// <summary>嵌入的清单。</summary>
    public static DocSite Current { get; } = Load();

    /// <summary>全部页面，按侧栏的顺序。</summary>
    public IEnumerable<DocPage> Pages => Categories.SelectMany(category => category.Pages);

    /// <summary>按页面名（组件文件夹名，如 Stat）找；没有时为 null。</summary>
    /// <param name="name">页面名。</param>
    public DocPage? Find(string name) => Pages.FirstOrDefault(page => page.Name == name);

    static DocSite Load()
    {
        using var stream = typeof(DocSite).Assembly.GetManifestResourceStream("pages.g.json") ?? throw new InvalidOperationException("示例程序集里没有嵌入 pages.g.json");
        return JsonSerializer.Deserialize<DocSite>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}

/// <summary>一个分类（侧栏的一组）。</summary>
/// <param name="Id">目录名：base、layout……</param>
/// <param name="Label">组名：基础、布局……</param>
/// <param name="Pages">组里的页面。</param>
public sealed record DocCategory(string Id, string Label, IReadOnlyList<DocPage> Pages);

/// <summary>一页。</summary>
/// <param name="Name">组件文件夹名（Stat）：示例的 Component 与它相同。</param>
/// <param name="Slug">地址里的名字（stat）。</param>
/// <param name="Title">标题。</param>
/// <param name="Description">一句话简介。</param>
/// <param name="Order">在分类里的顺序（可以是小数：插在两页之间）。</param>
/// <param name="Wide">宽版面（示例单列占满）。</param>
/// <param name="Columns">示例，按列。</param>
/// <param name="Keyboard">键盘表。</param>
/// <param name="Api">API 表里列的组件。</param>
public sealed record DocPage(string Name, string Slug, string Title, string Description, double? Order, bool Wide,
    IReadOnlyList<IReadOnlyList<DocExample>> Columns, IReadOnlyList<DocKey> Keyboard, IReadOnlyList<string> Api)
{
    /// <summary>全部示例，按列与先后。</summary>
    public IEnumerable<DocExample> Examples => Columns.SelectMany(column => column);
}

/// <summary>页面里的一个示例。</summary>
/// <param name="Id">锚点，也是示例名（同 Example.Id）。</param>
/// <param name="Title">标题。</param>
/// <param name="Demo">上游的示例文件名。</param>
/// <param name="Frame">整页示例的高度（CSS 长度，如 40rem）；不是整页示例时为 null。</param>
/// <param name="Description">说明。</param>
public sealed record DocExample(string Id, string Title, string? Demo, string? Frame, IReadOnlyList<DocRun> Description);

/// <summary>键盘表的一行。</summary>
/// <param name="Keys">按键（"↓ / Enter"）。</param>
/// <param name="Action">作用。</param>
public sealed record DocKey(string Keys, IReadOnlyList<DocRun> Action);

/// <summary>说明里的一段：文字、行内代码、按键或链接，只有一项有值。</summary>
/// <param name="Text">文字。</param>
/// <param name="Code">行内代码。</param>
/// <param name="Kbd">按键。</param>
/// <param name="Link">链接的文字。</param>
/// <param name="Href">链接的地址。</param>
public sealed record DocRun(string? Text = null, string? Code = null, string? Kbd = null, string? Link = null, string? Href = null)
{
    /// <summary>
    /// 一串片段 → CodeText 的写法：行内代码与按键包在反引号里，其余是文字。
    /// 文字里本来的反引号不会出现（上游的说明把代码都写成 Code）。
    /// </summary>
    /// <param name="runs">片段。</param>
    public static string ToCodeText(IEnumerable<DocRun> runs)
    {
        var text = new StringBuilder();
        foreach (var run in runs)
        {
            if ((run.Code ?? run.Kbd) is { } code) text.Append('`').Append(code).Append('`');
            else text.Append(run.Text ?? run.Link);
        }
        return text.ToString();
    }
}
