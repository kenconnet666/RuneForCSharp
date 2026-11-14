using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf;

/// <summary>
/// 条目列表（vue-tsx-pro 的 ItemList）：一行一条，起始边是图标、色点或头像，中间是标题与说明，结束边是标记、数字或操作。
/// 待办、通知、我负责的任务这类"界面里的列表"（正文里的列表用 <see cref="List"/>）。
/// <code>
/// &lt;rune:ItemList Divided="True"&gt;
///   &lt;rune:ItemListItem Title="资料审核" Description="PO-2026-0187 · 官网改版" /&gt;
/// &lt;/rune:ItemList&gt;
/// </code>
/// <see cref="Divided"/> 在条目之间画线，<see cref="Hoverable"/> 悬停时着底色，<see cref="Inset"/> 是条目两侧的内边距（放在弹出层、卡片里时与容器对齐；
/// 悬停着底色、整条可点的条目不写时留 sm）。里面的链接悬停才有下划线（一列下来都带下划线太重）。读屏里是列表。
/// </summary>
public class ItemList : StackPanel
{
    /// <summary>条目之间画线（borderMuted，最后一条下面没有）。</summary>
    public static readonly DependencyProperty DividedProperty = DependencyProperty.Register(nameof(Divided), typeof(bool), typeof(ItemList),
        new PropertyMetadata(false, (d, _) => ((ItemList)d).ApplyLooks()));

    /// <summary>悬停时着底色：整条可点时。</summary>
    public static readonly DependencyProperty HoverableProperty = DependencyProperty.Register(nameof(Hoverable), typeof(bool), typeof(ItemList),
        new PropertyMetadata(false, (d, _) => ((ItemList)d).ApplyLooks()));

    /// <summary>疏密，默认 standard。</summary>
    public static readonly DependencyProperty DensityProperty = DependencyProperty.Register(nameof(Density), typeof(ItemListDensity), typeof(ItemList),
        new PropertyMetadata(ItemListDensity.Standard, (d, _) => ((ItemList)d).ApplyLooks()));

    /// <summary>条目两侧的内边距；不写时悬停着底色、整条可点的条目留 sm，其余不留。</summary>
    public static readonly DependencyProperty InsetProperty = DependencyProperty.Register(nameof(Inset), typeof(Space?), typeof(ItemList),
        new PropertyMetadata(null, (d, _) => ((ItemList)d).ApplyLooks()));

    /// <summary>新建：里面的链接悬停才有下划线（上游 ItemList 给 LinkUnderlineContext 的 hover）。</summary>
    public ItemList() => SetCurrentValue(Link.UnderlineScopeProperty, LinkUnderline.Hover);

    /// <summary>条目之间画线。</summary>
    public bool Divided { get => (bool)GetValue(DividedProperty); set => SetValue(DividedProperty, value); }

    /// <summary>悬停时着底色。</summary>
    public bool Hoverable { get => (bool)GetValue(HoverableProperty); set => SetValue(HoverableProperty, value); }

    /// <summary>疏密。</summary>
    public ItemListDensity Density { get => (ItemListDensity)GetValue(DensityProperty); set => SetValue(DensityProperty, value); }

    /// <summary>条目两侧的内边距。</summary>
    public Space? Inset { get => (Space?)GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

    /// <summary>条目增减：最后一条变了，分隔线跟着换。</summary>
    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        ApplyLooks();
    }

    void ApplyLooks()
    {
        foreach (var item in Children.OfType<ItemListItem>()) item.ApplyLook();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ItemListAutomationPeer(this);

    sealed class ItemListAutomationPeer(ItemList owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => nameof(ItemList);
    }
}

/// <summary>
/// 条目列表的一条（vue-tsx-pro 的 ItemListItem）：<see cref="Start"/>、<see cref="Title"/> 与 <see cref="Description"/>、<see cref="End"/> 三列，
/// 说明下面放 Content（更多内容）。标题与说明放不下时换行；<see cref="Align"/> 为 start 时起始边、结束边贴着第一行。
/// 整条可点：写 <see cref="Href"/>（绝对地址，系统打开）、Command，或订阅 <see cref="Invoked"/>（TS 的 onSelect），点哪里都一样；
/// 结束边的按钮照常可点，点它不触发这一条。<see cref="IsSelected"/> 是正在看的一条（主从视图左边的列表）：accentSoft 底，读屏的项状态是"当前"。
/// 不可用（IsEnabled=False）只对整条可点的条目有外观：变淡、点不了。不可点的条目不能聚焦，读屏里是列表项。
/// </summary>
[TemplatePart(Name = "PART_Start", Type = typeof(ContentPresenter))]
public class ItemListItem : ButtonBase
{
    /// <summary>起始边：图标、色点、头像、状态。</summary>
    public static readonly DependencyProperty StartProperty = DependencyProperty.Register(nameof(Start), typeof(object), typeof(ItemListItem),
        new PropertyMetadata(null, (d, _) => ((ItemListItem)d).ApplyLook()));

    /// <summary>标题：一句话。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(object), typeof(ItemListItem));

    /// <summary>标题下的说明（textMuted、xs）。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(object), typeof(ItemListItem));

    /// <summary>结束边：标记、数字、时间、操作。</summary>
    public static readonly DependencyProperty EndProperty = DependencyProperty.Register(nameof(End), typeof(object), typeof(ItemListItem));

    /// <summary>起始边、结束边与内容的对齐，默认 center。</summary>
    public static readonly DependencyProperty AlignProperty = DependencyProperty.Register(nameof(Align), typeof(ItemListAlign), typeof(ItemListItem),
        new PropertyMetadata(ItemListAlign.Center, (d, _) => ((ItemListItem)d).ApplyLook()));

    /// <summary>整条是链接（绝对地址）：点了用系统打开。</summary>
    public static readonly DependencyProperty HrefProperty = DependencyProperty.Register(nameof(Href), typeof(Uri), typeof(ItemListItem),
        new PropertyMetadata(null, (d, _) => ((ItemListItem)d).ApplyLook()));

    /// <summary>正在看的一条。</summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ItemListItem));

    static readonly DependencyPropertyKey IsInteractiveKey = DependencyProperty.RegisterReadOnly(nameof(IsInteractive), typeof(bool), typeof(ItemListItem), new PropertyMetadata(false));

    /// <summary>整条可点：有地址、命令或订阅了 Invoked（主题的触发器按它）。</summary>
    public static readonly DependencyProperty IsInteractiveProperty = IsInteractiveKey.DependencyProperty;

    static readonly DependencyPropertyKey IsHoverableKey = DependencyProperty.RegisterReadOnly(nameof(IsHoverable), typeof(bool), typeof(ItemListItem), new PropertyMetadata(false));

    /// <summary>所在的列表悬停时着底色（主题的触发器按它）。</summary>
    public static readonly DependencyProperty IsHoverableProperty = IsHoverableKey.DependencyProperty;

    static readonly DependencyPropertyKey HasDividerKey = DependencyProperty.RegisterReadOnly(nameof(HasDivider), typeof(bool), typeof(ItemListItem), new PropertyMetadata(false));

    /// <summary>下面画分隔线：所在的列表 Divided，且不是最后一条（主题的触发器按它）。</summary>
    public static readonly DependencyProperty HasDividerProperty = HasDividerKey.DependencyProperty;

    EventHandler<RoutedEventArgs>? invoked;
    ContentPresenter? start;
    ContentPresenter? description;

    static ItemListItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ItemListItem), new FrameworkPropertyMetadata(typeof(ItemListItem)));
        CommandProperty.OverrideMetadata(typeof(ItemListItem), new FrameworkPropertyMetadata(null, (d, _) => ((ItemListItem)d).ApplyLook()));
    }

    /// <summary>新建：不出波纹（上游 ripple: false）。</summary>
    public ItemListItem() => SetCurrentValue(RippleProperty, false);

    /// <summary>起始边。</summary>
    public object? Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <summary>标题。</summary>
    public object? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>结束边。</summary>
    public object? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>对齐。</summary>
    public ItemListAlign Align { get => (ItemListAlign)GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>链接地址。</summary>
    public Uri? Href { get => (Uri?)GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>正在看的一条。</summary>
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>整条可点。</summary>
    public bool IsInteractive => (bool)GetValue(IsInteractiveProperty);

    /// <summary>所在的列表悬停时着底色。</summary>
    public bool IsHoverable => (bool)GetValue(IsHoverableProperty);

    /// <summary>下面画分隔线。</summary>
    public bool HasDivider => (bool)GetValue(HasDividerProperty);

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

    /// <summary>焦点环叠在条目的边上（上游 outline 宽 sm、偏移 -1px），不像按钮那样外扩。</summary>
    private protected override double FocusRingOutset => 1;

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        start = GetTemplateChild(ItemListItemParts.StartName) as ContentPresenter;
        description = GetTemplateChild("PART_Description") as ContentPresenter;
        ApplyLook();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) ApplyLook();
    }

    /// <inheritdoc />
    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        ApplyLook();
    }

    /// <summary>
    /// 按所在列表的设置与自己的状态换外观：状态给主题的触发器，内边距（上下按疏密，两侧按 Inset）是 em 系数；
    /// 对齐 start 时起始边至少一行高（内容在第一行里居中）；只有整条可点的条目能聚焦。
    /// </summary>
    internal void ApplyLook()
    {
        var list = Parent as ItemList;
        var interactive = Href is not null || Command is not null || invoked is not null;
        var hoverable = list?.Hoverable ?? false;
        SetValue(IsInteractiveKey, interactive);
        SetValue(IsHoverableKey, hoverable);
        SetValue(HasDividerKey, list is { Divided: true } && list.Children.OfType<ItemListItem>().LastOrDefault() != this);
        var block = this.Resource<double>(list?.Density == ItemListDensity.Compact ? ThemeKeys.Space.Xs : ThemeKeys.Space.Sm);
        var gutter = (list?.Inset ?? (hoverable || interactive ? Space.Sm : null)) is { ResourceKey: { } key } ? this.Resource<double>(key) : 0;
        SetCurrentValue(EmSize.PaddingProperty, new Thickness(gutter, block, gutter, block));
        SetCurrentValue(FocusableProperty, interactive);
        if (start is not null) start.MinHeight = Align == ItemListAlign.Start ? this.Resource<double>(ThemeKeys.LineHeight.Normal) * FontSize : 0;
        // 说明是普通文字（不是 Text）：WPF 的行高按像素继承，按它自己的 xs 字号重算（上游 12px 的说明行高 18）
        if (description is not null) TextBlock.SetLineHeight(description, Leading.GetFactor(this) * TextElement.GetFontSize(description));
    }

    /// <summary>先触发 Click、Command，再发 Invoked、打开链接；不可点、加载中、不可用时都不执行。</summary>
    protected override void OnClick()
    {
        if (!IsInteractive || IsLoading || IsDisabled) return;
        base.OnClick();
        invoked?.Invoke(this, new RoutedEventArgs());
        if (Href is { IsAbsoluteUri: true } href) LinkLauncher.Open(href);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ItemListItemAutomationPeer(this);

    /// <summary>读屏：可点时是链接或按钮（名称是标题，帮助文字是说明），否则是列表项；正在看的一条项状态是"当前"。</summary>
    sealed class ItemListItemAutomationPeer(ItemListItem owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            owner.Href is not null ? AutomationControlType.Hyperlink : owner.IsInteractive ? AutomationControlType.Button : AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(ItemListItem);

        protected override string GetNameCore() =>
            AutomationProperties.GetName(owner) is { Length: > 0 } name ? name : owner.Title as string ?? base.GetNameCore();

        protected override string GetHelpTextCore() =>
            AutomationProperties.GetHelpText(owner) is { Length: > 0 } help ? help : owner.Description as string ?? "";

        protected override string GetItemStatusCore() =>
            owner.IsSelected ? DesktopMessages.For(Provider.GetLang(owner)).Current : base.GetItemStatusCore();
    }
}

/// <summary>ItemListItem 的部件：标题、说明有部件样式（DESIGN §7），起始边、结束边是模板里的元素名。</summary>
public static class ItemListItemParts
{
    /// <summary>标题（TS 的 propsTitle）。</summary>
    public static readonly PartKey Title = new(typeof(ItemListItem), nameof(Title), typeof(ContentPresenter));

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public static readonly PartKey Description = new(typeof(ItemListItem), nameof(Description), typeof(ContentPresenter));

    /// <summary>起始边（模板里的元素名）。</summary>
    public const string StartName = "PART_Start";

    /// <summary>结束边（模板里的元素名）。</summary>
    public const string EndName = "PART_End";
}
