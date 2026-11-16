using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Transfer 的示例（vue-tsx-pro 文档站 input/Transfer）。tree 等 TreeTransfer。</summary>
static class TransferExamples
{
    public static Example[] All =>
    [
        new("Transfer", "members", "项目成员", "从全部成员里挑人加入项目：勾上之后点中间的箭头，或双击一项直接移过去；Searchable 时两边带搜索框（按文字，忽略大小写与重音；上游还认拼音，我们没有拼音表），离职的人（IsDisabled 的选项）勾不上、移不动。Values 按移过去的先后排列。", Members),
    ];

    sealed record Member(string Id, string Name, string Team, bool Left);

    static readonly Member[] People =
    [
        .. new[] { ("周一航", "设计"), ("许悠", "设计"), ("林小满", "前端"), ("赵浩然", "前端"), ("陈默", "后端"), ("韩笑", "后端"), ("宋知遥", "测试"), ("顾北辰", "运维"),
                   ("沈清禾", "产品"), ("陆星河", "前端"), ("唐语桐", "后端"), ("温子期", "测试"), ("苏念", "产品"), ("江屿", "设计") }
            .Select((person, index) => new Member($"u{index + 1}", person.Item1, person.Item2, index is 6 or 11)),
    ];

    static Control Members()
    {
        var transfer = new Transfer
        {
            ItemsSource = People,
            ToOption = item => item is Member member
                ? new ListboxOption(member.Id, member.Name) { Description = member.Left ? $"{member.Team} · 已离职" : member.Team, IsDisabled = member.Left }
                : new ListboxOption("", ""),
            Values = ["u3", "u5"],
            Titles = ["全部成员", "项目成员"],
            Searchable = true,
        };
        var summary = new Small { Tone = TextTone.Muted };
        void Sync() => summary.Text = $"已加入 {transfer.Values.Count} 人：{string.Join("、", transfer.Values.Select(id => People.FirstOrDefault(member => member.Id == id)?.Name))}";
        transfer.PropertyChanged += (_, e) => { if (e.Property == Transfer.ValuesProperty) Sync(); };
        Sync();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { new Field { Label = "项目成员", Description = "勾上之后点中间的箭头，或双击一项", Content = transfer }, summary },
        };
    }
}
