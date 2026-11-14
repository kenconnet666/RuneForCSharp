namespace Rune.WinForms.Tests;

/// <summary>AbbrControl：全称在提示里（读屏的说明），能聚焦，帮助指针。</summary>
public class AbbrTests
{
    [Fact]
    public Task Title_is_the_tooltip() => WinFormsUi.Run(() =>
    {
        var abbr = new AbbrControl { Text = "SLA", Title = "服务等级协议" };
        using var form = WinFormsUi.Show(abbr);

        Assert.Equal("服务等级协议", abbr.AccessibleDescription);
        Assert.True(abbr.CanSelect);
        Assert.Equal(Cursors.Help, abbr.Cursor);
    });
}
