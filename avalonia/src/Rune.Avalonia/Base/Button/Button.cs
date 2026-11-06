using global::Avalonia;
using global::Avalonia.Layout;

namespace Rune.Avalonia;

/// <summary>
/// 按钮（vue-tsx-pro 的 Button）：外观 = 样式 × 色调 × 字号档位 × 圆角（<see cref="ButtonCore"/>），内部尺寸都是 em，随字号缩放。
/// <code>
/// &lt;rune:Button Icon="{x:Static rune:Lucide.Plus}"&gt;新建项目&lt;/rune:Button&gt;
/// &lt;rune:Button Variant="outline" Tone="neutral"&gt;取消&lt;/rune:Button&gt;
/// &lt;rune:Button IsLoading="True"&gt;保存&lt;/rune:Button&gt;   &lt;!-- 显示旋转指示，宽度不变，点击无效，焦点保留 --&gt;
/// </code>
/// 使用方要改单个按钮的颜色，在带伪类的选择器里设 Background。
/// 部件（模板里的名字，见 <see cref="ButtonParts"/>）：内容层、前置图标、后置图标、加载指示，用 <c>/template/</c> 样式改。
/// 效果同 TS（<see cref="ButtonBase"/>）：按下有波纹，颜色在 durations.sm 内过渡，键盘聚焦时外面一圈焦点环，加载指示防闪烁。
/// </summary>
public class Button : ButtonCore
{
    /// <summary>文字后的图标。</summary>
    public static readonly StyledProperty<IconData?> IconEndProperty = AvaloniaProperty.Register<Button, IconData?>(nameof(IconEnd));

    /// <summary>占满容器宽度。</summary>
    public static readonly StyledProperty<bool> FullWidthProperty = AvaloniaProperty.Register<Button, bool>(nameof(FullWidth));

    /// <summary>文字后的图标。</summary>
    public IconData? IconEnd { get => GetValue(IconEndProperty); set => SetValue(IconEndProperty, value); }

    /// <summary>占满容器宽度。</summary>
    public bool FullWidth { get => GetValue(FullWidthProperty); set => SetValue(FullWidthProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != FullWidthProperty) return;
        if (FullWidth) HorizontalAlignment = HorizontalAlignment.Stretch;
        else ClearValue(HorizontalAlignmentProperty);
    }
}

/// <summary>按钮的部件名：样式里用 <c>rune|Button /template/ rune|Icon#PART_Icon</c> 选中。</summary>
public static class ButtonParts
{
    /// <summary>内容层（包着图标与文字，TS 的 propsContent）。</summary>
    public const string Content = "PART_Content";

    /// <summary>前置图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>后置图标（TS 的 propsIconEnd）。</summary>
    public const string IconEnd = "PART_IconEnd";

    /// <summary>加载指示（TS 的 propsSpinner）。</summary>
    public const string Spinner = "PART_Spinner";
}
