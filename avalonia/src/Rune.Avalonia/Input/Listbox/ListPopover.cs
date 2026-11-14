using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.LogicalTree;

namespace Rune.Avalonia;

/// <summary>
/// 输入框下面的选项弹出层（vue-tsx-pro 的 ListPopover）：TagsInput、Combobox 共用。贴着外壳下方、起始边对齐、至少与外壳同宽、隔 6，
/// 点外面关上；面板是 <c>Border#PART_Popover</c>（外观在 SelectParts.axaml），里面是不可聚焦的 <see cref="Listbox"/>（焦点一直在输入框里）。
/// </summary>
sealed class ListPopover
{
    /// <summary>与外壳的距离（像素）。</summary>
    const double Offset = 6;

    readonly Control owner;
    readonly Popup popup = new() { IsLightDismissEnabled = true, WindowManagerAddShadowHint = false, Placement = PlacementMode.BottomEdgeAlignedLeft };

    /// <summary>建好弹出层，挂在 <paramref name="owner"/> 的逻辑树下（沿用它的样式、主题）；<paramref name="closedOutside"/> 是点了外面关上时调用的。</summary>
    public ListPopover(Control owner, IList<ILogical> logicalChildren, Action closedOutside)
    {
        this.owner = owner;
        Panel.Child = Listbox;
        popup.Child = new global::Avalonia.Controls.Panel { Margin = new Thickness(0, Offset), Children = { Panel } };
        popup.PlacementTarget = owner;
        logicalChildren.Add(popup);
        popup.Closed += (_, _) => closedOutside();
    }

    /// <summary>面板（部件 PART_Popover）。</summary>
    public Border Panel { get; } = new() { Name = "PART_Popover", ClipToBounds = true };

    /// <summary>列表（部件 PART_Listbox）。</summary>
    public Listbox Listbox { get; } = new() { Name = "PART_Listbox", Focusable = false };

    /// <summary>显示着。</summary>
    public bool IsOpen => popup.IsOpen;

    /// <summary>显示或藏起；显示时面板至少与外壳同宽。</summary>
    public void Show(bool shown)
    {
        if (shown) Panel.MinWidth = owner.Bounds.Width;
        if (popup.IsOpen != shown) popup.IsOpen = shown;
    }

    /// <summary>按外壳的字号：面板 lg 圆角；弹出层不在外壳里，字号同外壳。</summary>
    public void UpdateMetrics(double em)
    {
        Panel.CornerRadius = new CornerRadius(owner.Resource<double>(ThemeKeys.Radius.Lg) * em);
        Panel.SetValue(TextElement.FontSizeProperty, em);
    }
}
