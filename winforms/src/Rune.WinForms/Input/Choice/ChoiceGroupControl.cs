using System.Collections;
using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 复选组、单选组共用的容器（vue-tsx-pro 的 choiceRecipe 的 group）：一个 <see cref="FlexControl"/>，竖排（gap xs）或横排换行（列距 lg、行距 xs）；
/// 整组只读、出错，项可以直接放在里面（也可以套在别的布局容器里），也可以来自 <see cref="ItemsSource"/>（ListboxOption、字符串，
/// 或任意数据加 <see cref="ToOption"/>）。整组不可用写 Enabled。项沿 Parent 链找到最近的组。
/// 卡片外观（<see cref="Card"/>）时每项是一张卡片：竖排一列（gap sm），横排按 <see cref="MinItemWidth"/>（默认 14em）auto-fill 分列（列距 lg、行距 xs），
/// 列数与列宽同 GridControl（孪生的 GridLayout），每项定成列宽。
/// </summary>
public abstract class ChoiceGroupControl : FlexControl, IFieldControl
{
    readonly List<Control> generated = [];

    /// <summary>新建：竖排。</summary>
    protected ChoiceGroupControl() => ApplyOrientation();

    /// <summary>排列方向；默认竖排。</summary>
    [Category("Layout")]
    [Description("排列方向：Vertical（默认，gap xs）、Horizontal（换行，列距 lg、行距 xs）。")]
    [DefaultValue(Orientation.Vertical)]
    public Orientation Orientation { get; set { field = value; ApplyOrientation(); } } = Orientation.Vertical;

    /// <summary>整组只读：能聚焦、读屏念"只读"，选择不能改（查看详情、审批时）；外观照常。</summary>
    [Category("Behavior")]
    [Description("整组只读：能聚焦，选择不能改；外观照常。")]
    [DefaultValue(false)]
    public bool IsReadOnly { get => readOnly ?? false; set { readOnly = value; Sync(); } }

    /// <summary>整组校验未通过（如"至少选一项"）。</summary>
    [Category("Appearance")]
    [Description("整组校验未通过：各项的描边是危险色。")]
    [DefaultValue(false)]
    public bool IsInvalid { get => invalid ?? false; set { invalid = value; Sync(); } }

    /// <summary>卡片外观：每项是一张带边框的卡片，勾选时边框与底色换成色调色；组里的项都跟着（项自己写了 Card 的除外）。</summary>
    [Category("Appearance")]
    [Description("卡片外观：每项是一张带边框的卡片；组里的项都跟着。")]
    [DefaultValue(false)]
    public bool Card { get; set { field = value; ApplyOrientation(); Sync(); } }

    /// <summary>卡片外观横排时每张的最小宽度，放得下几张就几列（auto-fill）；默认 14em。</summary>
    [Category("Layout")]
    [Description("卡片外观横排时每张的最小宽度（14em、200）。")]
    public Length MinItemWidth { get => field.Value is null ? DefaultMinItemWidth : field; set { field = value; PerformLayout(); } }

    static readonly Length DefaultMinItemWidth = "14em";

    bool ShouldSerializeMinItemWidth() => MinItemWidth != DefaultMinItemWidth;

    bool? readOnly, invalid;

    /// <summary>项的宽度是组按列宽定的（换回非卡片、竖排时还原）。</summary>
    bool columnsApplied;

    /// <summary>整组只读：自己写了的优先，其次所在字段的。</summary>
    internal bool ReadOnly => readOnly ?? FieldControl.Of(this)?.IsReadOnly ?? false;

    /// <summary>整组出错：自己写了的优先，其次所在字段的。</summary>
    internal bool Invalid => invalid ?? FieldControl.Of(this)?.Invalid ?? false;

    void IFieldControl.FieldChanged() => Sync();

    /// <summary>数据 → 选项（值、文字、说明、不可用）；数据是 ListboxOption、字符串时不用写。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, ListboxOption>? ToOption { get; set { field = value; Generate(); } }

    /// <summary>按数据生成的项（放在直接写的项后面）；null 时没有。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable? ItemsSource { get; set { field = value; Generate(); } }

    /// <summary>
    /// 竖排 gap xs（卡片 sm），项拉满宽度（同上游的 flex 列：结束边的文字贴得到结束边）；横排换行、列距 lg、行距 xs（上游排列方向的长写盖过卡片的 gap: sm）。
    /// </summary>
    void ApplyOrientation()
    {
        var horizontal = Orientation == Orientation.Horizontal;
        Direction = horizontal ? FlexDirection.Row : FlexDirection.Column;
        Wrap = horizontal ? FlexWrap.Wrap : FlexWrap.NoWrap;
        Gap = Card && !horizontal ? Space.Sm : Space.Xs;
        ColumnGap = horizontal ? Space.Lg : default;
        Align = horizontal ? default : FlexAlign.Stretch;
        if (!columnsApplied || (Card && horizontal)) return;
        columnsApplied = false;
        foreach (Control child in Controls) child.MinimumSize = child.MaximumSize = Size.Empty;
    }

    /// <summary>卡片外观横排：按最小宽度算列数（auto-fill，同 Grid），每项定成列宽，换行的 Flex 就排成栅格。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        if (Card && Orientation == Orientation.Horizontal && Controls.Count > 0)
        {
            var em = Font.SizeInPoints * DeviceDpi / 72f;
            var width = ClientSize.Width - Padding.Horizontal;
            var gap = Tokens.Space.Lg * em;
            var columns = GridLayout.Columns(width, gap, null, MinItemWidth.Pixels(em), Controls.Count, false);
            var column = (int)Math.Floor((width - (columns - 1) * gap) / columns);
            var size = new Size(Math.Max(0, column), 0);
            columnsApplied = true;
            foreach (Control child in Controls)
                if (child.MinimumSize != size)
                    child.MinimumSize = child.MaximumSize = size;
        }
        base.OnLayout(levent);
    }

    /// <summary>按 <see cref="ItemsSource"/> 重新生成项：值、文字、说明、不可用取自选项。</summary>
    void Generate()
    {
        foreach (var control in generated)
        {
            Controls.Remove(control);
            control.Dispose();
        }
        generated.Clear();
        if (ItemsSource is null) return;
        foreach (var item in ItemsSource)
        {
            var option = ListboxOption.From(item, ToOption);
            var control = CreateItem(option);
            control.Text = option.Label;
            control.Enabled = !option.IsDisabled;
            generated.Add(control);
            Controls.Add(control);
        }
    }

    /// <summary>派生类按选项新建一项（值、说明）。</summary>
    /// <param name="option">选项。</param>
    private protected abstract Control CreateItem(ListboxOption option);

    /// <summary>组的值、只读、出错变了，或加了项：让里面的项跟上。</summary>
    internal abstract void Sync();

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        Sync();
    }

    /// <summary>属于这一组的项（嵌套的组里的不算），按控件的先后。</summary>
    /// <typeparam name="T">项的控件类型。</typeparam>
    private protected List<T> Choices<T>() where T : Control
    {
        var found = new List<T>();
        Walk(this);
        return found;

        void Walk(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is T item) found.Add(item);
                else if (child is not ChoiceGroupControl) Walk(child);
            }
        }
    }

    /// <summary>控件所在的最近一个组。</summary>
    /// <typeparam name="T">组的类型。</typeparam>
    internal static T? Of<T>(Control control) where T : ChoiceGroupControl
    {
        for (var node = control.Parent; node is not null; node = node.Parent)
            if (node is ChoiceGroupControl group) return group as T;
        return null;
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ChoiceGroupAccessibleObject(this);

    /// <summary>读屏：一组（role="group"），名称写 AccessibleName，没写时是所在字段的标签；帮助文字是字段的说明与错误。</summary>
    sealed class ChoiceGroupAccessibleObject(ChoiceGroupControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => Owner is { AccessibleRole: not AccessibleRole.Default } control ? control.AccessibleRole : AccessibleRole.Grouping;

        public override string? Name
        {
            get => base.Name is { Length: > 0 } name ? name : FieldControl.Of(owner) is { } scope && scope.Owns(owner) ? scope.Label : null;
            set => base.Name = value;
        }

        public override string? Help => base.Help ?? FieldControl.Of(owner)?.HelpText;
    }
}
