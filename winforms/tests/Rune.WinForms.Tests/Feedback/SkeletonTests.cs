namespace Rune.WinForms.Tests;

/// <summary>SkeletonControl：大小跟着被占位的子控件（隐藏、不可用）；形状按 em；加载完显示内容；加载时读屏里没有角色。</summary>
public class SkeletonTests
{
    [Fact]
    public Task Wrapping_keeps_the_content_size_but_hides_and_disables_it() => WinFormsUi.Run(() =>
    {
        var button = new ButtonControl { Text = "保存", AutoSize = false, Size = new Size(80, 30) };
        var skeleton = new SkeletonControl();
        skeleton.Controls.Add(button);
        using var form = WinFormsUi.Show(skeleton);

        Assert.Equal(new Size(80, 30), skeleton.Size);
        Assert.False(button.Visible);
        Assert.False(button.Enabled); // 包着的按钮不能聚焦、点击
        Assert.Equal(AccessibleRole.None, skeleton.AccessibilityObject.Role);

        skeleton.Loading = false;
        Assert.True(button.Visible);
        Assert.True(button.Enabled);
    });

    [Fact]
    public Task Shapes_are_sized_in_em() => WinFormsUi.Run(() =>
    {
        var circle = new SkeletonControl { Shape = SkeletonShape.Circle };
        using var form = WinFormsUi.Show(circle);
        var em = circle.Font.SizeInPoints * circle.DeviceDpi / 72f;

        Assert.Equal(new Size((int)MathF.Round(2.5f * em), (int)MathF.Round(2.5f * em)), circle.Size);
    });
}
