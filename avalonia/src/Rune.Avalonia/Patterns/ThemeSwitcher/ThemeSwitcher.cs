using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace Rune.Avalonia;

/// <summary>
/// 主题切换（vue-tsx-pro 的 ThemeSwitcher）：图标按钮弹出菜单，列出"跟随系统"与各套主题。
/// <code>
/// &lt;rune:ThemeSwitcher Themes="{x:Static rune:RuneThemeVariants.All}" /&gt;   &lt;!-- 选中后改应用的 RequestedThemeVariant --&gt;
/// &lt;rune:ThemeSwitcher Value="{Binding ThemeVariant, ElementName=Scope}" AppliesToApplication="False" /&gt;   &lt;!-- 只改一块 --&gt;
/// </code>
/// <see cref="Value"/> 是当前的选择（ThemeVariant.Default 是跟随系统，默认双向绑定）；选中一项时写回，<see cref="AppliesToApplication"/>（默认 true）时同时设到
/// Application.Current.RequestedThemeVariant。按钮的图标表示当前的选择；内置主题（<see cref="RuneThemeVariants"/>）与跟随系统有默认的文字（语言包）和图标，
/// 自己的主题用 <see cref="Labels"/>、<see cref="Icons"/>（按主题变体的 Key）补上。上游选中后从按钮处圆形展开（View Transitions），桌面上直接切换；
/// 记住选择（上游 createThemePreference 存在 localStorage）由应用自己存 Value。部件：<see cref="ThemeSwitcherParts"/>。
/// </summary>
public class ThemeSwitcher : Control
{
    /// <summary>列出的主题，默认亮色、暗色；全部内置主题是 <see cref="RuneThemeVariants.All"/>。</summary>
    public static readonly StyledProperty<IReadOnlyList<ThemeVariant>> ThemesProperty =
        AvaloniaProperty.Register<ThemeSwitcher, IReadOnlyList<ThemeVariant>>(nameof(Themes), [ThemeVariant.Light, ThemeVariant.Dark]);

    /// <summary>当前的选择（默认双向绑定）：ThemeVariant.Default 是跟随系统。</summary>
    public static readonly StyledProperty<ThemeVariant> ValueProperty =
        AvaloniaProperty.Register<ThemeSwitcher, ThemeVariant>(nameof(Value), ThemeVariant.Default, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>选中时同时设到应用（Application.Current.RequestedThemeVariant），默认 true。</summary>
    public static readonly StyledProperty<bool> AppliesToApplicationProperty = AvaloniaProperty.Register<ThemeSwitcher, bool>(nameof(AppliesToApplication), true);

    /// <summary>列出"跟随系统"，默认 true。</summary>
    public static readonly StyledProperty<bool> IncludeSystemProperty = AvaloniaProperty.Register<ThemeSwitcher, bool>(nameof(IncludeSystem), true);

    /// <summary>各选项的文字（键是主题变体的 Key）；内置主题与跟随系统默认取语言包，其他默认是 Key。</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<object, string>?> LabelsProperty =
        AvaloniaProperty.Register<ThemeSwitcher, IReadOnlyDictionary<object, string>?>(nameof(Labels));

    /// <summary>各选项的图标（键是主题变体的 Key）；内置主题与跟随系统有默认图标，其他默认是调色板。</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<object, IconData>?> IconsProperty =
        AvaloniaProperty.Register<ThemeSwitcher, IReadOnlyDictionary<object, IconData>?>(nameof(Icons));

    /// <summary>菜单开着（默认双向绑定，同 Menu）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = Menu.IsOpenProperty.AddOwner<ThemeSwitcher>();

    /// <summary>菜单的弹出位置，同 Menu。</summary>
    public static readonly StyledProperty<FloatingPlacement> PlacementProperty = Menu.PlacementProperty.AddOwner<ThemeSwitcher>();

    /// <summary>菜单的字号档位，同 Menu。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = Menu.SizeProperty.AddOwner<ThemeSwitcher>();

    /// <summary>菜单项按下时的波纹，同 Menu。</summary>
    public static readonly StyledProperty<bool?> RippleProperty = Menu.RippleProperty.AddOwner<ThemeSwitcher>();

    /// <summary>用户开、关了菜单（同 Menu 的 OpenChanged）。</summary>
    public static readonly RoutedEvent<OpenChangedEventArgs> OpenChangedEvent = RoutedEvent.Register<ThemeSwitcher, OpenChangedEventArgs>(nameof(OpenChanged), RoutingStrategies.Direct);

    /// <summary>跟随系统在菜单里的值（单选组的值是字符串）。</summary>
    const string SystemKey = "system";

    // 每套内置主题的默认图标（同上游 DEFAULT_ICONS）
    static readonly Dictionary<object, IconData> DefaultIcons = new()
    {
        [SystemKey] = Lucide.Monitor,
        [ThemeVariant.Light.Key] = Lucide.Sun,
        [ThemeVariant.Dark.Key] = Lucide.Moon,
        [RuneThemeVariants.Paper.Key] = Lucide.ScrollText,
        [RuneThemeVariants.Midnight.Key] = Lucide.MoonStar,
        [RuneThemeVariants.HighContrast.Key] = Lucide.Contrast,
        [RuneThemeVariants.HighContrastDark.Key] = Lucide.Eclipse,
        [RuneThemeVariants.Forest.Key] = Lucide.Trees,
        [RuneThemeVariants.Lemon.Key] = Lucide.Citrus,
        [RuneThemeVariants.Sakura.Key] = Lucide.Flower2,
        [RuneThemeVariants.Festive.Key] = Lucide.PartyPopper,
        [RuneThemeVariants.Cyberpunk.Key] = Lucide.Zap,
        [RuneThemeVariants.BigScreen.Key] = Lucide.ChartNoAxesCombined,
        [RuneThemeVariants.Lavender.Key] = Lucide.Flower,
        [RuneThemeVariants.Ember.Key] = Lucide.Flame,
        [RuneThemeVariants.Mono.Key] = Lucide.Feather,
        [RuneThemeVariants.Celadon.Key] = Lucide.Amphora,
    };

    readonly IconButton trigger = new() { Name = ThemeSwitcherParts.Trigger };
    readonly MenuRadioGroup group = new();
    readonly Menu menu;
    bool syncing;

    /// <summary>新建：图标按钮与单选菜单。</summary>
    public ThemeSwitcher()
    {
        menu = new Menu { Trigger = trigger, Items = { group } };
        // 上游的其余属性落到菜单上：开关、位置、字号、波纹接到里面的菜单
        menu[!!Menu.IsOpenProperty] = this[!!IsOpenProperty];
        menu[!Menu.PlacementProperty] = this[!PlacementProperty];
        menu[!Menu.SizeProperty] = this[!SizeProperty];
        menu[!Menu.RippleProperty] = this[!RippleProperty];
        menu.OpenChanged += (_, e) => RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, e.IsOpen, e.Reason));
        group.PropertyChanged += (_, e) =>
        {
            if (!syncing && e.Property == MenuRadioGroup.ValueProperty && group.Value is { } key) Choose(key);
        };
        LogicalChildren.Add(menu);
        VisualChildren.Add(menu);
        Rebuild();
    }

    /// <summary>列出的主题。</summary>
    public IReadOnlyList<ThemeVariant> Themes { get => GetValue(ThemesProperty); set => SetValue(ThemesProperty, value); }

    /// <summary>当前的选择。</summary>
    public ThemeVariant Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>选中时同时设到应用。</summary>
    public bool AppliesToApplication { get => GetValue(AppliesToApplicationProperty); set => SetValue(AppliesToApplicationProperty, value); }

    /// <summary>列出"跟随系统"。</summary>
    public bool IncludeSystem { get => GetValue(IncludeSystemProperty); set => SetValue(IncludeSystemProperty, value); }

    /// <summary>各选项的文字。</summary>
    public IReadOnlyDictionary<object, string>? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>各选项的图标。</summary>
    public IReadOnlyDictionary<object, IconData>? Icons { get => GetValue(IconsProperty); set => SetValue(IconsProperty, value); }

    /// <summary>菜单开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>菜单的弹出位置。</summary>
    public FloatingPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>菜单的字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>菜单项按下时的波纹。</summary>
    public bool? Ripple { get => GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    /// <summary>用户开、关了菜单。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary>菜单里的单选组（测试用）。</summary>
    internal MenuRadioGroup Group => group;

    /// <summary>主题变体在菜单里的值：跟随系统是 system，其他是 Key 的文字。</summary>
    static string KeyOf(ThemeVariant variant) => variant == ThemeVariant.Default ? SystemKey : variant.Key.ToString() ?? "";

    /// <summary>内置主题与跟随系统（ThemeVariant.Default）的名字（上游 builtinThemeLabel，取语言包）；不是内置的时 null。</summary>
    /// <param name="lang">语言包。</param>
    /// <param name="variant">主题变体。</param>
    public static string? BuiltinLabel(Lang lang, ThemeVariant variant) => BuiltinLabel(lang, KeyOf(variant));

    static string? BuiltinLabel(Lang lang, string key) => key switch
    {
        SystemKey => lang.ThemeSystem,
        "Light" => lang.ThemeLight,
        "Dark" => lang.ThemeDark,
        "paper" => lang.ThemePaper,
        "midnight" => lang.ThemeMidnight,
        "high-contrast" => lang.ThemeHighContrast,
        "high-contrast-dark" => lang.ThemeHighContrastDark,
        "forest" => lang.ThemeForest,
        "lemon" => lang.ThemeLemon,
        "sakura" => lang.ThemeSakura,
        "festive" => lang.ThemeFestive,
        "cyberpunk" => lang.ThemeCyberpunk,
        "big-screen" => lang.ThemeBigScreen,
        "lavender" => lang.ThemeLavender,
        "ember" => lang.ThemeEmber,
        "mono" => lang.ThemeMono,
        "celadon" => lang.ThemeCeladon,
        _ => null,
    };

    string LabelOf(Lang lang, ThemeVariant variant, string key) =>
        Labels?.GetValueOrDefault(variant == ThemeVariant.Default ? SystemKey : variant.Key) ?? BuiltinLabel(lang, key) ?? key;

    IconData IconOf(ThemeVariant variant, string key) =>
        Icons?.GetValueOrDefault(variant == ThemeVariant.Default ? SystemKey : variant.Key) ?? DefaultIcons.GetValueOrDefault(variant == ThemeVariant.Default ? SystemKey : variant.Key) ?? Lucide.Palette;

    IEnumerable<ThemeVariant> Choices => IncludeSystem ? [ThemeVariant.Default, .. Themes] : Themes;

    /// <summary>选了一项：写回 Value，需要时设到应用。</summary>
    void Choose(string key)
    {
        var next = Choices.FirstOrDefault(variant => KeyOf(variant) == key) ?? ThemeVariant.Default;
        SetCurrentValue(ValueProperty, next);
        if (AppliesToApplication && Application.Current is { } application) application.RequestedThemeVariant = next;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ThemesProperty || change.Property == ValueProperty || change.Property == IncludeSystemProperty
            || change.Property == LabelsProperty || change.Property == IconsProperty || change.Property == Provider.LangProperty)
            Rebuild();
    }

    /// <summary>菜单项、当前打勾的那一项、按钮的图标与名称。</summary>
    void Rebuild()
    {
        var lang = Provider.GetLang(this);
        var messages = PatternsMessages.For(lang);
        syncing = true;
        try
        {
            group.Items.Clear();
            foreach (var variant in Choices)
            {
                var key = KeyOf(variant);
                group.Items.Add(new MenuRadioItem { Value = key, Icon = IconOf(variant, key), Content = LabelOf(lang, variant, key) });
            }
            group.Value = KeyOf(Value);
        }
        finally
        {
            syncing = false;
        }
        AutomationProperties.SetName(group, messages.ThemeLabel);
        trigger.Icon = IconOf(Value, KeyOf(Value));
        AutomationProperties.SetName(trigger, messages.ThemeLabel);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        menu.Measure(availableSize);
        return menu.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        menu.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>ThemeSwitcher 的部件名。外层是切换器自己，菜单的部件同 Menu。</summary>
public static class ThemeSwitcherParts
{
    /// <summary>触发按钮（TS 的 propsTrigger）：IconButton，图标表示当前的选择。</summary>
    public const string Trigger = "PART_Trigger";
}
