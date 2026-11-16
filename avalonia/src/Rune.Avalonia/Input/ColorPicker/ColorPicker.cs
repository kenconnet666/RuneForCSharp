using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 取色器面板（vue-tsx-pro 的 ColorPicker）：饱和度 / 明度的色板、色相与不透明度滑块、按格式输入、预设与最近使用。
/// 平铺在页面上（主题编辑器）或放进 <see cref="ColorField"/> 的弹出层。
/// <code>
/// &lt;rune:ColorPicker Value="{Binding Brand}" Presets="#3e63dd,#e5484d,#30a46c" /&gt;
/// &lt;rune:ColorPicker Value="{Binding Overlay}" Alpha="False" /&gt;    不要透明度
/// </code>
/// <list type="bullet">
/// <item>值是 #rrggbb（有透明度时 #rrggbbaa），同 ColorField；内部按 HSV 记着，灰色、黑色时色相不丢。</item>
/// <item>色板：按下即跳过去并接着拖；聚焦后 ←→ 改饱和度、↑↓ 改明度（Shift 一次 10%），读屏念"饱和度 80%，明度 45%"。色相、不透明度是 <see cref="Slider"/>，轨道画成渐变。</item>
/// <item>格式在 HEX、RGB、HSL 之间切，输入框里写任何 CSS 颜色都认（回车或失焦时写回）。上游的吸管（浏览器的 EyeDropper）桌面上没有。</item>
/// <item>最近使用：同一个应用里的取色器共用，最多 8 个，拖完一次、选了预设、输入后记下。</item>
/// </list>
/// 部件：<see cref="ColorPickerParts"/>。
/// </summary>
public class ColorPicker : Control
{
    /// <summary>颜色值（#rrggbb 或 #rrggbbaa，双向绑定），默认 #3e63dd。</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<ColorPicker, string>(nameof(Value), "#3e63dd", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>有不透明度滑块，默认 true。</summary>
    public static readonly StyledProperty<bool> AlphaProperty = AvaloniaProperty.Register<ColorPicker, bool>(nameof(Alpha), true);

    /// <summary>预设的颜色（品牌色、主题色）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> PresetsProperty = AvaloniaProperty.Register<ColorPicker, IReadOnlyList<string>?>(nameof(Presets));

    /// <summary>显示最近使用的颜色，默认 true（TS 的 recent）。</summary>
    public static readonly StyledProperty<bool> RecentProperty = AvaloniaProperty.Register<ColorPicker, bool>(nameof(Recent), true);

    /// <summary>输入框一开始的写法，默认 hex。</summary>
    public static readonly StyledProperty<ColorFormat> FormatProperty = AvaloniaProperty.Register<ColorPicker, ColorFormat>(nameof(Format));

    /// <summary>最近使用最多几个。</summary>
    const int RecentLimit = 8;

    /// <summary>同一个应用里的取色器共用的最近使用（最新的在前）。</summary>
    static List<string> recentColors = [];

    static event Action? RecentChanged;

    readonly Flex root = new() { Name = ColorPickerParts.Root, Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
    readonly ColorArea area;
    readonly Slider hue = new() { Name = ColorPickerParts.Hue, Size = TextSize.Sm, Min = 0, Max = 360, Step = 1, TrackEm = 0.75 };
    readonly Slider alpha = new() { Name = ColorPickerParts.Alpha, Size = TextSize.Sm, Min = 0, Max = 100, Step = 1, TrackEm = 0.75 };
    readonly SwatchView preview = new() { Name = ColorPickerParts.Preview };
    readonly ToggleGroup format = new() { Name = ColorPickerParts.Format, Size = TextSize.Xs, IsRequired = true };
    readonly TextField input = new() { Name = ColorPickerParts.Input, Size = TextSize.Sm };
    readonly StackPanel presets = new();
    readonly StackPanel recent = new();
    Hsva hsva;
    // 自己最后写出去的值：父级的 Value 等于它时不重算 HSV（拖到灰色时色相不丢）
    string emitted;
    bool syncing;
    bool typing;

    /// <summary>新建：色板、滑块与预览、格式与输入、预设与最近使用。</summary>
    public ColorPicker()
    {
        emitted = Value;
        hsva = ColorMath.ToHsva(Value) ?? new Hsva(0, 0, 0);
        area = new ColorArea(this) { Name = ColorPickerParts.Area };
        hue.ValueText = value => $"{value}°";
        alpha.ValueText = value => $"{value}%";
        hue.PaintTrack = (context, track) => context.DrawRectangle(HueGradient, null, track);
        alpha.PaintTrack = PaintAlphaTrack;
        hue.PropertyChanged += (_, e) =>
        {
            if (!syncing && e.Property == Slider.ValueProperty) Set(hsva with { H = hue.Value });
        };
        alpha.PropertyChanged += (_, e) =>
        {
            if (!syncing && e.Property == Slider.ValueProperty) Set(hsva with { A = alpha.Value / 100 });
        };
        format.Children.AddRange([new ToggleItem { Value = "hex", Content = "HEX" }, new ToggleItem { Value = "rgb", Content = "RGB" }, new ToggleItem { Value = "hsl", Content = "HSL" }]);
        format.PropertyChanged += (_, e) =>
        {
            if (!syncing && e.Property == ToggleGroup.ValueProperty && format.Value is { } next) SetCurrentValue(FormatProperty, Enum.Parse<ColorFormat>(next, ignoreCase: true));
        };
        input.PropertyChanged += (_, e) =>
        {
            if (!syncing && e.Property == TextBox.TextProperty) typing = true;
        };
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) CommitDraft();
        };
        input.LostFocus += (_, _) => CommitDraft();
        Flex.SetGrow(input, true);
        var sliders = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch, Children = { hue, alpha } };
        Flex.SetGrow(sliders, true);
        root.Children.AddRange(
        [
            area,
            new Flex { Name = ColorPickerParts.Tools, Align = FlexAlign.Center, Gap = Space.Sm, Children = { sliders, preview } },
            new Flex { Name = ColorPickerParts.Fields, Align = FlexAlign.Center, Gap = Space.Sm, Children = { format, input } },
            presets,
            recent,
        ]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Sync();
    }

    /// <summary>颜色值。</summary>
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>有不透明度滑块。</summary>
    public bool Alpha { get => GetValue(AlphaProperty); set => SetValue(AlphaProperty, value); }

    /// <summary>预设的颜色。</summary>
    public IReadOnlyList<string>? Presets { get => GetValue(PresetsProperty); set => SetValue(PresetsProperty, value); }

    /// <summary>显示最近使用的颜色。</summary>
    public bool Recent { get => GetValue(RecentProperty); set => SetValue(RecentProperty, value); }

    /// <summary>输入框的写法。</summary>
    public ColorFormat Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>色板上的把手（测试用）。</summary>
    internal Control Thumb => area.ThumbPart;

    /// <summary>输入框（测试用）。</summary>
    internal TextField Input => input;

    /// <summary>最近使用的颜色（测试用）。</summary>
    internal static IReadOnlyList<string> RecentColors => recentColors;

    /// <summary>当前颜色的 #hex（关掉透明度时不带 alpha）。</summary>
    string Hex => ColorMath.HsvaToHex(Alpha ? hsva : hsva with { A = 1 });

    /// <summary>不透明的当前颜色。</summary>
    internal Color Opaque => ToColor(hsva with { A = 1 });

    internal Hsva Current => hsva;

    static Color ToColor(Hsva color) => Color.Parse(ColorMath.HsvaToHex(color));

    static readonly IBrush HueGradient = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse("#f00"), 0), new GradientStop(Color.Parse("#ff0"), 0.17), new GradientStop(Color.Parse("#0f0"), 0.33),
            new GradientStop(Color.Parse("#0ff"), 0.5), new GradientStop(Color.Parse("#00f"), 0.67), new GradientStop(Color.Parse("#f0f"), 0.83),
            new GradientStop(Color.Parse("#f00"), 1),
        },
    };

    /// <summary>不透明度的轨道：棋盘格上叠一层从透明到当前颜色的渐变。</summary>
    void PaintAlphaTrack(DrawingContext context, RoundedRect track)
    {
        using (context.PushClip(track))
        {
            var bounds = track.Rect;
            context.FillRectangle(this.Resource<IBrush>(ThemeKeys.Brush.Surface), bounds);
            var checker = this.Resource<IBrush>(ThemeKeys.Brush.Fill);
            var cell = TextElement.GetFontSize(alpha) / 4;
            for (var y = 0; y * cell < bounds.Height; y++)
                for (var x = y % 2; x * cell < bounds.Width; x += 2)
                    context.FillRectangle(checker, new Rect(bounds.X + x * cell, bounds.Y + y * cell, cell, cell));
            var opaque = Opaque;
            context.FillRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb(0, opaque.R, opaque.G, opaque.B), 0), new GradientStop(opaque, 1) },
            }, bounds);
        }
    }

    /// <summary>记下一个颜色（最新的在前，最多 8 个）：同一个应用里的取色器都跟着更新。</summary>
    static void Remember(string color)
    {
        recentColors = [color, .. recentColors.Where(item => item != color).Take(RecentLimit - 1)];
        RecentChanged?.Invoke();
    }

    internal void RememberCurrent() => Remember(Hex);

    /// <summary>改成新的 HSV 并写回值。</summary>
    internal void Set(Hsva next)
    {
        hsva = next;
        emitted = Hex;
        SetCurrentValue(ValueProperty, emitted);
        Sync();
    }

    void CommitDraft()
    {
        typing = false;
        if (ColorMath.ToHex(input.Text ?? "") is { } parsed && ColorMath.ToHsva(parsed) is { } next)
        {
            Set(next);
            RememberCurrent();
        }
        Sync();
    }

    void Pick(string color)
    {
        if (ColorMath.ToHsva(color) is not { } next) return;
        Set(next);
        RememberCurrent();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            if (Value == emitted) return;
            if (ColorMath.ToHsva(Value) is { } parsed) hsva = parsed;
            emitted = Value;
            Sync();
        }
        else if (change.Property == AlphaProperty || change.Property == PresetsProperty || change.Property == RecentProperty || change.Property == FormatProperty
                 || change.Property == IsEnabledProperty || change.Property == Provider.LangProperty) Sync();
        else if (change.Property == TextElement.FontSizeProperty) Metrics();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RecentChanged += Sync;
        Metrics();
        Sync();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        RecentChanged -= Sync;
    }

    /// <summary>HSV → 色板、滑块、预览、输入框（正在输入时不打断）、预设与最近使用。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        syncing = true;
        area.InvalidateVisual();
        area.InvalidateArrange();
        area.UpdatePeer();
        hue.Value = Math.Round(hsva.H);
        alpha.Value = Math.Round(hsva.A * 100);
        alpha.IsVisible = Alpha;
        alpha.InvalidateVisual();
        AutomationProperties.SetName(hue, lang.ColorHue);
        AutomationProperties.SetName(alpha, lang.ColorAlpha);
        preview.Fill = new SolidColorBrush(Color.Parse(Hex));
        format.Value = Format.ToString().ToLowerInvariant();
        AutomationProperties.SetName(format, lang.ColorFormat);
        AutomationProperties.SetName(input, lang.ColorFormat);
        if (!typing) input.Text = ColorMath.FormatColor(Hex, Format);
        SwatchRow(presets, lang.ColorPresets, Presets ?? []);
        SwatchRow(recent, lang.ColorRecent, Recent ? recentColors : []);
        syncing = false;
    }

    /// <summary>一排颜色（预设、最近使用）：上面是 xs、textMuted 的标题，下面是可换行的色块（隔 xs）。</summary>
    void SwatchRow(StackPanel row, string title, IReadOnlyList<string> colors)
    {
        row.IsVisible = colors.Count > 0;
        AutomationProperties.SetName(row, title);
        var current = Hex;
        var swatches = new WrapPanel { Name = ColorPickerParts.Swatches };
        foreach (var color in colors)
        {
            var swatch = new SwatchButton { Name = ColorPickerParts.Swatch, Fill = ColorMath.ToHex(color) is { } hex ? new SolidColorBrush(Color.Parse(hex)) : null, IsPressed = ColorMath.ToHex(color) == current };
            AutomationProperties.SetName(swatch, color);
            swatch.AddHandler(Button.ClickEvent, (_, _) => Pick(color));
            swatches.Children.Add(swatch);
        }
        var label = new TextBlock { Name = ColorPickerParts.Label, Text = title };
        AutomationProperties.SetAccessibilityView(label, AccessibilityView.Raw);
        row.Children.Clear();
        row.Children.AddRange([label, swatches]);
        Metrics();
    }

    /// <summary>em 尺寸：宽 16rem（不超出可用宽度）；预览 2em、md 圆角；色块 1.5em、sm 圆角、隔 xs；标题 xs、textMuted。</summary>
    void Metrics()
    {
        var em = TextElement.GetFontSize(this);
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * em;
        root.Width = 16 * 16;
        preview.Width = preview.Height = 2 * em;
        foreach (var row in new[] { presets, recent })
            foreach (var child in row.Children)
                switch (child)
                {
                    case TextBlock label:
                        label.FontSize = this.Resource<double>(ThemeKeys.FontSize.Xs);
                        label.Foreground = this.Resource<IBrush>(ThemeKeys.Brush.TextMuted);
                        break;
                    case WrapPanel swatches:
                        swatches.ItemSpacing = swatches.LineSpacing = xs;
                        foreach (var swatch in swatches.Children.OfType<SwatchButton>()) swatch.Width = swatch.Height = 1.5 * em;
                        break;
                }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.MaxWidth = availableSize.Width;
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(root.DesiredSize.WithHeight(finalSize.Height)));
        return finalSize;
    }

    /// <summary>
    /// 饱和度 / 明度的色板：宽高 4:3、md 圆角，左白右纯色、上面压黑；把手 1em 的圆，白色 2px 描边、内外各一圈 30% 黑，填当前不透明的颜色。
    /// 按下即跳过去并接着拖（松开时记进最近使用）；把手可聚焦，方向键改饱和度、明度。
    /// </summary>
    sealed class ColorArea : Control
    {
        readonly ColorPicker owner;
        readonly AreaThumb thumb;
        bool dragging;

        public ColorArea(ColorPicker owner)
        {
            this.owner = owner;
            thumb = new AreaThumb(owner) { Name = ColorPickerParts.Thumb };
            VisualChildren.Add(thumb);
            LogicalChildren.Add(thumb);
            Cursor = new Cursor(StandardCursorType.Cross);
        }

        public Control ThumbPart => thumb;

        public void UpdatePeer() => thumb.UpdateName();

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? 16 * 16 : availableSize.Width;
            thumb.Measure(Size.Infinity);
            return new Size(width, width * 3 / 4);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = TextElement.GetFontSize(owner);
            var hsva = owner.Current;
            thumb.Arrange(new Rect(hsva.S * finalSize.Width - size / 2, (1 - hsva.V) * finalSize.Height - size / 2, size, size));
            return finalSize;
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            var radius = owner.Resource<double>(ThemeKeys.Radius.Md) * TextElement.GetFontSize(owner);
            var pure = ToColor(new Hsva(owner.Current.H, 1, 1));
            using (context.PushClip(new RoundedRect(bounds, radius)))
            {
                context.FillRectangle(new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(pure, 1) },
                }, bounds);
                context.FillRectangle(new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Color.FromArgb(0, 0, 0, 0), 1) },
                }, bounds);
            }
        }

        void PickAt(Point point) => owner.Set(owner.Current with
        {
            S = Math.Clamp(point.X / Math.Max(1, Bounds.Width), 0, 1),
            V = Math.Clamp(1 - point.Y / Math.Max(1, Bounds.Height), 0, 1),
        });

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!owner.IsEffectivelyEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            dragging = true;
            e.Pointer.Capture(this);
            PickAt(e.GetPosition(this));
            thumb.Focus(NavigationMethod.Pointer);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (dragging) PickAt(e.GetPosition(this));
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (!dragging) return;
            dragging = false;
            e.Pointer.Capture(null);
            owner.RememberCurrent();
        }
    }

    /// <summary>色板上的把手：读屏是滑块（值是饱和度，值文字念饱和度与明度）；方向键改饱和度、明度（Shift 一次 10%）。</summary>
    sealed class AreaThumb : Control
    {
        readonly ColorPicker owner;

        public AreaThumb(ColorPicker owner)
        {
            this.owner = owner;
            Focusable = true;
            FocusAdorner = null;
        }

        public void UpdateName()
        {
            var lang = Provider.GetLang(owner);
            var hsva = owner.Current;
            AutomationProperties.SetName(this, lang.ColorArea);
            AutomationProperties.SetItemStatus(this, lang.ColorAreaValue(Percent(hsva.S), Percent(hsva.V)));
            IsTabStop = owner.IsEffectivelyEnabled;
            InvalidateVisual();
        }

        static double Percent(double value) => Math.Round(value * 100);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!owner.IsEffectivelyEnabled) return;
            var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0.1 : 0.01;
            var hsva = owner.Current;
            (double S, double V)? move = e.Key switch
            {
                Key.Left => (hsva.S - step, hsva.V),
                Key.Right => (hsva.S + step, hsva.V),
                Key.Up => (hsva.S, hsva.V + step),
                Key.Down => (hsva.S, hsva.V - step),
                _ => null,
            };
            if (move is not { } next) return;
            owner.Set(hsva with { S = Math.Clamp(next.S, 0, 1), V = Math.Clamp(next.V, 0, 1) });
            e.Handled = true;
        }

        // 只认改颜色的键：Tab 经过、打开取色器的那次 Enter 松开时不该把没改过的颜色记进最近使用
        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End) owner.RememberCurrent();
        }

        // 焦点环只在键盘聚焦时画（同 Slider 的滑块）
        bool focusVisible;

        protected override void OnGotFocus(FocusChangedEventArgs e)
        {
            base.OnGotFocus(e);
            focusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
            InvalidateVisual();
        }

        protected override void OnLostFocus(FocusChangedEventArgs e)
        {
            base.OnLostFocus(e);
            focusVisible = false;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            var center = bounds.Center;
            var radius = bounds.Width / 2;
            var shade = new SolidColorBrush(Color.FromArgb(77, 0, 0, 0));
            var border = owner.Resource<double>(ThemeKeys.BorderWidth.Sm);
            context.DrawEllipse(new SolidColorBrush(owner.Opaque), null, center, radius, radius);
            // 白色描边，内外各一圈 30% 黑：浅色、深色上都看得见
            context.DrawEllipse(null, new Pen(Brushes.White, border), center, radius - border / 2, radius - border / 2);
            context.DrawEllipse(null, new Pen(shade, 1), center, radius + 0.5, radius + 0.5);
            context.DrawEllipse(null, new Pen(shade, 1), center, radius - border - 0.5, radius - border - 0.5);
            if (focusVisible)
                context.DrawEllipse(null, new Pen(owner.Resource<IBrush>(ThemeKeys.Brush.FocusRing), 2), center, radius + 3, radius + 3);
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new ThumbPeer(this);

        sealed class ThumbPeer(AreaThumb owner) : ControlAutomationPeer(owner), IRangeValueProvider
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

            protected override bool IsContentElementCore() => true;

            protected override bool IsControlElementCore() => true;

            public bool IsReadOnly => !owner.owner.IsEffectivelyEnabled;

            public double Minimum => 0;

            public double Maximum => 100;

            public double Value => Percent(owner.owner.Current.S);

            public double SmallChange => 1;

            public double LargeChange => 10;

            public void SetValue(double value) => owner.owner.Set(owner.owner.Current with { S = Math.Clamp(value / 100, 0, 1) });
        }
    }
}

/// <summary>
/// 一格可以点的颜色（预设、最近使用、ColorField 打开取色器的色块）：<see cref="SwatchView"/> 画颜色，悬停时略微放大；读屏是按钮，选中的念"已按下"。
/// </summary>
internal sealed class SwatchButton : Control
{
    readonly SwatchView view = new() { RadiusKey = ThemeKeys.Radius.Sm };

    public SwatchButton()
    {
        Focusable = true;
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Hand);
        RenderTransformOrigin = RelativePoint.Center;
        VisualChildren.Add(view);
        LogicalChildren.Add(view);
    }

    /// <summary>颜色。</summary>
    public IBrush? Fill { get => view.Fill; set => view.Fill = value; }

    /// <summary>是当前的颜色（读屏的"已按下"）。</summary>
    public bool IsPressed { get; set; }

    /// <summary>点了：发 Button.ClickEvent（Popover 认它，色块可以直接当弹出层的触发元素）。</summary>
    internal void Invoke()
    {
        if (IsEffectivelyEnabled) RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        view.Measure(availableSize);
        return new Size(double.IsNaN(Width) ? 0 : Width, double.IsNaN(Height) ? 0 : Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        view.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        RenderTransform = IsEffectivelyEnabled ? new ScaleTransform(1.1, 1.1) : null;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        RenderTransform = null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this))) Invoke();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is not (Key.Enter or Key.Space)) return;
        Invoke();
        e.Handled = true;
    }

    // 焦点环只在键盘聚焦时画（同 Slider 的滑块）
    bool focusVisible;

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        focusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        focusVisible = false;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!focusVisible) return;
        var radius = this.Resource<double>(ThemeKeys.Radius.Sm) * global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);
        context.DrawRectangle(null, new Pen(this.Resource<IBrush>(ThemeKeys.Brush.FocusRing), 2), new RoundedRect(new Rect(Bounds.Size).Inflate(3), radius + 3));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SwatchPeer(this);

    sealed class SwatchPeer(SwatchButton owner) : ControlAutomationPeer(owner), IInvokeProvider, IToggleProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => owner.Invoke();

        public ToggleState ToggleState => owner.IsPressed ? ToggleState.On : ToggleState.Off;

        public void Toggle() => owner.Invoke();
    }
}

/// <summary>ColorPicker 的部件名（TS 的 data-scope="color-picker"）。</summary>
public static class ColorPickerParts
{
    /// <summary>外层（Flex，竖排）。</summary>
    public const string Root = "PART_Root";

    /// <summary>饱和度 / 明度的色板（TS 的 propsArea）。</summary>
    public const string Area = "PART_Area";

    /// <summary>色板上的把手（TS 的 propsThumb）。</summary>
    public const string Thumb = "PART_Thumb";

    /// <summary>滑块与预览那一行。</summary>
    public const string Tools = "PART_Tools";

    /// <summary>色相滑块（TS 的 propsHue）。</summary>
    public const string Hue = "PART_Hue";

    /// <summary>不透明度滑块（TS 的 propsAlpha）。</summary>
    public const string Alpha = "PART_Alpha";

    /// <summary>当前颜色的预览块（TS 的 propsPreview）。</summary>
    public const string Preview = "PART_Preview";

    /// <summary>格式与输入那一行。</summary>
    public const string Fields = "PART_Fields";

    /// <summary>写法切换（TS 的 propsFormat）。</summary>
    public const string Format = "PART_Format";

    /// <summary>颜色输入框（TS 的 propsInput）。</summary>
    public const string Input = "PART_Input";

    /// <summary>一排颜色（TS 的 propsSwatches）。</summary>
    public const string Swatches = "PART_Swatches";

    /// <summary>每一格颜色（TS 的 propsSwatch）。</summary>
    public const string Swatch = "PART_Swatch";

    /// <summary>一排颜色的标题（预设、最近使用）。</summary>
    public const string Label = "PART_Label";
}
