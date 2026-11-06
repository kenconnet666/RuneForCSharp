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
}

/// <summary>测试会话的入口（HeadlessUnitTestSession 按约定找 BuildAvaloniaApp）。</summary>
sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}

/// <summary>测试用的应用：只装组件库自己的主题。</summary>
sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new RuneTheme());
}
