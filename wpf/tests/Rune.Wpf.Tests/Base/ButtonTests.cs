using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Rune.Wpf.Tests;

/// <summary>
/// Button：生成的配方触发器给颜色、尺寸按 em、胶囊圆角不超过高度的一半、加载中点击无效且指示防闪烁、
/// 按下出波纹、Provider 的部件样式（含给所有 Icon 的样式）进得了模板里的图标。
/// </summary>
public class ButtonTests
{
    static (StackPanel Host, RuneTheme Theme) Host(params UIElement[] children)
    {
        var host = new StackPanel();
        var theme = new RuneTheme();
        host.Resources.MergedDictionaries.Add(theme);
        foreach (var child in children) host.Children.Add(child);
        return (host, theme);
    }

    static Color BrushColor(Brush? brush) => Assert.IsType<SolidColorBrush>(brush, exactMatch: false).Color;

    static T Part<T>(Rune.Wpf.Button button, string name) => (T)button.Template.FindName(name, button);

    [Fact]
    public Task Recipe_triggers_give_variant_and_tone_colors() => WpfUi.Run(() =>
    {
        var solid = new Rune.Wpf.Button { Content = "确定" };
        var outline = new Rune.Wpf.Button { Content = "取消", Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral };
        var (host, _) = Host(solid, outline);
        WpfUi.Show(host);

        Assert.Equal((ButtonVariant.Solid, StatusTone.Accent, Radius.Md), (solid.Variant, solid.Tone, solid.Radius));
        Assert.Equal(BrushColor((Brush)host.FindResource("Rune.Brush.Accent")), BrushColor(solid.Background));
        Assert.Equal(BrushColor((Brush)host.FindResource("Rune.Brush.AccentContrast")), BrushColor(solid.Foreground));
        Assert.Equal(BrushColor((Brush)host.FindResource("Rune.Brush.Border")), BrushColor(outline.BorderBrush));
        Assert.Equal(BaseValueSource.DefaultStyleTrigger, DependencyPropertyHelper.GetValueSource(solid, Control.BackgroundProperty).BaseValueSource);
    });

    [Fact]
    public Task Sizes_are_em_and_full_radius_is_a_pill() => WpfUi.Run(() =>
    {
        var large = new Rune.Wpf.Button { Content = "lg", Size = TextSize.Lg };
        var pill = new Rune.Wpf.Button { Content = "full", Radius = Radius.Full };
        WpfUi.Show(Host(large, pill).Host);

        Assert.Equal(16, large.FontSize);
        Assert.Equal(new Thickness(16, 4, 16, 4), large.Padding); // 1em × 0.25em
        Assert.Equal(36, large.MinHeight);                         // 控件高度：round(2.25em, 4px)
        Assert.Equal(new CornerRadius(0.4 * 16), large.CornerRadius);
        Assert.Equal(pill.ActualHeight / 2, pill.CornerRadius.TopLeft, 3);
    });

    [Upstream("Button", "loading 变为 true")]
    [Upstream("Button", "loading：不用 disabled")]
    [Fact]
    public Task Loading_blocks_clicks_and_shows_the_spinner_after_a_delay() => WpfUi.Run(() =>
    {
        var clicks = 0;
        var button = new Rune.Wpf.Button { Content = "保存" };
        button.Click += (_, _) => clicks++;
        WpfUi.Show(Host(button).Host);
        var invoke = (System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(button).GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke);

        button.Focus();
        button.IsLoading = true;
        invoke.Invoke();
        WpfUi.Flush();
        Assert.Equal(0, clicks);
        // 不用不可用：焦点保留、读屏照常能找到它，状态念"加载中"
        Assert.True(button.IsEnabled && button.IsFocused);
        Assert.Equal(Provider.GetLang(button).Loading, UIElementAutomationPeer.CreatePeerForElement(button).GetItemStatus());
        Assert.False(button.ShowsSpinner); // 200 毫秒内不出现

        WpfUi.Wait(TimeSpan.FromMilliseconds(350));
        Assert.True(button.ShowsSpinner);
        Assert.Equal(0, Part<StackPanel>(button, "PART_Content").Opacity);
        Assert.Equal(Visibility.Visible, Part<Spinner>(button, "PART_Spinner").Visibility);

        button.IsLoading = false;
        invoke.Invoke();
        WpfUi.Flush();
        Assert.Equal(1, clicks);
        Assert.True(button.ShowsSpinner); // 出现了至少显示 0.5 秒
        WpfUi.Wait(TimeSpan.FromMilliseconds(600));
        Assert.False(button.ShowsSpinner);
    });

    [Fact]
    public Task Pressing_starts_a_ripple_of_the_text_color() => WpfUi.Run(() =>
    {
        var button = new Rune.Wpf.Button { Content = "发布", Width = 120 };
        WpfUi.Show(Host(button).Host);
        var layer = Part<Canvas>(button, "PART_Ripples");

        button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
        var ripple = Assert.IsType<Ellipse>(Assert.Single(layer.Children.OfType<Ellipse>()));
        var fill = Assert.IsType<SolidColorBrush>(ripple.Fill);
        Assert.Equal((BrushColor(button.Foreground), 0.22), (fill.Color, fill.Opacity));
        Assert.NotNull(layer.Clip); // 裁成按钮的圆角
    });

    [Upstream("Button", "type 默认 button")]
    [Fact]
    public Task Provider_styles_reach_template_icons() => WpfUi.Run(() =>
    {
        const string Xaml = """
            <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf">
              <StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><rune:RuneTheme /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>
              <rune:Provider>
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style TargetType="rune:Icon"><Setter Property="StrokeWidth" Value="3" /></Style>
                    <Style x:Key="{x:Static rune:ButtonParts.IconEnd}" TargetType="rune:Icon"><Setter Property="StrokeWidth" Value="1" /></Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
                <rune:Button x:Name="button" Icon="{x:Static rune:Lucide.Plus}" IconEnd="{x:Static rune:Lucide.ChevronDown}">新建</rune:Button>
              </rune:Provider>
            </StackPanel>
            """;
        var root = (FrameworkElement)XamlReader.Parse(Xaml);
        WpfUi.Show(root);
        var button = (Rune.Wpf.Button)root.FindName("button");

        Assert.Equal(3, Part<Icon>(button, "PART_Icon").StrokeWidth);
        Assert.Equal(1, Part<Icon>(button, "PART_IconEnd").StrokeWidth);
        Assert.Equal(Lucide.Plus, Part<Icon>(button, "PART_Icon").Data);
    });

    [Fact]
    public Task Control_heights_sit_on_a_4px_grid_and_xs_passes_its_factor_inside() => WpfUi.Run(() =>
    {
        // vue-tsx-pro 0.11：字号的 2.25 倍（xs 档 2 倍）取整到 4px → xs–xl 依次 24 / 28 / 32 / 36 / 40
        var steps = new[] { TextSize.Xs, TextSize.Sm, TextSize.Md, TextSize.Lg, TextSize.Xl }.Select(size => new Rune.Wpf.Button { Content = size.Value, Size = size }).ToList();
        // 外面的容器写了 xs 的系数，里面没写档位的按钮跟着它（同 CSS 变量的继承）
        var inner = new Rune.Wpf.Button { Content = "里面" };
        var toolbar = new StackPanel { Children = { inner } };
        TextElement.SetFontSize(toolbar, 12);
        ControlHeight.SetFactor(toolbar, ControlHeight.XsFactor);
        var (host, _) = Host([.. steps, toolbar]);
        WpfUi.Show(host);

        Assert.Equal([24d, 28, 32, 36, 40], steps.Select(button => button.MinHeight));
        Assert.Equal(24, inner.MinHeight);
        Assert.Equal((host.FindResource("Rune.Size.Control"), host.FindResource("Rune.Size.ControlRound"), host.FindResource("Rune.Size.MinTarget")),
            ((object)ControlHeight.DefaultFactor, (object)ControlHeight.Step, (object)ControlHeight.MinTarget));
    });

    [Upstream("Button", "disabled 用原生属性")]
    [Upstream("IconButton", "三种按钮共用一套行为")]
    [Fact]
    public Task Focusable_when_disabled_keeps_focus_but_blocks_clicks_and_hover() => WpfUi.Run(() =>
    {
        var clicks = 0;
        var plain = new Rune.Wpf.Button { Content = "提交", IsEnabled = false };
        var soft = new Rune.Wpf.Button { Content = "注销账号", IsEnabled = false, FocusableWhenDisabled = true };
        var icon = new IconButton { Icon = Lucide.Trash, IsEnabled = false, FocusableWhenDisabled = true };
        var link = new LinkButton { Content = "文档", IsEnabled = false, FocusableWhenDisabled = true };
        foreach (var button in new ButtonBase[] { plain, soft, icon, link }) button.Click += (_, _) => clicks++;
        var (host, _) = Host(plain, soft, icon, link);
        WpfUi.Show(host);

        // 原生的不可用：不能聚焦；可聚焦的不可用：能聚焦（留在 Tab 顺序里），外观同不可用，读屏念不可用
        Assert.False(plain.Focus());
        Assert.True(soft.Focus());
        Assert.True(soft.IsDisabled);
        Assert.Equal(plain.Opacity, soft.Opacity);
        Assert.False(UIElementAutomationPeer.CreatePeerForElement(soft).IsEnabled());

        // 键盘、鼠标都经过 OnClick，在那里拦下；读屏的 Invoke 报"不可用"
        var onClick = typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        foreach (var button in new ButtonBase[] { soft, icon, link })
        {
            onClick.Invoke(button, null);
            var invoke = (System.Windows.Automation.Provider.IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button).GetPattern(PatternInterface.Invoke);
            Assert.Throws<System.Windows.Automation.ElementNotEnabledException>(invoke.Invoke);
        }
        Assert.Equal(0, clicks);

        // 外面不可用时照样不可用；恢复可用后照常
        soft.IsEnabled = true;
        Assert.False(soft.IsDisabled);
        host.IsEnabled = false;
        Assert.True(soft.IsDisabled);
        Assert.False(soft.IsEnabled);
    });
}
