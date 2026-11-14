using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>
/// 输入框下面的选项弹出层（vue-tsx-pro 的 ListPopover）：TagsInput、Combobox 共用。贴着外壳下方、起始边对齐（放不下时翻到上方）、至少与外壳同宽、隔 6，
/// 点外面关上；面板与阴影层的样式取使用方给的部件样式键，里面是不可聚焦的 <see cref="Listbox"/>（焦点一直在输入框里）。
/// 弹出层要挂进使用方的逻辑树（<see cref="Popup"/>，使用方调 AddLogicalChild 并在 LogicalChildren 里报告它）。
/// </summary>
sealed class ListPopover
{
    /// <summary>与外壳的距离（像素）。</summary>
    const double Offset = 6;

    /// <summary>弹出层四周给阴影留的透明边（像素）。</summary>
    const double Overhang = 16;

    readonly FrameworkElement owner;
    readonly Border shadow = new();

    /// <summary>建好弹出层；<paramref name="closedOutside"/> 是点了外面关上时调用的。</summary>
    public ListPopover(FrameworkElement owner, PartKey panelStyle, PartKey shadowStyle, Action closedOutside)
    {
        this.owner = owner;
        shadow.SetResourceReference(FrameworkElement.StyleProperty, shadowStyle);
        Panel.SetResourceReference(FrameworkElement.StyleProperty, panelStyle);
        Panel.Child = Listbox;
        var layer = new LayoutGrid { Margin = new Thickness(Overhang) };
        layer.Children.Add(shadow);
        layer.Children.Add(Panel);
        Popup.Child = layer;
        Popup.PlacementTarget = owner;
        Popup.CustomPopupPlacementCallback = Place;
        Popup.Closed += (_, _) => closedOutside();
    }

    /// <summary>弹出层（使用方挂进逻辑树）。</summary>
    public Popup Popup { get; } = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Custom };

    /// <summary>面板。</summary>
    public Border Panel { get; } = new() { ClipToBounds = true };

    /// <summary>列表。</summary>
    public Listbox Listbox { get; } = new() { Focusable = false };

    /// <summary>显示或藏起；显示时面板至少与外壳同宽。</summary>
    public void Show(bool shown)
    {
        if (shown) Panel.MinWidth = owner.ActualWidth;
        if (Popup.IsOpen != shown) Popup.IsOpen = shown;
    }

    /// <summary>按外壳的字号：面板 lg 圆角；弹出层不在外壳里，字号同外壳。</summary>
    public void UpdateMetrics(double em)
    {
        Panel.CornerRadius = shadow.CornerRadius = new CornerRadius(owner.Resource<double>(ThemeKeys.Radius.Lg) * em);
        TextElement.SetFontSize(Panel, em);
    }

    /// <summary>平台的位置：外壳下方、起始边对齐，放不下时翻到上方；弹出层四周有透明边（阴影），与外壳隔 6。尺寸是设备像素。</summary>
    CustomPopupPlacement[] Place(Size popupSize, Size targetSize, Point offset)
    {
        var scale = owner.ActualWidth > 0 ? targetSize.Width / owner.ActualWidth : 1;
        var margin = Overhang * scale;
        var gap = Offset * scale;
        var x = owner.FlowDirection == FlowDirection.LeftToRight ? -margin : targetSize.Width - popupSize.Width + margin;
        return
        [
            new(new Point(x, targetSize.Height + gap - margin), PopupPrimaryAxis.Horizontal),
            new(new Point(x, -gap - popupSize.Height + margin), PopupPrimaryAxis.Horizontal),
        ];
    }
}
