using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;

namespace Rune.Avalonia;

/// <summary>
/// 组合输入（vue-tsx-pro 的 ControlGroup）：输入框、选择框、按钮连成一条，共用边框：币种 + 金额、协议 + 域名、搜索框 + 搜索按钮。
/// <code>
/// &lt;rune:Field Label="合同金额"&gt;
///   &lt;rune:ControlGroup&gt;
///     &lt;rune:Select ItemsSource="{Binding Currencies}" Value="{Binding Currency}" AutomationProperties.Name="币种" /&gt;
///     &lt;rune:NumberField Value="{Binding Amount}" AutomationProperties.Name="金额" /&gt;
///   &lt;/rune:ControlGroup&gt;
/// &lt;/rune:Field&gt;
/// </code>
/// 里面的输入框外壳与按钮家族去掉相邻一侧的圆角、边框叠在一起，聚焦的那个浮到上面，焦点环完整。默认占满一行，输入框分掉剩余宽度，
/// 选择框、按钮按内容宽；<see cref="Inline"/> 时按内容宽。只有文字的前后缀（单位、https://）用输入框自己的 Prefix / Suffix，不必拆成两个控件。
/// 放在 Field 里时组是 Field 的控件：标签、说明给组（读屏的名称与帮助文字），点标签聚焦第一个控件；里面的各个控件不再拿标签，各自写名称。
/// </summary>
public class ControlGroup : AttachedGroup, IFieldControl
{
    /// <summary>不占满一行（行内排列，按内容宽），默认占满。</summary>
    public static readonly StyledProperty<bool> InlineProperty = AvaloniaProperty.Register<ControlGroup, bool>(nameof(Inline));

    IDisposable? alignment;

    static ControlGroup() => AffectsMeasure<ControlGroup>(InlineProperty);

    /// <summary>不占满一行。</summary>
    public bool Inline { get => GetValue(InlineProperty); set => SetValue(InlineProperty, value); }

    /// <summary>输入框分掉剩余宽度（TS 的 attached 横排时 flex-grow: 1）；选择框、按钮按内容宽。</summary>
    protected override bool Grows(Control child) => !Inline && child is TextInputBase;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != InlineProperty) return;
        alignment?.Dispose();
        alignment = Inline ? SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left, BindingPriority.Template) : null;
    }

    void IFieldControl.FieldChanged()
    {
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ControlGroupAutomationPeer(this);

    /// <summary>读屏：一个组；没写名称时取所在字段的标签，帮助文字是字段的说明与错误（TS 的 aria-labelledby、aria-describedby 指向组）。</summary>
    sealed class ControlGroupAutomationPeer(ControlGroup owner) : AttachedGroupAutomationPeer(owner)
    {
        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null;

        protected override string? GetHelpTextCore() => Field.DescribedBy(owner, base.GetHelpTextCore());
    }
}
