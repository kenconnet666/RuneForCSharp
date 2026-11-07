using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 字数计数（vue-tsx-pro 的 CharacterCount）：显示"12 / 100"，超出上限时用危险色（伪类 <c>:over</c>）；sm、textMuted、等宽数字。
/// TextField 的 ShowCount 用它，也可以单独放在 Field 里给自己的输入组件用。读屏念完整的句子（语言包的 characterCount）。
/// </summary>
public class CharacterCount : TextBlock
{
    /// <summary>已输入的字符数。</summary>
    public static readonly StyledProperty<int> CountProperty = AvaloniaProperty.Register<CharacterCount, int>(nameof(Count));

    /// <summary>上限；不给时只显示字数。</summary>
    public static readonly StyledProperty<int?> MaxProperty = AvaloniaProperty.Register<CharacterCount, int?>(nameof(Max));

    /// <summary>新建：显示 0。</summary>
    public CharacterCount() => Update();

    /// <summary>已输入的字符数。</summary>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <summary>上限。</summary>
    public int? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(CharacterCount);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty || change.Property == MaxProperty) Update();
    }

    void Update()
    {
        Text = Max is { } max ? $"{Count} / {max}" : Count.ToString();
        PseudoClasses.Set(":over", Max is { } limit && Count > limit);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CountPeer(this);

    sealed class CountPeer(CharacterCount owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetClassNameCore() => nameof(CharacterCount);

        protected override string? GetNameCore() => Provider.GetLang(owner).CharacterCount(owner.Count, owner.Max);
    }
}
