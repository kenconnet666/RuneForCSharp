using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 用户按钮（vue-tsx-pro 的 UserButton）：头像 + 名字（可带一行说明）+ 展开箭头，做账号菜单、切换账号的触发器；放在 AppShell 顶栏、侧栏底部。
/// <code>
/// &lt;rune:UserButton DisplayName="林小满" Source="{Binding Avatar}" Click="OnAccount" /&gt;
/// &lt;rune:UserButton DisplayName="林小满" Description="采购部 · 管理员" /&gt;   &lt;!-- 两行：名字与说明，占满一行 --&gt;
/// &lt;rune:UserButton DisplayName="林小满" Compact="True" /&gt;               &lt;!-- 只剩头像：名字是按钮的名称 --&gt;
/// </code>
/// 没有说明时是一颗胶囊；有说明且不 <see cref="Compact"/> 时占满一行、箭头换成上下双箭头（<see cref="IsDescribed"/>，形态在主题样式里）。
/// 头像 sm；名字 medium、0.93em，说明 textMuted、0.86em，放不下时省略；箭头 textMuted。TS 的 name、src 在这里叫 DisplayName、Source（Name 是元素名）。
/// 部件样式键：<see cref="UserButtonParts"/>。
/// </summary>
public class UserButton : ButtonBase
{
    /// <summary>名字：显示在头像旁边，是按钮的名称。</summary>
    public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.Register(
        nameof(DisplayName), typeof(string), typeof(UserButton), new FrameworkPropertyMetadata(null, (d, _) => ((UserButton)d).Update()));

    /// <summary>名字下面的一行说明：部门、角色、邮箱。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(UserButton), new FrameworkPropertyMetadata(null, (d, _) => ((UserButton)d).Update()));

    /// <summary>头像图片；没有时显示名字的首字母。</summary>
    public static readonly DependencyProperty SourceProperty = Avatar.SourceProperty.AddOwner(
        typeof(UserButton), new FrameworkPropertyMetadata(null, (d, _) => ((UserButton)d).Update()));

    /// <summary>只显示头像（顶栏空间紧、侧栏收起时）。</summary>
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(UserButton), new FrameworkPropertyMetadata(false, (d, _) => ((UserButton)d).Update()));

    /// <summary>结束边的箭头；不写时 ChevronDown，有说明时 ChevronsUpDown。</summary>
    public static readonly DependencyProperty IndicatorProperty = DependencyProperty.Register(
        nameof(Indicator), typeof(IconData), typeof(UserButton), new FrameworkPropertyMetadata(null, (d, _) => ((UserButton)d).Update()));

    /// <summary>显示结束边的箭头，默认是（TS 的 indicator={false} 是 false）。</summary>
    public static readonly DependencyProperty ShowIndicatorProperty = DependencyProperty.Register(
        nameof(ShowIndicator), typeof(bool), typeof(UserButton), new FrameworkPropertyMetadata(true, (d, _) => ((UserButton)d).Update()));

    static readonly DependencyPropertyKey IsDescribedKey = DependencyProperty.RegisterReadOnly(nameof(IsDescribed), typeof(bool), typeof(UserButton), new PropertyMetadata(false));

    /// <summary>写了说明：主题据此在不 compact 时换成整行的形态。</summary>
    public static readonly DependencyProperty IsDescribedProperty = IsDescribedKey.DependencyProperty;

    readonly Avatar avatar = new() { Name = "PART_Avatar" };
    readonly StackPanel text = new() { Name = "PART_Text" };
    readonly Text name = new() { Name = "PART_Name" };
    readonly Text description = new() { Name = "PART_Description" };
    readonly Icon indicator = new() { Name = "PART_Indicator" };

    static UserButton() => DefaultStyleKeyProperty.OverrideMetadata(typeof(UserButton), new FrameworkPropertyMetadata(typeof(UserButton)));

    /// <summary>新建：头像、名字与说明、箭头；部件引用主题的部件样式。</summary>
    public UserButton()
    {
        avatar.SetResourceReference(StyleProperty, UserButtonParts.Avatar);
        text.SetResourceReference(StyleProperty, UserButtonParts.Text);
        name.SetResourceReference(StyleProperty, UserButtonParts.Name);
        description.SetResourceReference(StyleProperty, UserButtonParts.Description);
        indicator.SetResourceReference(StyleProperty, UserButtonParts.Indicator);
        text.Children.Add(name);
        text.Children.Add(description);
        Content = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { avatar, text, indicator } };
        Update();
    }

    /// <summary>名字。</summary>
    public string? DisplayName { get => (string?)GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>头像图片。</summary>
    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>只显示头像。</summary>
    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }

    /// <summary>结束边的箭头。</summary>
    public IconData? Indicator { get => (IconData?)GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }

    /// <summary>显示结束边的箭头。</summary>
    public bool ShowIndicator { get => (bool)GetValue(ShowIndicatorProperty); set => SetValue(ShowIndicatorProperty, value); }

    /// <summary>写了说明。</summary>
    public bool IsDescribed => (bool)GetValue(IsDescribedProperty);

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TextElement.FontSizeProperty) UpdateFontSizes();
    }

    /// <summary>头像、文字与箭头。</summary>
    void Update()
    {
        var (described, compact) = (!string.IsNullOrEmpty(Description), Compact);
        SetValue(IsDescribedKey, described);
        avatar.DisplayName = DisplayName;
        // 名字就写在旁边时头像只是装饰：不然按钮的名称里名字出现两遍
        avatar.Decorative = !compact;
        avatar.Source = Source;
        name.Text = DisplayName;
        description.Text = Description;
        description.Visibility = described ? Visibility.Visible : Visibility.Collapsed;
        text.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        // 只在整行时撑开把箭头推到结束边：Flex 量尺寸时 grow 的子元素会占满可用宽度，胶囊就不按内容的宽了
        Flex.SetGrow(text, described && !compact);
        indicator.Data = Indicator ?? (described && !compact ? Lucide.ChevronsUpDown : Lucide.ChevronDown);
        indicator.Visibility = !compact && ShowIndicator ? Visibility.Visible : Visibility.Collapsed;
        UpdateFontSizes();
    }

    /// <summary>名字比所在的文字小一档（0.93em），说明再小一档（0.86em，相对字号的 xs 档）。</summary>
    void UpdateFontSizes()
    {
        name.SetValue(TextElement.FontSizeProperty, 0.93 * FontSize);
        description.SetValue(TextElement.FontSizeProperty, 0.86 * FontSize);
    }

    /// <inheritdoc />
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new UserButtonAutomationPeer(this);

    /// <summary>读屏名称默认是名字、帮助文字默认是说明（compact 时没有）；使用方写的 AutomationProperties 优先。</summary>
    sealed class UserButtonAutomationPeer(UserButton owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override string GetNameCore() => AutomationProperties.GetName(owner) is { Length: > 0 } name ? name : owner.DisplayName ?? "";

        protected override string GetHelpTextCore() =>
            AutomationProperties.GetHelpText(owner) is { Length: > 0 } help ? help : owner.Compact ? "" : owner.Description ?? "";
    }
}

/// <summary>UserButton 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:UserButtonParts.Name}" TargetType="rune:Text"&gt;</c>。根就是按钮自己（TS 的 root）。</summary>
public static class UserButtonParts
{
    /// <summary>头像（TS 的 propsAvatar）：Avatar，sm。</summary>
    public static readonly PartKey Avatar = new(typeof(UserButton), nameof(Avatar), typeof(Avatar));

    /// <summary>名字与说明的外层：StackPanel。</summary>
    public static readonly PartKey Text = new(typeof(UserButton), nameof(Text), typeof(StackPanel));

    /// <summary>名字（TS 的 propsName）：Text，medium，放不下时省略。</summary>
    public static readonly PartKey Name = new(typeof(UserButton), nameof(Name), typeof(Text));

    /// <summary>说明（TS 的 propsDescription）：Text，textMuted，放不下时省略。</summary>
    public static readonly PartKey Description = new(typeof(UserButton), nameof(Description), typeof(Text));

    /// <summary>结束边的箭头（TS 的 propsIndicator）：Icon，textMuted。</summary>
    public static readonly PartKey Indicator = new(typeof(UserButton), nameof(Indicator), typeof(Icon));
}
