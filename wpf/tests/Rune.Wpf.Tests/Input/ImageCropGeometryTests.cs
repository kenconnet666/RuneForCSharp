namespace Rune.Wpf.Tests;

/// <summary>ImageCropGeometry：裁剪框放在中间、四周留边；盖满的缩放与初始状态；框里是图片的哪一块；输出的像素大小。</summary>
public class ImageCropGeometryTests
{
    [Fact]
    public void Frame_sits_in_the_middle_with_the_inset()
    {
        Assert.Equal(new PanZoomRegion(124, 24, 152, 152), ImageCropGeometry.Frame(400, 200, 1, 24));
        Assert.Equal(new PanZoomRegion(48, 24, 304, 152), ImageCropGeometry.Frame(400, 200, 2, 24));
        // 竖长的区域：宽度先用完
        Assert.Equal(new PanZoomRegion(24, 124, 152, 152), ImageCropGeometry.Frame(200, 400, 1, 24));
    }

    [Fact]
    public void Initial_covers_the_frame_and_centers()
    {
        var frame = new PanZoomRegion(100, 50, 100, 100);
        Assert.Equal(0.5, ImageCropGeometry.CoverScale(frame, (400, 200)));
        Assert.Equal(1, ImageCropGeometry.CoverScale(frame, null));
        // 缩放 0.5 后 200×100：横向居中在框里，竖向正好盖满
        Assert.Equal(new PanZoomState(50, 50, 0.5), ImageCropGeometry.Initial(frame, (400, 200)));
    }

    [Fact]
    public void Source_and_output_size()
    {
        var source = ImageCropGeometry.Source(new PanZoomRegion(100, 50, 100, 100), new PanZoomState(50, 50, 0.5));
        Assert.Equal(new PanZoomRegion(100, 0, 200, 200), source);
        Assert.Equal((200, 200), ImageCropGeometry.OutputSize(source, 1, default));
        Assert.Equal((256, 256), ImageCropGeometry.OutputSize(source, 1, new ImageCropOutput(256)));
        Assert.Equal((1280, 720), ImageCropGeometry.OutputSize(source, 16 / 9d, new ImageCropOutput(1280)));
        Assert.Equal((160, 90), ImageCropGeometry.OutputSize(source, 16 / 9d, new ImageCropOutput(Height: 90)));
    }
}
