using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;

namespace Rune.Wpf;

/// <summary>
/// 工具栏（vue-tsx-pro 的 Toolbar，WAI-ARIA 的 Toolbar 模式）：一组相关的按钮、链接。整组只占一个 Tab 位置，组内用方向键移动（漫游焦点），
/// 键盘用户不必一个个 Tab 过去。名称写 <c>AutomationProperties.Name</c>。
/// <code>
/// &lt;rune:Toolbar AutomationProperties.Name="文字格式"&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.Bold}" AutomationProperties.Name="加粗" /&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.Italic}" AutomationProperties.Name="斜体" /&gt;
/// &lt;/rune:Toolbar&gt;
/// </code>
/// 是一个 Flex：横排、放不下换行、交叉轴居中，项之间 space.xs；竖排时拉伸（主题样式）。里面的输入框不参与方向键移动，用 Tab 进出。
/// 和 WPF 自带的 ToolBar（带溢出菜单的工具条）不是一回事。
/// </summary>
public class Toolbar : Flex
{
    /// <summary>方向，默认 horizontal（左右键移动）；vertical 时上下键移动。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(ToolbarOrientation), typeof(Toolbar),
        new FrameworkPropertyMetadata(default(ToolbarOrientation), (d, _) => ((Toolbar)d).focus.Orientation = ((Toolbar)d).Orientation.Value ?? "horizontal"));

    /// <summary>到两端后是否从另一端继续，默认 true。</summary>
    public static readonly DependencyProperty LoopProperty = DependencyProperty.Register(
        nameof(Loop), typeof(bool), typeof(Toolbar), new FrameworkPropertyMetadata(true, (d, _) => ((Toolbar)d).focus.Loop = ((Toolbar)d).Loop));

    readonly RovingFocus focus;

    static Toolbar() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Toolbar), new FrameworkPropertyMetadata(typeof(Toolbar)));

    /// <summary>新建：按钮参与方向键移动。</summary>
    public Toolbar() => focus = new RovingFocus(this, element => element is System.Windows.Controls.Primitives.ButtonBase);

    /// <summary>方向。</summary>
    public ToolbarOrientation Orientation { get => (ToolbarOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>到两端后是否从另一端继续。</summary>
    public bool Loop { get => (bool)GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>排好之后定 Tab 停靠点（项增删、隐藏、变得不可用时都会重排）。</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        focus.Sync();
        return size;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ToolbarAutomationPeer(this);

    sealed class ToolbarAutomationPeer(Toolbar owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;

        protected override string GetClassNameCore() => nameof(Toolbar);
    }
}

/// <summary>工具栏方向（vue-tsx-pro 的 ToolbarOrientations）。</summary>
/// <param name="Value">取值（horizontal、vertical）。</param>
[TypeConverter(typeof(VariantConverter<ToolbarOrientation>))]
public readonly record struct ToolbarOrientation(string Value) : IVariant<ToolbarOrientation>
{
    /// <summary>横排（默认），左右键移动。</summary>
    public static readonly ToolbarOrientation Horizontal = new("horizontal");

    /// <summary>竖排，上下键移动。</summary>
    public static readonly ToolbarOrientation Vertical = new("vertical");

    /// <inheritdoc />
    public static IReadOnlyList<ToolbarOrientation> Known { get; } = [Horizontal, Vertical];

    /// <inheritdoc />
    public static ToolbarOrientation Parse(string value)
    {
        var orientation = new ToolbarOrientation(value);
        Variants.WarnIfUnknown(orientation);
        return orientation;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
