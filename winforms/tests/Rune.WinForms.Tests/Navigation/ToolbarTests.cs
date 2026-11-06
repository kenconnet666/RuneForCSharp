namespace Rune.WinForms.Tests;

/// <summary>ToolbarControl：整组一个 Tab 停靠点，方向键在按钮之间移动（漫游焦点），输入框不参与。</summary>
public class ToolbarTests
{
    /// <summary>按键交到工具栏（项本身不处理方向键，WinForms 把它交给父控件的 ProcessDialogKey）。</summary>
    sealed class Toolbar : ToolbarControl
    {
        public bool Press(Keys key) => ProcessDialogKey(key);
    }

    [Upstream("Toolbar", "role=")]
    [Fact]
    public Task One_tab_stop_and_arrow_keys_move_between_buttons() => WinFormsUi.Run(() =>
    {
        var (bold, italic, underline) = (new ButtonControl { Text = "粗" }, new ButtonControl { Text = "斜" }, new ButtonControl { Text = "下" });
        var size = new TextBox();
        var toolbar = new Toolbar();
        toolbar.Controls.AddRange([bold, italic, underline, size]);
        using var form = WinFormsUi.Show(toolbar);

        Assert.Equal(AccessibleRole.ToolBar, toolbar.AccessibilityObject.Role);
        Assert.Equal([true, false, false], new Control[] { bold, italic, underline }.Select(item => item.TabStop));
        Assert.True(size.TabStop); // 输入框不参与方向键移动，照常用 Tab

        bold.Focus();
        Assert.True(toolbar.Press(Keys.Right));
        Assert.True(italic.Focused);
        Assert.Equal([false, true, false], new Control[] { bold, italic, underline }.Select(item => item.TabStop)); // Tab 再进来时落在上次聚焦的项
        toolbar.Press(Keys.End);
        Assert.True(underline.Focused);
        toolbar.Press(Keys.Right);
        Assert.True(bold.Focused); // 到头从另一端继续
    });

    [Fact]
    public Task Vertical_toolbars_use_up_and_down_and_skip_disabled_items() => WinFormsUi.Run(() =>
    {
        var (first, off, last) = (new ButtonControl { Text = "一" }, new ButtonControl { Text = "二", Enabled = false }, new ButtonControl { Text = "三" });
        var toolbar = new Toolbar { Orientation = ToolbarOrientation.Vertical, Loop = false };
        toolbar.Controls.AddRange([first, off, last]);
        using var form = WinFormsUi.Show(toolbar);

        first.Focus();
        toolbar.Press(Keys.Right);
        Assert.True(first.Focused); // 竖排：左右键不动
        toolbar.Press(Keys.Down);
        Assert.True(last.Focused); // 跳过不可用的
        toolbar.Press(Keys.Down);
        Assert.True(last.Focused); // 不循环
        Assert.Equal(first.Width, last.Width); // 竖排时拉伸
    });
}
