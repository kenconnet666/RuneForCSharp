using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

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
public class Rating : Control, IFieldControl
{
    /// <summary>分数（默认双向绑定）；没有评分时是 null。</summary>
    public static readonly StyledProperty<double?> ValueProperty = AvaloniaProperty.Register<Rating, double?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>一共几颗星，默认 5。</summary>
    public static readonly StyledProperty<int> CountProperty = AvaloniaProperty.Register<Rating, int>(nameof(Count), 5, validate: count => count > 0);

    /// <summary>可以选半颗。</summary>
    public static readonly StyledProperty<bool> AllowHalfProperty = AvaloniaProperty.Register<Rating, bool>(nameof(AllowHalf));

    /// <summary>再点一次已选的分数时清空，默认 true。</summary>
    public static readonly StyledProperty<bool> ClearableProperty = AvaloniaProperty.Register<Rating, bool>(nameof(Clearable), true);

    /// <summary>图标，默认星星。</summary>
    public static readonly StyledProperty<IconData> IconProperty = AvaloniaProperty.Register<Rating, IconData>(nameof(Icon), Lucide.Star);

    /// <summary>选中的颜色，默认 warning（金黄）。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Rating, StatusTone>(nameof(Tone), StatusTone.Warning);

    /// <summary>字号档位：星随之缩放；不写时跟着外面。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Rating, TextSize>(nameof(Size));

    /// <summary>只读：只显示分数；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsReadOnlyProperty = AvaloniaProperty.Register<Rating, bool?>(nameof(IsReadOnly));

    /// <summary>字号（星按它缩放）：同 TextElement.FontSize，继承外面的。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = global::Avalonia.Controls.Documents.TextElement.FontSizeProperty.AddOwner<Rating>();

    readonly TemplateResources resources;
    readonly List<RatingOption> options = [];
    double? hover;
    int hoveredStar;

    static Rating()
    {
        AffectsRender<Rating>(ValueProperty, IconProperty, ToneProperty);
        AffectsMeasure<Rating>(FontSizeProperty, CountProperty);
    }

    /// <summary>新建：五颗星的单选组。</summary>
    public Rating()
    {
        resources = new TemplateResources(this);
        this.RedrawOnResourceChanges(this);
        Rebuild();
    }

    /// <summary>分数。</summary>
    public double? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>一共几颗星。</summary>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <summary>可以选半颗。</summary>
    public bool AllowHalf { get => GetValue(AllowHalfProperty); set => SetValue(AllowHalfProperty, value); }

    /// <summary>再点一次已选的分数时清空。</summary>
    public bool Clearable { get => GetValue(ClearableProperty); set => SetValue(ClearableProperty, value); }

    /// <summary>图标。</summary>
    public IconData Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>选中的颜色。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>每个分数一个单选项（测试用）；只读时没有。</summary>
    internal IReadOnlyList<RatingOption> Options => options;

    /// <summary>第 <paramref name="index"/> 颗星（从 1 开始）按分数填了多少：0、50、100（测试用，悬停预览不算）。</summary>
    internal int Filled(int index) => RatingScale.Filled(Value, index);

    internal bool ReadOnly => IsReadOnly ?? Field.Of(this)?.IsReadOnly ?? false;

    double Em => FontSize;

    double StarSize => RatingScale.StarEm * Em;

    double Pitch => (RatingScale.StarEm + RatingScale.GapEm) * Em;

    void IFieldControl.FieldChanged() => Rebuild();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty || change.Property == AllowHalfProperty || change.Property == IsReadOnlyProperty || change.Property == Field.OwnerProperty) Rebuild();
        else if (change.Property == ValueProperty) SyncOptions();
        else if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == IsEffectivelyEnabledProperty) InvalidateVisual();
    }

    /// <summary>按分数建单选项（整星一颗一个，半星一颗两个）；只读时一个也没有，整排是一张图。</summary>
    void Rebuild()
    {
        foreach (var option in options) VisualChildren.Remove(option);
        LogicalChildren.RemoveAll(options);
        options.Clear();
        if (!ReadOnly)
            foreach (var score in RatingScale.Scores(Count, AllowHalf))
            {
                var option = new RatingOption(this, score);
                options.Add(option);
                VisualChildren.Add(option);
                LogicalChildren.Add(option);
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
        if (ReadOnly || !IsEffectivelyEnabled) return;
        SetCurrentValue(ValueProperty, RatingScale.Pick(Value, score, Clearable));
        hover = null;
        InvalidateVisual();
    }

    /// <summary>指针移到一个分数上：预览。</summary>
    internal void Preview(double? score, int star)
    {
        hover = IsEffectivelyEnabled ? score : null;
        hoveredStar = score is null ? 0 : star;
        InvalidateVisual();
    }

    /// <summary>方向键：换到前一个、后一个分数并选中（到头绕回）；↓ 同 →；从右往左排版时左右对调。</summary>
    internal void Move(RatingOption from, Key key)
    {
        var forward = FlowDirection == FlowDirection.RightToLeft ? Key.Left : Key.Right;
        var back = FlowDirection == FlowDirection.RightToLeft ? Key.Right : Key.Left;
        var delta = key == forward || key == Key.Down ? 1 : key == back || key == Key.Up ? -1 : 0;
        if (delta == 0) return;
        // 还没评分时从聚焦的那个（第一个）算起，同原生单选组
        var next = RatingScale.Step(options.Select(option => option.Score).ToList(), Value ?? from.Score, delta);
        SetCurrentValue(ValueProperty, next);
        options.First(option => option.Score == next).Focus(NavigationMethod.Directional);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Preview(null, 0);
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        foreach (var option in options) option.Measure(LayoutSize.Infinity);
        return new LayoutSize(Count * StarSize + (Count - 1) * RatingScale.GapEm * Em, StarSize);
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        foreach (var option in options)
        {
            var star = (int)Math.Ceiling(option.Score);
            var x = (star - 1) * Pitch;
            var half = AllowHalf ? StarSize / 2 : StarSize;
            // 半星：起始边一半是 n - 0.5，结束边一半是 n（从右往左排版时 Avalonia 整个镜像，这里按从左往右排）
            var offset = AllowHalf && option.Score == star ? half : 0;
            option.Arrange(new Rect(x + offset, (finalSize.Height - StarSize) / 2, half, StarSize));
        }
        return finalSize;
    }

    /// <summary>
    /// 画星：空心的 borderStrong 描线打底，填色的那层按比例裁出（currentColor 的实色，填满图标）；指针下的那颗放大；
    /// 键盘聚焦的那颗外面一圈 2 像素的焦点环（压进 1 像素）。不可用时整排半透明。
    /// </summary>
    public override void Render(DrawingContext context)
    {
        var size = StarSize;
        var scale = size / IconData.Size;
        var empty = this.Resource<IBrush>(ThemeKeys.Brush.BorderStrong);
        var fill = this.Resource<IBrush>(ToneBrushKey(Tone));
        var shown = hover ?? Value;
        var focused = options.FirstOrDefault(option => option.IsFocused && option.ShowsFocusRing);
        using var opacity = IsEffectivelyEnabled ? default : context.PushOpacity(this.Resource<double>(ThemeKeys.Opacity.Disabled));
        for (var index = 1; index <= Count; index++)
        {
            var origin = new Point((index - 1) * Pitch, (Bounds.Height - size) / 2);
            var zoom = !ReadOnly && index == hoveredStar && hover is not null ? RatingScale.HoverScale : 1;
            var center = new Point(origin.X + size / 2, origin.Y + size / 2);
            using var hovered = context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(zoom, zoom) * Matrix.CreateTranslation(center.X, center.Y));
            if (focused is not null && (int)Math.Ceiling(focused.Score) == index)
            {
                var ring = new Rect(origin, new LayoutSize(size, size)).Deflate(-1);
                var radius = this.Resource<double>(ThemeKeys.Radius.Sm) * Em;
                context.DrawRectangle(null, new Pen(this.Resource<IBrush>(ThemeKeys.Brush.FocusRing), 2), new RoundedRect(ring, radius));
            }
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y)))
                context.DrawGeometry(null, new Pen(empty, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), Icon.Geometry);
            var percent = RatingScale.Filled(shown, index);
            if (percent == 0) continue;
            var visible = new Rect(origin, new LayoutSize(size * percent / 100.0, size));
            using (context.PushClip(visible))
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y)))
                context.DrawGeometry(fill, new Pen(fill, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), Icon.Geometry);
        }
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
    sealed class RatingPeer(Rating owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.ReadOnly ? AutomationControlType.Image : AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Rating);

        protected override string? GetHelpTextCore() => Field.DescribedBy(owner, base.GetHelpTextCore());

        protected override string? GetNameCore()
        {
            var own = base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null;
            if (!owner.ReadOnly) return own;
            var summary = Provider.GetLang(owner).RatingSummary(owner.Value, owner.Count);
            return string.IsNullOrEmpty(own) ? summary : $"{own} {summary}";
        }
    }
}

/// <summary>评分里的一个分数（单选项）：透明的点击、聚焦区域，盖在星（整颗或半颗）上，画由 <see cref="Rating"/> 负责。</summary>
public sealed class RatingOption : Control
{
    readonly Rating owner;
    bool focusVisible;

    internal RatingOption(Rating owner, double score)
    {
        this.owner = owner;
        Score = score;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>这个单选项的分数。</summary>
    public double Score { get; }

    /// <summary>选中了（分数就是它）。</summary>
    public bool IsChecked { get; internal set; }

    /// <summary>键盘聚焦（画焦点环）。</summary>
    internal bool ShowsFocusRing => focusVisible;

    // 透明也要接住指针
    /// <inheritdoc />
    public override void Render(DrawingContext context) => context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        owner.Preview(Score, (int)Math.Ceiling(Score));
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(NavigationMethod.Pointer);
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

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        focusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        owner.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        focusVisible = false;
        owner.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new OptionPeer(this);

    /// <summary>读屏：单选项，名称是"3 星"，选中与否同分数。</summary>
    sealed class OptionPeer(RatingOption option) : ControlAutomationPeer(option), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        protected override string GetClassNameCore() => nameof(RatingOption);

        protected override string? GetNameCore() => Provider.GetLang(option.owner).RatingStar(option.Score);

        public bool IsSelected => option.IsChecked;

        public ISelectionProvider? SelectionContainer => null;

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
