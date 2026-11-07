namespace Rune.WinForms.Tests;

/// <summary>
/// ProviderControl：作用域的主题、语言只作用到里面；默认值与外观按"实例 &gt; 内层 &gt; 外层 &gt; 组件默认"逐字段合并；
/// 组件换到别的作用域时按新位置重新取。
/// </summary>
public class ProviderTests
{
    static ThemeTokens Brand => BuiltinThemes.Dark;

    [Fact]
    [Upstream("Provider", "没有任何配置时使用默认值")]
    public Task Without_a_scope_components_use_the_app_level_theme_and_language() => WinFormsUi.Run(() =>
    {
        var close = new CloseButtonControl();
        using var form = WinFormsUi.Show(close);

        Assert.Null(ProviderControl.Of(close));
        Assert.Same(RuneTheme.Current.Value, ProviderControl.ThemeOf(close));
        Assert.Equal(RuneLang.Current.Value.Close, close.AccessibilityObject.Name);
    });

    [Fact]
    [Upstream("Provider", "切换语言与主题时立即更新")]
    public Task Scoped_theme_and_language_apply_inside_only_and_update_immediately() => WinFormsUi.Run(() =>
    {
        var inside = new CloseButtonControl();
        var outside = new CloseButtonControl();
        var scope = new ProviderControl { Dock = DockStyle.Top };
        scope.Controls.Add(inside);
        using var form = WinFormsUi.Show(scope, outside);

        scope.Lang = Lang.EnUs;
        scope.Theme = Brand;
        Assert.Equal(Lang.EnUs.Close, inside.AccessibilityObject.Name);
        Assert.Same(Brand, ProviderControl.ThemeOf(inside));
        Assert.Same(RuneTheme.Current.Value, ProviderControl.ThemeOf(outside)); // 作用域外不受影响
        Assert.Equal(RuneLang.Current.Value.Close, outside.AccessibilityObject.Name);

        scope.Theme = null; // 撤销：回到外层
        Assert.Same(RuneTheme.Current.Value, ProviderControl.ThemeOf(inside));
    });

    [Fact]
    [Upstream("Provider", "嵌套时内层只覆盖写到的字段")]
    public Task Nested_scopes_merge_defaults_field_by_field() => WinFormsUi.Run(() =>
    {
        var button = new ButtonControl { Text = "保存" };
        var outer = new ProviderControl { Dock = DockStyle.Fill };
        var inner = new ProviderControl { Dock = DockStyle.Fill };
        outer.Defaults.Set(new ButtonDefaults { Variant = ButtonVariant.Outline, Radius = Radius.Full });
        inner.Defaults.Set(new ButtonDefaults { Variant = ButtonVariant.Ghost });
        inner.Controls.Add(button);
        outer.Controls.Add(inner);
        using var form = WinFormsUi.Show(outer);

        // 内层只写了 Variant：Radius 沿用外层，Tone 是组件默认
        Assert.Equal((ButtonVariant.Ghost, Radius.Full, StatusTone.Accent), (button.Variant, button.Radius, button.Tone));
    });

    [Fact]
    [Upstream("useDefaults", "逐项合并：显式 prop > 最近的 Provider")]
    public Task Explicit_values_beat_the_scope_which_beats_component_defaults() => WinFormsUi.Run(() =>
    {
        var plain = new BadgeControl { Text = "默认" };
        var explicitSoft = new BadgeControl { Text = "显式", Variant = StatusVariant.Soft }; // 显式写成组件默认值也要赢过作用域
        var scope = new ProviderControl { Dock = DockStyle.Fill };
        scope.Defaults.Set(new BadgeDefaults { Variant = StatusVariant.Solid, Tone = StatusTone.Danger });
        scope.Controls.AddRange([plain, explicitSoft]);
        using var form = WinFormsUi.Show(scope);

        Assert.Equal((StatusVariant.Solid, StatusTone.Danger), (plain.Variant, plain.Tone));
        Assert.Equal((StatusVariant.Soft, StatusTone.Danger), (explicitSoft.Variant, explicitSoft.Tone));
        Assert.Equal(Radius.Sm, plain.Radius); // 哪层都没写：组件默认
    });

    [Fact]
    public Task Link_defaults_come_from_the_scope() => WinFormsUi.Run(() =>
    {
        var link = new LinkControl { Text = "文档" };
        var scope = new ProviderControl { Dock = DockStyle.Fill };
        scope.Defaults.Set(new LinkDefaults { Underline = LinkUnderline.Hover, Tone = TextTone.Neutral });
        scope.Controls.Add(link);
        using var form = WinFormsUi.Show(scope);

        Assert.Equal((LinkUnderline.Hover, TextTone.Neutral), (link.Underline, link.Tone));
    });

    [Fact]
    public Task Display_feedback_and_choice_components_take_their_defaults_from_the_scope() => WinFormsUi.Run(() =>
    {
        var indicator = new IndicatorControl();
        var avatar = new AvatarControl { Text = "林" };
        var card = new CardControl { Radius = Radius.None }; // 显式写的仍然赢
        var progress = new ProgressControl { Value = 40 };
        var empty = new EmptyControl();
        var toggle = new SwitchControl { Text = "通知" };
        var scope = new ProviderControl { Dock = DockStyle.Fill };
        scope.Defaults.Set(new IndicatorDefaults { Tone = StatusTone.Success });
        scope.Defaults.Set(new AvatarDefaults { Variant = AvatarVariant.Solid, Radius = Radius.Md });
        scope.Defaults.Set(new CardDefaults { Variant = CardVariant.Outline, Padding = Space.Sm, Radius = Radius.Sm });
        scope.Defaults.Set(new ProgressDefaults { Tone = StatusTone.Warning });
        scope.Defaults.Set(new EmptyDefaults { Size = TextSize.Sm });
        scope.Defaults.Set(new ChoiceDefaults { Tone = StatusTone.Danger, Size = TextSize.Lg });
        scope.Controls.AddRange([indicator, avatar, card, progress, empty, toggle]);
        using var form = WinFormsUi.Show(scope);

        Assert.Equal(StatusTone.Success, indicator.Tone);
        Assert.Equal((AvatarVariant.Solid, Radius.Md, StatusTone.Accent), (avatar.Variant, avatar.Radius, avatar.Tone));
        Assert.Equal((CardVariant.Outline, Space.Sm, Radius.None), (card.Variant, card.CardPadding, card.Radius));
        Assert.Equal((StatusTone.Warning, Radius.Full), (progress.Tone, progress.Radius));
        Assert.Equal(TextSize.Sm, empty.TextSize);
        Assert.Equal((StatusTone.Danger, TextSize.Lg), (toggle.Tone, toggle.TextSize));
    });

    [Fact]
    public Task Empty_re_applies_a_size_that_comes_from_the_scope() => WinFormsUi.Run(() =>
    {
        var empty = new EmptyControl();
        var scope = new ProviderControl { Dock = DockStyle.Fill };
        scope.Controls.Add(empty);
        using var form = WinFormsUi.Show(scope);
        var before = empty.Font.Size;

        scope.Defaults.Set(new EmptyDefaults { Size = TextSize.Xl });
        Assert.True(empty.Font.Size > before); // 作用域换了字号档位，空状态的字体跟着换
    });

    [Fact]
    public Task Changing_scope_defaults_resizes_the_components() => WinFormsUi.Run(() =>
    {
        var button = new ButtonControl { Text = "保存" };
        var scope = new ProviderControl { Dock = DockStyle.Fill };
        scope.Controls.Add(button);
        using var form = WinFormsUi.Show(scope);
        var before = button.Height;

        scope.Defaults.Set(new ButtonDefaults { Size = TextSize.Xl });
        Assert.True(button.Height > before); // 作用域的字号档位变大，按钮跟着变高
    });

    [Fact]
    public Task Moving_a_component_into_another_scope_re_resolves_it() => WinFormsUi.Run(() =>
    {
        var button = new ButtonControl { Text = "保存" };
        var (a, b) = (new ProviderControl { Dock = DockStyle.Left }, new ProviderControl { Dock = DockStyle.Fill });
        a.Defaults.Set(new ButtonDefaults { Variant = ButtonVariant.Outline });
        b.Defaults.Set(new ButtonDefaults { Variant = ButtonVariant.Soft });
        a.Controls.Add(button);
        using var form = WinFormsUi.Show(a, b);

        Assert.Equal(ButtonVariant.Outline, button.Variant);
        b.Controls.Add(button);
        Assert.Equal(ButtonVariant.Soft, button.Variant);
    });

    [Fact]
    public Task Instance_sx_beats_scope_styles_which_beat_the_recipe() => WinFormsUi.Run(() =>
    {
        static Color Pixel(Control control)
        {
            using var bitmap = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
            return bitmap.GetPixel(control.Width / 2, 6);
        }
        var plain = new ButtonControl { Text = "取消", Width = 120 };
        var styled = new ButtonControl { Text = "保存", Width = 120, Sx = new ButtonSx { Background = Color.FromArgb(0, 128, 0) } };
        var scope = new ProviderControl { Dock = DockStyle.Fill };
        scope.Styles.Set(new ButtonSx { Background = Color.FromArgb(200, 0, 0), Radius = Radius.None });
        scope.Controls.AddRange([plain, styled]);
        using var form = WinFormsUi.Show(scope);

        Assert.Equal(Color.FromArgb(200, 0, 0).ToArgb(), Pixel(plain).ToArgb()); // 作用域的外观盖过配方
        Assert.Equal(Color.FromArgb(0, 128, 0).ToArgb(), Pixel(styled).ToArgb()); // 实例的 Sx 盖过作用域
        Assert.Equal(Radius.Md, styled.Radius); // Sx 不改档位本身，只改画出来的
    });

    [Fact]
    [Upstream("Provider", "defineTheme 的局部修改")]
    public Task A_scoped_theme_colors_only_the_components_inside() => WinFormsUi.Run(() =>
    {
        static Color Pixel(Control control)
        {
            using var bitmap = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
            return bitmap.GetPixel(control.Width / 2, 6);
        }
        var inside = new ButtonControl { Text = "保存", Width = 120 };
        var outside = new ButtonControl { Text = "保存", Width = 120, Dock = DockStyle.Bottom };
        var scope = new ProviderControl { Theme = Brand, Dock = DockStyle.Top };
        scope.Controls.Add(inside);
        using var form = WinFormsUi.Show(scope, outside);

        Assert.Equal(Brand.Colors.Accent.ToArgb(), Pixel(inside).ToArgb()); // 实底按钮取作用域主题的强调色
        Assert.Equal(RuneTheme.Current.Value.Colors.Accent.ToArgb(), Pixel(outside).ToArgb());
    });
}
