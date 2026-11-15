using System.Globalization;
using global::Avalonia;
using global::Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 作用域配置（vue-tsx-pro 的 Provider）：包住一片区域，里面的组件读这里的设置。样式与局部主题用 Avalonia 自己的
/// Styles、ThemeVariantScope（DESIGN §4），这里放平台没有的：语言包、工作日历、权限判断。
/// <code>
/// &lt;rune:Provider Lang="{x:Static rune:Lang.EnUs}"&gt;……&lt;/rune:Provider&gt;
/// &lt;Window rune:Provider.Lang="{x:Static rune:Lang.EnUs}"&gt;   &lt;!-- 也能直接挂在任何控件上 --&gt;
/// </code>
/// </summary>
public class Provider : Decorator
{
    /// <summary>语言包：沿逻辑树继承；哪里都没写时按界面文化取（<see cref="Rune.Avalonia.Lang.ForCulture"/>）。</summary>
    public static readonly AttachedProperty<Lang?> LangProperty = AvaloniaProperty.RegisterAttached<Provider, AvaloniaObject, Lang?>("Lang", inherits: true);

    /// <summary>工作日历（上游 createUi / Provider 的 workCalendar）：沿逻辑树继承；哪里都没写时只按周末（<see cref="Rune.Avalonia.WorkCalendar.Weekdays"/>）。</summary>
    public static readonly AttachedProperty<WorkCalendar?> WorkCalendarProperty = AvaloniaProperty.RegisterAttached<Provider, AvaloniaObject, WorkCalendar?>("WorkCalendar", inherits: true);

    /// <summary>
    /// 权限判断（上游 createUi / Provider 的 permissions）：用户有没有这个权限点，沿逻辑树继承；哪里都没写时一律放行。
    /// 组件库不知道权限从哪来（后端返回的权限点、角色），由应用接入；登录、退出、换角色后换一个新的函数，<see cref="Rune.Avalonia.Can"/> 跟着变。
    /// 只控制界面，不是安全边界：接口上的权限校验才是。
    /// </summary>
    public static readonly AttachedProperty<Func<string, bool>?> PermissionsProperty = AvaloniaProperty.RegisterAttached<Provider, AvaloniaObject, Func<string, bool>?>("Permissions", inherits: true);

    /// <summary>这片区域的语言包。</summary>
    public Lang? Lang { get => GetValue(LangProperty); set => SetValue(LangProperty, value); }

    /// <summary>这片区域的工作日历。</summary>
    public WorkCalendar? WorkCalendar { get => GetValue(WorkCalendarProperty); set => SetValue(WorkCalendarProperty, value); }

    /// <summary>这片区域的权限判断。</summary>
    public Func<string, bool>? Permissions { get => GetValue(PermissionsProperty); set => SetValue(PermissionsProperty, value); }

    /// <summary>某个控件所在作用域的权限判断；没接入时 null（一律放行）。</summary>
    /// <param name="target">控件。</param>
    public static Func<string, bool>? GetPermissions(AvaloniaObject target) => target.GetValue(PermissionsProperty);

    /// <summary>给一个控件（及它里面的）设权限判断。</summary>
    /// <param name="target">控件。</param>
    /// <param name="value">权限判断；null 时沿用外面的。</param>
    public static void SetPermissions(AvaloniaObject target, Func<string, bool>? value) => target.SetValue(PermissionsProperty, value);

    /// <summary>
    /// 控件所在作用域里有没有权限（上游 useCan）：给几个时有其中任意一个就算有；没接入权限判断时一律放行。
    /// <code>deleteButton.IsEnabled = Provider.Can(deleteButton, "cost:project:remove");</code>
    /// </summary>
    /// <param name="target">控件。</param>
    /// <param name="permissions">权限点。</param>
    public static bool Can(AvaloniaObject target, params IEnumerable<string> permissions) =>
        GetPermissions(target) is not { } has || permissions.Any(has);

    /// <summary>某个控件所在作用域的工作日历；哪里都没写时只按周末。</summary>
    /// <param name="target">控件。</param>
    public static WorkCalendar GetWorkCalendar(AvaloniaObject target) => target.GetValue(WorkCalendarProperty) ?? Rune.Avalonia.WorkCalendar.Weekdays;

    /// <summary>给一个控件（及它里面的）设工作日历。</summary>
    /// <param name="target">控件。</param>
    /// <param name="value">工作日历；null 时沿用外面的。</param>
    public static void SetWorkCalendar(AvaloniaObject target, WorkCalendar? value) => target.SetValue(WorkCalendarProperty, value);

    /// <summary>某个控件所在作用域的语言包；哪里都没写时按界面文化取。</summary>
    /// <param name="target">控件。</param>
    public static Lang GetLang(AvaloniaObject target) => target.GetValue(LangProperty) ?? Rune.Avalonia.Lang.ForCulture(CultureInfo.CurrentUICulture);

    /// <summary>给一个控件（及它里面的）设语言包。</summary>
    /// <param name="target">控件。</param>
    /// <param name="value">语言包；null 时沿用外面的。</param>
    public static void SetLang(AvaloniaObject target, Lang? value) => target.SetValue(LangProperty, value);
}
