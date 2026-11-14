using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 步骤条（vue-tsx-pro 的 Steps）：多步流程里的位置（下单、注册、配置向导、任务的流转）。每一步是序号圆圈加标题与说明：
/// 已完成的显示对勾、品牌色底；当前步骤品牌色的圈；未开始的灰圈；出错的显示警示图标、危险色。读屏的说明是这一步的状态（当前步骤、已完成、未开始、出错），
/// 不只靠颜色。
/// <code>
/// var steps = new StepsControl { Items = [new("填写信息"), new("确认订单"), new("完成")], Current = 1, Navigable = true };
/// </code>
/// 每一步的状态默认按 <see cref="Current"/> 推出，<see cref="StepItem.Status"/> 可以改（出错的一步、非线性的流程）。
/// <see cref="Navigable"/> 时已完成、出错的步骤可以点回去（写回 Current、发 <see cref="CurrentChanged"/>）；有 <see cref="StepItem.Href"/> 的是链接。
/// 横排时各步等宽，连接线在标题右侧延伸到下一步；竖排时连接线从圆圈下面连到下一步的圆圈。<see cref="TextSize"/> 让圆圈、文字一起缩放。
/// 每一步按自然宽度放（不撑满一格），连接线由步骤条画在空出来的地方：子控件是各自的窗口，会盖住父级画的东西。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(CurrentChanged))]
[Description("步骤条：多步流程里的位置。")]
public class StepsControl : Control
{
    readonly List<View> views = [];

    /// <summary>一步的部件：外层（圆圈与文字，可点回时是按钮）、状态。</summary>
    sealed record View(Control Content, StepState State);

    /// <summary>新建：横排。读屏里是一个列表。</summary>
    public StepsControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AccessibleRole = AccessibleRole.List;
    }

    /// <summary>步骤。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<StepItem>? Items { get; set { field = value; Rebuild(); } }

    /// <summary>当前步骤的序号，从 0 开始。</summary>
    [Category("Behavior")]
    [Description("当前步骤的序号，从 0 开始。")]
    [DefaultValue(0)]
    public int Current
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Rebuild();
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>方向，默认横排。</summary>
    [Category("Appearance")]
    [Description("方向：horizontal（横排，默认）、vertical（竖排）。")]
    public StepsOrientation Orientation { get; set { field = value; Rebuild(); } }

    /// <summary>字号档位：圆圈、文字随之缩放；不写时跟随外面的字体。</summary>
    [Category("Appearance")]
    [Description("字号档位：圆圈、文字随之缩放；不写时跟随外面的字体。")]
    public TextSize TextSize { get; set { field = value; ApplyTextSize(); } }

    /// <summary>已完成、出错的步骤可以点回去（写回 Current）。</summary>
    [Category("Behavior")]
    [Description("已完成、出错的步骤可以点回去。")]
    [DefaultValue(false)]
    public bool Navigable { get; set { field = value; Rebuild(); } }

    /// <summary>当前步骤变了（点回已完成的步骤，或代码里改了 Current）。</summary>
    public event EventHandler? CurrentChanged;

    bool ShouldSerializeOrientation() => Orientation.Value is not null;

    void ResetOrientation() => Orientation = default;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    void ResetTextSize() => TextSize = default;

    bool Vertical => Orientation == StepsOrientation.Vertical;

    float Em => this.EmPixels();

    /// <summary>各步（测试用）：圆圈与文字，可点回时是外面的按钮。</summary>
    internal IReadOnlyList<Control> Contents => [.. views.Select(view => view.Content)];

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => ManualLayout.Instance;

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ApplyTextSize();
    }

    /// <inheritdoc />
    protected override void OnParentFontChanged(EventArgs e)
    {
        base.OnParentFontChanged(e);
        ApplyTextSize();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Rebuild();
    }

    /// <summary>写了档位时按它定字号（字体族、样式跟着外面），否则跟随外面。</summary>
    void ApplyTextSize()
    {
        if (TextSize.Pixels is not { } pixels)
        {
            ResetFont();
            return;
        }
        var outer = Parent?.Font ?? Font;
        Font = Fonts.Get(outer.FamilyName(), pixels * 72 / 96, outer.Style);
    }

    /// <summary>按步骤重建：每步是圆圈加标题与说明；已完成、出错又能点回去的（有地址或 Navigable）是按钮。</summary>
    void Rebuild()
    {
        SuspendLayout();
        Controls.Cast<Control>().DisposeAll();
        views.Clear();
        var items = Items ?? [];
        var lang = ProviderControl.LangOf(this);
        var em = Em;
        var mark = 0.93f * em;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var state = StepRules.StateAt(item, index, Current);
            var text = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true, Padding = new Padding(0, (int)MathF.Round(0.3f * em), 0, 0) };
            text.Controls.Add(new TextControl { Text = item.Title, Weight = TextWeight.Medium });
            if (item.Description is { } description)
                text.Controls.Add(new TextControl { Text = description, Tone = TextTone.Muted, TextSize = (TextSize)(mark * 96 / DeviceDpi) });
            var indicator = new StepIndicatorControl { State = state, Number = index + 1, Mark = item.Icon };
            Control content;
            if (StepRules.Reachable(item, state) && (item.Href is not null || Navigable))
            {
                var (href, at) = (item.Href, index);
                // 可点回的步骤：整块是按钮，悬停 fillHover 底、sm 圆角，键盘聚焦时画焦点环
                var trigger = new PressableFlex { Align = FlexAlign.Start, Gap = Space.Sm, AccessibleName = item.Title };
                trigger.Controls.AddRange([indicator, text]);
                trigger.Click += (_, _) =>
                {
                    if (href is not null) LinkLauncher.Open(new Uri(href, UriKind.RelativeOrAbsolute));
                    else Current = at;
                };
                content = trigger;
            }
            else
            {
                content = new FlexControl { Align = FlexAlign.Start, Gap = Space.Sm, AutoSize = true };
                content.Controls.AddRange([indicator, text]);
            }
            content.AccessibleDescription = state == StepState.Current ? "当前步骤" : lang.StepState(state.Value).Trim('（', '）', '(', ')', ' ');
            Controls.Add(content);
            views.Add(new View(content, state));
        }
        ResumeLayout(false);
        PerformLayout();
        Invalidate();
    }

    /// <summary>横排时每步一格（等宽，TS 的 flex: 1 1 0，间距 space.sm）；竖排时一列往下（间距 space.lg）。</summary>
    (int Gap, int Column) Grid(int width)
    {
        var em = Em;
        if (Vertical) return ((int)MathF.Round(Tokens.Space.Lg * em), width);
        var gap = (int)MathF.Round(Tokens.Space.Sm * em);
        var count = Math.Max(1, views.Count);
        return (gap, Math.Max(1, (width - gap * (count - 1)) / count));
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (views.Count == 0) return Size.Empty;
        // 不限宽时（自然宽度）：横排是最宽的一步乘步数加间距（各步等宽），竖排是最宽的一步
        var width = proposedSize.Width is > 1 and < int.MaxValue ? proposedSize.Width
            : Vertical ? int.MaxValue
            : views.Max(view => view.Content.GetPreferredSize(Size.Empty).Width) * views.Count + Grid(0).Gap * (views.Count - 1);
        var (gap, column) = Grid(width);
        var sizes = views.Select(view => view.Content.GetPreferredSize(new Size(column, 0))).ToList();
        return Vertical
            ? new Size(sizes.Max(size => size.Width), sizes.Sum(size => size.Height) + gap * (sizes.Count - 1))
            : new Size(width, sizes.Max(size => size.Height));
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var (gap, column) = Grid(ClientSize.Width);
        var x = 0;
        var y = 0;
        var rtl = RightToLeft == RightToLeft.Yes;
        foreach (var view in views)
        {
            var size = view.Content.GetPreferredSize(new Size(column, 0));
            size.Width = Math.Min(size.Width, column);
            var left = rtl ? ClientSize.Width - x - size.Width : x;
            view.Content.Bounds = new Rectangle(left, y, size.Width, size.Height);
            if (Vertical) y += size.Height + gap;
            else x += column + gap;
        }
        Invalidate();
    }

    /// <summary>
    /// 步骤之间的连接线（xs 宽；这一步已完成时品牌色，否则 border）：横排在圆圈与文字的右边、隔 space.sm，延伸到这一格的右边，对齐圆圈的中线；
    /// 竖排从圆圈下面隔 space.xs 连到下一步的圆圈上面 space.xs。
    /// </summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var colors = ProviderControl.ThemeOf(this).Colors;
        var em = Em;
        var line = Math.Max(1, LogicalToDeviceUnits(1));
        var diameter = (int)MathF.Round(2 * 0.93f * em);
        var (gap, column) = Grid(ClientSize.Width);
        var rtl = RightToLeft == RightToLeft.Yes;
        for (var index = 0; index < views.Count - 1; index++)
        {
            var (content, state) = views[index];
            using var brush = new SolidBrush(state == StepState.Complete ? colors.Accent : colors.Border);
            if (Vertical)
            {
                var top = content.Top + diameter + (int)MathF.Round(0.25f * em);
                var bottom = views[index + 1].Content.Top - (int)MathF.Round(0.25f * em);
                var x = (rtl ? content.Right - diameter / 2 : content.Left + diameter / 2) - line / 2;
                e.Graphics.FillRectangle(brush, x, top, line, Math.Max(0, bottom - top));
                continue;
            }
            var cell = index * (column + gap);
            var start = cell + content.Width + (int)MathF.Round(0.5f * em);
            var end = cell + column;
            var y = content.Top + diameter / 2 - line / 2;
            e.Graphics.FillRectangle(brush, rtl ? ClientSize.Width - end : start, y, Math.Max(0, end - start), line);
        }
    }
}

/// <summary>
/// 序号圆圈（Steps 的部件）：直径 2 × 0.93em、xs 边框；已完成品牌色底与边、accentContrast 的对勾，当前品牌色边、accentText，
/// 未开始 border 边、次要色，出错 danger 边、dangerSoft 底、dangerText 的警示图标；没有图标时是序号（0.93em、加粗）。读屏不念（状态在步骤的说明里）。
/// </summary>
sealed class StepIndicator : Element
{
    public StepState State { get; set { field = value; Invalidate(); } }

    public int Number { get; set { field = value; Invalidate(); } }

    /// <summary>代替序号的图标（已完成、出错时不看它）。</summary>
    public IconData? Mark { get; set { field = value; Invalidate(); } }

    protected internal override Size Measure(ElementContext context, Size available)
    {
        var d = (int)MathF.Round(2 * 0.93f * context.Em);
        return new Size(d, d);
    }

    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var colors = context.Theme.Colors;
        var (border, fill, fore) = State.Value switch
        {
            "complete" => (colors.Accent, (Color?)colors.Accent, colors.AccentContrast),
            "current" => (colors.Accent, null, colors.AccentText),
            "error" => (colors.Danger, colors.DangerSoft, colors.DangerText),
            _ => (colors.Border, null, colors.TextMuted),
        };
        var line = context.Scale(1);
        var circle = new RectangleF(Bounds.X + line / 2, Bounds.Y + line / 2, Bounds.Width - line, Bounds.Height - line);
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            if (fill is { } color)
            {
                using var brush = new SolidBrush(color);
                graphics.FillEllipse(brush, circle);
            }
            using (var pen = new Pen(border, line)) graphics.DrawEllipse(pen, circle);
            var mark = 0.93f * context.Em;
            var icon = State == StepState.Complete ? Lucide.Check : State == StepState.Error ? AlertControl.IconFor(StatusTone.Danger) : Mark;
            if (icon is not null)
            {
                graphics.TranslateTransform(Bounds.X + (Bounds.Width - mark) / 2, Bounds.Y + (Bounds.Height - mark) / 2);
                graphics.ScaleTransform(mark / IconData.Size, mark / IconData.Size);
                using var stroke = new Pen(fore, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                using var path = icon.CreatePath();
                graphics.DrawPath(stroke, path);
                return;
            }
            var font = Fonts.Get(context.Font.FamilyName(), context.Font.SizeInPoints * 0.93f, FontStyle.Bold);
            TextRenderer.DrawText(graphics, Number.ToString(), font, Bounds, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
        finally
        {
            graphics.Restore(state);
        }
    }
}

/// <summary>放进窗体的序号圆圈（单元素宿主），读屏不念。</summary>
sealed class StepIndicatorControl : ElementControl<StepIndicator>
{
    public StepIndicatorControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.None;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public StepState State { get => Element.State; set => Element.State = value; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Number { get => Element.Number; set => Element.Number = value; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconData? Mark { get => Element.Mark; set => Element.Mark = value; }
}
