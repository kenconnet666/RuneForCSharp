using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 加载中的旋转指示（vue-tsx-pro 的 Spinner）：Lucide 的 LoaderCircle 加旋转，默认跟随所在文字的大小与颜色。
/// <code>
/// &lt;rune:Spinner /&gt;                       &lt;!-- 装饰性：旁边已经有"加载中"之类的文字 --&gt;
/// &lt;rune:Spinner Label="正在加载订单" /&gt;   &lt;!-- 单独出现：读屏当作进度条，念出 Label --&gt;
/// </code>
/// 图标是部件（TS 的 propsIcon）：模板里的图标套部件样式 <see cref="SpinnerParts.Icon"/>，图标与大小写在主题的部件样式里，
/// Provider.Styles 里同键的样式逐个 setter 叠在上面（DESIGN §7）。旋转在代码里开：系统关闭动画时转慢一半，不停
/// （旋转本身就是"还在加载"的信息，同 TS 的 _reducedMotion）。
/// </summary>
[TemplatePart(Name = IconPart, Type = typeof(Icon))]
public class Spinner : Control
{
    const string IconPart = "PART_Icon";

    /// <summary>读屏念的说明；给了就是进度条（TS 的 role="status"），不给就是装饰性的（对读屏隐藏）。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Spinner));

    /// <summary>大小：档位（xs–xl）或像素数；不写时 1em，跟随所在文字。主题的部件样式把它转给图标。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Spinner));

    static Spinner()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Spinner), new FrameworkPropertyMetadata(typeof(Spinner)));
        FocusableProperty.OverrideMetadata(typeof(Spinner), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Spinner), new FrameworkPropertyMetadata(false));
    }

    /// <summary>读屏念的说明。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>大小。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild(IconPart) is not Icon icon) return;
        var rotation = new RotateTransform();
        icon.RenderTransformOrigin = new Point(0.5, 0.5);
        icon.RenderTransform = rotation;
        var period = TimeSpan.FromSeconds(Motion.Enabled ? 0.8 : 1.6);
        rotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, period) { RepeatBehavior = RepeatBehavior.Forever });
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SpinnerAutomationPeer(this);

    /// <summary>有 Label 时是进度条（不确定进度），名字是 Label；没有时是装饰，不进控件树与内容树。</summary>
    sealed class SpinnerAutomationPeer(Spinner owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override string GetClassNameCore() => nameof(Spinner);

        protected override string GetNameCore() => ((Spinner)Owner).Label is { Length: > 0 } label ? label : base.GetNameCore();

        protected override bool IsControlElementCore() => !string.IsNullOrEmpty(((Spinner)Owner).Label);

        protected override bool IsContentElementCore() => IsControlElementCore();
    }
}

/// <summary>Spinner 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:SpinnerParts.Icon}" TargetType="rune:Icon"&gt;</c>。</summary>
public static class SpinnerParts
{
    /// <summary>旋转的图标（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(Spinner), nameof(Icon), typeof(Icon));
}
