namespace Rune.WinForms.Examples;

/// <summary>TreeControl 的示例（vue-tsx-pro 文档站 display/Tree）。行是画出来的，没有 ItemTemplate：人数写进 GetLabel。</summary>
static class TreeExamples
{
    public static Example[] All =>
    [
        new("Tree", "files", "文件树", "GetKey、GetLabel、GetChildren 读你自己的数据结构，不用先转换；GetIcon 按展开状态换图标。键盘 ↑↓ 移动，→ 展开，← 收起，Enter 选中，输入文字跳到匹配项。节点少时按内容高，多了在树里滚动（最高 20em，或 MaximumSize 的高）；只画看得见的行。", Files),
        new("Tree", "outline", "搜索与拖动", "Filter 按节点文字过滤（FilterBy 自己判断）：留下匹配的节点与它们的上级并展开上级，匹配的加粗；清空后回到原来的展开状态。订阅了 Moved 才能拖动节点调整层级与顺序（或选中后 Alt + 方向键），用 TreeData.Move 生成新数据交回 ItemsSource；CanDrop 限制落点。", Outline),
        new("Tree", "checkable", "勾选", "Checkable 时每项前有勾选框：勾上父节点会勾上全部子孙，子节点全勾上时父节点也勾上，只勾一部分时父节点半选。Checked 里是全部勾上的节点；权限树里“只给这一级”时写 CheckStrictly。", Checkable),
        new("Tree", "lazy", "按需加载与不可用", "HasChildNodes 说节点有子节点（还没取回来也能展开），展开时调用 LoadChildren，任务结束前显示加载指示；取回后放进数据、调 Refresh。IsItemDisabled 的节点可以经过、展开，不能选中。行是画的，人数直接写进 GetLabel。", Lazy),
    ];

    static Control Framed(params Control[] children) =>
        Build.Card(new CardControl { CardPadding = Space.Sm }, Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch }, children));

    /// <summary>最高 rem 个 16 像素（逻辑像素）。</summary>
    static TreeControl Capped(TreeControl tree, int rem)
    {
        tree.MaximumSize = new Size(int.MaxValue, tree.LogicalToDeviceUnits(rem * 16));
        return tree;
    }

    sealed record Entry(string Path, string Name, IReadOnlyList<Entry>? Children = null);

    static Entry File(string dir, string name) => new($"{dir}/{name}", name);

    static Entry Folder(string dir, string name, Func<string, Entry[]> children) => new($"{dir}/{name}", name, children($"{dir}/{name}"));

    static readonly Entry[] Files_ =
    [
        Folder("", "src", src =>
        [
            Folder(src, "components", dir => [File(dir, "Button.tsx"), File(dir, "Card.tsx"), File(dir, "Dialog.tsx")]),
            Folder(src, "pages", dir => [File(dir, "Home.tsx"), File(dir, "Orders.tsx")]),
            File(src, "main.ts"),
            File(src, "router.ts"),
        ]),
        Folder("", "public", dir => [File(dir, "favicon.svg")]),
        File("", "package.json"),
        File("", "README.md"),
    ];

    static IconData IconOf(Entry entry, bool expanded) =>
        entry.Children is not null ? expanded ? Lucide.FolderOpen : Lucide.Folder
        : entry.Name.EndsWith(".json", StringComparison.Ordinal) ? Lucide.FileBraces
        : entry.Name.EndsWith(".md", StringComparison.Ordinal) ? Lucide.FileText
        : Lucide.FileCode;

    // 项目文件：单选，按展开状态换文件夹图标
    static Control Files()
    {
        var note = new SmallControl { Tone = TextTone.Muted };
        var tree = Capped(new TreeControl
        {
            ItemsSource = Files_,
            GetKey = node => ((Entry)node).Path,
            GetLabel = node => ((Entry)node).Name,
            GetChildren = node => ((Entry)node).Children,
            GetIcon = (node, expanded) => IconOf((Entry)node, expanded),
            Expanded = ["/src", "/src/components"],
            SelectionMode = SelectionMode.Single,
            Selected = ["/src/components/Button.tsx"],
            AccessibleName = "项目文件",
        }, 17);
        void Sync() => note.Text = $"选中：{(tree.Selected.Count > 0 ? tree.Selected[0] : "无")}";
        tree.SelectedChanged += (_, _) => Sync();
        Sync();
        return Framed(tree, note);
    }

    sealed record Section(string Id, string Title, IReadOnlyList<Section>? Children = null, bool Appendix = false);

    static readonly Section[] Manual =
    [
        new("intro", "第一章 入职须知", [new("intro-culture", "公司文化"), new("intro-it", "账号与设备")]),
        new("process", "第二章 采购流程", [new("process-request", "请购单"), new("process-quote", "询价与比价"), new("process-contract", "合同审批")]),
        new("quality", "第三章 来料检验", [new("quality-sampling", "抽样方案"), new("quality-report", "检验报告")]),
        new("appendix", "附录 表单模板", Appendix: true),
    ];

    // 培训手册的目录：搜索过滤，拖动调整层级与顺序（或 Alt + 方向键），附录不能再往下放
    static Control Outline()
    {
        var accessors = new TreeAccessors<Section>((section, _) => section.Id, section => section.Children);
        IReadOnlyList<Section> sections = Manual;
        var moves = 0;
        var note = new SmallControl { Tone = TextTone.Muted, Text = "拖动标题调整层级与顺序，或选中后按 Alt + 方向键；已移动 0 次" };
        var search = new TextFieldControl { PlaceholderText = "搜索章节（试试“检验”）", Icon = Lucide.Search, Clearable = true, AccessibleName = "搜索章节" };
        var tree = Capped(new TreeControl
        {
            ItemsSource = sections,
            GetKey = node => ((Section)node).Id,
            GetLabel = node => ((Section)node).Title,
            GetChildren = node => ((Section)node).Children,
            Expanded = ["intro", "process", "quality"],
            // 附录放在第一层，不放进别的章节里，也不接收子节点
            CanDrop = (node, target) => ((Section)node).Appendix ? target.Parent is null : target.Parent != "appendix",
            AccessibleName = "手册目录",
        }, 18);
        tree.Moved += (_, e) =>
        {
            sections = TreeData.Move(sections, accessors, (section, children) => section with { Children = children }, e.Key, e.Target) ?? sections;
            tree.ItemsSource = sections;
            note.Text = $"拖动标题调整层级与顺序，或选中后按 Alt + 方向键；已移动 {++moves} 次";
        };
        search.ValueChanged += (_, _) => tree.Filter = search.Value;
        return Framed(search, tree, note);
    }

    sealed record Region(string Code, string Name, IReadOnlyList<Region>? Children = null);

    static readonly Region[] Regions =
    [
        new("east", "华东",
        [
            new("sh", "上海"),
            new("zj", "浙江", [new("hz", "杭州"), new("nb", "宁波"), new("wz", "温州")]),
            new("js", "江苏", [new("nj", "南京"), new("su", "苏州")]),
        ]),
        new("south", "华南", [new("gz", "广州"), new("sz", "深圳")]),
    ];

    // 配送范围：勾上父节点勾上全部子孙，只勾一部分时父节点半选
    static Control Checkable()
    {
        var note = new SmallControl { Tone = TextTone.Muted };
        var tree = Capped(new TreeControl
        {
            ItemsSource = Regions,
            GetKey = node => ((Region)node).Code,
            GetLabel = node => ((Region)node).Name,
            GetChildren = node => ((Region)node).Children,
            Expanded = ["east", "zj"],
            Checkable = true,
            Checked = ["sh", "hz", "nb"],
            AccessibleName = "配送范围",
        }, 17);
        void Sync() => note.Text = $"已勾选 {tree.Checked.Count} 个节点";
        tree.CheckedChanged += (_, _) => Sync();
        Sync();
        return Framed(tree, note);
    }

    sealed class Department(string id, string name, int people, bool hasTeams, bool retired = false)
    {
        public string Id { get; } = id;

        public string Name { get; } = name;

        public int People { get; } = people;

        public bool HasTeams { get; } = hasTeams;

        public bool Retired { get; } = retired;

        public IReadOnlyList<Department>? Teams { get; set; }
    }

    /// <summary>模拟服务端：按部门取下一级，600 毫秒后返回。</summary>
    static async Task<Department[]> FetchTeams(Department parent)
    {
        await Task.Delay(600);
        return [.. new[] { "产品", "设计", "研发", "测试" }.Select((name, index) => new Department(
            $"{parent.Id}-{index}", $"{parent.Name.TrimEnd('部')}{name}组", 3 + (index * 7 + parent.People) % 9, false, index == 3 && parent.Id == "rd"))];
    }

    // 组织架构：子部门展开时才向“服务端”取；已停用的部门可以展开、不能选中
    static Control Lazy()
    {
        const string hint = "展开部门时才加载下面的小组";
        var note = new SmallControl { Tone = TextTone.Muted, Text = hint };
        TreeControl? tree = null;
        tree = Capped(new TreeControl
        {
            ItemsSource = new[]
            {
                new Department("product", "产品部", 24, true),
                new Department("rd", "研发部", 86, true),
                new Department("market", "市场部", 15, true),
                new Department("legal", "法务部（已停用）", 0, false, retired: true),
            },
            GetKey = node => ((Department)node).Id,
            GetLabel = node => $"{((Department)node).Name} · {((Department)node).People} 人",
            GetChildren = node => ((Department)node).Teams,
            HasChildNodes = node => ((Department)node).HasTeams,
            LoadChildren = async node =>
            {
                var department = (Department)node;
                department.Teams = await FetchTeams(department);
                tree!.Refresh();
            },
            IsItemDisabled = node => ((Department)node).Retired,
            GetIcon = (node, _) => ((Department)node).HasTeams ? Lucide.BuildingComplex : Lucide.Users,
            SelectionMode = SelectionMode.Single,
            Indent = 1.5,
            AccessibleName = "组织架构",
        }, 16);
        tree.SelectedChanged += (_, _) => note.Text = tree.Selected.Count > 0 ? $"选中了 {tree.Selected[0]}" : hint;
        return Framed(tree, note);
    }
}
