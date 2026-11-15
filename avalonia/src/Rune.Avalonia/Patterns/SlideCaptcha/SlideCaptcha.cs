using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>服务端给的一张拼图（vue-tsx-pro 的 SlideChallenge）。</summary>
/// <param name="Id">这一张的标识，换图时变；松手后连同结果一起交给服务端。</param>
/// <param name="Background">背景图（带缺口）。</param>
/// <param name="Piece">拼图图片：和背景一样高，左边缘对着缺口的左边缘。</param>
/// <param name="Width">背景图的宽（像素）。</param>
/// <param name="Height">背景图的高（像素）。</param>
/// <param name="PieceWidth">拼图图片的宽（像素）。</param>
public sealed record SlideChallenge(string Id, IImage Background, IImage Piece, int Width, int Height, int PieceWidth);

/// <summary>拖动轨迹上的一点：距按下的毫秒数，和当时的位置（像素）。</summary>
/// <param name="Time">距按下的毫秒数。</param>
/// <param name="X">位置（像素）。</param>
public readonly record struct SlidePoint(int Time, int X);

/// <summary>松手时交给服务端的内容（vue-tsx-pro 的 SlideResult）。</summary>
/// <param name="X">拼图左边缘在背景图里的位置（像素）。</param>
/// <param name="Track">拖动轨迹（真人拖动有加减速和抖动，服务端据此区分脚本）。</param>
/// <param name="Duration">从按下到松手的毫秒数。</param>
public sealed record SlideResult(int X, IReadOnlyList<SlidePoint> Track, int Duration);

/// <summary>验证码的状态（vue-tsx-pro 的 SlideStatus）。</summary>
public enum SlideStatus
{
    /// <summary>可以拖。</summary>
    Idle,

    /// <summary>等服务端判定：不能拖。</summary>
    Verifying,

    /// <summary>通过：锁定。</summary>
    Success,

    /// <summary>没对准：显示提示，父级设回 Idle（或换图）后重来。</summary>
    Error,
}

/// <summary>松手了（<see cref="SlideCaptcha.Released"/>）。</summary>
public sealed class SlideReleasedEventArgs(RoutedEvent routedEvent, SlideResult result) : RoutedEventArgs(routedEvent)
{
    /// <summary>位置、轨迹与时长。</summary>
    public SlideResult Result { get; } = result;
}

/// <summary>
/// 拼图滑块验证（vue-tsx-pro 的 SlideCaptcha）：画面是服务端生成的背景图（有一个缺口）和一块拼图，拖动下面的滑块让拼图对准缺口，
/// 松手后把位置和拖动轨迹交给服务端判定。组件只负责交互，不判对错：答案在服务端，每次给一张新图。
/// <code>&lt;rune:SlideCaptcha Challenge="{Binding Challenge}" Status="{Binding Status}" Released="OnReleased" RefreshRequested="OnRefresh" /&gt;</code>
/// <list type="bullet">
/// <item><see cref="Challenge"/>：背景与拼图是图片（服务端给的图解码成 Bitmap），拼图和背景一样高、左边缘就是缺口的左边缘，所以位置换算成像素直接就是答案；还没拿到时为 null，显示加载中。</item>
/// <item><see cref="Released"/> 交出 <see cref="SlideResult"/>：位置、轨迹（最多 240 点）与时长。</item>
/// <item><see cref="Status"/> 由父级控制：Verifying 时不能拖，Success 时锁定，Error 时图片底部与滑块条上是 <see cref="ErrorText"/>；换了新图或设回 Idle 时滑块回到起点。</item>
/// <item>键盘：聚焦滑块后 ←/→ 微调 2 像素、Home/End、Enter 或空格松手。这类验证对读屏和运动障碍的用户不友好，应用应当另外提供别的方式。</item>
/// </list>
/// 部件：<see cref="SlideCaptchaParts"/>。上游松手回到起点的回弹、没对准时滑块晃一下是过渡动画，这里直接到位。
/// </summary>
[PseudoClasses(":verifying", ":success", ":error", ":dragging")]
public class SlideCaptcha : Control
{
    /// <summary>当前的拼图；还没拿到时为 null。</summary>
    public static readonly StyledProperty<SlideChallenge?> ChallengeProperty = AvaloniaProperty.Register<SlideCaptcha, SlideChallenge?>(nameof(Challenge));

    /// <summary>状态，默认 Idle。</summary>
    public static readonly StyledProperty<SlideStatus> StatusProperty = AvaloniaProperty.Register<SlideCaptcha, SlideStatus>(nameof(Status));

    /// <summary>滑块条上的提示，默认"向右拖动滑块，让拼图对准缺口"。</summary>
    public static readonly StyledProperty<string?> HintProperty = AvaloniaProperty.Register<SlideCaptcha, string?>(nameof(Hint));

    /// <summary>没对准时的提示，默认"没对准，请再试一次"；服务端给了具体原因时用它。</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty = AvaloniaProperty.Register<SlideCaptcha, string?>(nameof(ErrorText));

    /// <summary>松手了：把结果发给服务端判定。</summary>
    public static readonly RoutedEvent<SlideReleasedEventArgs> ReleasedEvent = RoutedEvent.Register<SlideCaptcha, SlideReleasedEventArgs>(nameof(Released), RoutingStrategies.Bubble);

    /// <summary>点了"换一张"。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RefreshRequestedEvent = RoutedEvent.Register<SlideCaptcha, RoutedEventArgs>(nameof(RefreshRequested), RoutingStrategies.Bubble);

    /// <summary>键盘每次移动的距离（像素）。</summary>
    const int KeyStep = 2;

    /// <summary>轨迹最多记多少个点，太长的拖动只留一部分。</summary>
    const int MaxPoints = 240;

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
    readonly SlidePicture picture;
    readonly SlideTrack track;
    readonly LiveRegion live = new() { Height = 0 };
    readonly List<SlidePoint> points = [];
    readonly Stopwatch clock = new();
    double position;
    bool dragging;

    /// <summary>新建：画面（背景、拼图、换一张、结果条）与滑块条竖排。</summary>
    public SlideCaptcha()
    {
        picture = new SlidePicture(this);
        track = new SlideTrack(this);
        picture.Refresh.Click += (_, _) => RaiseEvent(new RoutedEventArgs(RefreshRequestedEvent));
        root.Children.AddRange([picture, track, live]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Sync();
    }

    /// <summary>当前的拼图。</summary>
    public SlideChallenge? Challenge { get => GetValue(ChallengeProperty); set => SetValue(ChallengeProperty, value); }

    /// <summary>状态。</summary>
    public SlideStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <summary>滑块条上的提示。</summary>
    public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }

    /// <summary>没对准时的提示。</summary>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <summary>松手了。</summary>
    public event EventHandler<SlideReleasedEventArgs>? Released
    {
        add => AddHandler(ReleasedEvent, value);
        remove => RemoveHandler(ReleasedEvent, value);
    }

    /// <summary>点了"换一张"。</summary>
    public event EventHandler<RoutedEventArgs>? RefreshRequested
    {
        add => AddHandler(RefreshRequestedEvent, value);
        remove => RemoveHandler(RefreshRequestedEvent, value);
    }

    /// <summary>滑块（测试用）。</summary>
    internal SlideHandle Handle => track.Handle;

    /// <summary>"换一张"按钮（测试用）。</summary>
    internal IconButton RefreshButton => picture.Refresh;

    /// <summary>拼图左边缘能走的距离（像素）：背景宽减去拼图宽。</summary>
    internal int Travel => Challenge is { } challenge ? challenge.Width - challenge.PieceWidth : 0;

    /// <summary>拼图左边缘在背景图里的位置（像素）。</summary>
    internal int Pixels => (int)Math.Round(position * Travel);

    /// <summary>位置占能走的距离的比例（0 到 1）。</summary>
    internal double Position => position;

    /// <summary>能拖：拿到了图、状态是 Idle。</summary>
    internal bool Interactive => Challenge is not null && Status == SlideStatus.Idle && IsEffectivelyEnabled;

    /// <summary>现在的提示：验证中、通过、没对准、或拖动提示。</summary>
    internal string Text
    {
        get
        {
            var messages = PatternsMessages.For(Provider.GetLang(this));
            return Status switch
            {
                SlideStatus.Verifying => messages.SlideVerifying,
                SlideStatus.Success => messages.SlideSuccess,
                SlideStatus.Error => ErrorText ?? messages.SlideError,
                _ => Hint ?? messages.SlideHint,
            };
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChallengeProperty)
        {
            // 换了新图：滑块回到起点
            Reset();
        }
        else if (change.Property == StatusProperty)
        {
            // 父级设回 Idle（重试）：滑块回到起点
            if (Status == SlideStatus.Idle && change.GetOldValue<SlideStatus>() != SlideStatus.Idle) Reset();
            else Sync();
            live.Announce(Status == SlideStatus.Idle ? "" : Text);
        }
        else if (change.Property == HintProperty || change.Property == ErrorTextProperty || change.Property == IsEnabledProperty || change.Property == Provider.LangProperty) Sync();
    }

    void Reset()
    {
        position = 0;
        points.Clear();
        Sync();
    }

    /// <summary>伪类、提示、滑块的状态，再按位置重排画面与滑块条。</summary>
    void Sync()
    {
        PseudoClasses.Set(":verifying", Status == SlideStatus.Verifying);
        PseudoClasses.Set(":success", Status == SlideStatus.Success);
        PseudoClasses.Set(":error", Status == SlideStatus.Error);
        PseudoClasses.Set(":dragging", dragging);
        picture.Sync();
        track.Sync();
    }

    /// <summary>记一个轨迹点（第一个点开始计时）。</summary>
    void Record()
    {
        if (points.Count == 0) clock.Restart();
        if (points.Count < MaxPoints) points.Add(new SlidePoint((int)clock.ElapsedMilliseconds, Pixels));
    }

    /// <summary>松手：没动过就不交（键盘直接按 Enter）。</summary>
    internal void Release()
    {
        if (points.Count == 0) return;
        var result = new SlideResult(Pixels, [.. points], (int)clock.ElapsedMilliseconds);
        points.Clear();
        RaiseEvent(new SlideReleasedEventArgs(ReleasedEvent, result));
    }

    /// <summary>移到某个比例（键盘、拖动）并记下轨迹。</summary>
    internal void MoveTo(double next)
    {
        position = Math.Clamp(next, 0, 1);
        Record();
        Sync();
    }

    /// <summary>键盘：←/→ 微调（从右往左时反过来）、Home / End 到两端、Enter 或空格松手。</summary>
    internal bool HandleKey(Key key)
    {
        if (!Interactive) return false;
        if (key is Key.Enter or Key.Space)
        {
            Release();
            return true;
        }
        var step = KeyStep / (double)Math.Max(Travel, 1);
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        double? next = key switch
        {
            Key.Right => position + (rtl ? -step : step),
            Key.Left => position + (rtl ? step : -step),
            Key.Home => 0,
            Key.End => 1,
            _ => null,
        };
        if (next is not { } target) return false;
        MoveTo(target);
        return true;
    }

    /// <summary>开始拖动（指针按下）：轨迹从这里记。</summary>
    internal void BeginDrag()
    {
        points.Clear();
        dragging = true;
        Record();
        Sync();
    }

    /// <summary>结束拖动：松手交出结果；取消时回到起点。</summary>
    internal void EndDrag(bool cancelled)
    {
        dragging = false;
        if (cancelled) Reset();
        else
        {
            Release();
            Sync();
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>画面：按拼图的宽高比，背景铺满，拼图按位置放（宽按比例），右上角"换一张"，通过、没对准时底部一条结果。</summary>
sealed class SlidePicture : Border
{
    readonly SlideCaptcha owner;
    readonly global::Avalonia.Controls.Image background = new() { Stretch = Stretch.Fill };
    readonly global::Avalonia.Controls.Image piece = new() { Stretch = Stretch.Fill };
    readonly Flex loading = new() { Align = FlexAlign.Center, Justify = FlexJustify.Center, Gap = Space.Sm };
    readonly Text loadingText = new();
    readonly Border banner = new() { Name = SlideCaptchaParts.Banner, IsVisible = false };
    readonly Text bannerText = new() { TextAlignment = TextAlignment.Center };
    readonly Canvas layer = new();

    public SlidePicture(SlideCaptcha owner)
    {
        this.owner = owner;
        Name = SlideCaptchaParts.Picture;
        ClipToBounds = true;
        // 拼图的阴影：拖到哪都看得清边
        piece.Effect = new DropShadowEffect { OffsetX = 0, OffsetY = 2, BlurRadius = 6, Color = Color.FromArgb(115, 0, 0, 0) };
        loading.Children.AddRange([new Spinner(), loadingText]);
        banner.Child = bannerText;
        layer.Children.AddRange([background, piece, loading, banner, Refresh]);
        Child = layer;
    }

    /// <summary>"换一张"：ghost、中性、sm，贴着右上角。</summary>
    public IconButton Refresh { get; } = new() { Name = SlideCaptchaParts.Refresh, Icon = Lucide.RefreshCw, Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };

    public void Sync()
    {
        var messages = PatternsMessages.For(Provider.GetLang(owner));
        var challenge = owner.Challenge;
        background.Source = challenge?.Background;
        piece.Source = challenge?.Piece;
        background.IsVisible = piece.IsVisible = challenge is not null;
        loading.IsVisible = challenge is null;
        loadingText.Text = messages.SlideLoading;
        AutomationProperties.SetName(Refresh, messages.SlideRefresh);
        Refresh.IsEnabled = owner.Status != SlideStatus.Verifying;
        banner.IsVisible = owner.Status is SlideStatus.Success or SlideStatus.Error;
        banner.Classes.Set("success", owner.Status == SlideStatus.Success);
        banner.Classes.Set("error", owner.Status == SlideStatus.Error);
        bannerText.Text = owner.Text;
        // Border 没有字号：md 圆角按验证码的字号算
        CornerRadius = new CornerRadius(owner.Resource<double>(ThemeKeys.Radius.Md) * global::Avalonia.Controls.Documents.TextElement.GetFontSize(owner));
        InvalidateMeasure();
        InvalidateArrange();
    }

    double Aspect => owner.Challenge is { } challenge ? challenge.Width / (double)challenge.Height : 2;

    /// <summary>宽占满，高按图的宽高比（上游的 aspect-ratio）。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        base.MeasureOverride(new Size(width, width / Aspect));
        return new Size(width, width / Aspect);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);
        var inner = finalSize.Deflate(BorderThickness);
        background.Width = loading.Width = inner.Width;
        background.Height = loading.Height = inner.Height;
        if (owner.Challenge is { } challenge)
        {
            var scale = inner.Width / challenge.Width;
            piece.Width = challenge.PieceWidth * scale;
            piece.Height = inner.Height;
            Canvas.SetLeft(piece, owner.Position * owner.Travel * scale);
        }
        banner.Width = inner.Width;
        Canvas.SetTop(banner, inner.Height - banner.DesiredSize.Height);
        var gap = this.Resource<double>(ThemeKeys.Space.Xs) * global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);
        Canvas.SetTop(Refresh, gap);
        Canvas.SetLeft(Refresh, inner.Width - gap - Refresh.DesiredSize.Width);
        layer.Arrange(new Rect(new Point(BorderThickness.Left, BorderThickness.Top), inner));
        return finalSize;
    }
}

/// <summary>滑块条：2.75rem 高，滑过的一段（accentSoft；通过 successSoft、没对准 dangerSoft）从左边起，中间是提示，滑块按位置走。</summary>
sealed class SlideTrack : Border
{
    /// <summary>滑块的宽度，也是滑块条的高度（2.75rem）。</summary>
    internal const double HandleSize = 44;

    readonly SlideCaptcha owner;
    readonly Border fill = new() { Name = SlideCaptchaParts.Fill };
    readonly Text hint = new() { Name = SlideCaptchaParts.Hint, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Canvas layer = new();

    public SlideTrack(SlideCaptcha owner)
    {
        this.owner = owner;
        Name = SlideCaptchaParts.Track;
        Height = HandleSize;
        ClipToBounds = true;
        Handle = new SlideHandle(owner) { Name = SlideCaptchaParts.Handle, Width = HandleSize };
        AutomationProperties.SetAccessibilityView(hint, AccessibilityView.Raw);
        layer.Children.AddRange([fill, hint, Handle]);
        Child = layer;
    }

    public SlideHandle Handle { get; }

    public void Sync()
    {
        fill.Classes.Set("success", owner.Status == SlideStatus.Success);
        fill.Classes.Set("error", owner.Status == SlideStatus.Error);
        // 拖动中不显示提示（滑过的一段盖着它）；验证中、结果出来后显示状态
        hint.Text = owner.Position == 0 || owner.Status != SlideStatus.Idle ? owner.Text : null;
        CornerRadius = Handle.CornerRadius = new CornerRadius(owner.Resource<double>(ThemeKeys.Radius.Md) * global::Avalonia.Controls.Documents.TextElement.GetFontSize(owner));
        Handle.Sync();
        InvalidateArrange();
    }

    /// <summary>滑块能走的距离：条宽减去滑块宽。</summary>
    internal double Room => Math.Max(1, Bounds.Width - BorderThickness.Left - BorderThickness.Right - HandleSize);

    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);
        var inner = finalSize.Deflate(BorderThickness);
        var room = Math.Max(0, inner.Width - HandleSize);
        var at = owner.Position * room;
        fill.Width = at + HandleSize;
        fill.Height = inner.Height;
        Handle.Height = inner.Height;
        Canvas.SetLeft(Handle, at);
        // 提示在滑块两侧各让出一个滑块宽的区域里居中
        hint.Width = Math.Max(0, inner.Width - 2 * HandleSize);
        Canvas.SetLeft(hint, HandleSize);
        Canvas.SetTop(hint, (inner.Height - hint.DesiredSize.Height) / 2);
        layer.Arrange(new Rect(new Point(BorderThickness.Left, BorderThickness.Top), inner));
        return finalSize;
    }
}

/// <summary>
/// 滑块（读屏里是滑杆：值是拼图左边缘的像素位置，0 到能走的距离）：surface 底、sm 阴影（拖动时 md）、md 圆角，图标是 ChevronsRight（通过 Check、没对准 X）。
/// 伪类 :locked（不能拖：没图、验证中、通过、没对准）。
/// </summary>
[PseudoClasses(":locked")]
public sealed class SlideHandle : Border
{
    readonly SlideCaptcha owner;
    readonly Icon icon = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    double startX;
    double startPosition;
    bool pressed;

    internal SlideHandle(SlideCaptcha owner)
    {
        this.owner = owner;
        Focusable = true;
        Child = icon;
    }

    internal void Sync()
    {
        icon.Data = owner.Status switch
        {
            SlideStatus.Success => Lucide.Check,
            SlideStatus.Error => Lucide.X,
            _ => Lucide.ChevronsRight,
        };
        PseudoClasses.Set(":locked", !owner.Interactive);
        IsTabStop = owner.Interactive;
        AutomationProperties.SetName(this, PatternsMessages.For(Provider.GetLang(owner)).SlideLabel);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && owner.HandleKey(e.Key)) e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!owner.Interactive || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(this);
        startX = e.GetPosition(owner).X;
        startPosition = owner.Position;
        pressed = true;
        owner.BeginDrag();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!pressed || Parent?.Parent is not SlideTrack track) return;
        owner.MoveTo(startPosition + (e.GetPosition(owner).X - startX) / track.Room);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!pressed) return;
        pressed = false;
        e.Pointer.Capture(null);
        owner.EndDrag(cancelled: false);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!pressed) return;
        pressed = false;
        owner.EndDrag(cancelled: true);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new HandlePeer(this);

    sealed class HandlePeer(SlideHandle owner) : ControlAutomationPeer(owner), IRangeValueProvider
    {
        SlideCaptcha Captcha => owner.owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override bool IsEnabledCore() => Captcha.Interactive;

        public bool IsReadOnly => !Captcha.Interactive;

        public double Minimum => 0;

        public double Maximum => Captcha.Travel;

        public double Value => Captcha.Pixels;

        public double LargeChange => Math.Max(1, Captcha.Travel / 10.0);

        public double SmallChange => 2;

        public void SetValue(double value)
        {
            if (Captcha.Interactive) Captcha.MoveTo(value / Math.Max(Captcha.Travel, 1));
        }
    }
}

/// <summary>SlideCaptcha 的部件名。外层是验证码自己（画面与滑块条竖排，隔 sm）。</summary>
public static class SlideCaptchaParts
{
    /// <summary>画面：Border，md 圆角、border 描边、fill 底，按图的宽高比。</summary>
    public const string Picture = "PART_Picture";

    /// <summary>"换一张"（TS 的 propsRefresh）：IconButton，sm、ghost、中性，右上角离边 xs。</summary>
    public const string Refresh = "PART_Refresh";

    /// <summary>通过、没对准时图片底部的结果条：Border，类 success / error（实色 88% 不透明、对比色的字，上下 xs、左右 md、sm 字号）。</summary>
    public const string Banner = "PART_Banner";

    /// <summary>滑块条：Border，2.75rem 高、md 圆角、border 描边、fill 底。</summary>
    public const string Track = "PART_Track";

    /// <summary>滑过的一段：Border，accentSoft（类 success：successSoft，error：dangerSoft）。</summary>
    public const string Fill = "PART_Fill";

    /// <summary>滑块条上的提示：Text，sm、textMuted，读屏跳过（状态由实时区域播报）。</summary>
    public const string Hint = "PART_Hint";

    /// <summary>滑块：SlideHandle。</summary>
    public const string Handle = "PART_Handle";
}
