using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Rune.Inspect;

namespace Rune.Wpf.Inspect;

/// <summary>
/// 基准（<c>inspect bench</c>）：每个对象建 N 个放进屏幕外窗口的 WrapPanel，依次量
/// 创建（构造）、布局（套样式与模板、测量排列）、渲染（RenderTargetBitmap 软件渲染一帧）、换主题（亮切暗后重新布局）。
/// 对照组是平台自带的 Button（Aero2 模板），看组件库在它之上多花了多少。
/// </summary>
static class BenchRunner
{
    static readonly (string Name, Func<FrameworkElement> Create)[] Subjects =
    [
        ("平台 Button（Aero2）", () => new System.Windows.Controls.Button { Content = "确定" }),
        ("Text", () => new Text { Text = "确定" }),
        ("Icon", () => new Icon { Data = Lucide.Plus }),
        ("Spinner", () => new Spinner()),
        ("Badge", () => new Badge { Content = "Beta" }),
        ("Badge 带图标", () => new Badge { Content = "已通过", Icon = Lucide.CircleCheck, Tone = StatusTone.Success }),
        ("ButtonBase", () => new ButtonBase { Content = "确定" }),
        ("Button", () => new Button { Content = "确定" }),
        ("Button 带图标", () => new Button { Content = "新建", Icon = Lucide.Plus }),
        ("IconButton", () => new IconButton { Icon = Lucide.Settings }),
        ("LinkButton", () => new LinkButton { Content = "文档", Href = new Uri("https://example.com") }),
    ];

    public static string Run(InspectOptions options)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var theme = new RuneTheme { Theme = "light" };
        app.Resources.MergedDictionaries.Add(theme);
        var report = new BenchReport("创建 ms", "布局 ms", "渲染 ms", "换主题 ms", "分配 MB", "常驻 KB/个");
        foreach (var (name, create) in Subjects.Where(s => options.Key is null || s.Name.StartsWith(options.Key, StringComparison.OrdinalIgnoreCase)))
        {
            for (var run = 0; run < options.Runs; run++)
            {
                theme.Theme = "light";
                var panel = new WrapPanel();
                var window = new Window
                {
                    Content = panel, Width = options.Width, Height = 800, Left = -20000, Top = -20000,
                    ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
                };
                window.Show();
                Flush();
                var before = Bench.Settled();
                var allocated = Bench.Allocated();

                var controls = new FrameworkElement[options.Count];
                var created = Bench.Time(() =>
                {
                    for (var i = 0; i < controls.Length; i++) controls[i] = create();
                });
                var laidOut = Bench.Time(() =>
                {
                    foreach (var control in controls) panel.Children.Add(control);
                    window.UpdateLayout();
                });
                var rendered = Bench.Time(() =>
                {
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(panel.ActualWidth), (int)Math.Ceiling(panel.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(panel);
                });
                var allocatedMb = (Bench.Allocated() - allocated) / 1048576d;
                var retainedKb = (Bench.Settled() - before) / 1024d / controls.Length;
                var switched = Bench.Time(() =>
                {
                    theme.Theme = "dark";
                    window.UpdateLayout();
                });

                window.Close();
                Flush();
                if (run == 0) continue;
                report.Add(name, "创建 ms", created);
                report.Add(name, "布局 ms", laidOut);
                report.Add(name, "渲染 ms", rendered);
                report.Add(name, "换主题 ms", switched);
                report.Add(name, "分配 MB", allocatedMb);
                report.Add(name, "常驻 KB/个", retainedKb);
            }
        }
        return $"{options.Count} 个，{options.Runs - 1} 轮取中位数（屏幕外窗口，RenderTargetBitmap）{Environment.NewLine}{Environment.NewLine}{report.Render(options.Json)}";
    }

    /// <summary>处理完排队的布局、渲染与加载事件。</summary>
    static void Flush()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
