using System.ComponentModel;
using System.Windows.Input;

namespace Rune.WinForms;

/// <summary>
/// 表单（vue-tsx-pro 的 Form）：把 <see cref="FormController{T}"/> 交给里面写了 <see cref="FieldControl.Path"/> 的字段。
/// <code>
/// var form = new FormControl { Controller = signup, LabelPlacement = FieldLabelPlacement.Start, Dock = DockStyle.Fill };
/// form.Controls.Add(new FlexControl { Direction = FlexDirection.Column, Controls = { new FieldControl { Path = "Email", Label = "邮箱", Content = new TextFieldControl() } } });
/// save.Command = form.SubmitCommand;
/// </code>
/// 提交（<see cref="SubmitAsync"/>、<see cref="SubmitCommand"/>、单行输入框里按回车）先校验，没通过就把焦点移到错误汇总（有 <see cref="FormErrorSummaryControl"/> 时）或第一个出错的字段；
/// <see cref="ResetCommand"/> 回到初始值。标签布局（<see cref="LabelPlacement"/>、<see cref="LabelWidth"/>、<see cref="LabelAlign"/>）与
/// <see cref="IsReadOnly"/> 在这里统一设置，字段上写了的优先；不可用写 Enabled。
/// WinForms 的 Form 是窗口，这里叫 FormControl。
/// </summary>
[ToolboxItem(true)]
[Description("表单：把表单控制器交给里面的字段，提交时校验并聚焦第一个出错的字段。")]
public class FormControl : Panel
{
    /// <summary>新建：大小跟着内容。</summary>
    public FormControl()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        SubmitCommand = new FormCommand(() => _ = SubmitAsync());
        ResetCommand = new FormCommand(Reset);
    }

    /// <summary>表单控制器。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IFormController? Controller { get; set { field = value; Propagate(fields: true); } }

    /// <summary>里面字段的标签放在哪；默认上方。</summary>
    [Category("Layout")]
    [Description("里面字段的标签放在哪：top（默认）、start。")]
    public FieldLabelPlacement LabelPlacement { get; set { field = value; Propagate(fields: true); } }

    /// <summary>里面字段的标签列宽度（设备无关像素）；null 时 8em。</summary>
    [Category("Layout")]
    [Description("里面字段的标签列宽度（设备无关像素）；空着时 8em。")]
    [DefaultValue(null)]
    public int? LabelWidth { get; set { field = value; Propagate(fields: true); } }

    /// <summary>里面字段的标签文字对齐；默认 end。</summary>
    [Category("Layout")]
    [Description("里面字段的标签文字对齐：start、end（默认）。")]
    public FieldLabelAlign LabelAlign { get; set { field = value; Propagate(fields: true); } }

    /// <summary>整张表单只读（查看详情、审批时）：字段与不在字段里的控件都只读。</summary>
    [Category("Behavior")]
    [Description("整张表单只读：字段与不在字段里的控件都只读。")]
    [DefaultValue(false)]
    public bool IsReadOnly { get; set { field = value; Propagate(fields: false); } }

    /// <summary>提交（给提交按钮的 Command）。</summary>
    [Browsable(false)]
    public ICommand SubmitCommand { get; }

    /// <summary>重置（给重置按钮的 Command）。</summary>
    [Browsable(false)]
    public ICommand ResetCommand { get; }

    bool ShouldSerializeLabelPlacement() => LabelPlacement.Value is not null;

    void ResetLabelPlacement() => LabelPlacement = default;

    bool ShouldSerializeLabelAlign() => LabelAlign.Value is not null;

    void ResetLabelAlign() => LabelAlign = default;

    /// <summary>控件所在的表单：沿 Parent 链往外找；不在表单里时为 null。</summary>
    /// <param name="control">控件。</param>
    internal static FormControl? Of(Control control)
    {
        for (var node = control.Parent; node is not null; node = node.Parent)
            if (node is FormControl form) return form;
        return null;
    }

    /// <summary>提交：没通过时把焦点移到第一个出错的字段；返回是否提交成功。</summary>
    public async Task<bool> SubmitAsync()
    {
        if (Controller is not { } controller) return false;
        if (await controller.SubmitAsync()) return true;
        FocusFirstError();
        return false;
    }

    /// <summary>回到初始值。</summary>
    public void Reset() => Controller?.Reset();

    /// <summary>有错误汇总时聚焦它（读屏念出有几项），否则聚焦第一个出错字段的控件（按 Tab 顺序）。</summary>
    internal void FocusFirstError()
    {
        if (Descendants(this).OfType<FormErrorSummaryControl>().FirstOrDefault(summary => summary.Visible) is { } summary)
        {
            summary.Focus();
            return;
        }
        if (Descendants(this).OfType<FieldControl>().FirstOrDefault(field => field.Invalid && field.Visible) is { Content: { } control }) FieldControl.FocusControl(control);
    }

    /// <summary>单行输入框里按回车提交（ProcessCmdKey 沿父链上来，表单能看到里面控件的按键）。</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter && FromHandle(msg.HWnd) is TextBoxBase { Multiline: false })
        {
            _ = SubmitAsync();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>后加进来的字段（连同外层的面板）接上表单。</summary>
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is { } added)
            foreach (var part in Descendants(added).OfType<IFormPart>()) part.Connect();
    }

    /// <summary>设置变了：字段与错误汇总重新接上（控制器、布局），或字段只同步状态；不在字段里的控件跟上只读。</summary>
    void Propagate(bool fields)
    {
        foreach (var control in Descendants(this).Skip(1))
        {
            if (fields && control is IFormPart part) part.Connect();
            else if (control is FieldControl field) field.Sync();
            else if (control is IFieldControl own && FieldControl.Of(control) is null) own.FieldChanged();
        }
    }

    /// <summary>自己和里面的控件，按 Tab 顺序深度优先。</summary>
    static IEnumerable<Control> Descendants(Control node)
    {
        yield return node;
        foreach (var child in node.Controls.Cast<Control>().OrderBy(child => child.TabIndex))
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    sealed class FormCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
