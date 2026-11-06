using System.Runtime.CompilerServices;
using System.Windows;

namespace Rune.Wpf;

/// <summary>
/// 部件样式键（DESIGN §7）：ComponentResourceKey 加上部件元素的类型。组件的部件键都用它声明：
/// <code>
/// public static readonly PartKey Icon = new(typeof(Spinner), nameof(Icon), typeof(Icon));
/// </code>
/// Provider 靠部件的类型把同类型的类型键样式叠到部件样式下面（ROADMAP 决定 19）：Provider.Styles 里给所有 rune:Icon 写的样式，
/// Spinner、Badge 里的图标部件也收到，部件键自己的样式再叠在上面。
/// </summary>
public sealed class PartKey : ComponentResourceKey
{
    static readonly List<PartKey> created = [];
    static readonly Lazy<bool> libraryLoaded = new(LoadLibraryParts);

    /// <summary>新建一个部件键并登记。</summary>
    /// <param name="owner">部件所属的组件（也决定到哪个程序集的主题里找部件样式）。</param>
    /// <param name="name">部件名。</param>
    /// <param name="targetType">部件元素的类型（部件样式的 TargetType）。</param>
    public PartKey(Type owner, string name, Type targetType) : base(owner, name)
    {
        TargetType = targetType;
        lock (created) created.Add(this);
    }

    /// <summary>部件元素的类型。</summary>
    public Type TargetType { get; }

    /// <summary>类型正好是 <paramref name="type"/> 的部件键（隐式样式按确切的类型生效，这里同样）。</summary>
    internal static List<PartKey> For(Type type)
    {
        _ = libraryLoaded.Value;
        lock (created) return created.Where(key => key.TargetType == type).ToList();
    }

    /// <summary>
    /// 组件库里各 *Parts 类的静态字段在第一次用到时才创建；Provider 合成时组件可能还没出现，先把库里的部件键都建出来。
    /// 使用方自己的部件键在创建时登记。
    /// </summary>
    static bool LoadLibraryParts()
    {
        foreach (var type in typeof(PartKey).Assembly.GetTypes().Where(t => t is { IsAbstract: true, IsSealed: true } && t.Name.EndsWith("Parts", StringComparison.Ordinal)))
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        return true;
    }
}
