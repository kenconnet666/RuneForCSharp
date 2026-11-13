using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>
/// ItemListControl：列表与条目的结构（起始、标题、说明、结束；没有起始边时不留第一列）；整条可点（地址是链接、Invoked 是按钮）、选中、不可用；
/// 点说明等于点这一条、结束边的按钮只触发按钮；分隔线、疏密与两侧内边距。
/// </summary>
public class ItemListTests
{
    static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(child => Descendants(child).Prepend(child));

    static void Click(Control target) =>
        typeof(Control).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, [new MouseEventArgs(MouseButtons.Left, 1, 2, 2, 0)]);

    [Fact]
    [Upstream("ItemList", "ul > li：起始、标题、说明、结束四块；没有 start 时不留第一列")]
    public Task Items_have_start_title_description_and_end() => WinFormsUi.Run(() =>
    {
        var dot = new StatusDotControl();
        var full = new ItemListItemControl { Start = dot, Text = "资料审核", Description = "PO-2026-0187", End = new SmallControl { Text = "剩 2 天" } };
        var plain = new ItemListItemControl { Text = "方案设计" };
        var list = new ItemListControl { Width = 400, Controls = { full, plain } };
        using var form = WinFormsUi.Show(list);

        Assert.Equal(AccessibleRole.List, list.AccessibilityObject.Role);
        Assert.Equal(AccessibleRole.ListItem, full.AccessibilityObject.Role);
        Assert.Equal(("资料审核", "PO-2026-0187"), (full.AccessibilityObject.Name, full.AccessibilityObject.Description));
        Assert.False(full.TabStop); // 不可点的条目不能聚焦
        Assert.True(dot.Parent!.Visible);
        Assert.False(Descendants(plain).OfType<SmallControl>().Single().Visible); // 没有说明时不显示
        // 没有 start 时不留第一列：标题从内边距之后开始
        var plainTitle = Descendants(plain).OfType<TextControl>().Single();
        Assert.Equal(plain.Padding.Left, plain.PointToClient(plainTitle.PointToScreen(Point.Empty)).X);
        // 说明 textMuted、xs
        var description = Descendants(full).OfType<SmallControl>().First(control => control.Text == "PO-2026-0187");
        Assert.Equal((TextTone.Muted, TextSize.Xs), (description.Tone, description.TextSize));
    });

    [Fact]
    [Upstream("ItemList", "整条可点：href 时标题是链接、onSelect 时是按钮；selected 带 aria-current；disabled 的按钮点不了")]
    public Task Whole_items_click_as_links_or_buttons() => WinFormsUi.Run(() =>
    {
        var picked = new List<string>();
        var link = new ItemListItemControl { Text = "收件箱", Href = new Uri("https://example.com/inbox"), IsSelected = true };
        var drafts = new ItemListItemControl { Text = "草稿" };
        drafts.Invoked += (_, _) => picked.Add("草稿");
        var archived = new ItemListItemControl { Text = "归档", Enabled = false };
        archived.Invoked += (_, _) => picked.Add("归档");
        using var form = WinFormsUi.Show(new ItemListControl { Width = 400, Controls = { link, drafts, archived } });

        Assert.Equal(AccessibleRole.Link, link.AccessibilityObject.Role);
        Assert.True(link.AccessibilityObject.State.HasFlag(AccessibleStates.Selected));
        Assert.Equal(AccessibleRole.PushButton, drafts.AccessibilityObject.Role);
        Assert.True(drafts.TabStop);
        drafts.AccessibilityObject.DoDefaultAction();
        archived.PerformClick();
        Assert.Equal(["草稿"], picked);
    });

    [Fact]
    // 上游是浏览器测试（ItemList.browser.test.tsx）：点说明选中这一条；点结束边的按钮只触发按钮
    public Task Clicking_anywhere_selects_but_end_buttons_only_act_themselves() => WinFormsUi.Run(() =>
    {
        var log = new List<string>();
        var remove = new ButtonControl { Text = "删除" };
        remove.Click += (_, _) => log.Add("删除");
        var item = new ItemListItemControl { Text = "资料审核", Description = "PO-2026-0187", End = remove };
        item.Invoked += (_, _) => log.Add("选中");
        using var form = WinFormsUi.Show(new ItemListControl { Width = 400, Controls = { item } });

        Click(Descendants(item).OfType<SmallControl>().Single());
        remove.PerformClick(); // 结束边的控件不转发给这一条
        Assert.Equal(["选中", "删除"], log);
    });

    [Fact]
    public Task Dividers_density_and_inset_shape_the_items() => WinFormsUi.Run(() =>
    {
        var first = new ItemListItemControl { Text = "一" };
        var last = new ItemListItemControl { Text = "二" };
        var list = new ItemListControl { Width = 400, Divided = true, Controls = { first, last } };
        using var form = WinFormsUi.Show(list);
        var em = first.Font.SizeInPoints * first.DeviceDpi / 72f;
        int Px(float value) => (int)MathF.Round(value);
        var line = first.LogicalToDeviceUnits(1);

        Assert.Equal(new Padding(0, Px(0.5f * em), 0, Px(0.5f * em) + line), first.Padding); // 标准：上下 sm，下面留出分隔线；不可点时两侧不留
        Assert.Equal(new Padding(0, Px(0.5f * em), 0, Px(0.5f * em)), last.Padding); // 最后一条下面没有线

        list.Density = ItemListDensity.Compact;
        list.Hoverable = true;
        Assert.Equal(new Padding(Px(0.5f * em), Px(0.25f * em), Px(0.5f * em), Px(0.25f * em) + line), first.Padding); // 紧凑：上下 xs；悬停着底色时两侧 sm

        list.Inset = Space.Md;
        Assert.Equal(Px(Tokens.Space.Md * em), first.Padding.Left); // 写了 Inset 按它
    });
}
