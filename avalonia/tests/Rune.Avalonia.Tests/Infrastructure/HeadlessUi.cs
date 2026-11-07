using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace Rune.Avalonia.Tests;

/// <summary>
/// headless 平台上的测试会话：所有测试共用一个 Avalonia 应用，测试体在它的 UI 线程上执行。
/// <code>
/// [Fact] public Task Foo() => HeadlessUi.Run(() => { var window = HeadlessUi.Show(control); … });
/// </code>
/// </summary>
static class HeadlessUi
{
    static readonly HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));

    /// <summary>在 UI 线程上执行测试体。</summary>
    public static Task Run(Action body) => session.Dispatch(body, TestContext.Current.CancellationToken);

    /// <summary>放进一个窗口并完成样式、模板与布局。</summary>
    public static Window Show(Control content)
    {
        var window = new Window { Width = 800, Height = 600, Content = content };
        window.Show();
        Flush(window);
        return window;
    }

    /// <summary>跑完排队的任务并布局：属性、样式、模板的变化都生效。</summary>
    public static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>边等真实时间边处理投递回 UI 线程的任务（库里的延时用 Task.Delay，headless 的调度器不走计时器）。</summary>
    public static void Pump(TimeSpan duration)
    {
        for (var clock = System.Diagnostics.Stopwatch.StartNew(); clock.Elapsed < duration;)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>边等边处理 UI 线程上的任务，直到条件成立（异步的事件处理、延时）；超过 3 秒算失败。</summary>
    public static void Until(Func<bool> condition)
    {
        for (var clock = System.Diagnostics.Stopwatch.StartNew(); !condition();)
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException("等了 3 秒条件仍不成立");
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }
    }
}

/// <summary>
/// 测试会话的入口（HeadlessUnitTestSession 按约定找 BuildAvaloniaApp）。弹出层（提示、菜单）建成独立的窗口，同桌面上一样：
/// headless 默认放进窗口模板里的覆盖层，测试的窗口只装了组件库的主题、没有模板。
/// </summary>
sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true, OverlayPopups = false });
}

/// <summary>测试用的应用：只装组件库自己的主题。</summary>
sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new RuneTheme());
}
