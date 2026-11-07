using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>为什么开、关（vue-tsx-pro 的 OpenChangeReason）：只报告用户的操作，代码里（父级、绑定）改 IsOpen 时不报告。</summary>
public enum OpenChangeReason
{
    /// <summary>点了触发器。</summary>
    Trigger,

    /// <summary>点了浮层外面（浮层窗口失去了激活）。</summary>
    Outside,

    /// <summary>按了 Esc。</summary>
    Escape,

    /// <summary>点了关闭按钮、取消按钮。</summary>
    Close,

    /// <summary>点了确认按钮（对话框）。</summary>
    Confirm,

    /// <summary>选中了一项（单选的下拉选中后关闭）。</summary>
    Select,

    /// <summary>焦点离开了（Tab 移走）。</summary>
    Focus,

    /// <summary>代码调用：快捷键打开（命令面板）、命令式打开的对话框。</summary>
    Imperative,
}

/// <summary>浮层开关变了的事件参数。</summary>
/// <param name="isOpen">现在开着。</param>
/// <param name="reason">为什么。</param>
public sealed class OpenChangedEventArgs(bool isOpen, OpenChangeReason reason) : EventArgs
{
    /// <summary>现在开着。</summary>
    public bool IsOpen { get; } = isOpen;

    /// <summary>为什么。</summary>
    public OpenChangeReason Reason { get; } = reason;
}

/// <summary>
/// 弹出框（vue-tsx-pro 的 Popover，非模态）：点触发器在旁边弹出一块内容（筛选条件、简短表单、详情），点外面或 Esc 关闭。
/// <code>
/// var sharing = new PopoverControl { Trigger = new ButtonControl { Icon = Lucide.Share2, Text = "分享" }, Content = sharePanel, Arrow = true };
/// sharing.OpenChanged += (_, e) => …;
/// </code>
/// 触发器照常排在原处（控件的大小就是触发器的大小），点它（Click）开关。浮层是两层窗口（<see cref="FloatingSurface"/>，激活模式）：底下的分层窗口
/// 画面板（surfaceRaised、xs 描边、lg 圆角、lg 阴影）与箭头，上面的普通窗口放内容，只盖内边距（space.md）以内。位置 <see cref="Placement"/>（12 种，默认 bottom-start，空间不够时翻到另一边、
/// 贴着屏幕边挪回来），与触发器隔 <see cref="Offset"/>，<see cref="Arrow"/> 时有指向触发器的箭头。打开时内容窗口激活、焦点移到第一个能聚焦的控件，
/// 关上后回到触发器。内容窗口失去激活（点了外面、切到别的窗口）、Esc 关闭；<see cref="OpenChanged"/> 报告原因。
/// 读屏：内容窗口是对话框，以触发器为名称。
/// </summary>
public class PopoverControl : Control, IBaseline, IScopeAware
{
    readonly FloatingSurface surface;
    long closedAt = long.MinValue / 2;
    bool open;

    /// <summary>新建：没有触发器与内容，位置 bottom-start，距离 6。</summary>
    public PopoverControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        surface = new FloatingSurface(this);
        surface.Form.AccessibleRole = AccessibleRole.Dialog;
        surface.Outside += (_, _) => SetOpen(false, OpenChangeReason.Outside);
        surface.Form.Escape += (_, _) => SetOpen(false, OpenChangeReason.Escape);
    }

    /// <summary>触发器（一般是按钮）：照常排在原处，点它开关。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Trigger
    {
        get;
        set
        {
            if (field == value) return;
            if (field is not null)
            {
                field.Click -= OnTriggerClick;
                Controls.Remove(field);
            }
            field = value;
            if (value is not null)
            {
                value.Click += OnTriggerClick;
                Controls.Add(value);
            }
            PerformLayout();
        }
    }

    /// <summary>浮层里的内容（铺满内容窗口；大小取它想要的大小，至少 MinimumSize）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content
    {
        get;
        set
        {
            if (field == value) return;
            if (field is not null) surface.Form.Controls.Remove(field);
            field = value;
            if (value is null) return;
            value.Dock = DockStyle.Fill;
            surface.Form.Controls.Add(value);
        }
    }

    /// <summary>开着。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => open; set => SetOpen(value, null); }

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    [Category("Behavior")]
    [Description("弹出位置：边加对齐，共 12 种，默认 bottom-start。")]
    public FloatingPlacement Placement { get; set; } = FloatingPlacement.BottomStart;

    /// <summary>与触发器的距离（设备无关像素），默认 6；有箭头时另加箭头伸出的一段。</summary>
    [Category("Behavior")]
    [Description("与触发器的距离（设备无关像素），默认 6。")]
    [DefaultValue(6f)]
    public float Offset { get; set; } = 6;

    /// <summary>显示指向触发器的箭头。</summary>
    [Category("Appearance")]
    [Description("显示指向触发器的箭头。")]
    [DefaultValue(false)]
    public bool Arrow { get; set; }

    bool ShouldSerializePlacement() => Placement != FloatingPlacement.BottomStart;

    void ResetPlacement() => Placement = FloatingPlacement.BottomStart;

    /// <summary>用户开、关了（触发器、点外面、Esc）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>放内容的窗口（测试用）。</summary>
    internal Form Surface => surface.Form;

    /// <summary>画面板的窗口（测试用）；没打开过时为 null。</summary>
    internal LayeredWindow? Backdrop => surface.Backdrop;

    /// <inheritdoc />
    public int? Baseline => (Trigger as IBaseline)?.Baseline;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => Trigger?.GetPreferredSize(proposedSize) ?? Size.Empty;

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (Trigger is not { } trigger) return;
        // 触发器自己变了大小（文字变了）：跟着变，再铺满
        if (levent.AffectedControl == trigger && AutoSize) Size = GetPreferredSize(Size.Empty);
        trigger.Bounds = ClientRectangle;
    }

    void IScopeAware.ScopeChanged()
    {
        ProviderControl.NotifyDescendants(surface.Form);
        if (IsOpen) Present();
    }

    /// <summary>
    /// 点了触发器：开关。浮层开着时点触发器，内容窗口先因为失去激活（点了外面）关了，紧接着这次点击又会把它打开：刚这样关掉的那一下不再打开。
    /// </summary>
    void OnTriggerClick(object? sender, EventArgs e)
    {
        if (!IsOpen && Environment.TickCount64 - closedAt < 300) return;
        SetOpen(!IsOpen, OpenChangeReason.Trigger);
    }

    void SetOpen(bool next, OpenChangeReason? reason)
    {
        if (next == open) return;
        open = next;
        if (next) Present();
        else Dismiss();
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(next, known));
    }

    /// <summary>面板 = 内容 + 内边距 + 描边，位置同提示（<see cref="FloatingLayout.Position"/>）；交给两层窗口画好、显示。</summary>
    void Present()
    {
        if (!IsHandleCreated) return;
        var context = ElementContext.Of(this);
        var em = context.Em;
        var inset = (int)MathF.Round(Tokens.Space.Md * em) + FloatingSurface.Border(context);
        var content = Content is { } control ? Measure(control) : Size.Empty;
        var panelSize = new Size(content.Width + 2 * inset, content.Height + 2 * inset);
        var arrow = Arrow ? 0.625f * em : 0;
        var gap = context.Scale(Offset) + 0.7f * arrow;
        var target = RectangleToScreen(ClientRectangle);
        var (side, panel) = FloatingLayout.Position(Placement, context.RightToLeft, target, panelSize, gap, Screen.FromControl(this).WorkingArea);
        surface.Form.AccessibleName = Trigger is { } trigger ? trigger.AccessibleName ?? trigger.Text : null;
        surface.Show(panel, inset, context, Arrow ? (side, target, arrow) : null);
    }

    static Size Measure(Control content)
    {
        var size = content.GetPreferredSize(Size.Empty);
        return new Size(Math.Max(size.Width, content.MinimumSize.Width), Math.Max(size.Height, content.MinimumSize.Height));
    }

    /// <summary>藏起两层窗口；焦点在里面（Esc、代码里关）时回到触发器。</summary>
    void Dismiss()
    {
        closedAt = Environment.TickCount64;
        if (!surface.Hide()) return;
        FindForm()?.Activate();
        Trigger?.Focus();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Trigger is { } trigger) trigger.Click -= OnTriggerClick;
            surface.Dispose();
        }
        base.Dispose(disposing);
    }
}
