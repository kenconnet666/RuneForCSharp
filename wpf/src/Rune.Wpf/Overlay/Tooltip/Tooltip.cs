using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 文字提示（vue-tsx-pro 的 Tooltip）：沿用平台的 <c>ToolTip</c> 属性与 ToolTipService，组件库补上 vue-tsx-pro 的外观与行为。
/// <code>
/// &lt;rune:IconButton Icon="{x:Static rune:Lucide.Copy}" AutomationProperties.Name="复制" ToolTip="复制（Ctrl+C）" /&gt;
/// &lt;rune:Button ToolTip="说明" rune:Tooltip.Placement="right-start" rune:Tooltip.Arrow="True"&gt;…&lt;/rune:Button&gt;
/// </code>
/// <list type="bullet">
/// <item>外观：反色底、sm 字号、sm 圆角，最宽 20em，没有系统阴影；淡入（durations.md、enter）。RuneTheme 带了平台 ToolTip 的隐式样式，应用里所有提示都一样。</item>
/// <item>时机：指针停留 <c>ToolTipService.InitialShowDelay</c>（默认改成 500 毫秒）后显示，刚关掉一个提示（<c>BetweenShowDelay</c>，默认改成 300 毫秒）
/// 时直接显示；键盘聚焦时显示；指针能移到提示上；同一时间只有一个（这些是平台的）。Esc 隐藏（组件库补的）。</item>
/// <item>位置：<see cref="PlacementProperty"/>（12 种，默认 top），空间不够时翻到另一边；<see cref="OffsetProperty"/> 是与触发器的距离；
/// <see cref="ArrowProperty"/> 显示指向触发器的箭头。触发器上写了 <c>ToolTipService.Placement</c> 时按平台的位置。</item>
/// <item>读屏：提示是文字时平台把它当作触发器的帮助文字（TS 的 aria-describedby）；触发器自己要有名称，提示里不放可交互的内容。</item>
/// </list>
/// 不显示：<c>ToolTipService.IsEnabled="False"</c>；开关事件：<c>ToolTipOpening</c>、<c>ToolTipClosing</c>。
/// </summary>
public static class Tooltip
{
    /// <summary>显示位置，默认 top；空间不够时翻到另一边。</summary>
    public static readonly DependencyProperty PlacementProperty = DependencyProperty.RegisterAttached(
        "Placement", typeof(FloatingPlacement), typeof(Tooltip), new PropertyMetadata(FloatingPlacement.Top));

    /// <summary>与触发器的距离（像素），默认 6；有箭头时另加箭头伸出的一段。</summary>
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.RegisterAttached(
        "Offset", typeof(double), typeof(Tooltip), new PropertyMetadata(6d));

    /// <summary>显示指向触发器的箭头（写在触发器上）。</summary>
    public static readonly DependencyProperty ArrowProperty = DependencyProperty.RegisterAttached(
        "Arrow", typeof(bool), typeof(Tooltip), new PropertyMetadata(false));

    /// <summary>提示样式用的转换器：触发器 → 按 <see cref="PlacementProperty"/> 定位的回调。</summary>
    public static IValueConverter PlacementCallback { get; } = new PlacementCallbackConverter();

    /// <summary>正显示着的提示（Esc 隐藏它）。</summary>
    static WeakReference<ToolTip>? shown;

    static Tooltip()
    {
        // vue-tsx-pro 的时机：停留 500 毫秒，关掉后 300 毫秒内下一个直接显示
        ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(500));
        ToolTipService.BetweenShowDelayProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(300));
        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler((sender, _) => OnOpened((ToolTip)sender)));
        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.ClosedEvent, new RoutedEventHandler((sender, _) => OnClosed((ToolTip)sender)));
        EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
    }

    /// <summary>取显示位置。</summary>
    public static FloatingPlacement GetPlacement(DependencyObject element) => (FloatingPlacement)element.GetValue(PlacementProperty);

    /// <summary>设显示位置。</summary>
    public static void SetPlacement(DependencyObject element, FloatingPlacement value) => element.SetValue(PlacementProperty, value);

    /// <summary>取与触发器的距离。</summary>
    public static double GetOffset(DependencyObject element) => (double)element.GetValue(OffsetProperty);

    /// <summary>设与触发器的距离。</summary>
    public static void SetOffset(DependencyObject element, double value) => element.SetValue(OffsetProperty, value);

    /// <summary>取是否显示箭头。</summary>
    public static bool GetArrow(DependencyObject element) => (bool)element.GetValue(ArrowProperty);

    /// <summary>设是否显示箭头。</summary>
    public static void SetArrow(DependencyObject element, bool value) => element.SetValue(ArrowProperty, value);

    /// <summary>装上默认值与行为（RuneTheme 创建时调用；只执行一次）。</summary>
    internal static void Install()
    {
    }

    /// <summary>
    /// 候选位置（触发器左上角为原点，设备像素），按先后试：要的那一边，放不下时翻到对面；平台选第一个整个在屏幕里的。
    /// 提示四周留着箭头伸出的透明边（<see cref="TooltipChrome.Overhang"/>），面板与触发器隔开 <see cref="OffsetProperty"/> 加箭头伸出的一段。
    /// </summary>
    static CustomPopupPlacement[] Place(FrameworkElement target, Size popup, Size anchor)
    {
        var scale = target.ActualWidth > 0 ? anchor.Width / target.ActualWidth : 1;
        var arrow = GetArrow(target) ? TooltipChrome.ArrowSize(target) : 0;
        var margin = TooltipChrome.Overhang(arrow) * scale;
        var gap = (GetOffset(target) + 0.7 * arrow) * scale;
        var placement = GetPlacement(target);
        var startIsLeft = target.FlowDirection == FlowDirection.LeftToRight;
        double Cross(string? align, double popupLength, double anchorLength, bool horizontal) => (align, horizontal && !startIsLeft) switch
        {
            ("start", false) or ("end", true) => -margin,
            ("end", false) or ("start", true) => anchorLength - popupLength + margin,
            _ => (anchorLength - popupLength) / 2,
        };
        CustomPopupPlacement At(string side) => side switch
        {
            "bottom" => new(new Point(Cross(placement.Align, popup.Width, anchor.Width, true), anchor.Height + gap - margin), PopupPrimaryAxis.Horizontal),
            "left" => new(new Point(-gap - popup.Width + margin, Cross(placement.Align, popup.Height, anchor.Height, false)), PopupPrimaryAxis.Vertical),
            "right" => new(new Point(anchor.Width + gap - margin, Cross(placement.Align, popup.Height, anchor.Height, false)), PopupPrimaryAxis.Vertical),
            _ => new(new Point(Cross(placement.Align, popup.Width, anchor.Width, true), -gap - popup.Height + margin), PopupPrimaryAxis.Horizontal),
        };
        var opposite = placement.Side switch { "bottom" => "top", "left" => "right", "right" => "left", _ => "bottom" };
        return [At(placement.Side), At(opposite)];
    }

    static void OnOpened(ToolTip tip)
    {
        shown = new WeakReference<ToolTip>(tip);
        FadeIn(tip);
        // 弹出层定好位置之后才知道最终在哪一边（可能翻转了）
        tip.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => PlaceArrow(tip));
    }

    static void OnClosed(ToolTip tip)
    {
        if (shown is not null && shown.TryGetTarget(out var current) && current == tip) shown = null;
    }

    /// <summary>淡入：durations.md、enter 缓动；系统关闭动画时直接显示。</summary>
    static void FadeIn(ToolTip tip)
    {
        var duration = Motion.Duration(tip, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200));
        tip.BeginAnimation(UIElement.OpacityProperty, duration <= TimeSpan.Zero ? null
            : new DoubleAnimation(0, 1, duration) { EasingFunction = tip.TryFindResource("Rune.Easing.Enter") as IEasingFunction, FillBehavior = FillBehavior.Stop });
    }

    /// <summary>箭头贴着朝向触发器的那条边：边按提示与触发器在屏幕上的实际位置判断（空间不够时翻到了另一边）。</summary>
    static void PlaceArrow(ToolTip tip)
    {
        if (!tip.IsOpen || tip.PlacementTarget is not FrameworkElement target || tip.Template?.FindName("PART_Chrome", tip) is not TooltipChrome chrome) return;
        if (PresentationSource.FromVisual(chrome) is null || PresentationSource.FromVisual(target) is null) return;
        var panel = chrome.PanelOnScreen();
        var anchor = new Rect(Screen(target, new Point()), Screen(target, new Point(target.ActualWidth, target.ActualHeight)));
        var side = panel.Bottom <= anchor.Top + 0.5 ? "top"
            : panel.Top >= anchor.Bottom - 0.5 ? "bottom"
            : panel.Right <= anchor.Left + 0.5 ? "left"
            : "right";
        var center = side is "top" or "bottom" ? anchor.Left + anchor.Width / 2 - panel.Left : anchor.Top + anchor.Height / 2 - panel.Top;
        chrome.PlaceArrow(side, center);
    }

    /// <summary>元素里的一点在屏幕上的位置（与设备无关的像素）。</summary>
    internal static Point Screen(Visual visual, Point point)
    {
        var device = visual.PointToScreen(point);
        var fromDevice = PresentationSource.FromVisual(visual)!.CompositionTarget!.TransformFromDevice;
        return fromDevice.Transform(device);
    }

    /// <summary>Esc 隐藏正显示的提示；提示在对话框里时不让同一次 Esc 关掉对话框。</summary>
    static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || shown is null || !shown.TryGetTarget(out var tip) || !tip.IsOpen) return;
        tip.IsOpen = false;
        e.Handled = true;
    }

    /// <summary>触发器 → 定位回调（提示的样式里绑定 PlacementTarget 用）。</summary>
    sealed class PlacementCallbackConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is FrameworkElement target ? new CustomPopupPlacementCallback((popup, anchor, _) => Place(target, popup, anchor)) : null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}

/// <summary>提示的部件样式键（TS 的 propsArrow）：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:TooltipParts.Arrow}" TargetType="Border"&gt;</c>。</summary>
public static class TooltipParts
{
    /// <summary>提示的面板（反色底、圆角）。</summary>
    public static readonly PartKey Panel = new(typeof(Tooltip), nameof(Panel), typeof(Border));

    /// <summary>箭头（旋转 45° 的小方块，尖角朝向触发器）。</summary>
    public static readonly PartKey Arrow = new(typeof(Tooltip), nameof(Arrow), typeof(Border));
}
