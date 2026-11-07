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
    static int Main(string[] args) => InspectProgram.Run(args, options =>
    {
        // 同应用里的 Application：await 之后回到界面线程（示例里有异步取数据的）
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        return options.Command switch
        {
            "list" => Inspector.List(options),
            "bench" => BenchRunner.Run(options),
            _ => Inspector.Run(options),
        };
    });
}

/// <summary>
/// 把示例放进屏幕外的窗口（背景、文字颜色、字号、字体取主题，同一个区域的 Provider），再按命令输出。
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
        var (window, root) = Show(content, example.Framed ? options with { Padding = 0, MinHeight = 0 } : options);
        try
        {
            var labels = Labels(content);
            return options.Command switch
            {
                "snapshot" => Snapshot(root, options),
                "tree" => Tree(content, labels).Render(options.Json),
                "props" => Props(content, labels).Render(options.Json),
                "styles" => Styles(content).Render(options.Json),
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
        var area = new Border { VerticalAlignment = VerticalAlignment.Top, Child = content };
        var root = new Border { Padding = new Thickness(options.Padding), MinHeight = options.MinHeight, Child = area };
        root.SetResourceReference(Border.BackgroundProperty, "Rune.Brush.Background");
        root.SetResourceReference(TextElement.ForegroundProperty, "Rune.Brush.Text");
        root.SetResourceReference(TextElement.FontSizeProperty, "Rune.FontSize.Md");
        root.SetResourceReference(TextElement.FontFamilyProperty, "Rune.Font.Sans");
        var window = new Window
        {
            Content = root, Width = options.Width, SizeToContent = SizeToContent.Height, Left = -20000, Top = -20000,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
        };
        window.Show();
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
        area.Margin = new Thickness(0, CenterOffset(root.ActualHeight - 2 * options.Padding - area.ActualHeight, options.Scale), 0, 0);
        root.UpdateLayout();
        // 高度带小数时补到整像素（同 ts-shots）：不然最后一行只盖了一半底色
        root.Height = Math.Ceiling(root.ActualHeight * options.Scale) / options.Scale;
        root.UpdateLayout();
        return (window, root);
    }

    /// <summary>
    /// 文档站的示例区至少 MinHeight 高、内容竖直居中（CodePreview 的 justify-content: center）。居中的偏移按设备像素取整：
    /// 浏览器的布局是小数，但边框贴着整像素画（偏移 35.5 时卡片的边画在 36）。
    /// </summary>
    static double CenterOffset(double free, double scale) => free > 0 ? Math.Floor(free / 2 * scale + 0.5) / scale : 0;

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

    /// <summary>
    /// 部件的外观取值（同 tools/ts-styles 的格式）：组件库的组件是 <c>scope/root</c>；它在 <c>*Parts</c> 类里声明的部件
    /// （模板里的，或代码里组合的）是 <c>scope/part</c>，部件本身是组件时只记部件。看不见的元素不记。
    /// </summary>
    static StyleReport Styles(FrameworkElement root)
    {
        var report = new StyleReport();
        Walk(root);
        return report;

        void Walk(DependencyObject node)
        {
            if (node is FrameworkElement { IsVisible: false }) return;
            if (node is FrameworkElement element)
            {
                // 组件在代码里组合的子组件（Badge 里的文字）不单独记：TS 里那是组件自己的文字节点
                var composed = element is TextBlock && VisualTreeHelper.GetParent(element) is FrameworkElement parent && IsComponent(parent) && parent is not TextBlock;
                var name = element.Name is { Length: > 0 } part && Owner(element, part) is { } owner && StyleReport.IsScoped(owner.GetType().Name)
                    ? StyleReport.Name(owner.GetType().Name, part)
                    : IsComponent(element) && !composed && StyleReport.IsScoped(element.GetType().Name) ? StyleReport.Name(element.GetType().Name, null) : null;
                if (name is not null) report.Add(name, element.GetType().Name, Values(element));
            }
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i));
        }

        // 组件内部组合用的面板（SwitchStack）不公开，不算组件：部件的归属越过它找到公开的组件
        static bool IsComponent(FrameworkElement element) => element.GetType() is { IsVisible: true } type && type.Namespace == typeof(RuneTheme).Namespace;

        // 部件的主人：声明了这个部件名的模板主人或组件祖先，越过组件内部组合用的组件（UserButton 内容里的 Flex）
        static FrameworkElement? Owner(FrameworkElement element, string part)
        {
            if (element.TemplatedParent is FrameworkElement parent) return DeclaredParts(parent.GetType()).Contains(part) ? parent : null;
            for (var node = VisualTreeHelper.GetParent(element); node is not null; node = VisualTreeHelper.GetParent(node))
                if (node is FrameworkElement ancestor && IsComponent(ancestor) && DeclaredParts(ancestor.GetType()).Contains(part)) return ancestor;
            return null;
        }
    }

    /// <summary>
    /// 组件声明的部件名：它与基类各自的 <c>&lt;类型&gt;Parts</c> 类里的部件。WPF 的部件是样式键（<c>ButtonParts.Icon</c>，模板里的元素名是
    /// <c>PART_Icon</c>）；只有名字没有样式键的部件（PART_Ripples）是字符串常量。
    /// </summary>
    static HashSet<string> DeclaredParts(Type type)
    {
        var names = new HashSet<string>();
        for (var t = type; t is not null; t = t.BaseType)
            if (typeof(RuneTheme).Assembly.GetType($"{t.Namespace}.{t.Name}Parts") is { } parts)
                foreach (var field in parts.GetFields(BindingFlags.Public | BindingFlags.Static))
                    if (field.IsLiteral) names.Add((string)field.GetRawConstantValue()!);
                    else if (field.GetValue(null) is ComponentResourceKey) names.Add($"PART_{field.Name}");
        return names;
    }

    static IEnumerable<(string, object?)> Values(FrameworkElement element)
    {
        object? Get(string name)
        {
            for (var type = element.GetType(); type is not null; type = type.BaseType)
                if (type.GetField($"{name}Property", BindingFlags.Public | BindingFlags.Static) is { } field && field.GetValue(null) is DependencyProperty property)
                    return element.GetValue(property);
            return null;
        }
        string? Brush(object? value) => value is SolidColorBrush brush
            ? StyleReport.Color((byte)Math.Round(brush.Color.A * brush.Opacity), brush.Color.R, brush.Color.G, brush.Color.B)
            : null;

        yield return ("Width", element.ActualWidth);
        yield return ("Height", element.ActualHeight);
        yield return ("MinWidth", element.MinWidth);
        yield return ("MinHeight", element.MinHeight);
        if (Get("Padding") is Thickness padding)
        {
            yield return ("PaddingTop", padding.Top);
            yield return ("PaddingRight", padding.Right);
            yield return ("PaddingBottom", padding.Bottom);
            yield return ("PaddingLeft", padding.Left);
        }
        if (Get("BorderThickness") is Thickness border)
        {
            yield return ("BorderTop", border.Top);
            yield return ("BorderRight", border.Right);
            yield return ("BorderBottom", border.Bottom);
            yield return ("BorderLeft", border.Left);
            yield return ("BorderColor", Brush(Get("BorderBrush")) ?? "#00000000");
        }
        if (Get("CornerRadius") is CornerRadius radius)
        {
            yield return ("RadiusTopLeft", radius.TopLeft);
            yield return ("RadiusTopRight", radius.TopRight);
            yield return ("RadiusBottomRight", radius.BottomRight);
            yield return ("RadiusBottomLeft", radius.BottomLeft);
        }
        yield return ("Background", Brush(Get("Background")) ?? "#00000000");
        yield return ("Foreground", Brush(element.GetValue(TextElement.ForegroundProperty)) ?? Brush(Get("Foreground")));
        yield return ("FontSize", (double)element.GetValue(TextElement.FontSizeProperty));
        yield return ("FontWeight", ((FontWeight)element.GetValue(TextElement.FontWeightProperty)).ToOpenTypeWeight());
        if ((double)element.GetValue(TextBlock.LineHeightProperty) is var lineHeight && !double.IsNaN(lineHeight)) yield return ("LineHeight", lineHeight);
        yield return ("Opacity", element.Opacity);
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
