using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Rune.Avalonia.Examples;
using Rune.Inspect;

namespace Rune.Avalonia.Inspect;

static class Program
{
    static int Main(string[] args) => InspectProgram.Run(args, options =>
    {
        if (options.Command == "list") return Inspector.List(options);
        AppBuilder.Configure<InspectApp>()
            .UseSkia()
            .UseHarfBuzz()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        return Inspector.Run(options);
    });
}

/// <summary>同 Gallery 的样式：FluentTheme（UserControl 等平台控件的模板）加组件库的主题。</summary>
sealed class InspectApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new RuneTheme());
    }
}

/// <summary>
/// 把示例放进 headless 窗口（背景、文字颜色、字号、字体取主题，同一个区域的 Provider），再按命令输出。
/// 元素在 tree、props、uia 里用同一个称呼：有 Name 用 Name，否则"类型[序号]"（按元素树的先后数）。
/// </summary>
static class Inspector
{
    /// <summary>每个元素都列的样式属性（只列被设置过的；组件库自己的属性不论是否设置都列）。</summary>
    static readonly string[] CommonProperties = ["Background", "Foreground", "BorderBrush", "BorderThickness", "CornerRadius", "Padding", "Margin", "FontSize", "FontWeight", "Opacity", "FlowDirection"];

    public static string List(InspectOptions options)
    {
        var table = new ReportTable("示例", "标题", "说明");
        foreach (var example in ExampleCatalog.All) table.Add(example.Key, example.Title, example.Description);
        return table.Render(options.Json);
    }

    public static string Run(InspectOptions options)
    {
        var example = ExampleCatalog.Find(options.Key!) ?? throw new UsageException($"没有示例 {options.Key}；inspect list 列出全部");
        var theme = RuneThemeVariants.All.FirstOrDefault(variant => string.Equals(variant.Key.ToString(), options.Theme, StringComparison.OrdinalIgnoreCase))
            ?? throw new UsageException($"没有主题 {options.Theme}；可选：{string.Join("、", RuneThemeVariants.All.Select(variant => variant.Key))}");
        Application.Current!.RequestedThemeVariant = theme;
        var content = example.Create();
        var window = Show(content, options);
        try
        {
            var labels = Labels(content);
            return options.Command switch
            {
                "snapshot" => Snapshot(window, options),
                "tree" => Tree(content, labels).Render(options.Json),
                "props" => Props(content, labels, theme).Render(options.Json),
                _ => Uia(content, labels).Render(options.Json),
            };
        }
        finally
        {
            window.Close();
        }
    }

    static Window Show(Control content, InspectOptions options)
    {
        var root = new Border { Padding = new Thickness(24), Child = content };
        root.Bind(Border.BackgroundProperty, root.GetResourceObservable("Rune.Brush.Background"));
        root.Bind(TextElement.ForegroundProperty, root.GetResourceObservable("Rune.Brush.Text"));
        root.Bind(TextElement.FontSizeProperty, root.GetResourceObservable("Rune.FontSize.Md"));
        root.Bind(TextElement.FontFamilyProperty, root.GetResourceObservable("Rune.Font.Sans"));
        var window = new Window { Width = options.Width, SizeToContent = SizeToContent.Height, Content = root };
        window.Show();
        window.SetRenderScaling(options.Scale);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    static string Snapshot(Window window, InspectOptions options)
    {
        var path = Report.SnapshotPath("avalonia", options);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("headless 窗口没有渲染出帧");
        frame.Save(path, PngBitmapEncoderOptions.Default);
        return $"{path}（{frame.PixelSize.Width}×{frame.PixelSize.Height}）{Environment.NewLine}";
    }

    static ReportTable Tree(Control root, Dictionary<Visual, string> labels)
    {
        var table = new ReportTable("元素", "类型", "类与伪类", "边界", "所在模板");
        Walk(root, 0);
        return table;

        void Walk(Visual visual, int depth)
        {
            var origin = visual.TranslatePoint(default, root) ?? default;
            var styled = visual as StyledElement;
            table.Add(
                $"{string.Concat(Enumerable.Repeat("· ", depth))}{labels[visual]}",
                visual.GetType().Name,
                styled is null ? null : string.Join(" ", styled.Classes),
                $"{Report.Format(origin.X)},{Report.Format(origin.Y)} {Report.Format(visual.Bounds.Width)}×{Report.Format(visual.Bounds.Height)}",
                styled?.TemplatedParent is { } parent ? labels.GetValueOrDefault((Visual)parent, parent.GetType().Name) : null);
            foreach (var child in visual.GetVisualChildren()) Walk(child, depth + 1);
        }
    }

    static ReportTable Props(Control root, Dictionary<Visual, string> labels, ThemeVariant theme)
    {
        var resourceKeys = BrushKeys(theme);
        var table = new ReportTable("元素", "属性", "值", "来源");
        foreach (var element in root.GetSelfAndVisualDescendants().OfType<StyledElement>())
        {
            var ownNamespace = typeof(RuneTheme).Namespace;
            var own = AvaloniaPropertyRegistry.Instance.GetRegistered(element).Where(property => property.OwnerType.Namespace == ownNamespace).ToList();
            var common = CommonProperties.Select(name => AvaloniaPropertyRegistry.Instance.FindRegistered(element, name)).OfType<AvaloniaProperty>();
            foreach (var property in own.Concat(common).Distinct())
            {
                var value = element.GetDiagnostic(property);
                if (!own.Contains(property) && value.Priority >= BindingPriority.Inherited) continue;
                table.Add(labels[(Visual)element], property.Name, Format(value.Value, resourceKeys), Source(value.Priority));
            }
        }
        return table;
    }

    static ReportTable Uia(Control root, Dictionary<Visual, string> labels)
    {
        // 只列控件视图（IsControlElement）：读屏看到的就是这些；不在控件视图里的节点，它的子节点上提一层
        var table = new ReportTable("节点", "角色", "名称", "元素", "可聚焦", "Pattern");
        Walk(ControlAutomationPeer.CreatePeerForElement(root), 0);
        return table;

        void Walk(AutomationPeer peer, int depth)
        {
            if (peer.IsControlElement())
            {
                var owner = (peer as ControlAutomationPeer)?.Owner;
                table.Add(
                    $"{string.Concat(Enumerable.Repeat("· ", depth))}{peer.GetAutomationControlType()}",
                    peer.GetAutomationControlType(),
                    peer.GetName(),
                    owner is null ? null : labels.GetValueOrDefault(owner),
                    peer.IsKeyboardFocusable(),
                    Patterns(peer));
                depth++;
            }
            foreach (var child in peer.GetChildren()) Walk(child, depth);
        }
    }

    static string? Patterns(AutomationPeer peer)
    {
        var patterns = new (string Name, bool Supported)[]
        {
            ("Invoke", peer.GetProvider<IInvokeProvider>() is not null),
            ("Toggle", peer.GetProvider<IToggleProvider>() is not null),
            ("Value", peer.GetProvider<IValueProvider>() is not null),
            ("RangeValue", peer.GetProvider<IRangeValueProvider>() is not null),
            ("ExpandCollapse", peer.GetProvider<IExpandCollapseProvider>() is not null),
            ("Selection", peer.GetProvider<ISelectionProvider>() is not null),
            ("SelectionItem", peer.GetProvider<ISelectionItemProvider>() is not null),
            ("Scroll", peer.GetProvider<IScrollProvider>() is not null),
        };
        var supported = patterns.Where(pattern => pattern.Supported).Select(pattern => pattern.Name).ToList();
        return supported.Count == 0 ? null : string.Join("、", supported);
    }

    /// <summary>元素的称呼：有 Name 用 Name，否则"类型[序号]"。</summary>
    static Dictionary<Visual, string> Labels(Control root)
    {
        var counts = new Dictionary<string, int>();
        return root.GetSelfAndVisualDescendants().ToDictionary(visual => visual, visual =>
        {
            if (visual is StyledElement { Name: { Length: > 0 } name }) return name;
            var type = visual.GetType().Name;
            counts[type] = counts.GetValueOrDefault(type) + 1;
            return $"{type}[{counts[type]}]";
        });
    }

    /// <summary>主题里的画刷 → 资源键：属性值是同一个画刷对象时，报出它来自哪个 token。</summary>
    static Dictionary<object, string> BrushKeys(ThemeVariant theme)
    {
        var keys = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        foreach (var styles in Application.Current!.Styles.OfType<RuneTheme>()) Collect(styles.Resources);
        return keys;

        void Collect(IResourceDictionary dictionary)
        {
            foreach (var key in dictionary.Keys.OfType<string>())
                if (dictionary.TryGetResource(key, theme, out var value) && value is IBrush) keys.TryAdd(value, key);
            foreach (var merged in dictionary.MergedDictionaries)
                if ((merged as IResourceDictionary ?? (merged as ResourceInclude)?.Loaded) is { } loaded) Collect(loaded);
            for (var variant = theme; variant is not null; variant = variant.InheritVariant)
                if (dictionary.ThemeDictionaries.TryGetValue(variant, out var themed) && themed is IResourceDictionary themedDictionary) Collect(themedDictionary);
        }
    }

    static string Format(object? value, Dictionary<object, string> brushKeys) => value switch
    {
        _ when value == AvaloniaProperty.UnsetValue => "—",
        ISolidColorBrush brush => brushKeys.TryGetValue(brush, out var key) ? $"{key}（{brush.Color}）" : brush.Color.ToString(),
        IconData data => data.Name,
        _ => Report.Format(value),
    };

    static string Source(BindingPriority priority) => priority switch
    {
        BindingPriority.Animation => "动画",
        BindingPriority.LocalValue => "本地值",
        BindingPriority.StyleTrigger => "样式（带条件）",
        BindingPriority.Template => "模板",
        BindingPriority.Style => "样式",
        BindingPriority.Inherited => "继承",
        _ => "默认值",
    };
}
