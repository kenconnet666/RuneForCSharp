namespace Rune.WinForms.Tests;

/// <summary>FloatButtonControl：单独用时在角落、组里跟着组排、名称转给按钮、提示默认是名称、角标；速拨展开收起、Esc 收起；BackTopControl 滚过阈值才出现、点了回到顶部。</summary>
public class FloatButtonTests
{
    static Form Open(params Control[] controls)
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(200, 200), Size = new Size(800, 600), FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false };
        var page = new Panel { Dock = DockStyle.Fill };
        page.Controls.AddRange(controls);
        form.Controls.Add(new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn, Controls = { page } });
        form.Show();
        WinFormsUi.Pump();
        return form;
    }

    [Fact]
    [Upstream("FloatButton", "单个与组里的按钮外观不同（单个自己固定在角落）；组是带名称的 group")]
    [Upstream("FloatButton", "组停在示例框的右下角；滚下去后多出回到顶部")]
    public Task Alone_it_sits_in_the_corner_and_in_a_group_it_follows_the_group() => WinFormsUi.Run(() =>
    {
        var alone = new FloatButtonControl { Icon = Lucide.MessageCircle, AccessibleName = "联系客服" };
        var grouped = new FloatButtonControl { Icon = Lucide.MessageSquareText, AccessibleName = "意见反馈" };
        var group = new FloatButtonGroupControl { AccessibleName = "帮助" };
        group.Buttons.Add(grouped);
        using var form = Open(alone, group);

        var margin = (int)MathF.Round(Tokens.Space.Lg * ElementContext.Of(alone).Em);
        var page = alone.Parent!;
        Assert.Equal(page.ClientSize.Width - margin, alone.Right);
        Assert.Equal(page.ClientSize.Height - margin, alone.Bottom);
        Assert.Equal(page.ClientSize.Width - margin, group.Right);
        Assert.False(grouped.Grouped && grouped.Right == group.Right - margin);
        Assert.Equal("联系客服", alone.Button.AccessibleName);
        Assert.Equal("联系客服", alone.ShownTip);
        Assert.Equal((ButtonVariant.Surface, Radius.Full, TextSize.Lg), (alone.Button.Variant, alone.Button.Radius, alone.Button.TextSize));
        Assert.Equal(AccessibleRole.Grouping, group.AccessibilityObject.Role);
        Assert.Equal("帮助", group.AccessibilityObject.Name);
        Assert.False(group.Trigger.Visible);

        var clicks = 0;
        alone.Click += (_, _) => clicks++;
        alone.Button.PerformClick();
        Assert.Equal(1, clicks);
        alone.ShowTip = false;
        Assert.Null(alone.ShownTip);

        // 父控件变大小：跟着停在角落
        form.Width += 100;
        WinFormsUi.Pump();
        Assert.Equal(page.ClientSize.Width - margin, alone.Right);
    });

    [Fact]
    [Upstream("FloatButton", "badge={false}（没有未读）和没写角标一样：照常固定在角落")]
    public Task Badge_shows_a_count_or_a_dot() => WinFormsUi.Run(() =>
    {
        var button = new FloatButtonControl { Icon = Lucide.MessageCircle, Badge = 2 };
        using var form = Open(button);
        Assert.Equal(2, button.Indicator.Count);
        button.Badge = null;
        button.BadgeDot = true;
        Assert.True(button.Indicator.Dot);
    });

    [Fact]
    [Upstream("FloatButton", "速拨：主按钮 aria-expanded 跟着切换、关联展开的那一列；Esc 收起")]
    [Upstream("FloatButton", "速拨：点主按钮展开，选一项后收起")]
    public Task Speed_dial_expands_collapses_and_escape_closes() => WinFormsUi.Run(() =>
    {
        var action = new FloatButtonControl { Icon = Lucide.FileText, AccessibleName = "新建文档" };
        var dial = new FloatButtonGroupControl { Icon = Lucide.Plus, AccessibleName = "新建" };
        dial.Buttons.Add(action);
        using var form = Open(dial);

        var trigger = dial.Trigger.Button;
        Assert.True(dial.Trigger.Visible);
        Assert.Equal("新建", trigger.AccessibleName);
        Assert.Null(dial.AccessibilityObject.Name);
        Assert.False(dial.Actions.Visible);
        Assert.True(trigger.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed));

        trigger.PerformClick();
        Assert.True(dial.IsOpen);
        Assert.True(dial.Actions.Visible);
        Assert.True(trigger.AccessibilityObject.State.HasFlag(AccessibleStates.Expanded));
        Assert.Same(Lucide.X, trigger.Icon);
        dial.Escape();
        Assert.False(dial.IsOpen);
        Assert.False(dial.Actions.Visible);
    });

    [Fact]
    public Task Speed_dial_named_by_the_language_pack_by_default() => WinFormsUi.Run(() =>
    {
        var dial = new FloatButtonGroupControl();
        using var form = Open(dial);
        dial.Icon = Lucide.Plus;
        Assert.Equal("更多操作", dial.Trigger.Button.AccessibleName);
    });

    [Fact]
    [Upstream("BackTop", "滚过阈值才出现；点了容器回到顶部、焦点移到容器")]
    [Upstream("FloatButton", "组停在示例框的右下角；滚下去后多出回到顶部")]
    public Task Back_top_appears_past_the_threshold_and_scrolls_back() => WinFormsUi.Run(() =>
    {
        var first = new ButtonControl { Text = "第一个" };
        var content = new FlexControl { Direction = FlexDirection.Column };
        content.Controls.AddRange([first, new Panel { Height = 2000 }]);
        var scroller = new ScrollAreaControl { Dock = DockStyle.Fill, Content = content };
        var back = new BackTopControl { Target = scroller, Threshold = 100 };
        using var form = Open(back, scroller);

        Assert.False(back.IsShown);
        Assert.False(back.Visible);
        Assert.Equal("回到顶部", back.Button.AccessibleName);
        scroller.ScrollOffset = new Point(0, back.LogicalToDeviceUnits(500));
        WinFormsUi.Pump();
        Assert.True(back.IsShown);
        Assert.True(back.Visible);

        back.Button.PerformClick();
        WinFormsUi.Until(() => scroller.ScrollOffset.Y == 0 && first.Focused);
        Assert.False(back.Visible);
    });

    [Fact]
    public Task Back_top_finds_the_form_scroller_without_a_target() => WinFormsUi.Run(() =>
    {
        var content = new FlexControl { Direction = FlexDirection.Column };
        content.Controls.Add(new Panel { Height = 2000 });
        var scroller = new ScrollAreaControl { Dock = DockStyle.Fill, Content = content };
        var back = new BackTopControl { Threshold = 100 };
        var group = new FloatButtonGroupControl();
        group.Buttons.Add(back);
        using var form = Open(group, scroller);
        WinFormsUi.Pump();

        scroller.ScrollOffset = new Point(0, back.LogicalToDeviceUnits(500));
        WinFormsUi.Until(() => back.Visible);
    });
}
