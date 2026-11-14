namespace Rune.WinForms.Tests;

/// <summary>ActionBarControl：起始边的说明在前、按钮在后，没有说明时只有按钮区；上下 space.md、左右 space.lg。</summary>
public class ActionBarTests
{
    [Fact]
    [Upstream("ActionBar", "start 在前、按钮在后")]
    public Task Start_comes_before_the_actions_and_is_hidden_without_text() => WinFormsUi.Run(() =>
    {
        var save = new ButtonControl { Text = "保存" };
        var bar = new ActionBarControl { Start = "有 3 处改动还没保存", Dock = DockStyle.Top };
        bar.Actions.Add(save);
        using var form = WinFormsUi.Show(bar);

        Assert.True(bar.StartPart.Visible);
        var startAt = bar.PointToClient(bar.StartPart.PointToScreen(Point.Empty));
        var saveAt = bar.PointToClient(save.PointToScreen(Point.Empty));
        Assert.True(startAt.X < saveAt.X);
        Assert.True(saveAt.X + save.Width >= bar.Width - bar.Padding.Right - 1); // 按钮靠结束边

        bar.Start = null;
        Assert.False(bar.StartPart.Visible);
        var lg = (int)MathF.Round(Tokens.Space.Lg * bar.Font.SizeInPoints * bar.DeviceDpi / 72f);
        Assert.Equal(lg, bar.Padding.Left);
    });
}
