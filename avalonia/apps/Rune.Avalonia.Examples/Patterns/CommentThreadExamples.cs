using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Rune.Avalonia.Examples;

/// <summary>CommentThread 页的示例（vue-tsx-pro 文档站 patterns/CommentThread）。</summary>
static class CommentThreadExamples
{
    public static Example[] All =>
    [
        new("CommentThread", "discussion", "沟通记录", "Comments 是留言，旧的在前，回复在各自的 Replies 里（只分一层：回复一条回复时仍挂在那一串下面，开头写上“@作者 ”）。People 是可以 @ 的人，也是留言里“@名字”高亮的依据；Uploader 给了才能带附件。Send 收到内容、提到的人、上传完的附件与 ReplyTo，返回的任务进行中按钮转圈，成功后清空，失败时保留内容并提示（内容里写“失败”试试）；留言存在哪、怎样出现在列表里由应用决定。Role 是名字后面的身份标记，Actions 给每条加操作（编辑、删除），时间写成相对现在、悬停看完整时间。", Discussion),
        new("CommentThread", "negotiation", "协商的逐条回应（只读）", "不给 Send 时只读，没有输入框与回复按钮。Extra 放在内容下面：回应的结论、改了的字段（FieldCompare）。Now 固定“现在”（演示数据、截图）。单独的输入框（协商回复、审批意见）用 CommentComposer。", Negotiation),
    ];

    static readonly ListboxOption[] People =
    [
        new("E2007", "方圆") { Description = "财务分析师", Group = "财务部" },
        new("E2015", "韩笑") { Description = "财务部负责人", Group = "财务部" },
        new("E1031", "陆川") { Description = "采购经理", Group = "采购部" },
        new("S0088", "华辰-李工") { Description = "供应商技术", Group = "供应商" },
    ];

    static Badge Role(string text, StatusTone? tone = null) => new() { Tone = tone ?? StatusTone.Neutral, Size = TextSize.Sm, Content = text };

    static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    static readonly CommentAuthor Me = new("方圆", Photos.Create("#10b981", "#0d9488"));

    static IReadOnlyList<CommentItem> Initial() =>
    [
        new("c1", "华辰-李工", "支架改用 6063-T5 后壁厚可以从 3.0 减到 2.6，强度校核见附件。@方圆 麻烦确认一下测算里的材料单价是否按新牌号。", At("2026-10-21T09:12:00+08:00"))
        {
            Role = Role("供应商", StatusTone.Warning),
            Attachments = [new("支架强度校核.pdf", new Uri("https://files.example.com/strength"), 1_240_000), new("6063 报价.xlsx", new Uri("https://files.example.com/quote"), 38_000)],
            Replies =
            [
                new("c2", "方圆", "已按 6063-T5 的 10 月均价重算，单价降到 ¥116.80。@韩笑 请过目。", At("2026-10-21T14:30:00+08:00")) { Avatar = Me.Avatar, Role = Role("财务分析师") },
                new("c3", "韩笑", "@方圆 年用量按 Q4 排产补到 45,500，其余没问题。", At("2026-10-22T10:05:00+08:00")) { Role = Role("财务部负责人"), Edited = true },
            ],
        },
        new("c4", "陆川", "换供应商的话交期能不能保证？@华辰-李工 请给一下首批的排产计划。", At("2026-10-23T16:40:00+08:00")) { Role = Role("采购经理") },
    ];

    /// <summary>假的上传：每 200 毫秒进 25%。</summary>
    static async Task<object?> Upload(IStorageFile file, IProgress<double> progress, CancellationToken cancellation)
    {
        for (var fraction = 0.25; ; fraction += 0.25)
        {
            await Task.Delay(200, cancellation);
            progress.Report(Math.Min(fraction, 1));
            if (fraction >= 1) return new Uri($"https://files.example.com/{Uri.EscapeDataString(file.Name)}");
        }
    }

    /// <summary>发出去的留言直接加进列表（乐观更新）；内容里写“失败”会模拟接口出错。自己的留言可以删除（Actions）。</summary>
    static Control Discussion()
    {
        var thread = new CommentThread { Comments = Initial(), People = People, CurrentUser = Me, Uploader = Upload };
        var sequence = 0;
        thread.Send = async draft =>
        {
            await Task.Delay(500);
            if (draft.Content.Contains("失败")) throw new InvalidOperationException("网络错误");
            var comment = new CommentItem($"new-{++sequence}", Me.Name, draft.Content, DateTimeOffset.Now)
            {
                Avatar = Me.Avatar, Role = Role("财务分析师"),
                Attachments = [.. draft.Attachments.Select(file => new CommentAttachment(file.Name, file.Response as Uri, file.Size))],
            };
            var comments = thread.Comments ?? [];
            thread.Comments = draft.ReplyTo is { } replyTo
                ? [.. comments.Select(item => item.Id == replyTo ? item with { Replies = [.. item.Replies ?? [], comment] } : item)]
                : [.. comments, comment];
        };
        thread.Actions = comment =>
        {
            if (comment.Author != Me.Name) return null;
            var remove = new Button { Variant = ButtonVariant.Link, Tone = StatusTone.Danger, Size = TextSize.Sm, Icon = Lucide.Trash, Content = "删除" };
            remove.Click += (_, _) => thread.Comments =
            [
                .. (thread.Comments ?? []).Where(item => item.Id != comment.Id)
                    .Select(item => item with { Replies = item.Replies?.Where(reply => reply.Id != comment.Id).ToList() }),
            ];
            return remove;
        };
        return new Card { Content = thread };
    }

    record Estimate(decimal UnitPrice, decimal ScrapRate);

    static readonly CompareField[] Fields =
    [
        new("UnitPrice", "单价") { Format = new NumericFormat { Style = NumericStyle.Currency }, Better = CompareBetter.Lower },
        new("ScrapRate", "废品率") { Format = new NumericFormat { Style = NumericStyle.Percent }, Better = CompareBetter.Lower },
    ];

    /// <summary>回应的结论（采纳 / 不采纳）与改了的字段。</summary>
    static Control Verdict(bool accepted, Estimate? before = null, Estimate? after = null)
    {
        var verdict = new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Start,
            Children = { Role(accepted ? "采纳" : "不采纳", accepted ? StatusTone.Success : StatusTone.Neutral) },
        };
        if (before is not null && after is not null) verdict.Children.Add(new FieldCompare { Fields = Fields, Before = before, After = after, Layout = FieldCompareLayout.List });
        return verdict;
    }

    static Control Negotiation()
    {
        Estimate v2 = new(119.35m, 0.032m), v3 = new(116.8m, 0.025m);
        CommentItem[] comments =
        [
            new("o1", "华辰-李工", "废品率按 3.2% 偏高，我们产线近 6 个月平均 2.5%，统计表见附件。", At("2026-10-19T10:20:00+08:00"))
            {
                Role = Role("第 1 轮异议", StatusTone.Warning),
                Attachments = [new("近 6 个月废品统计.xlsx", new Uri("https://files.example.com/scrap"), 52_000)],
                Replies =
                [
                    new("r1", "方圆", "核对过统计表，按 2.5% 重新测算，单价随之调整。", At("2026-10-20T15:02:00+08:00")) { Role = Role("财务部"), Extra = Verdict(true, v2, v3) },
                ],
            },
            new("o2", "华辰-李工", "模具摊销按 3 年太短，建议按 5 年摊。", At("2026-10-19T10:26:00+08:00"))
            {
                Role = Role("第 1 轮异议", StatusTone.Warning),
                Replies =
                [
                    new("r2", "方圆", "该零件的项目寿命只有 3 年，摊销年限不能超过项目寿命，维持原测算。", At("2026-10-20T15:10:00+08:00")) { Role = Role("财务部"), Extra = Verdict(false) },
                ],
            },
        ];
        return new Card { Content = new CommentThread { Comments = comments, Now = At("2026-10-24T09:00:00+08:00") } };
    }
}
