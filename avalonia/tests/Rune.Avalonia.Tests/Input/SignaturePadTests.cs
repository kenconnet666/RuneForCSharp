using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>SignaturePad：写一笔后值是 PNG、SVG 有路径，撤销回到 null；只读时拖动不写、显示“未签名”；画布是图片，名称默认“签名”；父级给的旧签名画在底下。</summary>
public class SignaturePadTests
{
    /// <summary>在画布上拖一笔（同上游测试：从左往右、上下起伏）。</summary>
    static void Stroke(SignaturePad pad, Window window)
    {
        Point At(double x, double y) => pad.CanvasPart.TranslatePoint(new Point(x, y), window)!.Value;
        window.MouseDown(At(20, 40), MouseButton.Left);
        for (var step = 1; step <= 12; step++) window.MouseMove(At(20 + step * 15, 40 + Math.Sin(step) * 20));
        window.MouseUp(At(200, 40), MouseButton.Left);
    }

    [Fact]
    [Upstream("SignaturePad", "写一笔：值变成 PNG，SVG 有路径；撤销回到 null")]
    public Task A_stroke_gives_a_png_and_undo_clears_it() => HeadlessUi.Run(() =>
    {
        var pad = new SignaturePad { Width = 400 };
        var window = HeadlessUi.Show(pad);

        var peer = pad.CanvasPart.Peer();
        Assert.Equal((AutomationControlType.Image, "签名", "未签名"), (peer.GetAutomationControlType(), peer.GetName(), peer.GetHelpText()));
        Assert.Equal("在此处签名", pad.PlaceholderPart.Text);
        Assert.False(pad.Tools.Undo.IsEffectivelyVisible);
        Stroke(pad, window);

        Assert.NotNull(pad.Value);
        Assert.Contains("<path", pad.ToSvg());
        Assert.Equal("已签名", peer.GetHelpText());
        Assert.False(pad.PlaceholderPart.IsVisible);
        Assert.Equal("撤销上一笔", pad.Tools.Undo.Peer().GetName());
        ((IInvokeProvider)pad.Tools.Undo.Peer()).Invoke();
        Assert.Null(pad.Value);
        Assert.Null(pad.ToSvg());
    });

    [Fact]
    [Upstream("SignaturePad", "只读：拖动不写，显示“未签名”")]
    public Task Read_only_does_not_write() => HeadlessUi.Run(() =>
    {
        var pad = new SignaturePad { Width = 400, IsReadOnly = true };
        var window = HeadlessUi.Show(pad);
        Stroke(pad, window);

        Assert.Null(pad.ToPng());
        Assert.Null(pad.Value);
        Assert.Equal("未签名", pad.PlaceholderPart.Text);
        Assert.False(pad.Tools.Undo.IsEffectivelyVisible || pad.Tools.Clear.IsEffectivelyVisible);
    });

    [Fact]
    public Task A_value_from_outside_is_the_base_and_clear_removes_it() => HeadlessUi.Run(() =>
    {
        // 编辑页读回的旧签名（headless 不真解码，PNG 的文件头就够）
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var field = new Field { Label = "签收人签字", Content = new SignaturePad { Width = 400, Value = png } };
        var window = HeadlessUi.Show(field);
        HeadlessUi.Flush(window);
        var pad = (SignaturePad)field.Content!;
        Assert.Equal("签收人签字", pad.CanvasPart.Peer().GetName());
        Assert.True(pad.Tools.Clear.IsEffectivelyVisible);
        Assert.False(pad.PlaceholderPart.IsVisible);
        ((IInvokeProvider)pad.Tools.Clear.Peer()).Invoke();
        Assert.Null(pad.Value);
        Assert.True(pad.PlaceholderPart.IsVisible);
    });
}
