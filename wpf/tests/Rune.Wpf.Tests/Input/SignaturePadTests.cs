using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Rune.Wpf.Tests;

/// <summary>SignaturePad：写一笔后值是 PNG、SVG 有路径，撤销回到 null；只读时不写、显示“未签名”；画布是图片，名称默认“签名”；父级给的旧签名画在底下。</summary>
public class SignaturePadTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    /// <summary>一笔（同上游测试：从左往右、上下起伏）。WPF 的鼠标位置模拟不了：直接喂画布里的坐标。</summary>
    static readonly Point[] Stroke = [new(20, 40), .. Enumerable.Range(1, 12).Select(step => new Point(20 + step * 15, 40 + Math.Sin(step) * 20)), new(200, 40)];

    static void Invoke(UIElement element)
    {
        ((IInvokeProvider)element.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("SignaturePad", "写一笔：值变成 PNG，SVG 有路径；撤销回到 null")]
    public Task A_stroke_gives_a_png_and_undo_clears_it() => WpfUi.Run(() =>
    {
        var pad = new SignaturePad { Width = 400 };
        WpfUi.Show(Host(pad));

        var peer = pad.CanvasPart.Peer();
        Assert.Equal((AutomationControlType.Image, "签名", "未签名"), (peer.GetAutomationControlType(), peer.GetName(), peer.GetHelpText()));
        Assert.Equal("在此处签名", pad.PlaceholderPart.Text);
        Assert.Equal(Visibility.Collapsed, pad.ToolbarPart.Visibility);
        pad.Write(Stroke);

        var png = pad.Value!;
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
        Assert.Contains("<path", pad.ToSvg());
        Assert.Equal("已签名", peer.GetHelpText());
        Assert.Equal(Visibility.Collapsed, pad.PlaceholderPart.Visibility);
        Assert.Equal("撤销上一笔", pad.Tools.Undo.Peer().GetName());
        Invoke(pad.Tools.Undo);
        Assert.Null(pad.Value);
        Assert.Null(pad.ToSvg());
    });

    [Fact]
    [Upstream("SignaturePad", "只读：拖动不写，显示“未签名”")]
    public Task Read_only_does_not_write() => WpfUi.Run(() =>
    {
        var pad = new SignaturePad { Width = 400, IsReadOnly = true };
        WpfUi.Show(Host(pad));
        pad.Write(Stroke);

        Assert.Null(pad.ToPng());
        Assert.Null(pad.Value);
        Assert.Equal("未签名", pad.PlaceholderPart.Text);
        Assert.Equal(Visibility.Collapsed, pad.ToolbarPart.Visibility);
    });

    [Fact]
    public Task A_value_from_outside_is_the_base_and_clear_removes_it() => WpfUi.Run(() =>
    {
        var first = new SignaturePad { Width = 400 };
        var host = Host(first);
        WpfUi.Show(host);
        first.Write(Stroke);
        var png = first.Value!;

        var pad = new SignaturePad { Width = 400, Value = png };
        host.Children.Add(new Field { Label = "签收人签字", Content = pad });
        WpfUi.Flush();
        Assert.Equal("签收人签字", pad.CanvasPart.Peer().GetName());
        Assert.Equal(Visibility.Visible, pad.ToolbarPart.Visibility);
        Assert.Equal(Visibility.Collapsed, pad.PlaceholderPart.Visibility);
        // 只有读回的旧签名：导出的 SVG 把它嵌成图片
        Assert.Contains("<image href=\"data:image/png;base64,", pad.ToSvg());
        var decoded = BitmapFrame.Create(new System.IO.MemoryStream(pad.ToPng()!));
        Assert.True(decoded.PixelWidth > 0);
        Invoke(pad.Tools.Clear);
        Assert.Null(pad.Value);
        Assert.Equal(Visibility.Visible, pad.PlaceholderPart.Visibility);
    });
}
