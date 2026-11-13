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
        using var form = Show(example.Create(), example.Framed ? options with { Padding = 0, MinHeight = 0 } : options);
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
        form.Padding = new Padding((int)(options.Padding * scale));
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
        // 文档站的示例区：至少 MinHeight 高，内容竖直居中（CodePreview 的 justify-content: center；多出的奇数像素给下面，同浏览器把 .5 往下取整到像素）
        var extra = (int)(options.MinHeight * scale) - form.ClientSize.Height;
        if (extra > 0)
        {
            form.Padding = new Padding(form.Padding.Left, form.Padding.Top + extra / 2, form.Padding.Right, form.Padding.Bottom + extra - extra / 2);
            form.ClientSize = new Size(form.ClientSize.Width, (int)(options.MinHeight * scale));
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
        using (var graphics = Graphics.FromImage(bitmap))
        using (var clip = new Region(new Rectangle(Point.Empty, form.ClientSize)))
            Render(form, graphics, Point.Empty, clip);
        bitmap.Save(path, ImageFormat.Png);
        return $"{path}（{bitmap.Width}×{bitmap.Height}，DPI {form.DeviceDpi}）{Environment.NewLine}";
    }

    /// <summary>
    /// 画一个控件，再从下往上把子控件逐个盖上去。DrawToBitmap 让 Windows 画子控件，
    /// 它从最上面的画起，叠在上面的控件（角标）反而被盖住；这里每层都用正确的 z 序重画一遍。
    /// 裁剪同窗口系统：自己的 Region 也裁到所有子控件（卡片的圆角裁掉贴边的横幅）。
    /// PrintWindow 取 DWM 合成的画面，屏幕外的窗体常取到没画完的黑块，所以不用它。
    /// </summary>
    static void Render(Control control, Graphics graphics, Point origin, Region clip)
    {
        // 宽或高为 0 的控件（还没排版的子控件）跳过
        if (control.Width <= 0 || control.Height <= 0) return;
        using var shape = control.Region?.Clone() ?? new Region(new Rectangle(Point.Empty, control.Size));
        shape.Translate(origin.X, origin.Y);
        shape.Intersect(clip);
        if (shape.IsEmpty(graphics)) return;
        using (var image = new Bitmap(control.Width, control.Height))
        {
            PaintSelf(control, image);
            graphics.Clip = shape;
            graphics.DrawImageUnscaled(image, origin);
            graphics.ResetClip();
        }
        // 客户区相对控件左上角的偏移（边框、标题栏）
        var window = control.Parent is { } parent ? parent.PointToScreen(control.Location) : control.Location;
        var client = control.Parent is null ? Point.Empty : control.PointToScreen(Point.Empty) - (Size)window;
        using var area = shape.Clone();
        area.Intersect(new Rectangle(origin + (Size)client, control.ClientSize));
        foreach (var child in control.Controls.Cast<Control>().Where(child => child.Visible).Reverse())
            Render(child, graphics, origin + (Size)client + (Size)child.Location, area);
    }

    /// <summary>
    /// 只画控件自己（WM_PRINT 不带 PRF_CHILDREN）：DrawToBitmap 会把子控件一起画进来、不按它们的 Region 裁，
    /// 卡片圆角裁掉的部分又从父控件的图里露出来。子控件由 Render 逐层盖上去。
    /// </summary>
    static void PaintSelf(Control control, Bitmap image)
    {
        const int print = 0x0317, nonClient = 0x2, clientArea = 0x4, eraseBackground = 0x8;
        using var graphics = Graphics.FromImage(image);
        var dc = graphics.GetHdc();
        try { SendMessage(control.Handle, print, dc, nonClient | clientArea | eraseBackground); }
        finally { graphics.ReleaseHdc(dc); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);

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
