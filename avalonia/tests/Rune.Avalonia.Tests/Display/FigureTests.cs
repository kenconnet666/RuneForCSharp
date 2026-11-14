using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Figure：说明在下（top 时在上）、读屏名称是说明；AspectRatio：高度 = 宽度 ÷ 比例，内容铺满，图片按 cover 裁切。</summary>
public class FigureTests
{
    [Fact]
    public Task Caption_sits_below_or_above_and_names_the_figure() => HeadlessUi.Run(() =>
    {
        var content = new Border { Height = 40 };
        var figure = new Figure { Caption = "图 1：采购额", Content = content, Width = 300 };
        var window = HeadlessUi.Show(figure);
        var caption = figure.GetVisualDescendants().OfType<ContentPresenter>().First(part => part.Name == FigureParts.Caption);

        Assert.True(caption.Bounds.Top >= content.Bounds.Bottom);
        Assert.Equal(AutomationControlType.Group, figure.Peer().GetAutomationControlType());
        Assert.Equal("图 1：采购额", figure.Peer().GetName());

        figure.CaptionSide = FigureCaptionSide.Top;
        HeadlessUi.Flush(window);
        Assert.True(caption.Bounds.Bottom <= content.TranslatePoint(default, figure)!.Value.Y);
    });

    [Fact]
    public Task Aspect_ratio_derives_the_height_and_covers_images() => HeadlessUi.Run(() =>
    {
        var fill = new Border();
        var box = new AspectRatio { Ratio = 4d / 3, Child = fill };
        HeadlessUi.Show(new StackPanel { Width = 400, Children = { box } });

        Assert.Equal(300, box.Bounds.Height, 1);
        Assert.Equal(box.Bounds.Size, fill.Bounds.Size);

        var image = new Image();
        box.Child = image;
        Assert.Equal(global::Avalonia.Media.Stretch.UniformToFill, image.Stretch);
    });
}
