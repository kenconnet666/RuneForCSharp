using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 工具栏（vue-tsx-pro 的 Toolbar，WAI-ARIA 的 Toolbar 模式）：一组相关的按钮、链接。整组只占一个 Tab 位置，组内用方向键移动（漫游焦点），
/// Home / End 到两端；不可用、隐藏的项跳过，Tab 再进来时落在上次聚焦的项。名称写 AccessibleName。
/// <code>
/// var format = new ToolbarControl { AccessibleName = "文字格式" };
/// format.Controls.AddRange([new IconButtonControl { Icon = Lucide.Bold, AccessibleName = "加粗" }, new IconButtonControl { Icon = Lucide.Italic, AccessibleName = "斜体" }]);
/// </code>
/// 是一个 FlexControl：横排、放不下换行、交叉轴居中，项之间 space.xs；竖排时拉伸。里面的输入框不参与方向键移动，用 Tab 进出。
/// 和 WinForms 自带的 ToolStrip 不是一回事。
/// </summary>
public class ToolbarControl : FlexControl
{
    Control? current;

    /// <summary>新建：横排、换行、居中，读屏里是工具栏。</summary>
    public ToolbarControl()
    {
        AccessibleRole = AccessibleRole.ToolBar;
        ApplyOrientation();
    }

    /// <summary>方向；不写时 horizontal（左右键移动），vertical 时上下键移动。</summary>
    [Category("Behavior")]
    [Description("方向：horizontal（默认，左右键移动）、vertical（上下键移动）。")]
    public ToolbarOrientation Orientation { get => field.Value is null ? ToolbarOrientation.Horizontal : field; set { field = value; ApplyOrientation(); } }

    /// <summary>到两端后是否从另一端继续，默认 true。</summary>
    [Category("Behavior")]
    [Description("方向键到两端后是否从另一端继续。")]
    [DefaultValue(true)]
    public bool Loop { get; set; } = true;

    bool IsVertical => Orientation.Value == "vertical";

    bool ShouldSerializeOrientation() => Orientation != ToolbarOrientation.Horizontal;

    void ResetOrientation() => Orientation = default;

    /// <summary>横排：换行、居中；竖排：一列、拉伸（vue-tsx-pro 的 toolbarStyles）。</summary>
    void ApplyOrientation()
    {
        Direction = IsVertical ? FlexDirection.Column : default;
        Align = IsVertical ? FlexAlign.Stretch : FlexAlign.Center;
        Wrap = !IsVertical;
        if (Gap.Value is null) Gap = Space.Xs;
    }

    /// <summary>项：按钮、链接（可见、可用；里面另一个工具栏的项不算）。</summary>
    List<Control> Items() => [.. Descendants(this).Where(control => control is IButtonControl or LinkControl && control.Visible && control.Enabled)];

    static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            if (child is ToolbarControl) continue;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    /// <summary>定下哪一项是 Tab 停靠点，其余不停。</summary>
    void Sync()
    {
        var items = Items();
        var stop = current is not null && items.Contains(current) ? current : items.FirstOrDefault();
        foreach (var item in Descendants(this).Where(control => control is IButtonControl or LinkControl)) item.TabStop = item == stop;
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        e.Control!.GotFocus += OnItemFocused;
    }

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        e.Control!.GotFocus -= OnItemFocused;
    }

    void OnItemFocused(object? sender, EventArgs e)
    {
        current = sender as Control;
        Sync();
    }

    /// <summary>方向键、Home、End 在项之间移动焦点（项本身不处理这些键，会交到这里）。</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        var items = Items();
        var index = items.FindIndex(item => item.Focused);
        if (index < 0) return base.ProcessDialogKey(keyData);
        var rtl = RightToLeft == RightToLeft.Yes;
        int? target = keyData switch
        {
            Keys.Right when !IsVertical => rtl ? index - 1 : index + 1,
            Keys.Left when !IsVertical => rtl ? index + 1 : index - 1,
            Keys.Down when IsVertical => index + 1,
            Keys.Up when IsVertical => index - 1,
            Keys.Home => 0,
            Keys.End => items.Count - 1,
            // 另一个方向的方向键不动（WinForms 默认会用方向键在控件之间移动，TS 里浏览器什么都不做）
            Keys.Left or Keys.Right or Keys.Up or Keys.Down => index,
            _ => null,
        };
        if (target is not { } next) return base.ProcessDialogKey(keyData);
        if (next == index) return true;
        if (next < 0 || next >= items.Count)
        {
            if (!Loop) return true;
            next = (next + items.Count) % items.Count;
        }
        current = items[next];
        Sync();
        current.Focus();
        return true;
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
