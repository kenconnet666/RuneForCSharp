using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>H1–H6：默认字号按级别、semibold、行高 tight、xxl 起收紧字间距；读屏念级别；Size 能改字号不改级别。</summary>
public class HeadingTests
{
    static double Token(string key) => Application.Current!.TryGetResource(key, ThemeVariant.Light, out var value) ? (double)value! : double.NaN;

    [Upstream("Heading", "每个组件渲染对应的标签")]
    [Upstream("Heading", "默认字号随级别不同")]
    [Fact]
    public Task Levels_have_their_defaults() => HeadlessUi.Run(() =>
    {
        Heading[] levels = [new H1 { Text = "1" }, new H2 { Text = "2" }, new H3 { Text = "3" }, new H4 { Text = "4" }, new H5 { Text = "5" }, new H6 { Text = "6" }];
        var smaller = new H2 { Text = "账户设置", Size = TextSize.Lg };
        var host = new StackPanel();
        host.Children.AddRange([.. levels, smaller]);
        HeadlessUi.Show(host);

        Assert.Equal(
            new[] { "Xxxl", "Xxl", "Xl", "Lg", "Md", "Sm" }.Select(step => Token($"Rune.FontSize.{step}")),
            levels.Select(h => h.FontSize));
        Assert.All(levels, h => Assert.Equal(FontWeight.SemiBold, h.FontWeight));
        Assert.Equal(1.25 * levels[0].FontSize, levels[0].LineHeight);
        Assert.Equal(Token("Rune.LetterSpacing.Tighter") * levels[0].FontSize, levels[0].LetterSpacing, 6);
        Assert.Equal(Token("Rune.LetterSpacing.Tight") * levels[1].FontSize, levels[1].LetterSpacing, 6);
        Assert.Equal(0, levels[2].LetterSpacing);
        Assert.Equal(Enumerable.Range(1, 6), levels.Select(h => ControlAutomationPeer.CreatePeerForElement(h).GetHeadingLevel()));
        // 字号用 Size 改，级别不变
        Assert.Equal((Token("Rune.FontSize.Lg"), 2), (smaller.FontSize, ControlAutomationPeer.CreatePeerForElement(smaller).GetHeadingLevel()));
    });

    [Upstream("prose-elements", "标题有 id 且 anchor 时末尾有指向本节的链接；没有 id 时不加")]
    [Fact]
    public Task Anchors_need_a_name_and_raise_the_section_id() => HeadlessUi.Run(() =>
    {
        var install = new H2 { Name = "install", Anchor = true, Text = "安装" };
        var unnamed = new H2 { Anchor = true, Text = "没有 id" };
        var window = HeadlessUi.Show(new StackPanel { Children = { install, unnamed } });

        var anchor = install.GetLogicalDescendants().OfType<ButtonBase>().Single();
        Assert.Equal((HeadingParts.Anchor, "链接到本节"), (anchor.Name, global::Avalonia.Automation.AutomationProperties.GetName(anchor)));
        Assert.Equal("安装", install.Text);
        Assert.Empty(unnamed.GetLogicalDescendants().OfType<ButtonBase>());
        // 平时透明，悬停标题时显示
        Assert.Equal(0, anchor.Opacity);
        string? requested = null;
        install.AnchorRequested += (_, e) => requested = e.Id;
        anchor.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("install", requested);

        // 改了文字照样跟着 # 按钮；关掉 Anchor 只剩文字
        install.Text = "安装与升级";
        Assert.Single(install.GetLogicalDescendants().OfType<ButtonBase>());
        install.Anchor = false;
        HeadlessUi.Flush(window);
        Assert.Equal("安装与升级", install.Text);
        Assert.Empty(install.GetLogicalDescendants().OfType<ButtonBase>());
    });
}
