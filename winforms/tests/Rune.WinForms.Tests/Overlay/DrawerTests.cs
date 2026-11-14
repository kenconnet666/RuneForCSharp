using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>DrawerControl：贴着所在一侧、宽度档位与 PanelWidth；宽度调整手柄改 PanelWidth；上下两侧没有手柄；Esc 关闭（内核同 DialogControl）。</summary>
public class DrawerTests
{
    static void Press(Control control, Keys key) =>
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [new KeyEventArgs(key)]);

    /// <summary>屏幕上的窗体（遮罩与面板按屏幕定位）。</summary>
    static void WithForm(Action<Form> body, DrawerControl drawer)
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(200, 200), Size = new Size(800, 600),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
        };
        form.Controls.Add(new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn, Controls = { drawer } });
        form.Show();
        form.Activate();
        WinFormsUi.Pump();
        try
        {
            drawer.IsOpen = true;
            WinFormsUi.Pump();
            body(form);
        }
        finally
        {
            drawer.IsOpen = false;
        }
    }

    static int Px(Control control, int pixels) => (int)MathF.Round(pixels * control.DeviceDpi / 96f);

    [Fact]
    public Task End_side_hugs_the_right_edge_at_the_size_width() => WinFormsUi.Run(() =>
    {
        var drawer = new DrawerControl { Title = "筛选" };
        WithForm(form =>
        {
            var area = form.RectangleToScreen(form.ClientRectangle);
            var panel = drawer.Surface.Backdrop!.Panel;
            Assert.Equal(new Rectangle(area.Right - Px(drawer, 480), area.Top, Px(drawer, 480), area.Height), panel);
            // 手柄贴着靠正文的一边（面板的左边）
            Assert.True(drawer.ResizeHandle.Visible);
            Assert.Equal(0, drawer.ResizeHandle.Left);
            Assert.Equal(drawer.Surface.Form.ClientSize.Height, drawer.ResizeHandle.Height);
            Assert.Equal(AccessibleRole.Separator, drawer.ResizeHandle.AccessibilityObject.Role);
            Assert.Equal("调整抽屉宽度", drawer.ResizeHandle.AccessibilityObject.Name);
        }, drawer);
    });

    [Fact]
    public Task Start_side_hugs_the_left_edge_and_panel_width_overrides_the_size() => WinFormsUi.Run(() =>
    {
        var drawer = new DrawerControl { Title = "导航", Side = DrawerSide.Start, DialogSize = DialogSize.Md, PanelWidth = 300 };
        WithForm(form =>
        {
            var area = form.RectangleToScreen(form.ClientRectangle);
            Assert.Equal(new Rectangle(area.Left, area.Top, 300, area.Height), drawer.Surface.Backdrop!.Panel);
            Assert.Equal(drawer.Surface.Form.ClientSize.Width, drawer.ResizeHandle.Right);
        }, drawer);
    });

    [Fact]
    public Task Resize_handle_writes_the_panel_width_within_min_and_form() => WinFormsUi.Run(() =>
    {
        var drawer = new DrawerControl { Title = "详情", PanelWidth = 400 };
        var changes = 0;
        drawer.PanelWidthChanged += (_, _) => changes++;
        WithForm(form =>
        {
            // 结束边的抽屉：手柄在它的左边，往左（←）是变宽
            Press(drawer.ResizeHandle, Keys.Left);
            Assert.Equal(400 + Px(drawer, 16), drawer.PanelWidth);
            WinFormsUi.Pump();
            Assert.Equal(400 + Px(drawer, 16), drawer.Surface.Backdrop!.Panel.Width);
            drawer.ResizeHandle.SetLength(1);
            Assert.Equal(Px(drawer, 48), drawer.PanelWidth);
            drawer.ResizeHandle.SetLength(5000);
            Assert.Equal(form.ClientSize.Width - Px(drawer, 48), drawer.PanelWidth);
            Assert.Equal(3, changes);
        }, drawer);
    });

    [Fact]
    public Task Top_and_bottom_span_the_width_without_a_handle() => WinFormsUi.Run(() =>
    {
        var drawer = new DrawerControl { Title = "操作", Side = DrawerSide.Bottom, Content = new TextControl { Text = "正文" } };
        WithForm(form =>
        {
            var area = form.RectangleToScreen(form.ClientRectangle);
            var panel = drawer.Surface.Backdrop!.Panel;
            Assert.Equal((area.Left, area.Width, area.Bottom), (panel.Left, panel.Width, panel.Bottom));
            Assert.True(panel.Height < area.Height);
            Assert.False(drawer.ResizeHandle.Visible);
        }, drawer);
    });

    [Fact]
    public Task Escape_closes_like_a_dialog() => WinFormsUi.Run(() =>
    {
        var drawer = new DrawerControl { Title = "筛选" };
        var reasons = new List<OpenChangeReason>();
        drawer.OpenChanged += (_, e) => reasons.Add(e.Reason);
        WithForm(_ =>
        {
            drawer.Surface.Form.GetType().GetMethod("ProcessDialogKey", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(Keys)])!.Invoke(drawer.Surface.Form, [Keys.Escape]);
            WinFormsUi.Pump();
            Assert.False(drawer.IsOpen);
            Assert.Equal([OpenChangeReason.Escape], reasons);
        }, drawer);
    });
}
