using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>SignaturePadControl：写一笔后值是 PNG、SVG 有路径，撤销回到 null；只读时不写、显示“未签名”；控件是图片，名称默认“签名”；代码里给的旧签名画在底下。</summary>
public class SignaturePadTests
{
    static Form Show(params Control[] controls)
    {
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        var stack = new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Dock = DockStyle.Top };
        stack.Controls.AddRange(controls);
        host.Controls.Add(stack);
        return WinFormsUi.Show(host);
    }

    static void Raise(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [args]);

    /// <summary>拖一笔（同上游测试：从左往右、上下起伏）。</summary>
    static void Stroke(Control pad)
    {
        Raise(pad, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 20, 40, 0));
        for (var step = 1; step <= 12; step++)
        {
            Raise(pad, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 20 + step * 15, 40 + (int)(Math.Sin(step) * 20), 0));
            Thread.Sleep(2);
        }
        Raise(pad, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 200, 40, 0));
    }

    [Fact]
    [Upstream("SignaturePad", "写一笔：值变成 PNG，SVG 有路径；撤销回到 null")]
    public Task A_stroke_gives_a_png_and_undo_clears_it() => WinFormsUi.Run(() =>
    {
        var pad = new SignaturePadControl();
        var changes = 0;
        pad.ValueChanged += (_, _) => changes++;
        using var form = Show(pad);

        Assert.Equal((AccessibleRole.Graphic, "签名", "未签名"), (pad.AccessibilityObject.Role, pad.AccessibilityObject.Name, pad.AccessibilityObject.Description));
        Assert.Equal("在此处签名", pad.PlaceholderText);
        Assert.False(pad.Tools.Undo.Visible);
        Stroke(pad);

        var png = pad.Value!;
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
        Assert.Equal(1, changes);
        Assert.Contains("<path", pad.ToSvg());
        Assert.Equal("已签名", pad.AccessibilityObject.Description);
        Assert.Null(pad.PlaceholderText);
        Assert.Equal("撤销上一笔", pad.Tools.Undo.AccessibleName);
        pad.Tools.Undo.PerformClick();
        Assert.Null(pad.Value);
        Assert.Null(pad.ToSvg());
    });

    [Fact]
    [Upstream("SignaturePad", "只读：拖动不写，显示“未签名”")]
    public Task Read_only_does_not_write() => WinFormsUi.Run(() =>
    {
        var pad = new SignaturePadControl { IsReadOnly = true };
        using var form = Show(pad);
        Stroke(pad);

        Assert.Null(pad.ToPng());
        Assert.Null(pad.Value);
        Assert.Equal("未签名", pad.PlaceholderText);
        Assert.False(pad.Tools.Undo.Visible || pad.Tools.Clear.Visible);
    });

    [Fact]
    public Task A_value_from_code_is_the_base_and_clear_removes_it() => WinFormsUi.Run(() =>
    {
        var first = new SignaturePadControl();
        var pad = new SignaturePadControl();
        var field = new FieldControl { Label = "签收人签字", Content = pad };
        using var form = Show(first, field);
        Stroke(first);

        pad.Value = first.Value;
        Assert.Equal("签收人签字", pad.AccessibilityObject.Name);
        Assert.True(pad.Tools.Clear.Visible);
        Assert.Null(pad.PlaceholderText);
        Assert.Contains("<image href=\"data:image/png;base64,", pad.ToSvg());
        pad.Tools.Clear.PerformClick();
        Assert.Null(pad.Value);
        Assert.Equal("在此处签名", pad.PlaceholderText);
    });
}
