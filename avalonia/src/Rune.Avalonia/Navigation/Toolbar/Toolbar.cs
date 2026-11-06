using System.ComponentModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 工具栏（vue-tsx-pro 的 Toolbar，WAI-ARIA 的 Toolbar 模式）：一组相关的按钮、链接。整组只占一个 Tab 位置，组内用方向键移动（漫游焦点），
/// 键盘用户不必一个个 Tab 过去。名称写 <c>AutomationProperties.Name</c>。
/// <code>
/// &lt;rune:Toolbar AutomationProperties.Name="文字格式"&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.Bold}" AutomationProperties.Name="加粗" /&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.Italic}" AutomationProperties.Name="斜体" /&gt;
/// &lt;/rune:Toolbar&gt;
/// </code>
/// 是一个 Flex：横排、放不下换行、交叉轴居中，项之间 space.xs；竖排时拉伸。里面的输入框不参与方向键移动（左右键要用来移动光标），用 Tab 进出。
/// </summary>
public class Toolbar : Flex
{
    /// <summary>方向，默认 horizontal（左右键移动）；vertical 时上下键移动。</summary>
    public static readonly StyledProperty<ToolbarOrientation> OrientationProperty = AvaloniaProperty.Register<Toolbar, ToolbarOrientation>(nameof(Orientation));

    /// <summary>到两端后是否从另一端继续，默认 true。</summary>
    public static readonly StyledProperty<bool> LoopProperty = AvaloniaProperty.Register<Toolbar, bool>(nameof(Loop), true);

    readonly RovingFocus focus;

    /// <summary>新建：按钮、链接参与方向键移动。</summary>
    public Toolbar() => focus = new RovingFocus(this, control => control is global::Avalonia.Controls.Button);

    /// <summary>方向。</summary>
    public ToolbarOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>到两端后是否从另一端继续。</summary>
    public bool Loop { get => GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty) focus.Orientation = Orientation.Value ?? "horizontal";
        else if (change.Property == LoopProperty) focus.Loop = Loop;
    }

    /// <summary>排好之后定 Tab 停靠点（项增删、隐藏、变得不可用时都会重排）。</summary>
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        focus.Sync();
        return size;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ToolbarAutomationPeer(this);

    sealed class ToolbarAutomationPeer(Toolbar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;
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
