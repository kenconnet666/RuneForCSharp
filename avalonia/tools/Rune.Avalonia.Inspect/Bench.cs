using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Rune.Inspect;

namespace Rune.Avalonia.Inspect;

/// <summary>
/// 基准（<c>inspect bench</c>）：每个对象建 N 个放进 headless 窗口的 WrapPanel，依次量
/// 创建（构造）、布局（套模板、样式匹配、测量排列）、渲染（Skia 画一帧）、换主题（亮切暗后重新布局）。
/// 对照组是平台自带的 Button（Fluent 模板），看组件库在它之上多花了多少。
/// </summary>
static class BenchRunner
{
    static readonly (string Name, Func<Control> Create)[] Subjects =
    [
        ("平台 Button（Fluent）", () => new global::Avalonia.Controls.Button { Content = "确定" }),
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
        var variants = RuneThemeVariants.All;
        var light = variants.First(v => string.Equals(v.Key.ToString(), "light", StringComparison.OrdinalIgnoreCase));
        var dark = variants.First(v => string.Equals(v.Key.ToString(), "dark", StringComparison.OrdinalIgnoreCase));
        var report = new BenchReport("创建 ms", "布局 ms", "渲染 ms", "换主题 ms", "分配 MB", "常驻 KB/个");
        foreach (var (name, create) in Subjects.Where(s => options.Key is null || s.Name.StartsWith(options.Key, StringComparison.OrdinalIgnoreCase)))
        {
            for (var run = 0; run < options.Runs; run++)
            {
                Application.Current!.RequestedThemeVariant = light;
                var panel = new WrapPanel();
                var window = new Window { Width = options.Width, Height = 800, Content = panel };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var before = Bench.Settled();
                var allocated = Bench.Allocated();

                var controls = new Control[options.Count];
                var created = Bench.Time(() =>
                {
                    for (var i = 0; i < controls.Length; i++) controls[i] = create();
                });
                var laidOut = Bench.Time(() =>
                {
                    panel.Children.AddRange(controls);
                    window.UpdateLayout();
                });
                var rendered = Bench.Time(() => window.CaptureRenderedFrame()?.Dispose());
                var allocatedMb = (Bench.Allocated() - allocated) / 1048576d;
                var retainedKb = (Bench.Settled() - before) / 1024d / controls.Length;
                var switched = Bench.Time(() =>
                {
                    Application.Current.RequestedThemeVariant = dark;
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                });

                window.Close();
                Dispatcher.UIThread.RunJobs();
                if (run == 0) continue;
                report.Add(name, "创建 ms", created);
                report.Add(name, "布局 ms", laidOut);
                report.Add(name, "渲染 ms", rendered);
                report.Add(name, "换主题 ms", switched);
                report.Add(name, "分配 MB", allocatedMb);
                report.Add(name, "常驻 KB/个", retainedKb);
            }
        }
        Application.Current!.RequestedThemeVariant = light;
        return $"{options.Count} 个，{options.Runs - 1} 轮取中位数（headless，Skia）{Environment.NewLine}{Environment.NewLine}{report.Render(options.Json)}";
    }
}
