using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 按钮家族的宿主（<see cref="ButtonControl"/>、<see cref="IconButtonControl"/> 共用）：外观属性转给元素，处理输入。
/// 悬停、按下（波纹）、键盘（Enter 按下就点击、空格松开点击，同 Windows 按钮）、焦点（只在键盘提示开着时显示焦点环）。
/// 实现 <see cref="IButtonControl"/>，能当窗体的 AcceptButton、CancelButton。加载中点击无效（Click 不触发），焦点保留。
/// 链接按钮（元素有 Href）：Click 之后用系统打开链接，读屏里是链接，外部链接的名称后补上"在新窗口打开"。
/// 默认留 4 像素的 Padding：焦点环画在按钮外面一圈（CSS 的 outline 不占位，WinForms 的控件画不出自己的边界）。
/// </summary>
/// <typeparam name="T">按钮元素。</typeparam>
public abstract class ButtonControlBase<T> : ElementControl<T>, IButtonControl where T : ButtonCore, new()
{
    /// <summary>元素的默认值：设计器只序列化与它不同的属性。</summary>
    static readonly T Defaults = new();

    Action? releasePointerRipple;
    Action? releaseKeyRipple;

    /// <summary>新建按钮：能获得焦点、进 Tab 顺序。</summary>
    protected ButtonControlBase()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        Padding = new Padding(LogicalToDeviceUnits(4));
        Cursor = Cursors.Hand;
    }

    /// <summary>样式。</summary>
    [Category("Appearance")]
    [Description("样式：solid、soft、surface、outline、ghost、link。")]
    public ButtonVariant Variant { get => Element.Variant; set => Element.Variant = value; }

    /// <summary>色调。</summary>
    [Category("Appearance")]
    [Description("色调：neutral、accent、info、success、warning、danger。")]
    public StatusTone Tone { get => Element.Tone; set => Element.Tone = value; }

    /// <summary>字号档位。</summary>
    [Category("Appearance")]
    [Description("字号：xs–xl 或像素数；空着时跟随字体。")]
    public TextSize TextSize { get => Element.Size; set => Element.Size = value; }

    /// <summary>圆角。</summary>
    [Category("Appearance")]
    [Description("圆角：none、xs、sm、md、lg、xl、full，默认 md。")]
    public Radius Radius { get => Element.Radius; set => Element.Radius = value; }

    /// <summary>图标（ButtonControl 里在文字前；预设按钮有自己的默认图标）。</summary>
    [Category("Appearance")]
    [Description("图标：Lucide 的图标名。")]
    public IconData? Icon { get => Element.Icon; set => Element.Icon = value; }

    /// <summary>加载中。</summary>
    [Category("Behavior")]
    [Description("加载中：显示旋转指示，宽度不变，点击无效。")]
    [DefaultValue(false)]
    public bool IsLoading { get => Element.IsLoading; set => Element.IsLoading = value; }

    /// <summary>按下时的波纹；不写时沿用全局开关。</summary>
    [Category("Behavior")]
    [Description("按下时的波纹；空着时沿用 Ripple.Enabled。")]
    [DefaultValue(null)]
    public bool? Ripple { get => Element.Ripple; set => Element.Ripple = value; }

    /// <summary>图标部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsIcon { get => Element.PropsIcon; set => Element.PropsIcon = value; }

    /// <summary>外观覆盖（代码里设，上游的 sx）：盖过作用域的 <see cref="ButtonSx"/> 与配方。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonSx? Sx { get => Element.Sx; set => Element.Sx = value; }

    /// <summary>加载指示部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsSpinner { get => Element.PropsSpinner; set => Element.PropsSpinner = value; }

    /// <summary>当窗体的 AcceptButton、CancelButton 时，点击后窗体的 DialogResult。</summary>
    [Category("Behavior")]
    [DefaultValue(DialogResult.None)]
    public DialogResult DialogResult { get; set; }

    bool ShouldSerializeVariant() => Element.Explicit.Variant;

    void ResetVariant() => Variant = default;

    bool ShouldSerializeTone() => Element.Explicit.Tone;

    void ResetTone() => Tone = default;

    bool ShouldSerializeTextSize() => Element.Explicit.Size;

    void ResetTextSize() => TextSize = default;

    bool ShouldSerializeRadius() => Element.Explicit.Radius;

    void ResetRadius() => Radius = default;

    bool ShouldSerializeIcon() => Icon != Defaults.Icon;

    void ResetIcon() => Icon = Defaults.Icon;

    /// <summary>没写 AccessibleName（也没有文字）时的读屏名称（预设按钮取语言包）。</summary>
    protected virtual string? DefaultAccessibleName => null;

    /// <summary>没写 AccessibleDescription 时的读屏说明（UserButton 的说明行）。</summary>
    protected virtual string? DefaultAccessibleDescription => null;

    /// <summary>窗体把它设成或取消默认按钮时调用（同 WinForms 的 Button）。</summary>
    public void NotifyDefault(bool value) => Invalidate();

    /// <summary>代码里点击（加载中、不可用时无效，由 OnClick 拦下）。</summary>
    public void PerformClick() => OnClick(EventArgs.Empty);

    /// <summary>加载中、不可用时点击无效；当窗体的默认按钮时设窗体的 DialogResult。</summary>
    protected override void OnClick(EventArgs e)
    {
        if (Element.IsLoading || !Enabled) return;
        if (DialogResult != DialogResult.None && FindForm() is { } form) form.DialogResult = DialogResult;
        base.OnClick(e);
        if (Element is ILink { Href: { IsAbsoluteUri: true } href }) Navigate(href);
    }

    /// <summary>用系统打开链接（Shell 的默认程序：浏览器、邮件……）。</summary>
    /// <param name="href">绝对地址。</param>
    protected virtual void Navigate(Uri href) => LinkLauncher.Open(href);

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Element.IsEnabled = Enabled;
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
        ReleasePointerRipple();
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        if (Element.RippleOn)
        {
            releasePointerRipple?.Invoke();
            releasePointerRipple = Element.StartRipple(new PointF(e.X - Element.Bounds.X, e.Y - Element.Bounds.Y));
        }
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        ReleasePointerRipple();
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    /// <summary>Enter 按下就点击；空格按下出波纹、松开点击。从中心扩散，按住不放的重复按键不再出新的波纹。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is not (Keys.Enter or Keys.Space)) return;
        if (Element.RippleOn && releaseKeyRipple is null)
            releaseKeyRipple = Element.StartRipple(new PointF(Element.Bounds.Width / 2f, Element.Bounds.Height / 2f));
        if (e.KeyCode == Keys.Enter) PerformClick();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        ReleaseKeyRipple();
        if (e.KeyCode == Keys.Space) PerformClick();
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
        ReleaseKeyRipple();
    }

    /// <inheritdoc />
    protected override void OnChangeUICues(UICuesEventArgs e)
    {
        base.OnChangeUICues(e);
        Element.IsFocusVisible = Focused && ShowFocusCues;
    }

    void ReleasePointerRipple()
    {
        releasePointerRipple?.Invoke();
        releasePointerRipple = null;
    }

    void ReleaseKeyRipple()
    {
        releaseKeyRipple?.Invoke();
        releaseKeyRipple = null;
    }

    /// <summary>读屏念的快捷键（aria-keyshortcuts，如 Control+K）；null 时没有。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string? KeyboardShortcut { get; set; }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ButtonAccessibleObject(this);

    /// <summary>
    /// 读屏里是按钮（链接按钮是链接）：默认动作是按下；加载中状态里带"忙"；没有名称时用 <see cref="DefaultAccessibleName"/>，
    /// 外部链接的名称后补上语言包的"在新窗口打开"。
    /// </summary>
    sealed class ButtonAccessibleObject(ButtonControlBase<T> owner) : ControlAccessibleObject(owner)
    {
        public override string? Name
        {
            get
            {
                var name = base.Name is { Length: > 0 } own ? own : owner.DefaultAccessibleName;
                return owner.Element is ILink { External: true } ? name + ProviderControl.LangOf(owner).LinkNewWindow : name;
            }
            set => base.Name = value;
        }

        public override string? Description => base.Description is { Length: > 0 } own ? own : owner.DefaultAccessibleDescription;

        public override AccessibleRole Role => Owner is { AccessibleRole: not AccessibleRole.Default } control ? control.AccessibleRole
            : owner.Element is ILink ? AccessibleRole.Link : AccessibleRole.PushButton;

        public override string DefaultAction => "按下";

        public override string? KeyboardShortcut => owner.KeyboardShortcut ?? base.KeyboardShortcut;

        public override AccessibleStates State => base.State | (owner.IsLoading ? AccessibleStates.Busy : 0) | TriggerStates();

        /// <summary>弹出框、菜单的触发器（TS 的 aria-haspopup、aria-expanded）：有弹出、开着时展开、关着时收起。</summary>
        AccessibleStates TriggerStates() => owner.Parent switch
        {
            MenuControl menu => AccessibleStates.HasPopup | (menu.IsOpen ? AccessibleStates.Expanded : AccessibleStates.Collapsed),
            PopoverControl popover => AccessibleStates.HasPopup | (popover.IsOpen ? AccessibleStates.Expanded : AccessibleStates.Collapsed),
            _ => 0,
        };

        public override void DoDefaultAction() => (Owner as ButtonControlBase<T>)?.PerformClick();
    }
}

/// <summary>有文字的按钮的宿主（<see cref="ButtonControl"/>、<see cref="LinkButtonControl"/> 共用）：文字内容就是控件的 <see cref="Control.Text"/>。</summary>
/// <typeparam name="T">按钮元素。</typeparam>
public abstract class ButtonControl<T> : ButtonControlBase<T> where T : Button, new()
{
    /// <summary>文字后的图标。</summary>
    [Category("Appearance")]
    [Description("文字后的图标：Lucide 的图标名。")]
    [DefaultValue(null)]
    public IconData? IconEnd { get => Element.IconEnd; set => Element.IconEnd = value; }

    /// <summary>占满宽度（AutoSize 时按父控件给的宽度）。</summary>
    [Category("Layout")]
    [Description("占满宽度。")]
    [DefaultValue(false)]
    public bool FullWidth { get => Element.FullWidth; set => Element.FullWidth = value; }

    /// <summary>后置图标部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsIconEnd { get => Element.PropsIconEnd; set => Element.PropsIconEnd = value; }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Element.Content = Text;
    }
}

/// <summary>
/// 放进窗体、设计器里用的 <see cref="Button"/>：文字内容就是控件的 <see cref="Control.Text"/>，字号档位叫 <see cref="ButtonControlBase{T}.TextSize"/>。
/// 输入、焦点、无障碍见 <see cref="ButtonControlBase{T}"/>。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[DefaultProperty(nameof(Text))]
[Description("按钮：样式 × 色调 × 字号档位 × 圆角，按下有波纹。")]
public class ButtonControl : ButtonControl<Button>;
