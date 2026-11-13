using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace Rune.Avalonia;

/// <summary>
/// 条目列表（vue-tsx-pro 的 ItemList）：一行一条，起始边是图标、色点或头像，中间是标题与说明，结束边是标记、数字或操作。
/// 待办、通知、我负责的任务这类"界面里的列表"（正文里的列表用 <see cref="List"/>）。
/// <code>
/// &lt;rune:ItemList Divided="True"&gt;
///   &lt;rune:ItemListItem Title="资料审核" Description="PO-2026-0187 · 官网改版" Start="{…StatusDot}" End="{…Badge}" /&gt;
/// &lt;/rune:ItemList&gt;
/// </code>
/// <see cref="Divided"/> 在条目之间画线，<see cref="Hoverable"/> 悬停时着底色，<see cref="Inset"/> 是条目两侧的内边距（放在弹出层、卡片里时与容器对齐；
/// 悬停着底色、整条可点的条目不写时留 sm）。里面的链接悬停才有下划线（一列下来都带下划线太重）。读屏里是列表。
/// </summary>
public class ItemList : StackPanel
{
    /// <summary>条目之间画线（borderMuted，最后一条下面没有）。</summary>
    public static readonly StyledProperty<bool> DividedProperty = AvaloniaProperty.Register<ItemList, bool>(nameof(Divided));

    /// <summary>悬停时着底色：整条可点时。</summary>
    public static readonly StyledProperty<bool> HoverableProperty = AvaloniaProperty.Register<ItemList, bool>(nameof(Hoverable));

    /// <summary>疏密，默认 standard。</summary>
    public static readonly StyledProperty<ItemListDensity> DensityProperty = AvaloniaProperty.Register<ItemList, ItemListDensity>(nameof(Density), ItemListDensity.Standard);

    /// <summary>条目两侧的内边距；不写时悬停着底色、整条可点的条目留 sm，其余不留。</summary>
    public static readonly StyledProperty<Space?> InsetProperty = AvaloniaProperty.Register<ItemList, Space?>(nameof(Inset));

    /// <summary>新建：里面的链接悬停才有下划线（上游 ItemList 给 LinkUnderlineContext 的 hover）。</summary>
    public ItemList() => SetValue(Link.UnderlineScopeProperty, LinkUnderline.Hover, BindingPriority.Template);

    /// <summary>条目之间画线。</summary>
    public bool Divided { get => GetValue(DividedProperty); set => SetValue(DividedProperty, value); }

    /// <summary>悬停时着底色。</summary>
    public bool Hoverable { get => GetValue(HoverableProperty); set => SetValue(HoverableProperty, value); }

    /// <summary>疏密。</summary>
    public ItemListDensity Density { get => GetValue(DensityProperty); set => SetValue(DensityProperty, value); }

    /// <summary>条目两侧的内边距。</summary>
    public Space? Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DividedProperty || change.Property == HoverableProperty || change.Property == DensityProperty || change.Property == InsetProperty)
            foreach (var item in Children.OfType<ItemListItem>()) item.ApplyLook();
    }

    /// <inheritdoc />
    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        foreach (var item in e.NewItems?.OfType<ItemListItem>() ?? []) item.ApplyLook();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ItemListAutomationPeer(this);

    sealed class ItemListAutomationPeer(ItemList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => nameof(ItemList);
    }
}

/// <summary>
/// 条目列表的一条（vue-tsx-pro 的 ItemListItem）：<see cref="Start"/>、<see cref="Title"/> 与 <see cref="Description"/>、<see cref="End"/> 三列，
/// 说明下面放 Content（更多内容）。标题与说明放不下时换行；<see cref="Align"/> 为 start 时起始边、结束边贴着第一行。
/// 整条可点：写 <see cref="LinkButton.Href"/>（绝对地址，系统打开）、Command，或订阅 <see cref="Invoked"/>（TS 的 onSelect），点哪里都一样；
/// 结束边的按钮照常可点，点它不触发这一条。<see cref="IsSelected"/> 是正在看的一条（主从视图左边的列表）：accentSoft 底，读屏的项状态是"当前"。
/// 不可用（IsEnabled=False）只对整条可点的条目有外观：变淡、点不了。不可点的条目不能聚焦，读屏里是列表项。
/// </summary>
[PseudoClasses(":divided", ":hoverable", ":interactive", ":selected", ":compact", ":align-start", ":has-start")]
public class ItemListItem : ButtonBase
{
    /// <summary>起始边：图标、色点、头像、状态。</summary>
    public static readonly StyledProperty<object?> StartProperty = AvaloniaProperty.Register<ItemListItem, object?>(nameof(Start));

    /// <summary>标题：一句话。</summary>
    public static readonly StyledProperty<object?> TitleProperty = AvaloniaProperty.Register<ItemListItem, object?>(nameof(Title));

    /// <summary>标题下的说明（textMuted、xs）。</summary>
    public static readonly StyledProperty<object?> DescriptionProperty = AvaloniaProperty.Register<ItemListItem, object?>(nameof(Description));

    /// <summary>结束边：标记、数字、时间、操作。</summary>
    public static readonly StyledProperty<object?> EndProperty = AvaloniaProperty.Register<ItemListItem, object?>(nameof(End));

    /// <summary>起始边、结束边与内容的对齐，默认 center。</summary>
    public static readonly StyledProperty<ItemListAlign> AlignProperty = AvaloniaProperty.Register<ItemListItem, ItemListAlign>(nameof(Align), ItemListAlign.Center);

    /// <summary>整条是链接（绝对地址）：点了用系统打开。</summary>
    public static readonly StyledProperty<Uri?> HrefProperty = LinkButton.HrefProperty.AddOwner<ItemListItem>();

    /// <summary>正在看的一条。</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<ItemListItem, bool>(nameof(IsSelected));

    EventHandler<RoutedEventArgs>? invoked;
    IDisposable? padding;
    IDisposable? focusable;

    /// <summary>新建：不出波纹（上游 ripple: false）。</summary>
    public ItemListItem() => SetValue(RippleProperty, false, BindingPriority.Template);

    /// <summary>起始边。</summary>
    public object? Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <summary>标题。</summary>
    public object? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>结束边。</summary>
    public object? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>对齐。</summary>
    public ItemListAlign Align { get => GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>链接地址。</summary>
    public Uri? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>正在看的一条。</summary>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>点了这一条（TS 的 onSelect）：订阅了才整条可点。在 Click、Command 之后发。</summary>
    public event EventHandler<RoutedEventArgs>? Invoked
    {
        add
        {
            invoked += value;
            ApplyLook();
        }
        remove
        {
            invoked -= value;
            ApplyLook();
        }
    }

    /// <summary>整条可点：有地址、命令或订阅了 Invoked。</summary>
    public bool IsInteractive => Href is not null || Command is not null || invoked is not null;

    /// <summary>焦点环叠在条目的边上（上游 outline 宽 sm、偏移 -1px），不像按钮那样外扩。</summary>
    private protected override double FocusRingOutset => 1;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HrefProperty || change.Property == CommandProperty || change.Property == StartProperty
            || change.Property == AlignProperty || change.Property == IsSelectedProperty || change.Property == FontSizeProperty)
            ApplyLook();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(global::Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        ApplyLook();
    }

    /// <summary>
    /// 按所在列表的设置与自己的状态换外观：伪类给主题，内边距（上下按疏密，两侧按 Inset）按模板优先级设、跟着字号；
    /// 只有整条可点的条目能聚焦。
    /// </summary>
    internal void ApplyLook()
    {
        var list = Parent as ItemList;
        var interactive = IsInteractive;
        var hoverable = list?.Hoverable ?? false;
        PseudoClasses.Set(":divided", list?.Divided ?? false);
        PseudoClasses.Set(":hoverable", hoverable);
        PseudoClasses.Set(":interactive", interactive);
        PseudoClasses.Set(":selected", IsSelected);
        PseudoClasses.Set(":compact", list?.Density == ItemListDensity.Compact);
        PseudoClasses.Set(":align-start", Align == ItemListAlign.Start);
        PseudoClasses.Set(":has-start", Start is not null);

        var block = this.Resource(list?.Density == ItemListDensity.Compact ? "Rune.Space.Xs" : "Rune.Space.Sm", 0d);
        var gutter = (list?.Inset ?? (hoverable || interactive ? Space.Sm : null)) is { ResourceKey: { } key } ? this.Resource(key, 0d) : 0;
        padding?.Dispose();
        padding = SetValue(PaddingProperty, new Thickness(gutter * FontSize, block * FontSize), BindingPriority.Template);
        focusable?.Dispose();
        focusable = SetValue(FocusableProperty, interactive, BindingPriority.Template);
    }

    /// <summary>先触发 Click、Command，再发 Invoked、打开链接；不可点、加载中、不可用时都不执行。</summary>
    protected override void OnClick()
    {
        if (!IsInteractive || IsLoading || IsDisabled) return;
        base.OnClick();
        invoked?.Invoke(this, new RoutedEventArgs());
        if (Href is { IsAbsoluteUri: true } href) LinkLauncher.Open(this, href);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ItemListItemAutomationPeer(this);

    /// <summary>读屏：可点时是链接或按钮（名称是标题，帮助文字是说明），否则是列表项；正在看的一条项状态是"当前"。</summary>
    sealed class ItemListItemAutomationPeer(ItemListItem owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            owner.Href is not null ? AutomationControlType.Hyperlink : owner.IsInteractive ? AutomationControlType.Button : AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(ItemListItem);

        protected override string? GetNameCore() =>
            AutomationProperties.GetName(owner) is { Length: > 0 } name ? name : owner.Title as string ?? base.GetNameCore();

        protected override string? GetHelpTextCore() =>
            AutomationProperties.GetHelpText(owner) is { Length: > 0 } help ? help : owner.Description as string;

        protected override string? GetItemStatusCore() =>
            owner.IsSelected ? DesktopMessages.For(Provider.GetLang(owner)).Current : base.GetItemStatusCore();

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>ItemListItem 的部件名。</summary>
public static class ItemListItemParts
{
    /// <summary>起始边（TS 的 start 外层）。</summary>
    public const string Start = "PART_Start";

    /// <summary>标题与说明的一栏（TS 的 body）。</summary>
    public const string Body = "PART_Body";

    /// <summary>标题（TS 的 propsTitle）。</summary>
    public const string Title = "PART_Title";

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public const string Description = "PART_Description";

    /// <summary>结束边（TS 的 end）。</summary>
    public const string End = "PART_End";
}
