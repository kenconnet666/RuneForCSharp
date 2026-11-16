using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 主题编辑器（vue-tsx-pro 的 ThemeCustomizer）：几个旋钮（品牌色、中性色、圆角、缩放、字体、动效）调出自己的主题，实时检查对比度，给出可以粘贴的 XAML。
/// 只管设置（<see cref="Settings"/>）；把设置套到界面上由使用方做（<see cref="ThemeCustomization.Apply"/>），这样设置可以记下来、和主题偏好组合：
/// <code>
/// var editor = new ThemeCustomizer();
/// editor.PropertyChanged += (_, e) =&gt; { if (e.Property == ThemeCustomizer.SettingsProperty) ThemeCustomization.Apply(window, editor.Settings); };
/// </code>
/// 旋钮分成颜色、形状与尺寸、字体与动效、检查与导出四组；每个选项带示意（色块、圆角的角、字号、字体），当前取值写在旋钮名称旁边。
/// 对比度按 <see cref="ThemeContrast"/> 检查，读的是 <see cref="ContrastTarget"/>（默认所在窗口）上实际生效的颜色，所以改了设置、换了主题都会重新检查。
/// 部件：<see cref="ThemeCustomizerParts"/>。
/// </summary>
public class ThemeCustomizer : Control
{
    /// <summary>设置（双向绑定）。</summary>
    public static readonly StyledProperty<ThemeSettings> SettingsProperty =
        AvaloniaProperty.Register<ThemeCustomizer, ThemeSettings>(nameof(Settings), new ThemeSettings(), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>设置套在哪套主题上：决定色块取亮色还是暗色色阶、导出的取值；不写时是所在位置实际的主题变体。</summary>
    public static readonly StyledProperty<ThemeVariant?> BaseProperty = AvaloniaProperty.Register<ThemeCustomizer, ThemeVariant?>(nameof(Base));

    /// <summary>从哪个元素上读实际生效的颜色做对比度检查，默认所在窗口。</summary>
    public static readonly StyledProperty<StyledElement?> ContrastTargetProperty = AvaloniaProperty.Register<ThemeCustomizer, StyledElement?>(nameof(ContrastTarget));

    static readonly double[] Scalings = [0.9, 0.95, 1, 1.05, 1.1];

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Xl, Align = FlexAlign.Stretch };
    readonly Dictionary<string, (ToggleGroup Group, Text Current)> fields = [];
    readonly Alert contrast = new() { Name = ThemeCustomizerParts.Contrast, IsVisible = false };
    readonly CodeBlock code = new() { Name = ThemeCustomizerParts.Code, Language = "xml" };
    readonly Button download = new() { Name = ThemeCustomizerParts.Download, Variant = ButtonVariant.Soft, Icon = Lucide.Download };
    readonly Button reset = new() { Name = ThemeCustomizerParts.Reset, Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral, Icon = Lucide.RotateCcw };
    // 写回设置时旋钮会跟着改值：不再当成用户的选择
    bool syncing;

    /// <summary>新建：四组旋钮。</summary>
    public ThemeCustomizer()
    {
        download.Click += async (_, _) => await Download();
        reset.Click += (_, _) => SetCurrentValue(SettingsProperty, new ThemeSettings());
        // 没写 Base 时跟着所在位置的亮暗：色块换色阶
        ActualThemeVariantChanged += (_, _) =>
        {
            if (Base is null && fields.Count > 0) Build();
        };
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>设置。</summary>
    public ThemeSettings Settings { get => GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }

    /// <summary>设置套在哪套主题上。</summary>
    public ThemeVariant? Base { get => GetValue(BaseProperty); set => SetValue(BaseProperty, value); }

    /// <summary>做对比度检查的元素。</summary>
    public StyledElement? ContrastTarget { get => GetValue(ContrastTargetProperty); set => SetValue(ContrastTargetProperty, value); }

    /// <summary>导出的代码（测试用）。</summary>
    internal string Code => code.Code;

    /// <summary>对比度检查的结果（测试用）。</summary>
    internal Alert ContrastAlert => contrast;

    ThemeVariant Variant => Base ?? ActualThemeVariant;

    ThemeCustomizerMessages Messages => ThemeCustomizerMessages.For(Provider.GetLang(this));

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Build();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BaseProperty || change.Property == Provider.LangProperty) Build();
        else if (change.Property == SettingsProperty || change.Property == ContrastTargetProperty) Sync();
    }

    /// <summary>改一个旋钮：空值表示沿用主题本来的取值。</summary>
    void Set(Func<ThemeSettings, ThemeSettings> change)
    {
        if (!syncing) SetCurrentValue(SettingsProperty, change(Settings));
    }

    void Build()
    {
        // 导出一组的几个控件是留着用的（代码、按钮的状态）：先从上一次的组里拿下来
        foreach (var kept in new Control[] { contrast, code, download, reset })
            (kept.Parent as Panel)?.Children.Remove(kept);
        root.Children.Clear();
        fields.Clear();
        var messages = Messages;
        var dark = ThemeCustomization.IsDark(Variant);
        root.Children.Add(Group(messages.GroupColor, messages.GroupColorHint,
            Field("accent", messages.Accent, ScaleOptions(ThemePalette.Accents, dark), swatches: true, value => Set(settings => settings with { Accent = Blank(value) })),
            Field("neutral", messages.Neutral, ScaleOptions(ThemePalette.Neutrals, dark), swatches: true, value => Set(settings => settings with { Neutral = Blank(value) }))));
        (ThemeRadius Value, string Label)[] radii = [(ThemeRadius.None, messages.RadiusNone), (ThemeRadius.Small, messages.RadiusSmall), (ThemeRadius.Medium, messages.RadiusMedium), (ThemeRadius.Large, messages.RadiusLarge)];
        root.Children.Add(Group(messages.GroupShape, messages.GroupShapeHint,
            Field("radius", messages.Radius, [.. radii.Select(item => new Option(item.Value.ToString(), item.Label, Corner(item.Value)))], swatches: false,
                value => Set(settings => settings with { Radius = Enum.Parse<ThemeRadius>(value) })),
            Field("scaling", messages.Scaling, [.. Scalings.Select(factor => new Option(Key(factor), Percent(factor), Sample(factor, null)))], swatches: false,
                value => Set(settings => settings with { Scaling = double.Parse(value, CultureInfo.InvariantCulture) }))));
        (ThemeFont Value, string Label)[] fonts = [(ThemeFont.Sans, messages.FontSans), (ThemeFont.SerifHeadings, messages.FontSerifHeadings)];
        (ThemeMotion Value, string Label)[] motions = [(ThemeMotion.None, messages.MotionNone), (ThemeMotion.Fast, messages.MotionFast), (ThemeMotion.Normal, messages.MotionNormal), (ThemeMotion.Slow, messages.MotionSlow)];
        root.Children.Add(Group(messages.GroupType, messages.GroupTypeHint,
            Field("font", messages.Font, [.. fonts.Select(item => new Option(item.Value.ToString(), item.Label, Sample(1, item.Value == ThemeFont.Sans ? ThemeKeys.Font.Sans : ThemeKeys.Font.Serif)))], swatches: false,
                value => Set(settings => settings with { Font = Enum.Parse<ThemeFont>(value) })),
            Field("motion", messages.Motion, [.. motions.Select(item => new Option(item.Value.ToString(), item.Label, null))], swatches: false,
                value => Set(settings => settings with { Motion = Enum.Parse<ThemeMotion>(value) }))));
        code.Title = messages.Code;
        download.Content = messages.Download;
        reset.Content = messages.Reset;
        root.Children.Add(Group(messages.GroupExport, messages.GroupExportHint, contrast, code, new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { download, reset } }));
        Sync();
    }

    static string? Blank(string value) => value.Length == 0 ? null : value;

    static string Key(double factor) => factor.ToString(CultureInfo.InvariantCulture);

    string Percent(double factor) => (factor * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>旋钮的当前取值写回各个分段控件、标题旁的文字、导出的代码与对比度。</summary>
    void Sync()
    {
        if (fields.Count == 0) return;
        var settings = Settings;
        var messages = Messages;
        var dark = ThemeCustomization.IsDark(Variant);
        string ScaleText(string? scale) => scale is null ? messages.ThemeDefault : $"{messages.ColorNames.GetValueOrDefault(scale, scale)} {ThemePalette.Swatch(scale, dark)}";
        syncing = true;
        try
        {
            Show("accent", settings.Accent ?? "", ScaleText(settings.Accent));
            Show("neutral", settings.Neutral ?? "", ScaleText(settings.Neutral));
            var radius = settings.Radius ?? ThemeRadius.Medium;
            Show("radius", radius.ToString(), radius switch { ThemeRadius.None => messages.RadiusNone, ThemeRadius.Small => messages.RadiusSmall, ThemeRadius.Large => messages.RadiusLarge, _ => messages.RadiusMedium });
            var scaling = settings.Scaling ?? 1;
            Show("scaling", Key(scaling), Percent(scaling));
            var font = settings.Font ?? ThemeFont.Sans;
            Show("font", font.ToString(), font == ThemeFont.Sans ? messages.FontSans : messages.FontSerifHeadings);
            var motion = settings.Motion ?? ThemeMotion.Normal;
            Show("motion", motion.ToString(), motion switch { ThemeMotion.None => messages.MotionNone, ThemeMotion.Fast => messages.MotionFast, ThemeMotion.Slow => messages.MotionSlow, _ => messages.MotionNormal });
        }
        finally
        {
            syncing = false;
        }
        code.Code = ThemeCustomization.Code(settings, Variant);
        reset.IsEnabled = settings != new ThemeSettings();
        // 使用方在设置变了之后才把覆盖套上去：等这一轮处理完再读实际生效的颜色
        Dispatcher.UIThread.Post(CheckContrast, DispatcherPriority.Background);
    }

    void Show(string name, string value, string current)
    {
        if (!fields.TryGetValue(name, out var field)) return;
        field.Group.Value = value;
        field.Current.Text = current;
    }

    /// <summary>读实际生效的颜色做对比度检查：读不到（还没放进窗口）时不显示结果。</summary>
    internal void CheckContrast()
    {
        var target = ContrastTarget ?? TopLevel.GetTopLevel(this);
        if (target is null)
        {
            contrast.IsVisible = false;
            return;
        }
        var messages = Messages;
        var issues = ThemeCustomization.CheckContrast(target);
        contrast.Title = messages.Contrast;
        contrast.Tone = issues.Count == 0 ? StatusTone.Success : StatusTone.Warning;
        if (issues.Count == 0) contrast.Content = messages.ContrastOk(ThemeContrast.Pairs.Count);
        else
        {
            var list = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs };
            foreach (var issue in issues)
                list.Children.Add(new TextBlock { Text = messages.ContrastIssue($"{issue.Foreground} / {issue.Background}", issue.Ratio.ToString("0.00", CultureInfo.InvariantCulture), issue.Minimum) });
            contrast.Content = list;
        }
        contrast.IsVisible = true;
    }

    async Task Download()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "theme.axaml", DefaultExtension = "axaml" });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(code.Code);
    }

    /// <summary>一个选项：值、名字、示意（色块只放示意，名字在悬停提示与读屏名称里）、悬停提示。</summary>
    sealed record Option(string Value, string Label, Control? Preview, string? Tip = null);

    /// <summary>一组：标题与说明，下面是几个旋钮；组与组之间一条分隔线（第一组上面不画）。</summary>
    Control Group(string title, string hint, params Control[] children)
    {
        var body = new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch };
        body.Children.Add(new Flex
        {
            Name = ThemeCustomizerParts.GroupHeader, Direction = FlexDirection.Column, Gap = Space.Xs,
            Children = { new Text { Text = title, Weight = TextWeight.Semibold }, new Text { Text = hint, Size = TextSize.Sm, Tone = TextTone.Muted } },
        });
        body.Children.AddRange(children);
        var group = new Border { Name = ThemeCustomizerParts.Group, Child = body };
        if (root.Children.Count > 0)
        {
            group[!Border.BorderBrushProperty] = this.GetResourceObservable(ThemeKeys.Brush.BorderMuted).ToBinding();
            group.BorderThickness = new Thickness(0, 1, 0, 0);
            group.Padding = new Thickness(0, 16, 0, 0);
        }
        return group;
    }

    /// <summary>一个旋钮：名称与当前取值，下面是选项（单选、至少选一项）。</summary>
    Control Field(string name, string label, IReadOnlyList<Option> options, bool swatches, Action<string> changed)
    {
        var group = new ToggleGroup { Name = ThemeCustomizerParts.Options, IsRequired = true, Size = TextSize.Sm, Wrap = FlexWrap.Wrap };
        AutomationProperties.SetName(group, label);
        foreach (var option in options)
        {
            var item = new ToggleItem { Name = ThemeCustomizerParts.Option, Value = option.Value, HorizontalContentAlignment = swatches ? HorizontalAlignment.Center : HorizontalAlignment.Left };
            if (swatches)
            {
                item.Content = option.Preview;
                // 色板只放色块：排得紧凑（上游每格至少 2em、左右不留内边距）
                item.MinWidth = 2 * 14;
                item.Padding = new Thickness(0, item.Padding.Top, 0, item.Padding.Bottom);
                AutomationProperties.SetName(item, option.Label);
            }
            else
            {
                item.MinWidth = 6.5 * 14;
                var content = new Flex { Align = FlexAlign.Center, Gap = Space.Sm };
                if (option.Preview is { } preview) content.Children.Add(preview);
                content.Children.Add(new TextBlock { Text = option.Label, TextTrimming = TextTrimming.CharacterEllipsis });
                item.Content = content;
            }
            if (option.Tip is { } tip) ToolTip.SetTip(item, tip);
            group.Children.Add(item);
        }
        group.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleGroup.ValueProperty && group.Value is { } value) changed(value);
        };
        var current = new Text { Size = TextSize.Xs, Tone = TextTone.Muted };
        fields[name] = (group, current);
        return new Flex
        {
            Name = ThemeCustomizerParts.Field, Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children =
            {
                new Flex { Name = ThemeCustomizerParts.FieldHeader, Align = FlexAlign.Baseline, Justify = FlexJustify.Between, Gap = Space.Sm, Children = { new Text { Text = label, Size = TextSize.Sm, Weight = TextWeight.Medium }, current } },
                group,
            },
        };
    }

    /// <summary>色阶选项：第一项是"主题默认"（虚线圈），其余是色块；名字与色值在悬停提示与读屏名称里。</summary>
    IReadOnlyList<Option> ScaleOptions(IReadOnlyList<string> scales, bool dark)
    {
        var messages = Messages;
        var size = 1.25 * 14;
        var dashed = new Ellipse { Name = ThemeCustomizerParts.Swatch, Width = size, Height = size, StrokeThickness = 1, StrokeDashArray = [3, 2] };
        dashed[!Shape.StrokeProperty] = this.GetResourceObservable(ThemeKeys.Brush.BorderStrong).ToBinding();
        List<Option> options = [new("", messages.ThemeDefault, dashed, messages.ThemeDefault)];
        foreach (var scale in scales)
        {
            var color = ThemePalette.Swatch(scale, dark);
            var swatch = new Ellipse { Name = ThemeCustomizerParts.Swatch, Width = size, Height = size, Fill = new SolidColorBrush(Color.Parse(color)), StrokeThickness = 1 };
            swatch[!Shape.StrokeProperty] = this.GetResourceObservable(ThemeKeys.Brush.BorderMuted).ToBinding();
            var name = messages.ColorNames.GetValueOrDefault(scale, scale);
            options.Add(new Option(scale, name, swatch, $"{name}（{scale}）{color}"));
        }
        return options;
    }

    /// <summary>圆角示意：一个只有上边与左边的角，起始的角按档位圆。</summary>
    static Control Corner(ThemeRadius radius)
    {
        var corner = new Border
        {
            Name = ThemeCustomizerParts.Corner, Width = 14, Height = 14, BorderThickness = new Thickness(2, 2, 0, 0),
            CornerRadius = new CornerRadius(radius switch { ThemeRadius.Small => 0.2 * 14, ThemeRadius.Medium => 0.45 * 14, ThemeRadius.Large => 0.8 * 14, _ => 0 }, 0, 0, 0),
        };
        corner[!Border.BorderBrushProperty] = corner[!global::Avalonia.Controls.Documents.TextElement.ForegroundProperty];
        return corner;
    }

    /// <summary>字号、字体的示意："Aa"。</summary>
    Control Sample(double factor, string? font)
    {
        var sample = new TextBlock { Name = ThemeCustomizerParts.Sample, Text = "Aa", FontWeight = FontWeight.SemiBold, FontSize = factor * 14, LineHeight = factor * 14 };
        if (font is not null) sample[!TextBlock.FontFamilyProperty] = this.GetResourceObservable(font).ToBinding();
        return sample;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>ThemeCustomizer 的部件名。</summary>
public static class ThemeCustomizerParts
{
    /// <summary>一组（Border；TS 的 propsGroup）。</summary>
    public const string Group = "PART_Group";

    /// <summary>组的标题与说明（Flex）。</summary>
    public const string GroupHeader = "PART_GroupHeader";

    /// <summary>一个旋钮（Flex）。</summary>
    public const string Field = "PART_Field";

    /// <summary>旋钮的名称与当前取值（Flex）。</summary>
    public const string FieldHeader = "PART_FieldHeader";

    /// <summary>选项（ToggleGroup）。</summary>
    public const string Options = "PART_Options";

    /// <summary>一个选项（ToggleItem）。</summary>
    public const string Option = "PART_Option";

    /// <summary>色块（Ellipse）。</summary>
    public const string Swatch = "PART_Swatch";

    /// <summary>圆角示意（Border）。</summary>
    public const string Corner = "PART_Corner";

    /// <summary>字号、字体的示意（TextBlock）。</summary>
    public const string Sample = "PART_Sample";

    /// <summary>对比度检查的结果（Alert；TS 的 propsContrast）。</summary>
    public const string Contrast = "PART_Contrast";

    /// <summary>导出的代码（CodeBlock；TS 的 propsCode）。</summary>
    public const string Code = "PART_Code";

    /// <summary>下载代码（Button；TS 的 propsDownload）。</summary>
    public const string Download = "PART_Download";

    /// <summary>恢复默认（Button；TS 的 propsReset）。</summary>
    public const string Reset = "PART_Reset";
}
