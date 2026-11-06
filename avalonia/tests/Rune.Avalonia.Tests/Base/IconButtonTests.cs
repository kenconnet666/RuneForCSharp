using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// IconButton 与 ButtonBase：图标按钮宽高相等、默认 ghost 中性色、读屏名称来自 AutomationProperties.Name；
/// ButtonBase 没有外观，但有波纹、焦点环、加载中拦下点击。
/// </summary>
public class IconButtonTests
{
    static Color BrushColor(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    static Color Token(string key)
    {
        Application.Current!.TryGetResource(key, ThemeVariant.Light, out var value);
        return BrushColor((IBrush?)value);
    }

    static T Part<T>(Visual control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().Single(child => child.Name == name);

    static Point Center(Visual visual, Visual root) => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root)!.Value;

    [Fact]
    public Task Icon_buttons_are_square_ghost_and_neutral() => HeadlessUi.Run(() =>
    {
        var large = new IconButton { Icon = Lucide.Settings, Size = TextSize.Lg };
        var small = new IconButton { Icon = Lucide.Settings, Size = new TextSize("8") };
        var window = HeadlessUi.Show(new StackPanel { Children = { large, small } });

        Assert.Equal((ButtonVariant.Ghost, StatusTone.Neutral, Radius.Md), (large.Variant, large.Tone, large.Radius));
        Assert.Equal(new Size(36, 36), large.Bounds.Size);   // max(2.25em, 24px)，lg = 16px
        Assert.Equal(new Size(24, 24), small.Bounds.Size);   // 8px 的 2.25em 不到 24px，取最小点击区域
        Assert.Equal(16, Part<Icon>(large, IconButtonParts.Icon).Bounds.Width); // 图标 1em
        Assert.Equal(Token("Rune.Brush.Text"), BrushColor(large.Foreground));

        window.MouseMove(Center(large, window));
        HeadlessUi.Flush(window);
        Assert.Equal(Token("Rune.Brush.FillHover"), BrushColor(large.GetBaseValue(TemplatedControl.BackgroundProperty).Value));
    });

    [Fact]
    public Task The_accessible_name_comes_from_automation_properties() => HeadlessUi.Run(() =>
    {
        var button = new IconButton { Icon = Lucide.Trash, Tone = StatusTone.Danger };
        AutomationProperties.SetName(button, "删除");
        HeadlessUi.Show(new StackPanel { Children = { button } });

        var peer = ControlAutomationPeer.CreatePeerForElement(button);
        Assert.Equal(("删除", AutomationControlType.Button), (peer.GetName(), peer.GetAutomationControlType()));
        Assert.Equal(Token("Rune.Brush.DangerText"), BrushColor(button.Foreground));
    });

    [Fact]
    public Task Presets_take_their_icon_and_name_from_the_language_pack() => HeadlessUi.Run(() =>
    {
        var close = new CloseButton();
        var clear = new ClearButton();
        var english = new StackPanel { Children = { clear } };
        Provider.SetLang(english, Lang.EnUs);
        var host = new StackPanel { Children = { close, english } };
        Provider.SetLang(host, Lang.ZhCn);
        HeadlessUi.Show(host);

        string Name(Control control) => ControlAutomationPeer.CreatePeerForElement(control).GetName();
        Assert.Equal((Lucide.X, "关闭"), (close.Icon, Name(close)));
        Assert.Equal((Lucide.CircleX, "Clear"), (clear.Icon, Name(clear))); // 内层的语言包
        AutomationProperties.SetName(close, "关闭提示");
        Assert.Equal("关闭提示", Name(close));
    });

    [Fact]
    public Task Button_base_has_no_look_but_ripples_and_blocks_clicks_while_loading() => HeadlessUi.Run(() =>
    {
        var clicks = 0;
        var button = new ButtonBase { Content = new TextBlock { Text = "个人版" }, Width = 160, Height = 60 };
        button.Click += (_, _) => clicks++;
        var window = HeadlessUi.Show(new StackPanel { Children = { button } });
        var at = Center(button, window);

        Assert.Equal((Colors.Transparent, new Thickness(0)), (BrushColor(button.Background), button.Padding));
        window.MouseDown(at, MouseButton.Left);
        Assert.IsType<Ellipse>(Assert.Single(Part<Canvas>(button, ButtonBaseParts.Ripples).Children));
        window.MouseUp(at, MouseButton.Left);
        Assert.Equal(1, clicks);

        button.IsLoading = true;
        Assert.Contains(":loading", button.Classes);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Assert.Equal(1, clicks);
    });

    [Fact]
    public Task Link_buttons_keep_the_block_padding() => HeadlessUi.Run(() =>
    {
        var link = new Button { Content = "查看文档", Variant = ButtonVariant.Link, Size = TextSize.Lg };
        HeadlessUi.Show(new StackPanel { Children = { link } });

        // TS 的 link 只去掉左右内边距与最小高度，上下 0.25em 保留
        Assert.Equal((new Thickness(0, 4), 0d), (link.Padding, link.MinHeight));
    });
}
