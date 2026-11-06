using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Empty：图标（默认 Inbox，null 不显示）、标题、说明、操作居中排成一列；没给的不占位；内边距与尺寸按字号。</summary>
public class EmptyTests
{
    // 操作里的按钮模板也有 PART_Icon：Empty 自己的部件排在前面
    static T Part<T>(Empty empty, string name) where T : Control => empty.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    [Fact]
    public Task Icon_title_description_and_action_stack_centered() => HeadlessUi.Run(() =>
    {
        var full = new Empty { Title = "还没有订单", Description = "下单后会显示在这里。", Action = new Button { Content = "去逛逛" } };
        var bare = new Empty { Icon = null, Title = "没有找到", Size = TextSize.Sm };
        HeadlessUi.Show(new StackPanel { Width = 400, Children = { full, bare } });
        var em = full.FontSize;

        var icon = Part<Icon>(full, EmptyParts.Icon);
        Assert.Equal((Lucide.Inbox, TextTone.Muted), (icon.Data, icon.Tone));
        Assert.Equal(2.5 * em, icon.Bounds.Width, 3);
        Assert.Equal(TextWeight.Medium, Part<Text>(full, EmptyParts.Title).Weight);
        Assert.Equal(TextTone.Muted, Part<Paragraph>(full, EmptyParts.Description).Tone);
        // 居中：图标的中心在中线上；上下内边距 space.xl（1.5em）
        var origin = icon.TranslatePoint(default, full)!.Value;
        Assert.Equal(200, origin.X + icon.Bounds.Width / 2, 1);
        Assert.Equal(1.5 * em, origin.Y, 1);

        Assert.False(Part<Icon>(bare, EmptyParts.Icon).IsVisible); // null 不显示图标
        Assert.False(Part<Paragraph>(bare, EmptyParts.Description).IsVisible); // 没给的不占位
        Assert.Equal(13, bare.FontSize); // 字号档位 sm
    });
}
