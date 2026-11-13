using System.ComponentModel;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 表单字段（vue-tsx-pro 的 Field）：标签 + 控件 + 说明 + 错误信息，并把字段的状态交给里面的控件。
/// <code>
/// var field = new FieldControl { Label = "通知方式", IsRequired = true, Content = channels };
/// channels.ValueChanged += (_, _) => field.Error = channels.Value.Count > 0 ? null : "至少选一种";
/// </code>
/// 里面的控件（勾选类、复选组、单选组，以后的输入框）取字段的出错、必填、只读（控件上自己写了的优先），读屏的帮助文字是说明与错误；
/// 第一个控件认领标签：没有自己的名称时以标签为名，点标签聚焦它。有错误（且 IsInvalid 不是 false）时显示错误、控件标红；只写 IsInvalid 时只标红。
/// 不可用写 Enabled（里面一起不可用，标签变淡）。
/// 标签默认在上方，竖排间距 space.xs；<see cref="LabelPlacement"/> 为 start 时标签一列（<see cref="LabelWidth"/>，默认 8em；
/// 文字默认贴着控件右对齐）、控件一列，列距 space.md，标签与控件第一行文字的基线对齐。<see cref="IsLabelHidden"/> 时标签只给读屏、不占位置。
/// 说明与错误是 sm 字号（muted；错误是 dangerText，前面有警示图标）。说明、错误是文字（上游可以是任意内容）。表单（Form）、字段布局的统一设置以后做。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Label))]
[Description("表单字段：标签、控件、说明、错误信息，并把出错、必填、只读交给里面的控件。")]
public class FieldControl : Panel
{
    readonly FieldLabelControl label = new();
    readonly TextControl description = new() { TextSize = TextSize.Sm, Tone = TextTone.Muted };
    readonly FlexControl error = new() { Gap = Space.Xs, Align = FlexAlign.Start };
    readonly IconControl errorIcon = new() { Data = Lucide.CircleExclamationPoint, IconSize = TextSize.Sm, Tone = TextTone.Danger };
    readonly TextControl errorText = new() { TextSize = TextSize.Sm, Tone = TextTone.Danger };
    readonly FieldEngine engine;

    /// <summary>新建：标签在上方。</summary>
    public FieldControl()
    {
        engine = new FieldEngine(this);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        error.Controls.AddRange([errorIcon, errorText]);
        Controls.AddRange([label, description, error]);
        Sync();
    }

    /// <summary>标签。</summary>
    [Category("Appearance")]
    [Description("标签。")]
    [DefaultValue(null)]
    public string? Label { get; set { field = value; Sync(); } }

    /// <summary>控件下方的说明。</summary>
    [Category("Appearance")]
    [Description("控件下方的说明（读屏的帮助文字）。")]
    [DefaultValue(null)]
    public string? Description { get; set { field = value; Sync(); } }

    /// <summary>错误信息：给了（且 IsInvalid 不是 false）就显示，并把控件标为出错。</summary>
    [Category("Appearance")]
    [Description("错误信息：给了就显示，并把控件标为出错。")]
    [DefaultValue(null)]
    public string? Error { get; set { field = value; Sync(); } }

    /// <summary>校验未通过；不写时有错误就算出错，写 false 时不显示错误。</summary>
    [Category("Appearance")]
    [Description("校验未通过；空着时有错误就算出错，写 false 时不显示错误。")]
    [DefaultValue(null)]
    public bool? IsInvalid { get; set { field = value; Sync(); } }

    /// <summary>必填：标签后加星号。</summary>
    [Category("Behavior")]
    [Description("必填：标签后加星号。")]
    [DefaultValue(false)]
    public bool IsRequired { get; set { field = value; Sync(); } }

    /// <summary>只读：交给里面的控件。</summary>
    [Category("Behavior")]
    [Description("只读：交给里面的控件。")]
    [DefaultValue(false)]
    public bool IsReadOnly { get; set { field = value; Sync(); } }

    /// <summary>标签放在哪；默认上方。</summary>
    [Category("Layout")]
    [Description("标签放在哪：top（默认）、start。")]
    public FieldLabelPlacement LabelPlacement { get; set { field = value; Sync(); } }

    /// <summary>标签在起始边时标签列的宽度（设备无关像素）；null 时 8em。</summary>
    [Category("Layout")]
    [Description("标签在起始边时标签列的宽度（设备无关像素）；空着时 8em。")]
    [DefaultValue(null)]
    public int? LabelWidth { get; set { field = value; PerformLayout(); } }

    /// <summary>标签在起始边时文字的对齐；默认 end（贴着控件）。</summary>
    [Category("Layout")]
    [Description("标签在起始边时文字的对齐：start、end（默认）。")]
    public FieldLabelAlign LabelAlign { get; set { field = value; PerformLayout(); } }

    /// <summary>标签只给读屏：视觉上隐藏，仍是控件的名称；不占标签列。</summary>
    [Category("Appearance")]
    [Description("标签只给读屏：视觉上隐藏，仍是控件的名称。")]
    [DefaultValue(false)]
    public bool IsLabelHidden { get; set { field = value; Sync(); } }

    /// <summary>说明部件的属性（代码里设，TS 的 propsDescription）：默认 sm、muted。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsDescription { get; set { field = value; new TextProps { Size = TextSize.Sm, Tone = TextTone.Muted }.Merge(value).ApplyTo(description.Element); } }

    /// <summary>错误文字部件的属性（代码里设，TS 的 propsError）：默认 sm、danger。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsError { get; set { field = value; new TextProps { Size = TextSize.Sm, Tone = TextTone.Danger }.Merge(value).ApplyTo(errorText.Element); } }

    /// <summary>控件（放进字段里的那一个；也可以直接加进 Controls）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content
    {
        get => Controls.Cast<Control>().FirstOrDefault(control => control != label && control != description && control != error);
        set
        {
            if (Content is { } old) Controls.Remove(old);
            if (value is not null) Controls.Add(value);
        }
    }

    bool ShouldSerializeLabelPlacement() => LabelPlacement.Value is not null;

    void ResetLabelPlacement() => LabelPlacement = default;

    bool ShouldSerializeLabelAlign() => LabelAlign.Value is not null;

    void ResetLabelAlign() => LabelAlign = default;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    /// <summary>出错：写了 IsInvalid 按它，否则有错误就算。</summary>
    internal bool Invalid => IsInvalid ?? HasText(Error);

    bool ShowsError => HasText(Error) && IsInvalid != false;

    bool Start => LabelPlacement == FieldLabelPlacement.Start && !IsLabelHidden;

    /// <summary>读屏的帮助文字：说明与（显示着的）错误。</summary>
    internal string? HelpText => string.Join(" ", new[] { Description, ShowsError ? Error : null }.Where(HasText)) is { Length: > 0 } text ? text : null;

    /// <summary>标签、说明、错误部件（测试用）。</summary>
    internal (FieldLabelControl Label, TextControl Description, FlexControl Error) Parts => (label, description, error);

    /// <summary>里面接入字段的控件（深度优先、不进到控件里面）：第一个认领标签。</summary>
    List<IFieldControl> FieldControls()
    {
        var found = new List<IFieldControl>();
        if (Content is { } content) Walk(content);
        return found;

        void Walk(Control node)
        {
            if (node is IFieldControl control)
            {
                found.Add(control);
                return;
            }
            if (node is FieldControl) return;
            foreach (Control child in node.Controls) Walk(child);
        }
    }

    /// <summary>控件是不是认领了标签的那个（第一个接入的）。</summary>
    internal bool Owns(IFieldControl control) => FieldControls().FirstOrDefault() == control;

    /// <summary>
    /// 控件读屏的帮助文字（上游的 aria-describedby，各层拼在一起、重复的只留一份）：所在字段的说明与错误（认领标签的控件才有），
    /// 再接控件自己的（勾选项的说明、平台给的 Help）。不在字段里时只有自己的。
    /// </summary>
    /// <param name="control">接入字段的控件。</param>
    /// <param name="own">控件自己的帮助文字。</param>
    internal static string? DescribedBy(Control control, params string?[] own)
    {
        var field = control is IFieldControl fieldControl && Of(control) is { } owner && owner.Owns(fieldControl) ? owner.HelpText : null;
        var parts = own.Prepend(field).Where(HasText).Distinct().ToArray();
        return parts.Length > 0 ? string.Join(" ", parts) : null;
    }

    /// <summary>控件所在的字段：沿 Parent 链往外找；不在字段里时为 null。</summary>
    internal static FieldControl? Of(Control control)
    {
        for (var node = control.Parent; node is not null; node = node.Parent)
            if (node is FieldControl field) return field;
        return null;
    }

    /// <summary>各部分的内容与显示，再让里面的控件跟上（出错、必填、只读、名称、帮助文字）。</summary>
    void Sync()
    {
        label.Text = Label ?? "";
        label.IsRequired = IsRequired;
        label.Visible = HasText(Label) && !IsLabelHidden;
        description.Text = Description ?? "";
        description.Visible = HasText(Description);
        errorText.Text = Error ?? "";
        error.Visible = ShowsError;
        var controls = FieldControls();
        label.Target = controls.FirstOrDefault() as Control;
        foreach (var control in controls) control.FieldChanged();
        PerformLayout();
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control != label && e.Control != description && e.Control != error) Sync();
    }

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        Sync();
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => engine.Measure(proposedSize);

    /// <summary>控件里第一行文字的基线（设备像素，相对控件的顶边）：控件报的基线，否则最上面的子控件的。</summary>
    internal static int? FirstBaseline(Control control)
    {
        if (control is IBaseline { Baseline: { } baseline }) return baseline;
        foreach (var child in control.Controls.Cast<Control>().Where(child => child.Visible).OrderBy(child => child.Top).ThenBy(child => child.Left))
            if (FirstBaseline(child) is { } inner) return child.Top + inner;
        return null;
    }

    /// <summary>聚焦字段的控件：能选中就聚焦它，否则聚焦它里面的 Tab 停靠项（单选组的选中项）或第一个能选中的。</summary>
    internal static void FocusControl(Control control)
    {
        var candidates = Descendants(control).Where(element => element.CanSelect).ToList();
        (candidates.FirstOrDefault(element => element.TabStop) ?? candidates.FirstOrDefault())?.Focus();

        static IEnumerable<Control> Descendants(Control node)
        {
            yield return node;
            foreach (Control child in node.Controls)
                foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    static bool HasText(string? text) => !string.IsNullOrEmpty(text);

    /// <summary>上方：标签、控件、说明、错误竖排；起始边：标签一列，控件、说明、错误一列，标签与控件第一行文字的基线对齐。</summary>
    sealed class FieldEngine(FieldControl owner) : LayoutEngine
    {
        float Em => owner.Font.Size * owner.DeviceDpi / 72f;

        int Gap => (int)MathF.Round(Tokens.Space.Xs * Em);

        int LabelColumn => owner.LabelWidth is { } width ? owner.LogicalToDeviceUnits(width) : (int)MathF.Round(8 * Em);

        int Column => owner.Start ? LabelColumn + (int)MathF.Round(Tokens.Space.Md * Em) : 0;

        IEnumerable<Control> Stack() =>
            new[] { owner.Content, owner.description, owner.error }.OfType<Control>().Where(control => control.Visible);

        public Size Measure(Size proposed)
        {
            var available = proposed.Width is > 1 and < int.MaxValue ? Math.Max(0, proposed.Width - owner.Padding.Horizontal) : int.MaxValue;
            var inner = available == int.MaxValue ? int.MaxValue : Math.Max(0, available - Column);
            var (width, height, rows) = (0, 0, 0);
            void Add(Control part, int limit)
            {
                var size = part.GetPreferredSize(new Size(limit, 0));
                height += size.Height + (rows++ > 0 ? Gap : 0);
                width = Math.Max(width, size.Width);
            }
            if (owner.label.Visible && !owner.Start) Add(owner.label, available);
            foreach (var part in Stack()) Add(part, inner);
            if (owner.Start)
            {
                width += Column;
                if (owner.label.Visible) height = Math.Max(height, owner.label.GetPreferredSize(new Size(LabelColumn, 0)).Height);
            }
            return new Size(width + owner.Padding.Horizontal, height + owner.Padding.Vertical);
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            var area = owner.DisplayRectangle;
            var rtl = owner.RightToLeft == RightToLeft.Yes;
            var x = Column;
            var width = Math.Max(0, area.Width - x);
            var y = 0;
            if (owner.label.Visible && !owner.Start)
            {
                var size = owner.label.GetPreferredSize(new Size(area.Width, 0));
                owner.label.Bounds = Place(0, y, Math.Min(area.Width, size.Width), size.Height);
                y += size.Height + Gap;
            }
            var first = true;
            foreach (var part in Stack())
            {
                if (!first) y += Gap;
                var height = part.GetPreferredSize(new Size(width, 0)).Height;
                // 控件、说明按自己想要的宽度（不拉伸），宽度受控件列限制
                var preferred = Math.Min(width, part.GetPreferredSize(new Size(width, 0)).Width);
                part.Bounds = Place(x, y, part is FlexControl { Direction.Value: "column" } ? width : preferred, height);
                y += height;
                first = false;
            }
            if (owner.label.Visible && owner.Start)
            {
                var size = owner.label.GetPreferredSize(new Size(LabelColumn, 0));
                var labelWidth = Math.Min(LabelColumn, size.Width);
                var labelX = owner.LabelAlign == FieldLabelAlign.Start ? 0 : LabelColumn - labelWidth;
                var top = owner.Content is { } content && FirstBaseline(content) is { } target && ((IBaseline)owner.label).Baseline is { } own ? Math.Max(0, content.Top - area.Y + target - own) : 0;
                owner.label.Bounds = Place(labelX, top, labelWidth, size.Height);
            }
            return false;

            Rectangle Place(int left, int top, int w, int h) => new(rtl ? area.Right - left - w : area.X + left, area.Y + top, w, h);
        }
    }
}

/// <summary>接入 <see cref="FieldControl"/> 的控件：字段的状态变了时跟上。</summary>
interface IFieldControl
{
    /// <summary>字段的出错、必填、只读、标签、说明变了。</summary>
    void FieldChanged();
}
