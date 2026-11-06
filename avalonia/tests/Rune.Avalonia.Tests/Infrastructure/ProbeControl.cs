using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>测试用的模板控件：Variant 映射成伪类，模板是一个 Border（PART_Root）。</summary>
sealed class ProbeControl : TemplatedControl
{
    public static readonly StyledProperty<string?> VariantProperty =
        AvaloniaProperty.Register<ProbeControl, string?>(nameof(Variant), "solid");

    public string? Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    public int BackgroundChanges;

    public ProbeControl() => PseudoClasses.Swap(null, Variant);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty) PseudoClasses.Swap(change.GetOldValue<string?>(), change.GetNewValue<string?>());
        else if (change.Property == BackgroundProperty) BackgroundChanges++;
    }

    public Border Root => (Border)this.GetTemplateDescendants().First(c => c.Name == "PART_Root");

    /// <summary>主题：Padding 与模板里的圆角都是 em；solid / soft 两个档位的背景。</summary>
    public static ControlTheme CreateTheme()
    {
        var theme = new ControlTheme(typeof(ProbeControl))
        {
            Setters =
            {
                new Setter(BackgroundProperty, Brushes.Gray),
                new Setter(PaddingProperty, new EmExtension(1, 0.25).ProvideValue(null!)),
                new Setter(TemplateProperty, new FuncControlTemplate<ProbeControl>((_, _) => new Border
                {
                    Name = "PART_Root",
                    [!Border.BackgroundProperty] = new TemplateBinding(BackgroundProperty),
                    [!Border.CornerRadiusProperty] = (BindingBase)new EmExtension(0.4) { Of = RelativeSourceMode.TemplatedParent }.ProvideValue(null!),
                })),
            },
        };
        theme.Children.Add(new Style(x => x.Nesting().Class(":solid")) { Setters = { new Setter(BackgroundProperty, Brushes.Blue) } });
        theme.Children.Add(new Style(x => x.Nesting().Class(":soft")) { Setters = { new Setter(BackgroundProperty, Brushes.LightBlue) } });
        return theme;
    }
}
