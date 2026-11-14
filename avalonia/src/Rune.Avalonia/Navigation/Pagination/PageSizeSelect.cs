using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.LogicalTree;

namespace Rune.Avalonia;

/// <summary>
/// 每页条数选择（vue-tsx-pro 的 PageSizeSelect）：分页的配套组件，与 <see cref="Pagination"/> 并排放（三样都要时用 PaginationBar）。
/// 选项文字取语言包（"20 条/页"），默认 sm、名称取语言包（每页条数）。值那一格按最长的选项定宽：换条数时不变宽窄。
/// <code>
/// &lt;rune:PageSizeSelect PageSize="{Binding Size}" PageSizeChanged="OnSizeChanged" /&gt;
/// </code>
/// 是一个 <see cref="Select"/>：外观、部件同 Select；<see cref="PageSize"/> 与 Select 的 Value（字符串）互相同步。
/// </summary>
public class PageSizeSelect : Select
{
    /// <summary>每页条数；默认双向绑定。</summary>
    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<PageSizeSelect, int>(nameof(PageSize), 10, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>可选的条数，默认 10、20、50、100。</summary>
    public static readonly StyledProperty<IReadOnlyList<int>> OptionsProperty =
        AvaloniaProperty.Register<PageSizeSelect, IReadOnlyList<int>>(nameof(Options), [10, 20, 50, 100]);

    TextBlock? valuePart;

    /// <summary>新建：sm，10 条/页。</summary>
    public PageSizeSelect()
    {
        Size = TextSize.Sm;
        SelectionMode = SelectionMode.Single;
        Rebuild();
    }

    /// <summary>每页条数。</summary>
    public int PageSize { get => GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    /// <summary>可选的条数。</summary>
    public IReadOnlyList<int> Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }

    /// <summary>用户换了条数（通常把页码回到第 1 页）；代码里改 PageSize 不发。</summary>
    public event EventHandler<int>? PageSizeChanged;

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Select);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OptionsProperty) Rebuild();
        else if (change.Property == PageSizeProperty) SetCurrentValue(ValueProperty, PageSize.ToString());
        else if (change.Property == ValueProperty && int.TryParse(Value, out var size) && size != PageSize)
        {
            SetCurrentValue(PageSizeProperty, size);
            PageSizeChanged?.Invoke(this, size);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Rebuild();
        if (string.IsNullOrEmpty(AutomationProperties.GetName(this))) AutomationProperties.SetName(this, Provider.GetLang(this).PageSizeLabel);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        valuePart = e.NameScope.Find<TextBlock>(SelectParts.Value);
        FitWidth();
    }

    void Rebuild()
    {
        var lang = Provider.GetLang(this);
        ItemsSource = Options.Select(size => new ListboxOption(size.ToString(), lang.PageSizeOption(size))).ToList();
        SetCurrentValue(ValueProperty, PageSize.ToString());
        FitWidth();
    }

    /// <summary>值那一格按最长的选项定宽（TS 把所有选项的文字叠在同一格）：量每个选项的文字，取最宽的。</summary>
    void FitWidth()
    {
        if (valuePart is null) return;
        var lang = Provider.GetLang(this);
        var probe = new TextBlock { FontFamily = valuePart.FontFamily, FontSize = valuePart.FontSize, FontWeight = valuePart.FontWeight };
        var widest = 0d;
        foreach (var size in Options)
        {
            probe.Text = lang.PageSizeOption(size);
            probe.Measure(global::Avalonia.Size.Infinity);
            widest = Math.Max(widest, probe.DesiredSize.Width);
        }
        valuePart.MinWidth = Math.Ceiling(widest);
    }
}
