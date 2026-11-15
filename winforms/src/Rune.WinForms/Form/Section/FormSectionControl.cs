using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 表单里的一组字段（vue-tsx-pro 的 FormSection）：标题、说明、结束边的操作，下面一条细线，再下面是字段的栅格。长表单按主题分组。
/// <code>
/// var basics = new FormSectionControl { Title = "项目基本信息", Columns = 2, Dock = DockStyle.Top };
/// basics.Fields.AddRange([new FieldControl { Label = "项目名称", Content = new TextFieldControl() }, …]);
/// </code>
/// <see cref="Columns"/> 是最多几列：每列不窄于 <see cref="MinColumnWidth"/>（默认 26em），窄时自动减少列数；跨列用 <see cref="Grid"/> 的 SetSpan 或 GridItemControl。
/// 列距 xl 比行距 lg 大（一眼看出哪几项是同一列），只有一个字段时也只占一列宽（auto-fill）。
/// 也是一层字段布局：标签位置、宽度、对齐、只读写在这里只作用于这一组，没写的沿用 FormControl 的；不可用写 Enabled。
/// <see cref="IsCollapsible"/> 时标题是展开收起的按钮（读屏带展开状态），收起的字段不能被 Tab 到。
/// 读屏：有标题时是一组（Grouping），以标题为名称、说明为帮助文字。WinForms 的读屏没有标题级别，<see cref="HeadingLevel"/> 选标题用哪一级的 H 控件。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(OpenChanged))]
[Description("表单里的一组字段：标题栏、多列的字段栅格，可折叠；这一组的标签布局与只读。")]
public class FormSectionControl : FlexControl, IFieldLayout
{
    readonly SectionHeader header = new();
    readonly FlexControl headerText = new() { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true };
    readonly DisclosureTrigger trigger = new() { Gap = Space.Xs, Align = FlexAlign.Center };
    readonly TextControl triggerText = new() { TextSize = TextSize.Md, Weight = TextWeight.Medium };
    readonly IconControl chevron = new() { Tone = TextTone.Muted };
    readonly TextControl description = new() { TextSize = TextSize.Sm, Tone = TextTone.Muted, Wrap = TextWrap.Wrap };
    readonly FlexControl extra = new() { Gap = Space.Sm, Align = FlexAlign.Center, AutoSize = true };
    readonly GridControl body = new() { RowGap = Space.Lg, ColumnGap = Space.Xl, Align = GridAlign.Start, AutoSize = true, AutoFill = true };
    readonly CollapseControl collapse = new();
    Control? heading;
    Control? headingControl;

    /// <summary>新建：一列，展开着。</summary>
    public FormSectionControl()
    {
        Direction = FlexDirection.Column;
        Align = FlexAlign.Stretch;
        Gap = Space.Lg;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
        trigger.Controls.AddRange([triggerText, chevron]);
        trigger.Click += (_, _) => IsOpen = !IsOpen;
        var row = new FlexControl { Justify = FlexJustify.Between, Align = FlexAlign.End, Wrap = true, ColumnGap = Space.Lg, RowGap = Space.Xs, AutoSize = true, Dock = DockStyle.Fill };
        row.Controls.AddRange([headerText, extra]);
        header.Controls.Add(row);
        Controls.AddRange([header, body]);
        Sync();
    }

    /// <summary>标题；不写时没有标题栏，只是一层栅格与字段布局。</summary>
    [Category("Appearance")]
    [Description("标题；空着时没有标题栏。")]
    [DefaultValue(null)]
    public string? Title { get; set { field = value; Sync(); } }

    /// <summary>标题下面的说明。</summary>
    [Category("Appearance")]
    [Description("标题下面的说明。")]
    [DefaultValue(null)]
    public string? Description { get; set { field = value; Sync(); } }

    /// <summary>标题栏结束边的内容：链接、按钮（"查看关联明细""添加"）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Extra
    {
        get;
        set
        {
            if (field is not null) extra.Controls.Remove(field);
            field = value;
            if (value is not null) extra.Controls.Add(value);
            Sync();
        }
    }

    /// <summary>标题用哪一级（2–6），默认 3。</summary>
    [Category("Appearance")]
    [Description("标题的级别（2–6）。")]
    [DefaultValue(3)]
    public int HeadingLevel
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 2);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 6);
            field = value;
            heading?.Dispose();
            heading = null;
            Sync();
        }
    } = 3;

    /// <summary>最多几列，默认 1；窄时按 MinColumnWidth 自动减少。</summary>
    [Category("Layout")]
    [Description("最多几列；窄时按 MinColumnWidth 自动减少。")]
    [DefaultValue(1)]
    public int Columns
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
            Sync();
        }
    } = 1;

    /// <summary>每列的最小宽度，默认 26em：标签在起始边时放得下标签、控件与横排的三四个选项。</summary>
    [Category("Layout")]
    [Description("每列的最小宽度（如 26em、240px）。")]
    public Length MinColumnWidth { get; set { field = value; Sync(); } } = new("26em");

    /// <summary>可以展开收起：标题变成按钮。</summary>
    [Category("Behavior")]
    [Description("可以展开收起：标题变成按钮。")]
    [DefaultValue(false)]
    public bool IsCollapsible { get; set { field = value; Sync(); } }

    /// <summary>是否展开（IsCollapsible 时），默认展开；变了发 <see cref="OpenChanged"/>。</summary>
    [Category("Behavior")]
    [Description("是否展开（可折叠时）。")]
    [DefaultValue(true)]
    public bool IsOpen
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
            OpenChanged?.Invoke(this, EventArgs.Empty);
        }
    } = true;

    /// <summary>这一组字段的标签放在哪；不写时沿用 FormControl 的。</summary>
    [Category("Layout")]
    [Description("这一组字段的标签放在哪：top、start；空着时沿用表单的。")]
    public FieldLabelPlacement LabelPlacement { get; set { field = value; FieldControl.LayoutChanged(this); } }

    /// <summary>这一组字段的标签列宽度（设备无关像素）；不写时沿用表单的。</summary>
    [Category("Layout")]
    [Description("这一组字段的标签列宽度（设备无关像素）；空着时沿用表单的。")]
    [DefaultValue(null)]
    public int? LabelWidth { get; set { field = value; FieldControl.LayoutChanged(this); } }

    /// <summary>这一组字段的标签文字对齐；不写时沿用表单的。</summary>
    [Category("Layout")]
    [Description("这一组字段的标签文字对齐：start、end；空着时沿用表单的。")]
    public FieldLabelAlign LabelAlign { get; set { field = value; FieldControl.LayoutChanged(this); } }

    /// <summary>这一组只读（审批时整组只读）。</summary>
    [Category("Behavior")]
    [Description("这一组只读。")]
    [DefaultValue(false)]
    public bool IsReadOnly { get; set { field = value; FieldControl.LayoutChanged(this); } }

    /// <summary>字段（放进栅格）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ControlCollection Fields => body.Controls;

    /// <summary>字段的栅格：跨列用它的 SetSpan。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GridControl Grid => body;

    /// <summary>展开、收起了。</summary>
    public event EventHandler? OpenChanged;

    /// <summary>标题栏、标题、展开收起的按钮（测试用）。</summary>
    internal (Control Header, Control? Heading, Control Trigger) Parts => (header, headingControl, trigger);

    bool ShouldSerializeLabelPlacement() => LabelPlacement.Value is not null;

    void ResetLabelPlacement() => LabelPlacement = default;

    bool ShouldSerializeLabelAlign() => LabelAlign.Value is not null;

    void ResetLabelAlign() => LabelAlign = default;

    bool ShouldSerializeMinColumnWidth() => MinColumnWidth != new Length("26em");

    /// <summary>各部分的内容与显示：标题栏（标题或按钮、说明、操作）、栅格的列、展开收起。</summary>
    void Sync()
    {
        var hasTitle = !string.IsNullOrEmpty(Title);
        var hasDescription = !string.IsNullOrEmpty(Description);
        // 标题 md 字号、medium 字重（各级 H 控件自己的档位是标题字号）
        heading ??= HeadingLevel switch
        {
            2 => new H2Control { TextSize = TextSize.Md, Weight = TextWeight.Medium },
            4 => new H4Control { TextSize = TextSize.Md, Weight = TextWeight.Medium },
            5 => new H5Control { TextSize = TextSize.Md, Weight = TextWeight.Medium },
            6 => new H6Control { TextSize = TextSize.Md, Weight = TextWeight.Medium },
            _ => (Control)new H3Control { TextSize = TextSize.Md, Weight = TextWeight.Medium },
        };
        headerText.Controls.Clear();
        if (hasTitle)
        {
            headingControl = IsCollapsible ? trigger : heading;
            if (IsCollapsible) triggerText.Text = Title!;
            else heading.Text = Title!;
            headerText.Controls.Add(headingControl);
        }
        description.Text = Description ?? "";
        if (hasDescription) headerText.Controls.Add(description);
        extra.Visible = Extra is not null;
        header.Visible = hasTitle || Extra is not null;

        body.Columns = Columns > 1 ? null : 1;
        body.MinItemWidth = Columns > 1 ? MinColumnWidth : (Length?)null;
        body.MaxColumns = Columns > 1 ? Columns : null;

        // 收起时箭头朝行尾（从左往右时朝右）
        chevron.Data = IsOpen ? Lucide.ChevronDown : RightToLeft == RightToLeft.Yes ? Lucide.ChevronLeft : Lucide.ChevronRight;
        trigger.Expanded = IsOpen;
        trigger.AccessibleName = Title;
        Place();
        collapse.IsOpen = !IsCollapsible || IsOpen;
        AccessibleRole = hasTitle ? AccessibleRole.Grouping : AccessibleRole.Pane;
        AccessibleName = hasTitle ? Title : null;
        AccessibleDescription = hasDescription ? Description : null;
    }

    /// <summary>可折叠时栅格放进展开收起层，否则直接放在标题栏下面。</summary>
    void Place()
    {
        var holder = IsCollapsible ? (Control)collapse : body;
        if (holder.Parent == this && (IsCollapsible ? body.Parent == collapse : true)) return;
        SuspendLayout();
        Controls.Remove(collapse);
        Controls.Remove(body);
        collapse.Controls.Clear();
        if (IsCollapsible) collapse.Controls.Add(body);
        Controls.Add(holder);
        ResumeLayout();
    }

    /// <summary>标题栏：下面一条 borderMuted 细线、下边距 sm。</summary>
    sealed class SectionHeader : Panel
    {
        public SectionHeader()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            Padding = new Padding(0, 0, 0, (int)MathF.Round(Tokens.Space.Sm * this.EmPixels()) + 1);
            base.OnLayout(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(ProviderControl.ThemeOf(this).Colors.BorderMuted);
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}
