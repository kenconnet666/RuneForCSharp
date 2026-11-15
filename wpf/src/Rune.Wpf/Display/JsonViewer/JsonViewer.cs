using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf;

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
/// 读屏：树的名称是 JsonViewer 的 AutomationProperties.Name。
/// </summary>
public class JsonViewer : CompositeControl
{
    /// <summary>要看的数据。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(object), typeof(JsonViewer), new PropertyMetadata(null, OnRebuild));

    /// <summary>一开始展开几层，默认 1（第一层的对象、数组是展开的）；0 全部收起。</summary>
    public static readonly DependencyProperty ExpandDepthProperty = DependencyProperty.Register(nameof(ExpandDepth), typeof(int), typeof(JsonViewer), new PropertyMetadata(1, OnRebuild));

    /// <summary>树的最大高度，超出后滚动；不写时 24em。</summary>
    public static readonly DependencyProperty TreeMaxHeightProperty = DependencyProperty.Register(nameof(TreeMaxHeight), typeof(double), typeof(JsonViewer), new PropertyMetadata(double.NaN, OnMetrics));

    /// <summary>顶部的搜索框与“全部展开 / 收起”，默认显示。</summary>
    public static readonly DependencyProperty ShowToolbarProperty = DependencyProperty.Register(nameof(ShowToolbar), typeof(bool), typeof(JsonViewer),
        new PropertyMetadata(true, (sender, _) => ((JsonViewer)sender).toolbar.Visibility = ((JsonViewer)sender).ShowToolbar ? Visibility.Visible : Visibility.Collapsed));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
    readonly Flex toolbar = new() { Align = FlexAlign.Center, Gap = Space.Xs, Wrap = true };
    readonly SearchField search = new() { Size = TextSize.Sm, Delay = TimeSpan.Zero };
    readonly Button expandAll = new() { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Icon = Lucide.ChevronsUpDown };
    readonly Button collapseAll = new() { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Icon = Lucide.ChevronsDownUp };
    readonly Tree tree = new() { SelectionMode = SelectionMode.Single, Indent = 1 };
    readonly Flex pathBar = new() { Align = FlexAlign.Center, Gap = Space.Xs };
    readonly TextBlock hint = new();
    readonly TextBlock path = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    readonly CopyButton copyPath = new() { Size = TextSize.Sm, Tone = StatusTone.Neutral };
    readonly CopyButton copyValue = new() { Size = TextSize.Sm, Tone = StatusTone.Neutral };
    // 见过的节点按路径记下：路径栏按选中的路径找回节点（随 Value 换掉）
    Dictionary<string, JsonViewerNode> byPath = [];
    IReadOnlyList<JsonViewerNode> roots = [];

    static JsonViewer()
    {
        FontSizeProperty.OverrideMetadata(typeof(JsonViewer), new FrameworkPropertyMetadata(OnMetrics));
        AutomationProperties.NameProperty.OverrideMetadata(typeof(JsonViewer),
            new FrameworkPropertyMetadata((sender, e) => AutomationProperties.SetName(((JsonViewer)sender).tree, (string)e.NewValue)));
    }

    /// <summary>新建：搜索框与两个按钮、树、路径栏。</summary>
    public JsonViewer()
    {
        Flex.SetGrow(search, true);
        search.MinWidth = 0;
        toolbar.Children.Add(search);
        toolbar.Children.Add(expandAll);
        toolbar.Children.Add(collapseAll);
        tree.GetKey = node => ((JsonViewerNode)node).Path;
        tree.GetLabel = node => $"{((JsonViewerNode)node).Name}: {JsonViewerNodes.Display((JsonViewerNode)node)}";
        tree.GetChildren = node => Remember(JsonViewerNodes.ChildrenOf((JsonViewerNode)node));
        tree.ItemTemplate = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(JsonViewerRow)) };
        tree.SetResourceReference(TextElement.FontFamilyProperty, ThemeKeys.Font.Mono);
        NoLigatures(tree);
        // 路径可能比路径栏长：缩到放得下，按钮不缩
        Flex.SetShrink(path, true);
        path.MinWidth = 0;
        path.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.Font.Mono);
        path.SetResourceReference(TextBlock.ForegroundProperty, ThemeKeys.Brush.Text);
        NoLigatures(path);
        pathBar.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.Brush.TextMuted);
        Flex.SetShrink(copyPath, false);
        Flex.SetShrink(copyValue, false);
        foreach (var part in new UIElement[] { hint, path, copyPath, copyValue }) pathBar.Children.Add(part);
        foreach (var part in new UIElement[] { toolbar, tree, pathBar }) root.Children.Add(part);
        Root = root;
        search.TextChanged += (_, _) => tree.Filter = search.Text;
        expandAll.Click += (_, _) => tree.Expanded = JsonViewerNodes.ContainersTo(roots, int.MaxValue, byPath);
        collapseAll.Click += (_, _) => tree.Expanded = [];
        DependencyPropertyDescriptor.FromProperty(Tree.SelectedProperty, typeof(Tree)).AddValueChanged(tree, (_, _) => SyncPathBar());
        Loaded += (_, _) => SyncLang();
        Rebuild();
        SyncLang();
    }

    /// <summary>要看的数据：JsonElement、JsonNode 或任意 .NET 值。</summary>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>一开始展开几层。</summary>
    public int ExpandDepth { get => (int)GetValue(ExpandDepthProperty); set => SetValue(ExpandDepthProperty, value); }

    /// <summary>树的最大高度（不写时 24em）。</summary>
    public double TreeMaxHeight { get => (double)GetValue(TreeMaxHeightProperty); set => SetValue(TreeMaxHeightProperty, value); }

    /// <summary>显示顶部的搜索框与按钮。</summary>
    public bool ShowToolbar { get => (bool)GetValue(ShowToolbarProperty); set => SetValue(ShowToolbarProperty, value); }

    /// <summary>里面的树（测试用）。</summary>
    internal Tree TreePart => tree;

    /// <summary>搜索框（测试用）。</summary>
    internal SearchField SearchPart => search;

    /// <summary>两个按钮（测试用）。</summary>
    internal (Button ExpandAll, Button CollapseAll) Buttons => (expandAll, collapseAll);

    /// <summary>路径栏的文字与两个复制按钮（测试用）。</summary>
    internal (TextBlock Path, TextBlock Hint, CopyButton CopyPath, CopyButton CopyValue) PathBarParts => (path, hint, copyPath, copyValue);

    static void OnRebuild(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((JsonViewer)sender).Rebuild();

    static void OnMetrics(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((JsonViewer)sender).UpdateMetrics();

    /// <summary>等宽字体的连字会把 >= 画成 ≥：看原始数据时关掉。</summary>
    static void NoLigatures(DependencyObject element)
    {
        Typography.SetStandardLigatures(element, false);
        Typography.SetContextualAlternates(element, false);
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
        TextElement.SetFontSize(tree, sm);
        tree.MaxHeight = double.IsNaN(TreeMaxHeight) ? 24 * em : TreeMaxHeight;
        TextElement.SetFontSize(pathBar, sm);
        pathBar.MinHeight = 2.25 * sm;
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * sm;
        pathBar.Margin = new Thickness(xs, 0, xs, 0);
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
            button.ToolTip = name;
        }
        hint.Text = lang.JsonPathHint;
        UpdateMetrics();
        SyncPathBar();
    }

    /// <summary>路径栏：选中了一项时是它的路径与两个复制按钮，没有时是提示。</summary>
    void SyncPathBar()
    {
        var node = tree.Selected.Count > 0 ? byPath.GetValueOrDefault(tree.Selected[0]) : null;
        hint.Visibility = node is null ? Visibility.Visible : Visibility.Collapsed;
        foreach (var part in new UIElement[] { path, copyPath, copyValue }) part.Visibility = node is null ? Visibility.Collapsed : Visibility.Visible;
        if (node is null) return;
        path.Text = node.Path;
        path.ToolTip = node.Path;
        copyPath.Value = node.Path;
        copyValue.Value = JsonViewerNodes.CopyText(node);
    }
}

/// <summary>JsonViewer 的一行（树的 ItemTemplate，数据上下文是节点）：键名（序号是注释色）、值（按类型着色，放不下时省略），容器后面是项数。</summary>
sealed class JsonViewerRow : Flex
{
    public JsonViewerRow()
    {
        Align = FlexAlign.Baseline;
        Gap = Space.Xs;
        DataContextChanged += (_, _) => Build();
    }

    void Build()
    {
        Children.Clear();
        if (DataContext is not JsonViewerNode node) return;
        var name = Colored(new TextBlock { Text = $"{node.Name}:" }, node.IsIndex ? ThemeKeys.Brush.CodeComment : ThemeKeys.Brush.CodeAttribute);
        var value = Colored(new TextBlock { Text = JsonViewerNodes.Display(node), TextTrimming = TextTrimming.CharacterEllipsis, MinWidth = 0 }, BrushOf(node.Kind));
        SetShrink(name, false);
        SetShrink(value, true);
        Children.Add(name);
        Children.Add(value);
        if (!node.IsContainer) return;
        var summary = Colored(new TextBlock { Text = Provider.GetLang(this).JsonItems(JsonViewerNodes.SizeOf(node)) }, ThemeKeys.Brush.CodeComment);
        SetShrink(summary, false);
        Children.Add(summary);
    }

    static TextBlock Colored(TextBlock text, string brush)
    {
        text.SetResourceReference(TextBlock.ForegroundProperty, brush);
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
