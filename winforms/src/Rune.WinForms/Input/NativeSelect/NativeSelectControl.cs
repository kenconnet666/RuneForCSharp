using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Rune.WinForms;

/// <summary>
/// 原生下拉框（vue-tsx-pro 的 NativeSelect）：就是 Win32 的 ComboBox（DropDownList），键盘、读屏、列表都是系统的。
/// <code>
/// var city = new NativeSelectControl { Options = cities, PlaceholderText = "选择城市" };
/// city.ValueChanged += (_, _) => Save(city.Value);
/// field.Content = city;
/// </code>
/// 和 <see cref="SelectControl"/> 怎么选：选项要图标、说明、搜索、多选、远程加载，或者要同别的输入框一样的外壳，用 SelectControl；
/// 选项只是一串文字、想要系统下拉框的行为用它。
/// <list type="bullet">
/// <item><see cref="Options"/> 是 ListboxOption 或字符串（值与文字相同）；带 Group 的按组排，组名是一行选不了的标题（没有组的在前）。选项的说明、图标不显示。
/// 系统的下拉框没有不可选的行：落到组名、不可用的选项上时跳到同方向的下一项。</item>
/// <item>值是选项的 Value，没选时是 null，这时显示 <see cref="PlaceholderText"/>；值变了发 <see cref="ValueChanged"/>。不要直接改 Items：选项由 Options 生成。</item>
/// <item>只读（<see cref="IsReadOnly"/> 或所在字段只读）：能聚焦、能看，点击、按键、滚轮都换不了、不打开（同上游，系统的下拉框没有只读）。</item>
/// <item>外框、下拉箭头是系统画的：没有外观档位、圆角、前置图标；出错时在系统外框上描一圈 danger。字号跟着 <see cref="TextSize"/>。</item>
/// </list>
/// </summary>
[DefaultEvent(nameof(ValueChanged))]
public class NativeSelectControl : ComboBox, IFieldControl, IScopeAware
{
    const int WmPaint = 0x000F, WmKeyDown = 0x0100, WmChar = 0x0102, WmSysKeyDown = 0x0104, WmLButtonDown = 0x0201, WmLButtonDblClk = 0x0203, WmMouseWheel = 0x020A;

    readonly List<Row> rows = [];
    // 改选中项时不发 ValueChanged（是 Value、Options 变了引起的）
    bool syncing;
    // 上一次替使用方填的读屏名称、说明：使用方自己写了别的就不再覆盖
    string? fieldName;
    string? fieldDescription;

    /// <summary>新建：系统的 DropDownList，行由自己画（组名、缩进、占位）。</summary>
    public NativeSelectControl()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawVariable;
        // 列表高度由 DropDownHeight 定；设它时平台会改 IntegralHeight 并重建句柄，先在这里改掉
        IntegralHeight = false;
    }

    /// <summary>选项：ListboxOption 或字符串。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable? Options { get; set { field = value; Rebuild(); } }

    /// <summary>选中的值；没选时是 null。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            SelectValue();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>没选时显示的文字。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? PlaceholderText { get; set { field = value; Invalidate(); } }

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

    /// <summary>校验未通过；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsInvalid { get; set { field = value; Sync(); } }

    /// <summary>必填；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsRequired { get; set; }

    /// <summary>只读：能聚焦、能看，换不了、不打开；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsReadOnly { get; set { field = value; Sync(); } }

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    /// <summary>选中的值变了。</summary>
    public event EventHandler? ValueChanged;

    bool Invalid => IsInvalid ?? FieldControl.Of(this)?.Invalid ?? false;

    bool ReadOnly => IsReadOnly ?? FieldControl.ReadOnlyOf(this);

    float Em => this.EmPixels();

    void IFieldControl.FieldChanged() => Sync();

    void IScopeAware.ScopeChanged() => Sync();

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Sync();
    }

    /// <summary>按组生成行：组名是一行选不了的标题，组里的选项缩进一档。</summary>
    void Rebuild()
    {
        rows.Clear();
        foreach (var (group, options) in NativeSelectGroups.Grouped(NativeSelectGroups.Options(Options?.Cast<object?>())))
        {
            if (group is not null) rows.Add(new Row(group, null, RowKind.Group, true));
            rows.AddRange(options.Select(option => new Row(option.Label, option.Value, group is null ? RowKind.Option : RowKind.Grouped, option.IsDisabled)));
        }
        syncing = true;
        BeginUpdate();
        Items.Clear();
        Items.AddRange([.. rows]);
        EndUpdate();
        syncing = false;
        SelectValue();
        Sync();
    }

    /// <summary>按 Value 选中对应的行；没有这个值时什么都不选（显示占位）。</summary>
    void SelectValue()
    {
        syncing = true;
        SelectedIndex = rows.FindIndex(row => row.Kind != RowKind.Group && row.Value == Value);
        syncing = false;
        Invalidate();
    }

    /// <summary>选了别的行：组名、不可用的行跳到同方向的下一项（系统的下拉框没有不可选的行）；只读时退回原来的值。</summary>
    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        if (syncing)
        {
            base.OnSelectedIndexChanged(e);
            return;
        }
        var index = SelectedIndex;
        var current = rows.FindIndex(row => row.Kind != RowKind.Group && row.Value == Value);
        var target = ReadOnly ? current : index < 0 || rows[index].Selectable ? index : Nearest(index, index >= current ? 1 : -1) ?? current;
        if (target != index)
        {
            syncing = true;
            SelectedIndex = target;
            syncing = false;
        }
        base.OnSelectedIndexChanged(e);
        if (target >= 0) Value = rows[target].Value;
    }

    /// <summary>从 <paramref name="index"/> 往 <paramref name="step"/> 方向找能选的行，找不到再往回找。</summary>
    int? Nearest(int index, int step)
    {
        foreach (var direction in new[] { step, -step })
            for (var i = index + direction; i >= 0 && i < rows.Count; i += direction)
                if (rows[i].Selectable) return i;
        return null;
    }

    /// <summary>系统外框、颜色、行高跟着字号与主题；读屏的名称、说明取所在字段（使用方自己写了就不覆盖）。</summary>
    void Sync()
    {
        var colors = ProviderControl.ThemeOf(this).Colors;
        BackColor = ReadOnly ? colors.Fill : colors.Surface;
        ForeColor = colors.Text;
        // 改 ItemHeight 会建句柄，没放进窗口时建不了：等 OnHandleCreated 再设
        if (IsHandleCreated)
        {
            // 编辑框那一行的高度：外框之内撑到控件高度（系统外框上下各 3 像素）
            ItemHeight = Math.Max(1, (int)Math.Round(ControlHeight.Factor(TextSize) * Em) - 6);
            // 行高不一时系统不按行数算列表高度：先按行高合计给个大概（决定往下还是往上打开），打开以后由 FitList 定准
            DropDownHeight = ListHeight() + (int)Math.Ceiling(4 * DeviceDpi / 96f);
        }
        var field = FieldControl.Of(this);
        var name = field is not null && field.Owns(this) ? field.Label : null;
        if (AccessibleName is null || AccessibleName == fieldName) AccessibleName = fieldName = name;
        var description = FieldControl.DescribedBy(this);
        if (AccessibleDescription is null || AccessibleDescription == fieldDescription) AccessibleDescription = fieldDescription = description;
        Invalidate();
    }

    /// <summary>
    /// 同浏览器原生 select 的自然宽度：最宽的选项（或占位）加上左右留白、系统的下拉按钮与外框；高度是控件高度。
    /// 系统按 ItemHeight 定高，排版先于句柄时它还没跟上字号，这里直接按字号算。
    /// </summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var em = Em;
        var widest = rows.Select(row => row.Text).Append(PlaceholderText ?? "")
            .Max(text => Fonts.Measure(text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width);
        var width = widest + (int)Math.Ceiling(em * 1.75f) + SystemInformation.VerticalScrollBarWidth + 6;
        return new Size(Math.Max(width, MinimumSize.Width), Math.Max((int)Math.Round(ControlHeight.Factor(TextSize) * em), MinimumSize.Height));
    }

    /// <summary>列表里的行：组名按 sm 字的一行加上下留白，选项按控件高度。</summary>
    protected override void OnMeasureItem(MeasureItemEventArgs e)
    {
        base.OnMeasureItem(e);
        if (e.Index < 0 || e.Index >= rows.Count) return;
        e.ItemHeight = RowHeight(rows[e.Index]);
    }

    /// <summary>列表里全部的行高：放得下就全放，最多 320（同 Avalonia、WPF 的 MaxDropDownHeight）。</summary>
    int ListHeight() => Math.Max(1, Math.Min(rows.Sum(RowHeight), (int)Math.Round(320 * DeviceDpi / 96f)));

    /// <inheritdoc />
    protected override void OnDropDown(EventArgs e)
    {
        base.OnDropDown(e);
        BeginInvoke(FitList);
    }

    /// <summary>系统按 DropDownHeight 摆好列表后还会再收几个像素，行高不一时多出滚动条：打开以后按行高合计重设列表窗口（往上打开时底边不动）。</summary>
    void FitList()
    {
        var info = new ComboBoxInfo { Size = Marshal.SizeOf<ComboBoxInfo>() };
        if (!DroppedDown || !GetComboBoxInfo(Handle, ref info) || !GetWindowRect(info.List, out var list) || !GetClientRect(info.List, out var client)
            || !GetWindowRect(Handle, out var combo)) return;
        var height = ListHeight() + (list.Bottom - list.Top) - (client.Bottom - client.Top);
        var top = list.Top < combo.Top ? list.Bottom - height : list.Top;
        SetWindowPos(info.List, 0, list.Left, top, list.Right - list.Left, height, SwpNoZOrder | SwpNoActivate);
    }

    int RowHeight(Row row) => row.Kind == RowKind.Group
        ? (int)Math.Round(Tokens.FontSize.Sm * DeviceDpi / 96f * Tokens.LineHeight.Normal + Em * 0.75f)
        : (int)Math.Round(ControlHeight.Factor(TextSize) * Em);

    /// <summary>画一行：编辑框里是选中的文字或占位；列表里组名 sm、textMuted（medium 在 GDI 里同常规体），组里的选项缩进一档，悬停 fillHover、已选 accentText、不可用半透明。</summary>
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var colors = ProviderControl.ThemeOf(this).Colors;
        var edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
        var row = e.Index >= 0 && e.Index < rows.Count ? rows[e.Index] : null;
        var highlighted = !edit && (e.State & DrawItemState.Selected) != 0 && row is { Selectable: true };
        var background = edit ? BackColor : highlighted ? colors.FillHover : colors.SurfaceRaised;
        using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
        var em = Em;
        var font = Font;
        Color color;
        var left = em * 0.5f;
        if (row is null)
        {
            if (!edit) return;
            color = colors.TextMuted;
        }
        else if (row.Kind == RowKind.Group && !edit)
        {
            font = Fonts.Get(Font.FamilyName(), Tokens.FontSize.Sm * 72 / 96, FontStyle.Regular);
            color = colors.TextMuted;
        }
        else
        {
            color = !edit && row.Value == Value ? colors.AccentText : colors.Text;
            if (!row.Selectable && !edit) color = Drawing.Mix(background, color, Tokens.Opacity.Disabled);
            if (row.Kind == RowKind.Grouped && !edit) left = em * 1.25f;
        }
        // 编辑框在系统外框里，左边只留一点（系统的下拉框自己画文字时也只留这么多）
        if (edit) left = em * 0.25f;
        var text = row?.Text ?? PlaceholderText ?? "";
        var bounds = Rectangle.FromLTRB(e.Bounds.Left + (int)Math.Round(left), e.Bounds.Top, e.Bounds.Right - (int)Math.Round(em * 0.5f), e.Bounds.Bottom);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | (RightToLeft == RightToLeft.Yes ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left);
        TextRenderer.DrawText(e.Graphics, text, font, bounds, Enabled ? color : Drawing.Mix(BackColor, color, Tokens.Opacity.Disabled), flags);
        if (edit && (e.State & DrawItemState.Focus) != 0 && (e.State & DrawItemState.NoFocusRect) == 0) e.DrawFocusRectangle();
    }

    /// <summary>只读时吞掉会换值、会打开列表的输入；出错时在系统外框上描一圈 danger。</summary>
    protected override void WndProc(ref Message m)
    {
        if (ReadOnly && Blocked(m))
        {
            if (m.Msg is WmLButtonDown or WmLButtonDblClk) Focus();
            return;
        }
        base.WndProc(ref m);
        if (m.Msg == WmPaint && Invalid)
        {
            using var graphics = CreateGraphics();
            using var pen = new Pen(ProviderControl.ThemeOf(this).Colors.Danger);
            graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    static bool Blocked(Message m) => m.Msg switch
    {
        WmLButtonDown or WmLButtonDblClk or WmMouseWheel or WmChar => true,
        WmKeyDown => (Keys)(int)m.WParam is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.F4,
        WmSysKeyDown => (Keys)(int)m.WParam is Keys.Up or Keys.Down,
        _ => false,
    };

    const uint SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ComboBoxInfo
    {
        public int Size;
        public Rect Item, Button;
        public int State;
        public nint Combo, Edit, List;
    }

    [DllImport("user32.dll")]
    static extern bool GetComboBoxInfo(nint combo, ref ComboBoxInfo info);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    static extern bool GetClientRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    enum RowKind { Option, Group, Grouped }

    /// <summary>列表里的一行；ToString 是系统读屏、按字母跳转用的文字。</summary>
    sealed record Row(string Text, string? Value, RowKind Kind, bool IsDisabled)
    {
        public bool Selectable => Kind != RowKind.Group && !IsDisabled;

        public override string ToString() => Text;
    }
}
