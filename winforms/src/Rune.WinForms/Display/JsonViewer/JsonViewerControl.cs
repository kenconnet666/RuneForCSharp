using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// JSON 查看器（vue-tsx-pro 的 JsonViewer）：接口返回、日志、配置、导入的原始数据，展开收起的键值树。建在 <see cref="TreeControl"/> 上：只画看得见的行（上万个节点也流畅）、
/// 树的键盘操作（↑↓ 移动、→ 展开、← 收起、按字母跳）与搜索过滤（留下匹配的项与祖先，匹配的加粗）。
/// <code>
/// var viewer = new JsonViewerControl { Value = JsonDocument.Parse(text).RootElement, ExpandDepth = 2, AccessibleName = "接口返回", Dock = DockStyle.Top };
/// </code>
/// <list type="bullet">
/// <item>值：System.Text.Json 的 JsonElement、JsonNode，或任意 .NET 值（字典、集合、记录、普通对象），按类型着色（代码高亮的颜色），规则见 <see cref="JsonViewerNodes"/>。</item>
/// <item>选中一项（点击、Enter）后，下方的路径栏显示它的路径（JavaScript 写法：$.items[0]["unit-price"]），可以复制路径与值（容器复制成缩进两格的 JSON）。</item>
/// <item>子节点按需生成：展开到哪一层才建哪一层；“全部展开”会建全部。</item>
/// </list>
/// 树的名称是 JsonViewerControl 的 AccessibleName。
/// </summary>
[DefaultProperty(nameof(Value))]
[Description("JSON 查看器：展开收起的键值树，按类型着色、可搜索、可复制值与路径。")]
public class JsonViewerControl : FlexControl
{
    readonly FlexControl toolbar = new() { Align = FlexAlign.Center, Gap = Space.Xs, Wrap = true };
    readonly SearchFieldControl search = new() { TextSize = TextSize.Sm, Delay = TimeSpan.Zero };
    readonly ButtonControl expandAll = new() { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Icon = Lucide.ChevronsUpDown };
    readonly ButtonControl collapseAll = new() { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Icon = Lucide.ChevronsDownUp };
    readonly TreeControl tree = new() { SelectionMode = SelectionMode.Single, Indent = 1 };
    readonly FlexControl pathBar = new() { Align = FlexAlign.Center, Gap = Space.Xs };
    readonly TextControl hint = new() { TextSize = TextSize.Sm, Tone = TextTone.Muted };
    readonly TextControl path = new() { TextSize = TextSize.Sm, Mono = true, Truncate = true, Visible = false };
    readonly CopyButtonControl copyPath = new() { TextSize = TextSize.Sm, Tone = StatusTone.Neutral, Visible = false };
    readonly CopyButtonControl copyValue = new() { TextSize = TextSize.Sm, Tone = StatusTone.Neutral, Visible = false };
    readonly TooltipProvider tooltips = new();
    // 见过的节点按路径记下：路径栏按选中的路径找回节点（随 Value 换掉）
    Dictionary<string, JsonViewerNode> byPath = [];
    IReadOnlyList<JsonViewerNode> roots = [];

    /// <summary>新建：搜索框与两个按钮、树、路径栏。</summary>
    public JsonViewerControl()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Xs;
        Align = FlexAlign.Stretch;
        toolbar.Controls.AddRange([search, expandAll, collapseAll]);
        toolbar.SetGrow(search, true);
        tree.GetKey = node => ((JsonViewerNode)node).Path;
        tree.GetLabel = node => $"{((JsonViewerNode)node).Name}: {JsonViewerNodes.Display((JsonViewerNode)node)}";
        tree.GetChildren = node => Remember(JsonViewerNodes.ChildrenOf((JsonViewerNode)node));
        tree.PaintContent = PaintRow;
        pathBar.Controls.AddRange([hint, path, copyPath, copyValue]);
        // 路径可能比路径栏长：缩到放得下，按钮不缩
        pathBar.SetShrink(path, true);
        pathBar.SetShrink(copyPath, false);
        pathBar.SetShrink(copyValue, false);
        Controls.AddRange([toolbar, tree, pathBar]);
        search.ValueChanged += (_, _) => tree.Filter = search.Value;
        expandAll.Click += (_, _) => tree.Expanded = JsonViewerNodes.ContainersTo(roots, int.MaxValue, byPath);
        collapseAll.Click += (_, _) => tree.Expanded = [];
        tree.SelectedChanged += (_, _) => SyncPathBar();
        Rebuild();
    }

    /// <summary>要看的数据：JsonElement、JsonNode 或任意 .NET 值。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? Value { get; set { field = value; Rebuild(); } }

    /// <summary>一开始展开几层，默认 1（第一层的对象、数组是展开的）；0 全部收起。</summary>
    [Category("Behavior")]
    [DefaultValue(1)]
    public int ExpandDepth { get; set { field = value; Rebuild(); } } = 1;

    /// <summary>树的最大高度（像素，按所在显示器的 DPI），超出后滚动；0 是 24em。</summary>
    [Category("Layout")]
    [DefaultValue(0)]
    public int TreeMaxHeight { get; set { field = value; UpdateMetrics(); } }

    /// <summary>顶部的搜索框与“全部展开 / 收起”，默认显示。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool ShowToolbar { get => toolbar.Visible; set => toolbar.Visible = value; }

    /// <summary>里面的树（测试用）。</summary>
    internal TreeControl TreePart => tree;

    /// <summary>搜索框（测试用）。</summary>
    internal SearchFieldControl SearchPart => search;

    /// <summary>两个按钮（测试用）。</summary>
    internal (ButtonControl ExpandAll, ButtonControl CollapseAll) Buttons => (expandAll, collapseAll);

    /// <summary>路径栏的文字与两个复制按钮（测试用）。</summary>
    internal (TextControl Path, TextControl Hint, CopyButtonControl CopyPath, CopyButtonControl CopyValue) PathBarParts => (path, hint, copyPath, copyValue);

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

    /// <summary>树是等宽字体（不带连字）的 sm，最高 TreeMaxHeight（默认 24em）；路径栏至少 2.25em 高、左右留 xs。</summary>
    void UpdateMetrics()
    {
        var sm = Font.SizeInPoints * Tokens.FontSize.Sm / Tokens.FontSize.Md;
        tree.Font = Fonts.Get(Fonts.CodeMono(ProviderControl.ThemeOf(this)), sm, FontStyle.Regular);
        var em = this.EmPixels();
        tree.MaximumSize = new Size(int.MaxValue, TreeMaxHeight > 0 ? TreeMaxHeight : (int)MathF.Round(24 * em));
        var smPixels = em * Tokens.FontSize.Sm / Tokens.FontSize.Md;
        pathBar.MinimumSize = new Size(0, (int)MathF.Round(2.25f * smPixels));
        var xs = (int)MathF.Round(Tokens.Space.Xs * smPixels);
        pathBar.Padding = new Padding(xs, 0, xs, 0);
    }

    void SyncLang()
    {
        var lang = ProviderControl.LangOf(this);
        search.PlaceholderText = lang.JsonSearch;
        search.AccessibleName = lang.JsonSearch;
        expandAll.Text = lang.JsonExpandAll;
        collapseAll.Text = lang.JsonCollapseAll;
        // 两个按钮图标一样：悬停、聚焦时提示各复制什么
        foreach (var (button, name) in new[] { (copyPath, lang.JsonCopyPath), (copyValue, lang.JsonCopyValue) })
        {
            button.AccessibleName = name;
            tooltips.SetTooltip(button, name);
        }
        hint.Text = lang.JsonPathHint;
    }

    /// <summary>路径栏：选中了一项时是它的路径与两个复制按钮，没有时是提示。</summary>
    void SyncPathBar()
    {
        var node = tree.Selected.Count > 0 ? byPath.GetValueOrDefault(tree.Selected[0]) : null;
        hint.Visible = node is null;
        foreach (var part in new Control[] { path, copyPath, copyValue }) part.Visible = node is not null;
        if (node is null) return;
        path.Text = node.Path;
        tooltips.SetTooltip(path, node.Path);
        copyPath.Value = node.Path;
        copyValue.Value = JsonViewerNodes.CopyText(node);
    }

    /// <summary>
    /// 画一行：键名（序号是注释色）、值（按类型着色，放不下时省略），容器后面是项数；之间隔 xs，匹配的加粗。
    /// 键名与项数不缩，值分剩下的宽度。
    /// </summary>
    void PaintRow(Graphics graphics, Rectangle bounds, object item, TreeItemState state)
    {
        var node = (JsonViewerNode)item;
        var colors = ProviderControl.ThemeOf(tree).Colors;
        var font = state.Matched ? Fonts.Get(tree.Font.FamilyName(), tree.Font.SizeInPoints, FontStyle.Bold) : tree.Font;
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
        var gap = (int)MathF.Round(Tokens.Space.Xs * tree.Font.EmPixels(tree.DeviceDpi));
        int Width(string text) => Fonts.Measure(text, font, new Size(int.MaxValue, int.MaxValue), flags).Width;
        var name = $"{node.Name}:";
        var summary = node.IsContainer ? ProviderControl.LangOf(this).JsonItems(JsonViewerNodes.SizeOf(node)) : null;
        var x = bounds.Left;
        var nameWidth = Math.Min(Width(name), bounds.Width);
        TextRenderer.DrawText(graphics, name, font, new Rectangle(x, bounds.Top, nameWidth, bounds.Height), node.IsIndex ? colors.CodeComment : colors.CodeAttribute, flags);
        x += nameWidth + gap;
        var summaryWidth = summary is null ? 0 : Width(summary) + gap;
        var value = JsonViewerNodes.Display(node);
        var valueWidth = Math.Max(0, Math.Min(Width(value), bounds.Right - x - summaryWidth));
        TextRenderer.DrawText(graphics, value, font, new Rectangle(x, bounds.Top, valueWidth, bounds.Height), ColorOf(node.Kind, colors), flags | TextFormatFlags.EndEllipsis);
        x += valueWidth + gap;
        if (summary is not null && x < bounds.Right)
            TextRenderer.DrawText(graphics, summary, font, new Rectangle(x, bounds.Top, bounds.Right - x, bounds.Height), colors.CodeComment, flags);
    }

    /// <summary>值按类型的颜色（同上游的 KIND_STYLES：代码高亮的 token）。</summary>
    static Color ColorOf(JsonViewerKind kind, ThemeColors colors) => kind switch
    {
        JsonViewerKind.String => colors.CodeString,
        JsonViewerKind.Number => colors.CodeNumber,
        JsonViewerKind.Boolean or JsonViewerKind.Null => colors.CodeKeyword,
        JsonViewerKind.Date or JsonViewerKind.Other or JsonViewerKind.Function => colors.CodeType,
        _ => colors.CodeComment,
    };

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        SyncLang();
        UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateMetrics();
    }

    /// <summary>树的名称跟着 JsonViewerControl 的 AccessibleName。</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        tree.AccessibleName ??= AccessibleName;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) tooltips.Dispose();
        base.Dispose(disposing);
    }
}
