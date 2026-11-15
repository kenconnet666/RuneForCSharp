using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Rune.Wpf.Tests;

/// <summary>Image：读的时候占位、读完显示、读不了时占位（名称 alt、说明加载失败）；能预览时是按钮、打开 Lightbox；ImageGroup 共用预览、左右切换、旋转、关闭写回。</summary>
public class ImageTests
{
    /// <summary>一张真的 PNG（40×30）：WPF 会解码它。</summary>
    internal static Uri Picture(string name = "photo")
    {
        var path = Path.Combine(Path.GetTempPath(), $"rune-{name}-{Guid.NewGuid():N}.png");
        var bitmap = BitmapSource.Create(40, 30, 96, 96, PixelFormats.Gray8, null, new byte[40 * 30], 40);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(path)) encoder.Save(file);
        return new Uri(path);
    }

    static readonly Uri Missing = new(Path.Combine(Path.GetTempPath(), "rune-missing-drawing.png"));

    internal static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    internal static void Invoke(UIElement element)
    {
        ((IInvokeProvider)element.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
    }

    internal static void Press(UIElement target, Key key) =>
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });

    [Fact]
    [Upstream("Image", "加载完之前 img 透明（外层扫光），load 后显示；error 后换成占位，名称是 alt、说明是加载失败")]
    public Task Loading_then_loaded_or_the_fallback_named_by_alt() => WpfUi.Run(() =>
    {
        var image = new Image { Source = Picture(), Alt = "支架正面", Width = 160, Height = 120 };
        var broken = new Image { Source = Missing, Alt = "不存在的图纸", Width = 160, Height = 120 };
        WpfUi.Show(Host(image, broken));

        WpfUi.Until(() => image.Status == ImageStatus.Loaded && broken.Status == ImageStatus.Error);
        WpfUi.Flush();
        Assert.Equal(new Size(160, 120), image.RenderSize);
        Assert.Equal(AutomationControlType.Image, image.Peer().GetAutomationControlType());
        Assert.Equal("支架正面", image.Peer().GetName());
        Assert.False(image.Focusable);

        Assert.Equal("不存在的图纸", broken.Peer().GetName());
        Assert.Equal("图片加载失败", broken.Peer().GetHelpText());
    });

    [Fact]
    [Upstream("Image", "preview：是按钮（名称“查看大图：alt”），点了打开 Lightbox，用 preview.src 的大图")]
    public Task Preview_is_a_button_that_opens_the_lightbox_on_the_large_picture() => WpfUi.Run(() =>
    {
        var large = Picture("large");
        var image = new Image { Source = Picture("thumb"), Alt = "支架正面", Width = 160, Height = 120, Preview = true, PreviewSource = large, Caption = "DWG-1021" };
        WpfUi.Show(Host(image));
        WpfUi.Until(() => image.Status == ImageStatus.Loaded);

        Assert.Equal(AutomationControlType.Button, image.Peer().GetAutomationControlType());
        Assert.Equal("查看大图：支架正面", image.Peer().GetName());
        Assert.True(image.Focusable);
        Invoke(image);
        var lightbox = image.OwnLightbox!;
        Assert.True(lightbox.IsOpen);
        Assert.Equal(large, lightbox.Items![0].Source);
        Assert.Equal("DWG-1021", lightbox.Items[0].Caption);
        lightbox.Close();
        Assert.False(lightbox.IsOpen);
        Press(image, Key.Enter);
        Assert.True(lightbox.IsOpen);
        lightbox.Close();
    });

    [Fact]
    [Upstream("ImageGroup", "同组的图片共用一个预览：从点的那张开始，→ 下一张，计数跟着变，最后一张没有“下一张”")]
    public Task Group_shares_one_preview_starting_at_the_clicked_image() => WpfUi.Run(() =>
    {
        var group = new ImageGroup();
        var images = Enumerable.Range(1, 3).Select(i => new Image { Source = Picture($"g{i}"), Alt = $"图 {i}", Width = 80, Height = 60, Preview = true }).ToList();
        // 夹在别的内容里也算（图片加名称的一格）
        group.Children.Add(new StackPanel { Children = { images[0], new TextBlock { Text = "图 1" } } });
        group.Children.Add(images[1]);
        group.Children.Add(images[2]);
        WpfUi.Show(Host(group));
        WpfUi.Until(() => images.All(image => image.Status == ImageStatus.Loaded));

        Invoke(images[1]);
        var lightbox = group.Lightbox;
        Assert.True(lightbox.IsOpen);
        Assert.Null(images[1].OwnLightbox);
        Assert.Equal(["图 1", "图 2", "图 3"], lightbox.Items!.Select(item => item.Alt));
        Assert.Equal(1, lightbox.Index);
        Assert.Equal("第 2 张，共 3 张", lightbox.Labels.Counter.Text);
        Assert.Equal("图 2", lightbox.Viewport.Peer().GetName());

        WpfUi.Flush();
        Press(lightbox.Viewport, Key.Right);
        Assert.Equal(2, lightbox.Index);
        Assert.Equal("第 3 张，共 3 张", lightbox.Labels.Counter.Text);
        Assert.Equal(Visibility.Collapsed, lightbox.Sides.Next.Visibility);
        Assert.Equal(Visibility.Visible, lightbox.Sides.Previous.Visibility);
        Press(lightbox.Viewport, Key.Right);
        Assert.Equal(2, lightbox.Index);
        lightbox.Close();
    });

    [Fact]
    [Upstream("ImageGroup", "旋转一档：图片转 90°；关闭按钮关上并写回 open")]
    public Task Rotating_turns_ninety_degrees_and_close_writes_back() => WpfUi.Run(() =>
    {
        var lightbox = new Lightbox { Items = [new LightboxItem(Picture(), "支架正面")] };
        var reasons = new List<OpenChangeReason>();
        lightbox.OpenChanged += (_, e) => reasons.Add(e.Reason);
        WpfUi.Show(Host(lightbox));
        lightbox.IsOpen = true;
        WpfUi.Until(() => lightbox.Viewport.ActualWidth > 0);

        Invoke(lightbox.Tools.RotateRight);
        Assert.Equal(90, lightbox.Rotation);
        Press(lightbox.Viewport, Key.R);
        Assert.Equal(180, lightbox.Rotation);
        Invoke(lightbox.Tools.RotateLeft);
        Invoke(lightbox.Tools.RotateLeft);
        Invoke(lightbox.Tools.RotateLeft);
        Assert.Equal(270, lightbox.Rotation);
        Invoke(lightbox.Tools.Reset);
        Assert.Equal(0, lightbox.Rotation);

        Invoke(lightbox.Tools.Close);
        WpfUi.Until(() => !lightbox.IsOpen);
        Assert.Equal([OpenChangeReason.Close], reasons);
    });
}
