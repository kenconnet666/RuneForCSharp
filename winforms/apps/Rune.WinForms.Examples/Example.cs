namespace Rune.WinForms.Examples;

/// <summary>一个示例：Gallery 里的一节，inspect 按 <see cref="Key"/> 找到它。</summary>
/// <param name="Component">组件名，同 vue-tsx-pro。</param>
/// <param name="Id">示例名，同 vue-tsx-pro 文档站上这一节的 id（basic、sizes……），ts-styles 与 inspect 按它配对。</param>
/// <param name="Title">标题。</param>
/// <param name="Description">说明。</param>
/// <param name="Create">新建示例的内容（停靠在宿主顶部，宽度随宿主）。</param>
/// <param name="Framed">整页示例（AppShell）：效果区不留内边距，内容贴满（TS 的 frame，示例在 iframe 里）。</param>
public sealed record Example(string Component, string Id, string Title, string Description, Func<Control> Create, bool Framed = false)
{
    /// <summary>"组件/示例名"，如 <c>Icon/basic</c>。</summary>
    public string Key => $"{Component}/{Id}";
}

/// <summary>全部示例：组件按 vue-tsx-pro 文档站的分类，示例按页面上的先后。</summary>
public static class ExampleCatalog
{
    /// <summary>全部示例。</summary>
    public static IReadOnlyList<Example> All { get; } = [.. IconExamples.All, .. TextExamples.All, .. ListExamples.All, .. SpinnerExamples.All, .. BadgeExamples.All, .. ButtonExamples.All, .. CardExamples.All, .. AvatarExamples.All, .. EmptyExamples.All, .. ProgressExamples.All, .. ProviderExamples.All, .. CheckboxExamples.All, .. ToggleGroupExamples.All, .. TextFieldExamples.All, .. ListboxExamples.All, .. SelectExamples.All, .. CodeBlockExamples.All, .. ScrollAreaExamples.All, .. TabsExamples.All, .. TableExamples.All, .. NavListExamples.All, .. PopoverExamples.All, .. AppShellExamples.All];

    /// <summary>按"组件/示例名"找（不分大小写）；没有时为 null。</summary>
    /// <param name="key">如 <c>Icon/basic</c>。</param>
    public static Example? Find(string key) => All.FirstOrDefault(example => string.Equals(example.Key, key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>设好属性的布局控件放进子控件后返回，表达式里就能嵌套（子控件可以用 [.. 序列] 展开）。</summary>
static class Build
{
    /// <summary>示例里的字号档位：同 vue-tsx-pro 文档站的 xs–xl（标题才用的 xxl、xxxl 不列）。</summary>
    public static readonly TextSize[] Steps = [TextSize.Xs, TextSize.Sm, TextSize.Md, TextSize.Lg, TextSize.Xl];

    /// <summary>
    /// TS 里外面包一层 <c>&lt;Text size tone&gt;</c>：给容器设字体与前景色，里面跟随字体与 ForeColor 的控件
    /// （Icon、Spinner、Link、没写档位与色调的文字）随之变化。换主题时 Gallery 重建示例，所以色调取当下的主题就够。
    /// </summary>
    public static T Styled<T>(T container, TextSize size = default, TextTone tone = default) where T : Control
    {
        if (size.Pixels is { } pixels) container.Font = new Font(Fonts.Installed(RuneTheme.Current.Peek().Fonts.Sans), pixels * 72 / 96);
        if (tone != default) container.ForeColor = tone.ColorIn(ElementContext.Of(container));
        return container;
    }

    public static FlexControl Flex(FlexControl flex, params Control[] children)
    {
        flex.Controls.AddRange(children);
        return flex;
    }

    public static GridControl Grid(GridControl grid, params Control[] children)
    {
        grid.Controls.AddRange(children);
        return grid;
    }

    public static CardControl Card(CardControl card, params Control[] body)
    {
        card.Controls.AddRange(body);
        return card;
    }
}
