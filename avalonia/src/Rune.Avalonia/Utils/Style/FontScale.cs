using global::Avalonia;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Data;
using global::Avalonia.Data.Converters;
using global::Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;

namespace Rune.Avalonia;

/// <summary>
/// 字号是外面的几倍（CSS 的 <c>font-size: 0.85em</c>）：Avalonia 的字号是像素数、按像素继承，所以绑定到外面那个元素
/// （继承来的）字号再乘上倍数，按模板优先级（使用方带类或伪类的样式、本地值盖得过）。Kbd、Code 用。
/// </summary>
static class FontScale
{
    /// <summary>把 <paramref name="element"/> 的字号绑定成 <paramref name="parent"/> 的字号 × <paramref name="scale"/>；挂到树上时调，摘下时释放。</summary>
    public static IDisposable Bind(StyledElement element, AvaloniaObject parent, double scale) =>
        element.Bind(TextElement.FontSizeProperty, new CompiledBinding(new CompiledBindingPathBuilder().Property(TextElement.FontSizeProperty, PropertyInfoAccessorFactory.CreateAvaloniaPropertyAccessor).Build())
        {
            Source = parent,
            Converter = new FuncValueConverter<double, double>(size => size * scale),
            Priority = BindingPriority.Template,
        });
}
