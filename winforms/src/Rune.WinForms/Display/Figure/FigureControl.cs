using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 带说明的插图（vue-tsx-pro 的 Figure）：图片、图表、代码、表格下面（或上面）配一行说明，读屏把说明当作这块内容的名称。
/// <code>
/// var figure = new FigureControl { Caption = "图 1：近 12 个月的采购额" };
/// figure.Controls.Add(new SparklineControl { Data = purchases });
/// </code>
/// 是一个竖排的 FlexControl：内容是放进去的子控件，说明是最后（CaptionSide=top 时最前）的一行，次要色、sm，和内容隔 space.sm。
/// 表格的说明习惯放上面。内容与说明都跟着 <see cref="FigureAlign"/> 对齐（start 时内容拉满宽度）。
/// </summary>
[ToolboxItem(true)]
[Description("带说明的插图：内容加一行说明。")]
public class FigureControl : FlexControl
{
    readonly TextControl caption = new() { TextSize = TextSize.Sm, Tone = TextTone.Muted, Visible = false };

    /// <summary>新建：竖排、间距 sm，读屏里是一个组。</summary>
    public FigureControl()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Sm;
        Align = FlexAlign.Stretch;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
        Controls.Add(caption);
    }

    /// <summary>说明；不写时就是内容本身。读屏把它当作这块内容的名称（没写 AccessibleName 时）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Caption
    {
        get;
        set
        {
            field = value;
            caption.Text = value ?? "";
            caption.Visible = !string.IsNullOrEmpty(value);
            AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        }
    }

    /// <summary>说明在内容的上面还是下面，默认 bottom。</summary>
    [Category("Appearance")]
    public FigureCaptionSide CaptionSide { get; set { field = value; PlaceCaption(); } } = FigureCaptionSide.Bottom;

    /// <summary>内容与说明的对齐，默认 start（内容拉满宽度）。</summary>
    [Category("Appearance")]
    public FigureAlign FigureAlign
    {
        get;
        set
        {
            field = value;
            Align = value == FigureAlign.Center ? FlexAlign.Center : value == FigureAlign.End ? FlexAlign.End : FlexAlign.Stretch;
            caption.Align = value == FigureAlign.Center ? TextAlign.Center : value == FigureAlign.End ? TextAlign.End : TextAlign.Start;
        }
    } = FigureAlign.Start;

    /// <summary>说明那一行的部件属性（TS 的 propsCaption）：字号、色调、字重等，盖过默认的 sm、次要色。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsCaption
    {
        get;
        set
        {
            field = value;
            (caption.TextSize, caption.Tone) = (TextSize.Sm, TextTone.Muted);
            value?.ApplyTo(caption.Element);
        }
    }

    bool ShouldSerializeCaptionSide() => CaptionSide != FigureCaptionSide.Bottom;

    void ResetCaptionSide() => CaptionSide = FigureCaptionSide.Bottom;

    bool ShouldSerializeFigureAlign() => FigureAlign != FigureAlign.Start;

    void ResetFigureAlign() => FigureAlign = FigureAlign.Start;

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control != caption) PlaceCaption();
    }

    /// <summary>说明排在最后（上面时排最前）：FlexControl 按 Controls 的顺序排，后加的内容会排到说明后面。</summary>
    void PlaceCaption()
    {
        if (caption is null) return;
        Controls.SetChildIndex(caption, CaptionSide.IsTop ? 0 : Controls.Count - 1);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new FigureAccessibleObject(this);

    /// <summary>读屏：没写名称时取说明（TS 的 figcaption 给 figure 命名）。</summary>
    sealed class FigureAccessibleObject(FigureControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name
        {
            get => base.Name is { Length: > 0 } name ? name : owner.Caption;
            set => base.Name = value;
        }
    }
}
