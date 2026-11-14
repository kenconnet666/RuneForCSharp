namespace Rune.Avalonia;

/// <summary>清单一项的状态（vue-tsx-pro 的 ChecklistStatuses）。</summary>
public enum ChecklistStatus
{
    /// <summary>待办（默认）。</summary>
    Todo,

    /// <summary>完成。</summary>
    Done,

    /// <summary>超时：过了计划日期还没完成（日期用危险色）。</summary>
    Overdue,

    /// <summary>受阻：等别人、缺前提（不能勾）。</summary>
    Blocked,

    /// <summary>不适用：这一次不用做，不算进进度（名称划掉，不能勾）。</summary>
    Skipped,
}

/// <summary>清单的排法（vue-tsx-pro 的 ChecklistLayouts）。</summary>
public enum ChecklistLayout
{
    /// <summary>一项一行（默认）：状态、名称与说明、负责人 · 日期 · 证据，操作在结束边。</summary>
    List,

    /// <summary>一行排开、放不下换行：状态、名称、日期、证据（详情行、卡片的一角）。</summary>
    Inline,

    /// <summary>只有一排状态图标（表格的格子里）：悬停与读屏有每一项的说明。</summary>
    Icons,
}

/// <summary>清单的一项（vue-tsx-pro 的 ChecklistItem）：名称、说明、负责人、日期、证据、操作可以是文字或控件。</summary>
public sealed class ChecklistItem
{
    /// <summary>唯一的 key（使用方按它找回自己的数据）。</summary>
    public string? Key { get; init; }

    /// <summary>名称。</summary>
    public object? Label { get; init; }

    /// <summary>状态，默认待办。</summary>
    public ChecklistStatus Status { get; init; }

    /// <summary>说明：怎么做、要什么材料、缺的由谁出具。</summary>
    public object? Description { get; init; }

    /// <summary>负责人（谁处理、谁出具）。</summary>
    public object? Owner { get; init; }

    /// <summary>日期：完成的日期，或计划日期（超时时用危险色）。</summary>
    public object? Date { get; init; }

    /// <summary>证据：附件、报告的链接。</summary>
    public object? Evidence { get; init; }

    /// <summary>操作：上传证据、去处理、催办（结束边）。</summary>
    public object? Action { get; init; }
}

/// <summary>清单的计算：每种状态的图标、语言包的键、汇总的完成数（不适用的不算）。三个平台各有一份逐字相同的副本。</summary>
static class ChecklistModel
{
    /// <summary>状态的图标。</summary>
    public static IconData Icon(ChecklistStatus status) => status switch
    {
        ChecklistStatus.Done => Lucide.CircleCheck,
        ChecklistStatus.Overdue => Lucide.CircleExclamationPoint,
        ChecklistStatus.Blocked => Lucide.Ban,
        ChecklistStatus.Skipped => Lucide.CircleMinus,
        _ => Lucide.CircleDashed,
    };

    /// <summary>语言包 ChecklistStatus 里的键（同 TS：done、todo、overdue、blocked、skipped）。</summary>
    public static string LangKey(ChecklistStatus status) => status.ToString().ToLowerInvariant();

    /// <summary>能不能勾：不适用、受阻的不能。</summary>
    public static bool Checkable(ChecklistStatus status) => status is not (ChecklistStatus.Skipped or ChecklistStatus.Blocked);

    /// <summary>汇总：完成了几项、一共几项（不适用的不算）。</summary>
    public static (int Done, int Total) Count(IEnumerable<ChecklistItem> items)
    {
        var counted = items.Where(item => item.Status != ChecklistStatus.Skipped).ToList();
        return (counted.Count(item => item.Status == ChecklistStatus.Done), counted.Count);
    }

    /// <summary>名称的纯文字（读屏、图标排法的说明用）：不是文字时为空。</summary>
    public static string LabelText(ChecklistItem item) => item.Label as string ?? "";
}
