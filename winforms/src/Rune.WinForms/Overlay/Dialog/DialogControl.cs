using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 模态对话框（vue-tsx-pro 的 Dialog 与内核 ModalCore）：居中显示，下面一层遮罩挡住窗体里的其他内容。
/// <code>
/// var dialog = new DialogControl { Title = "编辑资料", Description = "修改后立即对团队成员可见。", Content = form, Confirm = SaveAsync };
/// edit.Click += (_, _) => dialog.IsOpen = true;
/// </code>
/// 对话框放在哪里都行（自己不可见、不参与排版）：打开时盖住所在窗体的客户区。遮罩是半透明的分层窗口（overlay），面板用 <see cref="FloatingSurface"/>
/// （激活模式，xl 圆角、xl 阴影）；打开期间宿主窗体在 Win32 层禁用（同 ShowDialog 的模态），宿主移动、变大小时跟着。结构是标题行（H2 lg 与关闭按钮）、说明（muted）、
/// 正文（内容长时只滚正文）、页脚（<see cref="Footer"/>，靠结束边；写了 <see cref="Confirm"/> 时后面是"取消 / 确认"）。宽度档位 <see cref="DialogSize"/>（控件自己有 Size；默认 xs，不超出窗体，
/// 四周至少留 1em）。打开时焦点移到第一个能聚焦的控件（有关闭按钮时就是它），关上后还给打开前的控件。关闭按钮、取消、Esc、点遮罩（<see cref="Dismissible"/>）都先问
/// <see cref="BeforeClose"/>；确认的任务进行中按钮加载、关不掉，结果为 false 或失败时保持打开。<see cref="OpenChanged"/> 报告原因（代码里改 IsOpen 不报告）。
/// 读屏：对话框，以标题为名称、说明为帮助文字。
/// </summary>
[DefaultEvent(nameof(OpenChanged))]
public class DialogControl : Control
{
    readonly FloatingSurface surface;
    readonly LayeredWindow scrim = new();
    readonly DialogLayout layout;
    readonly H2Control title = new() { TextSize = TextSize.Lg };
    readonly CloseButtonControl close = new();
    readonly ParagraphControl description = new() { Tone = TextTone.Muted, Visible = false };
    readonly ScrollAreaControl body = new();
    readonly PaddedBody content = new();
    readonly FlexControl footer = new() { Justify = FlexJustify.End, Gap = Space.Sm, Wrap = true, Visible = false };
    readonly ButtonControl cancel = new() { Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral, Visible = false };
    readonly ButtonControl confirm = new() { Visible = false };
    Form? host;
    Control? returnFocus;
    bool open;
    bool confirming;

    /// <summary>新建：遮罩、面板（标题行、说明、正文、页脚）。</summary>
    public DialogControl()
    {
        // 自己不参与排版（同关着的原生 dialog 的 display: none）：对话框画在遮罩、面板两层窗口里
        Visible = false;
        SetStyle(ControlStyles.Selectable, false);
        surface = new FloatingSurface(this);
        surface.Form.AccessibleRole = AccessibleRole.Dialog;
        layout = new DialogLayout(this);
        body.Content = content;
        footer.Controls.AddRange([cancel, confirm]);
        layout.Controls.AddRange([title, close, description, body, footer]);
        layout.Dock = DockStyle.Fill;
        surface.Form.Controls.Add(layout);
        surface.Form.Escape += (_, _) => _ = RequestClose(OpenChangeReason.Escape);
        close.Click += (_, _) => _ = RequestClose(OpenChangeReason.Close);
        cancel.Click += (_, _) => _ = RequestClose(OpenChangeReason.Close);
        confirm.Click += (_, _) => _ = RunConfirm();
        scrim.MouseDown += (_, _) =>
        {
            if (Dismissible) _ = RequestClose(OpenChangeReason.Outside);
        };
        Sync();
    }

    /// <summary>开着。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => open; set => SetOpen(value, null); }

    /// <summary>标题，同时是对话框的读屏名称。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Title { get; set { field = value; Sync(); } }

    /// <summary>标题下的说明，读屏打开时一并念出。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Description { get; set { field = value; Sync(); } }

    /// <summary>正文（在可滚动的区域里，宽度随对话框）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content { get => content.Child; set => content.Child = value; }

    /// <summary>页脚的操作（靠结束边排列，确认按钮之前）；是布局面板时占满一行。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Footer
    {
        get;
        set
        {
            if (field is not null) footer.Controls.Remove(field);
            field = value;
            if (value is not null)
            {
                footer.Controls.Add(value);
                footer.Controls.SetChildIndex(value, 0);
                // 页脚内容是布局面板（一行两端分开的 Flex）时占满剩下的宽度；单个按钮照常靠结束边
                footer.SetGrow(value, value is Panel);
            }
            Sync();
        }
    }

    /// <summary>标题行右侧的关闭按钮，默认有。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Closable { get; set { field = value; Sync(); } } = true;

    /// <summary>点遮罩关闭，默认是；要用户明确选择时关掉（Esc 仍可关闭）。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Dismissible { get; set; } = true;

    /// <summary>用户要关闭时（关闭按钮、取消、Esc、点遮罩）先问它：参数是原因，结果为 false 时不关。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<OpenChangeReason, Task<bool>>? BeforeClose { get; set; }

    /// <summary>确认：写了就在页脚放"取消 / 确认"；任务进行中按钮加载、关不掉，结果为 false 或失败时保持打开，否则关闭。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Task<bool>>? Confirm { get; set { field = value; Sync(); } }

    /// <summary>确认按钮的文字；不写时取语言包（确定）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? ConfirmText { get; set { field = value; Sync(); } }

    /// <summary>取消按钮的文字；不写时取语言包（取消）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? CancelText { get; set { field = value; Sync(); } }

    /// <summary>确认按钮的色调，默认 accent；删除、驳回这类破坏性操作用 danger。</summary>
    [Category("Appearance")]
    public StatusTone Tone { get => field.Value is null ? StatusTone.Accent : field; set { field = value; Sync(); } }

    /// <summary>宽度档位（TS 的 size；控件自己有 Size），默认 xs（480）；full 占满窗体（四周留 1em）。</summary>
    [Category("Layout")]
    public DialogSize DialogSize { get => field.Value is null ? DialogSize.Xs : field; set { field = value; if (open) Present(); } }

    bool ShouldSerializeTone() => Tone != StatusTone.Accent;

    bool ShouldSerializeDialogSize() => DialogSize != DialogSize.Xs;

    /// <summary>用户开、关了（关闭按钮、取消、确认、Esc、点遮罩）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>放面板内容的窗口（测试用）。</summary>
    internal FloatingSurface Surface => surface;

    /// <summary>遮罩（测试用）。</summary>
    internal LayeredWindow Scrim => scrim;

    /// <summary>关闭、取消、确认按钮（测试用）。</summary>
    internal (CloseButtonControl Close, ButtonControl Cancel, ButtonControl Confirm) Buttons => (close, cancel, confirm);

    /// <summary>标题、说明、按钮跟着属性。</summary>
    void Sync()
    {
        var lang = ProviderControl.LangOf(this);
        title.Text = Title ?? "";
        description.Text = Description ?? "";
        description.Visible = !string.IsNullOrEmpty(Description);
        surface.Form.AccessibleName = Title;
        surface.Form.AccessibleDescription = Description;
        close.Visible = Closable;
        cancel.Text = CancelText ?? lang.Cancel;
        confirm.Text = ConfirmText ?? lang.Confirm;
        confirm.Tone = Tone;
        cancel.Visible = confirm.Visible = Confirm is not null;
        footer.Visible = Footer is not null || Confirm is not null;
        close.Enabled = cancel.Enabled = !confirming;
        confirm.IsLoading = confirming;
        layout.PerformLayout();
    }

    void SetOpen(bool next, OpenChangeReason? reason)
    {
        if (next == open) return;
        open = next;
        if (next) Open();
        else Dismiss();
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(next, known));
    }

    /// <summary>打开：记下焦点来源，禁用宿主窗体，盖上遮罩与面板。</summary>
    void Open()
    {
        host = FindForm();
        if (host is null) return;
        returnFocus = host.ActiveControl;
        host.Move += OnHostChanged;
        host.Resize += OnHostChanged;
        Present();
        // 同 ShowDialog：打开期间宿主窗体不接受输入（Win32 层禁用，不改 WinForms 的 Enabled：里面的控件不按不可用重画）
        EnableWindow(host.Handle, false);
    }

    /// <summary>关上：恢复宿主窗体，焦点还给打开前的控件。</summary>
    void Dismiss()
    {
        surface.Hide();
        scrim.Dismiss();
        if (host is null) return;
        host.Move -= OnHostChanged;
        host.Resize -= OnHostChanged;
        EnableWindow(host.Handle, true);
        host.Activate();
        returnFocus?.Focus();
        (host, returnFocus) = (null, null);
    }

    void OnHostChanged(object? sender, EventArgs e) => Present();

    /// <summary>遮罩铺满宿主窗体的客户区；面板宽度按档位、不超出（四周至少 1em），高度按内容、不超出，居中。</summary>
    void Present()
    {
        if (host is null || !host.IsHandleCreated) return;
        var context = ElementContext.Of(this);
        var area = host.RectangleToScreen(host.ClientRectangle);
        var bitmap = LayeredWindow.Canvas(area.Size);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(context.Theme.Colors.Overlay);
        if (scrim.Owner != host) scrim.Owner = host;
        scrim.Present(bitmap, area.Location, new Rectangle(area.Location, area.Size), null, TimeSpan.Zero);

        var em = context.Em;
        var border = FloatingSurface.Border(context);
        var margin = (int)MathF.Ceiling(em);
        var room = Math.Max(0, area.Width - 2 * margin);
        var width = DialogSize == DialogSize.Full ? room : Math.Min(room, (int)MathF.Round(context.Scale(DialogSize.Pixels)));
        var maxHeight = Math.Max(0, area.Height - 2 * margin);
        surface.Form.Font = Font;
        // 窗口显示之前里面的控件还没建句柄，量出的高度偏小：显示之后再量一次，变了就重新摆
        for (var pass = 0; pass < 2; pass++)
        {
            var height = DialogSize == DialogSize.Full ? maxHeight : Math.Min(maxHeight, layout.PreferredHeight(width - 2 * border) + 2 * border);
            var panel = new Rectangle(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
            if (pass > 0 && surface.Backdrop?.Panel == panel) break;
            surface.Show(panel, border, context, null, FloatingSurface.Elevation.Xl);
        }
    }

    /// <summary>用户要关闭：确认进行中不关；问过 BeforeClose 同意了才关。</summary>
    async Task RequestClose(OpenChangeReason reason)
    {
        if (confirming || !open) return;
        if (BeforeClose is { } ask && !await ask(reason)) return;
        SetOpen(false, reason);
    }

    /// <summary>确认：进行中按钮加载、关不掉；结果为 false 或失败时保持打开（调用方负责提示原因），否则关闭。</summary>
    async Task RunConfirm()
    {
        if (confirming || Confirm is not { } action) return;
        confirming = true;
        Sync();
        bool done;
        try
        {
            done = await action();
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceWarning($"对话框的 Confirm 失败，保持打开：{error}");
            done = false;
        }
        finally
        {
            confirming = false;
            Sync();
        }
        if (done) SetOpen(false, OpenChangeReason.Confirm);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (open && host is not null) EnableWindow(host.Handle, true);
            surface.Dispose();
            scrim.Dispose();
        }
        base.Dispose(disposing);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    static extern bool EnableWindow(IntPtr hWnd, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool enable);

    /// <summary>正文的内容层：四周留内边距（ScrollAreaControl 只管内容的大小与滚动，不认 Padding），宽度随对话框、高度按内容。</summary>
    sealed class PaddedBody : Control
    {
        public PaddedBody()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Control? Child
        {
            get;
            set
            {
                if (field is not null) Controls.Remove(field);
                field = value;
                if (value is not null) Controls.Add(value);
                PerformLayout();
            }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var width = proposedSize.Width > 0 ? proposedSize.Width : Width;
            var inner = Child?.GetPreferredSize(new Size(Math.Max(0, width - Padding.Horizontal), 0)) ?? Size.Empty;
            return new Size(width, inner.Height + Padding.Vertical);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (Child is not { } child) return;
            var width = Math.Max(0, ClientSize.Width - Padding.Horizontal);
            child.Bounds = new Rectangle(Padding.Left, Padding.Top, width, child.GetPreferredSize(new Size(width, 0)).Height);
        }
    }

    /// <summary>
    /// 面板里的排版：标题行（上、左右 space.xl；标题占满，关闭按钮上下各让出 space.xs、右边伸出 space.sm）、说明（上 space.xs）、
    /// 正文（上下 space.lg、左右 space.xl，占剩下的高度、内容长时滚动）、页脚（下、左右 space.xl）。
    /// </summary>
    sealed class DialogLayout(DialogControl owner) : Panel
    {
        float Em => owner.Font.SizeInPoints * DeviceDpi / 72f;

        int Px(float space) => (int)MathF.Round(space * Em);

        Padding BodyPadding => new(Px(Tokens.Space.Xl), Px(Tokens.Space.Lg), Px(Tokens.Space.Xl), Px(Tokens.Space.Lg));

        // 有没有说明、页脚按属性判断：窗口显示之前子控件的 Visible 一律是 false
        (int Header, int Description, int Footer) Bands(int width)
        {
            var (xs, xl) = (Px(Tokens.Space.Xs), Px(Tokens.Space.Xl));
            var inner = Math.Max(0, width - 2 * xl);
            var closeSize = owner.Closable ? owner.close.GetPreferredSize(Size.Empty) : Size.Empty;
            var titleHeight = owner.title.GetPreferredSize(new Size(Math.Max(0, inner - closeSize.Width), 0)).Height;
            var header = xl + Math.Max(titleHeight, closeSize.Height - 2 * xs);
            var description = !string.IsNullOrEmpty(owner.Description) ? xs + owner.description.GetPreferredSize(new Size(inner, 0)).Height : 0;
            var footer = owner.Footer is not null || owner.Confirm is not null ? owner.footer.GetPreferredSize(new Size(inner, 0)).Height + xl : 0;
            return (header, description, footer);
        }

        /// <summary>想要的高度：标题行、说明、正文（按内容）、页脚。</summary>
        public int PreferredHeight(int width)
        {
            var (header, description, footer) = Bands(width);
            var inner = Math.Max(0, width - 2 * Px(Tokens.Space.Xl));
            owner.content.Padding = BodyPadding;
            return header + description + owner.content.GetPreferredSize(new Size(width, 0)).Height + footer;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var (xs, sm, lg, xl) = (Px(Tokens.Space.Xs), Px(Tokens.Space.Sm), Px(Tokens.Space.Lg), Px(Tokens.Space.Xl));
            var width = ClientSize.Width;
            var inner = Math.Max(0, width - 2 * xl);
            var (header, description, footer) = Bands(width);
            var closeSize = owner.Closable ? owner.close.GetPreferredSize(Size.Empty) : Size.Empty;
            owner.close.Bounds = new Rectangle(width - xl + sm - closeSize.Width, xl - xs, closeSize.Width, closeSize.Height);
            owner.title.Bounds = new Rectangle(xl, xl, Math.Max(0, inner - (closeSize.Width > 0 ? closeSize.Width - sm + Px(Tokens.Space.Md) : 0)), header - xl);
            owner.description.Bounds = new Rectangle(xl, header + xs, inner, Math.Max(0, description - xs));
            var bodyTop = header + description;
            var bodyBottom = ClientSize.Height - footer;
            owner.body.Bounds = new Rectangle(0, bodyTop, width, Math.Max(0, bodyBottom - bodyTop));
            owner.content.Padding = BodyPadding;
            owner.footer.Bounds = new Rectangle(xl, bodyBottom, inner, Math.Max(0, footer - xl));
        }
    }
}

/// <summary>对话框的宽度档位（vue-tsx-pro 的 DialogSizes：容器宽度档位加 full）。</summary>
/// <param name="Value">取值（xs、sm、md、lg、xl、full）。</param>
[TypeConverter(typeof(VariantConverter<DialogSize>))]
public readonly record struct DialogSize(string Value) : IVariant<DialogSize>
{
    /// <summary>480（默认）：确认、简短表单。</summary>
    public static readonly DialogSize Xs = new("xs");

    /// <summary>640。</summary>
    public static readonly DialogSize Sm = new("sm");

    /// <summary>768。</summary>
    public static readonly DialogSize Md = new("md");

    /// <summary>1024。</summary>
    public static readonly DialogSize Lg = new("lg");

    /// <summary>1280。</summary>
    public static readonly DialogSize Xl = new("xl");

    /// <summary>占满窗体（四周留 1em）。</summary>
    public static readonly DialogSize Full = new("full");

    /// <inheritdoc />
    public static IReadOnlyList<DialogSize> Known { get; } = [Xs, Sm, Md, Lg, Xl, Full];

    /// <summary>宽度（设备无关像素，同容器档位）；full 时不用它。</summary>
    public float Pixels => Value switch
    {
        "sm" => Tokens.Size.Sm,
        "md" => Tokens.Size.Md,
        "lg" => Tokens.Size.Lg,
        "xl" => Tokens.Size.Xl,
        _ => Tokens.Size.Xs,
    };

    /// <inheritdoc />
    public static DialogSize Parse(string value)
    {
        var size = new DialogSize(value);
        Variants.WarnIfUnknown(size);
        return size;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
