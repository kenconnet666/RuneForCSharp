using System.Windows;

namespace Rune.Wpf.Examples;

/// <summary>一个示例：Gallery 里的一节，inspect 按 <see cref="Key"/> 找到它。</summary>
/// <param name="Component">组件名，同 vue-tsx-pro。</param>
/// <param name="Id">示例名，同 vue-tsx-pro 文档站上这一节的 id（basic、sizes……），ts-styles 与 inspect 按它配对。</param>
/// <param name="Title">标题。</param>
/// <param name="Description">说明。</param>
/// <param name="Create">新建示例的内容。</param>
public sealed record Example(string Component, string Id, string Title, string Description, Func<FrameworkElement> Create)
{
    /// <summary>"组件/示例名"，如 <c>Icon/basic</c>。</summary>
    public string Key => $"{Component}/{Id}";
}

/// <summary>全部示例：组件按 vue-tsx-pro 文档站的分类，示例按页面上的先后。</summary>
public static class ExampleCatalog
{
    /// <summary>全部示例。</summary>
    public static IReadOnlyList<Example> All { get; } = [.. IconExamples.All, .. TextExamples.All, .. ListExamples.All, .. SpinnerExamples.All, .. BadgeExamples.All, .. ButtonExamples.All, .. IconButtonExamples.All];

    /// <summary>按"组件/示例名"找（不分大小写）；没有时为 null。</summary>
    /// <param name="key">如 <c>Icon/basic</c>。</param>
    public static Example? Find(string key) => All.FirstOrDefault(example => string.Equals(example.Key, key, StringComparison.OrdinalIgnoreCase));
}
