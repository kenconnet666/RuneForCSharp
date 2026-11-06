using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>
/// 测试用的探针控件：外观在本程序集的 Themes/Generic.xaml，按组件库的约定写
/// （档位是主题样式的触发器、尺寸是 em 系数、部件按键取样式、背景带过渡）。
/// </summary>
public class ProbeButton : Control
{
    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.Register(nameof(Variant), typeof(string), typeof(ProbeButton), new PropertyMetadata("solid"));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(ProbeButton),
        new FrameworkPropertyMetadata(default(CornerRadius), null, Em.CoerceCornerRadius));

    static ProbeButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ProbeButton), new FrameworkPropertyMetadata(typeof(ProbeButton)));
        Em.Register(typeof(ProbeButton), CornerRadiusProperty);
    }

    public string Variant { get => (string)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>这个实例的 Style 换了几次（Provider 合成不应让子元素重复套样式）。</summary>
    public int StyleChanges { get; private set; }

    public T Part<T>(string name) where T : FrameworkElement => (T)Template.FindName(name, this);

    protected override void OnStyleChanged(Style oldStyle, Style newStyle)
    {
        base.OnStyleChanged(oldStyle, newStyle);
        StyleChanges++;
    }
}

/// <summary>探针控件的部件样式键。</summary>
public static class ProbeParts
{
    public static readonly ComponentResourceKey Label = new(typeof(ProbeButton), "Label");
}
