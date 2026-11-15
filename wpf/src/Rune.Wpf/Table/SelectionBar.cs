using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>
/// 批量操作栏（vue-tsx-pro 的 SelectionBar）：表格勾选了行之后出现在表格上方，“已选 N 项 · 取消选择”，结束边是批量操作。
/// <code>
/// &lt;rune:SelectionBar Count="{Binding SelectedCount}" ClearRequested="OnClearSelection"&gt;
///   &lt;rune:Button Size="sm" Variant="outline" Tone="neutral"&gt;批量导出&lt;/rune:Button&gt;
/// &lt;/rune:SelectionBar&gt;
/// </code>
/// accentSoft 底、md 圆角、控件高度，数目 semibold、accent、等宽数字；“取消选择”是 link 按钮，点了报告 <see cref="ClearRequested"/>（清空选择由使用方做）。
/// <see cref="Count"/> 为 0 时淡出后不占位（淡出期间仍显示最后的数目，不闪成“已选 0 项”），有选择时淡入。<see cref="Label"/> 换掉默认的数目文字（跨页全选时写“已选全部 1,286 项”）。
/// 数目变了经读屏的实时区域念出来。
/// </summary>
[ContentProperty(nameof(Actions))]
public class SelectionBar : CompositeControl
{
    /// <summary>已选几项；0 时不显示。</summary>
    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(nameof(Count), typeof(int), typeof(SelectionBar),
        new FrameworkPropertyMetadata(0, (d, _) => ((SelectionBar)d).Sync(announce: true)));

    /// <summary>已选数目的文字，默认取语言包（已选 N 项）。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(object), typeof(SelectionBar),
        new FrameworkPropertyMetadata(null, (d, _) => ((SelectionBar)d).Sync(announce: true)));

    /// <summary><see cref="ClearRequested"/> 的路由事件。</summary>
    public static readonly RoutedEvent ClearRequestedEvent = EventManager.RegisterRoutedEvent(nameof(ClearRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SelectionBar));

    readonly Border root = new();
    readonly ContentPresenter count = new();
    readonly Button clear = new() { Variant = ButtonVariant.Link, Size = TextSize.Sm };
    readonly Flex actions = new() { Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, Gap = Space.Sm, HorizontalAlignment = HorizontalAlignment.Right };
    readonly LiveRegion live = new();
    int shownCount;
    int hiding;

    static SelectionBar()
    {
        FocusableProperty.OverrideMetadata(typeof(SelectionBar), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(SelectionBar), new FrameworkPropertyMetadata(false));
    }

    /// <summary>新建：数目、取消选择、批量操作排成一行（放不下时换行）。</summary>
    public SelectionBar()
    {
        Flex.SetGrow(actions, true);
        var row = new Flex { Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, Gap = Space.Md };
        row.Children.Add(count);
        row.Children.Add(clear);
        row.Children.Add(actions);
        root.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.AccentSoft);
        root.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.Brush.Text);
        root.Child = new LayoutGrid { Children = { live, row } };
        clear.Click += (_, _) => RaiseEvent(new RoutedEventArgs(ClearRequestedEvent, this));
        Root = root;
        Visibility = Visibility.Collapsed;
        Loaded += (_, _) =>
        {
            clear.Content = Provider.GetLang(this).TableClearSelection;
            Sync(announce: false);
        };
    }

    /// <summary>已选几项。</summary>
    public int Count { get => (int)GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <summary>已选数目的文字。</summary>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>批量操作的按钮。</summary>
    public UIElementCollection Actions => actions.Children;

    /// <summary>点了“取消选择”：清空选择由使用方做。</summary>
    public event RoutedEventHandler ClearRequested
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

    /// <summary>
    /// 有选择时显示、淡入（md）；变成 0 时淡出后藏起来，淡出期间数目停在最后一次。数目或文字变了，读屏念出新的
    /// （给了文字的 Label 念它，否则念“已选 N 项”）。
    /// </summary>
    void Sync(bool announce)
    {
        var lang = Provider.GetLang(this);
        if (Count > 0) shownCount = Count;
        count.Content = Label is string or null ? new Text { Text = Label as string ?? lang.TableSelectedCount(shownCount), Weight = TextWeight.Semibold, Tone = TextTone.Accent, Numeric = true } : Label;
        if (announce && IsLoaded) live.Announce(Label as string ?? lang.TableSelectedCount(Count));
        var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
        var version = ++hiding;
        var visible = Count > 0;
        if (visible) Visibility = Visibility.Visible;
        BeginAnimation(OpacityProperty, null);
        if (duration > TimeSpan.Zero && IsLoaded)
            BeginAnimation(OpacityProperty, new DoubleAnimation(visible ? 1 : 0, duration));
        else Opacity = visible ? 1 : 0;
        if (visible) return;
        if (duration <= TimeSpan.Zero || !IsLoaded) Visibility = Visibility.Collapsed;
        else _ = HideAfter(version, duration);
    }

    /// <summary>淡出结束后藏起来（期间又有了选择就不藏）。</summary>
    async Task HideAfter(int version, TimeSpan duration)
    {
        await Task.Delay(duration);
        if (version == hiding) Visibility = Visibility.Collapsed;
    }

    /// <summary>md 圆角，最小高度是控件高度，内边距 xs × md；字号 sm，em 尺寸都按它算。</summary>
    protected override Size MeasureOverride(Size constraint)
    {
        var em = this.Resource<double>(ThemeKeys.FontSize.Sm);
        TextElement.SetFontSize(root, em);
        double Space(string key) => this.Resource<double>(key) * em;
        root.Padding = new Thickness(Space(ThemeKeys.Space.Md), Space(ThemeKeys.Space.Xs), Space(ThemeKeys.Space.Md), Space(ThemeKeys.Space.Xs));
        root.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Md) * em);
        root.MinHeight = ControlHeight.Of(em, ControlHeight.DefaultFactor);
        return base.MeasureOverride(constraint);
    }
}
