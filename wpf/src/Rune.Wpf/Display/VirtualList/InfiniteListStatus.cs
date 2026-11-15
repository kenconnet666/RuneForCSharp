using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Documents;

namespace Rune.Wpf;

/// <summary>
/// 滚动加载的列表末尾的状态（vue-tsx-pro 的 InfiniteListStatus，配合 <see cref="InfiniteList{T}"/>）：失败时是出错提示与“重试”，取完了是“没有更多了”，其余时候不占位。
/// <code>
/// &lt;rune:VirtualList ItemsSource="{Binding Orders.Items}" IsLoading="{Binding Orders.IsLoading}" … /&gt;
/// &lt;rune:InfiniteListStatus List="{Binding Orders}" /&gt;
/// </code>
/// 放在列表下方。加载中不在这里显示：列表自己在末尾接骨架。失败的文案是“加载失败，请重试”（上游按错误码取的文案桌面上没有：不把底层的英文说明露给用户）。
/// 读屏：一直是状态区域（礼貌的实时区域），内容变了会播报。
/// </summary>
public class InfiniteListStatus : CompositeControl
{
    /// <summary>列表的状态。</summary>
    public static readonly DependencyProperty ListProperty = DependencyProperty.Register(nameof(List), typeof(IInfiniteList), typeof(InfiniteListStatus),
        new PropertyMetadata(null, (sender, e) => ((InfiniteListStatus)sender).Watch((IInfiniteList?)e.OldValue, (IInfiniteList?)e.NewValue)));

    /// <summary>取完时的文字；不写时是语言包的“没有更多了”。</summary>
    public static readonly DependencyProperty NoMoreTextProperty = DependencyProperty.Register(nameof(NoMoreText), typeof(string), typeof(InfiniteListStatus),
        new PropertyMetadata(null, (sender, _) => ((InfiniteListStatus)sender).Sync()));

    /// <summary>取完时显示“没有更多了”，默认显示（上游的 noMore: false 是不显示）。</summary>
    public static readonly DependencyProperty ShowNoMoreProperty = DependencyProperty.Register(nameof(ShowNoMore), typeof(bool), typeof(InfiniteListStatus),
        new PropertyMetadata(true, (sender, _) => ((InfiniteListStatus)sender).Sync()));

    readonly Flex root = new() { Align = FlexAlign.Center, Justify = FlexJustify.Center, Wrap = true, Gap = Space.Sm };
    readonly Text error = new() { Tone = TextTone.Danger };
    readonly Text noMore = new() { Tone = TextTone.Muted };
    readonly Button retry = new() { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Icon = Lucide.RotateCw };
    string? announced;

    /// <summary>新建：没有内容时不占位。</summary>
    public InfiniteListStatus()
    {
        root.Children.Add(error);
        root.Children.Add(retry);
        root.Children.Add(noMore);
        Root = root;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        retry.Click += (_, _) => _ = List?.LoadMore();
        Loaded += (_, _) => Sync();
        Sync();
    }

    /// <summary>列表的状态。</summary>
    public IInfiniteList? List { get => (IInfiniteList?)GetValue(ListProperty); set => SetValue(ListProperty, value); }

    /// <summary>取完时的文字。</summary>
    public string? NoMoreText { get => (string?)GetValue(NoMoreTextProperty); set => SetValue(NoMoreTextProperty, value); }

    /// <summary>取完时显示“没有更多了”。</summary>
    public bool ShowNoMore { get => (bool)GetValue(ShowNoMoreProperty); set => SetValue(ShowNoMoreProperty, value); }

    /// <summary>出错提示、重试按钮、没有更多（测试用）。</summary>
    internal (Text Error, Button Retry, Text NoMore) Parts => (error, retry, noMore);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new StatusPeer(this);

    void Watch(IInfiniteList? previous, IInfiniteList? next)
    {
        if (previous is not null) previous.PropertyChanged -= OnListChanged;
        if (next is not null) next.PropertyChanged += OnListChanged;
        Sync();
    }

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
        error.Visibility = retry.Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
        noMore.Visibility = done ? Visibility.Visible : Visibility.Collapsed;
        var sm = FontSize * this.Resource<double>(ThemeKeys.FontSize.Sm) / this.Resource<double>(ThemeKeys.FontSize.Md);
        TextElement.SetFontSize(root, sm);
        var shown = failed || done;
        root.MinHeight = shown ? 2.75 * sm : 0;
        var (md, xs) = (this.Resource<double>(ThemeKeys.Space.Md) * sm, this.Resource<double>(ThemeKeys.Space.Xs) * sm);
        root.Margin = shown ? new Thickness(md, xs, md, xs) : default;
        // 读屏念出变化：名称跟着显示的内容，变了发 LiveRegionChanged
        var text = failed ? lang.ListLoadFailed : done ? noMore.Text : null;
        AutomationProperties.SetName(this, text ?? "");
        if (text == announced) return;
        announced = text;
        if (text is not null && IsLoaded) (UIElementAutomationPeer.FromElement(this) ?? UIElementAutomationPeer.CreatePeerForElement(this))?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    sealed class StatusPeer(InfiniteListStatus owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetClassNameCore() => nameof(InfiniteListStatus);
    }
}
