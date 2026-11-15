using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using AvaloniaGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia;

/// <summary>属性的种类：文档里分开列出。</summary>
public enum ApiPropKind
{
    /// <summary>普通属性：外观、状态、数据。</summary>
    Prop,

    /// <summary>部件属性：透传给内部的某个部件（TS 的 propsXxx）。</summary>
    Part,

    /// <summary>事件。</summary>
    Event,

    /// <summary>内容：子元素与模板（TS 的 children、renderXxx）。</summary>
    Content,
}

/// <summary>档位类属性的一个可选值。</summary>
/// <param name="Value">取值。</param>
/// <param name="Description">这个值的含义；没有注释时为空。</param>
public sealed record ApiValue(string Value, string Description = "");

/// <summary>一个属性的文档数据。</summary>
/// <param name="Name">名称。</param>
/// <param name="Kind">种类。</param>
/// <param name="Type">类型（源码里写的样子，如 <c>ButtonVariant</c>）。</param>
/// <param name="Description">说明。</param>
public sealed record ApiProp(string Name, ApiPropKind Kind, string Type, string Description)
{
    /// <summary>必填。</summary>
    public bool Required { get; init; }

    /// <summary>默认值（源码表达式，如 <c>"button"</c>、<c>false</c>）；null 时显示"—"。</summary>
    public string? Default { get; init; }

    /// <summary>能双向绑定（TS 的 _bind）。</summary>
    public bool Bindable { get; init; }

    /// <summary>档位类的可选值与各自的含义。</summary>
    public IReadOnlyList<ApiValue>? Values { get; init; }
}

/// <summary>
/// 组件 API 的表格（vue-tsx-pro docs-kit 的 PropsTable）：一行一个属性，列出名称、类型、默认值与说明；档位类的属性在说明下列出每个可选值的含义。
/// <code>
/// table.Props = [new ApiProp("Size", ApiPropKind.Prop, "TextSize", "尺寸") { Default = "Md", Values = [new("Sm", "紧凑"), new("Md", "常规")] }];
/// </code>
/// 必填的属性名称旁标"必填"（warning），能双向绑定的标"可绑定"（accent），文案取 <see cref="DocsKitMessages"/>。
/// 本身就是 <see cref="Table"/>（外观属性同 Table，默认 compact），窄时横向滚动；说明一列至少 18rem，类型再长也不挤掉它。
/// </summary>
public class PropsTable : Table
{
    /// <summary>要列出的属性（TS 的 props）。</summary>
    public static readonly StyledProperty<IReadOnlyList<ApiProp>> PropsProperty = AvaloniaProperty.Register<PropsTable, IReadOnlyList<ApiProp>>(nameof(Props), []);

    /// <summary>表格标题（caption），同时是表格的读屏名称。</summary>
    public static readonly StyledProperty<object?> CaptionProperty = AvaloniaProperty.Register<PropsTable, object?>(nameof(Caption));

    /// <summary>说明一列的最小宽度：18rem。</summary>
    const double DescriptionMinWidth = 18 * 16;

    static PropsTable() => DensityProperty.OverrideDefaultValue<PropsTable>(TableDensity.Compact);

    /// <summary>要列出的属性。</summary>
    public IReadOnlyList<ApiProp> Props { get => GetValue(PropsProperty); set => SetValue(PropsProperty, value); }

    /// <summary>表格标题。</summary>
    public object? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <summary>Table 的类型键：PropsTable 用 Table 的主题。</summary>
    protected override Type StyleKeyOverride => typeof(Table);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PropsProperty || change.Property == CaptionProperty || change.Property == Provider.LangProperty) Rebuild();
    }

    void Rebuild()
    {
        var messages = DocsKitMessages.For(Provider.GetLang(this));
        var head = new TableRow();
        foreach (var title in new[] { messages.Name, messages.Type, messages.DefaultValue, messages.Description }) head.Cells.Add(new TableHeaderCell { Content = title });
        var body = new TableBody();
        foreach (var prop in Props)
        {
            var row = new TableRow();
            row.Cells.Add(new TableCell { Content = NameCell(prop, messages) });
            row.Cells.Add(new TableCell { Content = CodeLine(prop.Type, type: true) });
            row.Cells.Add(new TableCell { Nowrap = true, Content = prop.Default is { } value ? CodeLine(value) : new Text { Text = "—", Tone = TextTone.Muted } });
            row.Cells.Add(new TableCell { Content = DescriptionCell(prop) });
            body.Rows.Add(row);
        }
        Sections.Clear();
        if (Caption is { } caption && !(caption is string text && string.IsNullOrEmpty(text))) Sections.Add(new TableCaption { Content = caption });
        Sections.Add(new TableHead { Rows = { head } });
        Sections.Add(body);
    }

    /// <summary>属性名不断开（行内代码能在任意处换行，表格会按它把名称列压到最窄），后面跟必填、可绑定的标记，放不下时标记换到下一行。</summary>
    static Flex NameCell(ApiProp prop, DocsKitMessages messages)
    {
        var name = CodeLine(prop.Name);
        name.Wrap = TextWrap.NoWrap;
        var flex = new Flex { Wrap = true, Align = FlexAlign.Center, Gap = Space.Xs, Children = { name } };
        if (prop.Required) flex.Children.Add(new Badge { Tone = StatusTone.Warning, Content = messages.Required });
        if (prop.Bindable) flex.Children.Add(new Badge { Tone = StatusTone.Accent, Content = messages.Bindable });
        return flex;
    }

    /// <summary>说明，下面是可选值（有任何一个带含义时）；至少 18rem 宽：说明是最长的一列，类型再长也给它留出一段可读的宽度。</summary>
    static StackPanel DescriptionCell(ApiProp prop)
    {
        var cell = new StackPanel { MinWidth = DescriptionMinWidth, Children = { new Text { Text = prop.Description } } };
        if (prop.Values is { } values && values.Any(value => !string.IsNullOrEmpty(value.Description))) cell.Children.Add(new ValueList(values));
        return cell;
    }

    /// <summary>一段行内代码；类型用 codeType 的颜色。</summary>
    static Text CodeLine(string source, bool type = false)
    {
        var code = new Code { Inlines = { new Run(source) } };
        var line = new Text { Inlines = [code] };
        if (type) code.Bind(TextElement.ForegroundProperty, line.GetResourceObservable(ThemeKeys.Brush.CodeType));
        return line;
    }

    /// <summary>可选值：两列（取值按最宽的一个、含义占余下的），sm 字号，列间距 md、行间距 xs，与说明隔 sm；间距按字号算，跟着字号变。</summary>
    sealed class ValueList : AvaloniaGrid
    {
        public ValueList(IReadOnlyList<ApiValue> values)
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*");
            this.Bind(TextElement.FontSizeProperty, this.GetResourceObservable(ThemeKeys.FontSize.Sm));
            for (var index = 0; index < values.Count; index++)
            {
                RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var term = CodeLine(values[index].Value);
                var text = new Text { Text = values[index].Description, Tone = TextTone.Muted, VerticalAlignment = VerticalAlignment.Top };
                SetRow(term, index);
                SetRow(text, index);
                SetColumn(text, 1);
                Children.AddRange([term, text]);
            }
            ApplySpacing();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TextElement.FontSizeProperty) ApplySpacing();
        }

        void ApplySpacing()
        {
            var em = TextElement.GetFontSize(this);
            ColumnSpacing = this.Resource<double>(ThemeKeys.Space.Md) * em;
            RowSpacing = this.Resource<double>(ThemeKeys.Space.Xs) * em;
            Margin = new Thickness(0, this.Resource<double>(ThemeKeys.Space.Sm) * em, 0, 0);
        }
    }
}
