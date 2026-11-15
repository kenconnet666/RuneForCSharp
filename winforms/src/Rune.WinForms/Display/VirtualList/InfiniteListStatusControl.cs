using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 滚动加载的列表末尾的状态（vue-tsx-pro 的 InfiniteListStatus，配合 <see cref="InfiniteList{T}"/>）：失败时是出错提示与“重试”，取完了是“没有更多了”，其余时候不占位。
/// <code>
/// var status = new InfiniteListStatusControl { List = orders, Dock = DockStyle.Top };
/// </code>
/// 放在列表下方。加载中不在这里显示：列表自己在末尾接骨架。失败的文案是“加载失败，请重试”（上游按错误码取的文案桌面上没有：不把底层的英文说明露给用户）。
/// 读屏：内容变了时发通知念出来（上游的 role="status"）。
/// </summary>
[Description("滚动加载的列表末尾的状态：失败时重试，取完了提示没有更多。")]
public class InfiniteListStatusControl : FlexControl
{
    readonly TextControl error = new() { Tone = TextTone.Danger, TextSize = TextSize.Sm };
    readonly ButtonControl retry = new() { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Icon = Lucide.RotateCw };
    readonly TextControl noMore = new() { Tone = TextTone.Muted, TextSize = TextSize.Sm };
    string? announced;

    /// <summary>新建：没有内容时不占位。</summary>
    public InfiniteListStatusControl()
    {
        Align = FlexAlign.Center;
        Justify = FlexJustify.Center;
        Wrap = true;
        Gap = Space.Sm;
        AccessibleRole = AccessibleRole.StaticText;
        Controls.AddRange([error, retry, noMore]);
        retry.Click += (_, _) => _ = List?.LoadMore();
        Sync();
    }

    /// <summary>列表的状态。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IInfiniteList? List
    {
        get;
        set
        {
            if (field is not null) field.PropertyChanged -= OnListChanged;
            field = value;
            if (value is not null) value.PropertyChanged += OnListChanged;
            Sync();
        }
    }

    /// <summary>取完时的文字；不写时是语言包的“没有更多了”。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? NoMoreText { get; set { field = value; Sync(); } }

    /// <summary>取完时显示“没有更多了”，默认显示（上游的 noMore: false 是不显示）。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool ShowNoMore { get; set { field = value; Sync(); } } = true;

    /// <summary>出错提示、重试按钮、没有更多（测试用）。</summary>
    internal (TextControl Error, ButtonControl Retry, TextControl NoMore) Parts => (error, retry, noMore);

    void OnListChanged(object? sender, PropertyChangedEventArgs e) => Sync();

    /// <summary>失败时是提示与重试；取完了（有项、不在加载）是“没有更多了”；其余时候空着、不占位（有内容时至少 2.75em 高，上下 xs、左右 md，按 sm 的字号）。</summary>
    void Sync()
    {
        var lang = ProviderControl.LangOf(this);
        var list = List;
        var failed = list?.Error is not null;
        var done = !failed && ShowNoMore && list is { IsLoading: false, IsLoaded: true, HasMore: false, Count: > 0 };
        error.Text = lang.ListLoadFailed;
        retry.Text = lang.ErrorRetry;
        noMore.Text = NoMoreText ?? lang.ListNoMore;
        error.Visible = retry.Visible = failed;
        noMore.Visible = done;
        var sm = this.EmPixels() * Tokens.FontSize.Sm / Tokens.FontSize.Md;
        var shown = failed || done;
        MinimumSize = new Size(0, shown ? (int)MathF.Round(2.75f * sm) : 0);
        var (md, xs) = ((int)MathF.Round(Tokens.Space.Md * sm), (int)MathF.Round(Tokens.Space.Xs * sm));
        Padding = shown ? new Padding(md, xs, md, xs) : Padding.Empty;
        var text = failed ? lang.ListLoadFailed : done ? noMore.Text : null;
        AccessibleName = text;
        if (text == announced) return;
        announced = text;
        if (text is not null && IsHandleCreated)
            AccessibilityObject.RaiseAutomationNotification(System.Windows.Forms.Automation.AutomationNotificationKind.Other,
                System.Windows.Forms.Automation.AutomationNotificationProcessing.ImportantMostRecent, text);
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Sync();
    }
}
