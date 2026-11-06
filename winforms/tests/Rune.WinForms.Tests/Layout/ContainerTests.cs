namespace Rune.WinForms.Tests;

/// <summary>ContainerPanel：子控件停靠在居中的一栏里，最大宽度按档位，左右内边距按字体算。</summary>
public class ContainerTests
{
    [Upstream("Container", "div，档位追加样式")]
    [Fact]
    public Task Docked_children_fill_a_centered_column() => WinFormsUi.Run(() =>
    {
        var content = new Panel { Dock = DockStyle.Top, Height = 10 };
        var page = new ContainerPanel { ContainerSize = ContainerSize.Xs, AutoSize = false };
        page.Controls.Add(content);
        using var form = WinFormsUi.Show(page);
        var scale = page.DeviceDpi / 96f;
        page.Size = new Size((int)(600 * scale), 100);
        page.PerformLayout();
        var em = page.Font.SizeInPoints * page.DeviceDpi / 72f;

        var width = (int)MathF.Round(480 * scale);
        var inline = (int)MathF.Round(em);
        Assert.Equal(new Rectangle((page.ClientSize.Width - width) / 2 + inline, 0, width - 2 * inline, 10), content.Bounds);
    });
}
