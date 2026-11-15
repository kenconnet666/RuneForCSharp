using Avalonia;

namespace Rune.Avalonia;

/// <summary>
/// 表单对话框（vue-tsx-pro 的 FormDialog）：Dialog 加 Form 加错误汇总，确认按钮就是提交——校验没通过时留在对话框里（焦点到错误汇总），
/// 提交（FormController 的 OnSubmit）进行时确认按钮显示加载中，成功后关闭，OnSubmit 抛 FormSubmitException 或别的异常时留着。
/// <code>
/// &lt;rune:FormDialog Controller="{Binding Menu}" IsOpen="{Binding Editing}" Title="新建菜单" Size="md"&gt;
///   &lt;rune:Field Path="Name" Label="名称"&gt;&lt;rune:TextField /&gt;&lt;/rune:Field&gt;
/// &lt;/rune:FormDialog&gt;
/// </code>
/// 提交被后端拒绝（抛 FormSubmitException 带整份表单的错误，如"名称已存在"）时，说明显示在表单顶上的提示条里，对话框留着。
/// 打开前由调用方 Reset(初始值)：新建是空白的初始值，修改是当前记录。在单行输入框里按回车也是提交，所以 OnSubmit 成功后要自己关（IsOpen = false）；
/// 只点确认按钮时对话框会在提交成功后自己关。字段写在 Content 里；表单（标签布局等）是 <see cref="Form"/>，错误汇总是 <see cref="Summary"/>。
/// </summary>
public class FormDialog : Dialog
{
    /// <summary>表单控制器；确认按钮调它的 SubmitAsync。</summary>
    public static readonly StyledProperty<IFormController?> ControllerProperty = AvaloniaProperty.Register<FormDialog, IFormController?>(nameof(Controller));

    readonly Alert formError = new() { Tone = StatusTone.Danger, IsVisible = false };
    readonly Flex column = new() { Direction = FlexDirection.Column, Gap = Space.Md };
    IFormController? subscribed;

    /// <summary>新建：确认就是提交。</summary>
    public FormDialog()
    {
        Form.Child = column;
        Confirm = () => Form.SubmitAsync();
        Rebuild();
    }

    /// <summary>
    /// 命令式的小表单对话框（TS 的 useFormDialog）：在 owner 所在的窗口打开，沿用那里的主题、语言；确认（OnSubmit 完成）后得到填好的值，取消、Esc 时得到 null。
    /// <code>
    /// var values = await FormDialog.PromptAsync(this, new("退回收件箱", [new("Reason", "原因") { Type = FormPromptFieldType.Textarea, Required = true }]) { ConfirmText = "退回" });
    /// if (values is not null) Refresh();
    /// </code>
    /// 字段多、有联动、要自己排版的表单用 FormDialog 组件。
    /// </summary>
    /// <param name="owner">在它所在的窗口打开。</param>
    /// <param name="options">标题、字段、提交。</param>
    public static Task<IReadOnlyDictionary<string, object>?> PromptAsync(global::Avalonia.Controls.Control owner, FormPromptOptions options) => FormPrompt.ShowAsync(owner, options);

    /// <summary>表单控制器。</summary>
    public IFormController? Controller { get => GetValue(ControllerProperty); set => SetValue(ControllerProperty, value); }

    /// <summary>里面的表单（TS 的 propsForm）：标签的布局（LabelPlacement、LabelWidth）等写在它上面。</summary>
    public Form Form { get; } = new();

    /// <summary>错误汇总（TS 的 propsSummary）。</summary>
    public FormErrorSummary Summary { get; } = new();

    /// <summary>整份表单的错误提示条（测试用）。</summary>
    internal Alert FormErrorAlert => formError;

    private protected override object? Body => Form;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty) Rebuild();
        else if (change.Property == ControllerProperty)
        {
            Form.Controller = Controller;
            if (subscribed is not null) subscribed.Changed -= OnFormChanged;
            subscribed = Controller;
            if (subscribed is not null) subscribed.Changed += OnFormChanged;
            OnFormChanged(null, EventArgs.Empty);
        }
    }

    /// <summary>表单里依次是：整份表单的错误、错误汇总、字段。</summary>
    void Rebuild()
    {
        column.Children.Clear();
        column.Children.AddRange([formError, Summary]);
        if (Content is global::Avalonia.Controls.Control fields) column.Children.Add(fields);
        else if (Content is not null) column.Children.Add(new Text { Text = Content.ToString() });
    }

    void OnFormChanged(object? sender, EventArgs e)
    {
        formError.Description = subscribed?.FormError;
        formError.IsVisible = subscribed?.FormError is not null;
    }
}
