using System.Reflection.Metadata;

[assembly: MetadataUpdateHandler(typeof(Rune.WinForms.HotReloadHandler))]

namespace Rune.WinForms;

/// <summary>
/// .NET 热重载（dotnet watch、Rider、Visual Studio 改了 C# 代码）之后的通知。
/// 组件的样式解析（配方、按档位与作用域算外观）读 <see cref="CodeVersion"/>：代码更新后它加一，
/// 读过它的派生值全部标脏，依赖它们的绘制 effect 让对应控件重绘——改了配方里的颜色，运行中的界面立即变（实测约 1.6 秒）。
/// </summary>
public static class HotReload
{
    /// <summary>代码版本：每次热重载加一。在 UI 线程上更新。</summary>
    public static Signal<int> CodeVersion { get; } = new(0);

    /// <summary>
    /// 用来切回 UI 线程的控件（运行时在后台线程上回调）。组件库在第一个组件创建句柄时设置，应用一般不用管。
    /// </summary>
    internal static Control? UiRoot { get; set; }

    internal static void Raise()
    {
        var root = UiRoot;
        if (root is { IsHandleCreated: true, InvokeRequired: true }) root.BeginInvoke(() => CodeVersion.Value++);
        else CodeVersion.Value++;
    }
}

/// <summary>运行时按约定调用的热重载回调（在后台线程上）。</summary>
static class HotReloadHandler
{
    public static void ClearCache(Type[]? updatedTypes) { }

    public static void UpdateApplication(Type[]? updatedTypes) => HotReload.Raise();
}
