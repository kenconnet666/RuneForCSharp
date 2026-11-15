using Avalonia;
using Avalonia.Automation;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>ThemeSwitcher：菜单列出跟随系统与各套主题（内置的有默认文字，其他的用 Labels），当前的选择打勾；选一项写回 Value 并设到应用。</summary>
public class ThemeSwitcherTests
{
    static readonly ThemeVariant Brand = new("brand", ThemeVariant.Light);

    static IReadOnlyList<MenuRadioItem> Radios(ThemeSwitcher switcher) => switcher.Group.Items.OfType<MenuRadioItem>().ToList();

    [Fact]
    [Upstream("ThemeSwitcher", "列出跟随系统与各套主题，当前的打勾；选一项交给 preference.set")]
    public Task Lists_system_and_themes_and_chooses() => HeadlessUi.Run(() =>
    {
        var switcher = new ThemeSwitcher { Themes = [ThemeVariant.Light, ThemeVariant.Dark, Brand], Labels = new Dictionary<object, string> { ["brand"] = "品牌" } };
        Provider.SetLang(switcher, Lang.ZhCn);
        HeadlessUi.Show(switcher);
        try
        {
            Assert.Equal(["跟随系统", "亮色", "暗色", "品牌"], Radios(switcher).Select(radio => radio.Content as string));
            Assert.Equal([true, false, false, false], Radios(switcher).Select(radio => radio.Checked == true));
            Radios(switcher)[3].Run();
            Assert.Equal(Brand, switcher.Value);
            Assert.Equal(Brand, Application.Current!.RequestedThemeVariant);
            Assert.True(Radios(switcher)[3].Checked);
            var trigger = switcher.GetLogicalDescendants().OfType<IconButton>().Single(button => button.Name == ThemeSwitcherParts.Trigger);
            Assert.Equal(Lucide.Palette, trigger.Icon);
            Assert.Equal("主题", AutomationProperties.GetName(trigger));
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    });

    [Fact]
    [Upstream("ThemeSwitcher", "system={false} 时不列“跟随系统”")]
    public Task Without_system() => HeadlessUi.Run(() =>
    {
        var switcher = new ThemeSwitcher { Themes = [ThemeVariant.Light, ThemeVariant.Dark, Brand], IncludeSystem = false, AppliesToApplication = false };
        Provider.SetLang(switcher, Lang.ZhCn);
        HeadlessUi.Show(switcher);
        Assert.Equal(["亮色", "暗色", "brand"], Radios(switcher).Select(radio => radio.Content as string));
        Radios(switcher)[1].Run();
        Assert.Equal(ThemeVariant.Dark, switcher.Value);
        Assert.Equal(ThemeVariant.Default, Application.Current!.RequestedThemeVariant);
    });
}
