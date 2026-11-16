using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>ThemeCustomizer：旋钮写回设置、"主题默认"清空、导出代码跟着设置变、当前取值显示在旋钮旁、恢复默认；对比度检查读实际生效的颜色。</summary>
public class ThemeCustomizerTests
{
    static ToggleItem Item(ThemeCustomizer editor, string label) =>
        editor.GetLogicalDescendants().OfType<ToggleItem>().First(item => AutomationProperties.GetName(item) == label
            || item.GetLogicalDescendants().OfType<TextBlock>().Any(text => text.Text == label));

    static void Click(Control button) => ((global::Avalonia.Automation.Provider.IInvokeProvider)button.Peer()).Invoke();

    [Fact]
    [Upstream("ThemeCustomizer", "选品牌色、圆角写回设置；选“主题默认”清空；代码跟着变；恢复默认清空全部")]
    public Task Knobs_write_settings_and_reset() => HeadlessUi.Run(() =>
    {
        var editor = new ThemeCustomizer { Base = ThemeVariant.Light, Width = 640 };
        var window = HeadlessUi.Show(editor);
        var reset = editor.Part<Button>(ThemeCustomizerParts.Reset);
        // 没改过时不能恢复默认；色块只有颜色，名字在读屏名称里，悬停显示名字、色阶与色值
        Assert.False(reset.IsEnabled);
        Assert.Matches("^绯红（crimson）#[0-9a-f]{6}$", (string)ToolTip.GetTip(Item(editor, "绯红"))!);

        Click(Item(editor, "绯红"));
        Assert.Equal(new ThemeSettings(Accent: "crimson"), editor.Settings);
        Assert.Contains("<Color x:Key=\"Rune.Color.Accent\">", editor.Code);

        Click(Item(editor, "大"));
        Assert.Equal(ThemeRadius.Large, editor.Settings.Radius);
        // 旋钮标题旁显示选中的名字与色值
        Assert.Contains(editor.GetLogicalDescendants().OfType<Text>(), text => text.Text is { } value && System.Text.RegularExpressions.Regex.IsMatch(value, "^绯红 #[0-9a-f]{6}$"));
        Assert.True(reset.IsEnabled);

        var accents = editor.GetLogicalDescendants().OfType<ToggleGroup>().First(group => AutomationProperties.GetName(group) == "品牌色");
        Click(accents.Children.OfType<ToggleItem>().First());
        Assert.Null(editor.Settings.Accent);

        Click(reset);
        Assert.Equal(new ThemeSettings(), editor.Settings);
    });

    [Fact]
    public Task Contrast_reads_the_target_colors() => HeadlessUi.Run(() =>
    {
        var preview = new Border();
        var editor = new ThemeCustomizer { Base = ThemeVariant.Light, ContrastTarget = preview };
        var window = HeadlessUi.Show(new StackPanel { Children = { editor, preview } });
        editor.CheckContrast();
        Assert.Equal((true, StatusTone.Success), (editor.ContrastAlert.IsVisible, editor.ContrastAlert.Tone));
        ThemeCustomization.Apply(preview, new ThemeSettings(Accent: "yellow"), ThemeVariant.Light);
        editor.CheckContrast();
        Assert.Equal(StatusTone.Warning, editor.ContrastAlert.Tone);
    });
}
