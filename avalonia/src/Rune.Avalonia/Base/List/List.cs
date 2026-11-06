using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Rune.Avalonia;

/// <summary>
/// 正文列表的共同部分（vue-tsx-pro 的 listStylesProse）：<see cref="ListItem"/> 竖着排，项之间 <see cref="Gap"/>；
/// 字号档位 <see cref="Size"/> 设在列表上，项继承。列表给直接子元素里的每一项设记号（圆点、编号、图标）。
/// </summary>
public abstract class ListBase : StackPanel
{
    /// <summary>字号档位；不写时继承外面的字号。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<ListBase, TextSize>(nameof(Size));

    /// <summary>项与项之间的间距档位，默认 xs；每项是一段话时用 md。</summary>
    public static readonly StyledProperty<Space> GapProperty = AvaloniaProperty.Register<ListBase, Space>(nameof(Gap), Space.Xs);

    static readonly StyledProperty<double> GapFactorProperty = AvaloniaProperty.Register<ListBase, double>("GapFactor");

    readonly TemplateResources resources;
    IDisposable? spacing;

    /// <summary>新建：间距绑到主题的 space。</summary>
    private protected ListBase()
    {
        resources = new TemplateResources(this);
        resources.Bind(GapFactorProperty, Gap.ResourceKey);
    }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>项与项之间的间距档位。</summary>
    public Space Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>直接子元素里的列表项（HTML 里 ol 只给 li 编号）。</summary>
    private protected IEnumerable<ListItem> Items => Children.OfType<ListItem>();

    /// <summary>给每一项设记号。</summary>
    private protected abstract void UpdateMarkers();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(TextElement.FontSizeProperty, pixels);
            else resources.Bind(TextElement.FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == GapProperty) resources.Bind(GapFactorProperty, Gap.ResourceKey);
        else if (change.Property == GapFactorProperty || change.Property == TextElement.FontSizeProperty)
        {
            // 模板优先级：使用方的样式、本地值写的 Spacing 盖得过
            spacing?.Dispose();
            spacing = SetValue(SpacingProperty, GetValue(GapFactorProperty) * GetValue(TextElement.FontSizeProperty), BindingPriority.Template);
        }
    }

    /// <inheritdoc />
    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        UpdateMarkers();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListAutomationPeer(this);

    sealed class ListAutomationPeer(ListBase owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}

/// <summary>
/// 无序列表（vue-tsx-pro 的 List）：圆点，或 <see cref="Icon"/> 代替圆点（功能清单的对勾），<see cref="ListItem.Icon"/> 逐项覆盖。
/// <code>
/// &lt;rune:List Icon="{x:Static rune:Lucide.Check}"&gt;
///   &lt;rune:ListItem&gt;不限项目&lt;/rune:ListItem&gt;
///   &lt;rune:ListItem Icon="{x:Static rune:Lucide.X}"&gt;单点登录&lt;/rune:ListItem&gt;
/// &lt;/rune:List&gt;
/// </code>
/// 各项的图标是部件 <see cref="ListParts.Icon"/>：改颜色写 <c>rune|List.plans rune|ListItem /template/ rune|Icon#PART_Icon</c>（TS 的 propsIcon）。
/// 界面结构上的列表（导航、分页、步骤）不要圆点，用布局组件。
/// </summary>
public class List : ListBase
{
    /// <summary>用图标代替圆点；ListItem 的 Icon 逐项覆盖。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<List, IconData?>(nameof(Icon));

    /// <summary>用图标代替圆点。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty) UpdateMarkers();
    }

    private protected override void UpdateMarkers()
    {
        foreach (var item in Items) item.SetMarker("•", Icon);
    }
}

/// <summary>有序列表（vue-tsx-pro 的 OrderedList）：编号从 <see cref="Start"/> 起；里面的项不沿用外层 List 的图标。</summary>
public class OrderedList : ListBase
{
    /// <summary>第一项的编号，默认 1（HTML 的 start）。</summary>
    public static readonly StyledProperty<int> StartProperty = AvaloniaProperty.Register<OrderedList, int>(nameof(Start), 1);

    /// <summary>第一项的编号。</summary>
    public int Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StartProperty) UpdateMarkers();
    }

    private protected override void UpdateMarkers()
    {
        var number = Start;
        foreach (var item in Items) item.SetMarker($"{number++}.", null);
    }
}

/// <summary>
/// 列表项（vue-tsx-pro 的 ListItem）：记号（圆点、编号）在起始边的 1.5em 里，文字与记号隔 space.xs；
/// 带图标时图标在前、文字在后（隔 space.sm），不缩进。部件：<see cref="ListItemParts.Icon"/>、<see cref="ListItemParts.Content"/>。
/// 记号与图标是装饰，读屏念项里的文字。
/// </summary>
[TemplatePart(ListItemParts.Icon, typeof(Icon))]
[TemplatePart(ListItemParts.Marker, typeof(TextBlock))]
[PseudoClasses(":icon")]
public class ListItem : ContentControl
{
    /// <summary>这一项的图标，覆盖 List 的 Icon（有序列表里不用）。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<ListItem, IconData?>(nameof(Icon));

    string? marker;
    IconData? listIcon;
    TextBlock? markerPart;
    Icon? iconPart;

    /// <summary>这一项的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>列表给的记号与默认图标。</summary>
    internal void SetMarker(string text, IconData? icon)
    {
        (marker, listIcon) = (text, icon);
        UpdateMarker();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        markerPart = e.NameScope.Find<TextBlock>(ListItemParts.Marker);
        iconPart = e.NameScope.Find<Icon>(ListItemParts.Icon);
        UpdateMarker();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty) UpdateMarker();
    }

    void UpdateMarker()
    {
        var shown = Icon ?? listIcon;
        PseudoClasses.Set(":icon", shown is not null);
        markerPart?.Text = shown is null ? marker : null;
        // 模板优先级：使用方的部件样式写的 Data 盖得过
        iconPart?.SetValue(Rune.Avalonia.Icon.DataProperty, shown, BindingPriority.Template);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListItemAutomationPeer(this);

    sealed class ListItemAutomationPeer(ListItem owner) : ContentControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
    }
}

/// <summary>List 的部件名：各项的图标（TS 的 propsIcon），与 <see cref="ListItemParts.Icon"/> 是同一个部件。</summary>
public static class ListParts
{
    /// <summary>各项的图标。</summary>
    public const string Icon = ListItemParts.Icon;
}

/// <summary>ListItem 的部件名。</summary>
public static class ListItemParts
{
    /// <summary>图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>圆点或编号（TS 里是 ::marker）。</summary>
    public const string Marker = "PART_Marker";

    /// <summary>内容层（TS 的 propsContent）。</summary>
    public const string Content = "PART_ContentPresenter";
}
