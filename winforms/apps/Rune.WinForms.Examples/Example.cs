namespace Rune.WinForms.Examples;

/// <summary>一个示例：Gallery 里的一节，inspect 按 <see cref="Key"/> 找到它。</summary>
/// <param name="Component">组件名，同 vue-tsx-pro。</param>
/// <param name="Id">示例名，同 vue-tsx-pro 文档站上这一节的 id（basic、sizes……），ts-styles 与 inspect 按它配对。</param>
/// <param name="Title">标题。</param>
/// <param name="Description">说明。</param>
/// <param name="Create">新建示例的内容（停靠在宿主顶部，宽度随宿主）。</param>
public sealed record Example(string Component, string Id, string Title, string Description, Func<Control> Create)
{
    /// <summary>"组件/示例名"，如 <c>Icon/basic</c>。</summary>
    public string Key => $"{Component}/{Id}";
}

/// <summary>全部示例：组件按 vue-tsx-pro 文档站的分类，示例按页面上的先后。</summary>
public static class ExampleCatalog
{
    /// <summary>全部示例。</summary>
    public static IReadOnlyList<Example> All { get; } = [.. IconExamples.All];

    /// <summary>按"组件/示例名"找（不分大小写）；没有时为 null。</summary>
    /// <param name="key">如 <c>Icon/basic</c>。</param>
    public static Example? Find(string key) => All.FirstOrDefault(example => string.Equals(example.Key, key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// 示例里的排版：自动大小的 FlowLayoutPanel 与 Label。Flex、Grid、Text 做出来后换掉。
/// 间距、宽度写设备无关像素，建控件时按 DPI 换算（代码建的控件没有设计器的自动缩放）；字号写磅值，系统按 DPI 换算。
/// </summary>
static class Layout
{
    /// <summary>一行，放不下时换行（停靠在宿主顶部，宽度随宿主）。</summary>
    public static FlowLayoutPanel Wrap(int gap, params Control[] children) => Flow(FlowDirection.LeftToRight, true, gap, children);

    /// <summary>一行，不换行。</summary>
    public static FlowLayoutPanel Row(int gap, params Control[] children) => Flow(FlowDirection.LeftToRight, false, gap, children);

    /// <summary>一列。</summary>
    public static FlowLayoutPanel Column(int gap, params Control[] children) => Flow(FlowDirection.TopDown, false, gap, children);

    /// <summary>一格：图标在上，说明在下，都居中；写了宽度时每格一样宽（列宽取最宽的子控件，所以加宽的是说明）。</summary>
    public static FlowLayoutPanel Cell(Control icon, string caption, int width = 0)
    {
        icon.Anchor = AnchorStyles.None;
        var label = Caption(caption);
        label.Anchor = AnchorStyles.None;
        if (width > 0)
        {
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.TopCenter;
            label.Size = new Size(Math.Max(label.LogicalToDeviceUnits(width), label.PreferredWidth), label.PreferredHeight);
        }
        return Column(4, icon, label);
    }

    /// <summary>文字；给了字号（设备无关像素）时按它建字体，否则跟随父控件。</summary>
    public static Label Text(string text, float? size = null) => new()
    {
        Text = text, AutoSize = true, Margin = Padding.Empty,
        Font = size is { } pixels ? new Font(SystemFonts.MessageBoxFont!.FontFamily, pixels * 72 / 96) : null,
    };

    /// <summary>说明文字：次要文字的颜色（取当前主题，换主题时 Gallery 重建示例）、sm 字号。</summary>
    public static Label Caption(string text)
    {
        var label = Text(text, Tokens.FontSize.Sm);
        label.ForeColor = RuneTheme.Current.Peek().Colors.TextMuted;
        return label;
    }

    static FlowLayoutPanel Flow(FlowDirection direction, bool wrap, int gap, Control[] children)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = direction, WrapContents = wrap, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty, Padding = Padding.Empty,
        };
        if (wrap) panel.Dock = DockStyle.Top;
        foreach (var child in children)
        {
            var space = panel.LogicalToDeviceUnits(gap);
            child.Margin = direction == FlowDirection.TopDown ? new Padding(0, 0, 0, space) : new Padding(0, 0, space, wrap ? space : 0);
            panel.Controls.Add(child);
        }
        return panel;
    }
}
