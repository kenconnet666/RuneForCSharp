using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// ItemList：列表与条目的结构（起始、标题、说明、结束；没有起始边时不留第一列）；整条可点（地址是链接、Invoked 是按钮）、选中、不可用；
/// 点说明等于点这一条、结束边的按钮只触发按钮；分隔线、疏密与两侧内边距。
/// </summary>
public class ItemListTests
{
    static T Template<T>(ItemListItem item, string name) where T : Control => item.GetVisualDescendants().OfType<T>().First(part => part.Name == name);

    [Fact]
    [Upstream("ItemList", "ul > li：起始、标题、说明、结束四块；没有 start 时不留第一列")]
    [Upstream("ItemList", "ItemList：列表是 root，条目是 item（不同组件不共用 root）")]
    public Task Items_have_start_title_description_and_end() => HeadlessUi.Run(() =>
    {
        var full = new ItemListItem { Start = new StatusDot(), Title = "资料审核", Description = "PO-2026-0187", End = "剩 2 天" };
        var plain = new ItemListItem { Title = "方案设计" };
        var list = new ItemList { Width = 320, Children = { full, plain } };
        HeadlessUi.Show(list);

        Assert.Equal(AutomationControlType.List, list.Peer().GetAutomationControlType());
        Assert.Equal(AutomationControlType.ListItem, full.Peer().GetAutomationControlType());
        Assert.Equal(("资料审核", "PO-2026-0187"), (full.Peer().GetName(), full.Peer().GetHelpText()));
        Assert.False(full.Focusable); // 不可点的条目不能聚焦
        Assert.True(Template<ContentPresenter>(full, ItemListItemParts.Start).IsVisible);
        Assert.False(Template<ContentPresenter>(plain, ItemListItemParts.Start).IsVisible); // 没有 start 时不留第一列
        Assert.False(Template<ContentPresenter>(plain, ItemListItemParts.Description).IsVisible);
        Assert.Equal("资料审核", Template<ContentPresenter>(full, ItemListItemParts.Title).Content); // 标题是部件（TS 的 data-part="title"）
        // 说明 textMuted、xs
        var description = Template<ContentPresenter>(full, ItemListItemParts.Description);
        Assert.Equal(full.ThemeColor("Rune.Brush.TextMuted"), Lookup.ColorOf(description.Foreground));
        Assert.True(full.TryFindResource("Rune.FontSize.Xs", full.ActualThemeVariant, out var xs));
        Assert.Equal(xs, description.FontSize);
    });

    [Fact]
    [Upstream("ItemList", "整条可点：href 时标题是链接、onSelect 时是按钮；selected 带 aria-current；disabled 的按钮点不了")]
    public Task Whole_items_click_as_links_or_buttons() => HeadlessUi.Run(() =>
    {
        var picked = new List<string>();
        var link = new ItemListItem { Title = "收件箱", Href = new Uri("https://example.com/inbox"), IsSelected = true };
        var drafts = new ItemListItem { Title = "草稿" };
        drafts.Invoked += (_, _) => picked.Add("草稿");
        var archived = new ItemListItem { Title = "归档", IsEnabled = false };
        archived.Invoked += (_, _) => picked.Add("归档");
        HeadlessUi.Show(new ItemList { Children = { link, drafts, archived } });

        Assert.Equal(AutomationControlType.Hyperlink, link.Peer().GetAutomationControlType());
        Assert.Equal("当前", link.Peer().GetItemStatus());
        Assert.Contains(":selected", link.Classes);
        Assert.Equal(AutomationControlType.Button, drafts.Peer().GetAutomationControlType());
        Assert.True(drafts.Focusable);
        ((IInvokeProvider)drafts.Peer()).Invoke();
        Assert.False(archived.Peer().IsEnabled());
        Assert.Equal(["草稿"], picked);
    });

    [Fact]
    // 上游是浏览器测试（ItemList.browser.test.tsx）：点说明选中这一条；点结束边的按钮只触发按钮
    public Task Clicking_anywhere_selects_but_end_buttons_only_act_themselves() => HeadlessUi.Run(() =>
    {
        var log = new List<string>();
        var remove = new Button { Content = "删除" };
        remove.Click += (_, _) => log.Add("删除");
        var item = new ItemListItem { Title = "资料审核", Description = "PO-2026-0187", End = remove };
        item.Invoked += (_, _) => log.Add("选中");
        var window = HeadlessUi.Show(new ItemList { Width = 480, Children = { item } });

        void Click(Visual target)
        {
            var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
        }
        Click(Template<ContentPresenter>(item, ItemListItemParts.Description));
        Click(remove);
        Assert.Equal(["选中", "删除"], log);
    });

    [Fact]
    public Task Dividers_density_and_inset_shape_the_items() => HeadlessUi.Run(() =>
    {
        var first = new ItemListItem { Title = "一" };
        var last = new ItemListItem { Title = "二" };
        var list = new ItemList { Divided = true, Children = { first, last } };
        var window = HeadlessUi.Show(list);
        var em = first.FontSize;

        Assert.Equal(new Thickness(0, 0, 0, 1), first.BorderThickness); // 条目之间一道线，最后一条下面没有
        Assert.Equal(new Thickness(0), last.BorderThickness);
        Assert.Equal(new Thickness(0, 0.5 * em), first.Padding); // 标准：上下 sm，不可点时两侧不留

        list.Density = ItemListDensity.Compact;
        list.Hoverable = true;
        HeadlessUi.Flush(window);
        Assert.Equal(new Thickness(0.5 * em, 0.25 * em), first.Padding); // 紧凑：上下 xs；悬停着底色时两侧 sm

        list.Inset = Space.Md;
        HeadlessUi.Flush(window);
        Assert.True(first.TryFindResource("Rune.Space.Md", first.ActualThemeVariant, out var md));
        Assert.Equal((double)md! * em, first.Padding.Left); // 写了 Inset 按它
    });
}
