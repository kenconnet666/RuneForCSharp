using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 放进窗体、设计器里用的 Text 家族的共同基类：属性都转给元素，文字内容就是控件的 <see cref="Control.Text"/>。
/// 控件自己有 <see cref="Control.Size"/>，字号档位在这里叫 <see cref="TextSize"/>。默认 AutoSize、不换行（同 Label）：
/// 要换行就设 MaximumSize 或关掉 AutoSize。属性等于组件的默认值时不写进设计器代码。
/// </summary>
/// <typeparam name="T">元素的类型（Text、Paragraph……）。</typeparam>
[ToolboxItem(false)]
public abstract class TextControlBase<T> : ElementControl<T> where T : Text, new()
{
    /// <summary>这个组件的默认值（Small 的字号、Strong 的字重……），判断要不要序列化用。</summary>
    static readonly T Defaults = new();

    /// <summary>新建：文字不能获得焦点。</summary>
    protected TextControlBase() => SetStyle(ControlStyles.Selectable, false);

    /// <summary>字号：档位（xs–xl）或像素数；不写时跟随字体（组件可能有自己的默认值，如 Small 是 xs）。</summary>
    [Category("Appearance")]
    [Description("字号：xs–xl 或像素数；空着时取组件的默认值或跟随字体。")]
    public TextSize TextSize { get => Element.Size; set => Element.Size = value; }

    /// <summary>字重档位。</summary>
    [Category("Appearance")]
    [Description("字重：light、normal、medium、semibold、bold；空着时取组件的默认值或跟随字体。")]
    public TextWeight Weight { get => Element.Weight; set => Element.Weight = value; }

    /// <summary>色调；不写时跟随 ForeColor。</summary>
    [Category("Appearance")]
    [Description("色调：neutral、muted、accent、danger、success、warning、info；空着时跟随 ForeColor。")]
    public TextTone Tone { get => Element.Tone; set => Element.Tone = value; }

    /// <summary>水平对齐（按书写方向）。</summary>
    [Category("Appearance")]
    [Description("水平对齐：Start、Center、End（按书写方向）；空着时同 Start。")]
    [DefaultValue(null)]
    public TextAlign? Align { get => Element.Align; set => Element.Align = value; }

    /// <summary>单行省略。</summary>
    [Category("Behavior")]
    [Description("单行显示，放不下时末尾省略。")]
    [DefaultValue(false)]
    public bool Truncate { get => Element.Truncate; set => Element.Truncate = value; }

    /// <summary>最多显示几行，超出省略。</summary>
    [Category("Behavior")]
    [Description("最多显示几行，超出省略；空着时不限。")]
    [DefaultValue(null)]
    public int? LineClamp { get => Element.LineClamp; set => Element.LineClamp = value; }

    /// <summary>换行方式。</summary>
    [Category("Behavior")]
    [Description("换行方式：Wrap、NoWrap（Balance、Pretty 在桌面上同 Wrap）。")]
    public TextWrap? Wrap { get => Element.Wrap; set => Element.Wrap = value; }

    /// <summary>数字等宽。</summary>
    [Category("Appearance")]
    [Description("数字等宽（金额、计数）。界面字体的数字默认等宽。")]
    [DefaultValue(false)]
    public bool Numeric { get => Element.Numeric; set => Element.Numeric = value; }

    /// <summary>等宽字体。</summary>
    [Category("Appearance")]
    [Description("等宽字体（主题的 fonts.mono）：编号、件号。")]
    [DefaultValue(false)]
    public bool Mono { get => Element.Mono; set => Element.Mono = value; }

    bool ShouldSerializeTextSize() => TextSize != Defaults.Size;

    void ResetTextSize() => TextSize = default;

    bool ShouldSerializeWeight() => Weight != Defaults.Weight;

    void ResetWeight() => Weight = default;

    bool ShouldSerializeTone() => Tone != default;

    void ResetTone() => Tone = default;

    bool ShouldSerializeWrap() => Wrap != Defaults.Wrap;

    void ResetWrap() => Wrap = null;

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Element.Content = Text;
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new TextAccessibleObject(this);

    /// <summary>没有另设 AccessibleRole 时是静态文字，名字是文字内容。</summary>
    sealed class TextAccessibleObject(Control owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => Owner is { AccessibleRole: not AccessibleRole.Default } control ? control.AccessibleRole : AccessibleRole.StaticText;
    }
}

/// <summary>正文文字（<see cref="Rune.WinForms.Text"/>）。</summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Text))]
[Description("正文文字：字号、字重、色调、对齐、截断。")]
public class TextControl : TextControlBase<Text>;

/// <summary>段落（<see cref="Rune.WinForms.Paragraph"/>）。</summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Text))]
[Description("段落：默认换行。")]
public class ParagraphControl : TextControlBase<Paragraph>;

/// <summary>重要的内容（<see cref="Rune.WinForms.Strong"/>）。</summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Text))]
[Description("重要的内容：默认 semibold。")]
public class StrongControl : TextControlBase<Strong>;

/// <summary>语气上的强调（<see cref="Rune.WinForms.Em"/>）。</summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Text))]
[Description("语气上的强调：斜体。")]
public class EmControl : TextControlBase<Em>;

/// <summary>附注、细则（<see cref="Rune.WinForms.Small"/>）。</summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Text))]
[Description("附注、版权、细则：默认 xs。")]
public class SmallControl : TextControlBase<Small>;
