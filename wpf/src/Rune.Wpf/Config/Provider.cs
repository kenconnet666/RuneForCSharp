using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf;

/// <summary>
/// 样式与默认值的作用域（对应 vue-tsx-pro 的 Provider）：Styles 里的样式按资源键叠在外层同键样式之上，
/// 内层只写要改的部分，其余沿用外层。WPF 原生的嵌套隐式样式是内层整个替换外层，这里把每一层串成 BasedOn 链。
/// <code>
/// &lt;rune:Provider&gt;
///   &lt;rune:Provider.Styles&gt;
///     &lt;ResourceDictionary&gt;
///       &lt;Style TargetType="rune:Button"&gt;&lt;Setter Property="Variant" Value="Outline" /&gt;&lt;/Style&gt;   &lt;!-- 默认值 --&gt;
///       &lt;Style x:Key="{x:Static rune:ButtonParts.Label}" TargetType="TextBlock"&gt;…&lt;/Style&gt;          &lt;!-- 部件样式 --&gt;
///     &lt;/ResourceDictionary&gt;
///   &lt;/rune:Provider.Styles&gt;
///   …
/// &lt;/rune:Provider&gt;
/// </code>
/// <see cref="StylesProperty"/> 是附加属性，所以不必包一层 Provider：任何 FrameworkElement 都能当一层
/// （单个实例的部件样式，对应 TS 的 <c>propsXxx</c>），合成规则与 Provider 完全一样，叠在最近的外层上：
/// <code>
/// &lt;rune:Progress Value="120"&gt;
///   &lt;rune:Provider.Styles&gt;
///     &lt;ResourceDictionary&gt;&lt;Style x:Key="{x:Static rune:ProgressParts.ValueLabel}" TargetType="rune:Text"&gt;…&lt;/Style&gt;&lt;/ResourceDictionary&gt;
///   &lt;/rune:Provider.Styles&gt;
/// &lt;/rune:Progress&gt;
/// </code>
/// 合并顺序：主题样式 &lt; 应用资源 &lt; 外层 Provider（或外层元素的这一层）&lt; 内层 &lt; 元素自己的 Style 与本地值。
/// 类型键（隐式样式）不叠主题样式：主题样式由 DefaultStyleKey 另外生效，叠进来会把库的触发器抬到“样式”优先级；
/// 部件键叠到主题里的部件样式之上：部件没有“默认样式”这一层，主题里的就是基础。
/// 语言包（<see cref="LangProperty"/>）沿元素树继承，也能直接挂在任何元素上：<c>&lt;Window rune:Provider.Lang="{x:Static rune:Lang.EnUs}"&gt;</c>。
/// 合成在 <see cref="ProviderLayer"/>，Provider 只是自己当宿主的 Decorator。
/// </summary>
public class Provider : Decorator
{
    /// <summary>语言包：沿元素树继承；哪里都没写时按界面文化取（<see cref="Rune.Wpf.Lang.ForCulture"/>）。</summary>
    public static readonly DependencyProperty LangProperty = DependencyProperty.RegisterAttached(
        "Lang", typeof(Lang), typeof(Provider), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>工作日历（上游 createUi / Provider 的 workCalendar）：沿元素树继承；哪里都没写时只按周末（<see cref="Rune.Wpf.WorkCalendar.Weekdays"/>）。</summary>
    public static readonly DependencyProperty WorkCalendarProperty = DependencyProperty.RegisterAttached(
        "WorkCalendar", typeof(WorkCalendar), typeof(Provider), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>
    /// 这一层的样式：类型键（隐式样式）用于组件的默认值与外观，<c>x:Key</c> 为部件键的用于部件。
    /// 只写要改的 setter 与触发器，不写 BasedOn。Provider 本身与任何 FrameworkElement 都能设。
    /// </summary>
    public static readonly DependencyProperty StylesProperty = DependencyProperty.RegisterAttached(
        "Styles", typeof(ResourceDictionary), typeof(Provider), new PropertyMetadata(null, ProviderLayer.OnStylesChanged));

    /// <summary>新建一层，Styles 先是空的。</summary>
    public Provider() => Styles = new ResourceDictionary();

    /// <inheritdoc cref="StylesProperty" />
    public ResourceDictionary Styles { get => GetStyles(this)!; set => SetStyles(this, value); }

    /// <summary>这片区域的语言包。</summary>
    public Lang? Lang { get => (Lang?)GetValue(LangProperty); set => SetValue(LangProperty, value); }

    /// <summary>这片区域的工作日历。</summary>
    public WorkCalendar? WorkCalendar { get => (WorkCalendar?)GetValue(WorkCalendarProperty); set => SetValue(WorkCalendarProperty, value); }

    /// <summary>某个元素所在作用域的工作日历；哪里都没写时只按周末。</summary>
    /// <param name="target">元素。</param>
    public static WorkCalendar GetWorkCalendar(DependencyObject target) => (WorkCalendar?)target.GetValue(WorkCalendarProperty) ?? Rune.Wpf.WorkCalendar.Weekdays;

    /// <summary>给一个元素（及它里面的）设工作日历。</summary>
    /// <param name="target">元素。</param>
    /// <param name="value">工作日历；null 时沿用外面的。</param>
    public static void SetWorkCalendar(DependencyObject target, WorkCalendar? value) => target.SetValue(WorkCalendarProperty, value);

    /// <summary>合成时实际生成了几个 Style（缓存命中不算）。测试与性能观察用。</summary>
    internal static int Compositions { get; set; }

    /// <summary>某个元素这一层的样式。</summary>
    /// <param name="element">元素。</param>
    public static ResourceDictionary? GetStyles(FrameworkElement element) => (ResourceDictionary?)element.GetValue(StylesProperty);

    /// <summary>让一个元素当一层样式：这一层的样式叠在最近的外层之上，作用到这个元素与它里面的。</summary>
    /// <param name="element">元素。</param>
    /// <param name="value">这一层的样式。</param>
    public static void SetStyles(FrameworkElement element, ResourceDictionary? value) => element.SetValue(StylesProperty, value);

    /// <summary>某个元素所在作用域的语言包；哪里都没写时按界面文化取。</summary>
    /// <param name="target">元素。</param>
    public static Lang GetLang(DependencyObject target) => (Lang?)target.GetValue(LangProperty) ?? Rune.Wpf.Lang.ForCulture(CultureInfo.CurrentUICulture);

    /// <summary>给一个元素（及它里面的）设语言包。</summary>
    /// <param name="target">元素。</param>
    /// <param name="value">语言包；null 时沿用外面的。</param>
    public static void SetLang(DependencyObject target, Lang? value) => target.SetValue(LangProperty, value);

    /// <inheritdoc />
    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        ProviderLayer.Of(this)?.Compose();
    }
}
