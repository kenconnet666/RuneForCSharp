using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 加载中的旋转指示（vue-tsx-pro 的 Spinner）：Lucide 的 LoaderCircle 加旋转，默认跟随宿主的字体大小与文字颜色。
/// 无窗口元素，放进窗体用 <see cref="SpinnerControl"/>。
/// <code>
/// new SpinnerControl();                           // 装饰性：旁边已经有"加载中"之类的文字
/// new SpinnerControl { Label = "正在加载订单" };   // 单独出现：读屏当作进度条，念出 Label
/// </code>
/// 图标是部件（<see cref="PropsIcon"/>，TS 的 propsIcon）：Spinner 给图标与大小，PropsIcon 逐字段盖在上面。
/// 旋转读 <see cref="FrameClock"/>：只有在画的 Spinner 驱动时钟，隐藏了就停。系统关闭动画时转慢一半，不停
/// （旋转本身就是"还在加载"的信息，同 TS 的 _reducedMotion）。
/// </summary>
public class Spinner : Element
{
    Icon icon = new();

    /// <summary>新建：图标是 LoaderCircle。</summary>
    public Spinner() => Rebuild();

    /// <summary>读屏念的说明；给了就是进度条（TS 的 role="status"），不给就是装饰性的。</summary>
    public string? Label { get; set; }

    /// <summary>大小：档位（xs–xl）或像素数；不写时 1em，跟随宿主的字体。</summary>
    public TextSize Size
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Rebuild();
        }
    }

    /// <summary>图标部件的属性：写了的字段盖住 Spinner 给的（图标、大小）。</summary>
    public IconProps? PropsIcon
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Rebuild();
        }
    }

    /// <summary>合并好的图标部件（测试用）。</summary>
    internal Icon IconPart => icon;

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available) => icon.Measure(context, available);

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var period = Motion.Enabled ? 0.8 : 1.6;
        var angle = (float)(FrameClock.Shared.Now.TotalSeconds % period / period * 360);
        var center = new PointF(Bounds.X + Bounds.Width / 2f, Bounds.Y + Bounds.Height / 2f);
        var state = graphics.Save();
        try
        {
            graphics.TranslateTransform(center.X, center.Y);
            graphics.RotateTransform(angle);
            graphics.TranslateTransform(-center.X, -center.Y);
            icon.Bounds = Bounds;
            icon.Paint(graphics, context);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>重新合成图标部件：Spinner 给的在下，PropsIcon 在上；没写的字段回到图标自己的默认值。</summary>
    void Rebuild()
    {
        icon = new Icon();
        new IconProps { Data = Lucide.LoaderCircle, Size = Size }.Merge(PropsIcon).ApplyTo(icon);
        InvalidateMeasure();
    }
}

/// <summary>
/// 放进窗体、设计器里用的 <see cref="Spinner"/>。控件自己有 <see cref="Control.Size"/>，大小档位在这里叫 <see cref="IconSize"/>。
/// 不能获得焦点；有 Label 时读屏当作进度条（名字只取 Label），没有时是动画（没有名字）。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Label))]
[Description("加载中的旋转指示：大小跟随字体，颜色跟随 ForeColor。")]
public class SpinnerControl : ElementControl<Spinner>
{
    /// <summary>新建一个旋转指示。</summary>
    public SpinnerControl() => SetStyle(ControlStyles.Selectable, false);

    /// <summary>读屏念的说明。</summary>
    [Category("Accessibility")]
    [Description("读屏念的说明；空着时是装饰性的。")]
    [DefaultValue(null)]
    public string? Label { get => Element.Label; set => Element.Label = value; }

    /// <summary>大小：档位（xs–xl）或像素数；不写时跟随字体（1em）。</summary>
    [Category("Layout")]
    [Description("大小：xs–xl 或像素数；空着时跟随字体。")]
    public TextSize IconSize { get => Element.Size; set => Element.Size = value; }

    /// <summary>图标部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsIcon { get => Element.PropsIcon; set => Element.PropsIcon = value; }

    bool ShouldSerializeIconSize() => IconSize != default;

    void ResetIconSize() => IconSize = default;

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new SpinnerAccessibleObject(this);

    sealed class SpinnerAccessibleObject(SpinnerControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => string.IsNullOrEmpty(Label) ? AccessibleRole.Animation : AccessibleRole.ProgressBar;

        /// <summary>只取 Label：没有时不借用前面 Label 控件的文字（同 IconControl）。</summary>
        public override string? Name
        {
            get => Label;
            set { }
        }

        public override AccessibleStates State => base.State & ~AccessibleStates.Focusable;

        string? Label => ((SpinnerControl)Owner!).Label;
    }
}
