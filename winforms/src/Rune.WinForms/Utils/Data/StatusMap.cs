using System.Collections;

namespace Rune.WinForms;

/// <summary>
/// 状态映射（vue-tsx-pro 的 createStatusMap）：一处写好每种状态的名字、色调、图标与说明，StatusBadge、表格的状态列、筛选、步骤共用，
/// 同一种状态在哪儿都是同一个颜色、同一个叫法。写的顺序就是阶段的顺序。
/// <code>
/// public static readonly StatusMap TaskStatuses = new()
/// {
///     ["pending"] = new("待启动", StatusTone.Neutral),
///     ["designing"] = new("设计中", StatusTone.Info),
///     ["closed"] = new("关闭", StatusTone.Success),
/// };
/// </code>
/// 不认识的 key（接口多给了新状态）当成中性色、名字就是 key，不报错。三个库各有一份，除命名空间外逐字相同（tools/twins）。
/// </summary>
public sealed class StatusMap : IEnumerable<KeyValuePair<string, StatusDefinition>>
{
    readonly List<string> keys = [];
    readonly Dictionary<string, StatusDefinition> definitions = [];

    /// <summary>全部状态的 key，按写的顺序（阶段顺序）。</summary>
    public IReadOnlyList<string> Keys => keys;

    /// <summary>一种状态的定义；写入时按顺序追加（集合初始化器用）。</summary>
    /// <param name="key">状态的 key。</param>
    public StatusDefinition this[string key]
    {
        get => Get(key);
        set
        {
            if (!definitions.ContainsKey(key)) keys.Add(key);
            definitions[key] = value;
        }
    }

    /// <summary>一种状态的定义；不认识的 key 是中性色、名字就是 key。</summary>
    /// <param name="key">状态的 key。</param>
    public StatusDefinition Get(string key) => definitions.TryGetValue(key, out var definition) ? definition : new StatusDefinition(key, StatusTone.Neutral);

    /// <summary>名字。</summary>
    /// <param name="key">状态的 key。</param>
    public string Label(string key) => Get(key).Label;

    /// <summary>色调。</summary>
    /// <param name="key">状态的 key。</param>
    public StatusTone Tone(string key) => Get(key).Tone;

    /// <summary>在阶段里的位置（从 0 起）；不认识的 key 是 -1。</summary>
    /// <param name="key">状态的 key。</param>
    public int Order(string key) => keys.IndexOf(key);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, StatusDefinition>> GetEnumerator() => keys.Select(key => KeyValuePair.Create(key, definitions[key])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>一种状态的外观与文案（vue-tsx-pro 的 StatusDefinition）。</summary>
/// <param name="Label">显示的名字。</param>
/// <param name="Tone">色调。</param>
/// <param name="Icon">名字前的图标；不写时 StatusBadge 画一个同色的圆点。</param>
/// <param name="Description">说明：筛选项、提示里显示。</param>
public sealed record StatusDefinition(string Label, StatusTone Tone, IconData? Icon = null, string? Description = null);
