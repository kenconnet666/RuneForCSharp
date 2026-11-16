using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Rune.Avalonia.Examples;

/// <summary>TreeSelect 的示例（vue-tsx-pro 文档站 input/TreeSelect）。</summary>
static class TreeSelectExamples
{
    public static Example[] All =>
    [
        new("TreeSelect", "basic", "组织机构", "单选从组织机构树里选一个节点（点节点或 Enter 选中并关闭）；多选勾选，勾上一个中心就是整枝，触发框里只列最上层的整枝。Searchable 时按节点文字过滤，命中节点的上级一起展开。", Basic),
        new("TreeSelect", "strategy", "勾选的取法与标签", "CheckedStrategy 决定多选的值里放哪些勾上的节点：All 全部（默认）、Parent 整枝时只放父节点、Child 只放叶子。点标签的 × 取消整枝，Backspace 移除最后一个；MaxTagCount=\"responsive\" 时标签排成一行，放不下的收进“+N”。", Strategy),
        new("TreeSelect", "appearance", "外观与状态", "前置图标、Variant、Radius、Size 与 Select 同一套；IsItemDisabled 让中心、部这一级不能选（只能展开），只能选到团队。在 Field 里沿用出错、必填、只读、不可用。", Appearance),
        new("TreeSelect", "remote", "远程搜索", "Load 按搜索词请求（同 Select 的 Load，只取第一页）：服务端返回命中的节点连同它们的上级，结果全部展开；ItemsSource 放上已选节点的那一枝（编辑页从详情接口拿到），触发框一开始就有文字。", Remote),
    ];

    /// <summary>示例用的组织机构（虚构）：公司 → 中心 / 部 → 团队。</summary>
    sealed record Org(string Id, string Name, Org[]? Children = null);

    static readonly Org[] Orgs =
    [
        new("hq", "总部",
        [
            new("pmo", "项目管理办公室（PMO）", [new("pmo-plan", "计划团队"), new("pmo-quality", "质量团队")]),
            new("finance", "财务部", [new("finance-budget", "预算团队"), new("finance-settle", "结算团队")]),
        ]),
        new("rd", "研发中心", [new("rd-frontend", "前端团队"), new("rd-backend", "后端团队")]),
        new("mfg", "生产中心", [new("mfg-assembly", "装配团队"), new("mfg-testing", "测试团队"), new("mfg-packing", "包装团队")]),
        new("purchase", "采购部"),
    ];

    static TreeSelect Create(IReadOnlyList<Org>? items = null) => new()
    {
        ItemsSource = items ?? Orgs,
        GetKey = org => ((Org)org).Id,
        GetLabel = org => ((Org)org).Name,
        GetChildren = org => ((Org)org).Children,
    };

    /// <summary>示例下方的"值：…"（值是行内代码），跟着选择更新。</summary>
    static Small ValueNote(TreeSelect select, string prefix, Func<string> value)
    {
        var note = new Small { Tone = TextTone.Muted };
        void Render() => note.Inlines = [new Run(prefix), new Code { Inlines = { new Run(value()) } }];
        Render();
        select.PropertyChanged += (_, e) =>
        {
            if (e.Property == SelectBase.ValueProperty || e.Property == SelectBase.ValuesProperty) Render();
        };
        return note;
    }

    static Control Basic()
    {
        var org = Create();
        org.Value = "rd-frontend";
        org.Searchable = org.Clearable = true;
        var teams = Create();
        teams.SelectionMode = SelectionMode.Multiple;
        teams.Values = ["mfg", "mfg-assembly", "mfg-testing", "mfg-packing", "finance-budget"];
        teams.Searchable = teams.Clearable = true;
        var orgNote = ValueNote(org, "值：", () => string.IsNullOrEmpty(org.Value) ? "—" : org.Value);
        var teamsNote = ValueNote(teams, "勾上的全部：", () => teams.Values is { Count: > 0 } values ? string.Join("、", values) : "—");
        return new Grid
        {
            MinItemWidth = "16rem", Gap = Space.Lg,
            Children =
            {
                new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch,
                    Children = { new Field { Label = "提出单位", Description = "打开后输入“团队”试试：命中节点的上级一起展开", Content = org }, orgNote },
                },
                new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch,
                    Children = { new Field { Label = "责任团队（多选）", Content = teams }, teamsNote },
                },
            },
        };
    }

    static Control Strategy()
    {
        // 勾上的全部节点；按取法整理后才是提交的值
        string[] initial = ["mfg", "mfg-assembly", "mfg-testing", "mfg-packing", "finance-budget"];
        var index = TreeData.Index<Org>(Orgs, new TreeAccessors<Org>((org, _) => org.Id, org => org.Children));
        var scope = Create();
        scope.SelectionMode = SelectionMode.Multiple;
        scope.CheckedStrategy = CheckStrategy.Parent;
        scope.MaxTagCount = TagLimit.Responsive;
        scope.Values = TreeData.CheckedByStrategy(index, initial, CheckStrategy.Parent);
        scope.Searchable = scope.Clearable = true;
        var note = ValueNote(scope, "提交的值：", () => scope.Values is { Count: > 0 } values ? string.Join("、", values) : "—");
        var strategy = new ToggleGroup
        {
            IsRequired = true, Size = TextSize.Sm, Value = nameof(CheckStrategy.Parent),
            Children =
            {
                new ToggleItem { Value = nameof(CheckStrategy.All), Content = "全部（默认）" },
                new ToggleItem { Value = nameof(CheckStrategy.Parent), Content = "整枝只要父节点" },
                new ToggleItem { Value = nameof(CheckStrategy.Child), Content = "只要叶子" },
            },
        };
        AutomationProperties.SetName(strategy, "checkedStrategy");
        strategy.PropertyChanged += (_, e) =>
        {
            if (e.Property != ToggleGroup.ValueProperty || strategy.Value is not { } value) return;
            // 换取法后按新的取法整理同一批勾选（演示用；实际表单里取法是固定的）
            scope.CheckedStrategy = Enum.Parse<CheckStrategy>(value);
            scope.Values = TreeData.CheckedByStrategy(index, initial, scope.CheckedStrategy);
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
            Children = { strategy, new Field { Label = "通知范围", Description = "点标签的 × 取消整枝；Backspace 移除最后一个", Content = scope }, note },
        };
    }

    static Control Appearance()
    {
        TreeSelect Team()
        {
            var select = Create();
            select.IsItemDisabled = org => ((Org)org).Children is not null;
            select.Placeholder = "选择团队";
            return select;
        }
        var team = Team();
        team.Icon = Lucide.Users;
        var plain = Create();
        plain.Placeholder = "选择团队";
        var required = new Field { Label = "必填", IsRequired = true, Error = "请选择承接团队", Content = plain };
        // 上游两处绑的是同一个值：选了承接团队，必填那一项跟着有值
        void Sync(object? sender, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != SelectBase.ValueProperty || sender is not TreeSelect source) return;
            var other = source == team ? plain : team;
            if (other.Value != source.Value) other.Value = source.Value;
            required.Error = string.IsNullOrEmpty(source.Value) ? "请选择承接团队" : null;
        }
        team.PropertyChanged += Sync;
        plain.PropertyChanged += Sync;
        var soft = Create();
        soft.Value = "finance";
        soft.Icon = Lucide.BuildingComplex;
        soft.Variant = ControlVariant.Soft;
        soft.Radius = Radius.Full;
        soft.Clearable = true;
        var small = Create();
        small.Value = "finance";
        small.Size = TextSize.Sm;
        soft.PropertyChanged += (_, e) =>
        {
            if (e.Property == SelectBase.ValueProperty && small.Value != soft.Value) small.Value = soft.Value;
        };
        var readOnly = Create();
        readOnly.Value = "finance-settle";
        readOnly.Icon = Lucide.BuildingComplex;
        var disabled = Create();
        disabled.Value = "purchase";
        return new Grid
        {
            MinItemWidth = "20rem", Gap = Space.Lg,
            Children =
            {
                new Field { Label = "承接团队", Description = "中心、部这一级不能选", Content = team },
                new Field { Label = "variant=\"soft\" radius=\"full\"", Content = soft },
                required,
                new Field { Label = "size=\"sm\"", Content = small },
                new Field { Label = "只读", IsReadOnly = true, Description = "能聚焦、能看，不能打开", Content = readOnly },
                new Field { Label = "不可用", IsEnabled = false, Content = disabled },
            },
        };
    }

    /// <summary>只留命中的节点与它们的上级（裁掉没有命中的分枝）。</summary>
    static Org[] Prune(IEnumerable<Org> nodes, Func<Org, bool> hit) =>
    [
        .. nodes.Select(org => (org, children: Prune(org.Children ?? [], hit)))
            .Where(pair => hit(pair.org) || pair.children.Length > 0)
            .Select(pair => pair.org with { Children = pair.children.Length > 0 ? pair.children : null }),
    ];

    /// <summary>假的搜索接口：400 毫秒后返回；被后来的请求顶掉、弹出层关上时取消。</summary>
    static async Task<OptionsPage> SearchOrgs(string query, int page, CancellationToken cancellation)
    {
        await Task.Delay(400, cancellation);
        return OptionsPage.All(query.Length > 0 ? Prune(Orgs, org => org.Name.Contains(query)) : Orgs);
    }

    static Control Remote()
    {
        var org = Create(Prune(Orgs, item => item.Id == "finance-settle"));
        org.Load = SearchOrgs;
        org.Searchable = org.Clearable = true;
        org.Value = "finance-settle";
        var note = ValueNote(org, "值：", () => string.IsNullOrEmpty(org.Value) ? "—" : org.Value);
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch,
            Children = { new Field { Label = "归口部门", Description = "打开后输入“团队”：服务端返回命中的节点与它们的上级", Content = org }, note },
        };
    }
}
