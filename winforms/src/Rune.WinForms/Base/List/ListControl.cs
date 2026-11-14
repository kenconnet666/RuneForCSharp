using System.ComponentModel;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 正文列表的共同部分（vue-tsx-pro 的 listStylesProse）：子控件按 Controls 的顺序竖着排、撑满宽度，项之间 <see cref="Gap"/>；
/// 字号档位 <see cref="TextSize"/> 改列表的字体，项继承（Font 是环境属性）。列表给每个 <see cref="ListItemControl"/> 设记号。
/// </summary>
public abstract class ListBaseControl : Panel
{
    readonly VerticalStack layout;

    private protected ListBaseControl()
    {
        layout = new VerticalStack(() => (int)MathF.Round((Gap.Factor ?? 0) * this.EmPixels()));
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AccessibleRole = AccessibleRole.List;
    }

    /// <summary>字号档位；不写时继承外面的字体。</summary>
    [Category("Appearance")]
    [Description("字号档位（xs–xl 或像素数）；不写时继承外面的字体。")]
    public TextSize TextSize { get; set { field = value; ApplyTextSize(); } }

    /// <summary>项与项之间的间距档位，默认 xs；每项是一段话时用 md。</summary>
    [Category("Layout")]
    [Description("项与项之间的间距档位，默认 xs；每项是一段话时用 md。")]
    public Space Gap { get => field.Value is null ? Space.Xs : field; set { field = value; PerformLayout(); } }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => layout;

    /// <summary>子控件里的列表项（HTML 里 ol 只给 li 编号）。</summary>
    private protected IEnumerable<ListItemControl> Items => Controls.OfType<ListItemControl>();

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    void ResetTextSize() => TextSize = default;

    bool ShouldSerializeGap() => Gap != Space.Xs;

    void ResetGap() => Gap = default;

    /// <summary>给每一项设记号。</summary>
    private protected abstract void UpdateMarkers();

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => layout.Measure(this, proposedSize);

    void ApplyTextSize()
    {
        if (TextSize.Pixels is not { } pixels)
        {
            ResetFont();
            return;
        }
        var outer = Parent?.Font ?? Font;
        Font = Fonts.Get(outer.FontFamily.Name, pixels * 72 / 96, outer.Style);
    }

    /// <inheritdoc />
    protected override void OnParentFontChanged(EventArgs e)
    {
        base.OnParentFontChanged(e);
        if (TextSize.Pixels is not null) ApplyTextSize();
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        UpdateMarkers();
    }

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        UpdateMarkers();
    }
}

/// <summary>
/// 无序列表（vue-tsx-pro 的 List）：圆点，或 <see cref="Icon"/> 代替圆点（功能清单的对勾），<see cref="ListItemControl.Icon"/> 逐项覆盖。
/// <code>
/// var plans = new UnorderedListControl { Icon = Lucide.Check, PropsIcon = new() { Tone = TextTone.Success } };
/// plans.Controls.Add(new ListItemControl { Text = "不限项目" });
/// plans.Controls.Add(new ListItemControl { Text = "单点登录", Icon = Lucide.X, PropsIcon = new() { Tone = TextTone.Muted } });
/// </code>
/// 叫 UnorderedListControl：WinForms 自己有 ListControl（ListBox、ComboBox 的基类）。界面结构上的列表（导航、分页、步骤）不要圆点，用布局组件。
/// </summary>
public class UnorderedListControl : ListBaseControl
{
    /// <summary>用图标代替圆点；ListItemControl 的 Icon 逐项覆盖。</summary>
    [Category("Appearance")]
    [Description("用图标代替圆点（功能清单的对勾）；各项的 Icon 逐项覆盖。")]
    [DefaultValue(null)]
    public IconData? Icon { get; set { field = value; UpdateMarkers(); } }

    /// <summary>各项图标的部件属性（Tone 改颜色）；ListItemControl 的 PropsIcon 合并在上面。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsIcon { get; set { field = value; UpdateMarkers(); } }

    private protected override void UpdateMarkers()
    {
        foreach (var item in Items) item.SetMarker("•", Icon, PropsIcon);
    }
}

/// <summary>有序列表（vue-tsx-pro 的 OrderedList）：编号从 <see cref="Start"/> 起；里面的项不沿用外层列表的图标。</summary>
public class OrderedListControl : ListBaseControl
{
    /// <summary>第一项的编号，默认 1（HTML 的 start）。</summary>
    [Category("Appearance")]
    [Description("第一项的编号。")]
    [DefaultValue(1)]
    public int Start { get; set { field = value; UpdateMarkers(); } } = 1;

    private protected override void UpdateMarkers()
    {
        var number = Start;
        foreach (var item in Items) item.SetMarker($"{number++}.", null, null);
    }
}
