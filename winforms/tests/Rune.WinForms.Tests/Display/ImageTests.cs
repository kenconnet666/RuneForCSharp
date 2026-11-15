using System.Drawing.Imaging;
using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>ImageControl：读的时候占位、读完显示、读不了时占位（名称 alt、说明加载失败）；能预览时是按钮、打开 Lightbox；ImageGroupControl 共用预览、左右切换、旋转、关闭。</summary>
public class ImageTests
{
    /// <summary>一张真的 PNG（40×30）。</summary>
    internal static Uri Picture(string name = "photo")
    {
        var path = Path.Combine(Path.GetTempPath(), $"rune-{name}-{Guid.NewGuid():N}.png");
        using (var bitmap = new Bitmap(40, 30)) bitmap.Save(path, ImageFormat.Png);
        return new Uri(path);
    }

    static readonly Uri Missing = new(Path.Combine(Path.GetTempPath(), "rune-missing-drawing.png"));

    /// <summary>屏幕上的窗体（遮罩与面板按屏幕定位）。</summary>
    internal static Form Open(params Control[] controls)
    {
        var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(200, 200), Size = new Size(800, 600),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
        };
        var stack = new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Start, Dock = DockStyle.Fill };
        stack.Controls.AddRange(controls);
        form.Controls.Add(new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn, Controls = { stack } });
        form.Show();
        form.Activate();
        WinFormsUi.Pump();
        return form;
    }

    static void Press(Control control, Keys key) =>
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [new KeyEventArgs(key)]);

    [Fact]
    [Upstream("Image", "加载完之前 img 透明（外层扫光），load 后显示；error 后换成占位，名称是 alt、说明是加载失败")]
    public Task Loading_then_loaded_or_the_fallback_named_by_alt() => WinFormsUi.Run(() =>
    {
        var image = new ImageControl { Source = Picture(), Alt = "支架正面", Size = new Size(160, 120) };
        var broken = new ImageControl { Source = Missing, Alt = "不存在的图纸", Size = new Size(160, 120) };
        using var form = Open(image, broken);

        WinFormsUi.Until(() => image.Status == ImageStatus.Loaded && broken.Status == ImageStatus.Error);
        WinFormsUi.Paint(image);
        WinFormsUi.Paint(broken);
        Assert.Equal(AccessibleRole.Graphic, image.AccessibilityObject.Role);
        Assert.Equal("支架正面", image.AccessibilityObject.Name);
        Assert.False(image.TabStop);

        Assert.Equal("不存在的图纸", broken.AccessibilityObject.Name);
        Assert.Equal("图片加载失败", broken.AccessibilityObject.Description);
    });

    [Fact]
    [Upstream("Image", "preview：是按钮（名称“查看大图：alt”），点了打开 Lightbox，用 preview.src 的大图")]
    public Task Preview_is_a_button_that_opens_the_lightbox_on_the_large_picture() => WinFormsUi.Run(() =>
    {
        var large = Picture("large");
        var image = new ImageControl { Source = Picture("thumb"), Alt = "支架正面", Size = new Size(160, 120), Preview = true, PreviewSource = large, Caption = "DWG-1021" };
        using var form = Open(image);
        WinFormsUi.Until(() => image.Status == ImageStatus.Loaded);

        Assert.Equal(AccessibleRole.PushButton, image.AccessibilityObject.Role);
        Assert.Equal("查看大图：支架正面", image.AccessibilityObject.Name);
        Assert.True(image.TabStop);
        image.AccessibilityObject.DoDefaultAction();
        var lightbox = image.OwnLightbox!;
        Assert.True(lightbox.IsOpen);
        Assert.Equal(large, lightbox.Items![0].Source);
        Assert.Equal("DWG-1021", lightbox.Items[0].Caption);
        lightbox.Close();
        Assert.False(lightbox.IsOpen);
        Press(image, Keys.Enter);
        Assert.True(lightbox.IsOpen);
        lightbox.Close();
    });

    [Fact]
    [Upstream("ImageGroup", "同组的图片共用一个预览：从点的那张开始，→ 下一张，计数跟着变，最后一张没有“下一张”")]
    public Task Group_shares_one_preview_starting_at_the_clicked_image() => WinFormsUi.Run(() =>
    {
        var group = new ImageGroupControl();
        var images = Enumerable.Range(1, 3).Select(i => new ImageControl { Source = Picture($"g{i}"), Alt = $"图 {i}", Size = new Size(80, 60), Preview = true }).ToList();
        // 夹在别的控件里也算（图片加名称的一格）
        var cell = new FlexControl { Direction = FlexDirection.Column };
        cell.Controls.AddRange([images[0], new SmallControl { Text = "图 1" }]);
        group.Controls.AddRange([cell, images[1], images[2]]);
        using var form = Open(group);
        WinFormsUi.Until(() => images.All(image => image.Status == ImageStatus.Loaded));

        images[1].AccessibilityObject.DoDefaultAction();
        var lightbox = group.Lightbox;
        Assert.True(lightbox.IsOpen);
        Assert.Null(images[1].OwnLightbox);
        Assert.Equal(["图 1", "图 2", "图 3"], lightbox.Items!.Select(item => item.Alt));
        Assert.Equal(1, lightbox.Index);
        Assert.Equal("第 2 张，共 3 张", lightbox.Parts.Counter.Text);
        Assert.Equal("图 2", lightbox.Parts.Stage.AccessibleName);

        Assert.True(lightbox.Press(Keys.Right));
        Assert.Equal(2, lightbox.Index);
        Assert.Equal("第 3 张，共 3 张", lightbox.Parts.Counter.Text);
        Assert.False(lightbox.Parts.Next.Visible);
        Assert.True(lightbox.Parts.Previous.Visible);
        lightbox.Press(Keys.Right);
        Assert.Equal(2, lightbox.Index);
        lightbox.Close();
    });

    [Fact]
    [Upstream("ImageGroup", "旋转一档：图片转 90°；关闭按钮关上并写回 open")]
    public Task Rotating_turns_ninety_degrees_and_close_reports_it() => WinFormsUi.Run(() =>
    {
        var lightbox = new LightboxControl { Items = [new LightboxItem(Picture(), "支架正面")] };
        var reasons = new List<OpenChangeReason>();
        lightbox.OpenChanged += (_, e) => reasons.Add(e.Reason);
        using var form = Open(lightbox);
        lightbox.IsOpen = true;
        WinFormsUi.Pump();

        lightbox.Parts.RotateRight.PerformClick();
        Assert.Equal(90, lightbox.Rotation);
        lightbox.Press(Keys.R);
        Assert.Equal(180, lightbox.Rotation);
        lightbox.Parts.RotateLeft.PerformClick();
        lightbox.Parts.RotateLeft.PerformClick();
        lightbox.Parts.RotateLeft.PerformClick();
        Assert.Equal(270, lightbox.Rotation);
        lightbox.Parts.Reset.PerformClick();
        Assert.Equal(0, lightbox.Rotation);

        lightbox.Parts.Close.PerformClick();
        WinFormsUi.Until(() => !lightbox.IsOpen);
        Assert.Equal([OpenChangeReason.Close], reasons);
    });
}
