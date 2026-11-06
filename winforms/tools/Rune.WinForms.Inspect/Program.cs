using System.ComponentModel;
using System.Drawing.Imaging;
using System.Reflection;
using Rune.Inspect;
using Rune.WinForms.Examples;

namespace Rune.WinForms.Inspect;

static class Program
{
    [STAThread]
    static int Main(string[] args) => InspectProgram.Run(args, options =>
    {
        if (options.Command == "list") return Inspector.List(options);
        ApplicationConfiguration.Initialize();
        return options.Command == "bench" ? BenchRunner.Run(options) : Inspector.Run(options);
    });
}

/// <summary>
/// 把示例放进屏幕外的窗体（背景、文字颜色、字体取当前主题，字号取 md），再按命令输出。
/// 控件与元素在 tree、props、uia 里用同一个称呼：有 Name 用 Name，否则"类型[序号]"（按控件树的先后数）。
/// WinForms 按显示器的 DPI 渲染，不支持 --scale。
/// </summary>
static class Inspector
{
    /// <summary>每个控件都列的外观属性（只列本地设过的）；组件库自己的属性不论是否设置都列。</summary>
    static readonly string[] CommonProperties = ["BackColor", "ForeColor", "Font", "Padding", "Margin", "RightToLeft", "AccessibleName"];

    public static string List(InspectOptions options)
    {
        var table = new ReportTable("示例", "标题", "说明");
        foreach (var example in ExampleCatalog.All) table.Add(example.Key, example.Title, example.Description);
        return table.Render(options.Json);
    }

    public static string Run(InspectOptions options)
    {
        var example = ExampleCatalog.Find(options.Key!) ?? throw new UsageException($"没有示例 {options.Key}；inspect list 列出全部");
        if (options.Scale != 1) throw new UsageException("WinForms 按显示器的 DPI 渲染，不支持 --scale");
        RuneTheme.Current.Value = BuiltinThemes.All.FirstOrDefault(theme => theme.Name == options.Theme)
            ?? throw new UsageException($"没有主题 {options.Theme}；可选：{string.Join("、", BuiltinThemes.All.Select(theme => theme.Name))}");
        using var form = Show(example.Create(), options);
        var content = form.Controls[0];
        var labels = Labels(content);
        return options.Command switch
        {
            "snapshot" => Snapshot(form, options),
            "tree" => Tree(content, labels).Render(options.Json),
            "props" => Props(labels).Render(options.Json),
            _ => Accessibility(content, labels).Render(options.Json),
        };
    }

    static Form Show(Control content, InspectOptions options)
    {
        var theme = RuneTheme.Current.Value;
        var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000), FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
            BackColor = theme.Colors.Background, ForeColor = theme.Colors.Text,
            Font = new Font(Fonts.Installed(theme.Fonts.Sans), Tokens.FontSize.Md * 72 / 96),
        };
        form.Show();
        var scale = form.DeviceDpi / 96f;
        form.Padding = new Padding((int)(24 * scale));
        form.ClientSize = new Size((int)(options.Width * scale), form.ClientSize.Height);
        content.Dock = DockStyle.Top;
        form.Controls.Add(content);
        // 自动大小的面板按上一次布局的结果量自己，换行的面板要几轮才收敛到最终高度
        for (var height = -1; height != content.Bottom;)
        {
            height = content.Bottom;
            form.ClientSize = new Size(form.ClientSize.Width, height + form.Padding.Bottom);
            form.PerformLayout();
            Application.DoEvents();
        }
        return form;
    }

    static string Snapshot(Form form, InspectOptions options)
    {
        var path = Report.SnapshotPath("winforms", options);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
        bitmap.Save(path, ImageFormat.Png);
        return $"{path}（{bitmap.Width}×{bitmap.Height}，DPI {form.DeviceDpi}）{Environment.NewLine}";
    }

    static ReportTable Tree(Control root, Dictionary<object, string> labels)
    {
        var table = new ReportTable("元素", "类型", "边界", "说明");
        Walk(root, 0);
        return table;

        void Walk(Control control, int depth)
        {
            var origin = root.PointToClient(control.Parent?.PointToScreen(control.Location) ?? control.Location);
            if (control == root) origin = Point.Empty;
            table.Add($"{Indent(depth)}{labels[control]}", control.GetType().Name, $"{origin.X},{origin.Y} {control.Width}×{control.Height}", control.AutoSize ? "AutoSize" : null);
            if (Element(control) is { } element)
                table.Add($"{Indent(depth + 1)}{labels[element]}", $"{element.GetType().Name}（元素）", $"{origin.X + element.Bounds.X},{origin.Y + element.Bounds.Y} {element.Bounds.Width}×{element.Bounds.Height}", null);
            foreach (Control child in control.Controls) Walk(child, depth + 1);
        }
    }

    static ReportTable Props(Dictionary<object, string> labels)
    {
        var colorNames = ThemeColorNames(RuneTheme.Current.Value);
        var table = new ReportTable("元素", "属性", "值", "来源");
        foreach (var control in labels.Keys.OfType<Control>())
        {
            var properties = TypeDescriptor.GetProperties(control);
            var own = properties.Cast<PropertyDescriptor>().Where(property => property.ComponentType.Namespace == typeof(RuneTheme).Namespace && property.IsBrowsable).ToList();
            foreach (var property in own.Concat(CommonProperties.Select(name => properties[name]).OfType<PropertyDescriptor>()).Distinct())
            {
                var local = property.ShouldSerializeValue(control);
                if (!own.Contains(property) && !local) continue;
                table.Add(labels[control], property.Name, Format(property.GetValue(control), colorNames), local ? "本地值" : "默认值");
            }
        }
        return table;
    }

    /// <summary>无障碍树（MSAA，与窗口的层级一致）：角色、名称、状态。</summary>
    static ReportTable Accessibility(Control root, Dictionary<object, string> labels)
    {
        var table = new ReportTable("节点", "角色", "名称", "元素", "可聚焦", "状态");
        Walk(root, 0);
        return table;

        void Walk(Control control, int depth)
        {
            var node = control.AccessibilityObject;
            table.Add($"{Indent(depth)}{node.Role}", node.Role, node.Name, labels[control], control.CanSelect, node.State == AccessibleStates.None ? null : node.State);
            foreach (Control child in control.Controls) Walk(child, depth + 1);
        }
    }

    static string Indent(int depth) => string.Concat(Enumerable.Repeat("· ", depth));

    /// <summary>单元素宿主承载的元素（ElementControl&lt;T&gt;.Element）。</summary>
    static Element? Element(Control control) =>
        control.GetType().GetProperty("Element", BindingFlags.Public | BindingFlags.Instance)?.GetValue(control) as Element;

    /// <summary>控件与元素的称呼：有 Name 用 Name，否则"类型[序号]"。</summary>
    static Dictionary<object, string> Labels(Control root)
    {
        var labels = new Dictionary<object, string>();
        var counts = new Dictionary<string, int>();
        Walk(root);
        return labels;

        void Walk(Control control)
        {
            labels[control] = control.Name is { Length: > 0 } name ? name : Next(control);
            if (Element(control) is { } element) labels[element] = Next(element);
            foreach (Control child in control.Controls) Walk(child);
        }

        string Next(object item)
        {
            var type = item.GetType().Name;
            counts[type] = counts.GetValueOrDefault(type) + 1;
            return $"{type}[{counts[type]}]";
        }
    }

    /// <summary>当前主题的颜色 → token 名：颜色值等于某个主题色时报出它的名字（同色的取第一个）。</summary>
    static Dictionary<int, string> ThemeColorNames(ThemeTokens theme)
    {
        var names = new Dictionary<int, string>();
        foreach (var property in typeof(ThemeColors).GetProperties())
            if (property.GetValue(theme.Colors) is Color color) names.TryAdd(color.ToArgb(), $"Colors.{property.Name}");
        return names;
    }

    static string Format(object? value, Dictionary<int, string> colorNames) => value switch
    {
        Color color when colorNames.TryGetValue(color.ToArgb(), out var name) => $"{name}（#{color.ToArgb():X8}）",
        Color color => color.IsNamedColor ? color.Name : $"#{color.ToArgb():X8}",
        Font font => $"{font.FontFamily.Name} {Report.Format(font.SizeInPoints)}pt",
        IconData data => data.Name,
        _ => Report.Format(value),
    };
}
