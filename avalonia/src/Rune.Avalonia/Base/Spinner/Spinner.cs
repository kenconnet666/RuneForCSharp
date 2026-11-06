using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Automation.Peers;
using global::Avalonia.Controls.Primitives;

namespace Rune.Avalonia;

/// <summary>
/// 加载中的旋转指示（vue-tsx-pro 的 Spinner）：Lucide 的 LoaderCircle 加旋转，默认跟随所在文字的大小与颜色。
/// <code>
/// &lt;rune:Spinner /&gt;                       &lt;!-- 装饰性：旁边已经有"加载中"之类的文字 --&gt;
/// &lt;rune:Spinner Label="正在加载订单" /&gt;   &lt;!-- 单独出现：读屏当作进度条，念出 Label --&gt;
/// </code>
/// 图标是部件 <see cref="SpinnerParts.Icon"/>（TS 的 propsIcon）：图标、大小、旋转都写在主题的嵌套样式里，
/// 使用方用 <c>rune|Spinner /template/ rune|Icon#PART_Icon</c> 的样式改（DESIGN §7）。
/// </summary>
public class Spinner : TemplatedControl
{
    /// <summary>读屏念的说明；给了就是进度条（TS 的 role="status"），不给就是装饰性的（对读屏隐藏）。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Spinner, string?>(nameof(Label));

    /// <summary>大小：档位（xs–xl）或像素数；不写时 1em，跟随所在文字。主题把它转给图标部件。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Spinner, TextSize>(nameof(Size));

    /// <summary>读屏念的说明。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>大小。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SpinnerAutomationPeer(this);

    /// <summary>有 Label 时是进度条（不确定进度），名字是 Label；没有时是装饰，不进控件树与内容树。</summary>
    sealed class SpinnerAutomationPeer(Spinner owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override string? GetNameCore() => ((Spinner)Owner).Label is { Length: > 0 } label ? label : base.GetNameCore();

        protected override bool IsControlElementCore() => !string.IsNullOrEmpty(GetName());

        protected override bool IsContentElementCore() => IsControlElementCore();
    }
}

/// <summary>Spinner 的部件名：样式里用 <c>#PART_Icon</c> 选中它。</summary>
public static class SpinnerParts
{
    /// <summary>旋转的图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";
}
