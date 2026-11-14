using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 过一段时间后在界面线程上做一件事（复制后复原、滚动条淡出、加载指示的延迟）。
/// 等待用 Task.Delay（headless 测试的调度器不走 DispatcherTimer），回来时投递给调用时所在的调度器：
/// 调度器要在界面线程上先取好——在线程池里才取 <see cref="Dispatcher.UIThread"/>，赶上它还没建（两个 headless 测试之间）时会在池线程上建一个，
/// 下一个测试的应用就因"另一个线程拥有它"起不来。
/// </summary>
static class UiDelay
{
    /// <summary>过 <paramref name="delay"/> 后在当前的界面线程上执行 <paramref name="action"/>（要在界面线程上调用）。</summary>
    public static void Run(TimeSpan delay, Action action)
    {
        var dispatcher = Dispatcher.UIThread;
        _ = Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero).ContinueWith(_ => dispatcher.Post(action), TaskScheduler.Default);
    }
}
