using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 放进窗体、设计器里用的 <see cref="Icon"/>：属性都转给 <see cref="ElementControl{T}.Element"/>。
/// 控件自己有 <see cref="Control.Size"/>，图标的大小档位在这里叫 <see cref="IconSize"/>。
/// 不能获得焦点；读屏把它当作图片，名字取 AccessibleName（装饰性的图标不写）。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Data))]
[Description("Lucide 风格的描边图标：大小跟随字体，色调取当前主题。")]
public class IconControl : ElementControl<Icon>
{
    /// <summary>新建一个图标控件。</summary>
    public IconControl() => SetStyle(ControlStyles.Selectable, false);

    /// <summary>图标数据：属性窗口里按 Lucide 的名字选。</summary>
    [Category("Appearance")]
    [Description("图标：Lucide 的图标名，如 Search。")]
    [DefaultValue(null)]
    public IconData? Data { get => Element.Data; set => Element.Data = value; }

    /// <summary>大小：档位（xs–xl）或像素数；不写时跟随字体（1em）。</summary>
    [Category("Layout")]
    [Description("大小：xs–xl 或像素数；空着时跟随字体。")]
    public TextSize IconSize { get => Element.Size; set => Element.Size = value; }

    /// <summary>色调；不写时跟随 ForeColor。</summary>
    [Category("Appearance")]
    [Description("色调：neutral、muted、accent、danger、success、warning、info；空着时跟随 ForeColor。")]
    public TextTone Tone { get => Element.Tone; set => Element.Tone = value; }

    /// <summary>线宽（24 格画布上的单位）。</summary>
    [Category("Appearance")]
    [Description("线宽（24 格画布上的单位）。")]
    [DefaultValue(Tokens.StrokeWidth.Md)]
    public float StrokeWidth { get => Element.StrokeWidth; set => Element.StrokeWidth = value; }

    /// <summary>从右到左排版时水平翻转。</summary>
    [Category("Appearance")]
    [Description("从右到左排版时水平翻转（箭头这类有方向的图标）。")]
    [DefaultValue(false)]
    public bool FlipInRtl { get => Element.FlipInRtl; set => Element.FlipInRtl = value; }

    bool ShouldSerializeIconSize() => IconSize != default;

    void ResetIconSize() => IconSize = default;

    bool ShouldSerializeTone() => Tone != default;

    void ResetTone() => Tone = default;

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new IconAccessibleObject(this);

    /// <summary>
    /// 没有另设 AccessibleRole 时是图片。名字只取 AccessibleName：系统会给没名字的窗口借用前面那个 Label 的文字，
    /// 装饰性的图标就会被读成上一行的内容（inspect uia 查出来的）。图片不能获得焦点，去掉窗口带的 Focusable。
    /// </summary>
    sealed class IconAccessibleObject(IconControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => owner.AccessibleRole is AccessibleRole.Default ? AccessibleRole.Graphic : owner.AccessibleRole;

        public override string? Name
        {
            get => owner.AccessibleName;
            set => owner.AccessibleName = value;
        }

        public override AccessibleStates State => base.State & ~AccessibleStates.Focusable;
    }
}
