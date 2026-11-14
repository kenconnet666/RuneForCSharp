using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Automation;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 提示条（vue-tsx-pro 的 Alert）：页面或区块里的说明、成功、警告、错误信息。图标按色调自动选（Lucide），可以换或去掉；可以关闭。
/// <code>
/// var expiring = new AlertControl { Tone = StatusTone.Warning, Title = "即将过期", Description = "你的订阅将在 3 天后到期。", Dock = DockStyle.Top };
/// </code>
/// 外观：一行排开图标、文字区（标题中等字重——GDI 按常规画；说明是正文色）、操作、关闭按钮，间距 space.sm，内边距 space.md；
/// 颜色 = 样式（<see cref="Variant"/>：soft 默认、surface、outline）× 色调（<see cref="Tone"/>，默认 info），同 Badge 的色表（BadgeRecipe）；圆角默认 md。
/// 直接加的子控件是说明下面的额外内容（TS 同时给 description 与 children 时的 children）。<see cref="Icon"/> 不写时按色调选，写 null 不显示；
/// <see cref="IsLoading"/> 时图标换成加载指示。<see cref="Live"/> 不是 off 时标题、说明变了让读屏念（自动化通知）。
/// 订阅了 <see cref="CloseRequested"/> 才有关闭按钮：点了请使用方移除；焦点原来在里面、被移除了时交给相邻的控件。子控件不能半透明，没有离场的淡出。
/// 宽度由父容器给（停靠、锚定），高度跟着内容。
/// </summary>
[ToolboxItem(true)]
[Description("提示条：说明、成功、警告、错误信息。")]
public class AlertControl : Panel, IElementHost, IScopeAware
{
    readonly AlertEngine engine;
    readonly Effect paint;
    Icon icon = new();
    Text title = new();
    Text description = new();
    readonly SpinnerControl spinner = new() { Visible = false };
    EventHandler? closeRequested;
    CloseButtonControl? close;
    bool closeHovered;
    bool iconSet;
    TextSize textSize;

    /// <summary>新建：高度跟着内容。</summary>
    public AlertControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AccessibleRole = AccessibleRole.Grouping;
        engine = new AlertEngine(this);
        RebuildIcon();
        RebuildTexts();
        paint = new Effect(Invalidate);
        Controls.Add(spinner);
        Recompute();
    }

    /// <summary>样式，默认 soft。</summary>
    [Category("Appearance")]
    public AlertVariant Variant { get => field.Value is null ? AlertVariant.Soft : field; set { field = value; Invalidate(); } }

    /// <summary>色调，默认 info。</summary>
    [Category("Appearance")]
    public StatusTone Tone { get => field.Value is null ? StatusTone.Info : field; set { field = value; Recompute(); } }

    /// <summary>圆角，默认 md；贴着页面顶部的通栏写 none。</summary>
    [Category("Appearance")]
    public Radius Radius { get => field.Value is null ? Radius.Md : field; set { field = value; Invalidate(); } }

    /// <summary>字号档位：图标、内边距随之缩放；不写时跟随字体。</summary>
    [Category("Appearance")]
    public TextSize TextSize { get => textSize; set { textSize = value; ApplyTextSize(); } }

    /// <summary>标题（中等字重的第一行）。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Title { get => title.Content; set { title.Content = value; Recompute(); } }

    /// <summary>说明（标题下面的正文）。</summary>
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

    /// <summary>说明的部件属性（TS 的 propsDescription）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsDescription { get; set { field = value; RebuildTexts(); } }

    /// <summary>新建图标元素，套上合并后的部件属性（没写的字段回到 Icon 自己的默认）。</summary>
    void RebuildIcon()
    {
        icon = new Icon { Host = this };
        new IconProps { Data = iconSet ? Icon : IconFor(Tone) }.Merge(PropsIcon).ApplyTo(icon);
        Relayout();
    }

    /// <summary>新建标题、说明元素（保留文字），套上合并后的部件属性。</summary>
    void RebuildTexts()
    {
        title = Rebuilt(title, new TextProps { Weight = TextWeight.Medium }.Merge(PropsTitle));
        description = Rebuilt(description, new TextProps().Merge(PropsDescription));
        Relayout();

        Text Rebuilt(Text old, TextProps props)
        {
            var text = new Text { Host = this, Content = old.Content };
            props.ApplyTo(text);
            return text;
        }
    }

    /// <summary>进行中（如"正在上传"）：图标换成加载指示。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsLoading { get; set { field = value; Recompute(); } }

    /// <summary>读屏播报方式，默认 off：页面加载时就在的提示不打断；操作后才出现的写 polite（错误用 assertive）。</summary>
    [Category("Accessibility")]
    public AlertLive Live { get => field.Value is null ? AlertLive.Off : field; set => field = value; }

    /// <summary>右侧的操作（如"重试"按钮）。</summary>
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

    /// <summary>请求关闭（TS 的 onClose）：使用方移除提示条。订阅了才有关闭按钮。</summary>
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

    bool ShouldSerializeVariant() => Variant != AlertVariant.Soft;

    bool ShouldSerializeTone() => Tone != StatusTone.Info;

    bool ShouldSerializeRadius() => Radius != Radius.Md;

    bool ShouldSerializeTextSize() => textSize.Value is not null;

    bool ShouldSerializeLive() => Live != AlertLive.Off;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    float Em => this.EmPixels();

    ElementContext Context => ElementContext.Of(this);

    /// <summary>显示的图标（测试用）：写了 null 或加载中时为 null。</summary>
    internal IconData? ShownIcon => IsLoading ? null : icon.Data;

    /// <summary>关闭按钮（测试用）；没有订阅时为 null。</summary>
    internal CloseButtonControl? CloseButton => close;

    /// <summary>每种色调默认的图标（vue-tsx-pro 的 statusIcons）：提示条、横幅按色调自动选。</summary>
    internal static IconData IconFor(StatusTone tone) => tone.Value switch
    {
        "success" => Lucide.CircleCheck,
        "warning" => Lucide.TriangleExclamationPoint,
        "danger" => Lucide.CircleExclamationPoint,
        _ => Lucide.Info,
    };

    /// <summary>图标、加载指示与读屏：内容变了重新排版；实时区域念出新的标题与说明。</summary>
    void Recompute()
    {
        if (PropsIcon?.Data is null) icon.Data = iconSet ? Icon : IconFor(Tone);
        spinner.Visible = IsLoading;
        Relayout();
        if (Live != AlertLive.Off && IsHandleCreated && (Title.Length > 0 || Description.Length > 0))
            AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.Other,
                Live == AlertLive.Assertive ? AutomationNotificationProcessing.ImportantMostRecent : AutomationNotificationProcessing.MostRecent,
                string.Join(" ", new[] { Title, Description }.Where(text => text.Length > 0)));
    }

    /// <summary>所在作用域的主题、默认值变了：重新排版、重画。</summary>
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
            Font = Fonts.Get(outer.FontFamily.Name, pixels * 72 / 96, outer.Style);
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

    /// <summary>订阅了关闭就放上关闭按钮，退订了就拿掉。</summary>
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
        UpdateCloseSx();
    }

    /// <summary>关闭按钮的颜色同提示条的文字色；悬停的底色是它的 14%（状态层）：彩色提示条上不是一块灰。</summary>
    void UpdateCloseSx()
    {
        if (close is null) return;
        var foreground = Colors().Foreground;
        var sx = new ButtonSx { Foreground = foreground, Background = closeHovered ? Color.FromArgb((int)MathF.Round(foreground.A * 0.14f), foreground) : null };
        if (close.Sx != sx) close.Sx = sx;
    }

    /// <summary>样式 × 色调的颜色（同 Badge 的色表）。</summary>
    (Color? Background, Color Foreground, Color? Border) Colors() => BadgeRecipe.Colors(Variant.Value, Tone.Value, false, Context.Theme.Colors);

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => engine.Measure(proposedSize);

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var context = Context;
        var (background, foreground, border) = Colors();
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var line = context.Scale(Tokens.BorderWidth.Xs);
        var radius = (Radius.In(context.Theme.Radii) ?? 0) * Em;
        using (var path = Drawing.RoundedRectangle(new RectangleF(line / 2, line / 2, Width - line, Height - line), radius))
        {
            if (background is { } fill)
            {
                using var brush = new SolidBrush(fill);
                graphics.FillPath(brush, path);
            }
            if (border is { } stroke)
            {
                using var pen = new Pen(stroke, line);
                graphics.DrawPath(pen, path);
            }
        }
        var tinted = context with { ForeColor = foreground };
        if (!IsLoading && icon.Data is not null) icon.Paint(graphics, tinted);
        if (Title.Length > 0) title.Paint(graphics, tinted);
        // 说明是正文色（标题、图标随色调）
        if (Description.Length > 0) description.Paint(graphics, context with { ForeColor = context.Theme.Colors.Text });
        UpdateCloseSx();
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new AlertAccessibleObject(this);

    void IElementHost.InvalidateElement(Element element) => Invalidate(element.Bounds);

    void IElementHost.InvalidateLayout(Element element) => Relayout();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>读屏：一组内容，名称是标题，说明是描述；进行中时状态是忙。</summary>
    sealed class AlertAccessibleObject(AlertControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.Title : owner.AccessibleName; set => owner.AccessibleName = value; }

        public override string? Description => string.IsNullOrEmpty(owner.AccessibleDescription) ? owner.Description : owner.AccessibleDescription;

        public override AccessibleStates State => base.State | (owner.IsLoading ? AccessibleStates.Busy : 0);
    }

    /// <summary>
    /// 排版：[图标 1em（与第一行文字对齐）][文字区：标题、说明、子控件，间距 space.xs][操作（竖直居中）][关闭按钮（顶部，上下各收 space.xs）]，
    /// 间距 space.sm，四周 space.md。
    /// </summary>
    sealed class AlertEngine(AlertControl owner) : LayoutEngine
    {
        static Size SizeOf(Control control, int width) => control.AutoSize ? control.GetPreferredSize(new Size(width, 0)) : control.Size;

        int Run(int width, bool arrange)
        {
            var em = owner.Em;
            var (pad, gap, xs) = ((int)MathF.Round(Tokens.Space.Md * em), (int)MathF.Round(Tokens.Space.Sm * em), (int)MathF.Round(Tokens.Space.Xs * em));
            var context = owner.Context;
            var side = (int)MathF.Round(em);
            var hasIcon = owner.IsLoading || owner.icon.Data is not null;
            var x = pad + (hasIcon ? side + gap : 0);
            var right = width - pad;
            Size? closeSize = null;
            if (owner.close is { } close)
            {
                // 小一档（TS 的 relativeFontSizeStyles.sm：sm / md）
                close.TextSize = Tokens.FontSize.Sm / Tokens.FontSize.Md * em * 96f / owner.DeviceDpi;
                closeSize = SizeOf(close, 0);
                right -= closeSize.Value.Width + gap;
            }
            Size? actionSize = null;
            if (owner.Action is { Visible: true } action)
            {
                actionSize = SizeOf(action, 0);
                right -= actionSize.Value.Width + gap;
            }
            var inner = Math.Max(0, right - x);
            var y = pad;
            var first = true;
            int? firstLine = null;
            void Next(int height)
            {
                if (!first) y += xs;
                first = false;
                firstLine ??= height;
                y += height;
            }
            foreach (var text in new[] { owner.title, owner.description })
            {
                if (text.Content.Length == 0) continue;
                var size = text.Measure(context, new Size(inner, int.MaxValue));
                if (arrange) text.Bounds = new Rectangle(x, y + (first ? 0 : xs), size.Width, size.Height);
                // 图标对准第一行文字：只量一行的高度
                firstLine ??= text.Measure(context, new Size(int.MaxValue, int.MaxValue)).Height;
                Next(size.Height);
            }
            var extra = owner.Controls.Cast<Control>().Where(child => child != owner.Action && child != owner.close && child != owner.spinner && (child.Visible || !owner.Visible));
            foreach (var child in extra)
            {
                var size = SizeOf(child, inner);
                if (arrange) child.SetBounds(x, y + (first ? 0 : xs), Math.Min(size.Width, inner), size.Height);
                Next(size.Height);
            }
            var body = Math.Max(y - pad, side);
            var height = Math.Max(body, actionSize?.Height ?? 0) + 2 * pad;
            if (arrange)
            {
                var iconTop = pad + ((firstLine ?? side) - side) / 2;
                owner.icon.Size = side * 96f / owner.DeviceDpi;
                owner.icon.Bounds = new Rectangle(pad, iconTop, side, side);
                var spinnerSize = SizeOf(owner.spinner, 0);
                owner.spinner.SetBounds(pad + (side - spinnerSize.Width) / 2, iconTop + (side - spinnerSize.Height) / 2, spinnerSize.Width, spinnerSize.Height);
                var edge = width - pad;
                if (closeSize is { } c)
                {
                    owner.close!.SetBounds(edge - c.Width, pad - xs, c.Width, c.Height);
                    edge -= c.Width + gap;
                }
                if (actionSize is { } a) owner.Action!.SetBounds(edge - a.Width, (height - a.Height) / 2, a.Width, a.Height);
            }
            return height;
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
