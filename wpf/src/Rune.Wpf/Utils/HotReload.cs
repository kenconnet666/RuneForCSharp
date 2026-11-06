using System.Reflection.Metadata;
using System.Windows;

[assembly: MetadataUpdateHandler(typeof(Rune.Wpf.HotReloadHandler))]

namespace Rune.Wpf;

/// <summary>
/// .NET 热重载（dotnet watch、Rider、Visual Studio 改了 C# 代码）之后的通知，已切回 UI 线程。
/// 组件库自己的缓存（Provider 的合成结果等）在这里失效重建。
/// </summary>
public static class HotReload
{
    /// <summary>代码更新已应用。</summary>
    public static event Action? Applied;

    internal static void Raise()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Applied?.Invoke();
        else dispatcher.BeginInvoke(() => Applied?.Invoke());
    }
}

/// <summary>运行时按约定调用的热重载回调（在后台线程上）。</summary>
static class HotReloadHandler
{
    public static void ClearCache(Type[]? updatedTypes) { }

    public static void UpdateApplication(Type[]? updatedTypes) => HotReload.Raise();
}
