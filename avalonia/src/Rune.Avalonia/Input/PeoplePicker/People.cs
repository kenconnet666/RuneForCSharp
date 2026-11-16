namespace Rune.Avalonia;

/// <summary>
/// 人员与组织选择器能选的类型（vue-tsx-pro 的 PeopleTypes）：标准的是人员、部门、角色；岗位、项目组这类由接入方自己定一个字符串，
/// 名字写进语言包的 PeopleTypes。
/// </summary>
public static class PeopleTypes
{
    /// <summary>人员。</summary>
    public const string User = "user";

    /// <summary>部门：选中就是部门里的人（怎么展开由后端定）。</summary>
    public const string Department = "department";

    /// <summary>角色：流程的角色收件箱。</summary>
    public const string Role = "role";
}

/// <summary>一个人员、部门或角色（vue-tsx-pro 的 PeopleEntry）：选中的值带着名字，回显不用再查。同一个人、部门按类型加标识判断（<see cref="Key"/>）。</summary>
/// <param name="Id">标识：同一种类型里唯一（接入方组织表的主键，或工号、编码）。</param>
/// <param name="Type">类型（<see cref="PeopleTypes"/>）。</param>
/// <param name="Name">名字。</param>
/// <param name="Description">说明：人员的部门与角色、部门的上级路径；搜索结果里分辨同名的。</param>
/// <param name="Avatar">头像地址（人员）；没有时显示名字的头一个字。</param>
/// <param name="HasChildren">部门下还有子部门或人员；false 时没有"下级"，不写时按有。</param>
public record PeopleEntry(string Id, string Type, string Name, string? Description = null, string? Avatar = null, bool? HasChildren = null)
{
    /// <summary>同一个人员、部门、角色的键：类型加标识。</summary>
    public string Key => $"{Type}:{Id}";

    /// <summary>写进选中的值、最近选过的那一项：去掉下级与派生类型带的东西，只留这一项本身。</summary>
    public PeopleEntry ToValue() => new(Id, Type, Name, Description, Avatar, HasChildren);
}

/// <summary>组织架构的一个节点（vue-tsx-pro 的 PeopleNode）：部门带着下级。</summary>
/// <param name="Id">标识。</param>
/// <param name="Type">类型。</param>
/// <param name="Name">名字。</param>
/// <param name="Description">说明。</param>
/// <param name="Children">子部门与人员。</param>
public sealed record PeopleNode(string Id, string Type, string Name, string? Description = null, IReadOnlyList<PeopleNode>? Children = null)
    : PeopleEntry(Id, Type, Name, Description);

/// <summary>
/// 数据来源（vue-tsx-pro 的 PeopleSource）：接入方按自己的接口实现；取消令牌在换了部门、改了搜索词、关掉选择器时取消。
/// <code>
/// new PeopleSource(
///     (parent, cancel) => api.Children(parent?.Id, cancel),
///     Search: (query, cancel) => api.Search(query, cancel),
///     Roles: cancel => api.Roles(cancel));
/// </code>
/// 组织架构不大、已经整棵在客户端时用 <see cref="Local"/>。
/// </summary>
/// <param name="Children">一个部门下的子部门与人员（部门在前）；parent 为 null 时是最上层。</param>
/// <param name="Search">按关键字搜，人员、部门、角色都可以返回；不写时没有搜索框。</param>
/// <param name="Roles">全部角色；不写时没有"角色"页。</param>
public sealed record PeopleSource(
    Func<PeopleEntry?, CancellationToken, Task<IReadOnlyList<PeopleEntry>>> Children,
    Func<string, CancellationToken, Task<IReadOnlyList<PeopleEntry>>>? Search = null,
    Func<CancellationToken, Task<IReadOnlyList<PeopleEntry>>>? Roles = null)
{
    /// <summary>
    /// 整棵在客户端的组织架构做成数据来源（vue-tsx-pro 的 localPeopleSource）：示例、测试与规模不大的系统直接用。
    /// 搜索按名字与说明（不分大小写），角色单独给；部门的 HasChildren 按有没有下级填上。
    /// </summary>
    public static PeopleSource Local(IReadOnlyList<PeopleNode> tree, IReadOnlyList<PeopleEntry>? roles = null)
    {
        roles ??= [];
        var byKey = new Dictionary<string, PeopleNode>();
        var all = new List<PeopleNode>();
        void Visit(IReadOnlyList<PeopleNode> level)
        {
            foreach (var node in level)
            {
                byKey[node.Key] = node;
                all.Add(node);
                Visit(node.Children ?? []);
            }
        }
        Visit(tree);
        PeopleEntry View(PeopleNode node) => node.Type == PeopleTypes.Department ? node.ToValue() with { HasChildren = node.Children is { Count: > 0 } } : node.ToValue();
        bool Matches(PeopleEntry entry, string text) =>
            entry.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase) || entry.Description?.Contains(text, StringComparison.CurrentCultureIgnoreCase) == true;
        return new PeopleSource(
            (parent, _) => Task.FromResult<IReadOnlyList<PeopleEntry>>([.. (parent is null ? tree : byKey.GetValueOrDefault(parent.Key)?.Children ?? []).Select(View)]),
            (query, _) =>
            {
                var text = query.Trim();
                return Task.FromResult<IReadOnlyList<PeopleEntry>>([.. all.Select(View).Concat(roles.Select(role => role.ToValue())).Where(entry => Matches(entry, text))]);
            },
            roles.Count > 0 ? _ => Task.FromResult<IReadOnlyList<PeopleEntry>>([.. roles.Select(role => role.ToValue())]) : null);
    }
}
