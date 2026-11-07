using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Rune.Wpf.Tests;

/// <summary>
/// WPF 测试的宿主：一个常驻的 STA 线程跑 Dispatcher 与 Application，测试体排到它上面执行。
/// <code>
/// [Fact] public Task Foo() => WpfUi.Run(() => { var window = WpfUi.Show(element); … });
/// </code>
/// 窗口放在屏幕外：WPF 照常渲染与播放动画（WinForms 离屏收不到 WM_PAINT，这里不受影响）。
/// </summary>
static class WpfUi
{
    static readonly Dispatcher dispatcher = StartDispatcher();

    static readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>
    /// 在 UI 线程上执行测试体；测试体里的异常原样抛给测试。一次只投递一个测试体：测试体里的 <see cref="Flush"/>、
    /// <see cref="Wait"/> 会推消息帧，别的测试体若已经排在队里就会在里面重入执行，改共享状态的测试互相干扰。
    /// </summary>
    public static async Task Run(Action body)
    {
        await gate.WaitAsync();
        try
        {
            await dispatcher.InvokeAsync(body).Task;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>放进屏幕外的窗口并完成样式、模板与布局。</summary>
    public static Window Show(UIElement content)
    {
        var window = new Window
        {
            Content = content, Width = 800, Height = 600, Left = -20000, Top = -20000,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
        };
        window.Show();
        Flush();
        return window;
    }

    /// <summary>处理完排队的工作（布局、数据绑定、渲染之后的空闲）。</summary>
    public static void Flush()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    /// <summary>可视树里的全部后代（深度优先）。</summary>
    public static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    /// <summary>边处理消息边等一段真实时间（动画按真实时间推进）。</summary>
    public static void Wait(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    /// <summary>边处理消息边等，直到条件成立（异步的事件处理、延时）；超过 3 秒算失败。</summary>
    public static void Until(Func<bool> condition)
    {
        for (var clock = System.Diagnostics.Stopwatch.StartNew(); !condition();)
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException("等了 3 秒条件仍不成立");
            Wait(TimeSpan.FromMilliseconds(10));
        }
    }

    static Dispatcher StartDispatcher()
    {
        Dispatcher? result = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            result = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "WPF 测试 UI 线程",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return result!;
    }
}
