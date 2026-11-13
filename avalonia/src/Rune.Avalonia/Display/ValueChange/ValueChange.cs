using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 一个值的变化（vue-tsx-pro 的 ValueChange）：原值划掉、箭头、新值。放在表格单元格、操作记录、审批意见里。
/// <code>
/// &lt;rune:ValueChange Before="¥1,200" After="¥1,350" /&gt;
/// &lt;rune:ValueChange After="新建时只有新值" /&gt;
/// </code>
/// 原值次要色、删除线；箭头（ArrowRight）次要色、竖直居中；新值正文色、中等字重。三者之间 space.xs，放不下时换行，按基线对齐。
/// 没有原值（null）时只显示新值。读屏念"原值 改为 新值"（TS 是 del、ins 与给读屏的"改为"，语言包的 FieldCompareTo）。
/// 文字的原值、新值是 Text；写成控件时原样放，删除线要自己加。
/// 部件：<c>rune|ValueChange Border#PART_Before</c>、<c>rune|Icon#PART_Arrow</c>、<c>Border#PART_After</c>。
/// </summary>
public class ValueChange : Control
{
    /// <summary>原值；没有（null）时只显示新值。</summary>
    public static readonly StyledProperty<object?> BeforeProperty = AvaloniaProperty.Register<ValueChange, object?>(nameof(Before));

    /// <summary>新值。</summary>
    public static readonly StyledProperty<object?> AfterProperty = AvaloniaProperty.Register<ValueChange, object?>(nameof(After));

    readonly Flex root = new() { Wrap = true, Align = FlexAlign.Baseline, ColumnGap = Space.Xs };
    readonly Border before = new() { Name = ValueChangeParts.Before };
    readonly Icon arrow = new() { Name = ValueChangeParts.Arrow, Data = Lucide.ArrowRight, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
    readonly Border after = new() { Name = ValueChangeParts.After };

    /// <summary>新建：只有新值。</summary>
    public ValueChange()
    {
        // 箭头不按基线、竖直居中（TS 的 align-self: center）
        Flex.SetAlignSelf(arrow, FlexAlign.Center);
        root.Children.AddRange([before, arrow, after]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
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

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BeforeProperty || change.Property == AfterProperty) Update();
    }

    void Update()
    {
        before.Child = Content(Before, strike: true);
        before.IsVisible = arrow.IsVisible = Before is not null;
        after.Child = Content(After, strike: false);
    }

    static Control? Content(object? value, bool strike) => value switch
    {
        null => null,
        Control control => control,
        _ => new Text { Text = Plain(value), TextDecorations = strike ? global::Avalonia.Media.TextDecorations.Strikethrough : null },
    };

    static string Plain(object? value) => value switch
    {
        null => "",
        TextBlock block => block.Text ?? "",
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.CurrentCulture),
        _ => value.ToString() ?? "",
    };

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ValueChangePeer(this);

    /// <summary>读屏：一段文字，念"原值 改为 新值"（里面的原值、箭头、新值不另念）。</summary>
    sealed class ValueChangePeer(ValueChange owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetNameCore() => owner.Spoken;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>ValueChange 的部件名。</summary>
public static class ValueChangeParts
{
    /// <summary>原值（TS 的 propsBefore）。</summary>
    public const string Before = "PART_Before";

    /// <summary>箭头（TS 的 propsArrow）。</summary>
    public const string Arrow = "PART_Arrow";

    /// <summary>新值（TS 的 propsAfter）。</summary>
    public const string After = "PART_After";
}
