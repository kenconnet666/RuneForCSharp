using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Abbr：全称在提示里，能聚焦，帮助指针，点状下划线。</summary>
public class AbbrTests
{
    [Fact]
    public Task Title_is_the_tooltip() => WpfUi.Run(() =>
    {
        var abbr = new Abbr { Text = "SLA", Title = "服务等级协议" };
        var host = new StackPanel { Children = { abbr } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);

        Assert.Equal("服务等级协议", Assert.IsType<ToolTip>(abbr.ToolTip).Content);
        Assert.True(abbr.Focusable);
        Assert.Equal(Cursors.Help, abbr.Cursor);
        Assert.Single(abbr.TextDecorations);

        abbr.Title = null;
        Assert.Null(abbr.ToolTip);
    });
}
