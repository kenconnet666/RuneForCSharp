using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Figure：说明在下（top 时在上）、读屏名称是说明；AspectRatio：高度 = 宽度 ÷ 比例，内容铺满，图片按 cover 裁切。</summary>
public class FigureTests
{
    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel { Width = 400, Children = { child } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        return host;
    }

    [Fact]
    public Task Caption_sits_below_or_above_and_names_the_figure() => WpfUi.Run(() =>
    {
        var content = new Border { Height = 40 };
        var figure = new Figure { Caption = "图 1：采购额", Content = content };
        WpfUi.Show(Host(figure));
        var caption = (ContentPresenter)figure.Template.FindName("PART_Caption", figure);

        Assert.True(caption.TranslatePoint(default, figure).Y >= content.TranslatePoint(default, figure).Y + content.ActualHeight);
        Assert.Equal(AutomationControlType.Group, figure.Peer().GetAutomationControlType());
        Assert.Equal("图 1：采购额", figure.Peer().GetName());

        figure.CaptionSide = FigureCaptionSide.Top;
        WpfUi.Flush();
        Assert.True(caption.TranslatePoint(default, figure).Y + caption.ActualHeight <= content.TranslatePoint(default, figure).Y);
    });

    [Fact]
    public Task Aspect_ratio_derives_the_height_and_covers_images() => WpfUi.Run(() =>
    {
        var image = new Image();
        var box = new AspectRatio { Ratio = 4d / 3, Child = image };
        WpfUi.Show(Host(box));

        Assert.Equal(300, box.ActualHeight, 1);
        Assert.Equal(Stretch.UniformToFill, image.Stretch);
    });
}
