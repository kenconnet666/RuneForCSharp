using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Rune.WinForms;

/// <summary>
/// 文字输入的外壳（vue-tsx-pro 的 control：inputRecipe、controlParts）：<see cref="TextFieldControl"/>、<see cref="TextareaControl"/> 共用。
/// 外壳（边框、悬停、聚焦时压住边框的焦点环、出错、只读、不可用）里放无边框的平台 TextBox，可以一键清空、显示字数。
/// <see cref="Control.Text"/> 是输入的值，<see cref="PlaceholderText"/>、<see cref="MaxLength"/>、<see cref="IsReadOnly"/> 转给里面的 TextBox。
/// 外观：<see cref="Variant"/>（surface / soft）、<see cref="TextSize"/>、<see cref="Radius"/>（默认 md）。放进 FieldControl 时以标签为名称，沿用字段的出错、只读。
/// 派生类把自己的输入框交给构造函数，再按自己的顺序把输入框、<see cref="ClearButton"/>、<see cref="Count"/> 与别的部件加进 Controls。
/// </summary>
public abstract class TextInputControl : FlexControl, IFieldControl
{
    readonly TextBox input;
    readonly ClearButtonControl clear = new() { Radius = Radius.Full, Visible = false };
    readonly CharacterCountControl count = new() { Visible = false };
    bool hovered;

    /// <summary>新建：外壳控件高度、左右 space.md，部件之间隔 space.sm。</summary>
    /// <param name="input">里面的输入框（无边框；单行或多行由派生类定）。</param>
    protected TextInputControl(TextBox input)
    {
        this.input = input;
        input.BorderStyle = BorderStyle.None;
        Align = FlexAlign.Center;
        Gap = Space.Sm;
        AutoSize = true;
        Cursor = Cursors.IBeam;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        input.TextChanged += (_, _) =>
        {
            Sync();
            OnTextChanged(EventArgs.Empty);
        };
        input.GotFocus += (_, _) => Invalidate();
        input.LostFocus += (_, _) => Invalidate();
        clear.Click += (_, _) =>
        {
            Text = "";
            input.Focus();
        };
        ControlAdded += (_, e) =>
        {
            e.Control!.MouseEnter += (_, _) => SetHovered(true);
            e.Control.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
            // 点在不能聚焦的部件上（图标、前后缀、字数）同点在空白处：焦点交给输入框；按钮这类能聚焦的照常（上游 1cc605e3）
            var part = e.Control;
            part.MouseDown += (_, _) =>
            {
                if (!part.CanSelect) input.Focus();
            };
        };
    }

    /// <summary>输入的值。</summary>
    [AllowNull]
    public override string Text { get => input.Text; set => input.Text = value ?? ""; }

    /// <summary>占位文字：没有内容时显示。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string PlaceholderText { get => input.PlaceholderText; set => input.PlaceholderText = value; }

    /// <summary>最多几个字符；0 是不限。显示字数时它是上限。</summary>
    [Category("Behavior")]
    [DefaultValue(32767)]
    public int MaxLength
    {
        get => input.MaxLength;
        set
        {
            input.MaxLength = value;
            Sync();
        }
    }

    /// <summary>只读：可以选中、复制，不能修改；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsReadOnly
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>校验未通过：红色描边与焦点环；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsInvalid
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>必填；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsRequired { get; set; }

    /// <summary>样式，默认 surface。</summary>
    [Category("Appearance")]
    public ControlVariant Variant
    {
        get => field.Value is null ? ControlVariant.Surface : field;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>字号档位；不写时跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get;
        set
        {
            field = value;
            if (value.Pixels is { } pixels) Font = Fonts.Get(Font.FontFamily.Name, pixels * 72 / 96, Font.Style);
            else ResetFont();
        }
    }

    /// <summary>圆角，默认 md；搜索框常用 full（胶囊形）。</summary>
    [Category("Appearance")]
    public Radius Radius
    {
        get => field.Value is null ? Radius.Md : field;
        set
        {
            field = value;
            Invalidate();
        }
    }

    /// <summary>有内容、可编辑时显示清空按钮。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Clearable
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>显示字数计数；上限取 MaxLength（32767 视为不限），超出时用危险色。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool ShowCount
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    bool ShouldSerializeVariant() => Variant != ControlVariant.Surface;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    bool ShouldSerializeRadius() => Radius != Radius.Md;

    /// <summary>里面的输入框。</summary>
    internal TextBox Input => input;

    /// <summary>清空按钮。</summary>
    internal ClearButtonControl ClearButton => clear;

    /// <summary>字数计数。</summary>
    internal CharacterCountControl Count => count;

    /// <summary>出错：自己写了的优先，其次所在字段的。</summary>
    internal bool Invalid => IsInvalid ?? FieldControl.Of(this)?.Invalid ?? false;

    /// <summary>只读：自己写了的优先，其次所在字段的。</summary>
    protected bool ReadOnly => IsReadOnly ?? FieldControl.Of(this)?.IsReadOnly ?? false;

    /// <summary>1em 是多少设备像素。</summary>
    protected float Em => Font.SizeInPoints * DeviceDpi / 72f;

    void IFieldControl.FieldChanged() => Sync();

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

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Sync();
    }

    /// <summary>内边距、控件高度、只读、清空按钮、字数；输入框的底色跟着外壳（平台的 TextBox 不能透明）。派生类接着同步自己的部件。</summary>
    protected virtual void Sync()
    {
        ControlShell.Layout(this, Em);
        ControlShell.FitInnerButton(clear, Em);
        input.ReadOnly = ReadOnly;
        var colors = ProviderControl.ThemeOf(this).Colors;
        input.BackColor = ControlShell.Fill(colors, Variant, ReadOnly);
        input.ForeColor = colors.Text;
        clear.Visible = Clearable && Enabled && !ReadOnly && Text.Length > 0;
        count.Visible = ShowCount;
        count.Count = Text.Length;
        count.Max = MaxLength is > 0 and < 32767 ? MaxLength : null;
        Invalidate();
    }

    /// <summary>不小于控件高度：放在 FieldControl 这类不是 FlexControl 的容器里时，它按想要的大小排，不看最小尺寸。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize);
        return new Size(Math.Max(size.Width, MinimumSize.Width), Math.Max(size.Height, MinimumSize.Height));
    }

    void SetHovered(bool value)
    {
        if (hovered == value) return;
        hovered = value;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        SetHovered(true);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
    }

    /// <summary>点在外壳的空白处（内边距）：焦点交给输入框。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        input.Focus();
    }

    /// <summary>外壳（<see cref="ControlShell"/>）；不可用时整体变淡由文字颜色体现。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ControlShell.Paint(e.Graphics, this, Em, Radius, Variant, (ReadOnly, Invalid, hovered, ContainsFocus));
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new FieldAccessibleObject(this);

    sealed class FieldAccessibleObject(TextInputControl owner) : ControlAccessibleObject(owner)
    {
        // 平台默认的名称取 Text，这里的 Text 是输入的值：只认 AccessibleName，没有时是字段的标签
        public override string? Name => owner.AccessibleName is { Length: > 0 } name ? name : FieldControl.Of(owner) is { } scope && scope.Owns(owner) ? scope.Label : null;

        public override AccessibleRole Role => AccessibleRole.Text;

        public override string? Value { get => owner.Text; set => owner.Text = value ?? ""; }
    }
}
