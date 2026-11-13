using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf;

/// <summary>
/// 确认对话框（vue-tsx-pro 的 AlertDialog，role="alertdialog"）：打断当前操作、要用户明确选择"确认"或"取消"，如删除前的确认。
/// <code>
/// &lt;rune:AlertDialog IsOpen="{Binding Confirming}" Tone="danger" Title="删除这个项目？" Description="删除后无法恢复。" ConfirmText="删除" Confirm="{Binding Remove}" /&gt;
/// </code>
/// 建在 <see cref="Dialog"/> 上：没有右上角的关闭按钮，点遮罩不关（Esc 仍然等于取消），宽度默认 xs；没写 Confirm 也有确认按钮（点了就关）。
/// 打开时焦点落在"取消"上，误按 Enter 不会执行破坏性操作。Confirm 的任务进行中确认按钮加载、取消与 Esc 暂不可用，结果为 false 或失败时保持打开。
/// 只想在一个操作前问一句时用 <see cref="ConfirmAsync(FrameworkElement, ConfirmOptions)"/>（TS 的 useConfirm），不必自己放对话框、管开关。
/// </summary>
public class AlertDialog : Dialog
{
    /// <summary>新建：没有关闭按钮、点遮罩不关；确认按钮默认点了就关。</summary>
    public AlertDialog()
    {
        Closable = false;
        Dismissible = false;
        Confirm = () => Task.FromResult(true);
    }

    /// <summary>焦点先落在哪里：不写时是取消按钮（要填理由时 <see cref="ConfirmAsync(FrameworkElement, ConfirmOptions, ConfirmReason)"/> 换成输入框）。</summary>
    internal UIElement? FocusFirst { get; set; }

    /// <inheritdoc />
    private protected override UIElement? InitialFocus => FocusFirst ?? Actions.Cancel;

    /// <summary>
    /// 命令式确认（TS 的 useConfirm）：在 <paramref name="owner"/> 所在的窗口问一句，确认（Confirm 完成）得到 true，取消、Esc 得到 false；
    /// owner 离开界面时对话框跟着关上，得到 false。
    /// <code>
    /// if (await AlertDialog.ConfirmAsync(button, new() { Title = "删除这个项目？", Tone = StatusTone.Danger, ConfirmText = "删除" })) Remove();
    /// </code>
    /// </summary>
    /// <param name="owner">在哪个窗口打开、沿用谁的设置（一般是点的那个按钮）。</param>
    /// <param name="options">标题、说明、色调、按钮文字与确认时做的事。</param>
    public static async Task<bool> ConfirmAsync(FrameworkElement owner, ConfirmOptions options) => await Ask(owner, options, null) is not null;

    /// <summary>
    /// 要填理由的确认（驳回、作废、退回）：对话框里多一个多行输入框，确认得到填的理由（去掉首尾空白），取消、Esc 得到 null。
    /// 必填没填时点确认提示"必填"、不关闭。
    /// </summary>
    /// <param name="owner">在哪个窗口打开、沿用谁的设置。</param>
    /// <param name="options">标题、说明、色调、按钮文字与确认时做的事（拿到理由）。</param>
    /// <param name="reason">输入框的标签、必填、占位文字、说明与字数上限。</param>
    public static Task<string?> ConfirmAsync(FrameworkElement owner, ConfirmOptions options, ConfirmReason reason) => Ask(owner, options, reason);

    static async Task<string?> Ask(FrameworkElement owner, ConfirmOptions options, ConfirmReason? reason)
    {
        var dialog = new AlertDialog
        {
            Title = options.Title,
            Description = options.Description,
            Tone = options.Tone,
            ConfirmText = options.ConfirmText,
            CancelText = options.CancelText,
        };
        if (options.Size is { } size) dialog.Size = size;
        Textarea? input = null;
        Field? field = null;
        if (reason is not null)
        {
            input = new Textarea { PlaceholderText = reason.Placeholder, MaxLength = reason.MaxLength ?? 0, ShowCount = reason.MaxLength is not null };
            field = new Field { Label = reason.Label, Description = reason.Description, IsRequired = reason.IsRequired, Content = input };
            // 要填理由时焦点先进输入框；没有理由时落在取消上
            dialog.FocusFirst = input;
        }
        dialog.Content = (options.Content, field) switch
        {
            (null, var only) => only,
            (var content, null) => content,
            (var content, var only) => new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { content as UIElement ?? new Paragraph { Text = content?.ToString() }, only } },
        };
        var text = () => input?.Text?.Trim() ?? "";
        var tried = false;
        var missing = () => reason?.IsRequired == true && text().Length == 0;
        // 必填没填：点确认后才提示，之后随输入更新
        void ShowError()
        {
            if (field is not null) field.Error = tried && missing() ? Provider.GetLang(owner).FormRequired : null;
        }
        if (input is not null) input.TextChanged += (_, _) => ShowError();
        var confirmed = false;
        dialog.Confirm = async () =>
        {
            tried = true;
            ShowError();
            if (missing()) return false;
            if (options.Confirm is { } action && !await action(text())) return false;
            confirmed = true;
            return true;
        };
        await dialog.ShowAsync(owner);
        return confirmed ? text() : null;
    }
}

/// <summary><see cref="AlertDialog.ConfirmAsync(FrameworkElement, ConfirmOptions)"/> 的选项（TS 的 ConfirmOptions：AlertDialog 的属性，开关由它管）。</summary>
public sealed record ConfirmOptions
{
    /// <summary>标题（读屏的名称）。</summary>
    public required string Title { get; init; }

    /// <summary>说明（muted）。</summary>
    public string? Description { get; init; }

    /// <summary>正文：说明下面的内容（文字或控件）。</summary>
    public object? Content { get; init; }

    /// <summary>确认按钮的色调，默认 accent；破坏性操作写 danger。</summary>
    public StatusTone Tone { get; init; } = StatusTone.Accent;

    /// <summary>确认按钮的文字；不写时取语言包的"确定"。</summary>
    public string? ConfirmText { get; init; }

    /// <summary>取消按钮的文字；不写时取语言包的"取消"。</summary>
    public string? CancelText { get; init; }

    /// <summary>宽度档位；不写时 xs。</summary>
    public DialogSize? Size { get; init; }

    /// <summary>确认时做的事：拿到填的理由（没给理由时是空字符串）；结果为 false 或失败时保持打开，可以再试或取消。</summary>
    public Func<string, Task<bool>>? Confirm { get; init; }
}

/// <summary>确认时要填的理由（TS 的 ConfirmReason）。</summary>
public sealed record ConfirmReason
{
    /// <summary>输入框的标签（"驳回理由"）。</summary>
    public required string Label { get; init; }

    /// <summary>必填：空着点确认时提示"必填"、不关闭。</summary>
    public bool IsRequired { get; init; }

    /// <summary>输入框的占位文字。</summary>
    public string? Placeholder { get; init; }

    /// <summary>输入框下的说明（"申请人会收到这段理由"）。</summary>
    public string? Description { get; init; }

    /// <summary>最多几个字，显示字数。</summary>
    public int? MaxLength { get; init; }
}
