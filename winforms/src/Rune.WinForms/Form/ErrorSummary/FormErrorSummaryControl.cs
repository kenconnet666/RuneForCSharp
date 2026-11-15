using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 错误汇总（vue-tsx-pro 的 FormErrorSummary）：提交没通过时在表单顶上列出各项错误（"有 3 项需要修改"），每项是链接，点了跳到那个字段的控件。
/// <code>
/// column.Controls.AddRange([new FormErrorSummaryControl(), new FieldControl { Path = "Email", Label = "邮箱", Content = new TextFieldControl() }]);
/// </code>
/// 放在 FormControl 里（通常是第一个）；提交失败时表单把焦点移到它上面，读屏念出有几项、各是什么（GOV.UK 的 Error summary）。
/// 只列显示着的错误（失焦过、提交过的字段），按字段登记的顺序；改对一项它就从列表里消失，全改对了汇总收起。整份表单的错误排在最前，不带链接。
/// 外观：dangerSoft 底、dangerBorder 细边（聚焦时焦点环色）、md 圆角、四周 md；标题是警示图标加文字（medium 字重、dangerText），列表项是 danger 色调的链接。
/// </summary>
[ToolboxItem(true)]
[Description("错误汇总：提交没通过时列出各项错误，点了跳到字段。")]
public class FormErrorSummaryControl : FlexControl, IFormPart
{
    readonly IconControl icon = new() { Data = AlertControl.IconFor(StatusTone.Danger), Tone = TextTone.Danger };
    readonly TextControl titleText = new() { Weight = TextWeight.Medium, Tone = TextTone.Danger };
    readonly FlexControl title = new() { Gap = Space.Xs, Align = FlexAlign.Center, AutoSize = true };
    readonly UnorderedListControl list = new() { AutoSize = true };
    IFormController? form;

    /// <summary>新建：没有要显示的错误时收起。</summary>
    public FormErrorSummaryControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = false;
        AutoSize = true;
        Direction = FlexDirection.Column;
        Gap = Space.Sm;
        Visible = false;
        AccessibleRole = AccessibleRole.Grouping;
        title.Controls.AddRange([icon, titleText]);
        Controls.AddRange([title, list]);
    }

    /// <summary>标题；不写时取语言包（有 N 项需要修改）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<int, string>? Title { get; set { field = value; Rebuild(); } }

    /// <summary>列表里每一项的内容（测试用）：字段的错误是链接，整份表单的错误是文字。</summary>
    internal IEnumerable<Control> Items => list.Controls.Cast<Control>().Select(item => item.Controls.Cast<Control>().First());

    /// <summary>标题的文字（测试用）。</summary>
    internal string TitleText => titleText.Text;

    float Em => this.EmPixels();

    /// <summary>接上所在的表单（换了位置、表单换了控制器时）。</summary>
    void IFormPart.Connect() => Connect();

    void Connect()
    {
        var next = FormControl.Of(this)?.Controller;
        if (next == form) return;
        if (form is not null) form.Changed -= OnFormChanged;
        form = next;
        if (form is not null) form.Changed += OnFormChanged;
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Connect();
    }

    /// <summary>外层的面板后来才放进表单时，ParentChanged 不会到这里：建句柄（显示出来）时再接一次。</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Connect();
    }

    void OnFormChanged(object? sender, EventArgs e) => Rebuild();

    /// <summary>显示着的错误：整份表单的在前，其余按字段登记的顺序。</summary>
    IReadOnlyList<(string Label, string Message, FormFieldInfo? Field)> Errors()
    {
        if (form is not { SubmitCount: > 0 }) return [];
        var fields = form.Fields
            .Select(entry => (entry.Value.Label, Message: form.VisibleError(entry.Key), Field: (FormFieldInfo?)entry.Value))
            .Where(entry => entry.Message is not null)
            .Select(entry => (entry.Label, entry.Message!, entry.Field));
        return form.FormError is { } whole ? [("", whole, null), .. fields] : [.. fields];
    }

    void Rebuild()
    {
        var errors = Errors();
        Visible = errors.Count > 0;
        foreach (var item in list.Controls.Cast<Control>().ToList()) item.Dispose();
        if (errors.Count == 0) return;
        var lang = ProviderControl.LangOf(this);
        titleText.Text = Title?.Invoke(errors.Count) ?? lang.FormErrorSummary(errors.Count);
        AccessibleName = titleText.Text;
        foreach (var (label, message, field) in errors)
        {
            Control content;
            if (field is null) content = new TextControl { Text = message };
            else
            {
                var link = new LinkControl { Tone = TextTone.Danger, Text = label.Length > 0 ? lang.FormErrorItem(label, message) : message };
                link.Click += (_, _) => field.Focus();
                content = link;
            }
            var item = new ListItemControl();
            item.Controls.Add(content);
            list.Controls.Add(item);
        }
        PerformLayout();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyPadding();
    }

    /// <inheritdoc />
    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        ApplyPadding();
    }

    /// <summary>四周 md（随字号与 DPI）。</summary>
    void ApplyPadding() => Padding = new Padding((int)MathF.Round(Tokens.Space.Md * Em));

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <summary>dangerSoft 底、md 圆角的细边；聚焦时边换成焦点环色。</summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        var theme = ProviderControl.ThemeOf(this);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var line = Focused ? LogicalToDeviceUnits(2) : 1;
        using var shape = Drawing.RoundedRectangle(new RectangleF(line / 2f, line / 2f, Width - line, Height - line), theme.Radii.Md * Em);
        using (var fill = new SolidBrush(theme.Colors.DangerSoft)) e.Graphics.FillPath(fill, shape);
        using var pen = new Pen(Focused ? theme.Colors.FocusRing : theme.Colors.DangerBorder, line);
        e.Graphics.DrawPath(pen, shape);
    }
}

/// <summary>放进 <see cref="FormControl"/> 里、要接上表单控制器的控件：字段与错误汇总。</summary>
interface IFormPart
{
    /// <summary>接上（或断开）所在的表单。</summary>
    void Connect();
}
