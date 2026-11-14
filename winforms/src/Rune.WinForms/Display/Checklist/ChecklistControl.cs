using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>可以勾选的清单里，一项的完成状态改了。</summary>
/// <param name="item">这一项。</param>
/// <param name="done">新的完成状态。</param>
public sealed class ChecklistCheckedEventArgs(ChecklistItem item, bool done) : EventArgs
{
    /// <summary>这一项。</summary>
    public ChecklistItem Item { get; } = item;

    /// <summary>新的完成状态（状态由使用方保存，再换掉 Items）。</summary>
    public bool Done { get; } = done;
}

/// <summary>
/// 完成清单（vue-tsx-pro 的 Checklist）：一组要做完的事，每项的状态（完成、待办、超时、受阻、不适用）与它的上下文——怎么做、谁负责、哪天、证据、去处理。
/// 里程碑与交付证据、结项材料、上线检查、新用户的引导任务。
/// <code>
/// var materials = new ChecklistControl { Items = items, Summary = true };
/// var nodes = new ChecklistControl { Items = milestones, ItemLayout = ChecklistLayout.Icons };
/// launch.CheckedChange += (_, e) => Save(e.Item.Key, e.Done);   // 订阅了就能勾选
/// </code>
/// <see cref="Summary"/> 在上面显示"已完成 2 / 3"与 xs 的进度条（不适用的不算，全部完成时 success 色）。没完成的名称是 textMuted，不适用的划掉；超时的日期 dangerText。
/// 状态图标比文字大一档（1.14em），done 是 successText、overdue 是 dangerText、blocked 是 warningText，其余 textMuted；状态写在读屏的名称里。
/// 订阅了 <see cref="CheckedChange"/> 时状态图标是复选按钮（读屏的名称是这一项的名称，描述是状态），点了报告新的完成状态；不适用、受阻的不能勾。
/// 排法见 <see cref="ChecklistLayout"/>。容器里的链接（证据）悬停才有下划线。
/// </summary>
[DefaultEvent(nameof(CheckedChange))]
public class ChecklistControl : FlexControl, IScopeAware
{
    readonly List<ChecklistRowControl> rows = [];
    readonly TooltipProvider tooltips = new();
    EventHandler<ChecklistCheckedEventArgs>? checkedChange;

    /// <summary>新建：空的清单，竖排。</summary>
    public ChecklistControl()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Sm;
        Align = FlexAlign.Stretch;
        AutoSize = true;
        // 界面里的链接（证据、操作）悬停才有下划线
        LinkControl.SetUnderlineScope(this, LinkUnderline.Hover);
    }

    /// <summary>各项。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable<ChecklistItem>? Items { get; set { field = value; Rebuild(); } }

    /// <summary>排法，默认 list（TS 的 layout；控件自己有 Layout 事件）。</summary>
    [Category("Appearance")]
    [DefaultValue(ChecklistLayout.List)]
    public ChecklistLayout ItemLayout { get; set { field = value; Rebuild(); } }

    /// <summary>上面显示"已完成 2 / 3"与进度条（list 排法）。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool Summary { get; set { field = value; Rebuild(); } }

    /// <summary>字号档位；不写时跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get;
        set
        {
            field = value;
            if (value.Pixels is { } pixels) Font = Fonts.Get(Font.FamilyName(), pixels * 72 / 96, Font.Style);
            else ResetFont();
        }
    }

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    /// <summary>可以勾选：订阅了它，状态图标就是复选按钮；点了报告新的完成状态（不适用、受阻的不能勾）。</summary>
    public event EventHandler<ChecklistCheckedEventArgs>? CheckedChange
    {
        add
        {
            checkedChange += value;
            Rebuild();
        }
        remove
        {
            checkedChange -= value;
            Rebuild();
        }
    }

    internal bool CanCheck => checkedChange is not null;

    /// <summary>显示的各行（测试用）：list 与 inline 排法的每一项。</summary>
    internal IReadOnlyList<ChecklistRowControl> Rows => rows;

    /// <summary>汇总行（测试用）；没有时为 null。</summary>
    internal FlexControl? SummaryPart { get; private set; }

    /// <summary>图标排法的那一排（测试用）。</summary>
    internal FlexControl? IconsPart { get; private set; }

    /// <summary>图标排法的提示（测试用）。</summary>
    internal string? IconsTooltip => IconsPart is null ? null : tooltips.GetTooltip(IconsPart);

    void IScopeAware.ScopeChanged() => Rebuild();

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 状态文字取所在位置的语言包
        Rebuild();
    }

    /// <summary>按排法重建（同 TS 每次渲染重算）；重建前放开旧行里使用方给的控件，新行才放得进去。</summary>
    void Rebuild()
    {
        SuspendLayout();
        foreach (var row in rows) row.Detach();
        rows.Clear();
        if (IconsPart is not null) tooltips.SetTooltip(IconsPart, null);
        foreach (var child in Controls.Cast<Control>().ToList()) child.Dispose();
        Controls.Clear();
        SummaryPart = IconsPart = null;
        var items = Items?.ToList() ?? [];
        var lang = ProviderControl.LangOf(this);
        if (ItemLayout == ChecklistLayout.Icons)
        {
            // 只有一排图标：整组是一张图，名称与提示是每一项的名称与状态
            var label = lang.ChecklistIconsLabel([.. items.Select(item => lang.ChecklistItemState(ChecklistModel.LabelText(item), lang.ChecklistStatus[ChecklistModel.LangKey(item.Status)]))]);
            IconsPart = new FlexControl { Align = FlexAlign.Center, Gap = Space.Xxs, AccessibleRole = AccessibleRole.Graphic, AccessibleName = label };
            IconsPart.Controls.AddRange([.. items.Select(item => StatusIcon(item.Status))]);
            tooltips.SetTooltip(IconsPart, label);
            Controls.Add(IconsPart);
        }
        else if (ItemLayout == ChecklistLayout.Inline)
        {
            var inline = new FlexListControl { Wrap = true, ColumnGap = Space.Lg, RowGap = Space.Xs, Align = FlexAlign.Center };
            inline.Controls.AddRange([.. items.Select(item => Add(new ChecklistRowControl(this, item, inline: true)))]);
            Controls.Add(inline);
        }
        else
        {
            var (done, total) = ChecklistModel.Count(items);
            if (Summary && total > 0)
            {
                var text = lang.ChecklistSummary(done, total);
                var progress = new ProgressControl { Value = done, Max = total, AccessibleName = text };
                progress.Element.Size = TextSize.Xs;
                progress.Element.Tone = done == total ? StatusTone.Success : StatusTone.Accent;
                progress.Element.ShowValueLabel = false;
                SummaryPart = new FlexControl { Align = FlexAlign.Center, Gap = Space.Md };
                SummaryPart.Controls.AddRange([new TextControl { Text = text, Tone = TextTone.Muted, TextSize = TextSize.Sm }, progress]);
                SummaryPart.SetGrow(progress, true);
                Controls.Add(SummaryPart);
            }
            var list = new FlexListControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch };
            list.Controls.AddRange([.. items.Select(item => Add(new ChecklistRowControl(this, item, inline: false)))]);
            Controls.Add(list);
        }
        ResumeLayout();
    }

    ChecklistRowControl Add(ChecklistRowControl row)
    {
        rows.Add(row);
        return row;
    }

    /// <summary>状态图标：done successText、overdue dangerText、blocked warningText，其余 textMuted；比文字大一档（1.14em）。</summary>
    internal IconControl StatusIcon(ChecklistStatus status) => new()
    {
        Data = ChecklistModel.Icon(status),
        Tone = ToneOf(status),
        // 图标跟着自己的字号：给它大一档的字体
        Font = Fonts.Get(Font.FamilyName(), Font.SizeInPoints * 1.14f, Font.Style),
    };

    /// <summary>fontSizes.sm 的字体（固定的字号，同 TS 的 _sm）：说明、负责人、日期、证据，使用方给的控件（证据的链接）也用它。</summary>
    internal Font SmallFont => Fonts.Get(Font.FamilyName(), Tokens.FontSize.Sm * 72 / 96, Font.Style);

    internal static TextTone ToneOf(ChecklistStatus status) => status switch
    {
        ChecklistStatus.Done => TextTone.Success,
        ChecklistStatus.Overdue => TextTone.Danger,
        ChecklistStatus.Blocked => TextTone.Warning,
        _ => TextTone.Muted,
    };

    /// <summary>勾了、取消勾了一项。</summary>
    internal void Toggle(ChecklistItem item) => checkedChange?.Invoke(this, new ChecklistCheckedEventArgs(item, item.Status != ChecklistStatus.Done));

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) tooltips.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// 清单的一项（列表的一项）：list 排法是 [状态][名称、说明、负责人 · 日期 · 证据][操作]，状态贴着名称那一行，上下 space.xs、隔 space.sm；
/// inline 排法是一行 [状态][名称][日期][证据]，隔 space.xs。
/// </summary>
public sealed class ChecklistRowControl : FlexControl
{
    readonly List<FlexControl> owned = [];

    readonly ChecklistControl owner;

    internal ChecklistRowControl(ChecklistControl owner, ChecklistItem item, bool inline)
    {
        this.owner = owner;
        Item = item;
        AccessibleRole = AccessibleRole.ListItem;
        var state = ProviderControl.LangOf(owner).ChecklistStatus[ChecklistModel.LangKey(item.Status)];
        LabelPart = Label(item);
        Status = StatusPart(owner, item, state);
        var date = item.Date is null ? null : Part(item.Date, item.Status == ChecklistStatus.Overdue ? TextTone.Danger : TextTone.Muted, small: true);
        if (inline)
        {
            Align = FlexAlign.Center;
            Gap = Space.Xs;
            Controls.AddRange([Status, LabelPart]);
            if (date is not null) Controls.Add(date);
            if (item.Evidence is not null) Controls.Add(Part(item.Evidence, TextTone.Muted, small: true));
            return;
        }
        Align = FlexAlign.Start;
        Gap = Space.Sm;
        Padding = new Padding(0, (int)Math.Round(Tokens.Space.Xs * owner.EmPixels()), 0, (int)Math.Round(Tokens.Space.Xs * owner.EmPixels()));
        var body = Own(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xxs, Align = FlexAlign.Start });
        body.Controls.Add(LabelPart);
        if (item.Description is not null) body.Controls.Add(Description = Part(item.Description, TextTone.Muted, small: true));
        if (item.Owner is not null || date is not null || item.Evidence is not null)
        {
            Meta = Own(new FlexControl { Align = FlexAlign.Center, Wrap = true, ColumnGap = Space.Sm, RowGap = Space.Xxs });
            if (item.Owner is not null) Meta.Controls.Add(Part(item.Owner, TextTone.Muted, small: true));
            if (date is not null) Meta.Controls.Add(date);
            if (item.Evidence is not null) Meta.Controls.Add(Part(item.Evidence, TextTone.Muted, small: true));
            body.Controls.Add(Meta);
        }
        Controls.AddRange([Status, body]);
        SetGrow(body, true);
        if (item.Action is null) return;
        Action = Part(item.Action, TextTone.Neutral, small: false);
        Controls.Add(Action);
    }

    /// <summary>这一项。</summary>
    public ChecklistItem Item { get; }

    /// <summary>状态（图标，或可勾选时的复选按钮）。</summary>
    internal Control Status { get; }

    /// <summary>名称。</summary>
    internal Control LabelPart { get; }

    /// <summary>说明。</summary>
    internal Control? Description { get; }

    /// <summary>负责人、日期、证据那一行。</summary>
    internal FlexControl? Meta { get; }

    /// <summary>操作。</summary>
    internal Control? Action { get; }

    FlexControl Own(FlexControl panel)
    {
        owned.Add(panel);
        return panel;
    }

    /// <summary>放开使用方给的控件：重建时它们要放进新的一行（重建会释放旧行，放开以后它们不跟着被释放）。</summary>
    internal void Detach()
    {
        foreach (var panel in Enumerable.Reverse(owned)) panel.Controls.Clear();
        Controls.Clear();
    }

    /// <summary>名称：没完成的是 textMuted，不适用的划掉。</summary>
    Control Label(ChecklistItem item)
    {
        var label = Part(item.Label ?? "", item.Status == ChecklistStatus.Done ? TextTone.Neutral : TextTone.Muted, small: false);
        if (label is TextControl text && item.Status == ChecklistStatus.Skipped) text.Element.Strikethrough = true;
        return label;
    }

    /// <summary>状态：可以勾选时是复选按钮（名称是这一项的名称、描述是状态），否则是图标（读屏的名称是状态）。</summary>
    static Control StatusPart(ChecklistControl owner, ChecklistItem item, string state)
    {
        if (!owner.CanCheck)
        {
            var icon = owner.StatusIcon(item.Status);
            icon.AccessibleName = state;
            return icon;
        }
        var toggle = new ChecklistToggleControl(item, ChecklistModel.LabelText(item), state) { Enabled = ChecklistModel.Checkable(item.Status) };
        toggle.Click += (_, _) => owner.Toggle(item);
        return toggle;
    }

    /// <summary>文字或使用方给的控件；小一号的是 fontSizes.sm（固定的字号，同 TS 的 _sm），使用方给的控件也换成这个字体。</summary>
    Control Part(object value, TextTone tone, bool small)
    {
        if (value is not Control control) return new TextControl { Text = value.ToString(), Tone = tone, TextSize = small ? TextSize.Sm : default };
        if (small) control.Font = owner.SmallFont;
        return control;
    }
}

/// <summary>可以勾选时的状态：只有图标的复选按钮（1.14em 的图标），可点的区域不小于 24 像素，悬停时圆圈后面一层 fillHover，键盘聚焦时焦点环。</summary>
sealed class ChecklistToggleControl : Control
{
    bool hovered;

    public ChecklistToggleControl(ChecklistItem item, string label, string state)
    {
        (Item, Label, State) = (item, label, state);
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = true;
        Cursor = Cursors.Hand;
        AutoSize = true;
    }

    ChecklistItem Item { get; }

    string Label { get; }

    string State { get; }

    float Em => this.EmPixels();

    public override Size GetPreferredSize(Size proposedSize)
    {
        var side = (int)Math.Ceiling(Math.Max(Tokens.Size.MinTarget * DeviceDpi / 96f, 1.14f * Em));
        return new Size(side, side);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = false;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is not (Keys.Space or Keys.Enter)) return;
        OnClick(EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var colors = ProviderControl.ThemeOf(this).Colors;
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
        if (hovered && Enabled)
        {
            using var fill = new SolidBrush(colors.FillHover);
            graphics.FillEllipse(fill, bounds);
        }
        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(colors.FocusRing, 2 * DeviceDpi / 96f);
            graphics.DrawEllipse(ring, RectangleF.Inflate(bounds, -1, -1));
        }
        var size = 1.14f * Em;
        var color = ChecklistControl.ToneOf(Item.Status).ColorIn(ElementContext.Of(this));
        if (!Enabled) color = Color.FromArgb((int)(255 * Tokens.Opacity.Disabled), color);
        graphics.TranslateTransform((Width - size) / 2, (Height - size) / 2);
        graphics.ScaleTransform(size / IconData.Size, size / IconData.Size);
        using var path = ChecklistModel.Icon(Item.Status).CreatePath();
        using var pen = new Pen(color, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        graphics.DrawPath(pen, path);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ToggleAccessibleObject(this);

    /// <summary>读屏：复选框，名称是这一项的名称，描述是状态。</summary>
    sealed class ToggleAccessibleObject(ChecklistToggleControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.CheckButton;

        public override string? Name => owner.Label;

        public override string? Description => owner.State;

        public override AccessibleStates State =>
            base.State | (owner.Item.Status == ChecklistStatus.Done ? AccessibleStates.Checked : AccessibleStates.None);

        public override string DefaultAction => "toggle";

        public override void DoDefaultAction()
        {
            if (owner.Enabled) owner.OnClick(EventArgs.Empty);
        }
    }
}
