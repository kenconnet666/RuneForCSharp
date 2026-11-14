using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 气泡确认（vue-tsx-pro 的 Popconfirm，role="alertdialog"）：在触发按钮旁边问一句再执行（移出成员、撤回、清空），比确认对话框轻。
/// <code>
/// var remove = new PopconfirmControl
/// {
///     Trigger = new IconButtonControl { Icon = Lucide.Trash, AccessibleName = "删除" },
///     Title = "删除这条记录？", Description = "删除后无法恢复", Tone = StatusTone.Danger, ConfirmText = "删除",
///     Confirm = async () => { await api.RemoveAsync(row.Id); return true; },
/// };
/// </code>
/// 建在 <see cref="PopoverControl"/> 上（默认在上方）：点外面、Esc 等于取消，关上后焦点回到触发器；打开时焦点落在"取消"上，误按 Enter 不会执行。
/// <see cref="Confirm"/> 的任务进行中确认按钮加载、取消不可用，完成后关闭；结果为 false 或失败时保持打开，失败原因由调用方提示。
/// 取消（取消按钮、点外面、Esc）发 <see cref="Canceled"/>；确认后关闭的不算。图标 <see cref="Icon"/> 不写时按 <see cref="Tone"/> 选，写 null 不显示。
/// 浮层窗口以问题为读屏名称、说明为描述。后果严重、要多写几句或填理由的用 <see cref="AlertDialogControl"/>。
/// </summary>
[DefaultEvent(nameof(Canceled))]
public class PopconfirmControl : PopoverControl
{
    readonly IconControl icon = new();
    readonly TextControl title = new() { Weight = TextWeight.Medium };
    readonly SmallControl description = new() { Tone = TextTone.Muted };
    readonly FlexControl footer = new() { Justify = FlexJustify.End, Gap = Space.Sm };
    readonly ButtonControl cancel = new() { TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral };
    readonly ButtonControl confirm = new() { TextSize = TextSize.Sm };
    bool iconSet;
    bool confirming;
    // 确认后关闭的不算取消
    bool confirmed;

    /// <summary>新建：图标、问题、说明与"取消 / 确认"，在上方弹出。</summary>
    public PopconfirmControl()
    {
        Placement = FloatingPlacement.Top;
        footer.Controls.AddRange([cancel, confirm]);
        // 图标与文字一行（隔 space.sm），问题与说明一列（隔 space.xs）；按钮行在下面，与文字隔 space.xs + space.sm（= space.md）
        var text = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Controls = { title, description } };
        var row = new FlexControl { Align = FlexAlign.Start, Gap = Space.Sm, Controls = { icon, text } };
        row.SetGrow(text, true);
        Content = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Controls = { row, footer } };
        cancel.Click += (_, _) => SetOpen(false, OpenChangeReason.Close);
        confirm.Click += (_, _) => _ = RunConfirm();
        Sync();
    }

    /// <summary>问题，同时是浮层的读屏名称。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Title { get; set { field = value; Sync(); } }

    /// <summary>补充说明（读屏的描述）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Description { get; set { field = value; Sync(); } }

    /// <summary>确认按钮与图标的色调，默认 warning；删除这类破坏性操作用 danger。</summary>
    [Category("Appearance")]
    public StatusTone Tone { get => field.Value is null ? StatusTone.Warning : field; set { field = value; Sync(); } }

    /// <summary>图标；不写时按色调选，写 null 不显示。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconData? Icon
    {
        get;
        set
        {
            field = value;
            iconSet = true;
            Sync();
        }
    }

    /// <summary>确认时做的事：任务完成后关闭；结果为 false 或失败时保持打开。不写时点了就关。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Task<bool>>? Confirm { get; set; }

    /// <summary>确认按钮的文字；不写时取语言包（确定）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? ConfirmText { get; set { field = value; Sync(); } }

    /// <summary>取消按钮的文字；不写时取语言包（取消）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? CancelText { get; set { field = value; Sync(); } }

    bool ShouldSerializeTone() => Tone != StatusTone.Warning;

    /// <summary>取消了：取消按钮、点外面、Esc 关闭（确认后关闭的不算；确认进行中被关掉时，等结果出来、没确认成才算）。</summary>
    public event EventHandler? Canceled;

    /// <summary>取消、确认两个按钮（测试用）。</summary>
    internal (ButtonControl Cancel, ButtonControl Confirm) Actions => (cancel, confirm);

    private protected override string? SurfaceName => Title;

    // 最宽 20rem（按根字号 16 换算）
    private protected override int? MaxPanelWidth(float em) => (int)MathF.Round(320 * DeviceDpi / 96f);

    private protected override void OnOpenChanged(bool open)
    {
        if (open)
        {
            confirmed = false;
            // 打开时焦点落在取消上（内容窗口激活后默认聚焦第一个能聚焦的控件，这里明确一下）：误按 Enter 不会执行
            cancel.Focus();
        }
        // 确认进行中被 Esc、点外面关掉：结果出来之前不算取消
        else if (!confirmed && !confirming) Canceled?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 按钮的默认文字取所在位置的语言包
        Sync();
    }

    /// <summary>文字、图标与按钮；没写的部分不占位置。</summary>
    void Sync()
    {
        var lang = ProviderControl.LangOf(this);
        var shown = iconSet ? Icon : AlertControl.IconFor(Tone);
        icon.Data = shown;
        icon.Visible = shown is not null;
        icon.Tone = new TextTone(Tone.Value);
        // 图标贴着第一行文字的顶上，往下挪 0.15em 对齐字面
        icon.Margin = new Padding(0, (int)MathF.Round(0.15f * this.EmPixels()), 0, 0);
        title.Text = Title ?? "";
        description.Text = Description ?? "";
        description.Visible = !string.IsNullOrEmpty(Description);
        Surface.AccessibleDescription = Description;
        cancel.Text = CancelText ?? lang.Cancel;
        confirm.Text = ConfirmText ?? lang.Confirm;
        confirm.Tone = Tone;
        cancel.Enabled = !confirming;
        confirm.IsLoading = confirming;
    }

    /// <summary>确认：进行中按钮加载、取消不可用；结果为 false 或失败时保持打开（调用方负责提示原因），否则关闭。</summary>
    async Task RunConfirm()
    {
        if (confirming) return;
        confirming = true;
        Sync();
        bool done;
        try
        {
            done = Confirm is not { } action || await action();
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceWarning($"Popconfirm 的 Confirm 失败，保持打开：{error}");
            done = false;
        }
        finally
        {
            confirming = false;
            Sync();
        }
        if (!done)
        {
            // 没确认成，浮层又已经在等的时候关掉了：这时才算取消
            if (!IsOpen) Canceled?.Invoke(this, EventArgs.Empty);
            return;
        }
        confirmed = true;
        SetOpen(false, OpenChangeReason.Confirm);
    }
}
