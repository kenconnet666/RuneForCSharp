using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Data;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 标题（vue-tsx-pro 的 H1–H6）：各级是一个组件，读屏念出级别（AutomationProperties.HeadingLevel）。级别要连续，不要为了字号跳级：
/// <code>
/// &lt;rune:H2 Size="lg"&gt;收货地址&lt;/rune:H2&gt;   &lt;!-- 文档结构与视觉层级分开：字号用 Size 改 --&gt;
/// </code>
/// 默认字号按级别（H1 xxxl、H2 xxl、H3 xl、H4 lg、H5 md、H6 sm），字重 semibold，标题字体，行高 tight；
/// xxl、xxxl 收紧字间距（letterSpacings.tight、tighter）。默认值在主题里，使用方的样式能改。
/// <see cref="Anchor"/>：有 Name（TS 的 id）时末尾放一个指向本节的 # 按钮（部件 <see cref="HeadingParts.Anchor"/>），悬停标题或键盘聚焦时显示；
/// 点了先发 <see cref="AnchorRequested"/>（文档站拿 Id 复制本节的链接），没有被处理就把这一节滚进视野（TS 的 #id 跳转）。
/// </summary>
public abstract class Heading : Text
{
    /// <summary>末尾放指向本节的 # 按钮（要写 Name）。</summary>
    public static readonly StyledProperty<bool> AnchorProperty = AvaloniaProperty.Register<Heading, bool>(nameof(Anchor));

    /// <summary>点了 # 按钮。</summary>
    public static readonly RoutedEvent<HeadingAnchorEventArgs> AnchorRequestedEvent =
        RoutedEvent.Register<Heading, HeadingAnchorEventArgs>(nameof(AnchorRequested), RoutingStrategies.Bubble);

    IDisposable? letterSpacing;
    ButtonBase? anchor;
    // 重建行内内容时设 Inlines 会改 Text：不再触发重建
    bool composing;

    /// <summary>新建某一级标题。</summary>
    /// <param name="level">级别（1–6）。</param>
    protected Heading(int level) => AutomationProperties.SetHeadingLevel(this, level);

    /// <summary>末尾放指向本节的 # 按钮。</summary>
    public bool Anchor { get => GetValue(AnchorProperty); set => SetValue(AnchorProperty, value); }

    /// <summary>点了 # 按钮：标记 Handled 时不滚动。</summary>
    public event EventHandler<HeadingAnchorEventArgs>? AnchorRequested
    {
        add => AddHandler(AnchorRequestedEvent, value);
        remove => RemoveHandler(AnchorRequestedEvent, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty || change.Property == FontSizeProperty) UpdateLetterSpacing();
        if (change.Property == FontSizeProperty && anchor is not null) SizeAnchor(anchor);
        if (!composing && (change.Property == AnchorProperty || change.Property == NameProperty || change.Property == TextProperty)) Compose();
    }

    /// <summary>有 Anchor 与 Name 时文字后面跟 # 按钮（行内，竖直居中）；否则只有文字。</summary>
    void Compose()
    {
        var text = Text;
        composing = true;
        if (Anchor && !string.IsNullOrEmpty(Name))
        {
            anchor ??= CreateAnchor();
            Inlines = [new Run(text), new InlineUIContainer(anchor) { BaselineAlignment = BaselineAlignment.Center }];
        }
        else if (anchor is not null)
        {
            anchor = null;
            Inlines = null;
            Text = text;
        }
        composing = false;
    }

    ButtonBase CreateAnchor()
    {
        // 外观在代码里设：按钮放在 InlineUIContainer 里，主题与样式实测都选不中它（类选择器也不行）
        var button = new ButtonBase { Name = HeadingParts.Anchor, Background = Brushes.Transparent, Padding = default, MinHeight = 0, Opacity = 0, Content = new Icon { Data = Lucide.Hash } };
        AutomationProperties.SetName(button, Provider.GetLang(this).HeadingAnchor);
        button.Transitions = [new global::Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = Motion.Duration(this, ThemeKeys.Duration.Sm), Easing = new global::Avalonia.Animation.Easings.SplineEasing(0.2, 0, 0, 1) }];
        // textMuted，悬停 accentText
        void Tint(bool hover) => button.Bind(TextElement.ForegroundProperty, this.GetResourceObservable(hover ? ThemeKeys.Brush.AccentText : ThemeKeys.Brush.TextMuted));
        Tint(false);
        button.PointerEntered += (_, _) => Tint(true);
        button.PointerExited += (_, _) => Tint(false);
        // 键盘聚焦时也显示（TS 的 :focus-visible）
        button.GotFocus += (_, e) => { if (e.NavigationMethod == NavigationMethod.Tab) button.Opacity = 1; };
        button.LostFocus += (_, _) => button.Opacity = IsPointerOver ? 1 : 0;
        button.Click += (_, _) =>
        {
            var args = new HeadingAnchorEventArgs(AnchorRequestedEvent, Name ?? "");
            RaiseEvent(args);
            if (!args.Handled) this.BringIntoView();
        };
        SizeAnchor(button);
        return button;
    }

    /// <summary>0.75em 的字号，与文字隔 xs（按按钮的字号），xs 圆角。</summary>
    void SizeAnchor(ButtonBase button)
    {
        var size = 0.75 * FontSize;
        button.SetValue(TextElement.FontSizeProperty, size);
        button.Margin = new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * size, 0, 0, 0);
        button.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Xs) * size);
    }

    /// <summary>悬停标题时显示 # 按钮（TS 的 :hover 祖先选择器）。</summary>
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (anchor is not null) anchor.Opacity = 1;
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (anchor is not null && !anchor.IsKeyboardFocusWithin) anchor.Opacity = 0;
    }

    /// <summary>大字号收紧字间距（em 系数 × 字号），按模板优先级。</summary>
    void UpdateLetterSpacing()
    {
        letterSpacing?.Dispose();
        letterSpacing = null;
        var key = Size.Value switch { "xxl" => ThemeKeys.LetterSpacing.Tight, "xxxl" => ThemeKeys.LetterSpacing.Tighter, _ => null };
        if (key is not null && this.TryFindResource(key, ActualThemeVariant, out var factor) && factor is double em)
            letterSpacing = SetValue(LetterSpacingProperty, em * FontSize, BindingPriority.Template);
    }
}

/// <summary>一级标题：页面标题，默认 xxxl。</summary>
public class H1() : Heading(1);

/// <summary>二级标题：一节，默认 xxl。</summary>
public class H2() : Heading(2);

/// <summary>三级标题：小节，默认 xl。</summary>
public class H3() : Heading(3);

/// <summary>四级标题：段落标题，默认 lg。</summary>
public class H4() : Heading(4);

/// <summary>五级标题：卡片标题，默认 md。</summary>
public class H5() : Heading(5);

/// <summary>六级标题：最小的标题，默认 sm。</summary>
public class H6() : Heading(6);

/// <summary>点了标题的 # 按钮的参数。</summary>
public class HeadingAnchorEventArgs(RoutedEvent routedEvent, string id) : RoutedEventArgs(routedEvent)
{
    /// <summary>这一节的标识（标题的 Name，TS 的 id）。</summary>
    public string Id { get; } = id;
}

/// <summary>标题的部件名。</summary>
public static class HeadingParts
{
    /// <summary>
    /// 指向本节的 # 按钮（TS 的 propsAnchor）：textMuted，悬停 accentText；平时透明，悬停标题、键盘聚焦时显示（durations.sm 渐变）。
    /// 它放在 InlineUIContainer 里，样式实测选不中，外观是代码设的。
    /// </summary>
    public const string Anchor = "PART_Anchor";

}
