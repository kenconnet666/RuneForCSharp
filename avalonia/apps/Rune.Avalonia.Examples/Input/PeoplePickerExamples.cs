using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>PeoplePicker 的示例（vue-tsx-pro 文档站 input/PeoplePicker）。上游的 recentKey 存在浏览器里，这里由示例自己记着 Recent。</summary>
static class PeoplePickerExamples
{
    public static Example[] All =>
    [
        new("PeoplePicker", "field", "表单字段", "抄送可以选人员与部门，触发框里是已选的标签；点开对话框，按组织架构一层层找或搜索，右边是已选的，按“确定”才写回。Recent 记下最近选过的（“最近”页，由使用方存）。经办人 Max=1 只选一个，触发框里直接是名字。", Field),
        new("PeoplePicker", "panel", "直接放面板", "直接放在页面里的面板：流程节点的处理人选角色（角色收件箱），Types 是角色与人员时都能勾，最多 3 个；勾满之后其余的勾选框不可用。“角色”页列出全部角色，“全选”只管这一页能勾的。", Panel),
        new("PeoplePicker", "remote", "远程数据", "PeopleSource 的函数按自己的接口实现（这里用延时模拟请求），取消令牌在换了部门、改了搜索词、关掉对话框时取消；加载中显示转圈。真实接口常是 GET /org/children?parent=…、GET /org/search?q=…", Remote),
    ];

    static PeopleNode Person(string id, string name, string description) => new(id, PeopleTypes.User, name, description);

    static PeopleNode Department(string id, string name, string description, params PeopleNode[] children) => new(id, PeopleTypes.Department, name, description, children);

    /// <summary>示例用的组织架构：公司下面三个部门，采购部再分两个组；另有四个角色。</summary>
    static readonly PeopleNode[] Org =
    [
        Department("d-buy", "采购部", "公司",
            Department("d-buy-source", "寻源组", "采购部", Person("u-zhao", "赵一凡", "采购部 · 寻源工程师"), Person("u-sun", "孙悦", "采购部 · 寻源工程师")),
            Department("d-buy-exec", "执行组", "采购部", Person("u-zhou", "周婷", "采购部 · 采购专员"), Person("u-wu", "吴昊", "采购部 · 采购专员")),
            Person("u-lin", "林晓", "采购部 · 经理")),
        Department("d-qa", "质量部", "公司", Person("u-chen", "陈思雨", "质量部 · 经理"), Person("u-li", "李想", "质量部 · 质量工程师")),
        Department("d-fin", "财务部", "公司", Person("u-wang", "王建国", "财务部 · 经理"), Person("u-wu-jia", "吴佳", "财务部 · 会计"), Person("u-zheng", "郑一鸣", "财务部 · 出纳")),
    ];

    static readonly PeopleEntry[] Roles =
    [
        new("r-buyer", PeopleTypes.Role, "采购专员", "采购部"),
        new("r-buy-manager", PeopleTypes.Role, "采购经理", "采购部"),
        new("r-qa", PeopleTypes.Role, "质量工程师", "质量部"),
        new("r-cashier", PeopleTypes.Role, "出纳", "财务部"),
    ];

    static readonly PeopleSource Local = PeopleSource.Local(Org, Roles);

    static Control Field()
    {
        var cc = new PeoplePicker
        {
            Source = Local,
            Entries = [new("u-lin", PeopleTypes.User, "林晓", "采购部 · 经理"), new("d-qa", PeopleTypes.Department, "质量部", "公司")],
            Types = [PeopleTypes.User, PeopleTypes.Department],
            Recent = [],
        };
        var owner = new PeoplePicker { Source = Local, Max = 1, Placeholder = "选择经办人" };
        var note = new Small { Tone = TextTone.Muted };
        void Update() => note.Text = "抄送：" + (cc.Entries.Count > 0 ? string.Join("、", cc.Entries.Select(entry => $"{entry.Name}（{(entry.Type == PeopleTypes.User ? "人员" : "部门")}）")) : "无");
        cc.PropertyChanged += (_, e) =>
        {
            if (e.Property == PeoplePicker.EntriesProperty) Update();
        };
        Update();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
            Children = { new Field { Label = "抄送", Content = cc }, new Field { Label = "经办人", Description = "只能选一个人", Content = owner }, note },
        };
    }

    static Control Panel()
    {
        var panel = new PeoplePickerPanel
        {
            Source = Local,
            Entries = [new("r-buyer", PeopleTypes.Role, "采购专员", "采购部")],
            Types = [PeopleTypes.Role, PeopleTypes.User],
            Max = 3,
            ListHeight = 16 * 16,
        };
        AutomationProperties.SetName(panel, "处理人");
        return panel;
    }

    /// <summary>等一会儿再给结果；被取消时抛 OperationCanceledException（同真实的请求）。</summary>
    static async Task<IReadOnlyList<PeopleEntry>> Later(Func<Task<IReadOnlyList<PeopleEntry>>> result, CancellationToken cancel)
    {
        await Task.Delay(600, cancel);
        return await result();
    }

    static Control Remote()
    {
        var source = new PeopleSource(
            (parent, cancel) => Later(() => Local.Children(parent, cancel), cancel),
            (query, cancel) => Later(() => Local.Search!(query, cancel), cancel));
        return new Field { Label = "项目成员", Content = new PeoplePicker { Source = source, MaxTagCount = TagLimit.Responsive } };
    }
}
