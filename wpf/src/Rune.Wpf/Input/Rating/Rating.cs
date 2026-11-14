using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 评分（vue-tsx-pro 的 Rating）：几颗星（或别的图标）里选一个分数，供应商评价、满意度、优先级。可以半星，可以只读显示。
/// <code>
/// &lt;rune:Field Label="交付质量"&gt;&lt;rune:Rating Value="{Binding Score}" AllowHalf="True" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:Rating Value="4.5" IsReadOnly="True" /&gt;
/// &lt;rune:Rating Value="{Binding Like}" Icon="{x:Static rune:Lucide.Heart}" Tone="danger" Count="10" /&gt;
/// </code>
/// 可编辑时是单选组：每个分数是一个单选项（读屏的名称是"3 星"），Tab 落在已选的分数上（没选时第一颗），方向键换分数（到头绕回），空格选中。
/// 指针悬停时预览将要选的分数，再点一次已选的分数清空（<see cref="Clearable"/>，默认开）；<see cref="AllowHalf"/> 时每颗星左右各一半。
/// 只读（<see cref="IsReadOnly"/> 或所在 Field 只读）时是一张图，读屏名称是自己的名称（或字段标签）接上"4.5 星，满分 5 星"。
/// 星是 1.35em 的方块、隔 0.15em：空的是 borderStrong 的描线，选中的按比例填上色调的实色（默认 warning）；指针下的那颗放大到 1.12 倍。
/// </summary>
public class Rating : FrameworkElement, IFieldControl
{
    /// <summary>分数（默认双向绑定）；没有评分时是 null。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double?), typeof(Rating),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((Rating)d).SyncOptions()));

    /// <summary>一共几颗星，默认 5。</summary>
    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(nameof(Count), typeof(int), typeof(Rating),
        new FrameworkPropertyMetadata(5, OnRebuild), count => (int)count > 0);

    /// <summary>可以选半颗。</summary>
    public static readonly DependencyProperty AllowHalfProperty = DependencyProperty.Register(nameof(AllowHalf), typeof(bool), typeof(Rating), new FrameworkPropertyMetadata(false, OnRebuild));

    /// <summary>再点一次已选的分数时清空，默认 true。</summary>
    public static readonly DependencyProperty ClearableProperty = DependencyProperty.Register(nameof(Clearable), typeof(bool), typeof(Rating), new FrameworkPropertyMetadata(true));

    /// <summary>图标，默认星星。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(Rating),
        new FrameworkPropertyMetadata(Lucide.Star, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>选中的颜色，默认 warning（金黄）。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(Rating),
        new FrameworkPropertyMetadata(StatusTone.Warning, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>字号档位：星随之缩放；不写时跟着外面。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Rating),
        new FrameworkPropertyMetadata(default(TextSize), (d, _) => ((Rating)d).ApplySize()));

    /// <summary>只读：只显示分数；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool?), typeof(Rating), new FrameworkPropertyMetadata(null, OnRebuild));

    /// <summary>字号（星按它缩放）：同 TextElement.FontSize，继承外面的。</summary>
    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(Rating),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    readonly VisualCollection children;
    readonly List<RatingOption> options = [];
    double? hover;
    int hoveredStar;

    static Rating() => IsEnabledProperty.OverrideMetadata(typeof(Rating), new UIPropertyMetadata(true, (d, _) => ((Rating)d).InvalidateVisual()));

    /// <summary>新建：五颗星的单选组。</summary>
    public Rating()
    {
        children = new VisualCollection(this);
        Loaded += (_, _) => Rebuild();
        Rebuild();
    }

    /// <summary>分数。</summary>
    public double? Value { get => (double?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>一共几颗星。</summary>
    public int Count { get => (int)GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <summary>可以选半颗。</summary>
    public bool AllowHalf { get => (bool)GetValue(AllowHalfProperty); set => SetValue(AllowHalfProperty, value); }

    /// <summary>再点一次已选的分数时清空。</summary>
    public bool Clearable { get => (bool)GetValue(ClearableProperty); set => SetValue(ClearableProperty, value); }

    /// <summary>图标。</summary>
    public IconData Icon { get => (IconData)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>选中的颜色。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => (bool?)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>每个分数一个单选项（测试用）；只读时没有。</summary>
    internal IReadOnlyList<RatingOption> Options => options;

    /// <summary>第 <paramref name="index"/> 颗星（从 1 开始）按分数填了多少：0、50、100（测试用，悬停预览不算）。</summary>
    internal int Filled(int index) => RatingScale.Filled(Value, index);

    internal bool ReadOnly => IsReadOnly ?? Field.Of(this)?.IsReadOnly ?? false;

    double StarSize => RatingScale.StarEm * FontSize;

    double Pitch => (RatingScale.StarEm + RatingScale.GapEm) * FontSize;

    void IFieldControl.FieldChanged() => Rebuild();

    static void OnRebuild(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Rating)d).Rebuild();

    /// <summary>字号档位换成主题的字号资源（像素值直接用）；不写时继承外面的。</summary>
    void ApplySize()
    {
        if (Size.Pixels is { } pixels) SetValue(FontSizeProperty, pixels);
        else if (Size.ResourceKey is { } key) SetResourceReference(FontSizeProperty, key);
        else ClearValue(FontSizeProperty);
    }

    /// <summary>按分数建单选项（整星一颗一个，半星一颗两个）；只读时一个也没有，整排是一张图。</summary>
    void Rebuild()
    {
        var readOnly = ReadOnly;
        // 已经建好、种类也没变时不重建（Loaded 会再来一次）
        if (options.Count == (readOnly ? 0 : Count * (AllowHalf ? 2 : 1)) && options.All(option => option.Half == AllowHalf)) return;
        foreach (var option in options) RemoveLogicalChild(option);
        children.Clear();
        options.Clear();
        if (!readOnly)
            foreach (var score in RatingScale.Scores(Count, AllowHalf))
            {
                var option = new RatingOption(this, score, AllowHalf);
                options.Add(option);
                children.Add(option);
                AddLogicalChild(option);
            }
        SyncOptions();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>选中的单选项跟着分数；Tab 只落在已选的分数上（没选时第一个），同原生单选组。</summary>
    void SyncOptions()
    {
        var tabStop = options.FirstOrDefault(option => option.Score == Value) ?? options.FirstOrDefault();
        foreach (var option in options)
        {
            option.IsChecked = option.Score == Value;
            KeyboardNavigation.SetIsTabStop(option, option == tabStop);
        }
    }

    /// <summary>点了一个分数（指针、空格）：可清空时再点已选的清空。</summary>
    internal void Pick(double score)
    {
        if (ReadOnly || !IsEnabled) return;
        SetCurrentValue(ValueProperty, RatingScale.Pick(Value, score, Clearable));
        hover = null;
        InvalidateVisual();
    }

    /// <summary>指针移到一个分数上：预览。</summary>
    internal void Preview(double? score)
    {
        hover = IsEnabled ? score : null;
        hoveredStar = score is { } value ? (int)Math.Ceiling(value) : 0;
        InvalidateVisual();
    }

    /// <summary>方向键：换到前一个、后一个分数并选中（到头绕回）；↓ 同 →；从右往左排版时左右对调。</summary>
    internal void Move(RatingOption from, Key key)
    {
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        var delta = key == Key.Down || key == (rtl ? Key.Left : Key.Right) ? 1 : key == Key.Up || key == (rtl ? Key.Right : Key.Left) ? -1 : 0;
        if (delta == 0) return;
        // 还没评分时从聚焦的那个（第一个）算起，同原生单选组
        var next = RatingScale.Step(options.Select(option => option.Score).ToList(), Value ?? from.Score, delta);
        SetCurrentValue(ValueProperty, next);
        options.First(option => option.Score == next).Focus();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        Preview(null);
    }

    /// <inheritdoc />
    protected override int VisualChildrenCount => children.Count;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => children[index];

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => options.GetEnumerator();

    /// <inheritdoc />
    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        foreach (var option in options) option.Measure(new System.Windows.Size(StarSize, StarSize));
        return new System.Windows.Size(Count * StarSize + (Count - 1) * RatingScale.GapEm * FontSize, StarSize);
    }

    /// <inheritdoc />
    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        var top = (finalSize.Height - StarSize) / 2;
        foreach (var option in options)
        {
            var star = (int)Math.Ceiling(option.Score);
            var width = AllowHalf ? StarSize / 2 : StarSize;
            // 半星：起始边一半是 n - 0.5，结束边一半是 n（从右往左排版时 WPF 整个镜像，这里按从左往右排）
            var offset = AllowHalf && option.Score == star ? width : 0;
            option.Arrange(new Rect((star - 1) * Pitch + offset, top, width, StarSize));
        }
        return finalSize;
    }

    /// <summary>
    /// 画星：空心的 borderStrong 描线打底，填色的那层按比例裁出（实色，填满图标）；指针下的那颗放大；
    /// 键盘聚焦的那颗外面一圈 2 像素的焦点环（压进 1 像素）。不可用时整排半透明。
    /// </summary>
    protected override void OnRender(DrawingContext context)
    {
        var size = StarSize;
        var scale = size / IconData.Size;
        var empty = new Pen(this.Resource<Brush>(ThemeKeys.Brush.BorderStrong), 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var brush = this.Resource<Brush>(ToneBrushKey(Tone));
        var fill = new Pen(brush, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var shown = hover ?? Value;
        var focused = options.FirstOrDefault(option => option.ShowsFocusRing);
        var top = (RenderSize.Height - size) / 2;
        context.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (!IsEnabled) context.PushOpacity(this.Resource<double>(ThemeKeys.Opacity.Disabled));
        for (var index = 1; index <= Count; index++)
        {
            var origin = new Point((index - 1) * Pitch, top);
            var zoom = !ReadOnly && index == hoveredStar && hover is not null ? RatingScale.HoverScale : 1;
            context.PushTransform(new ScaleTransform(zoom, zoom, origin.X + size / 2, origin.Y + size / 2));
            if (focused is not null && (int)Math.Ceiling(focused.Score) == index)
            {
                var radius = this.Resource<double>(ThemeKeys.Radius.Sm) * FontSize;
                var ring = new Rect(origin, new System.Windows.Size(size, size));
                ring.Inflate(1, 1);
                context.DrawRoundedRectangle(null, new Pen(this.Resource<Brush>(ThemeKeys.Brush.FocusRing), 2), ring, radius, radius);
            }
            var place = new MatrixTransform(scale, 0, 0, scale, origin.X, origin.Y);
            context.PushTransform(place);
            context.DrawGeometry(null, empty, Icon.Geometry);
            context.Pop();
            var percent = RatingScale.Filled(shown, index);
            if (percent > 0)
            {
                context.PushClip(new RectangleGeometry(new Rect(origin, new System.Windows.Size(size * percent / 100.0, size))));
                context.PushTransform(place);
                context.DrawGeometry(brush, fill, Icon.Geometry);
                context.Pop();
                context.Pop();
            }
            context.Pop();
        }
        if (!IsEnabled) context.Pop();
    }

    /// <summary>选中的颜色：各色调的实色（金黄的星、红色的心），neutral 是 textMuted。实心与空心靠形状区分，不只靠颜色。</summary>
    static string ToneBrushKey(StatusTone tone) => tone.Value switch
    {
        "accent" => ThemeKeys.Brush.Accent,
        "info" => ThemeKeys.Brush.Info,
        "success" => ThemeKeys.Brush.Success,
        "danger" => ThemeKeys.Brush.Danger,
        "neutral" => ThemeKeys.Brush.TextMuted,
        _ => ThemeKeys.Brush.Warning,
    };

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new RatingPeer(this);

    /// <summary>
    /// 读屏：可编辑时是单选组（名称取所在字段的标签，说明与错误是帮助文字），子节点是各个分数；
    /// 只读时是一张图，名称里总有分数：自己的名称或字段标签后面接上"4.5 星，满分 5 星"。
    /// </summary>
    sealed class RatingPeer(Rating owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.ReadOnly ? AutomationControlType.Image : AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Rating);

        protected override string GetHelpTextCore() => Field.DescribedBy(owner, base.GetHelpTextCore());

        protected override string GetNameCore()
        {
            var own = base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null;
            if (!owner.ReadOnly) return own ?? "";
            var summary = Provider.GetLang(owner).RatingSummary(owner.Value, owner.Count);
            return string.IsNullOrEmpty(own) ? summary : $"{own} {summary}";
        }
    }
}

/// <summary>评分里的一个分数（单选项）：透明的点击、聚焦区域，盖在星（整颗或半颗）上，画由 <see cref="Rating"/> 负责。</summary>
public sealed class RatingOption : FrameworkElement
{
    readonly Rating owner;
    bool focusVisible;

    internal RatingOption(Rating owner, double score, bool half)
    {
        this.owner = owner;
        Score = score;
        Half = half;
        Focusable = true;
        FocusVisualStyle = null;
        Cursor = Cursors.Hand;
    }

    /// <summary>这个单选项的分数。</summary>
    public double Score { get; }

    /// <summary>选中了（分数就是它）。</summary>
    public bool IsChecked { get; internal set; }

    /// <summary>是半颗星的一半。</summary>
    internal bool Half { get; }

    /// <summary>键盘聚焦着（画焦点环）。</summary>
    internal bool ShowsFocusRing => focusVisible && IsKeyboardFocused;

    // 透明也要接住指针
    /// <inheritdoc />
    protected override void OnRender(DrawingContext context) => context.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        owner.Preview(Score);
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        owner.Pick(Score);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        if (e.Key == Key.Space)
        {
            owner.Pick(Score);
            e.Handled = true;
        }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            owner.Move(this, e.Key);
            e.Handled = true;
        }
    }

    /// <summary>键盘聚焦时画焦点环（指针点进来不画，同 :focus-visible）。</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        focusVisible = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
        owner.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        focusVisible = false;
        owner.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new OptionPeer(this);

    /// <summary>读屏：单选项，名称是"3 星"，选中与否同分数。</summary>
    sealed class OptionPeer(RatingOption option) : FrameworkElementAutomationPeer(option), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        protected override string GetClassNameCore() => nameof(RatingOption);

        protected override string GetNameCore() => Provider.GetLang(option.owner).RatingStar(option.Score);

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);

        public bool IsSelected => option.IsChecked;

        public IRawElementProviderSimple? SelectionContainer => null;

        public void Select()
        {
            if (!option.IsChecked) option.owner.Pick(option.Score);
        }

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }
    }
}
