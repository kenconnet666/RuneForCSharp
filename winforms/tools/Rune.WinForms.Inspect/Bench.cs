using Rune.Inspect;

namespace Rune.WinForms.Inspect;

/// <summary>
/// 基准（<c>inspect bench</c>）：每个对象建 N 个元素放进一个多元素宿主（列表、表格这类数量大的地方的用法，DESIGN §9），依次量
/// 创建（构造）、布局（测量、排成几行）、渲染（每个元素在自己的绘制 effect 里画到 BufferedGraphics 上）、
/// 换主题（亮切暗：信号通知、失效，再全部重画）。单元素宿主（XxxControl）每个一个 HWND，不在这里比。
/// </summary>
static class BenchRunner
{
    static readonly (string Name, Func<Element> Create)[] Subjects =
    [
        ("Text", () => new Text { Content = "确定" }),
        ("Icon", () => new Icon { Data = Lucide.Plus }),
        ("Spinner", () => new Spinner()),
        ("Badge", () => new Badge { Content = "Beta" }),
        ("Badge 带图标", () => new Badge { Content = "已通过", Icon = Lucide.CircleCheck, Tone = StatusTone.Success }),
        ("Button", () => new Button { Content = "确定" }),
        ("Button 带图标", () => new Button { Content = "新建", Icon = Lucide.Plus }),
        ("IconButton", () => new IconButton { Icon = Lucide.Settings }),
        ("LinkButton", () => new LinkButton { Content = "文档", Href = new Uri("https://example.com") }),
    ];

    public static string Run(InspectOptions options)
    {
        var report = new BenchReport("创建 ms", "布局 ms", "渲染 ms", "换主题 ms", "分配 MB", "常驻 KB/个");
        foreach (var (name, create) in Subjects.Where(s => options.Key is null || s.Name.StartsWith(options.Key, StringComparison.OrdinalIgnoreCase)))
        {
            for (var run = 0; run < options.Runs; run++)
            {
                RuneTheme.Current.Value = BuiltinThemes.Light;
                using var form = new Form
                {
                    StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000), FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
                    Font = new Font(Fonts.Installed(BuiltinThemes.Light.Fonts.Sans), Tokens.FontSize.Md * 72 / 96),
                };
                form.Show();
                form.ClientSize = new Size((int)(options.Width * form.DeviceDpi / 96f), 800);
                using var host = new BenchHost { Dock = DockStyle.Fill };
                form.Controls.Add(host);
                Application.DoEvents();
                var before = Bench.Settled();
                var allocated = Bench.Allocated();

                var elements = new Element[options.Count];
                var created = Bench.Time(() =>
                {
                    for (var i = 0; i < elements.Length; i++) elements[i] = create();
                });
                var laidOut = Bench.Time(() => host.Arrange(elements));
                var rendered = Bench.Time(host.PaintAll);
                var allocatedMb = (Bench.Allocated() - allocated) / 1048576d;
                var retainedKb = (Bench.Settled() - before) / 1024d / elements.Length;
                var switched = Bench.Time(() =>
                {
                    RuneTheme.Current.Value = BuiltinThemes.Dark;
                    host.PaintAll();
                });

                if (run == 0) continue;
                report.Add(name, "创建 ms", created);
                report.Add(name, "布局 ms", laidOut);
                report.Add(name, "渲染 ms", rendered);
                report.Add(name, "换主题 ms", switched);
                report.Add(name, "分配 MB", allocatedMb);
                report.Add(name, "常驻 KB/个", retainedKb);
            }
        }
        RuneTheme.Current.Value = BuiltinThemes.Light;
        return $"{options.Count} 个元素，{options.Runs - 1} 轮取中位数（一个宿主控件，BufferedGraphics）{Environment.NewLine}{Environment.NewLine}{report.Render(options.Json)}";
    }

    /// <summary>最简单的多元素宿主：按行排开元素，每个元素一个绘制 effect；失效只记下来，由基准自己重画。</summary>
    sealed class BenchHost : Control, IElementHost
    {
        readonly List<(Element Element, Effect Paint)> elements = [];

        ElementContext Context => new(DeviceDpi, Font, ForeColor, false);

        /// <summary>接上元素，量好、排成几行（间距 8 设备无关像素）。</summary>
        public void Arrange(Element[] items)
        {
            var context = Context;
            var gap = context.Scale(8);
            var (x, y, line) = (0f, 0f, 0);
            foreach (var element in items)
            {
                element.Host = this;
                elements.Add((element, new Effect(() => Invalidate(element.Bounds))));
                var size = element.Measure(context, new Size(Width, int.MaxValue));
                if (x > 0 && x + size.Width > Width) (x, y, line) = (0, y + line + gap, 0);
                element.Bounds = new Rectangle((int)x, (int)y, size.Width, size.Height);
                x += size.Width + gap;
                line = Math.Max(line, size.Height);
            }
        }

        /// <summary>每个元素在自己的绘制 effect 里画一遍（画到 BufferedGraphics：GDI+ 位图上的 TextRenderer 会反复复制整张位图，DESIGN §9）。</summary>
        public void PaintAll()
        {
            var context = Context;
            var bottom = elements.Count == 0 ? 1 : elements.Max(e => e.Element.Bounds.Bottom);
            using var screen = CreateGraphics();
            using var buffer = BufferedGraphicsManager.Current.Allocate(screen, new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, bottom)));
            foreach (var (element, paint) in elements)
            {
                using var _ = paint.Track();
                element.Paint(buffer.Graphics, context);
            }
        }

        void IElementHost.InvalidateElement(Element element) => Invalidate(element.Bounds);

        void IElementHost.InvalidateLayout(Element element) => Invalidate();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var (_, paint) in elements) paint.Dispose();
            base.Dispose(disposing);
        }
    }
}
