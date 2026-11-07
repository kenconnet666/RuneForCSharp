namespace Rune.WinForms;

/// <summary>
/// 桌面专有的文案（vue-tsx-pro 没有对应）：自绘的窗口标题栏上的按钮。内置 <see cref="ZhCn"/>（默认）与 <see cref="EnUs"/>，
/// 组件按所在的语言包取对应的一份（<see cref="For"/>）；改几条用 <c>DesktopMessages.ZhCn with { WindowClose = "退出" }</c>。
/// </summary>
public sealed record DesktopMessages
{
    /// <summary>最小化窗口。</summary>
    public required string WindowMinimize { get; init; }

    /// <summary>最大化窗口。</summary>
    public required string WindowMaximize { get; init; }

    /// <summary>把最大化的窗口还原。</summary>
    public required string WindowRestore { get; init; }

    /// <summary>关闭窗口。</summary>
    public required string WindowClose { get; init; }

    /// <summary>简体中文（默认）。</summary>
    public static DesktopMessages ZhCn { get; } = new() { WindowMinimize = "最小化", WindowMaximize = "最大化", WindowRestore = "向下还原", WindowClose = "关闭" };

    /// <summary>美国英语。</summary>
    public static DesktopMessages EnUs { get; } = new() { WindowMinimize = "Minimize", WindowMaximize = "Maximize", WindowRestore = "Restore down", WindowClose = "Close" };

    /// <summary>语言包对应的一份：英文的语言包用 EnUs，其余用 ZhCn。</summary>
    /// <param name="lang">所在的语言包。</param>
    public static DesktopMessages For(Lang lang) => lang.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? EnUs : ZhCn;
}
