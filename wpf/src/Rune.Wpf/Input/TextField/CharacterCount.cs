using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf;

/// <summary>
/// 字数计数（vue-tsx-pro 的 CharacterCount）：显示"12 / 100"，超出上限时用危险色（<see cref="IsOver"/>）；sm、textMuted、等宽数字。
/// TextField 的 ShowCount 用它，也可以单独放在 Field 里给自己的输入组件用。读屏念完整的句子（语言包的 characterCount）。
/// </summary>
public class CharacterCount : TextBlock
{
    /// <summary>已输入的字符数。</summary>
    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(nameof(Count), typeof(int), typeof(CharacterCount),
        new FrameworkPropertyMetadata(0, (d, _) => ((CharacterCount)d).Update()));

    /// <summary>上限；不给时只显示字数。</summary>
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(nameof(Max), typeof(int?), typeof(CharacterCount),
        new FrameworkPropertyMetadata(null, (d, _) => ((CharacterCount)d).Update()));

    static readonly DependencyPropertyKey IsOverKey = DependencyProperty.RegisterReadOnly(nameof(IsOver), typeof(bool), typeof(CharacterCount), new PropertyMetadata(false));

    /// <summary>超出上限：主题样式按它换危险色。</summary>
    public static readonly DependencyProperty IsOverProperty = IsOverKey.DependencyProperty;

    static CharacterCount() => DefaultStyleKeyProperty.OverrideMetadata(typeof(CharacterCount), new FrameworkPropertyMetadata(typeof(CharacterCount)));

    /// <summary>新建：显示 0。</summary>
    public CharacterCount() => Update();

    /// <summary>已输入的字符数。</summary>
    public int Count { get => (int)GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <summary>上限。</summary>
    public int? Max { get => (int?)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>超出上限。</summary>
    public bool IsOver => (bool)GetValue(IsOverProperty);

    void Update()
    {
        Text = Max is { } max ? $"{Count} / {max}" : Count.ToString();
        SetValue(IsOverKey, Max is { } limit && Count > limit);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CountPeer(this);

    sealed class CountPeer(CharacterCount owner) : TextBlockAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(CharacterCount);

        protected override string GetNameCore() => Provider.GetLang(owner).CharacterCount(owner.Count, owner.Max);
    }
}
