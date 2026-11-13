using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Rune.WinForms;

/// <summary>
/// 勾选类控件（<see cref="SwitchControl"/>、<see cref="CheckboxControl"/>、<see cref="RadioControl"/>）共用的宿主：
/// 点击、空格（松开时）切换；只读时能聚焦、不能切换；不可用时点击无效。焦点环只在键盘提示开着时显示，画在宿主留出的 4 像素 Padding 里。
/// Text 是文字，也是读屏名称（没写 AccessibleName 时）；切换时通知读屏。
/// </summary>
/// <typeparam name="T">承载的元素。</typeparam>
public abstract class ChoiceControl<T> : ElementControl<T>, IFieldControl where T : ChoiceBase, new()
{
    /// <summary>新建：能获得焦点、进 Tab 顺序。</summary>
    protected ChoiceControl()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        Padding = new Padding(LogicalToDeviceUnits(4));
        Cursor = Cursors.Hand;
        Element.IsCheckedChanged += (_, _) => OnIsCheckedChanged(EventArgs.Empty);
    }

    /// <summary>要切换时（点击、空格、读屏、<see cref="Toggle"/>）先发：设 Cancel 不改值——切换要等接口、要先确认时拦下，之后再写 IsChecked。</summary>
    [Category("Behavior")]
    [Description("要切换时先发；设 Cancel 不改值（受控用法：之后再写 IsChecked）。")]
    public event EventHandler<ChoiceTogglingEventArgs>? Toggling;

    /// <summary>值变了；数据绑定认它。</summary>
    [Category("Property Changed")]
    [Description("IsChecked 变了。")]
    public event EventHandler? IsCheckedChanged;

    /// <summary>文字，也是读屏名称（没写 AccessibleName 时）。</summary>
    [AllowNull]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [Category("Appearance")]
    [Description("后面的文字，也是读屏名称。")]
    public override string Text { get => base.Text; set => base.Text = value; }

    /// <summary>文字部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsLabel { get => Element.PropsLabel; set => Element.PropsLabel = value; }

    /// <summary>
    /// 切换，同点击：只读、不可用（以及各控件自己的理由，如切换中）时不变；先发 <see cref="Toggling"/>，没被拦下才写回。
    /// 要直接设值用 IsChecked（单选项用组的 Value）。
    /// </summary>
    public void Toggle()
    {
        if (Element.ReadOnly || !Element.CanToggle || !Enabled) return;
        var next = Element.NextChecked;
        if (next == Element.IsChecked) return;
        var args = new ChoiceTogglingEventArgs(next);
        OnToggling(args);
        if (!args.Cancel) Element.ApplyChecked(next);
    }

    void IFieldControl.FieldChanged()
    {
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
    }

    /// <summary>引发 <see cref="Toggling"/>。</summary>
    /// <param name="e">参数。</param>
    protected virtual void OnToggling(ChoiceTogglingEventArgs e) => Toggling?.Invoke(this, e);

    /// <summary>引发 <see cref="IsCheckedChanged"/> 并通知读屏状态变了。</summary>
    /// <param name="e">参数。</param>
    protected virtual void OnIsCheckedChanged(EventArgs e)
    {
        IsCheckedChanged?.Invoke(this, e);
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
    }

    /// <inheritdoc />
    protected override void OnClick(EventArgs e)
    {
        Toggle();
        base.OnClick(e);
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Element.Content = Text;
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Element.IsEnabled = Enabled;
    }

    /// <summary>卡片外观时不留画焦点环的 Padding：焦点环贴着方框，画在卡片的内边距里；卡片的边就是控件的边。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        var padding = Element.AsCard ? 0 : LogicalToDeviceUnits(4);
        if (Padding.All != padding) Padding = new Padding(padding);
        base.OnLayout(levent);
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Element.IsHovered = true;
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Element.IsHovered = false;
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) Focus();
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Space) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    /// <summary>空格松开时切换（同原生 checkbox）。</summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode == Keys.Space) Toggle();
    }

    /// <summary>焦点环只在键盘提示开着时显示（用鼠标点出来的焦点不显示，同 :focus-visible）。</summary>
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Element.IsFocusVisible = ShowFocusCues;
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Element.IsFocusVisible = false;
    }

    /// <inheritdoc />
    protected override void OnChangeUICues(UICuesEventArgs e)
    {
        base.OnChangeUICues(e);
        Element.IsFocusVisible = Focused && ShowFocusCues;
    }

    /// <summary>
    /// 读屏的共同部分：名称取 AccessibleName 或文字，都没有时是所在字段的标签（第一个控件认领）；帮助文字是说明，其次字段的说明与错误；
    /// 选中、只读的状态；默认动作是切换。
    /// </summary>
    /// <param name="owner">控件。</param>
    protected abstract class ChoiceAccessibleObject(ChoiceControl<T> owner) : ControlAccessibleObject(owner)
    {
        /// <inheritdoc />
        public override string? Name
        {
            get => base.Name is { Length: > 0 } name ? name
                : owner.Text is { Length: > 0 } text ? text
                : owner.Element.Field is { } scope && scope.Owns(owner) ? scope.Label : null;
            set => base.Name = value;
        }

        /// <inheritdoc />
        public override string? Help => FieldControl.DescribedBy(owner, owner.Element.DescriptionPart.Content, base.Help);

        /// <inheritdoc />
        public override AccessibleStates State
        {
            get
            {
                var state = base.State;
                if (owner.Element.IsChecked) state |= AccessibleStates.Checked;
                if (owner.Element.ReadOnly) state |= AccessibleStates.ReadOnly;
                return state;
            }
        }

        /// <inheritdoc />
        public override void DoDefaultAction() => owner.Toggle();
    }
}
