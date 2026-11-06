using System.Windows;

namespace Rune.Wpf;

/// <summary>
/// 把一个样式片段复制成以另一个样式为 BasedOn 的新样式。片段本身不被使用，所以不会被封存（sealed），
/// 同一个片段能叠到不同的外层上。只支持样式里常用的 Setter 与四种属性/数据触发器；遇到别的直接报错，
/// 免得静默丢掉使用方写的东西。
/// </summary>
static class StyleCloner
{
    /// <summary>
    /// 叠到 <paramref name="basedOn"/> 上。片段的 Style.Resources 里按部件键写的嵌套样式（外层部件里的内层部件，DESIGN §7）
    /// 也递归叠上去：基础取 BasedOn 链里同键的嵌套样式，没有时取 <paramref name="resolve"/> 给的同键结果。
    /// WPF 原生是内层整个替换外层（主题与外层 Provider 的设置都丢了）。
    /// </summary>
    public static Style Rebase(Style fragment, Style? basedOn, Func<object, Style?> resolve)
    {
        var style = new Style(fragment.TargetType, basedOn);
        foreach (var setter in fragment.Setters) style.Setters.Add(Clone(setter));
        foreach (var trigger in fragment.Triggers) style.Triggers.Add(Clone(trigger));
        foreach (var key in fragment.Resources.Keys)
        {
            style.Resources[key] = fragment.Resources[key] is Style nested && key is ComponentResourceKey
                ? Rebase(nested, Nested(basedOn, key) ?? resolve(key), resolve)
                : fragment.Resources[key];
        }
        return style;
    }

    /// <summary>片段里有没有按部件键写的嵌套样式。</summary>
    public static bool HasNestedStyles(Style fragment) =>
        fragment.Resources.Keys.OfType<ComponentResourceKey>().Any(key => fragment.Resources[key] is Style);

    /// <summary>BasedOn 链里同键的嵌套样式（WPF 查 Style.Resources 时也沿着 BasedOn 往上找）。</summary>
    static Style? Nested(Style? style, object key)
    {
        for (; style is not null; style = style.BasedOn)
            if (style.Resources.Contains(key) && style.Resources[key] is Style found) return found;
        return null;
    }

    static SetterBase Clone(SetterBase setter) => setter switch
    {
        Setter s => new Setter(s.Property, s.Value, s.TargetName),
        _ => throw new NotSupportedException($"Provider 的样式里不支持 {setter.GetType().Name}，请改用 Setter。"),
    };

    static TriggerBase Clone(TriggerBase trigger)
    {
        switch (trigger)
        {
            case Trigger t:
                var trig = new Trigger { Property = t.Property, Value = t.Value, SourceName = t.SourceName };
                CopySetters(t.Setters, trig.Setters);
                return trig;
            case DataTrigger t:
                var data = new DataTrigger { Binding = t.Binding, Value = t.Value };
                CopySetters(t.Setters, data.Setters);
                return data;
            case MultiTrigger t:
                var multi = new MultiTrigger();
                foreach (var c in t.Conditions) multi.Conditions.Add(new Condition(c.Property, c.Value, c.SourceName));
                CopySetters(t.Setters, multi.Setters);
                return multi;
            case MultiDataTrigger t:
                var multiData = new MultiDataTrigger();
                foreach (var c in t.Conditions) multiData.Conditions.Add(new Condition(c.Binding, c.Value));
                CopySetters(t.Setters, multiData.Setters);
                return multiData;
            default:
                throw new NotSupportedException($"Provider 的样式里不支持 {trigger.GetType().Name}（EventTrigger 请写在组件模板里）。");
        }
    }

    static void CopySetters(SetterBaseCollection from, SetterBaseCollection to)
    {
        foreach (var s in from) to.Add(Clone(s));
    }
}
