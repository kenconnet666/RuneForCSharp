using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 悬停卡片（vue-tsx-pro 的 HoverCard）：指针停在触发器（通常是链接、用户名）上、或用键盘聚焦它时，弹出一张富内容卡片（用户资料、链接预览）。
/// <code>
/// var card = new HoverCardControl { Trigger = new LinkControl { Text = "@ada", Href = new Uri("https://example.com/u/ada") }, Content = profile };
/// </code>
/// 开关时机：指针停留 <see cref="Delay"/>（默认 700 毫秒）后打开，离开 <see cref="CloseDelay"/>（默认 300 毫秒）后关闭，中间可以移到卡片上；
/// 所有悬停卡片一组，同一时间只开一个，刚关掉一个时下一个直接打开。键盘聚焦触发器时立即打开，焦点离开、Esc、按下触发器时关闭。
/// 定位、外观、箭头同 <see cref="PopoverControl"/>（默认在下方居中），最宽 22em。
/// 出入：卡片窗口不激活（焦点留在触发器上，不抢宿主窗体的激活），键盘不能 Tab 进卡片；子控件吞掉鼠标进出事件，指针在哪儿每 50 毫秒看一次。
/// 卡片只是预览：里面的信息要在别处也能拿到（触屏没有悬停）。
/// </summary>
[ToolboxItem(true)]
[Description("悬停卡片：悬停或键盘聚焦时在触发器旁边显示富内容。")]
public class HoverCardControl : PopoverControl
{
    /// <summary>所有悬停卡片一组：同一时间只开一个。</summary>
    static readonly HoverGroup Cards = new();

    readonly HoverIntent intent;
    readonly System.Windows.Forms.Timer poll = new() { Interval = 50 };
    Spot spot;

    /// <summary>指针在哪儿。</summary>
    enum Spot
    {
        None,
        Trigger,
        Panel,
    }

    /// <summary>新建：悬停、键盘聚焦打开；卡片窗口不激活。</summary>
    public HoverCardControl()
    {
        Placement = FloatingPlacement.Bottom;
        intent = new HoverIntent(Cards, () => (Delay, CloseDelay, Disabled), (open, reason) => SetOpen(open, reason));
        FloatingHost.Form.Activates = false;
        poll.Tick += (_, _) => Track();
    }

    /// <summary>指针停留多久后打开，默认 700 毫秒。</summary>
    [Category("Behavior")]
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(700);

    /// <summary>指针离开多久后关闭，默认 300 毫秒。</summary>
    [Category("Behavior")]
    public TimeSpan CloseDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>不打开（触发器照常显示）。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Disabled { get; set; }

    bool ShouldSerializeDelay() => Delay != TimeSpan.FromMilliseconds(700);

    void ResetDelay() => Delay = TimeSpan.FromMilliseconds(700);

    bool ShouldSerializeCloseDelay() => CloseDelay != TimeSpan.FromMilliseconds(300);

    void ResetCloseDelay() => CloseDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>悬停的时机（测试用）。</summary>
    internal HoverIntent Intent => intent;

    // 按下触发器已经关了（MouseDown）；链接照常跳转
    private protected override void OnTriggerClick()
    {
    }

    private protected override int? MaxPanelWidth(float em) => (int)MathF.Round(22 * em);

    /// <summary>指针在哪儿：触发器上、卡片上、外面；变了告诉悬停的时机。外面且卡片关着时不再看。</summary>
    void Track()
    {
        var cursor = Cursor.Position;
        var next = RectangleToScreen(ClientRectangle).Contains(cursor) ? Spot.Trigger
            : IsOpen && FloatingHost.Backdrop is { Visible: true } backdrop && backdrop.Panel.Contains(cursor) ? Spot.Panel
            : Spot.None;
        if (next != spot)
        {
            if (next == Spot.Trigger) intent.PointerEntered();
            else if (next == Spot.Panel) intent.PanelEntered();
            else intent.PointerExited();
            spot = next;
        }
        if (spot == Spot.None && !IsOpen) poll.Stop();
    }

    /// <summary>指针进了触发器（或它的子控件）：开始看指针在哪儿。</summary>
    void StartTracking()
    {
        if (!poll.Enabled) poll.Start();
        Track();
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        var trigger = e.Control!;
        trigger.MouseEnter += (_, _) => StartTracking();
        trigger.MouseDown += (_, _) => intent.CloseNow(OpenChangeReason.Trigger);
        trigger.GotFocus += (_, _) =>
        {
            // 只在键盘聚焦时打开：点击聚焦时鼠标键按着
            if (MouseButtons == MouseButtons.None) intent.KeyboardFocused();
        };
        trigger.LostFocus += (_, _) =>
        {
            if (!FloatingHost.Form.ContainsFocus) intent.CloseNow(OpenChangeReason.Focus);
        };
        trigger.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape || !IsOpen) return;
            intent.CloseNow(OpenChangeReason.Escape);
            e.Handled = true;
        };
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        StartTracking();
    }

    /// <summary>开、关时告诉悬停的时机：关掉同组的另一个、记下关闭的时间；开着时一直看指针。</summary>
    private protected override void OnOpenChanged(bool open)
    {
        if (open)
        {
            intent.Opened();
            if (!poll.Enabled) poll.Start();
        }
        else intent.Closed();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            poll.Dispose();
            intent.Dispose();
        }
        base.Dispose(disposing);
    }
}
