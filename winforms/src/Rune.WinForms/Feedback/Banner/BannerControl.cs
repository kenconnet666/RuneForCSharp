using System.ComponentModel;
using System.Windows.Forms.Automation;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 横幅（vue-tsx-pro 的 Banner）：贴着页面（或区域）顶部的一行通栏通知——系统维护、试用到期、新功能上线、只读模式这类整页都相关的消息。
/// <code>
/// var maintenance = new BannerControl { Tone = StatusTone.Warning, Title = "系统维护", Description = "今晚 22:00–23:00 暂停服务", Dock = DockStyle.Top };
/// </code>
/// 和 <see cref="AlertControl"/> 的分工：Alert 放在内容里、可以有多行正文；Banner 占满宽度、一行说完，内容居中，没有圆角，放在页面最上方。
/// 排列：[图标][标题（中等字重——GDI 按常规画）正文][操作]，间距 space.sm，上下 space.sm、左右 space.lg；放不下时换行（行距 space.xs），
/// 标题与正文一行也放不下时标题在上、正文在下，都居中。关闭按钮贴在结束边（space.sm），不参与居中，有它时结束端多留 2em。
/// 颜色 = 样式（<see cref="Variant"/>：soft 默认、solid 实底）× 色调（默认 info），同 Badge 的色表（BadgeRecipe）。图标不写时按色调选，写 null 不显示；
/// <see cref="Live"/> 同 AlertControl。订阅了 <see cref="CloseRequested"/> 才有关闭按钮：请使用方移除（子控件不能半透明，没有离场的淡出）。
/// 宽度由父容器给（停靠、锚定），高度跟着内容。
/// </summary>
[ToolboxItem(true)]
[Description("横幅：页面顶部的一行通栏通知。")]
public class BannerControl : Panel, IElementHost, IScopeAware
{
    readonly BannerEngine engine;
    readonly Effect paint;
    Icon icon = new();
    Text title = new();
    Text description = new();
    EventHandler? closeRequested;
    CloseButtonControl? close;
    bool closeHovered;
    bool iconSet;
    TextSize textSize;

    /// <summary>新建：高度跟着内容。</summary>
    public BannerControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AccessibleRole = AccessibleRole.Grouping;
        engine = new BannerEngine(this);
        paint = new Effect(Invalidate);
        RebuildIcon();
        RebuildTexts();
    }

    /// <summary>样式，默认 soft。</summary>
    [Category("Appearance")]
    public BannerVariant Variant { get => field.Value is null ? BannerVariant.Soft : field; set { field = value; Invalidate(); } }

    /// <summary>色调，默认 info。</summary>
    [Category("Appearance")]
    public StatusTone Tone { get => field.Value is null ? StatusTone.Info : field; set { field = value; Recompute(); } }

    /// <summary>字号档位；不写时跟随字体。</summary>
    [Category("Appearance")]
    public TextSize TextSize { get => textSize; set { textSize = value; ApplyTextSize(); } }

    /// <summary>中等字重的开头（"系统维护"），与正文在同一行。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Title { get => title.Content; set { title.Content = value; Recompute(); } }

    /// <summary>正文。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Description { get => description.Content; set { description.Content = value; Recompute(); } }

    /// <summary>图标；不写时按色调选，写 null 不显示。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconData? Icon
    {
        get;
        set
        {
            field = value;
            iconSet = true;
            Recompute();
        }
    }

    /// <summary>图标的部件属性（TS 的 propsIcon），逐字段盖在默认值上。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsIcon { get; set { field = value; RebuildIcon(); } }

    /// <summary>标题的部件属性（TS 的 propsTitle），逐字段盖在默认的中等字重上。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsTitle { get; set { field = value; RebuildTexts(); } }

    /// <summary>正文的部件属性（TS 的 propsContent）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsContent { get; set { field = value; RebuildTexts(); } }

    /// <summary>读屏播报方式，默认 off（同 AlertControl）。</summary>
    [Category("Accessibility")]
    public AlertLive Live { get => field.Value is null ? AlertLive.Off : field; set => field = value; }

    /// <summary>正文后的操作（链接或小按钮）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Action
    {
        get;
        set
        {
            if (field is not null) Controls.Remove(field);
            field = value;
            if (value is not null) Controls.Add(value);
            Relayout();
        }
    }

    /// <summary>请求关闭（TS 的 onClose）：使用方移除横幅并记住"已关闭"。订阅了才有关闭按钮。</summary>
    [Category("Action")]
    public event EventHandler? CloseRequested
    {
        add
        {
            closeRequested += value;
            SyncClose();
        }
        remove
        {
            closeRequested -= value;
            SyncClose();
        }
    }

    bool ShouldSerializeVariant() => Variant != BannerVariant.Soft;

    bool ShouldSerializeTone() => Tone != StatusTone.Info;

    bool ShouldSerializeTextSize() => textSize.Value is not null;

    bool ShouldSerializeLive() => Live != AlertLive.Off;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    float Em => this.EmPixels();

    ElementContext Context => ElementContext.Of(this);

    /// <summary>显示的图标（测试用）。</summary>
    internal IconData? ShownIcon => icon.Data;

    /// <summary>关闭按钮（测试用）；没有订阅时为 null。</summary>
    internal CloseButtonControl? CloseButton => close;

    /// <summary>标题与正文的位置（测试用）。</summary>
    internal (Rectangle Title, Rectangle Description) TextBounds => (title.Bounds, description.Bounds);

    void RebuildIcon()
    {
        icon = new Icon { Host = this };
        new IconProps { Data = iconSet ? Icon : AlertControl.IconFor(Tone) }.Merge(PropsIcon).ApplyTo(icon);
        Relayout();
    }

    void RebuildTexts()
    {
        title = Rebuilt(title, new TextProps { Weight = TextWeight.Medium }.Merge(PropsTitle));
        description = Rebuilt(description, new TextProps().Merge(PropsContent));
        Relayout();

        Text Rebuilt(Text old, TextProps props)
        {
            var text = new Text { Host = this, Content = old.Content };
            props.ApplyTo(text);
            return text;
        }
    }

    /// <summary>图标与读屏：内容变了重新排版；实时区域念出新的标题与正文。</summary>
    void Recompute()
    {
        if (PropsIcon?.Data is null) icon.Data = iconSet ? Icon : AlertControl.IconFor(Tone);
        Relayout();
        if (Live != AlertLive.Off && IsHandleCreated && (Title.Length > 0 || Description.Length > 0))
            AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.Other,
                Live == AlertLive.Assertive ? AutomationNotificationProcessing.ImportantMostRecent : AutomationNotificationProcessing.MostRecent,
                string.Join(" ", new[] { Title, Description }.Where(text => text.Length > 0)));
    }

    void IScopeAware.ScopeChanged()
    {
        ApplyTextSize();
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ((IScopeAware)this).ScopeChanged();
    }

    void ApplyTextSize()
    {
        if (TextSize.Pixels is { } pixels)
        {
            var outer = Parent?.Font ?? Font;
            Font = Fonts.Get(outer.FamilyName(), pixels * 72 / 96, outer.Style);
        }
        else ResetFont();
        Relayout();
    }

    void Relayout()
    {
        PerformLayout();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    void SyncClose()
    {
        if (closeRequested is null)
        {
            if (close is null) return;
            Controls.Remove(close);
            close.Dispose();
            close = null;
            Relayout();
            return;
        }
        if (close is not null) return;
        close = new CloseButtonControl { Radius = Radius.Full };
        close.Click += (_, _) => Dismissal.Request(this, () => closeRequested?.Invoke(this, EventArgs.Empty));
        close.MouseEnter += (_, _) => SetCloseHovered(true);
        close.MouseLeave += (_, _) => SetCloseHovered(false);
        Controls.Add(close);
        Relayout();
    }

    void SetCloseHovered(bool hovered)
    {
        closeHovered = hovered;
        SyncChildColors();
    }

    /// <summary>
    /// 子控件跟着横幅的文字色：ForeColor 给色调写 inherit 的操作（实底上的链接对比度才够）；关闭按钮同色，
    /// 悬停的底色是它的 14%（状态层），彩色横幅上不是一块灰。
    /// </summary>
    void SyncChildColors()
    {
        var foreground = Colors().Foreground;
        if (ForeColor != foreground) ForeColor = foreground;
        if (close is null) return;
        var sx = new ButtonSx { Foreground = foreground, Background = closeHovered ? Color.FromArgb((int)MathF.Round(foreground.A * 0.14f), foreground) : null };
        if (close.Sx != sx) close.Sx = sx;
    }

    /// <summary>样式 × 色调的颜色（同 Badge 的色表：soft、solid 两档）。</summary>
    (Color? Background, Color Foreground, Color? Border) Colors() => BadgeRecipe.Colors(Variant.Value, Tone.Value, false, Context.Theme.Colors);

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => engine.Measure(proposedSize);

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var context = Context;
        var (background, foreground, _) = Colors();
        if (background is { } fill)
        {
            using var brush = new SolidBrush(fill);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }
        var tinted = context with { ForeColor = foreground };
        if (icon.Data is not null) icon.Paint(e.Graphics, tinted);
        if (Title.Length > 0) title.Paint(e.Graphics, tinted);
        if (Description.Length > 0) description.Paint(e.Graphics, tinted);
        SyncChildColors();
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new BannerAccessibleObject(this);

    void IElementHost.InvalidateElement(Element element) => Invalidate(element.Bounds);

    void IElementHost.InvalidateLayout(Element element) => Relayout();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>读屏：一组内容，名称是标题，描述是正文。</summary>
    sealed class BannerAccessibleObject(BannerControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.Title : owner.AccessibleName; set => owner.AccessibleName = value; }

        public override string? Description => string.IsNullOrEmpty(owner.AccessibleDescription) ? owner.Description : owner.AccessibleDescription;
    }

    /// <summary>
    /// 排版：图标、文字（标题 + 正文）、操作按顺序排成一行，间距 space.sm，放不下就换行（行距 space.xs），每行居中、行内竖直居中；
    /// 文字一行放不下时标题在上、正文在下（各自居中、可换行）。关闭按钮贴在结束边、整块竖直居中。
    /// </summary>
    sealed class BannerEngine(BannerControl owner) : LayoutEngine
    {
        static Size SizeOf(Control control) => control.AutoSize ? control.GetPreferredSize(Size.Empty) : control.Size;

        /// <summary>一项：宽、高与放下它的办法。</summary>
        sealed record Item(Size Size, Action<Point> Place);

        int Run(int width, bool arrange)
        {
            var em = owner.Em;
            var (sm, xs, lg) = ((int)MathF.Round(Tokens.Space.Sm * em), (int)MathF.Round(Tokens.Space.Xs * em), (int)MathF.Round(Tokens.Space.Lg * em));
            var context = owner.Context;
            var start = lg;
            var end = lg + (owner.close is null ? 0 : (int)MathF.Round(2 * em));
            var inner = Math.Max(1, width - start - end);
            var items = new List<Item>();
            if (owner.icon.Data is not null)
            {
                var side = (int)MathF.Round(em);
                owner.icon.Size = side * 96f / owner.DeviceDpi;
                items.Add(new Item(new Size(side, side), at => owner.icon.Bounds = new Rectangle(at, new Size(side, side))));
            }
            items.AddRange(TextItems(context, inner, sm));
            if (owner.Action is { Visible: true } action)
            {
                var size = SizeOf(action);
                items.Add(new Item(size, at => action.SetBounds(at.X, at.Y, size.Width, size.Height)));
            }
            // 一行一行地排：放得下就接在后面，放不下就另起一行
            var lines = new List<List<Item>>();
            var used = 0;
            foreach (var item in items)
            {
                if (lines.Count == 0 || used + sm + item.Size.Width > inner)
                {
                    lines.Add([item]);
                    used = item.Size.Width;
                }
                else
                {
                    lines[^1].Add(item);
                    used += sm + item.Size.Width;
                }
            }
            var y = sm;
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var lineWidth = line.Sum(item => item.Size.Width) + sm * (line.Count - 1);
                var lineHeight = line.Max(item => item.Size.Height);
                var x = start + (inner - lineWidth) / 2;
                foreach (var item in line)
                {
                    if (arrange) item.Place(new Point(x, y + (lineHeight - item.Size.Height) / 2));
                    x += item.Size.Width + sm;
                }
                y += lineHeight + (index < lines.Count - 1 ? xs : 0);
            }
            var height = Math.Max(y + sm, (int)MathF.Round(Tokens.LineHeight.Normal * em) + 2 * sm);
            if (arrange && owner.close is { } close)
            {
                // 小一档（TS 的 relativeFontSizeStyles.sm：sm / md）
                close.TextSize = Tokens.FontSize.Sm / Tokens.FontSize.Md * em * 96f / owner.DeviceDpi;
                var size = SizeOf(close);
                close.SetBounds(width - sm - size.Width, (height - size.Height) / 2, size.Width, size.Height);
            }
            return height;
        }

        /// <summary>文字：标题与正文一行放得下时并排（中间 space.sm）；放不下时标题在上、正文在下，各自居中、可换行。</summary>
        IEnumerable<Item> TextItems(ElementContext context, int inner, int gap)
        {
            var (title, text) = (owner.title, owner.description);
            var hasTitle = title.Content.Length > 0;
            var hasText = text.Content.Length > 0;
            if (!hasTitle && !hasText) yield break;
            var titleSize = hasTitle ? title.Measure(context, new Size(inner, int.MaxValue)) : Size.Empty;
            var textSize = hasText ? text.Measure(context, new Size(int.MaxValue, int.MaxValue)) : Size.Empty;
            var together = titleSize.Width + (hasTitle && hasText ? gap : 0) + textSize.Width;
            if (together <= inner)
            {
                var height = Math.Max(titleSize.Height, textSize.Height);
                yield return new Item(new Size(together, height), at =>
                {
                    if (hasTitle) title.Bounds = new Rectangle(at.X, at.Y + (height - titleSize.Height) / 2, titleSize.Width, titleSize.Height);
                    if (hasText) text.Bounds = new Rectangle(at.X + together - textSize.Width, at.Y + (height - textSize.Height) / 2, textSize.Width, textSize.Height);
                });
                yield break;
            }
            var wrapped = hasText ? text.Measure(context, new Size(inner, int.MaxValue)) : Size.Empty;
            var width = Math.Max(titleSize.Width, wrapped.Width);
            yield return new Item(new Size(width, titleSize.Height + wrapped.Height), at =>
            {
                if (hasTitle) title.Bounds = new Rectangle(at.X + (width - titleSize.Width) / 2, at.Y, titleSize.Width, titleSize.Height);
                if (hasText) text.Bounds = new Rectangle(at.X + (width - wrapped.Width) / 2, at.Y + titleSize.Height, wrapped.Width, wrapped.Height);
            });
        }

        public Size Measure(Size proposed)
        {
            var width = proposed.Width is > 0 and < int.MaxValue ? proposed.Width : owner.Width;
            return new Size(width, Run(width, arrange: false));
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            Run(owner.ClientSize.Width, arrange: true);
            return owner.AutoSize;
        }
    }
}
