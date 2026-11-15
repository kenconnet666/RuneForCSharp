using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace Rune.Avalonia;

/// <summary>
/// 批量操作栏（vue-tsx-pro 的 SelectionBar）：表格勾选了行之后出现在表格上方，“已选 N 项 · 取消选择”，结束边是批量操作。
/// <code>
/// &lt;rune:SelectionBar Count="{Binding SelectedCount}" ClearRequested="OnClearSelection"&gt;
///   &lt;rune:Button Size="sm" Variant="outline" Tone="neutral"&gt;批量导出&lt;/rune:Button&gt;
/// &lt;/rune:SelectionBar&gt;
/// </code>
/// accentSoft 底、md 圆角、控件高度，数目 semibold、accentText、等宽数字；“取消选择”是 link 按钮，点了报告 <see cref="ClearRequested"/>（清空选择由使用方做）。
/// <see cref="Count"/> 为 0 时淡出后不占位（淡出期间仍显示最后的数目，不闪成“已选 0 项”），有选择时淡入。<see cref="Label"/> 换掉默认的数目文字（跨页全选时写“已选全部 1,286 项”）。
/// 数目变了经读屏的实时区域念出来。部件：<see cref="SelectionBarParts"/>。
/// </summary>
public class SelectionBar : Control
{
    /// <summary>已选几项；0 时不显示。</summary>
    public static readonly StyledProperty<int> CountProperty = AvaloniaProperty.Register<SelectionBar, int>(nameof(Count));

    /// <summary>已选数目的文字，默认取语言包（已选 N 项）。</summary>
    public static readonly StyledProperty<object?> LabelProperty = AvaloniaProperty.Register<SelectionBar, object?>(nameof(Label));

    /// <summary><see cref="ClearRequested"/> 的路由事件。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClearRequestedEvent = RoutedEvent.Register<SelectionBar, RoutedEventArgs>(nameof(ClearRequested), RoutingStrategies.Bubble);

    readonly Border root = new() { Name = SelectionBarParts.Root };
    readonly ContentPresenter count = new() { Name = SelectionBarParts.Count };
    readonly Button clear = new() { Name = SelectionBarParts.Clear, Variant = ButtonVariant.Link, Size = TextSize.Sm };
    readonly Flex actions = new() { Name = SelectionBarParts.Actions, Align = FlexAlign.Center, Wrap = true, Gap = Space.Sm };
    readonly LiveRegion live = new();
    int shownCount;
    int hiding;

    /// <summary>新建：数目、取消选择、批量操作排成一行（放不下时换行）。</summary>
    public SelectionBar()
    {
        Flex.SetGrow(actions, true);
        actions.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right;
        var row = new Flex { Align = FlexAlign.Center, Wrap = true, Gap = Space.Md, Children = { count, clear, actions } };
        root.Child = new Panel { Children = { live, row } };
        clear.Click += (_, _) => RaiseEvent(new RoutedEventArgs(ClearRequestedEvent, this));
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        IsVisible = false;
    }

    /// <summary>已选几项。</summary>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <summary>已选数目的文字。</summary>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>批量操作的按钮。</summary>
    [Content]
    public global::Avalonia.Controls.Controls Actions => actions.Children;

    /// <summary>点了“取消选择”：清空选择由使用方做。</summary>
    public event EventHandler<RoutedEventArgs>? ClearRequested
    {
        add => AddHandler(ClearRequestedEvent, value);
        remove => RemoveHandler(ClearRequestedEvent, value);
    }

    /// <summary>读屏最后念的话（测试用）。</summary>
    internal string? Announced => live.Text;

    /// <summary>数目的文字（测试用）。</summary>
    internal object? CountContent => count.Content;

    /// <summary>取消选择按钮（测试用）。</summary>
    internal Button ClearButton => clear;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        clear.Content = Provider.GetLang(this).TableClearSelection;
        Sync(announce: false);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty || change.Property == LabelProperty) Sync(announce: true);
    }

    /// <summary>
    /// 有选择时显示、淡入（md）；变成 0 时淡出后藏起来，淡出期间数目停在最后一次。数目或文字变了，读屏念出新的
    /// （给了文字的 Label 念它，否则念“已选 N 项”）。
    /// </summary>
    void Sync(bool announce)
    {
        var lang = Provider.GetLang(this);
        if (Count > 0) shownCount = Count;
        count.Content = Label is string or null ? new Text { Text = Label as string ?? lang.TableSelectedCount(shownCount), Weight = TextWeight.Semibold, Tone = TextTone.Accent, Numeric = true } : Label;
        if (announce && VisualRoot is not null) live.Announce(Label as string ?? lang.TableSelectedCount(Count));
        var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
        Transitions = duration > TimeSpan.Zero ? [new DoubleTransition { Property = OpacityProperty, Duration = duration }] : null;
        var version = ++hiding;
        if (Count > 0)
        {
            if (!IsVisible)
            {
                Opacity = 0;
                IsVisible = true;
            }
            Opacity = 1;
            return;
        }
        Opacity = 0;
        if (duration <= TimeSpan.Zero || VisualRoot is null) IsVisible = false;
        else _ = HideAfter(version, duration);
    }

    /// <summary>淡出结束后藏起来（期间又有了选择就不藏）。</summary>
    async Task HideAfter(int version, TimeSpan duration)
    {
        await Task.Delay(duration);
        if (version == hiding) IsVisible = false;
    }

    /// <summary>accentSoft 底、md 圆角，最小高度是控件高度，内边距 xs × md；字号 sm，em 尺寸都按它算。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var em = this.Resource<double>(ThemeKeys.FontSize.Sm);
        TextElement.SetFontSize(root, em);
        double Space(string key) => this.Resource<double>(key) * em;
        root.Padding = new Thickness(Space(ThemeKeys.Space.Md), Space(ThemeKeys.Space.Xs));
        root.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Md) * em);
        root.MinHeight = ControlHeight.Of(em, ControlHeight.DefaultFactor);
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>SelectionBar 的部件名：<c>rune|SelectionBar Border#PART_Root</c>。</summary>
public static class SelectionBarParts
{
    /// <summary>外层（底色、圆角）。</summary>
    public const string Root = "PART_Root";

    /// <summary>已选数目（TS 的 propsCount）。</summary>
    public const string Count = "PART_Count";

    /// <summary>取消选择（TS 的 propsClear）。</summary>
    public const string Clear = "PART_Clear";

    /// <summary>批量操作区（TS 的 propsActions）。</summary>
    public const string Actions = "PART_Actions";
}
