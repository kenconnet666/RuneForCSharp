using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Rune.Inspect;
using Rune.Wpf.Examples;

namespace Rune.Wpf.Inspect;

static class Program
{
    [STAThread]
    static int Main(string[] args) => InspectProgram.Run(args, options => options.Command == "list" ? Inspector.List(options) : Inspector.Run(options));
}

/// <summary>
/// 把示例放进屏幕外的窗口（背景、文字颜色、字号取主题，同一个区域的 Provider），再按命令输出。
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
        if (RuneTheme.BuiltinThemes.All(theme => theme.Name != options.Theme))
            throw new UsageException($"没有主题 {options.Theme}；可选：{string.Join("、", RuneTheme.BuiltinThemes.Select(theme => theme.Name))}");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new RuneTheme { Theme = options.Theme });
        var content = example.Create();
        var (window, root) = Show(content, options);
        try
        {
            var labels = Labels(content);
            return options.Command switch
            {
                "snapshot" => Snapshot(root, options),
                "tree" => Tree(content, labels).Render(options.Json),
                "props" => Props(content, labels).Render(options.Json),
                _ => Uia(content, labels).Render(options.Json),
            };
        }
        finally
        {
            window.Close();
        }
    }

    static (Window Window, Border Root) Show(FrameworkElement content, InspectOptions options)
    {
        var root = new Border { Padding = new Thickness(24), Child = content };
        root.SetResourceReference(Border.BackgroundProperty, "Rune.Brush.Background");
        root.SetResourceReference(TextElement.ForegroundProperty, "Rune.Brush.Text");
        root.SetResourceReference(TextElement.FontSizeProperty, "Rune.FontSize.Md");
        var window = new Window
        {
            Content = root, Width = options.Width, SizeToContent = SizeToContent.Height, Left = -20000, Top = -20000,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
        };
        window.Show();
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
        return (window, root);
    }

    static string Snapshot(FrameworkElement root, InspectOptions options)
    {
        var path = Report.SnapshotPath("wpf", options);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(root.ActualWidth * options.Scale), (int)Math.Ceiling(root.ActualHeight * options.Scale),
            96 * options.Scale, 96 * options.Scale, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(path)) encoder.Save(file);
        return $"{path}（{bitmap.PixelWidth}×{bitmap.PixelHeight}）{Environment.NewLine}";
    }

    static ReportTable Tree(FrameworkElement root, Dictionary<DependencyObject, string> labels)
    {
        var table = new ReportTable("元素", "类型", "样式", "边界", "所在模板");
        Walk(root, 0);
        return table;

        void Walk(DependencyObject node, int depth)
        {
            var element = node as FrameworkElement;
            var bounds = node is Visual visual && element is not null
                ? visual.TransformToAncestor(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight))
                : Rect.Empty;
            table.Add(
                $"{string.Concat(Enumerable.Repeat("· ", depth))}{labels[node]}",
                node.GetType().Name,
                element is null ? null : StyleOrigin(element),
                bounds.IsEmpty ? null : $"{Report.Format(bounds.X)},{Report.Format(bounds.Y)} {Report.Format(bounds.Width)}×{Report.Format(bounds.Height)}",
                element?.TemplatedParent is { } parent ? labels.GetValueOrDefault(parent, parent.GetType().Name) : null);
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i), depth + 1);
        }
    }

    /// <summary>元素的 Style 从哪来：隐式（按类型查到的）、显式（写了 Style）、没有。</summary>
    static string? StyleOrigin(FrameworkElement element) => DependencyPropertyHelper.GetValueSource(element, FrameworkElement.StyleProperty).BaseValueSource switch
    {
        BaseValueSource.ImplicitStyleReference => "隐式",
        BaseValueSource.Default => null,
        _ => "显式",
    };

    static ReportTable Props(FrameworkElement root, Dictionary<DependencyObject, string> labels)
    {
        var brushKeys = BrushKeys();
        var table = new ReportTable("元素", "属性", "值", "来源");
        foreach (var (node, label) in labels)
        {
            var own = OwnProperties(node.GetType()).ToList();
            var common = CommonProperties.Select(name => DependencyPropertyDescriptor.FromName(name, node.GetType(), node.GetType())?.DependencyProperty).OfType<DependencyProperty>();
            foreach (var property in own.Concat(common).Distinct())
            {
                var source = DependencyPropertyHelper.GetValueSource(node, property);
                if (!own.Contains(property) && source.BaseValueSource is BaseValueSource.Default or BaseValueSource.Inherited) continue;
                table.Add(label, property.Name, Format(node.GetValue(property), brushKeys), Source(source));
            }
        }
        return table;
    }

    /// <summary>组件库在这个类型（与基类）上声明的依赖属性，含私有的（如 Icon 的 ResolvedSize）。</summary>
    static IEnumerable<DependencyProperty> OwnProperties(Type type)
    {
        for (var current = type; current is not null && current.Namespace == typeof(RuneTheme).Namespace; current = current.BaseType)
            foreach (var field in current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                if (field.GetValue(null) is DependencyProperty property) yield return property;
    }

    static ReportTable Uia(FrameworkElement root, Dictionary<DependencyObject, string> labels)
    {
        // 只列控件视图（IsControlElement）：读屏看到的就是这些；不在控件视图里的节点，它的子节点上提一层
        var table = new ReportTable("节点", "角色", "名称", "元素", "可聚焦", "Pattern");
        Walk(UIElementAutomationPeer.CreatePeerForElement(root), 0);
        return table;

        void Walk(AutomationPeer peer, int depth)
        {
            if (peer.IsControlElement())
            {
                var owner = (peer as UIElementAutomationPeer)?.Owner;
                table.Add(
                    $"{string.Concat(Enumerable.Repeat("· ", depth))}{peer.GetAutomationControlType()}",
                    peer.GetAutomationControlType(),
                    peer.GetName(),
                    owner is null ? null : labels.GetValueOrDefault(owner),
                    peer.IsKeyboardFocusable(),
                    Patterns(peer));
                depth++;
            }
            foreach (var child in peer.GetChildren() ?? []) Walk(child, depth);
        }
    }

    static string? Patterns(AutomationPeer peer)
    {
        PatternInterface[] patterns = [PatternInterface.Invoke, PatternInterface.Toggle, PatternInterface.Value, PatternInterface.RangeValue, PatternInterface.ExpandCollapse, PatternInterface.Selection, PatternInterface.SelectionItem, PatternInterface.Scroll];
        var supported = patterns.Where(pattern => peer.GetPattern(pattern) is not null).ToList();
        return supported.Count == 0 ? null : string.Join("、", supported);
    }

    /// <summary>元素的称呼：有 Name 用 Name，否则"类型[序号]"。</summary>
    static Dictionary<DependencyObject, string> Labels(FrameworkElement root)
    {
        var labels = new Dictionary<DependencyObject, string>();
        var counts = new Dictionary<string, int>();
        Walk(root);
        return labels;

        void Walk(DependencyObject node)
        {
            if (node is FrameworkElement { Name: { Length: > 0 } name }) labels[node] = name;
            else
            {
                var type = node.GetType().Name;
                counts[type] = counts.GetValueOrDefault(type) + 1;
                labels[node] = $"{type}[{counts[type]}]";
            }
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i));
        }
    }

    /// <summary>主题里的画刷 → 资源键：属性值是同一个画刷对象时，报出它来自哪个 token。</summary>
    static Dictionary<object, string> BrushKeys()
    {
        var keys = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        Collect(Application.Current.Resources);
        return keys;

        void Collect(ResourceDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                if (entry is { Key: string key, Value: Brush brush }) keys.TryAdd(brush, key);
            foreach (var merged in dictionary.MergedDictionaries) Collect(merged);
        }
    }

    static string Format(object? value, Dictionary<object, string> brushKeys) => value switch
    {
        SolidColorBrush brush => brushKeys.TryGetValue(brush, out var key) ? $"{key}（{brush.Color}）" : brush.Color.ToString(),
        IconData data => data.Name,
        _ => Report.Format(value),
    };

    static string Source(ValueSource source)
    {
        var origin = source.BaseValueSource switch
        {
            BaseValueSource.Local => "本地值",
            BaseValueSource.Style => "样式",
            BaseValueSource.ImplicitStyleReference => "隐式样式",
            BaseValueSource.StyleTrigger => "样式触发器",
            BaseValueSource.DefaultStyle => "主题样式",
            BaseValueSource.DefaultStyleTrigger => "主题样式触发器",
            BaseValueSource.ParentTemplate => "模板",
            BaseValueSource.ParentTemplateTrigger => "模板触发器",
            BaseValueSource.TemplateTrigger => "模板触发器",
            BaseValueSource.Inherited => "继承",
            _ => "默认值",
        };
        if (source.IsExpression) origin += "（资源引用或绑定）";
        if (source.IsAnimated) origin += "（动画）";
        if (source.IsCoerced) origin += "（强制）";
        return origin;
    }
}
