using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 文字提示（vue-tsx-pro 的 Tooltip）：沿用平台的 <c>ToolTip.Tip</c>，组件库补上 vue-tsx-pro 的外观与行为。
/// <code>
/// &lt;rune:IconButton Icon="{x:Static rune:Lucide.Copy}" AutomationProperties.Name="复制" ToolTip.Tip="复制（Ctrl+C）" /&gt;
/// &lt;rune:Button ToolTip.Tip="说明" rune:Tooltip.Placement="right-start" rune:Tooltip.Arrow="True"&gt;…&lt;/rune:Button&gt;
/// </code>
/// <list type="bullet">
/// <item>外观：反色底、sm 字号、sm 圆角，最宽 20em；淡入（durations.md、enter）。主题给平台的 ToolTip 换了样式，应用里所有提示都一样。</item>
/// <item>时机：指针停留 <c>ToolTip.ShowDelay</c>（有提示的元素默认改成 500 毫秒）后显示，刚关掉一个提示（<c>ToolTip.BetweenShowDelay</c>，默认改成 300 毫秒）
/// 时直接显示；按下触发器隐藏；指针能移到提示上；同一时间只有一个（这些是平台的）。</item>
/// <item>键盘：Tab、方向键聚焦触发器时立即显示，失焦、Esc 隐藏（组件库补的）。</item>
/// <item>位置：<see cref="PlacementProperty"/>（12 种，默认 top），空间不够时平台翻到另一边；<see cref="OffsetProperty"/> 是与触发器的距离；
/// <see cref="ArrowProperty"/> 显示指向触发器的箭头。</item>
/// <item>读屏：提示的文字是触发器的帮助文字（TS 的 aria-describedby）；触发器自己要有名称，提示里不放可交互的内容。</item>
/// </list>
/// 不显示：<c>ToolTip.ServiceEnabled="False"</c>；受控：<c>ToolTip.IsOpen</c>；开关事件：<c>ToolTip.ToolTipOpening</c>、<c>ToolTipClosing</c>。
/// </summary>
public static class Tooltip
{
    /// <summary>显示位置，默认 top；空间不够时翻到另一边。</summary>
    public static readonly AttachedProperty<FloatingPlacement> PlacementProperty =
        AvaloniaProperty.RegisterAttached<Control, FloatingPlacement>("Placement", typeof(Tooltip), FloatingPlacement.Top);

    /// <summary>与触发器的距离（像素），默认 6；有箭头时另加箭头伸出的一段。</summary>
    public static readonly AttachedProperty<double> OffsetProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("Offset", typeof(Tooltip), 6);

    /// <summary>显示指向触发器的箭头。</summary>
    public static readonly AttachedProperty<bool> ArrowProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Arrow", typeof(Tooltip));

    /// <summary>正显示着的提示的触发器（Esc 隐藏它）。</summary>
    static WeakReference<Control>? shown;

    static Tooltip()
    {
        ToolTip.ToolTipOpeningEvent.AddClassHandler<Control>((control, _) => Prepare(control));
        ToolTip.IsOpenProperty.Changed.AddClassHandler<Control>(OnIsOpenChanged);
        // 平台的 ToolTip 在第一次显示时才建：显示出来（Loaded，已经定好位置）时淡入、放箭头
        Control.LoadedEvent.AddClassHandler<ToolTip>((tip, _) => OnShown(tip));
        ToolTip.TipProperty.Changed.AddClassHandler<Control>((control, _) => Adopt(control));
        InputElement.GotFocusEvent.AddClassHandler<Control>(OnGotFocus);
        InputElement.LostFocusEvent.AddClassHandler<Control>(OnLostFocus);
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>(OnKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>取显示位置。</summary>
    public static FloatingPlacement GetPlacement(Control control) => control.GetValue(PlacementProperty);

    /// <summary>设显示位置。</summary>
    public static void SetPlacement(Control control, FloatingPlacement value) => control.SetValue(PlacementProperty, value);

    /// <summary>取与触发器的距离。</summary>
    public static double GetOffset(Control control) => control.GetValue(OffsetProperty);

    /// <summary>设与触发器的距离。</summary>
    public static void SetOffset(Control control, double value) => control.SetValue(OffsetProperty, value);

    /// <summary>取是否显示箭头。</summary>
    public static bool GetArrow(Control control) => control.GetValue(ArrowProperty);

    /// <summary>设是否显示箭头。</summary>
    public static void SetArrow(Control control, bool value) => control.SetValue(ArrowProperty, value);

    /// <summary>装上默认值与行为（RuneTheme 创建时调用；只执行一次）。</summary>
    internal static void Install()
    {
    }

    /// <summary>显示之前：平台的位置按 <see cref="PlacementProperty"/> 与书写方向换算。</summary>
    static void Prepare(Control control) =>
        control.SetCurrentValue(ToolTip.PlacementProperty, FloatingPopup.ToPlacementMode(GetPlacement(control), control.FlowDirection == FlowDirection.RightToLeft));

    /// <summary>提示的触发器：平台把提示放进一个 Popup，Popup 的目标就是触发器。</summary>
    internal static Control? Owner(ToolTip tip) => (tip.Parent as Popup)?.PlacementTarget;

    /// <summary>提示与触发器的距离：<see cref="OffsetProperty"/>，有箭头时加上箭头伸出的一段（边长的 0.7 倍：旋转后对角线的一半）。</summary>
    /// <param name="control">触发器。</param>
    /// <param name="arrowSize">箭头的边长。</param>
    internal static double Gap(Control control, double arrowSize) => GetOffset(control) + (GetArrow(control) ? 0.7 * arrowSize : 0);

    static void OnIsOpenChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) shown = new WeakReference<Control>(control);
        else if (shown is not null && shown.TryGetTarget(out var current) && current == control) shown = null;
    }

    static void OnShown(ToolTip tip)
    {
        if (Owner(tip) is not { } control) return;
        FadeIn(control, tip);
        PlaceArrow(control, tip);
    }

    /// <summary>淡入：durations.md、enter 缓动；系统关闭动画时直接显示。</summary>
    static void FadeIn(Control control, ToolTip tip)
    {
        var duration = Motion.Duration(control, ThemeKeys.Duration.Md);
        tip.Transitions = null;
        tip.Opacity = duration > TimeSpan.Zero ? 0 : 1;
        if (duration <= TimeSpan.Zero) return;
        tip.Transitions = [new DoubleTransition { Property = Visual.OpacityProperty, Duration = duration, Easing = control.Resource<Easing>(ThemeKeys.Easing.Enter) }];
        tip.Opacity = 1;
    }

    /// <summary>箭头贴着朝向触发器的那条边（<see cref="FloatingPopup.PlaceArrow"/>）。</summary>
    static void PlaceArrow(Control control, ToolTip tip)
    {
        if (tip.GetVisualDescendants().OfType<Border>().FirstOrDefault(part => part.Name == TooltipParts.Arrow) is not { } arrow) return;
        arrow.IsVisible = GetArrow(control);
        if (arrow.IsVisible && arrow.GetVisualParent() is Visual layer) FloatingPopup.PlaceArrow(arrow, layer, control);
    }

    /// <summary>键盘（Tab、方向键）聚焦触发器时立即显示：同 :focus-visible，用指针点出来的焦点不显示。</summary>
    static void OnGotFocus(Control control, FocusChangedEventArgs e)
    {
        if (e.Source != control || e.NavigationMethod is not (NavigationMethod.Tab or NavigationMethod.Directional)) return;
        if (ToolTip.GetTip(control) is null || !ToolTip.GetServiceEnabled(control)) return;
        if (!control.IsEffectivelyEnabled && !ToolTip.GetShowOnDisabled(control)) return;
        ToolTip.SetIsOpen(control, true);
    }

    static void OnLostFocus(Control control, FocusChangedEventArgs e)
    {
        if (e.Source == control && ToolTip.GetIsOpen(control)) control.ClearValue(ToolTip.IsOpenProperty);
    }

    /// <summary>Esc 隐藏正显示的提示；提示在对话框里时不让同一次 Esc 关掉对话框。</summary>
    static void OnKeyDown(TopLevel root, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || shown is null || !shown.TryGetTarget(out var control) || TopLevel.GetTopLevel(control) != root) return;
        control.ClearValue(ToolTip.IsOpenProperty);
        e.Handled = true;
    }

    /// <summary>
    /// 有了提示的触发器：用 vue-tsx-pro 的时机与位置（停留 500 毫秒，关掉后 300 毫秒内下一个直接显示，在上方，不按指针偏移）；
    /// 提示的文字是它的帮助文字（TS 的 aria-describedby）。都写在模板优先级上，使用方写的本地值、样式照样盖过。
    /// 平台的这几个附加属性注册在 Control 上，改不了 Control 自己的默认值，所以逐个触发器写。
    /// </summary>
    static void Adopt(Control control)
    {
        control.SetValue(ToolTip.ShowDelayProperty, 500, BindingPriority.Template);
        control.SetValue(ToolTip.BetweenShowDelayProperty, 300, BindingPriority.Template);
        control.SetValue(ToolTip.VerticalOffsetProperty, 0, BindingPriority.Template);
        var text = TextOf(ToolTip.GetTip(control));
        control.SetValue(AutomationProperties.HelpTextProperty, string.IsNullOrWhiteSpace(text) ? AvaloniaProperty.UnsetValue : text, BindingPriority.Template);
    }

    /// <summary>提示内容里的文字：字符串、文字块、内容控件的内容，面板里的逐个用空格连起来。</summary>
    static string? TextOf(object? content) => content switch
    {
        string text => text,
        TextBlock block => block.Text ?? block.Inlines?.Text,
        ContentControl { Content: var inner } => TextOf(inner),
        ILogical logical => string.Join(" ", logical.LogicalChildren.Select(TextOf).Where(text => !string.IsNullOrWhiteSpace(text))),
        _ => null,
    };
}

/// <summary>提示的部件名：样式里用 <c>ToolTip /template/ Border#PART_Arrow</c> 选中（TS 的 propsArrow）。</summary>
public static class TooltipParts
{
    /// <summary>提示的面板（反色底、圆角）。</summary>
    public const string Panel = "PART_Panel";

    /// <summary>箭头（旋转 45° 的小方块，尖角朝向触发器）。</summary>
    public const string Arrow = "PART_Arrow";
}

/// <summary>
/// 提示模板的根：在主轴两侧（上下或左右）留出与触发器的距离（<see cref="Tooltip.Gap"/>），面板与箭头层排在中间。
/// 距离做成提示自己的透明边，翻到另一边时照样对（平台弹出层的偏移量翻转时不跟着反向）；对齐的那条轴不留，start / end 对齐贴着触发器的边。
/// </summary>
internal sealed class TooltipLayout : Panel
{
    Thickness Gap()
    {
        if (TemplatedParent is not ToolTip tip || Tooltip.Owner(tip) is not { } control) return default;
        var arrow = Children.OfType<Canvas>().SelectMany(layer => layer.Children).FirstOrDefault();
        var gap = Tooltip.Gap(control, arrow?.Width ?? 0);
        return Tooltip.GetPlacement(control).IsVertical ? new Thickness(0, gap) : new Thickness(gap, 0);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var gap = Gap();
        var inner = availableSize.Deflate(gap);
        var size = default(Size);
        foreach (var child in Children)
        {
            child.Measure(inner);
            size = new Size(Math.Max(size.Width, child.DesiredSize.Width), Math.Max(size.Height, child.DesiredSize.Height));
        }
        return size.Inflate(gap);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var inner = new Rect(finalSize).Deflate(Gap());
        foreach (var child in Children) child.Arrange(inner);
        return finalSize;
    }
}
