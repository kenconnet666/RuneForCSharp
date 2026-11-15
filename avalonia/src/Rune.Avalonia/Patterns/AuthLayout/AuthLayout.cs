using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Rune.Avalonia;

/// <summary>
/// 认证页的整页版式（vue-tsx-pro 的 AuthLayout）：登录、注册、找回密码。宽时左边是品牌区（Hero）、右边是表单，窄时品牌区收成顶部一块、表单紧跟其后。
/// <code>
/// &lt;rune:AuthLayout&gt;
///   &lt;rune:AuthLayout.Hero&gt;&lt;local:LoginHero /&gt;&lt;/rune:AuthLayout.Hero&gt;
///   &lt;rune:AuthLayout.Tools&gt;&lt;local:ThemeToggle /&gt;&lt;/rune:AuthLayout.Tools&gt;
///   &lt;rune:Card&gt;……登录表单……&lt;/rune:Card&gt;
/// &lt;/rune:AuthLayout&gt;
/// </code>
/// Hero 是品牌区的内容（标志、口号、背景），铺满它那一边；不写时表单居中，不分栏。Tools 放在整页的结束边上角（主题切换、语言切换）：
/// 宽时就是表单那一边的上角，窄时落在顶部品牌区那一条上，不压住表单。表单区把内容垂直居中（宽时）或贴近顶部（窄时，登录卡片不被品牌区挤出窗口）。
/// 分栏的断点是 64rem（sizes.lg，与 AppShell 一致），按自己的宽度算。部件：<see cref="AuthLayoutParts"/>。
/// </summary>
public class AuthLayout : Control
{
    /// <summary>品牌区：宽时在开始边一半，窄时在顶部；不写时不分栏。</summary>
    public static readonly StyledProperty<object?> HeroProperty = AvaloniaProperty.Register<AuthLayout, object?>(nameof(Hero));

    /// <summary>整页结束边上角的工具（主题切换、语言切换）。</summary>
    public static readonly StyledProperty<object?> ToolsProperty = AvaloniaProperty.Register<AuthLayout, object?>(nameof(Tools));

    /// <summary>表单。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<AuthLayout, object?>(nameof(Content));

    /// <summary>整页的底色，默认 background（主题样式写在 AuthLayoutParts.axaml）。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<AuthLayout>();

    /// <summary>宽于这个时左右分栏（sizes.lg）。</summary>
    static readonly Length Breakpoint = new("64rem");

    readonly Border hero = new() { Name = AuthLayoutParts.Hero, IsVisible = false };
    readonly ContentPresenter heroContent = new();
    readonly Border main = new() { Name = AuthLayoutParts.Main };
    readonly ContentPresenter mainContent = new() { HorizontalAlignment = HorizontalAlignment.Center };
    readonly ContentPresenter tools = new() { Name = AuthLayoutParts.Tools, IsVisible = false };
    bool wide;

    static AuthLayout()
    {
        AffectsMeasure<AuthLayout>(global::Avalonia.Controls.Documents.TextElement.FontSizeProperty);
        AffectsRender<AuthLayout>(BackgroundProperty);
    }

    /// <summary>新建：品牌区、表单区、工具区（工具区在最上层）。</summary>
    public AuthLayout()
    {
        hero.Child = heroContent;
        main.Child = mainContent;
        LogicalChildren.AddRange([hero, main, tools]);
        VisualChildren.AddRange([hero, main, tools]);
    }

    /// <summary>品牌区。</summary>
    public object? Hero { get => GetValue(HeroProperty); set => SetValue(HeroProperty, value); }

    /// <summary>工具。</summary>
    public object? Tools { get => GetValue(ToolsProperty); set => SetValue(ToolsProperty, value); }

    /// <summary>表单。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>整页的底色。</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>现在左右分栏（测试用）。</summary>
    internal bool Wide => wide;

    double Em => global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);

    double Space(string key) => this.Resource<double>(key) * Em;

    bool Split => Hero is not null;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeroProperty)
        {
            heroContent.Content = Hero;
            hero.IsVisible = Hero is not null;
            InvalidateMeasure();
        }
        else if (change.Property == ToolsProperty)
        {
            tools.Content = Tools;
            tools.IsVisible = Tools is not null;
        }
        else if (change.Property == ContentProperty) mainContent.Content = Content;
    }

    /// <summary>表单区：四周 lg，窄时顶部 xl；宽时内容垂直居中，窄时贴近顶部（Border 没有字号，em 在这里算）。</summary>
    void SyncMain()
    {
        var lg = Space(ThemeKeys.Space.Lg);
        main.Padding = new Thickness(lg, wide || !Split ? lg : Space(ThemeKeys.Space.Xl), lg, lg);
        mainContent.VerticalAlignment = wide || !Split ? VerticalAlignment.Center : VerticalAlignment.Top;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        wide = Split && !double.IsInfinity(availableSize.Width) && availableSize.Width >= Breakpoint.Pixels(Em);
        SyncMain();
        tools.Measure(availableSize);
        if (!Split)
        {
            main.Measure(availableSize);
            return main.DesiredSize;
        }
        if (wide)
        {
            var (start, end) = Columns(availableSize.Width);
            hero.Measure(availableSize.WithWidth(start));
            main.Measure(availableSize.WithWidth(end));
            return new Size(availableSize.Width, Math.Max(hero.DesiredSize.Height, main.DesiredSize.Height));
        }
        hero.Measure(availableSize.WithHeight(double.PositiveInfinity));
        main.Measure(availableSize.WithHeight(double.PositiveInfinity));
        return new Size(Math.Max(hero.DesiredSize.Width, main.DesiredSize.Width), hero.DesiredSize.Height + main.DesiredSize.Height);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Background is { } background) context.FillRectangle(background, new Rect(Bounds.Size));
    }

    /// <summary>左右两栏的宽度：1.15 : 1。</summary>
    static (double Start, double End) Columns(double width) => (width * 1.15 / 2.15, width / 2.15);

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!Split) main.Arrange(new Rect(finalSize));
        else if (wide)
        {
            // 从右往左时平台整体镜像，品牌区到右边
            var (start, end) = Columns(finalSize.Width);
            hero.Arrange(new Rect(0, 0, start, finalSize.Height));
            main.Arrange(new Rect(start, 0, end, finalSize.Height));
        }
        else
        {
            hero.Arrange(new Rect(0, 0, finalSize.Width, hero.DesiredSize.Height));
            main.Arrange(new Rect(0, hero.DesiredSize.Height, finalSize.Width, Math.Max(main.DesiredSize.Height, finalSize.Height - hero.DesiredSize.Height)));
        }
        // 工具区按整页定位：结束边上角，离两边 md
        var md = Space(ThemeKeys.Space.Md);
        tools.Arrange(new Rect(Math.Max(0, finalSize.Width - md - tools.DesiredSize.Width), md, tools.DesiredSize.Width, tools.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>AuthLayout 的部件名（TS 的部件）。外层（TS 的 root）是版式自己（background 底）。</summary>
public static class AuthLayoutParts
{
    /// <summary>品牌区（TS 的 propsHero）：Border，铺满它那一边（窄时按内容高）。</summary>
    public const string Hero = "PART_Hero";

    /// <summary>表单区（TS 的 propsMain）：Border，四周 lg（窄时顶部 xl）。</summary>
    public const string Main = "PART_Main";

    /// <summary>工具区（TS 的 propsTools）：ContentPresenter，整页结束边上角、离两边 md、在最上层。</summary>
    public const string Tools = "PART_Tools";
}
