using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 一个值的变化（vue-tsx-pro 的 ValueChange）：原值划掉、箭头、新值。放在表格单元格、操作记录、审批意见里。
/// <code>
/// &lt;rune:ValueChange Before="¥1,200" After="¥1,350" /&gt;
/// &lt;rune:ValueChange After="新建时只有新值" /&gt;
/// </code>
/// 原值次要色、删除线；箭头（ArrowRight）次要色、竖直居中；新值正文色、中等字重。三者之间 space.xs，放不下时换行，按基线对齐。
/// 没有原值（null）时只显示新值。读屏念"原值 改为 新值"（TS 是 del、ins 与给读屏的"改为"，语言包的 FieldCompareTo）。
/// 文字的原值、新值是 Text；写成元素时原样放，删除线要自己加。部件样式键：<see cref="ValueChangeParts"/>。
/// </summary>
public class ValueChange : CompositeControl
{
    /// <summary>原值；没有（null）时只显示新值。</summary>
    public static readonly DependencyProperty BeforeProperty = DependencyProperty.Register(
        nameof(Before), typeof(object), typeof(ValueChange), new FrameworkPropertyMetadata(null, (d, _) => ((ValueChange)d).Update()));

    /// <summary>新值。</summary>
    public static readonly DependencyProperty AfterProperty = DependencyProperty.Register(
        nameof(After), typeof(object), typeof(ValueChange), new FrameworkPropertyMetadata(null, (d, _) => ((ValueChange)d).Update()));

    readonly Flex root = new() { Wrap = true, Align = FlexAlign.Baseline, ColumnGap = Space.Xs };
    readonly Border before = new() { Name = "PART_Before" };
    readonly Icon arrow = new() { Name = "PART_Arrow", Data = Lucide.ArrowRight };
    readonly Border after = new() { Name = "PART_After" };

    static ValueChange()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ValueChange), new FrameworkPropertyMetadata(typeof(ValueChange)));
        FocusableProperty.OverrideMetadata(typeof(ValueChange), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(ValueChange), new FrameworkPropertyMetadata(false));
    }

    /// <summary>新建：只有新值。</summary>
    public ValueChange()
    {
        before.SetResourceReference(StyleProperty, ValueChangeParts.Before);
        arrow.SetResourceReference(StyleProperty, ValueChangeParts.Arrow);
        after.SetResourceReference(StyleProperty, ValueChangeParts.After);
        // 箭头不按基线、竖直居中（TS 的 align-self: center）
        Flex.SetAlignSelf(arrow, FlexAlign.Center);
        root.Children.Add(before);
        root.Children.Add(arrow);
        root.Children.Add(after);
        Root = root;
        Update();
    }

    /// <summary>原值。</summary>
    public object? Before { get => GetValue(BeforeProperty); set => SetValue(BeforeProperty, value); }

    /// <summary>新值。</summary>
    public object? After { get => GetValue(AfterProperty); set => SetValue(AfterProperty, value); }

    /// <summary>读屏念的一句："原值 改为 新值"；没有原值时是新值。</summary>
    internal string Spoken
    {
        get
        {
            var next = Plain(After);
            return Before is null ? next : $"{Plain(Before)} {Provider.GetLang(this).FieldCompareTo} {next}";
        }
    }

    void Update()
    {
        before.Child = Content(Before, strike: true);
        before.Visibility = arrow.Visibility = Before is null ? Visibility.Collapsed : Visibility.Visible;
        after.Child = Content(After, strike: false);
    }

    static UIElement? Content(object? value, bool strike) => value switch
    {
        null => null,
        UIElement element => element,
        _ => new Text { Text = Plain(value), TextDecorations = strike ? System.Windows.TextDecorations.Strikethrough : null },
    };

    static string Plain(object? value) => value switch
    {
        null => "",
        TextBlock block => block.Text ?? "",
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.CurrentCulture),
        _ => value.ToString() ?? "",
    };

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ValueChangePeer(this);

    /// <summary>读屏：一段文字，念"原值 改为 新值"（里面的原值、箭头、新值不另念）。</summary>
    sealed class ValueChangePeer(ValueChange owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetClassNameCore() => nameof(ValueChange);

        protected override string GetNameCore() => owner.Spoken;

        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>ValueChange 的部件样式键。</summary>
public static class ValueChangeParts
{
    /// <summary>原值（TS 的 propsBefore）。</summary>
    public static readonly PartKey Before = new(typeof(ValueChange), nameof(Before), typeof(Border));

    /// <summary>箭头（TS 的 propsArrow）。</summary>
    public static readonly PartKey Arrow = new(typeof(ValueChange), nameof(Arrow), typeof(Icon));

    /// <summary>新值（TS 的 propsAfter）。</summary>
    public static readonly PartKey After = new(typeof(ValueChange), nameof(After), typeof(Border));
}
