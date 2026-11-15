using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia;

/// <summary>
/// 滚动加载的列表末尾的状态（vue-tsx-pro 的 InfiniteListStatus，配合 <see cref="InfiniteList{T}"/>）：失败时是出错提示与“重试”，取完了是“没有更多了”，其余时候不占位。
/// <code>
/// &lt;rune:VirtualList ItemsSource="{Binding Orders.Items}" IsLoading="{Binding Orders.IsLoading}" … /&gt;
/// &lt;rune:InfiniteListStatus List="{Binding Orders}" /&gt;
/// </code>
/// 放在列表下方。加载中不在这里显示：列表自己在末尾接骨架。失败的文案是“加载失败，请重试”（上游按错误码取的文案桌面上没有：不把底层的英文说明露给用户）。
/// 读屏：一直是状态区域（礼貌的实时区域），内容变了会播报。部件名在 <see cref="InfiniteListStatusParts"/>。
/// </summary>
public class InfiniteListStatus : Control
{
    /// <summary>列表的状态。</summary>
    public static readonly StyledProperty<IInfiniteList?> ListProperty = AvaloniaProperty.Register<InfiniteListStatus, IInfiniteList?>(nameof(List));

    /// <summary>取完时的文字；不写时是语言包的“没有更多了”。</summary>
    public static readonly StyledProperty<string?> NoMoreTextProperty = AvaloniaProperty.Register<InfiniteListStatus, string?>(nameof(NoMoreText));

    /// <summary>取完时显示“没有更多了”，默认显示（上游的 noMore: false 是不显示）。</summary>
    public static readonly StyledProperty<bool> ShowNoMoreProperty = AvaloniaProperty.Register<InfiniteListStatus, bool>(nameof(ShowNoMore), true);

    /// <summary>字号：内容是它的 sm。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = global::Avalonia.Controls.Documents.TextElement.FontSizeProperty.AddOwner<InfiniteListStatus>();

    readonly Flex root = new() { Align = FlexAlign.Center, Justify = FlexJustify.Center, Wrap = true, Gap = Space.Sm };
    readonly Text error = new() { Tone = TextTone.Danger, Name = InfiniteListStatusParts.Error };
    readonly Text noMore = new() { Tone = TextTone.Muted, Name = InfiniteListStatusParts.NoMore };
    readonly Button retry = new() { Name = InfiniteListStatusParts.Retry, Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Icon = Lucide.RotateCw };
    IInfiniteList? watched;

    /// <summary>新建：没有内容时不占位。</summary>
    public InfiniteListStatus()
    {
        root.Children.AddRange([error, retry, noMore]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        retry.Click += (_, _) => _ = List?.LoadMore();
        Sync();
    }

    /// <summary>列表的状态。</summary>
    public IInfiniteList? List { get => GetValue(ListProperty); set => SetValue(ListProperty, value); }

    /// <summary>取完时的文字。</summary>
    public string? NoMoreText { get => GetValue(NoMoreTextProperty); set => SetValue(NoMoreTextProperty, value); }

    /// <summary>取完时显示“没有更多了”。</summary>
    public bool ShowNoMore { get => GetValue(ShowNoMoreProperty); set => SetValue(ShowNoMoreProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>出错提示、重试按钮、没有更多（测试用）。</summary>
    internal (Text Error, Button Retry, Text NoMore) Parts => (error, retry, noMore);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ListProperty)
        {
            if (watched is not null) watched.PropertyChanged -= OnListChanged;
            watched = List;
            if (watched is not null) watched.PropertyChanged += OnListChanged;
            Sync();
        }
        else if (change.Property == NoMoreTextProperty || change.Property == ShowNoMoreProperty || change.Property == FontSizeProperty) Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(global::Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Sync();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new StatusPeer(this);

    void OnListChanged(object? sender, PropertyChangedEventArgs e) => Sync();

    /// <summary>失败时是提示与重试；取完了（有项、不在加载）是“没有更多了”；其余时候空着、不占位（有内容时至少 2.75em 高，上下 xs、左右 md）。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        var list = List;
        var failed = list?.Error is not null;
        var done = !failed && ShowNoMore && list is { IsLoading: false, IsLoaded: true, HasMore: false, Count: > 0 };
        error.Text = lang.ListLoadFailed;
        retry.Content = lang.ErrorRetry;
        noMore.Text = NoMoreText ?? lang.ListNoMore;
        error.IsVisible = retry.IsVisible = failed;
        noMore.IsVisible = done;
        var em = FontSize;
        var sm = em * this.Resource<double>(ThemeKeys.FontSize.Sm) / this.Resource<double>(ThemeKeys.FontSize.Md);
        root.SetValue(global::Avalonia.Controls.Documents.TextElement.FontSizeProperty, sm);
        var shown = failed || done;
        root.MinHeight = shown ? 2.75 * sm : 0;
        root.Margin = shown ? new Thickness(this.Resource<double>(ThemeKeys.Space.Md) * sm, this.Resource<double>(ThemeKeys.Space.Xs) * sm) : default;
        // 读屏念出变化：名称跟着显示的内容
        AutomationProperties.SetName(this, failed ? lang.ListLoadFailed : done ? noMore.Text : null);
    }

    sealed class StatusPeer(InfiniteListStatus owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>InfiniteListStatus 的部件名。</summary>
public static class InfiniteListStatusParts
{
    /// <summary>出错提示。</summary>
    public const string Error = "PART_Error";

    /// <summary>重试按钮。</summary>
    public const string Retry = "PART_Retry";

    /// <summary>没有更多了。</summary>
    public const string NoMore = "PART_NoMore";
}
