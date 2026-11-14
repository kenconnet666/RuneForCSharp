using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 用户按钮（vue-tsx-pro 的 UserButton）：头像 + 名字（可带一行说明）+ 展开箭头，做账号菜单、切换账号的触发器；放在 AppShell 顶栏、侧栏底部。
/// <code>
/// &lt;rune:UserButton DisplayName="林小满" Source="{Binding Avatar}" Click="OnAccount" /&gt;
/// &lt;rune:UserButton DisplayName="林小满" Description="采购部 · 管理员" /&gt;   &lt;!-- 两行：名字与说明，占满一行 --&gt;
/// &lt;rune:UserButton DisplayName="林小满" Compact="True" /&gt;               &lt;!-- 只剩头像：名字是按钮的名称 --&gt;
/// </code>
/// 建在 <see cref="ButtonBase"/> 上：没有说明时是一颗胶囊（内边距上下、起始边 space.xs，结束边 space.sm，圆角 full，悬停 fillHover，正文色）；
/// 有说明且不 <see cref="Compact"/> 时占满一行（左右 space.sm、圆角 md），箭头换成上下双箭头；Compact 时只剩头像（四周 space.xs），名字成为读屏名称。
/// 头像 sm；名字 medium、0.93em，说明 textMuted、0.86em，放不下时省略；箭头 textMuted。部件见 <see cref="UserButtonParts"/>。
/// </summary>
[PseudoClasses(":compact", ":described")]
public class UserButton : ButtonBase
{
    /// <summary>名字：显示在头像旁边，是按钮的名称（同 Avatar 的 DisplayName；Name 是控件名）。</summary>
    public static readonly StyledProperty<string?> DisplayNameProperty = AvaloniaProperty.Register<UserButton, string?>(nameof(DisplayName));

    /// <summary>名字下面的一行说明：部门、角色、邮箱。</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<UserButton, string?>(nameof(Description));

    /// <summary>头像图片；没有时显示名字的首字母。</summary>
    public static readonly StyledProperty<IImage?> SourceProperty = Avatar.SourceProperty.AddOwner<UserButton>();

    /// <summary>只显示头像（顶栏空间紧、侧栏收起时）。</summary>
    public static readonly StyledProperty<bool> CompactProperty = AvaloniaProperty.Register<UserButton, bool>(nameof(Compact));

    /// <summary>结束边的箭头；不写时 ChevronDown，有说明时 ChevronsUpDown。</summary>
    public static readonly StyledProperty<IconData?> IndicatorProperty = AvaloniaProperty.Register<UserButton, IconData?>(nameof(Indicator));

    /// <summary>显示结束边的箭头，默认是（TS 的 indicator={false} 是 false）。</summary>
    public static readonly StyledProperty<bool> ShowIndicatorProperty = AvaloniaProperty.Register<UserButton, bool>(nameof(ShowIndicator), true);

    readonly Avatar avatar = new() { Name = UserButtonParts.Avatar, Size = TextSize.Sm, VerticalAlignment = VerticalAlignment.Center };
    readonly StackPanel text = new() { Name = UserButtonParts.Text, VerticalAlignment = VerticalAlignment.Center };
    readonly Text name = new() { Name = UserButtonParts.Name, Weight = TextWeight.Medium, Truncate = true };
    readonly Text description = new() { Name = UserButtonParts.Description, Tone = TextTone.Muted, Truncate = true };
    readonly Icon indicator = new() { Name = UserButtonParts.Indicator, Tone = TextTone.Muted, VerticalAlignment = VerticalAlignment.Center };
    readonly Flex row = new() { Align = FlexAlign.Center, Gap = Space.Sm };
    readonly List<IDisposable> shape = [];

    /// <summary>新建：头像、名字与说明、箭头。</summary>
    public UserButton()
    {
        text.Children.AddRange([name, description]);
        row.Children.AddRange([avatar, text, indicator]);
        Content = row;
        Update();
    }

    /// <summary>名字。</summary>
    public string? DisplayName { get => GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>头像图片。</summary>
    public IImage? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>只显示头像。</summary>
    public bool Compact { get => GetValue(CompactProperty); set => SetValue(CompactProperty, value); }

    /// <summary>结束边的箭头。</summary>
    public IconData? Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }

    /// <summary>显示结束边的箭头。</summary>
    public bool ShowIndicator { get => GetValue(ShowIndicatorProperty); set => SetValue(ShowIndicatorProperty, value); }

    bool Described => !string.IsNullOrEmpty(Description);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DisplayNameProperty || change.Property == DescriptionProperty || change.Property == SourceProperty || change.Property == CompactProperty
            || change.Property == IndicatorProperty || change.Property == ShowIndicatorProperty || change.Property == TextElement.FontSizeProperty) Update();
    }

    /// <summary>
    /// 头像、文字、箭头与形态：有说明且不 compact 时整行（左右 space.sm、md 圆角），compact 只剩头像（四周 space.xs），否则胶囊
    /// （上下与起始边 space.xs、结束边 space.sm、圆角 full）。
    /// </summary>
    void Update()
    {
        var (described, compact) = (Described, Compact);
        PseudoClasses.Set(":compact", compact);
        PseudoClasses.Set(":described", described);
        avatar.DisplayName = DisplayName;
        // 名字就写在旁边时头像只是装饰：不然按钮的名称里名字出现两遍
        avatar.Decorative = !compact;
        avatar.Source = Source;
        name.Text = DisplayName;
        description.Text = Description;
        description.IsVisible = described;
        text.IsVisible = !compact;
        // 只在整行时撑开把箭头推到结束边：Flex 量尺寸时 grow 的子元素会占满可用宽度，胶囊就不按内容的宽了
        Flex.SetGrow(text, described && !compact);
        indicator.Data = Indicator ?? (described && !compact ? Lucide.ChevronsUpDown : Lucide.ChevronDown);
        indicator.IsVisible = !compact && ShowIndicator;

        var em = TextElement.GetFontSize(this);
        var (xs, sm) = (this.Resource<double>(ThemeKeys.Space.Xs) * em, this.Resource<double>(ThemeKeys.Space.Sm) * em);
        name.FontSize = 0.93 * em;
        description.FontSize = 0.86 * em;
        var row = described && !compact;
        // 形态按模板优先级写：使用方的样式与本地值盖得过
        foreach (var value in shape) value.Dispose();
        shape.Clear();
        shape.AddRange(new[]
        {
            SetValue(PaddingProperty, compact ? new Thickness(xs) : row ? new Thickness(sm, xs) : new Thickness(xs, xs, sm, xs), BindingPriority.Template),
            SetValue(CornerRadiusProperty, new CornerRadius(row ? this.Resource<double>(ThemeKeys.Radius.Md) * em : 9999), BindingPriority.Template),
            SetValue(HorizontalAlignmentProperty, row ? HorizontalAlignment.Stretch : HorizontalAlignment.Left, BindingPriority.Template),
        }.OfType<IDisposable>());
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new UserButtonAutomationPeer(this);

    /// <summary>读屏名称默认是名字、帮助文字默认是说明（compact 时没有）；使用方写的 AutomationProperties 优先。</summary>
    sealed class UserButtonAutomationPeer(UserButton owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override string? GetNameCore() => AutomationProperties.GetName(owner) is { Length: > 0 } name ? name : owner.DisplayName;

        protected override string? GetHelpTextCore() =>
            AutomationProperties.GetHelpText(owner) is { Length: > 0 } help ? help : owner.Compact ? null : owner.Description;
    }
}

/// <summary>UserButton 的部件名（内容里的元素）：<c>rune|UserButton rune|Avatar#PART_Avatar</c>。根就是按钮自己（TS 的 root）。</summary>
public static class UserButtonParts
{
    /// <summary>头像（TS 的 propsAvatar）。</summary>
    public const string Avatar = "PART_Avatar";

    /// <summary>名字与说明的外层。</summary>
    public const string Text = "PART_Text";

    /// <summary>名字（TS 的 propsName）。</summary>
    public const string Name = "PART_Name";

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public const string Description = "PART_Description";

    /// <summary>结束边的箭头（TS 的 propsIndicator）。</summary>
    public const string Indicator = "PART_Indicator";
}
