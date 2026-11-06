using System.Windows;

namespace Rune.Wpf;

/// <summary>
/// 把一个样式片段复制成以另一个样式为 BasedOn 的新样式。片段本身不被使用，所以不会被封存（sealed），
/// 同一个片段能叠到不同的外层上。只支持样式里常用的 Setter 与四种属性/数据触发器；遇到别的直接报错，
/// 免得静默丢掉使用方写的东西。
/// </summary>
static class StyleCloner
{
    public static Style Rebase(Style fragment, Style? basedOn)
    {
        var style = new Style(fragment.TargetType, basedOn);
        foreach (var setter in fragment.Setters) style.Setters.Add(Clone(setter));
        foreach (var trigger in fragment.Triggers) style.Triggers.Add(Clone(trigger));
        foreach (var key in fragment.Resources.Keys) style.Resources[key] = fragment.Resources[key];
        return style;
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
