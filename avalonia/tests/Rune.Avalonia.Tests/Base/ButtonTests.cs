using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Button：调色板给颜色（含悬停）、默认值在主题里、尺寸按 em、加载中点击无效且指示防闪烁、按下出波纹、部件能用 /template/ 样式改。
/// </summary>
public class ButtonTests
{
    static Color BrushColor(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    static Color Token(string key)
    {
        Application.Current!.TryGetResource(key, ThemeVariant.Light, out var value);
        return BrushColor((IBrush?)value);
    }

    static T Part<T>(Button button, string name) where T : Control =>
        button.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    static Point Center(Visual visual, Visual root) => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root)!.Value;

    [Fact]
    public Task Palette_gives_variant_colors_and_hover() => HeadlessUi.Run(() =>
    {
        var solid = new Button { Content = "确定" };
        var outline = new Button { Content = "取消", Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral };
        var window = HeadlessUi.Show(new StackPanel { Children = { solid, outline } });

        Assert.Equal((ButtonVariant.Solid, StatusTone.Accent, Radius.Md), (solid.Variant, solid.Tone, solid.Radius));
        Assert.Equal(Token("Rune.Brush.Accent"), BrushColor(solid.Background));
        Assert.Equal(Token("Rune.Brush.AccentContrast"), BrushColor(solid.Foreground));
        Assert.Equal(Token("Rune.Brush.Border"), BrushColor(outline.BorderBrush));

        window.MouseMove(Center(solid, window));
        HeadlessUi.Flush(window);
        // 基础值是悬停色（不含颜色过渡的中间值）
        Assert.Equal(Token("Rune.Brush.AccentHover"), BrushColor(solid.GetBaseValue(Button.BackgroundProperty).Value));
    });

    [Upstream("Button", "组件默认值")]
    [Fact]
    public Task Sizes_are_em_and_styles_can_change_defaults() => HeadlessUi.Run(() =>
    {
        var large = new Button { Content = "lg", Size = TextSize.Lg };
        var styled = new Button { Content = "默认" };
        var host = new StackPanel { Children = { large, new Border { Child = styled } } };
        host.Children[1].Styles.Add(new Style(x => x.OfType<Button>()) { Setters = { new Setter(Button.VariantProperty, ButtonVariant.Ghost) } });
        HeadlessUi.Show(host);

        Assert.Equal(16, large.FontSize);
        Assert.Equal(new Thickness(16, 4), large.Padding);           // 1em × 0.25em
        Assert.Equal(36, large.MinHeight);                           // 控件高度：round(2.25em, 4px)
        Assert.Equal(new CornerRadius(0.4 * 16), large.CornerRadius); // radii.md = 0.4em
        Assert.Equal(ButtonVariant.Ghost, styled.Variant);
    });

    [Upstream("Button", "loading 变为 true")]
    [Upstream("Button", "loading：不用 disabled")]
    [Fact]
    public Task Loading_blocks_clicks_and_shows_the_spinner_after_a_delay() => HeadlessUi.Run(() =>
    {
        var clicks = 0;
        var button = new Button { Content = "保存" };
        button.Click += (_, _) => clicks++;
        var window = HeadlessUi.Show(new StackPanel { Children = { button } });
        var at = Center(button, window);

        button.Focus();
        button.IsLoading = true;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Assert.Equal(0, clicks);
        // 不用不可用：焦点保留、读屏照常能找到它，状态念"加载中"
        Assert.True(button.IsEnabled && button.IsFocused);
        Assert.Equal(Provider.GetLang(button).Loading, ControlAutomationPeer.CreatePeerForElement(button).GetItemStatus());
        Assert.DoesNotContain(":spinner", button.Classes); // 200 毫秒内不出现

        Pump(TimeSpan.FromMilliseconds(350));
        Assert.Contains(":spinner", button.Classes);
        Assert.True(Part<Spinner>(button, ButtonParts.Spinner).IsVisible);
        Assert.Equal(0, Part<StackPanel>(button, ButtonParts.Content).Opacity); // 内容透明，宽度不变

        button.IsLoading = false;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Assert.Equal(1, clicks);
        Assert.Contains(":spinner", button.Classes); // 出现了至少显示 0.5 秒
        Pump(TimeSpan.FromMilliseconds(600));
        Assert.DoesNotContain(":spinner", button.Classes);
    });

    /// <summary>边等真实时间边处理投递回 UI 线程的任务（DelayedLoading 用 Task.Delay 等待）。</summary>
    static void Pump(TimeSpan duration)
    {
        for (var clock = System.Diagnostics.Stopwatch.StartNew(); clock.Elapsed < duration;)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public Task Pressing_starts_a_ripple_of_the_text_color() => HeadlessUi.Run(() =>
    {
        var button = new Button { Content = "发布", Width = 120 };
        var window = HeadlessUi.Show(new StackPanel { Children = { button } });
        var layer = Part<Canvas>(button, "PART_Ripples");

        window.MouseDown(layer.TranslatePoint(new Point(10, 10), window)!.Value, MouseButton.Left);
        var ripple = Assert.IsType<Ellipse>(Assert.Single(layer.Children));
        // 圆心到最远的角：扩散完正好盖满整个按钮
        var radius = Math.Sqrt(Math.Pow(layer.Bounds.Width - 10, 2) + Math.Pow(layer.Bounds.Height - 10, 2));
        Assert.Equal(2 * radius, ripple.Width, 3);
        var fill = Assert.IsAssignableFrom<ISolidColorBrush>(ripple.Fill);
        Assert.Equal((BrushColor(button.Foreground), 0.22), (fill.Color, fill.Opacity));

        // 关掉波纹的按钮不出
        button.Ripple = false;
        window.MouseUp(layer.TranslatePoint(new Point(10, 10), window)!.Value, MouseButton.Left);
        window.MouseDown(layer.TranslatePoint(new Point(10, 10), window)!.Value, MouseButton.Left);
        Assert.Single(layer.Children);
    });

    [Upstream("Button", "type 默认 button")]
    [Fact]
    public Task Parts_take_template_styles() => HeadlessUi.Run(() =>
    {
        var button = new Button { Content = "新建", Icon = Lucide.Plus, Size = TextSize.Xl };
        var host = new StackPanel { Children = { button } };
        host.SetValue(TextElement.FontSizeProperty, 14d);
        host.Styles.Add(new Style(x => x.OfType<Button>().Template().OfType<Icon>().Name(ButtonParts.Icon)) { Setters = { new Setter(Icon.StrokeWidthProperty, 3d) } });
        HeadlessUi.Show(host);

        var icon = Part<Icon>(button, ButtonParts.Icon);
        Assert.Equal((3d, Lucide.Plus, true), (icon.StrokeWidth, icon.Data, icon.IsVisible));
        Assert.Equal(18, icon.Bounds.Width); // 1em，跟随按钮的字号
        Assert.False(Part<Icon>(button, ButtonParts.IconEnd).IsVisible);
    });

    [Upstream("Button", "使用方的 sx")]
    [Fact]
    public Task Plain_type_styles_override_the_tone_palette_and_radius() => HeadlessUi.Run(() =>
    {
        // 色调解析出的调色板、圆角档位在主题这一层（Resolved）：只按类型选的样式也盖得过（vue-tsx-pro 的"定制段盖过组件段"）
        Application.Current!.TryGetResource("Rune.Palette.Danger", ThemeVariant.Light, out var danger);
        var button = new Button { Content = "删除" };
        var host = new StackPanel { Children = { button } };
        host.Styles.Add(new Style(x => x.OfType<Button>())
        {
            Setters = { new Setter(ButtonCore.PaletteProperty, danger), new Setter(Button.CornerRadiusProperty, new CornerRadius(3)) },
        });
        HeadlessUi.Show(host);

        Assert.Same(danger, button.Palette);
        Assert.Equal(Token("Rune.Brush.Danger"), BrushColor(button.Background));
        Assert.Equal(new CornerRadius(3), button.CornerRadius);
    });

    [Fact]
    public Task Class_styles_override_the_size_step() => HeadlessUi.Run(() =>
    {
        // 字号是继承属性，档位按模板优先级绑定：带类的样式盖得过，只按类型选的盖不过（DESIGN §4"档位的优先级"）
        var classed = new Button { Content = "大", Size = TextSize.Xs, Classes = { "big" } };
        var plain = new Button { Content = "小", Size = TextSize.Xs };
        var host = new StackPanel { Children = { classed, plain } };
        host.Styles.Add(new Style(x => x.OfType<Button>().Class("big")) { Setters = { new Setter(Button.FontSizeProperty, 30d) } });
        host.Styles.Add(new Style(x => x.OfType<Button>()) { Setters = { new Setter(Button.FontSizeProperty, 40d) } });
        HeadlessUi.Show(host);

        Assert.Equal(30, classed.FontSize);
        Application.Current!.TryGetResource("Rune.FontSize.Xs", ThemeVariant.Light, out var xs);
        Assert.Equal(xs, plain.FontSize);
    });

    [Fact]
    public Task Control_heights_sit_on_a_4px_grid_and_xs_passes_its_factor_inside() => HeadlessUi.Run(() =>
    {
        // vue-tsx-pro 0.11：字号的 2.25 倍（xs 档 2 倍）取整到 4px → xs–xl 依次 24 / 28 / 32 / 36 / 40
        var steps = new[] { TextSize.Xs, TextSize.Sm, TextSize.Md, TextSize.Lg, TextSize.Xl }.Select(size => new Button { Content = size.Value, Size = size }).ToList();
        // 外面的容器写了 xs 的系数，里面没写档位的按钮跟着它（同 CSS 变量的继承）
        var inner = new Button { Content = "里面" };
        var toolbar = new StackPanel { Children = { inner } };
        toolbar.SetValue(TextElement.FontSizeProperty, 12d);
        ControlHeight.SetFactor(toolbar, ControlHeight.XsFactor);
        var host = new StackPanel();
        host.Children.AddRange(steps);
        host.Children.Add(toolbar);
        HeadlessUi.Show(host);

        Assert.Equal([24d, 28, 32, 36, 40], steps.Select(button => button.MinHeight));
        Assert.Equal(24, inner.MinHeight);
    });

    [Fact]
    public Task Control_height_constants_match_the_tokens() => HeadlessUi.Run(() =>
    {
        double Token(string key) => Application.Current!.TryGetResource(key, ThemeVariant.Light, out var value) ? (double)value! : double.NaN;
        Assert.Equal((Token("Rune.Size.Control"), Token("Rune.Size.ControlRound"), Token("Rune.Size.MinTarget")), (ControlHeight.DefaultFactor, ControlHeight.Step, ControlHeight.MinTarget));
    });

    [Upstream("Button", "disabled 用原生属性")]
    [Upstream("IconButton", "三种按钮共用一套行为")]
    [Fact]
    public Task Focusable_when_disabled_keeps_focus_but_blocks_clicks_and_hover() => HeadlessUi.Run(() =>
    {
        var clicks = 0;
        var plain = new Button { Content = "提交", IsEnabled = false };
        var soft = new Button { Content = "注销账号", IsEnabled = false, FocusableWhenDisabled = true };
        var icon = new IconButton { Icon = Lucide.Trash, IsEnabled = false, FocusableWhenDisabled = true };
        var link = new LinkButton { Content = "文档", IsEnabled = false, FocusableWhenDisabled = true };
        foreach (var button in new ButtonBase[] { plain, soft, icon, link }) button.Click += (_, _) => clicks++;
        var window = HeadlessUi.Show(new StackPanel { Children = { plain, soft, icon, link } });

        // 原生的不可用：不能聚焦；可聚焦的不可用：能聚焦（留在 Tab 顺序里），外观同不可用，读屏念不可用
        Assert.False(plain.Focus());
        Assert.True(soft.Focus());
        Assert.Contains(":soft-disabled", soft.Classes);
        Assert.Equal(plain.Opacity, soft.Opacity);
        Assert.False(ControlAutomationPeer.CreatePeerForElement(soft).IsEnabled());

        // 键盘、点击都无效（链接也不打开），悬停不变色
        var background = soft.Background;
        foreach (ButtonBase button in new ButtonBase[] { soft, icon, link })
        {
            button.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.MouseMove(Center(button, window));
            window.MouseDown(Center(button, window), MouseButton.Left);
            window.MouseUp(Center(button, window), MouseButton.Left);
        }
        window.MouseMove(Center(soft, window));
        HeadlessUi.Flush(window);
        Assert.Equal(0, clicks);
        Assert.Same(background, soft.Background);

        // 恢复可用后照常
        soft.IsEnabled = true;
        window.MouseDown(Center(soft, window), MouseButton.Left);
        window.MouseUp(Center(soft, window), MouseButton.Left);
        Assert.Equal(1, clicks);
        Assert.DoesNotContain(":soft-disabled", soft.Classes);
    });
}
