using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Rune.Avalonia;

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
/// 部件：<see cref="FormErrorSummaryParts"/>，默认外观在 FormErrorSummary.axaml。
/// </summary>
public class FormErrorSummary : Border
{
    /// <summary>标题；不写时取语言包（有 N 项需要修改）。</summary>
    public static readonly StyledProperty<Func<int, string>?> TitleProperty = AvaloniaProperty.Register<FormErrorSummary, Func<int, string>?>(nameof(Title));

    readonly Text titleText = new();
    readonly Flex title = new() { Name = FormErrorSummaryParts.Title, Gap = Space.Xs, Align = FlexAlign.Center };
    readonly List list = new() { Name = FormErrorSummaryParts.List };
    IFormController? form;

    /// <summary>新建：没有要显示的错误时收起。</summary>
    public FormErrorSummary()
    {
        Focusable = true;
        IsVisible = false;
        title.Children.AddRange([new Icon { Data = Alert.IconFor(StatusTone.Danger) }, titleText]);
        Child = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { title, list } };
    }

    /// <summary>标题。</summary>
    public Func<int, string>? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>列表里的链接（测试用）。</summary>
    internal IEnumerable<Control> Items => list.Children.OfType<ListItem>().Select(item => (Control)item.Content!);

    /// <summary>标题的文字（测试用）。</summary>
    internal string? TitleText => titleText.Text;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == Form.ControllerProperty) Connect();
        else if (change.Property == TitleProperty || change.Property == Provider.LangProperty) Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Connect();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        Connect();
    }

    void Connect()
    {
        var next = ((ILogical)this).IsAttachedToLogicalTree ? Form.GetController(this) : null;
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
        IsVisible = errors.Count > 0;
        list.Children.Clear();
        if (errors.Count == 0) return;
        var lang = Provider.GetLang(this);
        titleText.Text = Title?.Invoke(errors.Count) ?? lang.FormErrorSummary(errors.Count);
        AutomationProperties.SetName(this, titleText.Text);
        foreach (var (label, message, field) in errors)
        {
            Control content;
            if (field is null) content = new Text { Text = message };
            else
            {
                var link = new Link { Name = FormErrorSummaryParts.Link, Tone = TextTone.Danger, Content = label.Length > 0 ? lang.FormErrorItem(label, message) : message };
                link.Click += (_, _) => field.Focus();
                content = link;
            }
            list.Children.Add(new ListItem { Content = content });
        }
    }
}

/// <summary>FormErrorSummary 的部件名：<c>rune|FormErrorSummary rune|Flex#PART_Title</c>。外层（TS 的 root）就是 FormErrorSummary 自己。</summary>
public static class FormErrorSummaryParts
{
    /// <summary>标题（TS 的 propsTitle）：Flex，警示图标与文字。</summary>
    public const string Title = "PART_Title";

    /// <summary>列表（TS 的 propsList）：List。</summary>
    public const string List = "PART_List";

    /// <summary>每一项的链接（TS 的 propsLink）：Link，danger 色调。</summary>
    public const string Link = "PART_Link";
}
