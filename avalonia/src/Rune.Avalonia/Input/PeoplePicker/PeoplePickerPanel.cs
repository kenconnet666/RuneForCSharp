using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 人员与组织选择面板（vue-tsx-pro 的 PeoplePickerPanel）：左边按组织架构一层层找（面包屑回到上层），或搜索、看角色与最近选过的，勾选；右边是已选的，可以移除、清空。
/// <see cref="PeoplePicker"/> 把它放进对话框；也可以直接放在页面、抽屉里。数据见 <see cref="PeopleSource"/>。
/// <code>
/// &lt;rune:PeoplePickerPanel Source="{Binding Org}" Entries="{Binding Members}" Types="user,department" /&gt;
/// </code>
/// <list type="bullet">
/// <item>部门的"下级"进到下一层（不能选部门时点整行也是进去），面包屑点哪一级回到哪一级。</item>
/// <item>搜索框有字时列表换成搜索结果（输入停下 300 毫秒再搜，只采用最后一次），清空回到原来的页。</item>
/// <item><see cref="Types"/> 决定能勾哪几种；<see cref="Max"/> 限制个数，1 时勾一个就换掉原来的。多选时列表上方有"全选"，只管这一页能勾的。</item>
/// <item>"最近"页：上游按 recentKey 存在浏览器里；桌面上由使用方存 <see cref="Recent"/>（双向绑定，最多 20 个），不写（null）时没有这一页。</item>
/// <item>按自己的宽度决定左右排（宽于 36rem 时左 3 右 2）还是上下排。</item>
/// </list>
/// 部件：<see cref="PeoplePickerPanelParts"/>。
/// </summary>
public class PeoplePickerPanel : Control
{
    /// <summary>数据来源。</summary>
    public static readonly StyledProperty<PeopleSource?> SourceProperty = AvaloniaProperty.Register<PeoplePickerPanel, PeopleSource?>(nameof(Source));

    /// <summary>选中的（TS 的 _value，双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<PeopleEntry>> EntriesProperty =
        AvaloniaProperty.Register<PeoplePickerPanel, IReadOnlyList<PeopleEntry>>(nameof(Entries), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>能选哪几种，默认只有人员。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> TypesProperty = AvaloniaProperty.Register<PeoplePickerPanel, IReadOnlyList<string>>(nameof(Types), [PeopleTypes.User]);

    /// <summary>最多选几个；1 时勾一个就换掉原来的。</summary>
    public static readonly StyledProperty<int?> MaxProperty = AvaloniaProperty.Register<PeoplePickerPanel, int?>(nameof(Max));

    /// <summary>最近选过的（双向绑定，由使用方存）；null 时没有"最近"页。</summary>
    public static readonly StyledProperty<IReadOnlyList<PeopleEntry>?> RecentProperty =
        AvaloniaProperty.Register<PeoplePickerPanel, IReadOnlyList<PeopleEntry>?>(nameof(Recent), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>两边列表的高度（设备无关像素），默认 20rem。</summary>
    public static readonly StyledProperty<double> ListHeightProperty = AvaloniaProperty.Register<PeoplePickerPanel, double>(nameof(ListHeight), 20 * 16);

    /// <summary>"最近"最多记几个。</summary>
    const int RecentLimit = 20;

    /// <summary>窄于它时上下排（36rem）。</summary>
    const double SideBySide = 36 * 16;

    readonly global::Avalonia.Controls.Grid grid = new();
    readonly Flex browse = new() { Name = PeoplePickerPanelParts.Browse, Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
    readonly Border selected = new() { Name = PeoplePickerPanelParts.Selected };
    readonly SearchField search = new() { Name = PeoplePickerPanelParts.Search };
    readonly ToggleGroup pages = new() { IsRequired = true, Size = TextSize.Sm, HorizontalAlignment = HorizontalAlignment.Left };
    readonly Breadcrumbs breadcrumbs = new();
    readonly Checkbox selectAll = new() { Name = PeoplePickerPanelParts.SelectAll, Size = TextSize.Sm };
    readonly ScrollArea browseScroll = new() { IsViewportFocusable = false };
    readonly ScrollArea selectedScroll = new() { VerticalAlignment = VerticalAlignment.Top };
    readonly Strong count = new() { Size = TextSize.Sm };
    readonly Button clear = new() { Name = PeoplePickerPanelParts.Clear, Variant = ButtonVariant.Link, Size = TextSize.Sm };
    readonly PeopleRequest level = new();
    readonly PeopleRequest found = new();
    readonly PeopleRequest roles = new();
    List<PeopleEntry> path = [];
    string page = "org";
    CancellationTokenSource? debounce;
    bool syncing;

    /// <summary>新建：左边浏览、右边已选。</summary>
    public PeoplePickerPanel()
    {
        Name ??= null;
        search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) OnQueryChanged();
        };
        pages.PropertyChanged += (_, e) =>
        {
            if (syncing || e.Property != ToggleGroup.ValueProperty || pages.Value is not { } next) return;
            page = next;
            if (page == "roles" && roles.Data is null && !roles.Loading) LoadRoles();
            Render();
        };
        breadcrumbs.Navigate += (_, e) =>
        {
            path = [.. path.Take(e.Index)];
            LoadLevel();
        };
        selectAll.PropertyChanged += (_, e) =>
        {
            if (!syncing && e.Property == Checkbox.IsCheckedProperty) ToggleAll(Shown().Items);
        };
        clear.Click += (_, _) => SetCurrentValue(EntriesProperty, Array.Empty<PeopleEntry>());
        browse.Children.AddRange([search, pages, breadcrumbs, selectAll, browseScroll]);
        selected.Child = new DockPanel { Children = { Header(), selectedScroll } };
        grid.Children.AddRange([browse, selected]);
        LogicalChildren.Add(grid);
        VisualChildren.Add(grid);
        AutomationProperties.SetName(browseScroll, null);
    }

    DockPanel Header()
    {
        var header = new DockPanel { Name = PeoplePickerPanelParts.Header, LastChildFill = false };
        DockPanel.SetDock(clear, Dock.Right);
        header.Children.AddRange([clear, count]);
        DockPanel.SetDock(header, Dock.Top);
        return header;
    }

    /// <summary>数据来源。</summary>
    public PeopleSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>选中的。</summary>
    public IReadOnlyList<PeopleEntry> Entries { get => GetValue(EntriesProperty); set => SetValue(EntriesProperty, value); }

    /// <summary>能选哪几种。</summary>
    public IReadOnlyList<string> Types { get => GetValue(TypesProperty); set => SetValue(TypesProperty, value); }

    /// <summary>最多选几个。</summary>
    public int? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>最近选过的。</summary>
    public IReadOnlyList<PeopleEntry>? Recent { get => GetValue(RecentProperty); set => SetValue(RecentProperty, value); }

    /// <summary>两边列表的高度。</summary>
    public double ListHeight { get => GetValue(ListHeightProperty); set => SetValue(ListHeightProperty, value); }

    /// <summary>搜索框（测试用；PeoplePicker 打开对话框时把焦点放进去）。</summary>
    internal SearchField SearchBox => search;

    /// <summary>左边列表里的行（测试用）。</summary>
    internal IEnumerable<ItemListItem> BrowseRows => browseScroll.GetVisualDescendants().OfType<ItemListItem>();

    /// <summary>右边已选的行（测试用）。</summary>
    internal IEnumerable<ItemListItem> SelectedRows => selectedScroll.GetVisualDescendants().OfType<ItemListItem>();

    /// <summary>面包屑（测试用）。</summary>
    internal Breadcrumbs Trail => breadcrumbs;

    /// <summary>浏览方式（测试用）。</summary>
    internal ToggleGroup Pages => pages;

    bool Searching => Source?.Search is not null && (search.Text ?? "").Trim().Length > 0;

    bool Pickable(PeopleEntry entry) => Types.Contains(entry.Type);

    static bool Drillable(PeopleEntry entry) => entry.Type == PeopleTypes.Department && entry.HasChildren != false;

    bool Full => Max is > 1 && Entries.Count >= Max;

    HashSet<string> SelectedKeys => [.. Entries.Select(entry => entry.Key)];

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Metrics();
        if (level.Data is null && !level.Loading) LoadLevel();
        Render();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // 关掉选择器时中止还在进行的请求
        level.Cancel();
        found.Cancel();
        roles.Cancel();
        debounce?.Cancel();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            path = [];
            level.Reset();
            found.Reset();
            roles.Reset();
            if (VisualRoot is not null) LoadLevel();
            Render();
        }
        else if (change.Property == EntriesProperty || change.Property == TypesProperty || change.Property == MaxProperty || change.Property == RecentProperty
                 || change.Property == Provider.LangProperty) Render();
        else if (change.Property == ListHeightProperty || change.Property == TextElement.FontSizeProperty) Metrics();
    }

    void LoadLevel()
    {
        if (Source is not { } source) return;
        var parent = path.LastOrDefault();
        _ = level.Run(cancel => source.Children(parent, cancel), Render);
        Render();
    }

    void LoadRoles()
    {
        if (Source?.Roles is not { } load) return;
        _ = roles.Run(load, Render);
    }

    /// <summary>搜索词变了：停下 300 毫秒再搜（只采用最后一次）；清空回到原来的页。</summary>
    void OnQueryChanged()
    {
        debounce?.Cancel();
        found.Reset();
        if (Searching && Source?.Search is { } load) _ = SearchLater(load, (search.Text ?? "").Trim(), (debounce = new CancellationTokenSource()).Token);
        Render();
    }

    async Task SearchLater(Func<string, CancellationToken, Task<IReadOnlyList<PeopleEntry>>> load, string text, CancellationToken token)
    {
        try
        {
            await Task.Delay(RemoteOptions.Debounce, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        await found.Run(cancel => load(text, cancel), Render);
    }

    /// <summary>现在列出的：搜索结果，或当前页。</summary>
    (IReadOnlyList<PeopleEntry> Items, bool Loading) Shown()
    {
        // 防抖等待时请求还没发出：没有结果也没出错就算加载中，不先显示"没有找到"
        if (Searching) return (found.Data ?? [], found.Loading || (found.Data is null && !found.Failed));
        return page switch
        {
            "roles" => (roles.Data ?? [], roles.Loading),
            "recent" => (Recent ?? [], false),
            _ => (level.Data ?? [], level.Loading),
        };
    }

    void Remember(PeopleEntry entry)
    {
        if (Recent is not { } recent) return;
        SetCurrentValue(RecentProperty, recent.Where(item => item.Key != entry.Key).Prepend(entry.ToValue()).Take(RecentLimit).ToList());
    }

    /// <summary>勾上或取消一项：1 个时换掉原来的，勾满时不再加。</summary>
    internal void Toggle(PeopleEntry entry)
    {
        if (SelectedKeys.Contains(entry.Key))
        {
            SetCurrentValue(EntriesProperty, Entries.Where(item => item.Key != entry.Key).ToList());
            return;
        }
        if (Max == 1) SetCurrentValue(EntriesProperty, new[] { entry.ToValue() });
        else if (Full) return;
        else SetCurrentValue(EntriesProperty, (IReadOnlyList<PeopleEntry>)[.. Entries, entry.ToValue()]);
        Remember(entry);
    }

    /// <summary>全选这一页能勾的；都勾上了时全部取消。有上限时加到满为止。</summary>
    void ToggleAll(IReadOnlyList<PeopleEntry> items)
    {
        var candidates = items.Where(Pickable).ToList();
        var keys = SelectedKeys;
        if (candidates.All(entry => keys.Contains(entry.Key)))
        {
            var drop = candidates.Select(entry => entry.Key).ToHashSet();
            SetCurrentValue(EntriesProperty, Entries.Where(item => !drop.Contains(item.Key)).ToList());
            return;
        }
        var adding = candidates.Where(entry => !keys.Contains(entry.Key)).ToList();
        var room = Max is { } max ? Math.Max(0, max - Entries.Count) : adding.Count;
        SetCurrentValue(EntriesProperty, (IReadOnlyList<PeopleEntry>)[.. Entries, .. adding.Take(room).Select(entry => entry.ToValue())]);
    }

    /// <summary>进到一个部门：从搜索结果进去时面包屑只有它（不知道它的上级）；进去后焦点落到列表上。</summary>
    internal void Open(PeopleEntry department)
    {
        path = Searching ? [department] : [.. path, department];
        search.Text = "";
        page = "org";
        LoadLevel();
        Dispatcher.UIThread.Post(() => browseScroll.Focus(), DispatcherPriority.Loaded);
    }

    /// <summary>点一行（勾选框、按钮自己处理）：能选的勾上或取消，不能选的部门进去。</summary>
    void OnRowClick(PeopleEntry entry, RoutedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<ChoiceBase>(true) is not null) return;
        if (Pickable(entry)) Toggle(entry);
        else if (Drillable(entry)) Open(entry);
    }

    /// <summary>按数据、页、已选重建两边。</summary>
    void Render()
    {
        var lang = Provider.GetLang(this);
        var source = Source;
        var (items, loading) = Shown();
        var searching = Searching;
        var keys = SelectedKeys;
        syncing = true;
        search.IsVisible = source?.Search is not null;
        search.PlaceholderText = lang.PeopleSearch;
        AutomationProperties.SetName(search, lang.PeopleSearch);
        List<string> available = ["org"];
        if (source?.Roles is not null) available.Add("roles");
        if (Recent is not null) available.Add("recent");
        if (!available.Contains(page)) page = "org";
        pages.IsVisible = !searching && available.Count > 1;
        string PageLabel(string name) => name switch { "roles" => lang.PeopleRoles, "recent" => lang.PeopleRecent, _ => lang.PeopleOrg };
        if (pages.Children.OfType<ToggleItem>().Select(item => item.Value).SequenceEqual(available) is false)
        {
            pages.Children.Clear();
            pages.Children.AddRange(available.Select(name => new ToggleItem { Value = name, Content = PageLabel(name) }));
        }
        pages.Value = page;
        AutomationProperties.SetName(pages, lang.PeoplePages);
        breadcrumbs.IsVisible = !searching && page == "org";
        breadcrumbs.Items = [new BreadcrumbItem(lang.PeopleRoot), .. path.Select(department => new BreadcrumbItem(department.Name))];
        var candidates = items.Where(Pickable).ToList();
        var checkedCount = candidates.Count(entry => keys.Contains(entry.Key));
        selectAll.IsVisible = Max != 1 && candidates.Count > 1;
        selectAll.Content = lang.PeopleSelectAll;
        selectAll.IsChecked = candidates.Count > 0 && checkedCount == candidates.Count;
        selectAll.IsIndeterminate = checkedCount > 0 && checkedCount < candidates.Count;

        browseScroll.Content = loading
            ? Status(new Spinner { Label = lang.Loading })
            : items.Count == 0
                ? Status(new Small { Text = searching ? lang.PeopleNoResult : lang.PeopleEmpty })
                : BrowseList(items, keys, PageLabel(searching ? "org" : page));

        count.Text = lang.PeopleCount(Entries.Count, Max);
        clear.IsVisible = Entries.Count > 0;
        clear.Content = lang.Clear;
        selectedScroll.Content = Entries.Count == 0 ? Status(new Small { Text = lang.PeopleNothingSelected }) : SelectedList(lang);
        syncing = false;
    }

    ItemList BrowseList(IReadOnlyList<PeopleEntry> items, HashSet<string> keys, string label)
    {
        var list = new ItemList { Hoverable = true, Density = ItemListDensity.Compact };
        AutomationProperties.SetName(list, label);
        var lang = Provider.GetLang(this);
        foreach (var entry in items)
        {
            var start = new Flex { Name = PeoplePickerPanelParts.Start, Align = FlexAlign.Center, Gap = Space.Sm };
            if (Pickable(entry))
            {
                var isChecked = keys.Contains(entry.Key);
                var box = new Checkbox { Name = PeoplePickerPanelParts.Checkbox, IsChecked = isChecked, IsEnabled = isChecked || !Full };
                AutomationProperties.SetName(box, entry.Name);
                box.PropertyChanged += (_, e) =>
                {
                    if (!syncing && e.Property == Checkbox.IsCheckedProperty) Toggle(entry);
                };
                start.Children.Add(box);
            }
            start.Children.Add(Badge(entry));
            Control? end = null;
            if (Drillable(entry))
            {
                var drill = new Button { Name = PeoplePickerPanelParts.Drill, Variant = ButtonVariant.Ghost, Size = TextSize.Sm, IconEnd = Lucide.ChevronRight, Content = lang.PeopleChildren };
                AutomationProperties.SetName(drill, lang.PeopleOpen(entry.Name));
                drill.Click += (_, e) =>
                {
                    e.Handled = true;
                    Open(entry);
                };
                end = drill;
            }
            var row = new ItemListItem { Start = start, Title = entry.Name, Description = entry.Description, End = end, Tag = entry };
            row.Click += (_, e) => OnRowClick(entry, e);
            list.Children.Add(row);
        }
        return list;
    }

    ItemList SelectedList(Lang lang)
    {
        var list = new ItemList { Density = ItemListDensity.Compact, Inset = Space.Sm };
        AutomationProperties.SetName(list, lang.PeopleSelected);
        foreach (var entry in Entries)
        {
            var remove = new IconButton { Name = PeoplePickerPanelParts.Remove, Icon = Lucide.X, Variant = ButtonVariant.Ghost, Size = TextSize.Sm };
            AutomationProperties.SetName(remove, lang.PeopleRemove(entry.Name));
            remove.Click += (_, e) =>
            {
                e.Handled = true;
                Toggle(entry);
            };
            list.Children.Add(new ItemListItem
            {
                Start = Badge(entry), Title = entry.Name, Description = entry.Description ?? lang.PeopleTypes.GetValueOrDefault(entry.Type, entry.Type), End = remove, Tag = entry,
            });
        }
        return list;
    }

    /// <summary>行首的圆形标记：人员是名字的头一个字（头像地址桌面上不下载），部门、角色是图标。</summary>
    static Avatar Badge(PeopleEntry entry) => entry.Type == PeopleTypes.User
        ? new Avatar { Name = PeoplePickerPanelParts.Avatar, DisplayName = entry.Name, Size = TextSize.Sm }
        : new Avatar { Name = PeoplePickerPanelParts.Avatar, Size = TextSize.Sm, Decorative = true, Fallback = new Icon { Data = entry.Type == PeopleTypes.Department ? Lucide.Building : Lucide.BriefcaseBusiness } };

    /// <summary>加载中、没有结果：居中、上下 lg、textMuted。</summary>
    Control Status(Control content)
    {
        var em = TextElement.GetFontSize(this);
        var box = new Border { Name = PeoplePickerPanelParts.Status, Padding = new Thickness(0, this.Resource<double>(ThemeKeys.Space.Lg) * em), Child = content, HorizontalAlignment = HorizontalAlignment.Stretch };
        content.HorizontalAlignment = HorizontalAlignment.Center;
        TextElement.SetForeground(box, this.Resource<IBrush>(ThemeKeys.Brush.TextMuted));
        return box;
    }

    /// <summary>em 尺寸：两栏隔 md；右边 surfaceMuted 底、borderMuted 描边、md 圆角、上下 sm；表头最少 2em 高、左右 sm；全选左右 sm 与行里的勾选框对齐。</summary>
    void Metrics()
    {
        var em = TextElement.GetFontSize(this);
        double Token(string key) => this.Resource<double>(key) * em;
        grid.ColumnSpacing = grid.RowSpacing = Token(ThemeKeys.Space.Md);
        browseScroll.Height = selectedScroll.Height = ListHeight;
        selected.Padding = new Thickness(0, Token(ThemeKeys.Space.Sm));
        selected.Background = this.Resource<IBrush>(ThemeKeys.Brush.SurfaceMuted);
        selected.BorderBrush = this.Resource<IBrush>(ThemeKeys.Brush.BorderMuted);
        selected.BorderThickness = new Thickness(1);
        selected.CornerRadius = new CornerRadius(Token(ThemeKeys.Radius.Md));
        if (selected.Child is DockPanel { Children: [DockPanel header, ..] })
        {
            header.MinHeight = 2 * em;
            header.Margin = new Thickness(Token(ThemeKeys.Space.Sm), 0, Token(ThemeKeys.Space.Sm), Token(ThemeKeys.Space.Sm));
        }
        selectAll.Margin = new Thickness(Token(ThemeKeys.Space.Sm), 0);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        Arrange(availableSize.Width);
        grid.Measure(availableSize);
        return grid.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        grid.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>左右排（左 3 右 2）还是上下排，按自己的宽度（上游的容器查询）。</summary>
    void Arrange(double width)
    {
        var wide = width >= SideBySide;
        grid.ColumnDefinitions = wide ? new ColumnDefinitions("3*,2*") : new ColumnDefinitions("*");
        grid.RowDefinitions = wide ? new RowDefinitions("Auto") : new RowDefinitions("Auto,Auto");
        global::Avalonia.Controls.Grid.SetColumn(selected, wide ? 1 : 0);
        global::Avalonia.Controls.Grid.SetRow(selected, wide ? 0 : 1);
    }

    /// <summary>一种请求（下级、搜索、角色）：只采用最后一次，新的请求取消旧的。</summary>
    sealed class PeopleRequest
    {
        CancellationTokenSource? pending;

        public IReadOnlyList<PeopleEntry>? Data { get; private set; }

        public bool Loading { get; private set; }

        public bool Failed { get; private set; }

        public void Cancel()
        {
            pending?.Cancel();
            pending = null;
            Loading = false;
        }

        public void Reset()
        {
            Cancel();
            Data = null;
            Failed = false;
        }

        public async Task Run(Func<CancellationToken, Task<IReadOnlyList<PeopleEntry>>> load, Action changed)
        {
            Cancel();
            var cancel = pending = new CancellationTokenSource();
            Loading = true;
            Failed = false;
            Data = null;
            try
            {
                var result = await load(cancel.Token);
                if (cancel.IsCancellationRequested) return;
                Data = result;
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                if (cancel.IsCancellationRequested) return;
                Failed = true;
                Trace.TraceWarning($"人员选择器的数据加载失败：{error}");
            }
            Loading = false;
            changed();
        }
    }
}

/// <summary>PeoplePickerPanel 的部件名。</summary>
public static class PeoplePickerPanelParts
{
    /// <summary>左边（搜索、浏览方式、面包屑、全选、列表）。</summary>
    public const string Browse = "PART_Browse";

    /// <summary>右边已选的（surfaceMuted 底的框）。</summary>
    public const string Selected = "PART_Selected";

    /// <summary>右边的表头（已选几个、清空）。</summary>
    public const string Header = "PART_Header";

    /// <summary>搜索框（TS 的 propsSearch）。</summary>
    public const string Search = "PART_Search";

    /// <summary>全选（TS 的 propsSelectAll）。</summary>
    public const string SelectAll = "PART_SelectAll";

    /// <summary>行首（勾选框与头像）。</summary>
    public const string Start = "PART_Start";

    /// <summary>行首的头像或图标（TS 的 propsAvatar）。</summary>
    public const string Avatar = "PART_Avatar";

    /// <summary>每一行的勾选框（TS 的 propsCheckbox）。</summary>
    public const string Checkbox = "PART_Checkbox";

    /// <summary>部门行的"下级"（TS 的 propsDrill）。</summary>
    public const string Drill = "PART_Drill";

    /// <summary>已选一侧的"清空"（TS 的 propsClear）。</summary>
    public const string Clear = "PART_Clear";

    /// <summary>已选的每一项的"移除"（TS 的 propsRemove）。</summary>
    public const string Remove = "PART_Remove";

    /// <summary>加载中、没有结果的那一行。</summary>
    public const string Status = "PART_Status";
}
