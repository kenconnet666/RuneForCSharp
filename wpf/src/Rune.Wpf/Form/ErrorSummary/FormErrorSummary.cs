using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Rune.Wpf;

/// <summary>
/// 错误汇总（vue-tsx-pro 的 FormErrorSummary）：提交没通过时在表单顶上列出各项错误（"有 3 项需要修改"），每项是链接，点了跳到那个字段的控件。
/// <code>
/// &lt;rune:Form Controller="{Binding Signup}"&gt;
///   &lt;StackPanel&gt;
///     &lt;rune:FormErrorSummary /&gt;
///     &lt;rune:Field Path="Email" Label="邮箱"&gt;&lt;rune:TextField /&gt;&lt;/rune:Field&gt;
///   &lt;/StackPanel&gt;
/// &lt;/rune:Form&gt;
/// </code>
/// 放在 Form 里（通常是第一个）；提交失败时 Form 把焦点移到它上面，读屏念出有几项、各是什么（GOV.UK 的 Error summary）。
/// 只列显示着的错误（失焦过、提交过的字段），按字段登记的顺序；改对一项它就从列表里消失，全改对了汇总收起。整份表单的错误排在最前，不带链接。
/// 外观：dangerSoft 底、dangerBorder 细边、md 圆角、四周 md；标题是警示图标加文字（medium 字重、dangerText），列表项是 danger 色调的链接。
/// 部件样式键见 <see cref="FormErrorSummaryParts"/>，默认外观在 FormErrorSummary.xaml。
/// </summary>
public class FormErrorSummary : Border
{
    /// <summary>标题；不写时取语言包（有 N 项需要修改）。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(Func<int, string>), typeof(FormErrorSummary), new PropertyMetadata(null, (d, _) => ((FormErrorSummary)d).Rebuild()));

    static readonly DependencyProperty PaddingFactorProperty = DependencyProperty.Register(
        "PaddingFactor", typeof(double), typeof(FormErrorSummary), new PropertyMetadata(0.75, (d, _) => ((FormErrorSummary)d).Resize()));

    static readonly DependencyProperty RadiusFactorProperty = DependencyProperty.Register(
        "RadiusFactor", typeof(double), typeof(FormErrorSummary), new PropertyMetadata(0.375, (d, _) => ((FormErrorSummary)d).Resize()));

    readonly Text titleText = new();
    readonly Flex title = new() { Name = "PART_Title" };
    readonly List list = new() { Name = "PART_List" };
    IFormController? form;

    static FormErrorSummary()
    {
        FocusableProperty.OverrideMetadata(typeof(FormErrorSummary), new FrameworkPropertyMetadata(true));
        TextElement.FontSizeProperty.OverrideMetadata(typeof(FormErrorSummary), new FrameworkPropertyMetadata((d, _) => ((FormErrorSummary)d).Resize()));
        Form.ControllerProperty.OverrideMetadata(typeof(FormErrorSummary), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((FormErrorSummary)d).Connect()));
    }

    /// <summary>新建：没有要显示的错误时收起；外观引用主题的样式与资源。</summary>
    public FormErrorSummary()
    {
        Visibility = Visibility.Collapsed;
        SetResourceReference(StyleProperty, typeof(FormErrorSummary));
        SetResourceReference(PaddingFactorProperty, ThemeKeys.Space.Md);
        SetResourceReference(RadiusFactorProperty, ThemeKeys.Radius.Md);
        title.SetResourceReference(StyleProperty, FormErrorSummaryParts.Title);
        list.SetResourceReference(StyleProperty, FormErrorSummaryParts.List);
        title.Children.Add(new Icon { Data = Alert.IconFor(StatusTone.Danger) });
        title.Children.Add(titleText);
        var stack = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm };
        stack.Children.Add(title);
        stack.Children.Add(list);
        Child = stack;
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Connect();
        Resize();
    }

    /// <summary>标题。</summary>
    public Func<int, string>? Title { get => (Func<int, string>?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>列表里每一项的内容（测试用）：字段的错误是链接，整份表单的错误是文字。</summary>
    internal IEnumerable<object> Items => list.Children.OfType<ListItem>().Select(item => item.Content is TextBlock { Inlines.FirstInline: Link link } ? link : item.Content);

    /// <summary>标题的文字（测试用）。</summary>
    internal string? TitleText => titleText.Text;

    void Resize()
    {
        var em = TextElement.GetFontSize(this);
        Padding = new Thickness((double)GetValue(PaddingFactorProperty) * em);
        CornerRadius = new CornerRadius((double)GetValue(RadiusFactorProperty) * em);
    }

    void Connect()
    {
        var next = IsLoaded ? Form.GetController(this) : null;
        if (next == form) return;
        if (form is not null) form.Changed -= OnFormChanged;
        form = next;
        if (form is not null) form.Changed += OnFormChanged;
        Rebuild();
    }

    void OnFormChanged(object? sender, EventArgs e) => Rebuild();

    /// <summary>显示着的错误：整份表单的在前，其余按字段登记的顺序。</summary>
    IReadOnlyList<(string Label, string Message, FormFieldInfo? Field)> Errors()
    {
        if (form is not { SubmitCount: > 0 }) return [];
        var fields = form.Fields
            .Select(entry => (entry.Value.Label, Message: form.VisibleError(entry.Key), Field: (FormFieldInfo?)entry.Value))
            .Where(entry => entry.Message is not null)
            .Select(entry => (entry.Label, entry.Message!, entry.Field));
        return form.FormError is { } whole ? [("", whole, null), .. fields] : [.. fields];
    }

    void Rebuild()
    {
        var errors = Errors();
        Visibility = errors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        list.Children.Clear();
        if (errors.Count == 0) return;
        var lang = Provider.GetLang(this);
        titleText.Text = Title?.Invoke(errors.Count) ?? lang.FormErrorSummary(errors.Count);
        AutomationProperties.SetName(this, titleText.Text);
        foreach (var (label, message, field) in errors)
        {
            object content;
            if (field is null) content = new Text { Text = message };
            else
            {
                var link = new Link { Tone = TextTone.Danger };
                link.SetResourceReference(FrameworkContentElement.StyleProperty, FormErrorSummaryParts.Link);
                link.Inlines.Add(label.Length > 0 ? lang.FormErrorItem(label, message) : message);
                link.Click += (_, _) => field.Focus();
                content = new TextBlock { Inlines = { link } };
            }
            list.Children.Add(new ListItem { Content = content });
        }
    }
}

/// <summary>FormErrorSummary 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:FormErrorSummaryParts.Title}" TargetType="rune:Flex"&gt;</c>。外层就是 FormErrorSummary 自己。</summary>
public static class FormErrorSummaryParts
{
    /// <summary>标题（TS 的 propsTitle）：Flex，警示图标与文字，medium 字重、dangerText。</summary>
    public static readonly PartKey Title = new(typeof(FormErrorSummary), nameof(Title), typeof(Flex));

    /// <summary>列表（TS 的 propsList）：List。</summary>
    public static readonly PartKey List = new(typeof(FormErrorSummary), nameof(List), typeof(List));

    /// <summary>每一项的链接（TS 的 propsLink）：Link，danger 色调。</summary>
    public static readonly PartKey Link = new(typeof(FormErrorSummary), nameof(Link), typeof(Link));
}
