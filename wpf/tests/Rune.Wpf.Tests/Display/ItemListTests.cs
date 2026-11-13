using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>
/// ItemList：列表与条目的结构（起始、标题、说明、结束；没有起始边时不留第一列）；整条可点（地址是链接、Invoked 是按钮）、选中、不可用；
/// 点说明等于点这一条、结束边的按钮只触发按钮；分隔线、疏密与两侧内边距。
/// </summary>
public class ItemListTests
{
    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel { Width = 480 };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(child);
        return host;
    }

    static T Part<T>(ItemListItem item, string name) where T : FrameworkElement => (T)item.Template.FindName(name, item);

    [Fact]
    [Upstream("ItemList", "ul > li：起始、标题、说明、结束四块；没有 start 时不留第一列")]
    [Upstream("ItemList", "ItemList：列表是 root，条目是 item（不同组件不共用 root）")]
    public Task Items_have_start_title_description_and_end() => WpfUi.Run(() =>
    {
        var full = new ItemListItem { Start = new StatusDot(), Title = "资料审核", Description = "PO-2026-0187", End = "剩 2 天" };
        var plain = new ItemListItem { Title = "方案设计" };
        var list = new ItemList { Children = { full, plain } };
        WpfUi.Show(Host(list));

        Assert.Equal(AutomationControlType.List, list.Peer().GetAutomationControlType());
        Assert.Equal(AutomationControlType.ListItem, full.Peer().GetAutomationControlType());
        Assert.Equal(("资料审核", "PO-2026-0187"), (full.Peer().GetName(), full.Peer().GetHelpText()));
        Assert.False(full.Focusable); // 不可点的条目不能聚焦
        Assert.True(Part<ContentPresenter>(full, "PART_Start").IsVisible);
        Assert.False(Part<ContentPresenter>(plain, "PART_Start").IsVisible); // 没有 start 时不留第一列
        Assert.False(Part<ContentPresenter>(plain, "PART_Description").IsVisible);
        Assert.Same(full.FindResource(ItemListItemParts.Title), Part<ContentPresenter>(full, "PART_Title").Style); // 标题是部件，有部件样式（TS 的 data-part="title"）
        // 说明 textMuted、xs；标题从起始边后 space.sm 开始
        var description = Part<ContentPresenter>(full, "PART_Description");
        Assert.Equal(((SolidColorBrush)full.FindResource("Rune.Brush.TextMuted")).Color, ((SolidColorBrush)TextElement.GetForeground(description)).Color);
        Assert.Equal((double)full.FindResource("Rune.FontSize.Xs"), TextElement.GetFontSize(description));
        var start = Part<ContentPresenter>(full, "PART_Start");
        var title = Part<ContentPresenter>(full, "PART_Title");
        Assert.Equal(start.ActualWidth + 0.5 * full.FontSize, title.TranslatePoint(default, full).X - start.TranslatePoint(default, full).X, 0.5);
    });

    [Fact]
    [Upstream("ItemList", "整条可点：href 时标题是链接、onSelect 时是按钮；selected 带 aria-current；disabled 的按钮点不了")]
    public Task Whole_items_click_as_links_or_buttons() => WpfUi.Run(() =>
    {
        var picked = new List<string>();
        var link = new ItemListItem { Title = "收件箱", Href = new Uri("https://example.com/inbox"), IsSelected = true };
        var drafts = new ItemListItem { Title = "草稿" };
        drafts.Invoked += (_, _) => picked.Add("草稿");
        var archived = new ItemListItem { Title = "归档", IsEnabled = false };
        archived.Invoked += (_, _) => picked.Add("归档");
        WpfUi.Show(Host(new ItemList { Children = { link, drafts, archived } }));

        Assert.Equal(AutomationControlType.Hyperlink, link.Peer().GetAutomationControlType());
        Assert.Equal("当前", link.Peer().GetItemStatus());
        Assert.Equal(((SolidColorBrush)link.FindResource("Rune.Brush.AccentSoft")).Color, ((SolidColorBrush)link.Background).Color);
        Assert.Equal(AutomationControlType.Button, drafts.Peer().GetAutomationControlType());
        Assert.True(drafts.Focusable);
        ((IInvokeProvider)drafts.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
        Assert.False(archived.Peer().IsEnabled());
        Assert.Equal(["草稿"], picked);
    });

    [Fact]
    // 上游是浏览器测试（ItemList.browser.test.tsx）：点说明选中这一条；点结束边的按钮只触发按钮
    public Task Clicking_anywhere_selects_but_end_buttons_only_act_themselves() => WpfUi.Run(() =>
    {
        var log = new List<string>();
        var remove = new Button { Content = "删除" };
        remove.Click += (_, _) => log.Add("删除");
        var item = new ItemListItem { Title = "资料审核", Description = "PO-2026-0187", End = remove };
        item.Invoked += (_, _) => log.Add("选中");
        WpfUi.Show(Host(new ItemList { Children = { item } }));

        // 合成的鼠标事件按不下 WPF 的按钮（按下要按真实指针命中）：改看指针落在谁身上，再用读屏的 Invoke 点
        DependencyObject? HitAt(FrameworkElement target) =>
            VisualTreeHelper.HitTest(item, target.TranslatePoint(new Point(target.ActualWidth / 2, target.ActualHeight / 2), item))?.VisualHit;
        static object? Owner(DependencyObject? hit)
        {
            for (var node = hit; node is not null; node = VisualTreeHelper.GetParent(node))
                if (node is System.Windows.Controls.Primitives.ButtonBase button) return button;
            return null;
        }
        Assert.Same(item, Owner(HitAt(Part<ContentPresenter>(item, "PART_Description")))); // 点说明落在这一条上
        Assert.Same(remove, Owner(HitAt(remove))); // 结束边的按钮在上面，点它落在按钮上
        ((IInvokeProvider)item.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        ((IInvokeProvider)remove.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
        Assert.Equal(["选中", "删除"], log);
    });

    [Fact]
    public Task Dividers_density_and_inset_shape_the_items() => WpfUi.Run(() =>
    {
        var first = new ItemListItem { Title = "一" };
        var last = new ItemListItem { Title = "二" };
        var list = new ItemList { Divided = true, Children = { first, last } };
        WpfUi.Show(Host(list));
        var em = first.FontSize;

        Assert.Equal(new Thickness(0, 0, 0, 1), first.BorderThickness); // 条目之间一道线，最后一条下面没有
        Assert.Equal(new Thickness(0), last.BorderThickness);
        Assert.Equal(new Thickness(0, 0.5 * em, 0, 0.5 * em), first.Padding); // 标准：上下 sm，不可点时两侧不留

        list.Density = ItemListDensity.Compact;
        list.Hoverable = true;
        WpfUi.Flush();
        Assert.Equal(new Thickness(0.5 * em, 0.25 * em, 0.5 * em, 0.25 * em), first.Padding); // 紧凑：上下 xs；悬停着底色时两侧 sm

        list.Inset = Space.Md;
        WpfUi.Flush();
        Assert.Equal((double)first.FindResource("Rune.Space.Md") * em, first.Padding.Left); // 写了 Inset 按它
    });
}
