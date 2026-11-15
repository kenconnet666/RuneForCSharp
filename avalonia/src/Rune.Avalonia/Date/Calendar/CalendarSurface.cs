using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 日历类组件共有的外壳（vue-tsx-pro 的 calendarRecipe：CalendarView 与 QuarterCalendar 共用一个配方）：字号档位、竖排的根、读屏的分组，
/// 以及按语言取文字用的 Lang、Culture、Em。默认外观在 CalendarView.axaml，选择器写 <c>:is(rune|CalendarSurface)</c>，两种日历都命中。
/// </summary>
public abstract class CalendarSurface : Control
{
    /// <summary>字号档位，格子随之缩放；不写时继承外面的字号。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<CalendarSurface, TextSize>(nameof(Size));

    readonly TemplateResources resources;

    /// <summary>新建：放好空的根。</summary>
    private protected CalendarSurface()
    {
        resources = new TemplateResources(this);
        LogicalChildren.Add(Root);
        VisualChildren.Add(Root);
    }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>根：页头、网格、读屏的实时区域、页脚竖着排，隔 sm。</summary>
    private protected Flex Root { get; } = new() { Direction = FlexDirection.Column, Gap = Space.Sm };

    internal Lang Lang => Provider.GetLang(this);

    internal CultureInfo Culture => CalendarText.CultureOf(Lang);

    /// <summary>1em（像素）。</summary>
    private protected double Em => global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != SizeProperty) return;
        if (Size.Pixels is { } pixels) resources.Set(global::Avalonia.Controls.Documents.TextElement.FontSizeProperty, pixels);
        else resources.Bind(global::Avalonia.Controls.Documents.TextElement.FontSizeProperty, Size.ResourceKey);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        Root.Measure(availableSize);
        return Root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        Root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    sealed class GroupPeer(CalendarSurface owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
