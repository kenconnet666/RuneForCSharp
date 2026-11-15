using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// JSON 查看器（vue-tsx-pro 的 JsonViewer）：接口返回、日志、配置、导入的原始数据，展开收起的键值树。建在 <see cref="Tree"/> 上：虚拟滚动（上万个节点也流畅）、
/// 树的键盘操作（↑↓ 移动、→ 展开、← 收起、按字母跳）与搜索过滤（留下匹配的项与祖先，匹配的加粗）。
/// <code>
/// &lt;rune:JsonViewer Value="{Binding Response}" AutomationProperties.Name="接口返回" /&gt;
/// var viewer = new JsonViewer { Value = JsonDocument.Parse(text).RootElement, ExpandDepth = 2, TreeMaxHeight = 32 * 16 };
/// </code>
/// <list type="bullet">
/// <item>值：System.Text.Json 的 JsonElement、JsonNode，或任意 .NET 值（字典、集合、记录、普通对象），按类型着色（代码高亮的颜色），规则见 <see cref="JsonViewerNodes"/>。</item>
/// <item>选中一项（点击、Enter）后，下方的路径栏显示它的路径（JavaScript 写法：$.items[0]["unit-price"]），可以复制路径与值（容器复制成缩进两格的 JSON）。</item>
/// <item>子节点按需生成：展开到哪一层才建哪一层；“全部展开”会建全部。</item>
/// </list>
/// 读屏：树的名称是 JsonViewer 的 AutomationProperties.Name。部件名在 <see cref="JsonViewerParts"/>。
/// </summary>
public class JsonViewer : Control
{
    /// <summary>要看的数据。</summary>
    public static readonly StyledProperty<object?> ValueProperty = AvaloniaProperty.Register<JsonViewer, object?>(nameof(Value));

    /// <summary>一开始展开几层，默认 1（第一层的对象、数组是展开的）；0 全部收起。</summary>
    public static readonly StyledProperty<int> ExpandDepthProperty = AvaloniaProperty.Register<JsonViewer, int>(nameof(ExpandDepth), 1);

    /// <summary>树的最大高度，超出后滚动；不写时 24em。</summary>
    public static readonly StyledProperty<double> TreeMaxHeightProperty = AvaloniaProperty.Register<JsonViewer, double>(nameof(TreeMaxHeight), double.NaN);

    /// <summary>顶部的搜索框与“全部展开 / 收起”，默认显示。</summary>
    public static readonly StyledProperty<bool> ShowToolbarProperty = AvaloniaProperty.Register<JsonViewer, bool>(nameof(ShowToolbar), true);

    /// <summary>字号：树与路径栏是它的 sm。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<JsonViewer>();

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
    readonly Flex toolbar = new() { Name = JsonViewerParts.Toolbar, Align = FlexAlign.Center, Gap = Space.Xs, Wrap = true };
    readonly SearchField search = new() { Name = JsonViewerParts.Search, Size = TextSize.Sm, Delay = TimeSpan.Zero };
    readonly Button expandAll = new() { Name = JsonViewerParts.ExpandAll, Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Icon = Lucide.ChevronsUpDown };
    readonly Button collapseAll = new() { Name = JsonViewerParts.CollapseAll, Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Icon = Lucide.ChevronsDownUp };
    readonly Tree tree = new() { Name = JsonViewerParts.Tree, SelectionMode = SelectionMode.Single, Indent = 1 };
    readonly Flex pathBar = new() { Name = JsonViewerParts.PathBar, Align = FlexAlign.Center, Gap = Space.Xs };
    readonly TextBlock hint = new();
    readonly TextBlock path = new() { Name = JsonViewerParts.Path, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly CopyButton copyPath = new() { Size = TextSize.Sm, Tone = StatusTone.Neutral };
    readonly CopyButton copyValue = new() { Size = TextSize.Sm, Tone = StatusTone.Neutral };
    // 见过的节点按路径记下：路径栏按选中的路径找回节点（随 Value 换掉）
    Dictionary<string, JsonViewerNode> byPath = [];
    IReadOnlyList<JsonViewerNode> roots = [];

    /// <summary>新建：搜索框与两个按钮、树、路径栏。</summary>
    public JsonViewer()
    {
        Flex.SetGrow(search, true);
        search.MinWidth = 0;
        toolbar.Children.AddRange([search, expandAll, collapseAll]);
        tree.GetKey = node => ((JsonViewerNode)node).Path;
        tree.GetLabel = node => $"{((JsonViewerNode)node).Name}: {JsonViewerNodes.Display((JsonViewerNode)node)}";
        tree.GetChildren = node => Remember(JsonViewerNodes.ChildrenOf((JsonViewerNode)node));
        tree.ItemTemplate = new FuncDataTemplate<JsonViewerNode>((node, _) => Row(node));
        tree.Bind(TextElement.FontFamilyProperty, tree.GetResourceObservable(ThemeKeys.Font.Mono));
        // 等宽字体的连字会把 >= 画成 ≥：看原始数据时关掉
        tree.SetValue(TextElement.FontFeaturesProperty, NoLigatures());
        // 路径可能比路径栏长：缩到放得下，按钮不缩
        Flex.SetShrink(path, true);
        path.MinWidth = 0;
        path.Bind(TextBlock.FontFamilyProperty, path.GetResourceObservable(ThemeKeys.Font.Mono));
        path.FontFeatures = NoLigatures();
        path.Bind(TextBlock.ForegroundProperty, path.GetResourceObservable(ThemeKeys.Brush.Text));
        pathBar.Bind(TextElement.ForegroundProperty, pathBar.GetResourceObservable(ThemeKeys.Brush.TextMuted));
        Flex.SetShrink(copyPath, false);
        Flex.SetShrink(copyValue, false);
        pathBar.Children.AddRange([hint, path, copyPath, copyValue]);
        root.Children.AddRange([toolbar, tree, pathBar]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) tree.Filter = search.Text;
        };
        expandAll.Click += (_, _) => tree.Expanded = JsonViewerNodes.ContainersTo(roots, int.MaxValue, byPath);
        collapseAll.Click += (_, _) => tree.Expanded = [];
        tree.PropertyChanged += (_, e) =>
        {
            if (e.Property == Tree.SelectedProperty) SyncPathBar();
        };
        AttachedToLogicalTree += (_, _) => SyncLang();
        Rebuild();
    }

    /// <summary>要看的数据：JsonElement、JsonNode 或任意 .NET 值。</summary>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>一开始展开几层。</summary>
    public int ExpandDepth { get => GetValue(ExpandDepthProperty); set => SetValue(ExpandDepthProperty, value); }

    /// <summary>树的最大高度（不写时 24em）。</summary>
    public double TreeMaxHeight { get => GetValue(TreeMaxHeightProperty); set => SetValue(TreeMaxHeightProperty, value); }

    /// <summary>显示顶部的搜索框与按钮。</summary>
    public bool ShowToolbar { get => GetValue(ShowToolbarProperty); set => SetValue(ShowToolbarProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>里面的树（测试用）。</summary>
    internal Tree TreePart => tree;

    /// <summary>搜索框（测试用）。</summary>
    internal SearchField SearchPart => search;

    /// <summary>两个按钮（测试用）。</summary>
    internal (Button ExpandAll, Button CollapseAll) Buttons => (expandAll, collapseAll);

    /// <summary>路径栏的文字与两个复制按钮（测试用）。</summary>
    internal (TextBlock Path, TextBlock Hint, CopyButton CopyPath, CopyButton CopyValue) PathBarParts => (path, hint, copyPath, copyValue);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == ExpandDepthProperty) Rebuild();
        else if (change.Property == ShowToolbarProperty) toolbar.IsVisible = ShowToolbar;
        else if (change.Property == TreeMaxHeightProperty || change.Property == FontSizeProperty) UpdateMetrics();
        else if (change.Property == AutomationProperties.NameProperty) AutomationProperties.SetName(tree, AutomationProperties.GetName(this));
    }

    IReadOnlyList<JsonViewerNode>? Remember(IReadOnlyList<JsonViewerNode>? nodes)
    {
        foreach (var node in nodes ?? []) byPath[node.Path] = node;
        return nodes;
    }

    /// <summary>换了数据或展开层数：重建第一层，一开始展开到 ExpandDepth 层，清掉选中。</summary>
    void Rebuild()
    {
        byPath = [];
        roots = Remember(JsonViewerNodes.Roots(Value))!;
        tree.ItemsSource = roots;
        tree.Expanded = JsonViewerNodes.ContainersTo(roots, ExpandDepth, byPath);
        tree.Selected = [];
    }

    /// <summary>树与路径栏的字号是 sm（按自己的字号等比），树最高 TreeMaxHeight（默认 24em），路径栏至少 2.25em 高。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        var sm = em * this.Resource<double>(ThemeKeys.FontSize.Sm) / this.Resource<double>(ThemeKeys.FontSize.Md);
        tree.FontSize = sm;
        tree.MaxHeight = double.IsNaN(TreeMaxHeight) ? 24 * em : TreeMaxHeight;
        pathBar.SetValue(TextElement.FontSizeProperty, sm);
        pathBar.MinHeight = 2.25 * sm;
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * sm;
        pathBar.Margin = new Thickness(xs, 0);
    }

    void SyncLang()
    {
        var lang = Provider.GetLang(this);
        search.PlaceholderText = lang.JsonSearch;
        AutomationProperties.SetName(search, lang.JsonSearch);
        expandAll.Content = lang.JsonExpandAll;
        collapseAll.Content = lang.JsonCollapseAll;
        // 两个按钮图标一样：悬停、聚焦时提示各复制什么
        foreach (var (button, name) in new[] { (copyPath, lang.JsonCopyPath), (copyValue, lang.JsonCopyValue) })
        {
            AutomationProperties.SetName(button, name);
            ToolTip.SetTip(button, name);
        }
        hint.Text = lang.JsonPathHint;
        UpdateMetrics();
        SyncPathBar();
    }

    /// <summary>路径栏：选中了一项时是它的路径与两个复制按钮，没有时是提示。</summary>
    void SyncPathBar()
    {
        var node = tree.Selected.Count > 0 ? byPath.GetValueOrDefault(tree.Selected[0]) : null;
        hint.IsVisible = node is null;
        foreach (var part in new Control[] { path, copyPath, copyValue }) part.IsVisible = node is not null;
        if (node is null) return;
        path.Text = node.Path;
        ToolTip.SetTip(path, node.Path);
        copyPath.Value = node.Path;
        copyValue.Value = JsonViewerNodes.CopyText(node);
    }

    /// <summary>一行：键名（序号是注释色）、值（按类型着色，放不下时省略），容器后面是项数。</summary>
    Control Row(JsonViewerNode node)
    {
        var row = new Flex { Align = FlexAlign.Baseline, Gap = Space.Xs, Name = JsonViewerParts.Row };
        var name = Colored(new TextBlock { Text = $"{node.Name}:" }, node.IsIndex ? ThemeKeys.Brush.CodeComment : ThemeKeys.Brush.CodeAttribute);
        var value = Colored(new TextBlock { Text = JsonViewerNodes.Display(node), TextTrimming = TextTrimming.CharacterEllipsis, MinWidth = 0, Name = JsonViewerParts.Value }, BrushOf(node.Kind));
        Flex.SetShrink(name, false);
        Flex.SetShrink(value, true);
        row.Children.AddRange([name, value]);
        if (node.IsContainer)
        {
            var summary = Colored(new TextBlock { Text = Provider.GetLang(this).JsonItems(JsonViewerNodes.SizeOf(node)), Name = JsonViewerParts.Summary }, ThemeKeys.Brush.CodeComment);
            Flex.SetShrink(summary, false);
            row.Children.Add(summary);
        }
        return row;
    }

    static FontFeatureCollection NoLigatures() => [FontFeature.Parse("-calt"), FontFeature.Parse("-liga")];

    static TextBlock Colored(TextBlock text, string brush)
    {
        text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable(brush));
        return text;
    }

    /// <summary>值按类型的颜色（同上游的 KIND_STYLES：代码高亮的 token）。</summary>
    static string BrushOf(JsonViewerKind kind) => kind switch
    {
        JsonViewerKind.String => ThemeKeys.Brush.CodeString,
        JsonViewerKind.Number => ThemeKeys.Brush.CodeNumber,
        JsonViewerKind.Boolean or JsonViewerKind.Null => ThemeKeys.Brush.CodeKeyword,
        JsonViewerKind.Date or JsonViewerKind.Other or JsonViewerKind.Function => ThemeKeys.Brush.CodeType,
        _ => ThemeKeys.Brush.CodeComment,
    };
}

/// <summary>JsonViewer 的部件名（上游的 data-part）。</summary>
public static class JsonViewerParts
{
    /// <summary>搜索框与按钮的一行。</summary>
    public const string Toolbar = "PART_Toolbar";

    /// <summary>搜索框。</summary>
    public const string Search = "PART_Search";

    /// <summary>“全部展开”按钮。</summary>
    public const string ExpandAll = "PART_ExpandAll";

    /// <summary>“全部收起”按钮。</summary>
    public const string CollapseAll = "PART_CollapseAll";

    /// <summary>树。</summary>
    public const string Tree = "PART_Tree";

    /// <summary>一行（键名、值、项数）。</summary>
    public const string Row = "PART_Row";

    /// <summary>值。</summary>
    public const string Value = "PART_Value";

    /// <summary>容器收起时的项数。</summary>
    public const string Summary = "PART_Summary";

    /// <summary>路径栏。</summary>
    public const string PathBar = "PART_PathBar";

    /// <summary>路径栏里的路径。</summary>
    public const string Path = "PART_Path";
}
