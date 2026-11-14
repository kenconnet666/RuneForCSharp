namespace Rune.WinForms.Tests;

/// <summary>FigureControl：说明在下（top 时在上）、读屏名称是说明；AspectRatioControl：高度 = 宽度 ÷ 比例，子控件铺满。</summary>
public class FigureTests
{
    [Fact]
    public Task Caption_sits_below_or_above_and_names_the_figure() => WinFormsUi.Run(() =>
    {
        var content = new Panel { Height = 40 };
        var figure = new FigureControl { Caption = "图 1：采购额", Width = 300 };
        figure.Controls.Add(content);
        using var form = WinFormsUi.Show(figure);
        var caption = figure.Controls.OfType<TextControl>().Single();

        Assert.True(caption.Top >= content.Bottom);
        Assert.Equal(AccessibleRole.Grouping, figure.AccessibilityObject.Role);
        Assert.Equal("图 1：采购额", figure.AccessibilityObject.Name);

        figure.CaptionSide = FigureCaptionSide.Top;
        figure.PerformLayout();
        Assert.True(caption.Bottom <= content.Top);
    });

    [Fact]
    public Task Aspect_ratio_derives_the_height_and_fills_the_child() => WinFormsUi.Run(() =>
    {
        var child = new Panel();
        var box = new AspectRatioControl { Ratio = 4d / 3 };
        box.Controls.Add(child);

        Assert.Equal(new Size(400, 300), box.GetPreferredSize(new Size(400, 0)));
        box.AutoSize = false;
        box.Size = new Size(400, 300);
        box.PerformLayout();
        Assert.Equal(box.ClientRectangle, child.Bounds);
    });
}
