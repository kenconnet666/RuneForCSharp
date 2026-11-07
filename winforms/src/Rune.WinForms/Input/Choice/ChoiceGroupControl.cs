using System.Collections;
using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 复选组、单选组共用的容器（vue-tsx-pro 的 choiceRecipe 的 group）：一个 <see cref="FlexControl"/>，竖排（gap xs）或横排换行（列距 lg、行距 xs）；
/// 整组只读、出错，项可以直接放在里面（也可以套在别的布局容器里），也可以来自 <see cref="ItemsSource"/>（ListboxOption、字符串，
/// 或任意数据加 <see cref="ToOption"/>）。整组不可用写 Enabled。项沿 Parent 链找到最近的组。
/// </summary>
public abstract class ChoiceGroupControl : FlexControl
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
    public bool IsReadOnly { get; set { field = value; Sync(); } }

    /// <summary>整组校验未通过（如"至少选一项"）。</summary>
    [Category("Appearance")]
    [Description("整组校验未通过：各项的描边是危险色。")]
    [DefaultValue(false)]
    public bool IsInvalid { get; set { field = value; Sync(); } }

    /// <summary>数据 → 选项（值、文字、说明、不可用）；数据是 ListboxOption、字符串时不用写。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, ListboxOption>? ToOption { get; set { field = value; Generate(); } }

    /// <summary>按数据生成的项（放在直接写的项后面）；null 时没有。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable? ItemsSource { get; set { field = value; Generate(); } }

    void ApplyOrientation()
    {
        var horizontal = Orientation == Orientation.Horizontal;
        Direction = horizontal ? FlexDirection.Row : FlexDirection.Column;
        Wrap = horizontal ? FlexWrap.Wrap : FlexWrap.NoWrap;
        Gap = Space.Xs;
        ColumnGap = horizontal ? Space.Lg : default;
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

    /// <summary>读屏：一组（role="group"），名称写 AccessibleName。</summary>
    sealed class ChoiceGroupAccessibleObject(ChoiceGroupControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => Owner is { AccessibleRole: not AccessibleRole.Default } control ? control.AccessibleRole : AccessibleRole.Grouping;
    }
}
