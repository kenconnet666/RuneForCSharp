using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 表单（vue-tsx-pro 的 Form）：把 <see cref="FormController{T}"/> 交给里面写了 <see cref="Field.Path"/> 的字段。
/// <code>
/// &lt;rune:Form Controller="{Binding Signup}" LabelPlacement="start"&gt;
///   &lt;rune:Field Path="Email" Label="邮箱"&gt;&lt;rune:TextField /&gt;&lt;/rune:Field&gt;
///   &lt;rune:Button Command="{Binding #form.SubmitCommand}" IsLoading="{Binding Signup.IsSubmitting}"&gt;保存&lt;/rune:Button&gt;
/// &lt;/rune:Form&gt;
/// </code>
/// 提交（<see cref="SubmitAsync"/>、<see cref="SubmitCommand"/>、单行输入框里按回车）先校验，没通过就把焦点移到错误汇总（有 <see cref="FormErrorSummary"/> 时）或第一个出错的字段；
/// <see cref="ResetCommand"/> 回到初始值。标签布局（<see cref="LabelPlacement"/>、<see cref="LabelWidth"/>、<see cref="LabelAlign"/>）与
/// <see cref="IsReadOnly"/> 在这里统一设置，字段上写了的优先；不可用写 IsEnabled。
/// </summary>
[PseudoClasses(":submitting")]
public class Form : Decorator
{
    /// <summary>表单控制器（继承给里面的字段）。</summary>
    public static readonly AttachedProperty<IFormController?> ControllerProperty =
        AvaloniaProperty.RegisterAttached<Form, Control, IFormController?>(nameof(Controller), inherits: true);

    /// <summary>里面字段的标签放在哪。</summary>
    public static readonly StyledProperty<FieldLabelPlacement> LabelPlacementProperty = Field.LabelPlacementProperty.AddOwner<Form>();

    /// <summary>里面字段的标签列宽度。</summary>
    public static readonly StyledProperty<double> LabelWidthProperty = Field.LabelWidthProperty.AddOwner<Form>();

    /// <summary>里面字段的标签文字对齐。</summary>
    public static readonly StyledProperty<FieldLabelAlign> LabelAlignProperty = Field.LabelAlignProperty.AddOwner<Form>();

    /// <summary>整张表单只读（查看详情、审批时）。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = Field.IsReadOnlyProperty.AddOwner<Form>();

    IFormController? subscribed;

    /// <summary>新建：单行输入框里按回车提交。</summary>
    public Form()
    {
        SubmitCommand = new FormCommand(() => _ = SubmitAsync());
        ResetCommand = new FormCommand(Reset);
        AddHandler(KeyDownEvent, OnKeyDown);
    }

    /// <summary>表单控制器。</summary>
    public IFormController? Controller { get => GetValue(ControllerProperty); set => SetValue(ControllerProperty, value); }

    /// <summary>标签放在哪。</summary>
    public FieldLabelPlacement LabelPlacement { get => GetValue(LabelPlacementProperty); set => SetValue(LabelPlacementProperty, value); }

    /// <summary>标签列的宽度。</summary>
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>标签文字的对齐。</summary>
    public FieldLabelAlign LabelAlign { get => GetValue(LabelAlignProperty); set => SetValue(LabelAlignProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>提交（给提交按钮的 Command）。</summary>
    public ICommand SubmitCommand { get; }

    /// <summary>重置（给重置按钮的 Command）。</summary>
    public ICommand ResetCommand { get; }

    /// <summary>取元素所在表单的控制器。</summary>
    /// <param name="element">元素。</param>
    public static IFormController? GetController(Control element) => element.GetValue(ControllerProperty);

    /// <summary>设置元素（及里面）的表单控制器。</summary>
    /// <param name="element">元素。</param>
    /// <param name="value">控制器。</param>
    public static void SetController(Control element, IFormController? value) => element.SetValue(ControllerProperty, value);

    /// <summary>提交：没通过时把焦点移到错误汇总或第一个出错的字段；返回是否提交成功。</summary>
    public async Task<bool> SubmitAsync()
    {
        if (Controller is not { } controller) return false;
        if (await controller.SubmitAsync()) return true;
        // 字段在控制器的 Changed 里同步更新了出错状态，等这一轮布局完（错误行显示出来）再移焦点
        Dispatcher.UIThread.Post(FocusFirstError, DispatcherPriority.Loaded);
        return false;
    }

    /// <summary>回到初始值。</summary>
    public void Reset() => Controller?.Reset();

    /// <summary>有错误汇总时聚焦它（读屏念出有几项），否则聚焦第一个出错字段的控件。</summary>
    internal void FocusFirstError()
    {
        if (this.GetLogicalDescendants().OfType<FormErrorSummary>().FirstOrDefault(summary => summary.IsEffectivelyVisible) is { } summary)
        {
            summary.Focus();
            return;
        }
        var invalid = this.GetLogicalDescendants().OfType<Field>().FirstOrDefault(field => field.Invalid && field.IsEffectivelyVisible);
        if (invalid?.Content is { } control) Field.FocusControl(control);
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None || e.Source is not TextBox { AcceptsReturn: false }) return;
        e.Handled = true;
        _ = SubmitAsync();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ControllerProperty) Subscribe();
        // 字段里的控件由字段通知；不在字段里的控件只有这里能告诉它
        else if (change.Property == IsReadOnlyProperty)
            foreach (var control in this.GetLogicalDescendants().OfType<IFieldControl>()) control.FieldChanged();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Subscribe();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        Subscribe();
    }

    void Subscribe()
    {
        var next = ((ILogical)this).IsAttachedToLogicalTree ? Controller : null;
        if (next == subscribed) return;
        if (subscribed is not null) subscribed.Changed -= OnChanged;
        subscribed = next;
        if (next is not null) next.Changed += OnChanged;
        OnChanged(null, EventArgs.Empty);
    }

    void OnChanged(object? sender, EventArgs e) => PseudoClasses.Set(":submitting", subscribed?.IsSubmitting == true);

    sealed class FormCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
