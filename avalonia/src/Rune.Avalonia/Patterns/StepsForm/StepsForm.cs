using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>分步表单的一步。</summary>
/// <param name="Title">步骤条上的标题。</param>
/// <param name="Fields">这一步的字段路径（含子路径）："下一步"只校验它们，出错时这一步标成出错。</param>
/// <param name="Content">这一步的内容（Field 们）：换到这一步时调用、建新的。</param>
public sealed record FormStep(string Title, IReadOnlyList<string> Fields, Func<Control> Content)
{
    /// <summary>步骤条上的说明。</summary>
    public string? Description { get; init; }
}

/// <summary>
/// 分步表单（vue-tsx-pro 的 StepsForm，同 Ant Design ProComponents 的 StepsForm）：一份表单分成几步填（开户、准入申请、配置向导），上方是步骤条，下方是这一步的字段与"上一步 / 下一步 / 提交"。
/// <code>
/// form.Controller = signup;
/// form.Steps = [new("企业信息", ["Company", "Contact"], () => CompanyFields()), new("账号", ["Password", "Confirm"], () => AccountFields())];
/// </code>
/// <list type="bullet">
/// <item>"下一步"只校验这一步的字段（ValidateAsync(fields)），通过才往后走，没通过的错误立即显示、焦点到第一个出错的控件上；单行输入框里按回车也是下一步（最后一步才是提交）；走过的步骤可以点回去。</item>
/// <item>最后一步是"提交"（控制器的 SubmitAsync）：整份校验没通过时跳到第一个有错的步骤，那一步在步骤条上标成出错。</item>
/// <item>换了一步焦点移到这一步的内容上（读屏念出第几步），键盘接着往下填。</item>
/// <item>只读（<see cref="IsReadOnly"/>，回看已提交的单子）、不可用（<see cref="IsDisabled"/>）时不校验、各步随便切换；只读时最后一步没有提交按钮，不可用时字段与提交按钮不可用。</item>
/// </list>
/// 外层就是 <see cref="Form"/>：标签布局（<see cref="LabelPlacement"/>、<see cref="LabelWidth"/>、<see cref="LabelAlign"/>）转给它。部件：<see cref="StepsFormParts"/>。
/// </summary>
public class StepsForm : Control
{
    /// <summary>表单控制器（TS 的 form）。</summary>
    public static readonly StyledProperty<IFormController?> ControllerProperty = AvaloniaProperty.Register<StepsForm, IFormController?>(nameof(Controller));

    /// <summary>各步。</summary>
    public static readonly StyledProperty<IReadOnlyList<FormStep>> StepsProperty = AvaloniaProperty.Register<StepsForm, IReadOnlyList<FormStep>>(nameof(Steps), []);

    /// <summary>当前第几步，从 0 开始；默认双向绑定。</summary>
    public static readonly StyledProperty<int> CurrentProperty = AvaloniaProperty.Register<StepsForm, int>(nameof(Current), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>提交按钮的文字，默认取文案（提交）。</summary>
    public static readonly StyledProperty<object?> SubmitTextProperty = AvaloniaProperty.Register<StepsForm, object?>(nameof(SubmitText));

    /// <summary>只读：回看，不校验、各步随便切换，没有提交按钮。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<StepsForm, bool>(nameof(IsReadOnly));

    /// <summary>不可用：字段与提交按钮不可用，各步照常切换（IsEnabled 会连步骤条、上一步下一步一起禁用）。</summary>
    public static readonly StyledProperty<bool> IsDisabledProperty = AvaloniaProperty.Register<StepsForm, bool>(nameof(IsDisabled));

    /// <summary>字段的标签放在哪（同 Form）。</summary>
    public static readonly StyledProperty<FieldLabelPlacement> LabelPlacementProperty = Field.LabelPlacementProperty.AddOwner<StepsForm>();

    /// <summary>字段的标签列宽度（同 Form）。</summary>
    public static readonly StyledProperty<double> LabelWidthProperty = Field.LabelWidthProperty.AddOwner<StepsForm>();

    /// <summary>字段的标签文字对齐（同 Form）。</summary>
    public static readonly StyledProperty<FieldLabelAlign> LabelAlignProperty = Field.LabelAlignProperty.AddOwner<StepsForm>();

    readonly Form form = new() { Name = StepsFormParts.Root };
    readonly Steps steps = new() { Name = StepsFormParts.Steps, Navigable = true };
    readonly Flex panel = new() { Name = StepsFormParts.Panel, Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Focusable = true };
    readonly Flex actions = new() { Name = StepsFormParts.Actions, Justify = FlexJustify.End, Gap = Space.Sm };
    readonly Button previous = new() { Name = StepsFormParts.Previous, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };
    readonly Button next = new() { Name = StepsFormParts.Next };
    readonly Button submit = new() { Name = StepsFormParts.Submit };
    IFormController? subscribed;
    // 内容是哪一步建的：步骤换了（或 Steps 后到）才重建，Sync 别的时候不动已经填着的字段
    FormStep? shown;

    /// <summary>新建：表单里是步骤条、这一步的内容与按钮行，竖排隔 lg。</summary>
    public StepsForm()
    {
        actions.Children.AddRange([previous, next, submit]);
        form.Child = new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch, Children = { steps, panel, actions } };
        // 换步后焦点在内容上：不画焦点环（内容本身不是控件），读屏念出它的名称
        panel.FocusAdorner = null;
        steps.PropertyChanged += (_, e) =>
        {
            if (e.Property == Rune.Avalonia.Steps.CurrentProperty && steps.Current != Current) Go(steps.Current);
        };
        previous.Click += (_, _) => Go(Current - 1);
        next.Click += (_, _) => _ = NextAsync();
        submit.Click += (_, _) => _ = SubmitAsync();
        // 单行输入框里按回车：不在最后一步时是下一步（不然只有一个输入框的步骤里按回车会提交整份表单，后面没填的步骤全报错）；先于 Form 的回车提交
        form.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        form[!Form.LabelPlacementProperty] = this[!LabelPlacementProperty];
        form[!Form.LabelWidthProperty] = this[!LabelWidthProperty];
        form[!Form.LabelAlignProperty] = this[!LabelAlignProperty];
        LogicalChildren.Add(form);
        VisualChildren.Add(form);
    }

    /// <summary>表单控制器。</summary>
    public IFormController? Controller { get => GetValue(ControllerProperty); set => SetValue(ControllerProperty, value); }

    /// <summary>各步。</summary>
    public IReadOnlyList<FormStep> Steps { get => GetValue(StepsProperty); set => SetValue(StepsProperty, value); }

    /// <summary>当前第几步。</summary>
    public int Current { get => GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }

    /// <summary>提交按钮的文字。</summary>
    public object? SubmitText { get => GetValue(SubmitTextProperty); set => SetValue(SubmitTextProperty, value); }

    /// <summary>标签放在哪。</summary>
    public FieldLabelPlacement LabelPlacement { get => GetValue(LabelPlacementProperty); set => SetValue(LabelPlacementProperty, value); }

    /// <summary>标签列的宽度。</summary>
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>标签文字的对齐。</summary>
    public FieldLabelAlign LabelAlign { get => GetValue(LabelAlignProperty); set => SetValue(LabelAlignProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>不可用。</summary>
    public bool IsDisabled { get => GetValue(IsDisabledProperty); set => SetValue(IsDisabledProperty, value); }

    /// <summary>只读、不可用：只是看——不校验，各步随便切换。</summary>
    bool Locked => IsReadOnly || IsDisabled;

    bool Last => Current >= Steps.Count - 1;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ControllerProperty)
        {
            form.Controller = Controller;
            Subscribe();
        }
        else if (change.Property == IsReadOnlyProperty) form.IsReadOnly = IsReadOnly;
        if (change.Property == StepsProperty || change.Property == CurrentProperty || change.Property == ControllerProperty || change.Property == IsReadOnlyProperty
            || change.Property == IsDisabledProperty || change.Property == SubmitTextProperty || change.Property == Provider.LangProperty) Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Subscribe();
        Sync();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        Subscribe();
    }

    void Subscribe()
    {
        var target = ((ILogical)this).IsAttachedToLogicalTree ? Controller : null;
        if (target == subscribed) return;
        if (subscribed is not null) subscribed.Changed -= OnChanged;
        subscribed = target;
        if (target is not null) target.Changed += OnChanged;
    }

    void OnChanged(object? sender, EventArgs e) => Sync();

    /// <summary>这一步有显示着的错误。</summary>
    bool HasError(FormStep step) =>
        Controller is { } controller && controller.Errors.Keys.Any(path => InStep(step, path) && controller.VisibleError(path) is not null);

    /// <summary>字段路径在这一步里：就是它，或是它的子路径（Address.City 属于 Address）。</summary>
    static bool InStep(FormStep step, string path) => step.Fields.Any(field => path == field || path.StartsWith(field + ".", StringComparison.Ordinal));

    /// <summary>步骤条、这一步的内容（换了步才重建）、按钮与读屏名称跟着当前状态。</summary>
    void Sync()
    {
        var messages = PatternsMessages.For(Provider.GetLang(this));
        var locked = Locked;
        steps.Items = [.. Steps.Select((step, index) => new StepItem(step.Title)
        {
            Description = step.Description,
            Status = HasError(step) ? StepState.Error : null,
            // 只能点回走过的步骤；只读、不可用时是回看，哪一步都能点
            IsDisabled = !locked && index > Current,
        })];
        steps.Current = Current;
        var step = Steps.ElementAtOrDefault(Current);
        if (shown != step)
        {
            shown = step;
            panel.Children.Clear();
            if (step is not null) panel.Children.Add(step.Content());
        }
        panel.IsEnabled = !IsDisabled;
        AutomationProperties.SetName(panel, messages.StepsFormStep(Current + 1, Steps.Count));
        previous.Content = messages.StepsFormPrevious;
        previous.IsVisible = Current > 0;
        next.Content = messages.StepsFormNext;
        next.IsVisible = !Last;
        next.IsLoading = Controller?.IsValidating() == true;
        // 只读是回看：没有提交
        submit.Content = SubmitText ?? messages.StepsFormSubmit;
        submit.IsVisible = Last && !IsReadOnly;
        submit.IsEnabled = !IsDisabled;
        submit.IsLoading = Controller?.IsSubmitting == true;
    }

    /// <summary>换到第 index 步，焦点移到这一步的内容上（等它建好、排好）。</summary>
    void Go(int index)
    {
        if (index < 0 || index >= Steps.Count) return;
        SetCurrentValue(CurrentProperty, index);
        Dispatcher.UIThread.Post(() => panel.Focus(), DispatcherPriority.Loaded);
    }

    /// <summary>下一步：只校验这一步的字段，通过才走；没通过时焦点到这一步第一个出错的控件上（同表单提交没通过）。</summary>
    async Task NextAsync()
    {
        if (Steps.ElementAtOrDefault(Current) is not { } step) return;
        if (Locked || Controller is null || await Controller.ValidateAsync(step.Fields))
        {
            Go(Current + 1);
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            var invalid = panel.GetLogicalDescendants().OfType<Field>().FirstOrDefault(field => field.Invalid && field.IsEffectivelyVisible);
            if (invalid?.Content is { } control) Field.FocusControl(control);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>提交：整份校验没通过时跳到第一个有错的步骤（这一步自己没错时）。</summary>
    async Task SubmitAsync()
    {
        if (Locked) return;
        if (await form.SubmitAsync()) return;
        var first = Steps.ToList().FindIndex(HasError);
        if (first >= 0 && first != Current && !HasError(Steps[Current])) Go(first);
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None || e.Source is not TextBox { AcceptsReturn: false }) return;
        e.Handled = true;
        // 只读、不可用的表单不提交
        if (!Last) _ = NextAsync();
        else if (!Locked) _ = SubmitAsync();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        form.Measure(availableSize);
        return form.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        form.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>StepsForm 的部件名（TS 的 propsRoot、propsSteps、propsPanel、propsActions、propsPrevious、propsNext、propsSubmit）。</summary>
public static class StepsFormParts
{
    /// <summary>外层的表单（Form）：标签布局写在它上面。</summary>
    public const string Root = "PART_Root";

    /// <summary>步骤条（Steps，可以点回走过的步骤）。</summary>
    public const string Steps = "PART_Steps";

    /// <summary>这一步的内容（竖排隔 md；换步后焦点在这里，读屏念"第几步，共几步"）。</summary>
    public const string Panel = "PART_Panel";

    /// <summary>按钮行（Flex，靠结束边、隔 sm）。</summary>
    public const string Actions = "PART_Actions";

    /// <summary>上一步（ghost、中性）。</summary>
    public const string Previous = "PART_Previous";

    /// <summary>下一步。</summary>
    public const string Next = "PART_Next";

    /// <summary>提交。</summary>
    public const string Submit = "PART_Submit";
}
