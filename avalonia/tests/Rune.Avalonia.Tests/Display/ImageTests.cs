using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>Image：读的时候占位、读完显示、读不了时占位（名称 alt、说明加载失败）；能预览时是按钮、打开 Lightbox；ImageGroup 共用预览、左右切换、旋转、关闭写回。</summary>
public class ImageTests
{
    /// <summary>一个能读的图片文件（headless 不真解码，内容随意）。</summary>
    internal static Uri Picture(string name = "photo")
    {
        var path = Path.Combine(Path.GetTempPath(), $"rune-{name}-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        return new Uri(path);
    }

    static readonly Uri Missing = new(Path.Combine(Path.GetTempPath(), "rune-missing-drawing.png"));

    static void Invoke(Control control) => ((IInvokeProvider)control.Peer()).Invoke();

    static void Press(Control target, Key key) => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

    [Fact]
    [Upstream("Image", "加载完之前 img 透明（外层扫光），load 后显示；error 后换成占位，名称是 alt、说明是加载失败")]
    public Task Loading_then_loaded_or_the_fallback_named_by_alt() => HeadlessUi.Run(() =>
    {
        var image = new Image { Source = Picture(), Alt = "支架正面", Width = 160, Height = 120 };
        var broken = new Image { Source = Missing, Alt = "不存在的图纸", Width = 160, Height = 120 };
        var window = HeadlessUi.Show(new StackPanel { Children = { image, broken } });

        HeadlessUi.Until(() => image.Status == ImageStatus.Loaded && broken.Status == ImageStatus.Error);
        HeadlessUi.Flush(window);
        Assert.Equal(new Size(160, 120), image.Bounds.Size);
        Assert.Equal(AutomationControlType.Image, image.Peer().GetAutomationControlType());
        Assert.Equal("支架正面", image.Peer().GetName());
        Assert.False(image.Focusable);

        Assert.Equal("不存在的图纸", broken.Peer().GetName());
        Assert.Equal("图片加载失败", broken.Peer().GetHelpText());
        Assert.Contains(":error", broken.Classes);
    });

    [Fact]
    [Upstream("Image", "preview：是按钮（名称“查看大图：alt”），点了打开 Lightbox，用 preview.src 的大图")]
    public Task Preview_is_a_button_that_opens_the_lightbox_on_the_large_picture() => HeadlessUi.Run(() =>
    {
        var large = Picture("large");
        var image = new Image { Source = Picture("thumb"), Alt = "支架正面", Width = 160, Height = 120, Preview = true, PreviewSource = large, Caption = "DWG-1021" };
        var window = HeadlessUi.Show(image);
        HeadlessUi.Until(() => image.Status == ImageStatus.Loaded);

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
        HeadlessUi.Flush(window);
    });

    [Fact]
    [Upstream("ImageGroup", "同组的图片共用一个预览：从点的那张开始，→ 下一张，计数跟着变，最后一张没有“下一张”")]
    public Task Group_shares_one_preview_starting_at_the_clicked_image() => HeadlessUi.Run(() =>
    {
        var group = new ImageGroup();
        var images = Enumerable.Range(1, 3).Select(i => new Image { Source = Picture($"g{i}"), Alt = $"图 {i}", Width = 80, Height = 60, Preview = true }).ToList();
        // 夹在别的内容里也算（图片加名称的一格）
        group.Children.Add(new StackPanel { Children = { images[0], new TextBlock { Text = "图 1" } } });
        group.Children.Add(images[1]);
        group.Children.Add(images[2]);
        var window = HeadlessUi.Show(group);
        HeadlessUi.Until(() => images.All(image => image.Status == ImageStatus.Loaded));

        Invoke(images[1]);
        var lightbox = group.Lightbox;
        Assert.True(lightbox.IsOpen);
        Assert.Null(images[1].OwnLightbox);
        Assert.Equal(["图 1", "图 2", "图 3"], lightbox.Items!.Select(item => item.Alt));
        Assert.Equal(1, lightbox.Index);
        Assert.Equal("第 2 张，共 3 张", lightbox.Labels.Counter.Text);
        Assert.Equal("图 2", lightbox.Viewport.Peer().GetName());

        Press(lightbox.Viewport, Key.Right);
        Assert.Equal(2, lightbox.Index);
        Assert.Equal("第 3 张，共 3 张", lightbox.Labels.Counter.Text);
        Assert.False(lightbox.Sides.Next.IsVisible);
        Assert.True(lightbox.Sides.Previous.IsVisible);
        Press(lightbox.Viewport, Key.Right);
        Assert.Equal(2, lightbox.Index);
        lightbox.Close();
        HeadlessUi.Flush(window);
    });

    [Fact]
    [Upstream("ImageGroup", "旋转一档：图片转 90°；关闭按钮关上并写回 open")]
    public Task Rotating_turns_ninety_degrees_and_close_writes_back() => HeadlessUi.Run(() =>
    {
        var lightbox = new Lightbox { Items = [new LightboxItem(Picture(), "支架正面")] };
        var reasons = new List<OpenChangeReason>();
        lightbox.OpenChanged += (_, e) => reasons.Add(e.Reason);
        var window = HeadlessUi.Show(lightbox);
        lightbox.IsOpen = true;
        HeadlessUi.Until(() => lightbox.Viewport.Bounds.Width > 0);

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
        HeadlessUi.Until(() => !lightbox.IsOpen);
        Assert.Equal([OpenChangeReason.Close], reasons);
        HeadlessUi.Flush(window);
    });
}
